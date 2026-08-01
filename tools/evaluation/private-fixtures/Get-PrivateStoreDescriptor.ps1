[CmdletBinding()]
param(
    [switch] $IncludeLocatorForDelegation
)

$ErrorActionPreference = 'Stop'

$repositoryRootText = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRootText)) {
    throw 'Run this command from an Infinium Git checkout.'
}

$repositoryRoot = [IO.Path]::GetFullPath($repositoryRootText.Trim())
$configuredPath = (& git config --local --get infinium.evaluatorPrivateStorePath 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($configuredPath)) {
    throw 'The local infinium.evaluatorPrivateStorePath Git setting is not configured.'
}

$configuredPathText = $configuredPath.Trim()
$isWindowsDriveAbsolute = $configuredPathText -match '^[A-Za-z]:[\\/]'
$isUncAbsolute = $configuredPathText -match '^[\\/]{2}[^\\/]+[\\/][^\\/]+'
$isPlatformAbsolute =
    -not [System.Environment]::OSVersion.Platform.ToString().StartsWith('Win') -and
    [IO.Path]::IsPathRooted($configuredPathText)
if (-not ($isWindowsDriveAbsolute -or $isUncAbsolute -or $isPlatformAbsolute)) {
    throw 'The evaluator-private store path must be fully qualified.'
}

$storeRoot = [IO.Path]::GetFullPath($configuredPathText)
$repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if ($storeRoot.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $storeRoot.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The evaluator-private store must not be the Infinium repository or a nested directory.'
}

$storeMetadataPath = Join-Path $storeRoot 'STORE.json'
$storeGitPath = Join-Path $storeRoot '.git'
if (-not (Test-Path -LiteralPath $storeMetadataPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $storeGitPath)) {
    throw 'The configured path is not an initialized Infinium evaluator-private store.'
}

$storeRepositoryRootText = (& git -C $storeRoot rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($storeRepositoryRootText)) {
    throw 'The configured evaluator-private store is not a Git worktree.'
}
$storeRepositoryRoot = [IO.Path]::GetFullPath($storeRepositoryRootText.Trim())
if (-not $storeRepositoryRoot.Equals($storeRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The configured evaluator-private store path must be its Git worktree root.'
}

$storeMetadata = Get-Content -LiteralPath $storeMetadataPath -Raw |
    ConvertFrom-Json
if ($storeMetadata.schema_id -ne 'infinium.evaluation.private-store/v1' -or
    $storeMetadata.store_id -ne 'infinium-evaluator-fixtures' -or
    $storeMetadata.relationship -ne 'separate-git-history') {
    throw 'The configured private store metadata is not accepted.'
}

$storeRevision = (& git -C $storeRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $storeRevision -notmatch '^[0-9a-f]{40}$') {
    throw 'The configured private store does not have an immutable Git revision.'
}

$descriptor = [ordered]@{
    schema_id = 'infinium.evaluation.private-store-descriptor/v1'
    schema_version = '1'
    store_id = $storeMetadata.store_id
    relationship = $storeMetadata.relationship
    governance_version = $storeMetadata.governance_version
    revision = $storeRevision
}

if ($IncludeLocatorForDelegation) {
    $descriptor.delegation_store_root = $storeRoot
}

$descriptor | ConvertTo-Json -Depth 4
