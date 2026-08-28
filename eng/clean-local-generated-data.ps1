[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Execute
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -eq $pwsh) { throw 'Generated-data cleanup requires PowerShell 7 or pwsh on PATH.' }
    $arguments = @('-NoProfile', '-File', $PSCommandPath)
    if (-not [string]::IsNullOrWhiteSpace($RepositoryRoot)) { $arguments += @('-RepositoryRoot', $RepositoryRoot) }
    if ($Execute) { $arguments += '-Execute' }
    & $pwsh.Source @arguments
    if ($LASTEXITCODE -ne 0) { throw "Generated-data cleanup failed with exit code $LASTEXITCODE." }
    return
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    Split-Path -Parent $PSScriptRoot
} else { $RepositoryRoot }
$repositoryRootPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd('\')
$repositoryPrefix = $repositoryRootPath + [IO.Path]::DirectorySeparatorChar
$gitRoot = @(& git -C $repositoryRootPath rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0 -or $gitRoot.Count -ne 1 -or
    (Resolve-Path -LiteralPath $gitRoot[0]).Path.TrimEnd('\') -cne $repositoryRootPath) {
    throw 'RepositoryRoot must resolve to the exact Git root.'
}
$trackedPaths = @(& git -C $repositoryRootPath ls-files | ForEach-Object { $_.Replace('\','/') })
$trackedSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$trackedPaths | ForEach-Object { $null = $trackedSet.Add($_) }

function Assert-InRepository([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target escapes the repository root: $full"
    }
    $full
}

function Get-DirectoryFileBytes([string]$Path) {
    $files = @(Get-ChildItem -LiteralPath $Path -Recurse -File -Force)
    if ($files.Count -eq 0) { return [long]0 }
    [long](($files | Measure-Object Length -Sum).Sum)
}

$directoryTargets = [Collections.Generic.List[object]]::new()
$packageRoot = Join-Path $repositoryRootPath '.packages'
if (Test-Path -LiteralPath $packageRoot -PathType Container) {
    $directoryTargets.Add([ordered]@{ relative_path='.packages'; absolute_path=(Assert-InRepository $packageRoot); category='package-cache' })
}

foreach ($top in @('src','tests','fixtures','eng')) {
    $topRoot = Join-Path $repositoryRootPath $top
    if (-not (Test-Path -LiteralPath $topRoot -PathType Container)) { continue }
    foreach ($directory in Get-ChildItem -LiteralPath $topRoot -Recurse -Directory -Force |
        Where-Object { $_.Name -in @('bin','obj','TestResults','__pycache__') }) {
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing generated-data reparse point: $($directory.FullName)"
        }
        $absolute = Assert-InRepository $directory.FullName
        $relative = [IO.Path]::GetRelativePath($repositoryRootPath, $absolute).Replace('\','/')
        $tracked = @($trackedPaths | Where-Object { $_.StartsWith($relative + '/', [StringComparison]::Ordinal) })
        if ($tracked.Count -ne 0) { throw "Generated directory contains tracked files: $relative" }
        $category = if ($directory.Name -eq '__pycache__') { 'python-cache' } else { $directory.Name.ToLowerInvariant() }
        $directoryTargets.Add([ordered]@{ relative_path=$relative; absolute_path=$absolute; category=$category })
    }
}

$workRoot = Join-Path $repositoryRootPath 'work'
if (Test-Path -LiteralPath $workRoot -PathType Container) {
    $workItem = Get-Item -LiteralPath $workRoot -Force
    if (($workItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing generated-data reparse point: $($workItem.FullName)"
    }
    $absolute = Assert-InRepository $workItem.FullName
    $relative = [IO.Path]::GetRelativePath($repositoryRootPath, $absolute).Replace('\','/')
    $tracked = @($trackedPaths | Where-Object { $_.StartsWith($relative + '/', [StringComparison]::Ordinal) })
    if ($tracked.Count -ne 0) { throw "Generated directory contains tracked files: $relative" }
    $directoryTargets.Add([ordered]@{ relative_path=$relative; absolute_path=$absolute; category='work-output' })
}

$artifactFileTargets = [Collections.Generic.List[object]]::new()
$ignoredArtifacts = @(& git -C $repositoryRootPath ls-files --others --ignored --exclude-standard -- 'artifacts/' |
    ForEach-Object { $_.Replace('\','/') } | Sort-Object -Unique)
foreach ($relative in $ignoredArtifacts) {
    $absolute = Assert-InRepository (Join-Path $repositoryRootPath $relative)
    if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) { continue }
    if ($trackedSet.Contains($relative)) {
        throw "Artifact file is tracked and cannot be cleaned locally: $relative"
    }
    $artifactFileTargets.Add([ordered]@{ relative_path=$relative; absolute_path=$absolute; category='ignored-artifact' })
}

$directoryTargets = @($directoryTargets | Sort-Object { $_.absolute_path.Length } -Descending)
$artifactFileTargets = @($artifactFileTargets | Sort-Object relative_path)
$bytes = [long]0
foreach ($target in $directoryTargets) {
    $bytes += Get-DirectoryFileBytes $target.absolute_path
}
$bytes += [long](($artifactFileTargets | ForEach-Object { (Get-Item -LiteralPath $_.absolute_path).Length } | Measure-Object -Sum).Sum)

$mode = if ($Execute) { 'EXECUTE' } else { 'DRY-RUN' }
Write-Host "$mode generated-data cleanup: $($directoryTargets.Count) directories and $($artifactFileTargets.Count) ignored artifact files; $bytes bytes."
$directoryTargets | ForEach-Object { Write-Host "[$mode][$($_.category)] $($_.relative_path)" }
if ($artifactFileTargets.Count -gt 0) {
    Write-Host "[$mode][ignored-artifact] artifacts/ ($($artifactFileTargets.Count) exact ignored files; paths are in the accepted local inventory)"
}

if (-not $Execute) { return }

foreach ($target in $directoryTargets) {
    $absolute = Assert-InRepository $target.absolute_path
    if (Test-Path -LiteralPath $absolute -PathType Container) {
        Remove-Item -LiteralPath $absolute -Recurse -Force
    }
}
foreach ($target in $artifactFileTargets) {
    $absolute = Assert-InRepository $target.absolute_path
    if (Test-Path -LiteralPath $absolute -PathType Leaf) {
        Remove-Item -LiteralPath $absolute -Force
    }
}

$artifactsRoot = Join-Path $repositoryRootPath 'artifacts'
if (Test-Path -LiteralPath $artifactsRoot -PathType Container) {
    Get-ChildItem -LiteralPath $artifactsRoot -Recurse -Directory -Force |
        Sort-Object { $_.FullName.Length } -Descending |
        ForEach-Object {
            $absolute = Assert-InRepository $_.FullName
            if (@(Get-ChildItem -LiteralPath $absolute -Force).Count -eq 0) {
                Remove-Item -LiteralPath $absolute -Force
            }
        }
    if (@(Get-ChildItem -LiteralPath $artifactsRoot -Force).Count -eq 0) {
        Remove-Item -LiteralPath (Assert-InRepository $artifactsRoot) -Force
    }
}

Write-Host 'Generated-data cleanup completed.'
