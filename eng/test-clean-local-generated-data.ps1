[CmdletBinding()]
param()

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -eq $pwsh) { throw 'Generated-data cleanup boundary testing requires PowerShell 7 or pwsh on PATH.' }
    & $pwsh.Source -NoProfile -File $PSCommandPath
    if ($LASTEXITCODE -ne 0) { throw "Generated-data cleanup boundary testing failed with exit code $LASTEXITCODE." }
    return
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$cleanupScript = Join-Path $PSScriptRoot 'clean-local-generated-data.ps1'
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('infinium-generated-data-cleanup-' + [Guid]::NewGuid().ToString('N'))

function Write-Utf8NoBom([string]$Path, [string]$Value) {
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Write-Utf8NoBom (Join-Path $temporaryRoot '.gitignore') @"
/.packages/
/artifacts/
/work/
bin/
obj/
TestResults/
"@
    Write-Utf8NoBom (Join-Path $temporaryRoot 'docs/retained.txt') 'retained'
    Write-Utf8NoBom (Join-Path $temporaryRoot '.packages/tool/cache.bin') 'cache'
    Write-Utf8NoBom (Join-Path $temporaryRoot 'artifacts/run/receipt.json') '{}'
    Write-Utf8NoBom (Join-Path $temporaryRoot 'work/frontend-tool-probe/output.bin') 'probe'
    Write-Utf8NoBom (Join-Path $temporaryRoot 'eng/tooling/obj/project.assets.json') '{}'
    Write-Utf8NoBom (Join-Path $temporaryRoot 'src/App/bin/app.dll') 'assembly'
    Write-Utf8NoBom (Join-Path $temporaryRoot 'tests/App/TestResults/result.trx') '<TestRun />'
    New-Item -ItemType Directory -Path (Join-Path $temporaryRoot 'tests/Empty/TestResults') -Force | Out-Null

    & git -C $temporaryRoot init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Cleanup test repository initialization failed.' }
    & git -C $temporaryRoot -c core.autocrlf=false add .gitignore docs/retained.txt
    if ($LASTEXITCODE -ne 0) { throw 'Cleanup test repository staging failed.' }

    $dryRun = @(& $cleanupScript -RepositoryRoot $temporaryRoot *>&1)
    if ($LASTEXITCODE -ne 0) { throw "Cleanup dry-run failed: $($dryRun -join [Environment]::NewLine)" }
    foreach ($expected in @('.packages', 'artifacts/', 'work', 'eng/tooling/obj', 'src/App/bin', 'tests/App/TestResults', 'tests/Empty/TestResults')) {
        if (($dryRun -join "`n").IndexOf($expected, [StringComparison]::Ordinal) -lt 0) {
            throw "Cleanup dry-run omitted expected target: $expected"
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $temporaryRoot 'work/frontend-tool-probe/output.bin'))) {
        throw 'Cleanup dry-run changed generated work output.'
    }

    $execute = @(& $cleanupScript -RepositoryRoot $temporaryRoot -Execute *>&1)
    if ($LASTEXITCODE -ne 0) { throw "Cleanup execution failed: $($execute -join [Environment]::NewLine)" }
    foreach ($removed in @(
        '.packages',
        'work',
        'eng/tooling/obj',
        'src/App/bin',
        'tests/App/TestResults',
        'tests/Empty/TestResults',
        'artifacts/run/receipt.json')) {
        if (Test-Path -LiteralPath (Join-Path $temporaryRoot $removed)) {
            throw "Cleanup retained generated target: $removed"
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $temporaryRoot 'docs/retained.txt') -PathType Leaf)) {
        throw 'Cleanup removed tracked repository content.'
    }

    Write-Utf8NoBom (Join-Path $temporaryRoot 'work/retained.txt') 'retained work'
    & git -C $temporaryRoot -c core.autocrlf=false add --force work/retained.txt
    if ($LASTEXITCODE -ne 0) { throw 'Cleanup tracked-work refusal fixture staging failed.' }
    $refusedTrackedWork = $false
    try {
        & $cleanupScript -RepositoryRoot $temporaryRoot -Execute 2>&1 | Out-Null
    }
    catch {
        if ($_.Exception.Message -match 'Generated directory contains tracked files: work') {
            $refusedTrackedWork = $true
        } else {
            throw
        }
    }
    if (-not $refusedTrackedWork) { throw 'Cleanup accepted a work directory containing tracked content.' }
    if (-not (Test-Path -LiteralPath (Join-Path $temporaryRoot 'work/retained.txt') -PathType Leaf)) {
        throw 'Cleanup changed tracked work content while refusing the target.'
    }

    Write-Output 'Generated-data cleanup boundary test passed.'
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporaryRoot)
    $temporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolvedTemporary.StartsWith($temporaryParent, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedTemporary).StartsWith('infinium-generated-data-cleanup-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force -ErrorAction SilentlyContinue
    }
}
