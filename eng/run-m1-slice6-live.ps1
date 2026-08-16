[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Qualification','SourceClaimExtraction','CandidateInvestigation')]
    [string] $Operation,
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [Parameter(Mandatory = $true)] [string] $OutputRoot
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
        -Operation $Operation -AuthorizationManifest $AuthorizationManifest -OutputRoot $OutputRoot
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$branch = (& git -C $repoRoot branch --show-current).Trim()
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$status = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=all)
if ($branch -cne 'codex/m1-s6' -or $status.Count -ne 0) {
    throw 'Campaign stage execution requires the exact clean codex/m1-s6 authority candidate.'
}

$stagePath = (Resolve-Path -LiteralPath $AuthorizationManifest).Path
$liveRoot = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/live'
if (-not $stagePath.StartsWith($liveRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Campaign stage execution requires an exact canonical live stage manifest.'
}
$stage = Get-Content -LiteralPath $stagePath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$expectedOrdinal = switch ($Operation) { 'Qualification' { 1 } 'SourceClaimExtraction' { 2 } default { 3 } }
if ([int]$stage.stage.ordinal -ne $expectedOrdinal -or [string]$stage.stage.operation -cne $Operation -or
    [string]$stage.status -cne 'reviewed-and-admitted') {
    throw 'The stage operation, ordinal, or authority state is stale.'
}
$stageSha = (Get-FileHash -LiteralPath $stagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$campaignPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v1.json'
$campaignReceipt = & (Join-Path $PSScriptRoot 'validate-m1-slice6-campaign.ps1') `
    -AuthorizationManifest $campaignPath -RequireState RolloverAdmitted | ConvertFrom-Json -Depth 20
$campaign = Get-Content -LiteralPath $campaignPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
if ([DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse([string]$campaign.expires_at_utc,
        [Globalization.CultureInfo]::InvariantCulture)) {
    throw 'The finite provider campaign expired before stage launch.'
}
$credentialPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
$credentialReceipt = & (Join-Path $PSScriptRoot 'validate-m1-slice6-wp9-profile-authorization.ps1') `
    -AuthorizationManifest $credentialPath -RequireReady | ConvertFrom-Json -Depth 20
$credential = Get-Content -LiteralPath $credentialPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$expectedOutputRelative = switch ($Operation) {
    'Qualification' { 'artifacts/m1-slice6/wp9-live' }
    'SourceClaimExtraction' { 'artifacts/m1-slice6/wp10-live' }
    default { 'artifacts/m1-slice6/wp11-live' }
}
$expectedOutput = [IO.Path]::GetFullPath((Join-Path $repoRoot ($expectedOutputRelative -replace '/', [IO.Path]::DirectorySeparatorChar)))
$resolvedOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputRoot))
if ($resolvedOutput -cne $expectedOutput -or [IO.Directory]::Exists($resolvedOutput) -or [IO.File]::Exists($resolvedOutput)) {
    throw 'The stage output must be its exact fresh absent one-shot root.'
}
$credentialOutput = Join-Path $repoRoot ([string]$credential.output.output_root_relative -replace '/', [IO.Path]::DirectorySeparatorChar)
$ledgerPath = Join-Path $credentialOutput 'finite-campaign-ledger.v1.jsonl'
$safetyRoot = Join-Path $repoRoot ([string]$credential.durable_state.product_state_root_relative -replace '/', [IO.Path]::DirectorySeparatorChar)
if (-not [IO.File]::Exists($ledgerPath) -or -not [IO.Directory]::Exists($safetyRoot)) {
    throw 'Campaign stage execution requires the exact accepted credential ledger and product state.'
}
$coordinator = Join-Path $repoRoot ([string]$credential.release_build.coordinator_relative_path -replace '/', [IO.Path]::DirectorySeparatorChar)
$helper = Join-Path $repoRoot ([string]$credential.release_build.helper_relative_path -replace '/', [IO.Path]::DirectorySeparatorChar)
if ((Get-FileHash -LiteralPath $coordinator -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$credential.release_build.coordinator_sha256 -or
    (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$credential.release_build.helper_sha256) {
    throw 'The campaign stage executable closure differs from the exact reviewed credential binding.'
}

[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$evidencePath = Join-Path $resolvedOutput 'stage-evidence.json'
& $coordinator --m1-slice6-campaign-stage --stage-manifest $stagePath `
    --stage-manifest-sha256 $stageSha --campaign-manifest $campaignPath `
    --campaign-manifest-sha256 ([string]$campaignReceipt.manifest_sha256) `
    --campaign-reviewed-candidate ([string]$campaignReceipt.reviewed_candidate_commit) `
    --credential-manifest $credentialPath --credential-manifest-sha256 ([string]$credentialReceipt.manifest_sha256) `
    --campaign-ledger $ledgerPath --safety-state-root $safetyRoot --helper-binary $helper `
    --helper-sha256 ([string]$credential.release_build.helper_sha256) --evidence $evidencePath
if ($LASTEXITCODE -ne 0) {
    throw "The exact one-shot $Operation campaign stage stopped; retry is prohibited."
}
