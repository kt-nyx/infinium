[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EvaluatorRoot,

    [Parameter(Mandatory)]
    [string]$CopiedMo2Root,

    [Parameter(Mandatory)]
    [string]$PublicPackageRoot,

    [Parameter(Mandatory)]
    [string]$LiveGameRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedMo2Hash = '442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622'
$expectedGameHash = 'C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9'
$liveGameRootFull = [IO.Path]::GetFullPath($LiveGameRoot)
$liveGameExe = Join-Path $liveGameRootFull 'SkyrimSE.exe'
$liveGameData = Join-Path $liveGameRootFull 'Data'
$copiedMo2Exe = Join-Path $CopiedMo2Root 'ModOrganizer.exe'

function Assert-DescendantPath {
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Root
    )

    $candidateFull = [IO.Path]::GetFullPath($Candidate)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $candidateFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$candidateFull' is not beneath evaluator root '$rootFull'."
    }
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )

    $parent = Split-Path -Parent $Path
    if ($parent) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function Copy-And-AssertHash {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][string]$ExpectedHash
    )

    New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    $actual = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if ($actual -ne $ExpectedHash) {
        throw "Copied file hash mismatch for '$Destination': $actual"
    }
}

function New-JsonFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 100
    Write-Utf8NoBom -Path $Path -Value ($json + "`n")
}

function Get-RelativeFileFacts {
    param([Parameter(Mandatory)][string]$Root)

    $resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse -Force |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                relative_path = $_.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                byte_length = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                attributes = [string]$_.Attributes
                last_write_utc = $_.LastWriteTimeUtc.ToString('O')
            }
        }
}

$rootFull = [IO.Path]::GetFullPath($EvaluatorRoot)
$mo2Full = [IO.Path]::GetFullPath($CopiedMo2Root)
Assert-DescendantPath -Candidate $mo2Full -Root $rootFull

if (-not (Test-Path -LiteralPath $copiedMo2Exe -PathType Leaf)) {
    throw "Copied MO2 executable is missing: $copiedMo2Exe"
}

$mo2Hash = (Get-FileHash -LiteralPath $copiedMo2Exe -Algorithm SHA256).Hash
if ($mo2Hash -ne $expectedMo2Hash) {
    throw "Copied MO2 executable hash mismatch: $mo2Hash"
}

$instanceRoot = Join-Path $rootFull 'instance'
$gameRoot = Join-Path $rootFull 'game-root'
$targetRoot = Join-Path $rootFull 'target-matrix'
$evidenceRoot = Join-Path $rootFull 'evidence'
$outputRoot = Join-Path $rootFull 'output'

foreach ($path in @(
    $instanceRoot,
    (Join-Path $instanceRoot 'profiles'),
    (Join-Path $instanceRoot 'mods'),
    (Join-Path $instanceRoot 'downloads'),
    (Join-Path $instanceRoot 'cache'),
    (Join-Path $instanceRoot 'overwrite'),
    (Join-Path $instanceRoot 'logs'),
    $gameRoot,
    (Join-Path $gameRoot 'Data'),
    $targetRoot,
    $evidenceRoot,
    $outputRoot
)) {
    Assert-DescendantPath -Candidate $path -Root $rootFull
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

Copy-And-AssertHash `
    -Source $liveGameExe `
    -Destination (Join-Path $gameRoot 'SkyrimSE.exe') `
    -ExpectedHash $expectedGameHash

foreach ($masterName in @(
    'Skyrim.esm',
    'Update.esm',
    'Dawnguard.esm',
    'HearthFires.esm',
    'Dragonborn.esm'
)) {
    $sourceMaster = Join-Path $liveGameData $masterName
    $masterHash = (Get-FileHash -LiteralPath $sourceMaster -Algorithm SHA256).Hash
    Copy-And-AssertHash `
        -Source $sourceMaster `
        -Destination (Join-Path $gameRoot "Data\$masterName") `
        -ExpectedHash $masterHash
}

$profileExplicit = Join-Path $instanceRoot 'profiles\Explicit Target'
$profileSaved = Join-Path $instanceRoot 'profiles\Saved Suggestion'
New-Item -ItemType Directory -Path $profileExplicit,$profileSaved -Force | Out-Null

$modAlpha = Join-Path $instanceRoot 'mods\01 Physical Alpha'
$modBeta = Join-Path $instanceRoot 'mods\02 Physical Beta Renamed'
$modDisabled = Join-Path $instanceRoot 'mods\03 Disabled Provider'
$modDuplicate = Join-Path $instanceRoot 'mods\04 Duplicate Source Mapping'
New-Item -ItemType Directory -Path $modAlpha,$modBeta,$modDisabled,$modDuplicate -Force | Out-Null

Write-Utf8NoBom -Path (Join-Path $gameRoot 'Data\meshes\oracle\shared.txt') -Value "unmanaged-base`n"
Write-Utf8NoBom -Path (Join-Path $gameRoot 'Data\meshes\oracle\unmanaged-only.txt') -Value "unmanaged-only`n"
Write-Utf8NoBom -Path (Join-Path $gameRoot 'Data\archives\oracle-archive.bsa') -Value "project-authored archive placeholder; archive members unsupported`n"

Write-Utf8NoBom -Path (Join-Path $modAlpha 'meshes\oracle\shared.txt') -Value "alpha-provider`n"
Write-Utf8NoBom -Path (Join-Path $modAlpha 'meshes\oracle\alpha-only.txt') -Value "alpha-only`n"
Write-Utf8NoBom -Path (Join-Path $modAlpha 'Meshes\Case\File.TXT') -Value "alpha-case-provider`n"
Copy-Item -LiteralPath (Join-Path $gameRoot 'Data\Update.esm') -Destination (Join-Path $modAlpha 'FixturePlugin.esm') -Force
Write-Utf8NoBom -Path (Join-Path $modAlpha 'meta.ini') -Value @"
[General]
gameName=Skyrim Special Edition
modid=4242
version=1.0.0
installationFile=alpha-source.zip
repository=Nexus

[installedFiles]
1\modid=4242
1\fileid=100
size=1
"@

Write-Utf8NoBom -Path (Join-Path $modBeta 'meshes\oracle\shared.txt') -Value "beta-provider`n"
Write-Utf8NoBom -Path (Join-Path $modBeta 'meshes\oracle\beta-only.txt') -Value "beta-only`n"
Write-Utf8NoBom -Path (Join-Path $modBeta 'meshes\case\file.txt') -Value "beta-case-provider`n"
Write-Utf8NoBom -Path (Join-Path $modBeta 'meshes\oracle\hidden-only.txt.mohidden') -Value "physically-present-hidden`n"
Write-Utf8NoBom -Path (Join-Path $modBeta '.git\ignored-by-directory.txt') -Value "physically-present-skipped-directory`n"
Copy-Item -LiteralPath (Join-Path $gameRoot 'Data\Update.esm') -Destination (Join-Path $modBeta 'FixturePlugin.esm') -Force
$betaPlugin = Join-Path $modBeta 'FixturePlugin.esm'
$betaStream = [IO.File]::Open($betaPlugin, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $betaStream.Position = $betaStream.Length - 1
    $betaPluginByte = $betaStream.ReadByte()
    $betaStream.Position = $betaStream.Length - 1
    $betaStream.WriteByte($betaPluginByte -bxor 0x01)
    $betaStream.Flush($true)
}
finally {
    $betaStream.Dispose()
}
Write-Utf8NoBom -Path (Join-Path $modBeta 'meta.ini') -Value @"
[General]
gameName=Skyrim Special Edition
modid=4242
version=9.9.0
installationFile=beta-source.zip
repository=Nexus

[installedFiles]
1\modid=4242
1\fileid=200
size=1
"@

Write-Utf8NoBom -Path (Join-Path $modDisabled 'meshes\oracle\shared.txt') -Value "disabled-provider`n"
Write-Utf8NoBom -Path (Join-Path $modDisabled 'meshes\oracle\disabled-only.txt') -Value "disabled-only`n"
Copy-Item -LiteralPath (Join-Path $gameRoot 'Data\Dawnguard.esm') -Destination (Join-Path $modDisabled 'FixtureDisabled.esm') -Force
Write-Utf8NoBom -Path (Join-Path $modDisabled 'meta.ini') -Value @"
[General]
gameName=Skyrim Special Edition
modid=7001
version=1.0.0
repository=Nexus
"@

Write-Utf8NoBom -Path (Join-Path $modDuplicate 'meshes\oracle\duplicate-source-only.txt') -Value "duplicate-source-local-entity`n"
Write-Utf8NoBom -Path (Join-Path $modDuplicate 'meta.ini') -Value @"
[General]
gameName=Skyrim Special Edition
modid=4242
version=1.0.0
installationFile=alpha-source.zip
repository=Nexus

[installedFiles]
1\modid=4242
1\fileid=100
size=1
"@

Write-Utf8NoBom -Path (Join-Path $instanceRoot 'overwrite\meshes\oracle\shared.txt') -Value "overwrite-provider`n"
Write-Utf8NoBom -Path (Join-Path $instanceRoot 'overwrite\meshes\oracle\overwrite-only.txt') -Value "overwrite-only`n"

$modList = @"
# This file was generated for an independent disposable EVAL-0051 oracle.
+04 Duplicate Source Mapping
+02 Physical Beta Renamed
-03 Disabled Provider
+01 Physical Alpha
"@

$plugins = @"
# This file was generated for an independent disposable EVAL-0051 oracle.
*Skyrim.esm
*Update.esm
*Dawnguard.esm
*HearthFires.esm
*Dragonborn.esm
*FixturePlugin.esm
FixtureDisabled.esm
"@

$loadOrder = @"
# This file was generated for an independent disposable EVAL-0051 oracle.
Skyrim.esm
Update.esm
Dawnguard.esm
HearthFires.esm
Dragonborn.esm
FixturePlugin.esm
FixtureDisabled.esm
"@

foreach ($profile in @($profileExplicit, $profileSaved)) {
    Write-Utf8NoBom -Path (Join-Path $profile 'modlist.txt') -Value $modList
    Write-Utf8NoBom -Path (Join-Path $profile 'plugins.txt') -Value $plugins
    Write-Utf8NoBom -Path (Join-Path $profile 'loadorder.txt') -Value $loadOrder
    Write-Utf8NoBom -Path (Join-Path $profile 'archives.txt') -Value "oracle-archive.bsa`n"
}

$baseDirectoryIni = $instanceRoot.Replace('\', '/')
$gamePathIni = $gameRoot.Replace('\', '\\')
$sanitizedIni = @"
[General]
gameName=Skyrim Special Edition
selected_profile=@ByteArray(Saved Suggestion)
gamePath=@ByteArray($gamePathIni)
first_start=false
archive_parsing=false

[Settings]
base_directory=$baseDirectoryIni
skip_file_suffixes=.mohidden
skip_directories=.git
"@
Write-Utf8NoBom -Path (Join-Path $CopiedMo2Root 'ModOrganizer.ini') -Value $sanitizedIni

$exactTarget = Join-Path $targetRoot 'exact\SkyrimSE.exe'
$oneByte = Join-Path $targetRoot 'one-byte-mutation\SkyrimSE.exe'
$sameVersionUnknown = Join-Path $targetRoot 'same-version-unknown-hash\SkyrimSE.exe'
$inconsistentBytes = Join-Path $targetRoot 'inconsistent-metadata-hash\SkyrimSE.exe'

Copy-And-AssertHash -Source $liveGameExe -Destination $exactTarget -ExpectedHash $expectedGameHash
Copy-And-AssertHash -Source $exactTarget -Destination $oneByte -ExpectedHash $expectedGameHash
Copy-And-AssertHash -Source $exactTarget -Destination $sameVersionUnknown -ExpectedHash $expectedGameHash
Copy-And-AssertHash -Source $exactTarget -Destination $inconsistentBytes -ExpectedHash $expectedGameHash

$mutationOffsets = [ordered]@{
    $oneByte = 1
    $sameVersionUnknown = 2
}
foreach ($path in $mutationOffsets.Keys) {
    $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $offset = $mutationOffsets[$path]
        $stream.Position = $stream.Length - $offset
        $value = $stream.ReadByte()
        $stream.Position = $stream.Length - $offset
        $stream.WriteByte($value -bxor 0x01)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
    (Get-Item -LiteralPath $path).LastWriteTimeUtc = (Get-Item -LiteralPath $exactTarget).LastWriteTimeUtc
}

$malformed = Join-Path $targetRoot 'malformed-pe\SkyrimSE.exe'
$malformedBytes = [byte[]]::new(64)
$malformedBytes[0] = 0x4D
$malformedBytes[1] = 0x5A
$malformedBytes[60] = 0xFF
$malformedBytes[61] = 0xFF
$malformedBytes[62] = 0xFF
$malformedBytes[63] = 0x7F
New-Item -ItemType Directory -Path (Split-Path -Parent $malformed) -Force | Out-Null
[IO.File]::WriteAllBytes($malformed, $malformedBytes)

$race = Join-Path $targetRoot 'capture-race\SkyrimSE.exe'
Copy-And-AssertHash -Source $liveGameExe -Destination $race -ExpectedHash $expectedGameHash

$accessDenied = Join-Path $targetRoot 'access-denied\SkyrimSE.exe'
Copy-And-AssertHash -Source $liveGameExe -Destination $accessDenied -ExpectedHash $expectedGameHash

$descriptors = [ordered]@{
    schema_id = 'infinium.eval.slice3.target-negative-matrix'
    schema_version = 1
    support_manifest = [ordered]@{
        target_id = 'skyrimse-steam-windows-x64-1.6.1170.0'
        executable_name = 'SkyrimSE.exe'
        byte_length = 37157144
        sha256 = $expectedGameHash
        pe_machine = '0x8664'
        pe_optional_magic = '0x020B'
        pe_subsystem = '0x0002'
        fixed_file_version = '1.6.1170.0'
        fixed_product_version = '1.6.1170.0'
        channel = 'steam'
        steam_app_id = 489830
    }
    cases = @(
        [ordered]@{ id = 'exact'; path = 'exact/SkyrimSE.exe'; expected_state = 'supported-exact' },
        [ordered]@{ id = 'one-byte-mutation'; path = 'one-byte-mutation/SkyrimSE.exe'; expected_state = 'unrecognized-build'; apparent_version = '1.6.1170.0' },
        [ordered]@{ id = 'same-version-unknown-hash'; path = 'same-version-unknown-hash/SkyrimSE.exe'; expected_state = 'unrecognized-build'; apparent_version = '1.6.1170.0' },
        [ordered]@{ id = 'known-unsupported-channel'; path = $null; expected_state = 'unsupported-known'; declared_channel = 'gog'; declared_runtime = '1.6.1179.0' },
        [ordered]@{ id = 'malformed-pe'; path = 'malformed-pe/SkyrimSE.exe'; expected_state = 'indeterminate'; exact_reason = 'malformed-or-truncated-pe' },
        [ordered]@{ id = 'missing'; path = 'missing/SkyrimSE.exe'; expected_state = 'indeterminate'; exact_reason = 'missing-input' },
        [ordered]@{ id = 'access-denied'; path = 'access-denied/SkyrimSE.exe'; expected_state = 'indeterminate'; exact_reason = 'access-denied'; setup = 'apply disposable ACL with scripts/Set-AccessDeniedFixture.ps1' },
        [ordered]@{ id = 'inconsistent-metadata-hash'; path = 'inconsistent-metadata-hash/SkyrimSE.exe'; expected_state = 'inconsistent-metadata'; declared_sha256 = $expectedGameHash; declared_fixed_version = '0.0.0.0' },
        [ordered]@{ id = 'capture-race'; path = 'capture-race/SkyrimSE.exe'; expected_state = 'invalidated'; setup = 'mutate only this disposable copy during guarded capture' },
        [ordered]@{ id = 'unsupported-manager'; path = 'exact/SkyrimSE.exe'; expected_state = 'unsupported-target'; declared_manager = 'not-mo2' },
        [ordered]@{ id = 'unsupported-platform'; path = 'exact/SkyrimSE.exe'; expected_state = 'unsupported-target'; declared_platform = 'linux-x64' },
        [ordered]@{ id = 'unsupported-architecture'; path = 'exact/SkyrimSE.exe'; expected_state = 'unsupported-target'; declared_platform = 'windows-arm64' }
    )
}
New-JsonFile -Path (Join-Path $targetRoot 'matrix.json') -Value $descriptors

$directFacts = [ordered]@{
    schema_id = 'infinium.eval.slice3.direct-physical-facts'
    schema_version = 1
    observed_at = [DateTimeOffset]::UtcNow.ToString('O')
    evaluator_root_token = '<EVALUATOR_ROOT>'
    copied_mo2 = [ordered]@{
        relative_path = 'mo2-app/ModOrganizer.exe'
        byte_length = (Get-Item -LiteralPath $copiedMo2Exe).Length
        sha256 = $mo2Hash
        file_version = (Get-Item -LiteralPath $copiedMo2Exe).VersionInfo.FileVersion
    }
    disposable_game = [ordered]@{
        relative_path = 'game-root/SkyrimSE.exe'
        byte_length = (Get-Item -LiteralPath (Join-Path $gameRoot 'SkyrimSE.exe')).Length
        sha256 = (Get-FileHash -LiteralPath (Join-Path $gameRoot 'SkyrimSE.exe') -Algorithm SHA256).Hash
        file_version = (Get-Item -LiteralPath (Join-Path $gameRoot 'SkyrimSE.exe')).VersionInfo.FileVersion
    }
    explicit_profile = 'Explicit Target'
    saved_selection = 'Saved Suggestion'
    file_facts = @(Get-RelativeFileFacts -Root $instanceRoot)
    game_fixture_facts = @(Get-RelativeFileFacts -Root (Join-Path $gameRoot 'Data'))
    target_negative_facts = @(Get-RelativeFileFacts -Root $targetRoot)
    expected_absent_paths = @(
        'instance/mods/02 Physical Beta Renamed/meshes/oracle/deleted-currently-absent.txt',
        'target-matrix/missing/SkyrimSE.exe'
    )
    direct_fact_limits = @(
        'Direct physical facts do not establish MO2 UI/VFS winners.',
        'Source metadata is mutable mapping evidence and does not collapse local entities.',
        'Archive bytes are a declared unsupported population for this package.'
    )
}
New-JsonFile -Path (Join-Path $evidenceRoot 'direct-physical-facts.private.json') -Value $directFacts

$publicFacts = $directFacts
$publicFacts.observed_at = '<RECORDED-IN-PRIVATE-EVIDENCE>'
New-JsonFile -Path (Join-Path $PublicPackageRoot 'direct-physical-facts.json') -Value $publicFacts

$commandRecord = [ordered]@{
    schema_id = 'infinium.eval.slice3.command-record'
    schema_version = 1
    generated_at = [DateTimeOffset]::UtcNow.ToString('O')
    commands = @(
        'Get-FileHash -Algorithm SHA256 <LIVE_MO2_ROOT>\ModOrganizer.exe',
        'Get-FileHash -Algorithm SHA256 <LIVE_GAME_ROOT>\SkyrimSE.exe',
        'robocopy <LIVE_MO2_APPLICATION_ROOT> <EVALUATOR_ROOT>\mo2-app /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:16 /XD <LIVE_INSTANCE_DATA_DIRS>',
        '& scripts\New-ProtectedRootManifest.ps1 -OutputPath <EVALUATOR_ROOT>\evidence\protected-before.json',
        '& scripts\Initialize-Slice3Oracle.ps1 -EvaluatorRoot <EVALUATOR_ROOT> -CopiedMo2Root <EVALUATOR_ROOT>\mo2-app -PublicPackageRoot <PACKAGE_ROOT>',
        '& scripts\New-EvaluatorRootManifest.ps1 -EvaluatorRoot <EVALUATOR_ROOT> -OutputPath <EVALUATOR_ROOT>\evidence\evaluator-before-ui.json',
        '<MANDATORY COMPUTER-USE UI OBSERVATION AGAINST COPIED EXECUTABLE ONLY>',
        '& scripts\New-EvaluatorRootManifest.ps1 -EvaluatorRoot <EVALUATOR_ROOT> -OutputPath <EVALUATOR_ROOT>\evidence\evaluator-after-ui.json',
        '& scripts\New-ProtectedRootManifest.ps1 -OutputPath <EVALUATOR_ROOT>\evidence\protected-after.json',
        '& scripts\Compare-ProtectedRootManifests.ps1 -BeforePath ... -AfterPath ... -OutputPath ...'
    )
    notes = @(
        'The live executables are never launched.',
        'All mutation targets are disposable copies.',
        'The copied ModOrganizer.ini contains only evaluator-root paths before launch; Settings/base_directory owns the disposable instance.',
        'UI observation remains a separate authority surface from direct physical facts.'
    )
}
New-JsonFile -Path (Join-Path $PublicPackageRoot 'command-record.json') -Value $commandRecord

Write-Output ([ordered]@{
    evaluator_root = $rootFull
    copied_mo2_executable = $copiedMo2Exe
    disposable_game_executable = (Join-Path $gameRoot 'SkyrimSE.exe')
    disposable_instance_root = $instanceRoot
    target_matrix_root = $targetRoot
    direct_facts = (Join-Path $evidenceRoot 'direct-physical-facts.private.json')
} | ConvertTo-Json -Depth 10)
