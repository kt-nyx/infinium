[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [int]$Mutations = 64
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Disposable capture-race file is missing: $Path"
}
for ($i = 0; $i -lt $Mutations; $i++) {
    try {
        $stream = [IO.File]::Open(
            $Path,
            [IO.FileMode]::Open,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::ReadWrite
        )
        try {
            $stream.Position = $stream.Length - 1
            $value = $stream.ReadByte()
            $stream.Position = $stream.Length - 1
            $stream.WriteByte($value -bxor 0x01)
            $stream.Flush($true)
        }
        finally {
            $stream.Dispose()
        }
    }
    catch [IO.IOException] {
        # A guarded capture may correctly deny the concurrent writer.
    }
    Start-Sleep -Milliseconds 5
}
