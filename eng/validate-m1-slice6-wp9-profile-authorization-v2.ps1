[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [switch] $RequireReady
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $relay = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath,
        '-AuthorizationManifest',$AuthorizationManifest)
    if ($RequireReady) { $relay += '-RequireReady' }
    & (Get-Command pwsh.exe).Source @relay
    exit $LASTEXITCODE
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$path = (Resolve-Path -LiteralPath $AuthorizationManifest).Path
$expected = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v2.json'
if ($path -cne $expected) { throw 'Only the exact successor production-profile authority path is valid.' }
$schema = Join-Path $repoRoot 'contracts/repository/wp9-production-profile-authorization.v2.schema.json'
$json = [IO.File]::ReadAllText($path)
$validator = Join-Path $repoRoot 'src/Infinium.Coordinator/bin/Release/net10.0/Infinium.Coordinator.dll'
if (-not [IO.File]::Exists($validator)) { throw 'Build the exact Release coordinator before validation.' }
& dotnet $validator --validate-repository-authority-json --document $path --schema $schema *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'The successor production-profile authority failed its complete accepted schema.'
}
$manifest = $json | ConvertFrom-Json -Depth 100 -DateKind String
if ([string]$manifest.schema_identity -cne 'infinium.repository.wp9-production-profile-authorization/2.0.0' -or
    [string]$manifest.status -cne 'ready-for-owner-acceptance' -or
    [string]$manifest.expires_at_utc -cne '2026-08-31T23:00:00.0000000Z') {
    throw 'The successor production-profile identity, state, or expiry is not exact.'
}
if ($RequireReady -and [DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse(
        [string]$manifest.expires_at_utc, [Globalization.CultureInfo]::InvariantCulture)) {
    throw 'The successor production-profile authority expired.'
}
$sha = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
$relative = 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v2.json'
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$workBlob = (& git -C $repoRoot hash-object -- $relative).Trim()
$headBlob = (& git -C $repoRoot rev-parse "HEAD`:$relative").Trim()
$authorityCommit = (& git -C $repoRoot log -1 --format=%H -- $relative).Trim()
$closeReady = [string]$manifest.candidate_binding.close_ready_implementation_commit
$releaseSource = [string]$manifest.release_build.source_commit
$expectedBuild = "dotnet build Infinium.sln -c Release --no-restore --nologo --no-incremental -p:SourceRevisionId=$closeReady"
if ($LASTEXITCODE -ne 0 -or $head.Length -ne 40 -or $workBlob -cne $headBlob -or
    $authorityCommit.Length -ne 40 -or
    (& git -C $repoRoot rev-parse "$authorityCommit`:$relative").Trim() -cne $headBlob -or
    $releaseSource -cne $closeReady -or [string]$manifest.release_build.build_command -cne $expectedBuild) {
    throw 'The successor production profile does not bind one exact committed candidate and Release source.'
}
& git -C $repoRoot merge-base --is-ancestor $closeReady $authorityCommit
if ($LASTEXITCODE -ne 0 -or $closeReady -ceq $authorityCommit) {
    throw 'The successor production profile candidate does not descend from its distinct Release source.'
}
$acceptedCandidate = $null
if ($RequireReady) {
    $campaignPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v2.json'
    $campaignReceipt = & (Join-Path $PSScriptRoot 'validate-m1-slice6-campaign-v2.ps1') `
        -AuthorizationManifest $campaignPath -RequireState RolloverAdmitted | ConvertFrom-Json -Depth 20
    $campaign = [IO.File]::ReadAllText($campaignPath) | ConvertFrom-Json -Depth 100 -DateKind String
    $acceptedCandidate = [string]$campaignReceipt.reviewed_candidate_commit
    if ($acceptedCandidate -cne $authorityCommit -or
        (& git -C $repoRoot rev-parse "$acceptedCandidate`:$relative").Trim() -cne $headBlob -or
        [string]$campaign.credential_envelope.source_manifest_id -cne [string]$manifest.manifest_id -or
        [string]$campaign.credential_envelope.source_manifest_sha256 -cne $sha -or
        [string]$campaign.credential_envelope.profile_id -cne [string]$manifest.profile.access_profile_id -or
        [string]$campaign.credential_envelope.generation_id -cne [string]$manifest.profile.generation_id -or
        [string]$campaign.credential_envelope.target_fingerprint_sha256 -cne [string]$manifest.profile.target_fingerprint_sha256) {
        throw 'The successor production profile is not the exact campaign-reviewed rollover envelope.'
    }
}
[ordered]@{
    schema = 'infinium.m1-s6.wp9.production-profile-authorization-validation/v2'
    disposition = if ($RequireReady) { 'ready' } else { 'valid' }
    manifest_id = [string]$manifest.manifest_id
    manifest_sha256 = $sha
    profile_id = [string]$manifest.profile.access_profile_id
    generation_id = [string]$manifest.profile.generation_id
    target_fingerprint_sha256 = [string]$manifest.profile.target_fingerprint_sha256
    authority_candidate_commit = $authorityCommit
    accepted_campaign_candidate_commit = $acceptedCandidate
    effect_count = 0
} | ConvertTo-Json -Depth 5
