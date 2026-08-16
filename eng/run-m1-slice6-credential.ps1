[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [ValidateSet('EnrollOrVerifyProfile')] [string] $Operation,
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [Parameter(Mandatory = $true)] [string] $OutputRoot,
    [switch] $ValidateCampaignAdmissionOnly
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $relay = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath,
        '-Operation',$Operation,'-AuthorizationManifest',$AuthorizationManifest,'-OutputRoot',$OutputRoot)
    if ($ValidateCampaignAdmissionOnly) { $relay += '-ValidateCampaignAdmissionOnly' }
    & (Get-Command pwsh.exe).Source @relay
    exit $LASTEXITCODE
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'wp9-owner-documentation-contract.ps1')

function Get-Wp9Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-Wp9BinaryInventory([string] $Root) {
    $files = @([IO.Directory]::EnumerateFiles($Root, '*', [IO.SearchOption]::AllDirectories) |
        Where-Object {
            [IO.Path]::GetExtension($_) -in @('.exe', '.dll') -or
            $_.EndsWith('.deps.json', [StringComparison]::Ordinal) -or
            $_.EndsWith('.runtimeconfig.json', [StringComparison]::Ordinal)
        })
    [Array]::Sort($files, [StringComparer]::Ordinal)
    $lines = foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($Root, $file).Replace('\', '/')
        "$relative|$(Get-Wp9Sha256 $file)"
    }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($lines -join "`n") + "`n")
    try {
        return [pscustomobject]@{
            count = $files.Count
            sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        }
    }
    finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes) }
}

function Write-Wp9CreateNewText([string] $Path, [string] $Text) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Text)
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
        [Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes)
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = (Resolve-Path -LiteralPath $AuthorizationManifest).Path
$expectedManifestPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
if ($manifestPath -cne $expectedManifestPath) { throw 'Only the exact WP9 production-profile manifest path is executable.' }
$validation = & (Join-Path $PSScriptRoot 'validate-m1-slice6-wp9-profile-authorization.ps1') `
    -AuthorizationManifest $manifestPath -RequireReady | ConvertFrom-Json
$m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
if ([DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse($m.expires_at_utc, [Globalization.CultureInfo]::InvariantCulture)) {
    throw 'The exact WP9 production-profile manifest has expired.'
}

$branch = (& git -C $repoRoot branch --show-current).Trim()
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$status = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0 -or $branch -cne 'codex/m1-s6' -or $status.Count -ne 0) {
    throw 'WP9 profile execution requires the exact clean codex/m1-s6 candidate.'
}
$closeReady = [string]$m.candidate_binding.close_ready_implementation_commit
$expectedBuildCommand = "dotnet build Infinium.sln -c Release --no-restore --nologo --no-incremental -p:SourceRevisionId=$closeReady"
if ([string]$m.release_build.build_command -cne $expectedBuildCommand) {
    throw 'WP9 execution requires the canonical Release build command pinned to the close-ready source revision.'
}
& git -C $repoRoot merge-base --is-ancestor $closeReady $head
if ($LASTEXITCODE -ne 0) { throw 'The WP9 close-ready implementation is not an ancestor of the exact execution candidate.' }
$allowedPostClose = @(
    'docs/current-state.md',
    'docs/plans/milestones/m1/slices/s6/README.md',
    'docs/plans/milestones/m1/slices/s6/record.md',
    'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
)
$manifestSha = [string]$validation.manifest_sha256
$canonical = "WP9_PROFILE_OWNER_ACCEPTANCE manifest_id=$($m.manifest_id) sha256=$manifestSha close_ready_commit=$closeReady expires_at_utc=$($m.expires_at_utc)"
$reviewPattern = '^WP9_PROFILE_REVIEW_ACCEPTANCE candidate_commit=([0-9a-f]{40}) manifest_id=' +
    [Regex]::Escape([string]$m.manifest_id) + ' sha256=' + [Regex]::Escape($manifestSha) +
    ' verdicts=security,semantics,diff$'
$recordPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/record.md'
$campaignManifestPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v1.json'
$campaignIdentity = Get-Content -LiteralPath $campaignManifestPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$campaignIntentLines = @([IO.File]::ReadAllLines($recordPath) | Where-Object {
    ($_.StartsWith('M1_S6_CAMPAIGN_REVIEW_ACCEPTANCE ', [StringComparison]::Ordinal) -or
        $_.StartsWith('M1_S6_CAMPAIGN_ADMISSION ', [StringComparison]::Ordinal) -or
        $_.StartsWith('WP9_PROFILE_CAMPAIGN_ROLLOVER_ADMISSION ', [StringComparison]::Ordinal)) -and
    $_.Contains("campaign_id=$($campaignIdentity.campaign_id)", [StringComparison]::Ordinal)
})
$campaignValidation = $null
try {
    $campaignValidation = & (Join-Path $PSScriptRoot 'validate-m1-slice6-campaign.ps1') `
        -AuthorizationManifest $campaignManifestPath -RequireState RolloverAdmitted 2>$null | ConvertFrom-Json
}
catch { $campaignValidation = $null }
$campaignRoute = $null -ne $campaignValidation -and $campaignValidation.disposition -ceq 'rollover-admitted'
if (-not $campaignRoute -and $campaignIntentLines.Count -ne 0) {
    throw 'Campaign-scoped authority is present but invalid; credential execution refuses downgrade to an owner-marker route.'
}
if ($campaignRoute) {
    $campaign = Get-Content -LiteralPath $campaignManifestPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
    $reviewedCandidate = [string]$campaignValidation.reviewed_candidate_commit
    $rolloverLine = "WP9_PROFILE_CAMPAIGN_ROLLOVER_ADMISSION campaign_candidate_commit=$reviewedCandidate authority_sha256=$($campaign.authority_source.attachment_sha256) campaign_id=$($campaign.campaign_id) campaign_sha256=$($campaignValidation.manifest_sha256) manifest_id=$($m.manifest_id) sha256=$manifestSha close_ready_commit=$closeReady credential_expires_at_utc=$($m.expires_at_utc)"
    $rolloverLines = @([IO.File]::ReadAllLines($recordPath) | Where-Object { $_ -ceq $rolloverLine })
    if ($rolloverLines.Count -ne 1) { throw 'Campaign-derived credential execution requires one exact identity-scoped rollover admission.' }
    & git -C $repoRoot diff --quiet $reviewedCandidate -- 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
    if ($LASTEXITCODE -ne 0) { throw 'The campaign-derived credential manifest differs from the exact reviewed campaign candidate.' }
    $postReview = @(& git -C $repoRoot diff --name-only "$reviewedCandidate..$head")
    $expectedPostReview = @('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md','docs/plans/milestones/m1/slices/s6/record.md')
    if (@($postReview | Where-Object { $_ -notin $expectedPostReview }).Count -ne 0) {
        throw 'Campaign-derived execution contains post-review product or manifest drift.'
    }
}
else {
    $allReviewLines = @([IO.File]::ReadAllLines($recordPath) | Where-Object {
        $_.StartsWith('WP9_PROFILE_REVIEW_ACCEPTANCE ', [StringComparison]::Ordinal)
    })
    $reviewLines = @($allReviewLines | Where-Object { [Regex]::IsMatch($_, $reviewPattern) })
    $currentOwnerPrefix = "WP9_PROFILE_OWNER_ACCEPTANCE manifest_id=$($m.manifest_id) sha256=$manifestSha "
    $acceptanceLines = @([IO.File]::ReadAllLines($recordPath) | Where-Object { $_.StartsWith($currentOwnerPrefix, [StringComparison]::Ordinal) })
    if ($reviewLines.Count -ne 1) { throw 'WP9 profile execution requires one exact independent-review acceptance for the current manifest bytes.' }
    $reviewedCandidate = [Regex]::Match($reviewLines[0], $reviewPattern).Groups[1].Value
    & git -C $repoRoot merge-base --is-ancestor $closeReady $reviewedCandidate
    if ($LASTEXITCODE -ne 0) { throw 'The independently reviewed WP9 candidate does not descend from the close-ready implementation.' }
    $postClose = @(& git -C $repoRoot diff --name-only "$closeReady..$reviewedCandidate")
    if (@($postClose | Where-Object { $_ -notin $allowedPostClose }).Count -ne 0) { throw 'Code or unapproved documentation drift exists before the exact independently reviewed WP9 candidate.' }
    & git -C $repoRoot diff --quiet $reviewedCandidate -- 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
    if ($LASTEXITCODE -ne 0) { throw 'The owner-accepted WP9 manifest differs from the exact independently reviewed bytes.' }
    $postReview = @(& git -C $repoRoot diff --name-only "$reviewedCandidate..$head")
    $expectedPostReview = @('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md','docs/plans/milestones/m1/slices/s6/record.md')
    if ([string]::Join("`n", @($postReview | Sort-Object -CaseSensitive)) -cne [string]::Join("`n", $expectedPostReview)) { throw 'Only the exact owner-stop authority-document transition and append-only markers may follow the independently reviewed candidate.' }
    $reviewedRecord = [string]::Join("`n", @(& git -C $repoRoot show "$reviewedCandidate`:docs/plans/milestones/m1/slices/s6/record.md"))
    $currentRecord = ([IO.File]::ReadAllText($recordPath) -replace "`r`n", "`n").TrimEnd("`n")
    $expectedRecord = $reviewedRecord.TrimEnd("`n") + "`n`n" + $reviewLines[0] + "`n" + $canonical
    if ($LASTEXITCODE -ne 0 -or $currentRecord -cne $expectedRecord -or $acceptanceLines.Count -ne 1 -or $acceptanceLines[0] -cne $canonical) { throw 'WP9 profile execution requires exactly one canonical owner acceptance.' }
    $currentStateText = [IO.File]::ReadAllText((Join-Path $repoRoot 'docs/current-state.md')) -replace "`r`n", "`n"
    $sliceReadmeText = [IO.File]::ReadAllText((Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/README.md')) -replace "`r`n", "`n"
    $ownerAcceptedRequirements = Get-Wp9OwnerAcceptedDocumentationRequirements -ManifestId ([string]$m.manifest_id) `
        -ManifestSha256 $manifestSha -CloseReadyCommit $closeReady -ReviewedCandidate $reviewedCandidate
    if (-not (Test-Wp9DocumentationRequirements -CurrentStateText $currentStateText -ReadmeText $sliceReadmeText -Requirements $ownerAcceptedRequirements)) { throw 'WP9 execution requires exact owner-accepted documentation.' }
}

$expectedOutput = Join-Path $repoRoot ([string]$m.output.output_root_relative -replace '/', [IO.Path]::DirectorySeparatorChar)
$resolvedOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputRoot))
if ($resolvedOutput -cne $expectedOutput -or [IO.Directory]::Exists($resolvedOutput) -or [IO.File]::Exists($resolvedOutput)) {
    throw 'WP9 profile output must be the exact fresh absent manifest-bound root.'
}
$stateRoot = Join-Path $repoRoot ([string]$m.durable_state.product_state_root_relative -replace '/', [IO.Path]::DirectorySeparatorChar)
if ([IO.Directory]::Exists($stateRoot) -or [IO.File]::Exists($stateRoot)) {
    throw 'The new-only WP9 production profile state root already exists; verification/replacement is not authorized.'
}

$coordinator = Join-Path $repoRoot ([string]$m.release_build.coordinator_relative_path -replace '/', [IO.Path]::DirectorySeparatorChar)
$helper = Join-Path $repoRoot ([string]$m.release_build.helper_relative_path -replace '/', [IO.Path]::DirectorySeparatorChar)
$releaseRoot = Split-Path -Parent $coordinator
if (-not (Test-Path -LiteralPath $coordinator -PathType Leaf) -or
    -not (Test-Path -LiteralPath $helper -PathType Leaf)) {
    throw 'The exact repository-built Coordinator executable is absent. Build and revalidate before owner-authorized execution.'
}
if ([string]$m.release_build.source_commit -cne $closeReady -or
    (Get-Wp9Sha256 $coordinator) -cne [string]$m.release_build.coordinator_sha256 -or
    (Get-Wp9Sha256 $helper) -cne [string]$m.release_build.helper_sha256) {
    throw 'The exact reviewed Release coordinator/helper binary binding differs.'
}
$inventory = Get-Wp9BinaryInventory $releaseRoot
if ($inventory.count -ne [int]$m.release_build.binary_inventory_file_count -or
    $inventory.sha256 -cne [string]$m.release_build.binary_inventory_sha256) {
    throw 'The exact reviewed Release dependency-binary inventory differs.'
}
if ($ValidateCampaignAdmissionOnly) {
    if (-not $campaignRoute) { throw 'Campaign admission-only validation cannot use the owner-marker route.' }
    & $coordinator --wp9-campaign-credential-admission-probe --manifest $manifestPath `
        --manifest-sha256 $manifestSha --campaign-manifest $campaignManifestPath `
        --campaign-manifest-sha256 ([string]$campaignValidation.manifest_sha256) `
        --campaign-reviewed-candidate $reviewedCandidate
    if ($LASTEXITCODE -ne 0) { throw 'The contained Coordinator campaign credential route rejected admission.' }
    [pscustomobject]@{
        schema = 'infinium.m1-s6.wp9.campaign-credential-route-validation/v1'
        disposition = 'validated-before-output-lock-helper-readiness-ui-native-or-provider-effect'
        campaign_id = [string]$campaign.campaign_id
        campaign_manifest_sha256 = [string]$campaignValidation.manifest_sha256
        reviewed_candidate_commit = $reviewedCandidate
        credential_manifest_id = [string]$m.manifest_id
        credential_manifest_sha256 = $manifestSha
        effect_count = 0
    } | ConvertTo-Json -Depth 5
    return
}
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$campaignLedgerPath = Join-Path $resolvedOutput 'finite-campaign-ledger.v1.jsonl'
if ($campaignRoute) {
    & $coordinator --wp9-campaign-credential-handoff-admission --manifest $manifestPath `
        --manifest-sha256 $manifestSha --campaign-manifest $campaignManifestPath `
        --campaign-manifest-sha256 ([string]$campaignValidation.manifest_sha256) `
        --campaign-reviewed-candidate $reviewedCandidate --campaign-ledger $campaignLedgerPath
    if ($LASTEXITCODE -ne 0) {
        throw 'The zero-effect campaign credential handoff admission failed before the authority lock or helper launch.'
    }
}
$lockPath = Join-Path $resolvedOutput 'authority-lock.json'
$lock = [ordered]@{
    schema = 'infinium.m1-s6.wp9.profile-authority-lock/v1'
    manifest_id = [string]$m.manifest_id
    manifest_sha256 = $manifestSha
    close_ready_implementation_commit = $closeReady
    execution_candidate_commit = $head
    independently_reviewed_candidate_commit = $reviewedCandidate
    authority_route = if ($campaignRoute) { 'campaign-derived-semantic-rollover' } else { 'exact-owner-marker' }
    campaign_id = if ($campaignRoute) { [string]$campaign.campaign_id } else { $null }
    campaign_manifest_sha256 = if ($campaignRoute) { [string]$campaignValidation.manifest_sha256 } else { $null }
    release_source_commit = [string]$m.release_build.source_commit
    coordinator_sha256 = [string]$m.release_build.coordinator_sha256
    helper_sha256 = [string]$m.release_build.helper_sha256
    binary_inventory_file_count = [int]$m.release_build.binary_inventory_file_count
    binary_inventory_sha256 = [string]$m.release_build.binary_inventory_sha256
    consumed_at_utc = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
    retry_permitted = $false
}
$lockBytes = [Text.UTF8Encoding]::new($false).GetBytes(($lock | ConvertTo-Json -Depth 10) + "`n")
$lockStream = [IO.FileStream]::new($lockPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $lockStream.Write($lockBytes, 0, $lockBytes.Length)
    $lockStream.Flush($true)
}
finally {
    $lockStream.Dispose()
    [Security.Cryptography.CryptographicOperations]::ZeroMemory($lockBytes)
}

if ($campaignRoute) {
    & $coordinator --wp9-production-profile-enrollment --manifest $manifestPath `
        --manifest-sha256 $manifestSha --output-root $resolvedOutput --product-root $stateRoot `
        --campaign-manifest $campaignManifestPath `
        --campaign-manifest-sha256 ([string]$campaignValidation.manifest_sha256) `
        --campaign-reviewed-candidate $reviewedCandidate --campaign-ledger $campaignLedgerPath
}
else {
    & $coordinator --wp9-production-profile-enrollment --manifest $manifestPath `
        --manifest-sha256 $manifestSha --output-root $resolvedOutput --product-root $stateRoot
}
$coordinatorExit = $LASTEXITCODE
if ($coordinatorExit -ne 0) {
    $failurePath = Join-Path $resolvedOutput 'profile-enrollment-failure.json'
    if (-not [IO.File]::Exists($failurePath)) {
        $fallback = [ordered]@{
            schema = 'infinium.m1-s6.wp9.production-profile-enrollment-failure/v1'
            status = 'stopped-ambiguous-effect'
            failure_kind = "coordinator-exit-$coordinatorExit-without-complete-evidence"
            manifest_id = [string]$m.manifest_id
            manifest_sha256 = $manifestSha
            profile_id = [string]$m.profile.access_profile_id
            generation_id = [string]$m.profile.generation_id
            target_fingerprint_sha256 = [string]$m.profile.target_fingerprint_sha256
            native_call_count_status = 'unknown-helper-or-evidence-failure'
            native_credential_operation_count = $null
            native_call_trace = $null
            allocation_free_pairing = 'unknown-recovery-required'
            canary_evidence = 'unknown-recovery-required'
            ui_cleanup_evidence = 'unknown-recovery-required'
            durable_lifecycle_state = 'inspect-authoritative-store-before-recovery'
            durable_verification_state = 'unavailable'
            recovery_required = $true
            provider_requests_blocked = $true
            retry_permitted = $false
            network_operation_count = $null
            provider_operation_count = 0
            billable_operation_count = 0
            containment_probe_executed = $false
            process_tree_terminated = $false
            process_tree_survivor_count = $null
            excluded_handle_accessible = $null
        }
        $failureBytes = [Text.UTF8Encoding]::new($false).GetBytes(($fallback | ConvertTo-Json -Depth 10) + "`n")
        $failureStream = [IO.FileStream]::new($failurePath, [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $failureStream.Write($failureBytes, 0, $failureBytes.Length)
            $failureStream.Flush($true)
        }
        finally {
            $failureStream.Dispose()
            [Security.Cryptography.CryptographicOperations]::ZeroMemory($failureBytes)
        }
    }
    $mainEvidencePath = Join-Path $resolvedOutput 'profile-enrollment-evidence.json'
    if (-not [IO.File]::Exists($mainEvidencePath)) {
        $mainFallback = [ordered]@{
            schema = 'infinium.m1-s6.wp9.production-profile-enrollment-evidence/v1'
            status = 'stopped-ambiguous-effect'
            manifest_id = [string]$m.manifest_id
            manifest_sha256 = $manifestSha
            profile_id = [string]$m.profile.access_profile_id
            generation_id = [string]$m.profile.generation_id
            target_fingerprint_sha256 = [string]$m.profile.target_fingerprint_sha256
            lifecycle_state = 'inspect-authoritative-store-before-recovery'
            verification_state = 'unavailable'
            native_credential_operation_count = $null
            native_call_trace = $null
            entry_evidence = 'unknown-recovery-required'
            canaries = 'unknown-recovery-required'
            network_operation_count = $null
            listener_count = $null
            provider_operation_count = 0
            billable_operation_count = 0
            retry_attempted = $false
            recovery_required = $true
            qualification_request_authority = 'none'
        }
        Write-Wp9CreateNewText $mainEvidencePath (($mainFallback | ConvertTo-Json -Depth 10) + "`n")
    }
    $summaryPath = Join-Path $resolvedOutput 'profile-enrollment-summary.txt'
    if (-not [IO.File]::Exists($summaryPath)) {
        $summaryFallback = @(
            'WP9 production profile enrollment',
            'status=stopped-ambiguous-effect',
            "profile_id=$($m.profile.access_profile_id)",
            "generation_id=$($m.profile.generation_id)",
            "target_fingerprint_sha256=$($m.profile.target_fingerprint_sha256)",
            'lifecycle_state=inspect-authoritative-store-before-recovery',
            'verification_state=unavailable',
            'native_calls=unknown',
            'network_operations=unknown',
            'provider_operations=0',
            'billable_operations=0',
            'retry_attempted=false',
            'recovery_required=true',
            'qualification_request_authority=none'
        ) -join "`n"
        Write-Wp9CreateNewText $summaryPath ($summaryFallback + "`n")
    }
    throw "WP9 profile enrollment stopped with typed exit code $coordinatorExit; no retry is authorized."
}
