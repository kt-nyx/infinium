param(
    [Parameter(Mandatory = $true)]
    [string]$EvaluatorRoot,

    [Parameter(Mandatory = $true)]
    [DateTimeOffset]$WindowStartUtc,

    [Parameter(Mandatory = $true)]
    [DateTimeOffset]$WindowEndUtc,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = (Resolve-Path -LiteralPath $EvaluatorRoot).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
if (-not $outputFullPath.StartsWith($resolvedRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must be inside the evaluator root."
}
if ($WindowEndUtc -lt $WindowStartUtc) {
    throw "WindowEndUtc must not precede WindowStartUtc."
}

$scopes = @('mo2-app', 'instance', 'game-root')
$entries = foreach ($scope in $scopes) {
    $scopeRoot = Join-Path $resolvedRoot $scope
    if (-not (Test-Path -LiteralPath $scopeRoot)) {
        continue
    }
    Get-ChildItem -LiteralPath $scopeRoot -File -Recurse -Force |
        Where-Object {
            $when = [DateTimeOffset]$_.LastWriteTimeUtc
            $when -ge $WindowStartUtc -and $when -le $WindowEndUtc
        } |
        Sort-Object FullName |
        ForEach-Object {
            [pscustomobject]@{
                path = $_.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                byte_length = $_.Length
                last_write_time_utc = $_.LastWriteTimeUtc.ToString('O')
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        }
}

$record = [ordered]@{
    schema_id = 'infinium.eval.copied-mo2-write-window'
    schema_version = 1
    root_token = '<EVALUATOR_ROOT>'
    window_start_utc = $WindowStartUtc.ToUniversalTime().ToString('O')
    window_end_utc = $WindowEndUtc.ToUniversalTime().ToString('O')
    attribution = 'Files under copied-MO2-owned scopes whose last-write timestamp falls inside the isolated UI observation window.'
    limitations = @(
        'Timestamp-window membership records observed write effects; it does not identify the internal MO2 call site.',
        'The record is valid only when no other writer is authorized in the listed scopes during the window.'
    )
    entries = @($entries)
}

$parent = Split-Path -Parent $outputFullPath
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$json = $record | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($outputFullPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Output $outputFullPath
