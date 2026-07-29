param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $RepositoryRoot 'dependencies/dependency-manifest.json'
$packagesRoot = Join-Path $RepositoryRoot '.packages'
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json

$central = [xml](Get-Content -Raw (Join-Path $RepositoryRoot 'Directory.Packages.props'))
$directVersions = @{}
foreach ($item in $central.Project.ItemGroup.PackageVersion) {
    $directVersions[$item.Include] = $item.Version
}

$resolved = @{}
$locks = Get-ChildItem $RepositoryRoot -Recurse -Filter packages.lock.json |
    Where-Object { $_.FullName -notmatch '\\(?:bin|obj|\.packages)\\' }
foreach ($lock in $locks) {
    $document = Get-Content -Raw $lock.FullName | ConvertFrom-Json
    foreach ($framework in $document.dependencies.PSObject.Properties) {
        foreach ($package in $framework.Value.PSObject.Properties) {
            if ($package.Value.resolved) {
                $resolved["$($package.Name)/$($package.Value.resolved)"] = [pscustomobject]@{
                    Id = $package.Name
                    Version = $package.Value.resolved
                }
            }
        }
    }
}

function Get-PackageMetadata([string]$id, [string]$version) {
    $folder = Join-Path $packagesRoot "$($id.ToLowerInvariant())/$version"
    $nuspec = Get-ChildItem $folder -Filter *.nuspec | Select-Object -First 1
    if (-not $nuspec) {
        throw "No NuSpec was found for $id/$version."
    }

    $xml = [xml](Get-Content -Raw $nuspec.FullName)
    $metadata = $xml.package.metadata
    $license = if ($metadata.license -and $metadata.license.type -eq 'expression') {
        [string]$metadata.license.'#text'
    }
    elseif ($id -eq 'SQLite') {
        'Public Domain'
    }
    else {
        "Package license file: $($metadata.license.'#text')"
    }
    $repository = if ($metadata.repository) { [string]$metadata.repository.url } else { $null }
    $commit = if ($metadata.repository) { [string]$metadata.repository.commit } else { $null }
    $metadataFile = Join-Path $folder '.nupkg.metadata'
    $contentHash = if (Test-Path $metadataFile) {
        (Get-Content -Raw $metadataFile | ConvertFrom-Json).contentHash
    } else {
        $null
    }

    [pscustomobject]@{
        License = $license
        Repository = $repository
        Commit = $commit
        ProjectUrl = [string]$metadata.projectUrl
        ContentHash = $contentHash
    }
}

$metadataByIdentity = @{}
foreach ($identity in $resolved.Keys) {
    $package = $resolved[$identity]
    $metadataByIdentity[$identity] = Get-PackageMetadata $package.Id $package.Version
}

$direct = foreach ($id in ($directVersions.Keys | Sort-Object)) {
    $version = $directVersions[$id]
    $identity = "$id/$version"
    $metadata = $metadataByIdentity[$identity]
    if (-not $metadata) {
        throw "Direct dependency $identity is absent from all lock files."
    }

    [ordered]@{
        id = $id
        version = $version
        license = $metadata.License
        repository = $metadata.Repository
        repositoryCommit = $metadata.Commit
        nupkgSha512 = $metadata.ContentHash
        use = if ($id -like 'MSTest.*' -or $id -eq 'Microsoft.NET.Test.Sdk') {
            'test-only'
        } elseif ($id -eq 'Grpc.Tools') {
            'build-time protobuf code generation'
        } else {
            'M1 production runtime'
        }
    }
}

$resolvedEntries = foreach ($identity in ($resolved.Keys | Sort-Object)) {
    $package = $resolved[$identity]
    $metadata = $metadataByIdentity[$identity]
    [ordered]@{
        id = $package.Id
        version = $package.Version
        license = $metadata.License
    }
}

$verified = @()
$limitations = @()
foreach ($identity in ($resolved.Keys | Sort-Object)) {
    $metadata = $metadataByIdentity[$identity]
    if ($metadata.Repository -and $metadata.Commit) {
        $verified += [ordered]@{
            package = $identity
            repository = $metadata.Repository
            commit = $metadata.Commit
        }
    }
    else {
        $verifiedPackage = $resolved[$identity]
        $limitations += [ordered]@{
            package = $identity
            license = $metadata.License
            projectUrl = $metadata.ProjectUrl
            sourceRevision = $null
            control = 'Exact NuGet identity and SHA-512 remain locked; immutable source revision is unavailable in package metadata.'
            effect = 'Runtime use is admitted; redistribution and source-compliance closure remain separately reviewable.'
        }
    }
}

$manifest.revision = 'm1-slice-2/1'
$manifest.reviewedOn = '2026-07-29'
$manifest.scope = 'NuGet packages and .NET toolchain used through M1 Slice 2'
$manifest.lockIdentity.resolvedPackageCount = $resolvedEntries.Count
$manifest.lockIdentity.productionGraph = 'src/Infinium.Persistence/packages.lock.json'
$manifest.lockIdentity.emptyProjectLocks = 'Project-local locks without external package dependencies'
$manifest.directPackages = @($direct)
$manifest.resolvedPackages = @($resolvedEntries)
$manifest.provenanceGroups = @()
$manifest.individuallyVerifiedProvenance = @($verified)
$manifest.explicitProvenanceLimitations = @($limitations)
$manifest.excludedDependencies = @(
    $manifest.excludedDependencies |
        Where-Object { $_ -ne 'SQLite binding' } |
        Sort-Object
)

$json = $manifest | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText(
    $manifestPath,
    $json + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
