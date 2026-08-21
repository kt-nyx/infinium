[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Coordinator,
    [Parameter(Mandatory)][string]$CoordinatorSha256,
    [Parameter(Mandatory)][string]$Authority,
    [Parameter(Mandatory)][string]$AuthoritySha256,
    [Parameter(Mandatory)][string]$Review,
    [Parameter(Mandatory)][string]$ReviewSha256,
    [Parameter(Mandatory)][string]$ProductStateRoot,
    [Parameter(Mandatory)][string]$Ledger,
    [Parameter(Mandatory)][string]$Helper,
    [Parameter(Mandatory)][string]$HelperSha256,
    [Parameter(Mandatory)][string]$Evidence
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-ExactFile([string]$Path, [string]$ExpectedSha256) {
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Required exact file is absent: $Path"
    }
    $actual = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne $ExpectedSha256.ToLowerInvariant()) {
        throw "Exact file digest differs: $Path"
    }
    return $resolved
}

$coordinatorPath = Resolve-ExactFile $Coordinator $CoordinatorSha256
$authorityPath = Resolve-ExactFile $Authority $AuthoritySha256
$reviewPath = Resolve-ExactFile $Review $ReviewSha256
$helperPath = Resolve-ExactFile $Helper $HelperSha256
$productRoot = (Resolve-Path -LiteralPath $ProductStateRoot).Path
$ledgerPath = (Resolve-Path -LiteralPath $Ledger).Path
$evidencePath = [IO.Path]::GetFullPath($Evidence)

# One invocation only. The credential is entered exclusively into the helper-owned
# masked native window; it is never a parameter, command-line value, environment
# variable, pipeline value, or repository artifact.
& $coordinatorPath --m1-slice6-successor-credential-replacement `
    --authority $authorityPath --authority-sha256 $AuthoritySha256 `
    --review $reviewPath --review-sha256 $ReviewSha256 `
    --product-state-root $productRoot --ledger $ledgerPath `
    --helper $helperPath --helper-sha256 $HelperSha256 --evidence $evidencePath
exit $LASTEXITCODE
