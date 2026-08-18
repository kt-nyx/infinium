[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Qualification','SourceClaimExtraction','CandidateInvestigation')]
    [string] $Operation,
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [Parameter(Mandatory = $true)] [string] $CampaignManifest,
    [Parameter(Mandatory = $true)] [string] $CampaignReviewedCandidate,
    [Parameter(Mandatory = $true)] [string] $CredentialManifest,
    [Parameter(Mandatory = $true)] [string] $CampaignLedger,
    [Parameter(Mandatory = $true)] [string] $ProductStateRoot,
    [Parameter(Mandatory = $true)] [string] $CoordinatorBinary,
    [Parameter(Mandatory = $true)] [string] $HelperBinary,
    [Parameter(Mandatory = $true)] [string] $RuntimeAuthorityManifest,
    [Parameter(Mandatory = $true)] [string] $RuntimeAuthoritySha256,
    [Parameter(Mandatory = $true)] [string] $OutputRoot
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath @PSBoundParameters
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RequiredFile([string] $Path) {
    return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
}
function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$stagePath = Resolve-RequiredFile $AuthorizationManifest
$campaignPath = Resolve-RequiredFile $CampaignManifest
$credentialPath = Resolve-RequiredFile $CredentialManifest
$ledgerPath = Resolve-RequiredFile $CampaignLedger
$stateRoot = (Resolve-Path -LiteralPath $ProductStateRoot -ErrorAction Stop).Path
$coordinator = Resolve-RequiredFile $CoordinatorBinary
$helper = Resolve-RequiredFile $HelperBinary
$runtimeAuthority = Resolve-RequiredFile $RuntimeAuthorityManifest
$runtimeSha = Get-Sha256 $runtimeAuthority
if ($runtimeSha -cne $RuntimeAuthoritySha256) {
    throw 'The typed runtime authority bytes differ from the exact supplied digest.'
}

$resolvedOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputRoot))
if ([IO.Directory]::Exists($resolvedOutput) -or [IO.File]::Exists($resolvedOutput)) {
    throw 'The one-shot stage output root must be absent.'
}
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

$evidencePath = Join-Path $resolvedOutput 'stage-evidence.json'
& $coordinator --m1-slice6-campaign-stage --stage-manifest $stagePath `
    --stage-manifest-sha256 (Get-Sha256 $stagePath) --campaign-manifest $campaignPath `
    --campaign-manifest-sha256 (Get-Sha256 $campaignPath) `
    --campaign-reviewed-candidate $CampaignReviewedCandidate `
    --credential-manifest $credentialPath --credential-manifest-sha256 (Get-Sha256 $credentialPath) `
    --campaign-ledger $ledgerPath --safety-state-root $stateRoot --helper-binary $helper `
    --helper-sha256 (Get-Sha256 $helper) --runtime-authority $runtimeAuthority `
    --runtime-authority-sha256 $runtimeSha --evidence $evidencePath
if ($LASTEXITCODE -ne 0) {
    throw "The exact one-shot $Operation stage stopped; retry is prohibited."
}
