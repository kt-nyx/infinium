[CmdletBinding()]
param(
    [string] $ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json',
    [string] $OutputPath
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    $forward = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-ManifestPath', $ManifestPath)
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) { $forward += @('-OutputPath', $OutputPath) }
    & $pwsh.Source @forward
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedManifest = if ([IO.Path]::IsPathRooted($ManifestPath)) {
    [IO.Path]::GetFullPath($ManifestPath)
} else { [IO.Path]::GetFullPath((Join-Path $repoRoot $ManifestPath)) }
$expectedManifest = [IO.Path]::GetFullPath((Join-Path $repoRoot `
    'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json'))
if (-not [string]::Equals($resolvedManifest, $expectedManifest, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WP4 v2 validator accepts only the exact tracked close-ready manifest path.'
}
$schema = Join-Path $repoRoot 'contracts/repository/wp4-credential-native-authorization.v2.schema.json'
if (-not (Test-Json -LiteralPath $resolvedManifest -SchemaFile $schema -ErrorAction Stop)) {
    throw 'WP4 v2 manifest failed structural JSON Schema validation.'
}

$bytes = [IO.File]::ReadAllBytes($resolvedManifest)
$text = [Text.Encoding]::UTF8.GetString($bytes)
$manifest = $text | ConvertFrom-Json -Depth 100 -DateKind String
$expectedManifestId = 'infinium.m1-s6.wp4.credential-native-authorization/e6e04651-4cd5-4f5d-8b46-5ec84a81cbbe'
$expectedWp3 = 'b32939e8b7491a5c47453f912d25dd98c090f103'
$expectedWp7Product = '59367a7479a7395b173b974bf720543aab2404d4'
$expectedWp7Evidence = '51251c0e0eb98d67dbc9b295b9ff084ebca33890'
$expectedHandoff = '44fbcc0542bef77f93c83f1422406a2b6012f0d5'
$expectedCorrection = '2f95692687b60d97db2710835e9d0966f131c164'
$expectedOldManifest = '9f43e5d9d7fb8b0cdba9195ba835631fa6073dff1c6ae86eb68a914b04c57db0'
$expectedOldEvidence = '18b4bd64d5ae32596330271e415b10a0a6d8516fded9dfc35bf1fee26dc7cd9f'
$expectedOldLock = '945d2bbf440af7d5a305ae4cbb4dee73636175ff679ac8582a28e84cd73e0e5d'
$historicalManifestBlob = (& git -C $repoRoot rev-parse `
    'f0ee9814f8bd0100692dfa7b7cab83ed9181457f:docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json').Trim()
if ($LASTEXITCODE -ne 0 -or $historicalManifestBlob -ne '2767ad896d2f46d83e988d90a2ccc7319c0ec788') {
    throw 'The consumed e3f76cd6 manifest history differs from its terminal exact-byte authority.'
}
function Assert-ExactArtifactHash([string] $RelativePath, [string] $ExpectedSha256) {
    $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $RelativePath))
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required immutable predecessor artifact is absent: $RelativePath"
    }
    $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [IO.File]::ReadAllBytes($path))).ToLowerInvariant()
    if ($actual -ne $ExpectedSha256) {
        throw "Required immutable predecessor artifact hash differs: $RelativePath"
    }
    return $path
}
$priorEvidencePath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-e3f76cd6/credential-native-primary-failure.v2.json' $expectedOldEvidence
$priorLockPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-authority-locks/cd9dd843fdd8e222a4b2d812ec0378d823d94d3308590ccce8aa280399276acf.json' $expectedOldLock
$recoveryEvidencePath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-8b7fc811/credential-native-recovery-evidence.v1.json' `
    '29fe8a1686564961a87d42018c77fa36260670d7b4d8aa976a5f212bb94f2329'
$recoveryLockPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-locks/87b696655c2696b8bc5c038ba785da5163f8b88c1f02f723665c3589a5051d5d.json' `
    '1ef3d2ecf4bb088eca9c5411cb7537519f64a0f659bd418358b48ea8ffda4e4b'
$recoveryReceiptPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-8b7fc811/credentialnativerecovery.json' `
    'f71b0668bc7c220d01272fa0a85406ce5ab99e75a59ccfbdf3e097fc352df908'
$priorEvidence = Get-Content -LiteralPath $priorEvidencePath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$priorLock = Get-Content -LiteralPath $priorLockPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryEvidence = Get-Content -LiteralPath $recoveryEvidencePath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryLock = Get-Content -LiteralPath $recoveryLockPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryReceipt = Get-Content -LiteralPath $recoveryReceiptPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
if ($priorEvidence.status -ne 'failed-primary-cleanup-confirmed' -or
    $priorEvidence.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-authorization/e3f76cd6-45c1-4e3a-a84b-fa3251b3cb60' -or
    $priorEvidence.manifest_sha256 -ne $expectedOldManifest -or
    -not [bool]$priorEvidence.namespace_blocked -or
    [int64]$priorEvidence.later_native_calls -ne 0 -or
    @($priorEvidence.absence_target_fingerprints).Count -ne 10 -or
    $priorLock.disposition -ne 'consumed-before-native-launch-never-delete-or-reuse' -or
    $priorLock.manifest_id -ne $priorEvidence.manifest_id -or
    $priorLock.manifest_sha256 -ne $expectedOldManifest) {
    throw 'The consumed e3f76cd6 failure evidence or authority lock is not terminal and exact.'
}
if ($recoveryEvidence.status -ne 'passed' -or
    $recoveryEvidence.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-recovery/8b7fc811-7cd2-4c2a-abe1-506bd7b06bf5' -or
    [bool]$recoveryEvidence.cleanup_ambiguity -or
    -not [bool]$recoveryEvidence.namespace_reuse_blocked -or
    @($recoveryEvidence.target_absence).Count -ne 2 -or
    [int64]$recoveryEvidence.prior_exact_absence_count -ne 10 -or
    [int64]$recoveryEvidence.combined_namespace_target_absence_count -ne 12 -or
    $recoveryLock.disposition -ne 'consumed-never-reuse' -or
    $recoveryLock.manifest_id -ne $recoveryEvidence.manifest_id -or
    $recoveryReceipt.status -ne 'passed' -or
    [int64]$recoveryReceipt.evidence.combined_namespace_target_absence_count -ne 12) {
    throw 'The e3f76cd6 cleanup recovery evidence, lock, or receipt is not terminal and exact.'
}
if ($manifest.schema_identity -ne 'infinium.repository.wp4-credential-native-authorization/1.3.0' -or
    $manifest.manifest_id -ne $expectedManifestId -or
    $manifest.effect_authority -ne 'none-until-owner-accepts-exact-manifest-bytes' -or
    $manifest.candidate_binding.accepted_wp3_candidate_commit -ne $expectedWp3 -or
    $manifest.candidate_binding.accepted_wp7_product_candidate_commit -ne $expectedWp7Product -or
    $manifest.candidate_binding.accepted_wp7_evidence_commit -ne $expectedWp7Evidence -or
    $manifest.candidate_binding.authorization_handoff_commit -ne $expectedHandoff -or
    $manifest.candidate_binding.sqlite_correction_candidate_commit -ne $expectedCorrection) {
    throw 'WP4 v2 manifest is not bound to the exact accepted WP3/WP7/handoff identities.'
}
if ($manifest.supersedes.manifest_sha256 -ne $expectedOldManifest -or
    $manifest.supersedes.native_evidence_sha256 -ne $expectedOldEvidence -or
    $manifest.supersedes.authority_lock_sha256 -ne $expectedOldLock -or
    $manifest.supersedes.namespace_disposition -ne 'terminal-cleanup-confirmed-absent-never-reuse' -or
    $manifest.supersedes.cleanup_recovery.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-recovery/8b7fc811-7cd2-4c2a-abe1-506bd7b06bf5' -or
    $manifest.supersedes.cleanup_recovery.manifest_sha256 -ne '6649b694ca235a8d0f4dcce9da6040f5c01a2ec22bdbcad7fcc7f9f6a4610cbb' -or
    $manifest.supersedes.cleanup_recovery.evidence_sha256 -ne '29fe8a1686564961a87d42018c77fa36260670d7b4d8aa976a5f212bb94f2329' -or
    $manifest.supersedes.cleanup_recovery.authority_lock_sha256 -ne '1ef3d2ecf4bb088eca9c5411cb7537519f64a0f659bd418358b48ea8ffda4e4b' -or
    $manifest.supersedes.cleanup_recovery.receipt_sha256 -ne 'f71b0668bc7c220d01272fa0a85406ce5ab99e75a59ccfbdf3e097fc352df908' -or
    [int64]$manifest.supersedes.cleanup_recovery.combined_namespace_target_absence_count -ne 12) {
    throw 'WP4 v2 manifest does not preserve the exact consumed predecessor terminal evidence.'
}

$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$branch = (& git -C $repoRoot branch --show-current).Trim()
if ($branch -ne 'codex/m1-s6') { throw 'WP4 v2 manifest requires branch codex/m1-s6.' }
foreach ($ancestor in @($expectedWp3, $expectedWp7Product, $expectedWp7Evidence, $expectedHandoff, $expectedCorrection)) {
    & git -C $repoRoot merge-base --is-ancestor $ancestor $head
    if ($LASTEXITCODE -ne 0) { throw "Required ancestor $ancestor is not retained." }
}
$currentState = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/current-state.md') -Raw
if (-not $currentState.Contains('fresh WP4 qualification-manifest consumer binding and owner-review preparation only', [StringComparison]::Ordinal) -or
    -not $currentState.Contains($expectedWp3, [StringComparison]::Ordinal) -or
    -not $currentState.Contains($expectedWp7Product, [StringComparison]::Ordinal) -or
    -not $currentState.Contains($expectedWp7Evidence, [StringComparison]::Ordinal)) {
    throw 'WP4 v2 proposal requires the current closed native gate and exact accepted WP3/WP7 identities.'
}
$closeReady = [string]$manifest.candidate_binding.close_ready_implementation_commit
$bindingPending = $closeReady -eq ('0' * 40)
if ($bindingPending -ne ($manifest.status -eq 'draft-close-ready-binding-pending')) {
    throw 'WP4 v2 status and close-ready implementation binding disagree.'
}
if (-not $bindingPending) {
    if ($manifest.status -ne 'ready-for-owner-acceptance') {
        throw 'WP4 v2 bound candidate is not marked ready for owner acceptance.'
    }
    & git -C $repoRoot merge-base --is-ancestor $closeReady $head
    if ($LASTEXITCODE -ne 0) { throw 'Close-ready implementation commit is not an ancestor of HEAD.' }
}

$prepared = [DateTimeOffset]::ParseExact($manifest.prepared_at_utc,
    'yyyy-MM-ddTHH:mm:ss.fffffffZ', [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
$expires = [DateTimeOffset]::ParseExact($manifest.expires_at_utc,
    'yyyy-MM-ddTHH:mm:ss.fffffffZ', [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
if (($expires - $prepared).TotalSeconds -ne [int64]$manifest.operation_limits.authorization_window_seconds -or
    [int64]$manifest.operation_limits.gate_wall_clock_seconds -ne
        [int64]$manifest.operation_limits.primary_phase_seconds +
        [int64]$manifest.operation_limits.cleanup_reserve_seconds +
        [int64]$manifest.operation_limits.evidence_reserve_seconds) {
    throw 'WP4 v2 finite authorization/deadline partition is inconsistent.'
}
if ($expires -le [DateTimeOffset]::UtcNow) { throw 'WP4 v2 manifest has expired.' }

$allowed = @('CredWriteW', 'CredReadW', 'CredDeleteW', 'CredFree')
$forbidden = @('CredEnumerateW', 'CredRenameW', 'CredWriteDomainCredentialsW',
    'any alternate credential or secret-storage mechanism')
if (($manifest.native_boundary.allowed_calls -join '|') -ne ($allowed -join '|') -or
    ($manifest.native_boundary.forbidden_calls -join '|') -ne ($forbidden -join '|') -or
    $manifest.native_boundary.fallback -ne 'none' -or
    @($manifest.native_boundary.forbidden_calls | Where-Object { $_ -match 'Enumerate' }).Count -eq 0) {
    throw 'WP4 v2 native boundary differs from the accepted exact-call/no-enumeration/no-fallback rule.'
}
$max = $manifest.operation_limits.native_call_maxima
if ([int64]$max.CredWriteW + [int64]$max.CredReadW + [int64]$max.CredDeleteW + [int64]$max.CredFree -ne
    [int64]$max.total) { throw 'WP4 v2 native-call maxima do not sum exactly.' }
if ([int64]$manifest.provider_boundary.dns_operations -ne 0 -or
    [int64]$manifest.provider_boundary.network_operations -ne 0 -or
    [int64]$manifest.provider_boundary.provider_operations -ne 0 -or
    [int64]$manifest.provider_boundary.billable_operations -ne 0 -or
    [int64]$manifest.operation_limits.fake_provider_dispatches -ne 1) {
    throw 'WP4 v2 permits a non-fake or external provider effect.'
}
if ([int64]$manifest.operation_limits.entry_dialogs -ne 3) {
    throw 'WP4 v2 requires exactly three genuine manual entry dialogs: submit, cancel, and restored next generation.'
}
if ([bool]$manifest.entry_boundary.prepopulate -or [bool]$manifest.entry_boundary.echo -or
    [bool]$manifest.entry_boundary.clipboard_return -or
    -not ([string]$manifest.entry_boundary.control).Contains('begins empty', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('manually types', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains(
        'no secret is supplied in arguments, environment, file, stdin, IPC, or programmatic window message',
        [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('current input-desktop object', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('short finite 10-second automatic pre-entry readiness window', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('separate finite five-minute human response interval', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.control).Contains('Settings -> Add/Replace -> WPF-parented helper modal', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('clipboard paste is deliberately blocked only in the qualification harness', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('paste-capable WPF-parented helper-owned masked modal', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('React/WebView provides only the gesture and non-secret status', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('first terminal action', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.action_routing).Contains('first terminal action only', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.action_routing).Contains('never poll global key state', [StringComparison]::Ordinal)) {
    throw 'WP4 v2 does not require genuine blank non-echoing operator entry.'
}
if ([bool]$manifest.qualification_components.native_success_run.inject_cleanup_ambiguity -or
    -not ([string]$manifest.qualification_components.non_native_prerequisites.cleanup_ambiguity_probe).Contains(
        'zero later native calls', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.qualification_components.actual_cleanup_ambiguity_rule).Contains(
        'no subsequent credential API call', [StringComparison]::Ordinal)) {
    throw 'WP4 v2 ambiguity placement is not terminal, non-native, and no-later-call.'
}
$processModel = [string]$manifest.qualification_components.native_success_run.process_model
foreach ($requiredProcessPhrase in @('exactly two inherited anonymous-pipe handles', 'no other inherited handle',
    'Job Object', 'no nested uncontained child')) {
    if (-not $processModel.Contains($requiredProcessPhrase, [StringComparison]::Ordinal)) {
        throw "WP4 v2 process containment is missing '$requiredProcessPhrase'."
    }
}

$targets = @($manifest.disposable_namespace.targets)
if ($targets.Count -ne 12 -or
    @($targets.alias | Sort-Object -Unique).Count -ne 12 -or
    @($targets.target_fingerprint_sha256 | Sort-Object -Unique).Count -ne 12) {
    throw 'WP4 v2 disposable target inventory is not exactly 12 unique targets.'
}
foreach ($target in $targets) {
    $raw = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
    if ($actual -ne $target.target_fingerprint_sha256 -or $text.Contains($raw, [StringComparison]::Ordinal)) {
        throw "WP4 v2 target '$($target.alias)' is not exact fingerprint-only authority."
    }
}
$expectedAliases = @('interactive-primary', 'interactive-cancel', 'size-valid', 'size-oversize',
    'unavailable-store', 'replacement-old', 'replacement-new', 'revoke-delete', 'crash-restart',
    'backup-old', 'backup-new', 'fake-dispatch')
if (($targets.alias -join '|') -ne ($expectedAliases -join '|')) {
    throw 'WP4 v2 exact target aliases are incomplete or reordered.'
}
$scenarios = @('interactive-entry-submit', 'interactive-entry-cancel', 'credential-size-boundaries',
    'secure-store-unavailable', 'replacement', 'revoke-delete',
    'helper-and-coordinator-crash-restart', 'backup-restore-reauthentication', 'fake-provider-dispatch')
if (($manifest.required_scenarios.id -join '|') -ne ($scenarios -join '|')) {
    throw 'WP4 v2 native success scenario set is incomplete, reordered, or includes ambiguity injection.'
}
foreach ($scenario in $manifest.required_scenarios) {
    foreach ($alias in $scenario.targets) {
        if ($targets.alias -notcontains $alias) { throw "Scenario '$($scenario.id)' references unknown target '$alias'." }
    }
}
$expectedScenarioTargets = @{
    'interactive-entry-submit' = 'interactive-primary'
    'interactive-entry-cancel' = 'interactive-cancel'
    'credential-size-boundaries' = 'size-valid|size-oversize'
    'secure-store-unavailable' = 'unavailable-store'
    'replacement' = 'replacement-old|replacement-new'
    'revoke-delete' = 'revoke-delete'
    'helper-and-coordinator-crash-restart' = 'crash-restart'
    'backup-restore-reauthentication' = 'backup-old|backup-new'
    'fake-provider-dispatch' = 'fake-dispatch'
}
foreach ($scenario in $manifest.required_scenarios) {
    if (($scenario.targets -join '|') -ne $expectedScenarioTargets[[string]$scenario.id]) {
        throw "WP4 v2 scenario '$($scenario.id)' has the wrong exact target set."
    }
}

$expectedCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNative -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json -OutputRoot artifacts/m1-slice6/wp4-native-e6e04651'
if ($manifest.execution_command -ne $expectedCommand -or
    -not ([string]$manifest.acceptance_binding.recording).Contains(
        'WP4_V2_OWNER_ACCEPTANCE manifest_id=<manifest_id> sha256=<manifest_sha256> close_ready_commit=<close_ready_implementation_commit> expires_at_utc=<expires_at_utc>',
        [StringComparison]::Ordinal)) {
    throw 'WP4 v2 command or canonical owner-acceptance record shape differs from the finite gate.'
}

$requiredEvidenceText = $manifest.required_evidence -join "`n"
$expectedPredecessorEvidence = 'exact manifest bytes and SHA-256 plus the superseded e3f76cd6 terminal manifest, failure evidence, authority lock, cleanup-recovery manifest/evidence/lock/receipt, and combined 12-target absence disposition'
if ([string]$manifest.required_evidence[0] -cne $expectedPredecessorEvidence) {
    throw 'WP4 v2 required evidence does not bind the exact consumed e3f76cd6 terminal and recovery lineage.'
}
foreach ($phrase in @('ordered allowed-call trace', 'real coordinator lifecycle', 'final-gate receipt',
    'initially blank', 'canary', 'Job Object', 'fresh independent Windows credential/security ACCEPT')) {
    if (-not $requiredEvidenceText.Contains($phrase, [StringComparison]::OrdinalIgnoreCase)) {
        throw "WP4 v2 required evidence is missing '$phrase'."
    }
}
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$receipt = [ordered]@{
    validation = 'M1/S6/WP4 v2 close-ready authorization proposal'
    status = if ($bindingPending) { 'draft-close-ready-binding-pending' } else { 'validated-ready-for-owner-acceptance' }
    manifest_id = $manifest.manifest_id
    manifest_bytes = $bytes.Length
    manifest_sha256 = $hash
    close_ready_implementation_commit = $closeReady
    accepted_wp3_candidate_commit = $expectedWp3
    accepted_wp7_product_candidate_commit = $expectedWp7Product
    accepted_wp7_evidence_commit = $expectedWp7Evidence
    authorization_handoff_commit = $expectedHandoff
    branch = $branch
    repository_head = $head
    expires_at_utc = $manifest.expires_at_utc
    target_count = $targets.Count
    native_scenario_count = $scenarios.Count
    non_native_ambiguity_prerequisite = $true
    execution_authorized = $false
    credential_manager_operations = 0
    network_operations = 0
    provider_operations = 0
}
$json = $receipt | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolved = if ([IO.Path]::IsPathRooted($OutputPath)) { [IO.Path]::GetFullPath($OutputPath) }
        else { [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath)) }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolved)) | Out-Null
    [IO.File]::WriteAllText($resolved, $json + "`n", [Text.UTF8Encoding]::new($false))
}
$json
