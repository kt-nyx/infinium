[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [Parameter(Mandatory = $true)] [string] $CampaignManifest,
    [Parameter(Mandatory = $true)] [string] $CampaignReviewedCandidate,
    [Parameter(Mandatory = $true)] [string] $CampaignLedger,
    [Parameter(Mandatory = $true)] [string] $Evidence,
    [Parameter(Mandatory = $true)] [string] $Failure,
    [Parameter(Mandatory = $true)] [string] $ProductStateRoot,
    [Parameter(Mandatory = $true)] [string] $CoordinatorBinary,
    [Parameter(Mandatory = $true)] [string] $HelperBinary,
    [Parameter(Mandatory = $true)] [string] $RuntimeAuthorityManifest,
    [Parameter(Mandatory = $true)] [string] $RuntimeAuthoritySha256
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath @PSBoundParameters
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Resolve-RequiredFile([string] $Path) { (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path }
function Sha([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

$credential = Resolve-RequiredFile $AuthorizationManifest
$campaign = Resolve-RequiredFile $CampaignManifest
$ledger = Resolve-RequiredFile $CampaignLedger
$evidence = Resolve-RequiredFile $Evidence
$failure = Resolve-RequiredFile $Failure
$coordinator = Resolve-RequiredFile $CoordinatorBinary
$helper = Resolve-RequiredFile $HelperBinary
$runtime = Resolve-RequiredFile $RuntimeAuthorityManifest
$productRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $ProductStateRoot))
if ((Sha $runtime) -cne $RuntimeAuthoritySha256) { throw 'Credential recovery runtime-authority bytes differ.' }

& $coordinator --wp9-campaign-credential-evidence-recovery --manifest $credential `
    --manifest-sha256 (Sha $credential) --campaign-manifest $campaign `
    --campaign-manifest-sha256 (Sha $campaign) --campaign-reviewed-candidate $CampaignReviewedCandidate `
    --campaign-ledger $ledger --evidence $evidence --failure $failure `
    --product-root $productRoot `
    --helper $helper --runtime-authority $runtime --runtime-authority-sha256 $RuntimeAuthoritySha256
if ($LASTEXITCODE -ne 0) { throw 'Credential evidence recovery stopped without retry.' }
