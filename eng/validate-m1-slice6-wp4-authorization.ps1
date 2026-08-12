[CmdletBinding()]
param(
    [string] $ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v1.json',
    [string] $OutputPath
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath,
        '-ManifestPath', $ManifestPath
    )
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $arguments += @('-OutputPath', $OutputPath)
    }

    & $pwsh.Source @arguments
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedManifestPath = if ([System.IO.Path]::IsPathRooted($ManifestPath)) {
    [System.IO.Path]::GetFullPath($ManifestPath)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ManifestPath))
}
$schemaPath = Join-Path $repoRoot 'contracts/repository/wp4-credential-native-authorization.v1.schema.json'

if (-not (Test-Json -LiteralPath $resolvedManifestPath -SchemaFile $schemaPath -ErrorAction Stop)) {
    throw 'WP4 CredentialNative authorization manifest failed structural JSON Schema validation.'
}

$manifestBytes = [System.IO.File]::ReadAllBytes($resolvedManifestPath)
$manifestText = [System.Text.Encoding]::UTF8.GetString($manifestBytes)
$manifest = $manifestText | ConvertFrom-Json -Depth 100 -DateKind String

$expectedWp3 = 'b32939e8b7491a5c47453f912d25dd98c090f103'
$expectedHandoff = 'fa38419b2c539524bbed01b7994f99ace491c293'
if ($manifest.candidate_binding.accepted_wp3_candidate_commit -ne $expectedWp3) {
    throw 'Manifest is not bound to the accepted WP3 candidate.'
}
if (($manifest.candidate_binding.authorization_handoff_commit -ne $expectedHandoff) -or
    ($manifest.candidate_binding.repository_head_at_preparation -ne $expectedHandoff)) {
    throw 'Manifest is not bound to the exact WP3 acceptance/handoff commit.'
}

$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$branch = (& git -C $repoRoot branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or $branch -ne 'codex/m1-s6') {
    throw 'Repository branch does not match the prepared authorization branch.'
}
& git -C $repoRoot merge-base --is-ancestor $expectedWp3 $expectedHandoff
if ($LASTEXITCODE -ne 0) {
    throw 'Accepted WP3 candidate is not an ancestor of the handoff commit.'
}
& git -C $repoRoot merge-base --is-ancestor $expectedHandoff $head
if ($LASTEXITCODE -ne 0) {
    throw 'Repository HEAD is not a descendant of the exact prepared authorization base.'
}

$preparedAt = [DateTimeOffset]::ParseExact(
    $manifest.prepared_at_utc,
    'yyyy-MM-ddTHH:mm:ss.fffffffZ',
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
$expiresAt = [DateTimeOffset]::ParseExact(
    $manifest.expires_at_utc,
    'yyyy-MM-ddTHH:mm:ss.fffffffZ',
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
if (($expiresAt - $preparedAt).TotalSeconds -ne $manifest.operation_limits.authorization_window_seconds) {
    throw 'Manifest authorization window does not equal the declared finite limit.'
}
if ($expiresAt -le [DateTimeOffset]::UtcNow) {
    throw 'Manifest has expired and cannot be presented for owner acceptance.'
}

$expectedCalls = @('CredWriteW', 'CredReadW', 'CredDeleteW', 'CredFree')
if (($manifest.native_boundary.allowed_calls -join '|') -ne ($expectedCalls -join '|')) {
    throw 'Allowed native-call set or order differs from the accepted WP4 boundary.'
}
$nativeTotal = $manifest.operation_limits.native_call_maxima.CredWriteW +
    $manifest.operation_limits.native_call_maxima.CredReadW +
    $manifest.operation_limits.native_call_maxima.CredDeleteW +
    $manifest.operation_limits.native_call_maxima.CredFree
if ($nativeTotal -ne $manifest.operation_limits.native_call_maxima.total) {
    throw 'Native-call maximum total does not equal the exact per-call maxima.'
}
if (($manifest.provider_boundary.dns_operations -ne 0) -or
    ($manifest.provider_boundary.network_operations -ne 0) -or
    ($manifest.provider_boundary.provider_operations -ne 0) -or
    ($manifest.provider_boundary.billable_operations -ne 0)) {
    throw 'Manifest permits a DNS, network, provider, or billable operation.'
}

$targetAliases = @($manifest.disposable_namespace.targets.alias)
$targetFingerprints = @($manifest.disposable_namespace.targets.target_fingerprint_sha256)
if ((($targetAliases | Sort-Object -Unique).Count -ne $targetAliases.Count) -or
    (($targetFingerprints | Sort-Object -Unique).Count -ne $targetFingerprints.Count)) {
    throw 'Disposable target aliases and fingerprints must be unique.'
}
foreach ($target in $manifest.disposable_namespace.targets) {
    $rawTarget = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $actualFingerprint = ([Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($rawTarget)))).ToLowerInvariant()
    if ($actualFingerprint -ne $target.target_fingerprint_sha256) {
        throw "Target fingerprint mismatch for alias '$($target.alias)'."
    }
    if ($manifestText.Contains($rawTarget, [StringComparison]::Ordinal)) {
        throw "Raw Credential Manager target is retained in the manifest for alias '$($target.alias)'."
    }
}

$expectedScenarios = @(
    'interactive-entry-submit',
    'interactive-entry-cancel',
    'credential-size-boundaries',
    'secure-store-unavailable',
    'replacement',
    'revoke-delete',
    'helper-and-coordinator-crash-restart',
    'backup-restore-reauthentication',
    'fake-provider-dispatch',
    'cleanup-failure-and-ambiguity'
)
if (($manifest.required_scenarios.id -join '|') -ne ($expectedScenarios -join '|')) {
    throw 'Required WP4 scenario inventory is incomplete, reordered, or widened.'
}
foreach ($scenario in $manifest.required_scenarios) {
    foreach ($targetAlias in $scenario.targets) {
        if ($targetAliases -notcontains $targetAlias) {
            throw "Scenario '$($scenario.id)' references undeclared target alias '$targetAlias'."
        }
    }
}

$planPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/plan.md'
$planText = [System.IO.File]::ReadAllText($planPath)
$requiredPlanPhrases = @(
    'one owner-authorized disposable namespace',
    '`CredWriteW`, exact-target `CredReadW`, exact-target',
    'helper-owned non-',
    'fake-provider dispatch only',
    'Cleanup uncertainty is visible and',
    'Fresh Windows credential/security reviewer'
)
foreach ($phrase in $requiredPlanPhrases) {
    if (-not $planText.Contains($phrase, [StringComparison]::Ordinal)) {
        throw "Accepted WP4 plan phrase is missing: $phrase"
    }
}

$currentStatePath = Join-Path $repoRoot 'docs/current-state.md'
$currentStateText = [System.IO.File]::ReadAllText($currentStatePath)
if ((-not $currentStateText.Contains('WP4 remains closed pending', [StringComparison]::Ordinal)) -or
    (-not $currentStateText.Contains($expectedWp3, [StringComparison]::Ordinal))) {
    throw 'Current-state no longer preserves the closed WP4 gate or accepted WP3 identity.'
}

$verifierPath = Join-Path $repoRoot 'eng/verify-m1-slice6.ps1'
$verifierText = [System.IO.File]::ReadAllText($verifierPath)
$credentialNativeImplemented = $verifierText.Contains("'CredentialNative'", [StringComparison]::Ordinal)

$manifestHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($manifestBytes)).ToLowerInvariant()
$receipt = [ordered]@{
    validation = 'M1/S6/WP4 authorization proposal'
    status = 'validated-ready-for-owner-acceptance'
    manifest_id = $manifest.manifest_id
    manifest_bytes = $manifestBytes.Length
    manifest_sha256 = $manifestHash
    accepted_wp3_candidate_commit = $expectedWp3
    authorization_handoff_commit = $expectedHandoff
    repository_head = $head
    branch = $branch
    expires_at_utc = $manifest.expires_at_utc
    target_count = $targetAliases.Count
    scenario_count = $expectedScenarios.Count
    allowed_native_calls = $expectedCalls
    native_call_maximum = $nativeTotal
    credential_native_gate_implemented = $credentialNativeImplemented
    execution_authorized = $false
    credential_manager_operations = 0
    dns_operations = 0
    network_operations = 0
    provider_operations = 0
    billable_operations = 0
}
$receiptJson = $receipt | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        [System.IO.Path]::GetFullPath($OutputPath)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
    }
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }
    [System.IO.File]::WriteAllText($resolvedOutputPath, $receiptJson + "`n", [Text.UTF8Encoding]::new($false))
}

$receiptJson
