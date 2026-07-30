param(
    [Parameter(Mandatory = $true)]
    [string]$EvaluatorRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = (Resolve-Path -LiteralPath $EvaluatorRoot).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)

if (-not $outputFullPath.StartsWith($resolvedRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath must be inside the evaluator root."
}

$files = Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse -Force |
    Where-Object { $_.FullName -ne $outputFullPath } |
    Sort-Object FullName

$entries = foreach ($file in $files) {
    [pscustomobject]@{
        path = $file.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
        byte_length = $file.Length
        last_write_time_utc = $file.LastWriteTimeUtc.ToString('O')
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}

$canonicalLines = foreach ($entry in $entries) {
    '{0}|{1}|{2}|{3}' -f $entry.path, $entry.byte_length, $entry.last_write_time_utc, $entry.sha256
}
$canonicalBytes = [System.Text.Encoding]::UTF8.GetBytes(($canonicalLines -join "`n"))
$digestBytes = [System.Security.Cryptography.SHA256]::HashData($canonicalBytes)
$digest = [Convert]::ToHexString($digestBytes)

$manifest = [ordered]@{
    schema_id = 'infinium.eval.evaluator-root-manifest'
    schema_version = 1
    root_token = '<EVALUATOR_ROOT>'
    captured_at = (Get-Date).ToUniversalTime().ToString('O')
    file_count = @($entries).Count
    byte_count = [long](($files | Measure-Object -Property Length -Sum).Sum)
    structural_and_content_sha256 = $digest
    files = @($entries)
}

$parent = Split-Path -Parent $outputFullPath
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($outputFullPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Output $outputFullPath
