param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$Check
)

if ($PSVersionTable.PSVersion.Major -lt 7) {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    $arguments = @(
        '-NoProfile',
        '-File',
        $PSCommandPath,
        '-RepositoryRoot',
        $RepositoryRoot
    )
    if ($Check) {
        $arguments += '-Check'
    }
    & $pwsh.Source @arguments
    exit $LASTEXITCODE
}

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $RepositoryRoot 'dependencies/dependency-manifest.json'
$curationPath = Join-Path $RepositoryRoot 'dependencies/dependency-curation.json'
$packagesRoot = Join-Path $RepositoryRoot '.packages'
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json
$curation = Get-Content -Raw $curationPath | ConvertFrom-Json
if ($curation.schema -ne 'infinium.dependency-curation/v1') {
    throw "Unsupported dependency curation schema '$($curation.schema)'."
}

$curatedLicenses = @{}
foreach ($property in $curation.licenses.PSObject.Properties) {
    if ([string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "Curated license classification '$($property.Name)' is empty."
    }
    $curatedLicenses[$property.Name] = [string]$property.Value
}

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
    $identity = "$id/$version"
    $license = if ($curatedLicenses.ContainsKey($identity)) {
        $curatedLicenses[$identity]
    }
    elseif ($metadata.license -and $metadata.license.type -eq 'expression') {
        [string]$metadata.license.'#text'
    }
    elseif ($metadata.license -and
        $metadata.license.type -eq 'file' -and
        -not [string]::IsNullOrWhiteSpace([string]$metadata.license.'#text')) {
        "Package license file: $($metadata.license.'#text')"
    }
    elseif ($id -eq 'SQLite') {
        'Public Domain'
    }
    else {
        throw "No accepted license classification is available for $identity."
    }
    $repository = if ($metadata.repository) { [string]$metadata.repository.url } else { $null }
    $commit = if ($metadata.repository) { [string]$metadata.repository.commit } else { $null }
    $shaFile = Get-ChildItem $folder -Filter *.nupkg.sha512 | Select-Object -First 1
    if (-not $shaFile) {
        throw "No NuGet package SHA-512 sidecar was found for $identity."
    }
    $nupkgSha512 = (Get-Content -Raw $shaFile.FullName).Trim()
    if ([string]::IsNullOrWhiteSpace($nupkgSha512)) {
        throw "NuGet package SHA-512 sidecar was empty for $identity."
    }

    [pscustomobject]@{
        License = $license
        Repository = $repository
        Commit = $commit
        ProjectUrl = [string]$metadata.projectUrl
        NupkgSha512 = $nupkgSha512
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
        nupkgSha512 = $metadata.NupkgSha512
        use = if ($id -like 'MSTest.*' -or $id -eq 'Microsoft.NET.Test.Sdk') {
            'test-only'
        } elseif ($id -eq 'Grpc.Tools') {
            'build-time protobuf code generation'
        } else {
            'production runtime'
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
$groupedIdentities = @{}
foreach ($group in $curation.provenanceGroups) {
    foreach ($identity in $group.packages) {
        if (-not $resolved.ContainsKey([string]$identity)) {
            throw "Curated provenance identity '$identity' is absent from all lock files."
        }
        if ($groupedIdentities.ContainsKey([string]$identity)) {
            throw "Curated provenance identity '$identity' occurs in more than one group."
        }
        $groupedIdentities[[string]$identity] = $true
    }
}
foreach ($identity in ($resolved.Keys | Sort-Object)) {
    $metadata = $metadataByIdentity[$identity]
    if ($groupedIdentities.ContainsKey($identity)) {
        continue
    }
    elseif ($metadata.Repository -and $metadata.Commit) {
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

$manifest.revision = 'dependency-manifest/1'
$manifest.reviewedOn = '2026-08-12'
$manifest.scope = 'NuGet packages and .NET toolchain used by the current Infinium solution'
$manifest.lockIdentity.resolvedPackageCount = $resolvedEntries.Count
$manifest.lockIdentity.productionGraph = 'src/Infinium.Persistence/packages.lock.json'
$manifest.lockIdentity.emptyProjectLocks = 'Project-local locks without external package dependencies'
$manifest.directPackages = @($direct)
$manifest.resolvedPackages = @($resolvedEntries)
$manifest.provenanceGroups = @($curation.provenanceGroups)
$manifest.individuallyVerifiedProvenance = @($verified)
$manifest.explicitProvenanceLimitations = @($limitations)
$manifest.excludedDependencies = @(
    $manifest.excludedDependencies |
        Where-Object { $_ -ne 'SQLite binding' } |
        Sort-Object
)

$json = $manifest | ConvertTo-Json -Depth 20
$generated = $json + [Environment]::NewLine
if ($Check) {
    $current = [System.IO.File]::ReadAllText($manifestPath)
    if ($current -cne $generated) {
        throw "Dependency manifest is stale. Run eng/update-dependency-manifest.ps1."
    }
}
else {
    [System.IO.File]::WriteAllText(
        $manifestPath,
        $generated,
        [System.Text.UTF8Encoding]::new($false))
}
