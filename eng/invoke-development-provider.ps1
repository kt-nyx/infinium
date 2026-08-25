[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $Manifest,

    [switch] $Live
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src/Infinium.CredentialHelper/Infinium.CredentialHelper.csproj'
$mode = if ($Live) { '--live' } else { '--offline' }
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path

& dotnet run --project $project --no-launch-profile -- --development-provider-invocation --manifest $manifestPath $mode

if ($LASTEXITCODE -ne 0) {
    throw "The development provider invocation failed with exit code $LASTEXITCODE."
}
