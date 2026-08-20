[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Coordinator,
    [Parameter(Mandatory)][string]$CoordinatorSha256,
    [Parameter(Mandatory)][string]$CampaignManifest,
    [Parameter(Mandatory)][string]$CampaignManifestSha256,
    [Parameter(Mandatory)][string]$StageManifest,
    [Parameter(Mandatory)][string]$StageManifestSha256,
    [Parameter(Mandatory)][string]$CredentialManifest,
    [Parameter(Mandatory)][string]$CredentialManifestSha256,
    [Parameter(Mandatory)][string]$RuntimeAuthority,
    [Parameter(Mandatory)][string]$RuntimeAuthoritySha256,
    [Parameter(Mandatory)][string]$Ledger,
    [Parameter(Mandatory)][string]$SafetyStateRoot,
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
$campaignPath = Resolve-ExactFile $CampaignManifest $CampaignManifestSha256
$stagePath = Resolve-ExactFile $StageManifest $StageManifestSha256
$credentialPath = Resolve-ExactFile $CredentialManifest $CredentialManifestSha256
$runtimePath = Resolve-ExactFile $RuntimeAuthority $RuntimeAuthoritySha256
$helperPath = Resolve-ExactFile $Helper $HelperSha256
$ledgerPath = [IO.Path]::GetFullPath($Ledger)
$safetyRoot = (Resolve-Path -LiteralPath $SafetyStateRoot).Path
$evidencePath = [IO.Path]::GetFullPath($Evidence)

# This wrapper deliberately performs one invocation. It has no loop, retry,
# fallback, authority derivation, credential inspection, or output selection.
& $coordinatorPath --m1-slice6-successor-attempt `
    --campaign-manifest $campaignPath --campaign-manifest-sha256 $CampaignManifestSha256 `
    --stage-manifest $stagePath --stage-manifest-sha256 $StageManifestSha256 `
    --credential-manifest $credentialPath --credential-manifest-sha256 $CredentialManifestSha256 `
    --runtime-authority $runtimePath --runtime-authority-sha256 $RuntimeAuthoritySha256 `
    --ledger $ledgerPath --safety-state-root $safetyRoot `
    --helper-binary $helperPath --helper-sha256 $HelperSha256 --evidence $evidencePath
exit $LASTEXITCODE
