[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [ValidateSet('EnrollOrVerifyProfile')] [string] $Operation,
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [Parameter(Mandatory = $true)] [string] $CampaignManifest,
    [Parameter(Mandatory = $true)] [string] $CampaignReviewedCandidate,
    [Parameter(Mandatory = $true)] [string] $CampaignLedger,
    [Parameter(Mandatory = $true)] [string] $ProductStateRoot,
    [Parameter(Mandatory = $true)] [string] $CoordinatorBinary,
    [Parameter(Mandatory = $true)] [string] $RuntimeAuthorityManifest,
    [Parameter(Mandatory = $true)] [string] $RuntimeAuthoritySha256,
    [Parameter(Mandatory = $true)] [string] $OutputRoot,
    [switch] $ValidateCampaignAdmissionOnly
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

$credentialPath = Resolve-RequiredFile $AuthorizationManifest
$campaignPath = Resolve-RequiredFile $CampaignManifest
$coordinator = Resolve-RequiredFile $CoordinatorBinary
$runtimeAuthority = Resolve-RequiredFile $RuntimeAuthorityManifest
$runtimeSha = Get-Sha256 $runtimeAuthority
if ($runtimeSha -cne $RuntimeAuthoritySha256) {
    throw 'The typed runtime authority bytes differ from the exact supplied digest.'
}
$credentialSha = Get-Sha256 $credentialPath
$campaignSha = Get-Sha256 $campaignPath

& $coordinator --wp9-campaign-credential-admission-probe --manifest $credentialPath `
    --manifest-sha256 $credentialSha --campaign-manifest $campaignPath `
    --campaign-manifest-sha256 $campaignSha --campaign-reviewed-candidate $CampaignReviewedCandidate `
    --runtime-authority $runtimeAuthority --runtime-authority-sha256 $runtimeSha
if ($LASTEXITCODE -ne 0) {
    throw 'The typed credential authority failed before output, helper, UI, native, or provider effect.'
}
if ($ValidateCampaignAdmissionOnly) { return }

$resolvedOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputRoot))
$ledgerPath = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $CampaignLedger))
$stateRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $ProductStateRoot))
if (([IO.Directory]::Exists($resolvedOutput) -or [IO.File]::Exists($resolvedOutput)) -or
    ([IO.Directory]::Exists($stateRoot) -or [IO.File]::Exists($stateRoot))) {
    throw 'Credential execution requires absent output and product-state roots.'
}
if ([IO.File]::Exists($ledgerPath)) {
    throw 'Credential execution requires an absent new-campaign ledger.'
}
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
if ([IO.Path]::GetDirectoryName($ledgerPath) -cne $resolvedOutput) {
    throw 'The credential campaign ledger must be directly contained by the exact output root.'
}

& $coordinator --wp9-campaign-credential-handoff-admission --manifest $credentialPath `
    --manifest-sha256 $credentialSha --campaign-manifest $campaignPath `
    --campaign-manifest-sha256 $campaignSha --campaign-reviewed-candidate $CampaignReviewedCandidate `
    --campaign-ledger $ledgerPath --runtime-authority $runtimeAuthority `
    --runtime-authority-sha256 $runtimeSha
if ($LASTEXITCODE -ne 0) {
    throw 'The durable typed credential handoff admission failed before helper, UI, native, or provider effect.'
}

& $coordinator --wp9-production-profile-enrollment --manifest $credentialPath `
    --manifest-sha256 $credentialSha --output-root $resolvedOutput --product-root $stateRoot `
    --campaign-manifest $campaignPath --campaign-manifest-sha256 $campaignSha `
    --campaign-reviewed-candidate $CampaignReviewedCandidate --campaign-ledger $ledgerPath `
    --runtime-authority $runtimeAuthority --runtime-authority-sha256 $runtimeSha
if ($LASTEXITCODE -ne 0) {
    throw 'Credential enrollment stopped with a typed failure; retry is prohibited.'
}
