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

$devRoot = Join-Path $root 'S6-CANDIDATE-DEV-v2'
$valRoot = Join-Path $root 'S6-CANDIDATE-VAL-v2'
$branchMap = [ordered]@{
    'd02' = 'v08'
    'd03' = 'v09'
    'd04' = 'v10'
    'd05' = 'v11'
    'd06' = 'v12'
    'd07' = 'v13'
    'd08' = 'v14'
}

$devInput = Get-Content -LiteralPath (Join-Path $devRoot 'execution-input.v1.json') -Raw | ConvertFrom-Json
$valInput = Get-Content -LiteralPath (Join-Path $valRoot 'execution-input.v1.json') -Raw | ConvertFrom-Json
foreach ($mapping in $branchMap.GetEnumerator()) {
    $context = $devInput.contexts | Where-Object { $_.context_id -eq "context-$($mapping.Key)" }
    $json = ($context | ConvertTo-Json -Depth 64 -Compress).Replace([string]$mapping.Key, [string]$mapping.Value)
    $valInput.contexts += ($json | ConvertFrom-Json)
}
[System.IO.File]::WriteAllText(
    (Join-Path $valRoot 'execution-input.v1.json'),
    ($valInput | ConvertTo-Json -Depth 64) + "`n",
    [System.Text.UTF8Encoding]::new($false))

$devTranscripts = Get-Content -LiteralPath (Join-Path $devRoot 'retained-transcripts.v1.json') -Raw | ConvertFrom-Json
$valTranscripts = Get-Content -LiteralPath (Join-Path $valRoot 'retained-transcripts.v1.json') -Raw | ConvertFrom-Json
foreach ($mapping in $branchMap.GetEnumerator()) {
    $transcript = $devTranscripts.transcripts | Where-Object { $_.context_id -eq "context-$($mapping.Key)" }
    $json = $transcript | ConvertTo-Json -Depth 64 -Compress
    $json = $json.Replace([string]$mapping.Key, [string]$mapping.Value)
    $json = $json.Replace('operation-candidate-dev-1', 'operation-candidate-val-1')
    $valTranscripts.transcripts += ($json | ConvertFrom-Json)
}
[System.IO.File]::WriteAllText(
    (Join-Path $valRoot 'retained-transcripts.v1.json'),
    ($valTranscripts | ConvertTo-Json -Depth 64) + "`n",
    [System.Text.UTF8Encoding]::new($false))

$evidence = @($valInput.contexts | ForEach-Object { @($_.evidence) })
$manifest = [ordered]@{
    schema_id = 'infinium.llm.candidate-investigation-context/v1'
    schema_version = '1'
    analysis_run_id = [string]$valInput.analysis_run_id
    selection_policy = 'exact-declared-candidates-and-evidence-in-declared-order/v1'
    context_ids = @($valInput.contexts | ForEach-Object { [string]$_.context_id })
    candidate_ids = @($valInput.contexts | ForEach-Object { [string]$_.candidate_id })
    hypothesis_ids = @($valInput.contexts | ForEach-Object { [string]$_.hypothesis_id })
    dependency_closure_ids = @($valInput.contexts | ForEach-Object { [string]$_.dependency_closure_id })
    evidence_ids = @($evidence | ForEach-Object { [string]$_.evidence_id })
    evidence_fingerprints = @($evidence | ForEach-Object { [string]$_.content_sha256 })
}
[System.IO.File]::WriteAllText(
    (Join-Path $valRoot 'context-manifest.v1.json'),
    ($manifest | ConvertTo-Json -Depth 64 -Compress) + "`n",
    [System.Text.UTF8Encoding]::new($false))
