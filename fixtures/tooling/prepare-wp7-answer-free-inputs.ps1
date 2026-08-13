[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$root = Join-Path $repoRoot 'fixtures\public\provider\candidate-investigations'
$files = @('execution-input.v1.json', 'context-manifest.v1.json', 'retained-transcripts.v1.json')
$packages = @(
    [ordered]@{
        Source = 'S6-CANDIDATE-DEV-v1'
        Target = 'S6-CANDIDATE-DEV-v2'
        Replacements = [ordered]@{
            'dev-matched-negative' = 'd02'
            'dev-unavailable-provider' = 'd08'
            'dev-positive' = 'd01'
            'dev-conditional' = 'd03'
            'dev-unsupported' = 'd04'
            'dev-contradiction' = 'd05'
            'dev-abstention' = 'd06'
            'dev-no-model' = 'd07'
        }
    },
    [ordered]@{
        Source = 'S6-CANDIDATE-VAL-v1'
        Target = 'S6-CANDIDATE-VAL-v2'
        Replacements = [ordered]@{
            'val-positive-control' = 'v01'
            'val-hostile' = 'v02'
            'val-malformed' = 'v03'
            'val-refusal' = 'v04'
            'val-incomplete' = 'v05'
            'val-deleted' = 'v06'
            'val-drift' = 'v07'
        }
    }
)

foreach ($package in $packages) {
    $source = Join-Path $root $package.Source
    $target = Join-Path $root $package.Target
    [System.IO.Directory]::CreateDirectory($target) | Out-Null
    foreach ($name in $files) {
        $text = [System.IO.File]::ReadAllText((Join-Path $source $name))
        $text = $text.Replace($package.Source, $package.Target)
        foreach ($replacement in $package.Replacements.GetEnumerator()) {
            $text = $text.Replace([string]$replacement.Key, [string]$replacement.Value)
        }
        [System.IO.File]::WriteAllText(
            (Join-Path $target $name),
            $text,
            [System.Text.UTF8Encoding]::new($false))
    }
}
