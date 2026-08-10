[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRootPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
$allowedStatuses = @(
    'Draft',
    'Proposed',
    'Completed',
    'Accepted',
    'Deferred',
    'Rejected',
    'Superseded',
    'Template'
)

$documentationRoot = Join-Path $repositoryRootPath 'docs'
$metadataFiles = @(
    Get-ChildItem -LiteralPath $documentationRoot -Recurse -File -Filter '*.md'
    Get-Item -LiteralPath (Join-Path $repositoryRootPath 'dependencies/README.md')
)
$linkFiles = @(
    Get-Item -LiteralPath (Join-Path $repositoryRootPath 'README.md')
    Get-Item -LiteralPath (Join-Path $repositoryRootPath 'AGENTS.md')
    $metadataFiles
)

$failures = [System.Collections.Generic.List[string]]::new()

foreach ($file in $metadataFiles) {
    $header = @(Get-Content -LiteralPath $file.FullName -TotalCount 40)
    $statusLine = $header | Where-Object {
        $_ -match '^Status:\s*(?<value>.+?)\s*$'
    } | Select-Object -First 1

    if ($null -eq $statusLine) {
        $failures.Add("Missing document Status metadata: $($file.FullName)")
    }
    else {
        $null = $statusLine -match '^Status:\s*(?<value>.+?)\s*$'
        $value = $Matches.value.Trim()
        if ($allowedStatuses -notcontains $value) {
            $failures.Add("Unknown document status '$value': $($file.FullName)")
        }
    }

    if (-not ($header -match '^Last reviewed:\s*\d{4}-\d{2}-\d{2}\s*$')) {
        $failures.Add("Missing or invalid Last reviewed metadata: $($file.FullName)")
    }
}

$markdownLinkPattern = '(?<!!)\[[^\]]+\]\((?<target>[^)]+)\)'
foreach ($file in $linkFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($content, $markdownLinkPattern)) {
        $target = $match.Groups['target'].Value.Trim()
        if ($target.StartsWith('<') -and $target.EndsWith('>')) {
            $target = $target.Substring(1, $target.Length - 2)
        }
        if ($target -match '^(?:https?://|mailto:|#)') {
            continue
        }

        $target = ($target -split '#', 2)[0]
        if ([string]::IsNullOrWhiteSpace($target)) {
            continue
        }

        $target = [System.Uri]::UnescapeDataString($target)
        $resolved = [System.IO.Path]::GetFullPath(
            (Join-Path (Split-Path -Parent $file.FullName) $target))
        if (-not (Test-Path -LiteralPath $resolved)) {
            $relativeFile = [System.IO.Path]::GetRelativePath($repositoryRootPath, $file.FullName)
            $failures.Add("Broken local link in ${relativeFile}: $target")
        }
    }
}

$jsonFiles = @(
    Get-ChildItem -LiteralPath $documentationRoot -Recurse -File -Filter '*.json'
    Get-Item -LiteralPath (Join-Path $repositoryRootPath 'dependencies/dependency-manifest.json')
)
$jsonOptions = [System.Text.Json.JsonDocumentOptions]::new()
$jsonOptions.AllowTrailingCommas = $false
$jsonOptions.CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
$jsonOptions.MaxDepth = 128

foreach ($file in $jsonFiles) {
    try {
        $document = [System.Text.Json.JsonDocument]::Parse(
            [System.IO.File]::ReadAllText($file.FullName),
            $jsonOptions)
        $document.Dispose()
    }
    catch {
        $failures.Add("Invalid strict JSON in $($file.FullName): $($_.Exception.Message)")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Documentation validation failed with $($failures.Count) finding(s)."
}

Write-Host (
    'Documentation validation passed: {0} metadata files, {1} Markdown link sources, {2} JSON files.' -f
    $metadataFiles.Count,
    $linkFiles.Count,
    $jsonFiles.Count)
