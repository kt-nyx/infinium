[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [ValidateSet('Ready','Reviewed','Admitted','RolloverAdmitted')] [string] $RequireState = 'Ready'
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
        -AuthorizationManifest $AuthorizationManifest -RequireState $RequireState
    exit $LASTEXITCODE
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$path = (Resolve-Path -LiteralPath $AuthorizationManifest).Path
$expected = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v2.json'
if ($path -cne $expected) { throw 'Only the exact successor finite-campaign authority path is valid.' }
$schema = Join-Path $repoRoot 'contracts/repository/m1-slice6-finite-campaign-authorization.v2.schema.json'
$json = [IO.File]::ReadAllText($path)
$validator = Join-Path $repoRoot 'src/Infinium.Coordinator/bin/Release/net10.0/Infinium.Coordinator.dll'
if (-not [IO.File]::Exists($validator)) { throw 'Build the exact Release coordinator before validation.' }
& dotnet $validator --validate-repository-authority-json --document $path --schema $schema *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'The successor finite-campaign authority failed its complete accepted schema.'
}
$manifest = $json | ConvertFrom-Json -Depth 100 -DateKind String
if ([string]$manifest.schema_identity -cne 'infinium.repository.m1-slice6-finite-campaign-authorization/2.0.0' -or
    [string]$manifest.campaign_id -cne 'infinium.m1-s6.finite-live-campaign/51b9dba6-aca3-41d7-82d1-afd805e33e66' -or
    [string]$manifest.status -cne 'ready-for-campaign-review' -or
    [string]$manifest.expires_at_utc -cne '2026-08-31T23:59:00.0000000Z') {
    throw 'The successor finite-campaign identity, state, or expiry is not exact.'
}
if ([DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse(
        [string]$manifest.expires_at_utc, [Globalization.CultureInfo]::InvariantCulture)) {
    throw 'The successor finite-campaign authority expired.'
}
$sha = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head.Length -ne 40) { throw 'The successor campaign candidate is unavailable.' }
$relative = 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v2.json'
$recordRelative = 'docs/plans/milestones/m1/slices/s6/record.md'
$workBlob = (& git -C $repoRoot hash-object -- $relative).Trim()
$headBlob = (& git -C $repoRoot rev-parse "HEAD`:$relative").Trim()
$authorityCommit = (& git -C $repoRoot log -1 --format=%H -- $relative).Trim()
if ($LASTEXITCODE -ne 0 -or $workBlob -cne $headBlob -or $authorityCommit.Length -ne 40 -or
    (& git -C $repoRoot rev-parse "$authorityCommit`:$relative").Trim() -cne $headBlob) {
    throw 'The successor campaign manifest is not the exact committed authority blob.'
}
$closeReady = [string]$manifest.candidate_binding.close_ready_implementation_commit
& git -C $repoRoot merge-base --is-ancestor $closeReady $authorityCommit
if ($LASTEXITCODE -ne 0 -or $closeReady -ceq $authorityCommit) {
    throw 'The successor campaign authority does not descend from its distinct close-ready implementation.'
}
$recordPath = Join-Path $repoRoot $recordRelative
$recordLines = @([IO.File]::ReadAllLines($recordPath))
$reviewPrefix = "M1_S6_CAMPAIGN_REVIEW_ACCEPTANCE candidate_commit=$authorityCommit campaign_id=$($manifest.campaign_id) sha256=$sha verdicts=security,semantics,diff"
$admission = "M1_S6_CAMPAIGN_ADMISSION candidate_commit=$authorityCommit authority_sha256=$($manifest.authority_source.attachment_sha256) campaign_id=$($manifest.campaign_id) sha256=$sha close_ready_commit=$closeReady expires_at_utc=$($manifest.expires_at_utc)"
$profilePath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v2.json'
$rollover = $null
if ([IO.File]::Exists($profilePath)) {
    $profile = [IO.File]::ReadAllText($profilePath) | ConvertFrom-Json -Depth 100 -DateKind String
    $profileSha = (Get-FileHash -LiteralPath $profilePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $rollover = "WP9_PROFILE_CAMPAIGN_ROLLOVER_ADMISSION campaign_candidate_commit=$authorityCommit authority_sha256=$($manifest.authority_source.attachment_sha256) campaign_id=$($manifest.campaign_id) campaign_sha256=$sha manifest_id=$($profile.manifest_id) sha256=$profileSha close_ready_commit=$($profile.candidate_binding.close_ready_implementation_commit) credential_expires_at_utc=$($profile.expires_at_utc)"
}

function Get-ExactTransitionCommit([string] $Marker, [string] $ExpectedParent) {
    $matches = @($recordLines | Where-Object { $_ -ceq $Marker })
    if ($matches.Count -ne 1) { throw 'A successor campaign lifecycle marker is absent or duplicated.' }
    $commits = @(& git -C $repoRoot log --format=%H "-S$Marker" -- $recordRelative)
    $commits = @($commits | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($LASTEXITCODE -ne 0 -or $commits.Count -ne 1) {
        throw 'A successor campaign lifecycle marker has no unique committed transition.'
    }
    $commit = $commits[0]
    $parent = (& git -C $repoRoot rev-parse "$commit^").Trim()
    if ($parent -cne $ExpectedParent) { throw 'A successor campaign lifecycle transition has a stale predecessor.' }
    $paths = @(& git -C $repoRoot -c core.quotePath=false diff --name-only $parent $commit --)
    $expectedPaths = @('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md',$recordRelative)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    [Array]::Sort($expectedPaths, [StringComparer]::Ordinal)
    if ([string]::Join("`n", $paths) -cne [string]::Join("`n", $expectedPaths)) {
        throw 'A successor campaign lifecycle transition changed a fourth or missing path.'
    }
    $before = @(& git -C $repoRoot show "$parent`:$recordRelative")
    $after = @(& git -C $repoRoot show "$commit`:$recordRelative")
    if (@($before | Where-Object { $_ -ceq $Marker }).Count -ne 0 -or
        @($after | Where-Object { $_ -ceq $Marker }).Count -ne 1) {
        throw 'A successor campaign lifecycle marker was inherited or not added exactly once.'
    }
    return $commit
}

$reviewCommit = $null
$admissionCommit = $null
$rolloverCommit = $null
if ($RequireState -ne 'Ready') {
    $reviewCommit = Get-ExactTransitionCommit $reviewPrefix $authorityCommit
}
if ($RequireState -in @('Admitted','RolloverAdmitted')) {
    $admissionCommit = Get-ExactTransitionCommit $admission $reviewCommit
}
if ($RequireState -ceq 'RolloverAdmitted') {
    if ($null -eq $rollover) { throw 'The exact successor profile manifest is absent.' }
    $rolloverCommit = Get-ExactTransitionCommit $rollover $admissionCommit
}
$reviewLike = @($recordLines | Where-Object { $_.StartsWith('M1_S6_CAMPAIGN_REVIEW_ACCEPTANCE ', [StringComparison]::Ordinal) -and $_.Contains("campaign_id=$($manifest.campaign_id)", [StringComparison]::Ordinal) })
$admissionLike = @($recordLines | Where-Object { $_.StartsWith('M1_S6_CAMPAIGN_ADMISSION ', [StringComparison]::Ordinal) -and $_.Contains("campaign_id=$($manifest.campaign_id)", [StringComparison]::Ordinal) })
$rolloverLike = @($recordLines | Where-Object { $_.StartsWith('WP9_PROFILE_CAMPAIGN_ROLLOVER_ADMISSION ', [StringComparison]::Ordinal) -and $_.Contains("campaign_id=$($manifest.campaign_id)", [StringComparison]::Ordinal) })
$expectedCounts = switch ($RequireState) {
    'Ready' { @(0,0,0) }
    'Reviewed' { @(1,0,0) }
    'Admitted' { @(1,1,0) }
    default { @(1,1,1) }
}
if ($reviewLike.Count -ne $expectedCounts[0] -or $admissionLike.Count -ne $expectedCounts[1] -or
    $rolloverLike.Count -ne $expectedCounts[2]) {
    throw 'The requested successor campaign lifecycle state does not match its exact marker set.'
}
$stateCommit = switch ($RequireState) {
    'Ready' { $authorityCommit }
    'Reviewed' { $reviewCommit }
    'Admitted' { $admissionCommit }
    default { $rolloverCommit }
}
& git -C $repoRoot merge-base --is-ancestor $stateCommit $head
if ($LASTEXITCODE -ne 0 -or ($RequireState -ne 'RolloverAdmitted' -and $head -cne $stateCommit)) {
    throw 'The requested successor campaign lifecycle is not the exact committed current state.'
}
[ordered]@{
    schema = 'infinium.m1-s6.finite-campaign-authorization-validation/v2'
    disposition = switch ($RequireState) {
        'Ready' { 'ready' }
        'Reviewed' { 'reviewed' }
        'Admitted' { 'admitted' }
        default { 'rollover-admitted' }
    }
    campaign_id = [string]$manifest.campaign_id
    manifest_sha256 = $sha
    reviewed_candidate_commit = $authorityCommit
    lifecycle_commit = $stateCommit
    effect_count = 0
} | ConvertTo-Json -Depth 5
