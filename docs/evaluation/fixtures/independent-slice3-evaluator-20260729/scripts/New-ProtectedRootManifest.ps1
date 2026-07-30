[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OutputPath,

    [Parameter(Mandatory)]
    [string]$LiveMo2Root,

    [Parameter(Mandatory)]
    [string]$LiveGameRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$mo2Root = [IO.Path]::GetFullPath($LiveMo2Root)
$gameRoot = [IO.Path]::GetFullPath($LiveGameRoot)
$mo2InstanceData = @(
    (Join-Path $mo2Root 'Skyrim SE'),
    (Join-Path $mo2Root 'Morrowind')
)

function Get-CanonicalStructuralEntries {
    param(
        [Parameter(Mandatory)][string]$Root,
        [string[]]$ExcludedRoots = @()
    )

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $exclusions = @($ExcludedRoots | ForEach-Object {
        [IO.Path]::GetFullPath($_).TrimEnd('\') + '\'
    })

    Get-ChildItem -LiteralPath $rootFull -File -Recurse -Force -ErrorAction Stop |
        Where-Object {
            $candidate = $_.FullName
            -not ($exclusions | Where-Object {
                $candidate.StartsWith($_, [StringComparison]::OrdinalIgnoreCase)
            })
        } |
        Sort-Object FullName |
        ForEach-Object {
            [pscustomobject][ordered]@{
                relative_path = $_.FullName.Substring($rootFull.Length + 1).Replace('\', '/')
                byte_length = $_.Length
                last_write_utc_ticks = $_.LastWriteTimeUtc.Ticks
                attributes = [string]$_.Attributes
            }
        }
}

function Get-StructuralDigest {
    param([Parameter(Mandatory)]$Entries)

    $builder = [Text.StringBuilder]::new()
    foreach ($entry in $Entries) {
        [void]$builder.Append($entry.relative_path.Length)
        [void]$builder.Append(':')
        [void]$builder.Append($entry.relative_path)
        [void]$builder.Append('|')
        [void]$builder.Append($entry.byte_length)
        [void]$builder.Append('|')
        [void]$builder.Append($entry.last_write_utc_ticks)
        [void]$builder.Append('|')
        [void]$builder.Append($entry.attributes)
        [void]$builder.Append("`n")
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($builder.ToString())
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

$mo2AppEntries = @(Get-CanonicalStructuralEntries -Root $mo2Root -ExcludedRoots $mo2InstanceData)
$mo2SkyrimEntries = @(Get-CanonicalStructuralEntries -Root $mo2InstanceData[0])
$mo2MorrowindEntries = @(Get-CanonicalStructuralEntries -Root $mo2InstanceData[1])
$gameEntries = @(Get-CanonicalStructuralEntries -Root $gameRoot)

$manifest = [ordered]@{
    schema_id = 'infinium.eval.protected-root-manifest'
    schema_version = 1
    captured_at = [DateTimeOffset]::UtcNow.ToString('O')
    roots = @(
        [ordered]@{
            root_token = '<LIVE_MO2_APPLICATION_ROOT>'
            population = 'application-payload-excluding-portable-instance-data'
            file_count = $mo2AppEntries.Count
            total_bytes = ($mo2AppEntries | Measure-Object -Property byte_length -Sum).Sum
            structural_sha256 = Get-StructuralDigest -Entries $mo2AppEntries
            scoped_content = @(
                [ordered]@{
                    relative_path = 'ModOrganizer.exe'
                    byte_length = (Get-Item -LiteralPath (Join-Path $mo2Root 'ModOrganizer.exe')).Length
                    sha256 = (Get-FileHash -LiteralPath (Join-Path $mo2Root 'ModOrganizer.exe') -Algorithm SHA256).Hash
                },
                [ordered]@{
                    relative_path = 'ModOrganizer.ini'
                    byte_length = (Get-Item -LiteralPath (Join-Path $mo2Root 'ModOrganizer.ini')).Length
                    sha256 = (Get-FileHash -LiteralPath (Join-Path $mo2Root 'ModOrganizer.ini') -Algorithm SHA256).Hash
                }
            )
        },
        [ordered]@{
            root_token = '<LIVE_MO2_SKYRIM_INSTANCE_DATA_ROOT>'
            population = 'full-structural'
            file_count = $mo2SkyrimEntries.Count
            total_bytes = ($mo2SkyrimEntries | Measure-Object -Property byte_length -Sum).Sum
            structural_sha256 = Get-StructuralDigest -Entries $mo2SkyrimEntries
            scoped_content = @()
        },
        [ordered]@{
            root_token = '<LIVE_MO2_MORROWIND_INSTANCE_DATA_ROOT>'
            population = 'full-structural'
            file_count = $mo2MorrowindEntries.Count
            total_bytes = ($mo2MorrowindEntries | Measure-Object -Property byte_length -Sum).Sum
            structural_sha256 = Get-StructuralDigest -Entries $mo2MorrowindEntries
            scoped_content = @()
        },
        [ordered]@{
            root_token = '<LIVE_SKYRIM_GAME_ROOT>'
            population = 'full-structural-plus-executable-content'
            file_count = $gameEntries.Count
            total_bytes = ($gameEntries | Measure-Object -Property byte_length -Sum).Sum
            structural_sha256 = Get-StructuralDigest -Entries $gameEntries
            scoped_content = @(
                [ordered]@{
                    relative_path = 'SkyrimSE.exe'
                    byte_length = (Get-Item -LiteralPath (Join-Path $gameRoot 'SkyrimSE.exe')).Length
                    sha256 = (Get-FileHash -LiteralPath (Join-Path $gameRoot 'SkyrimSE.exe') -Algorithm SHA256).Hash
                }
            )
        }
    )
    assurance = [ordered]@{
        structure = 'complete for declared roots'
        content = 'scoped to MO2 executable, canonical portable INI, and Skyrim executable'
        excluded_from_content_sealing = 'large profile/mod/game populations'
        note = 'Structural identity is not a claim of full byte sealing.'
    }
}

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
[IO.File]::WriteAllText(
    $OutputPath,
    (($manifest | ConvertTo-Json -Depth 20) + "`n"),
    [Text.UTF8Encoding]::new($false)
)

Write-Output $OutputPath
