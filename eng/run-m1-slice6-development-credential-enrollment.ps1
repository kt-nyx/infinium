[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$coordinator = Join-Path $repository 'src\Infinium.Coordinator\bin\Release\net10.0\Infinium.Coordinator.exe'
$helper = Join-Path $repository 'src\Infinium.CredentialHelper\bin\Release\net10.0\Infinium.CredentialHelper.exe'

if (-not (Test-Path -LiteralPath $coordinator -PathType Leaf) -or
    -not (Test-Path -LiteralPath $helper -PathType Leaf)) {
    throw 'Release coordinator and credential-helper apphosts must exist before enrollment.'
}

# The credential is accepted only by the helper-owned masked native dialog. It
# never becomes a parameter, environment variable, pipeline value, or log item.
& $coordinator --m1-slice6-development-credential-enrollment
exit $LASTEXITCODE
