[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Contracts', 'Documentation')]
    [string] $Gate,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

function Read-StrictJson([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required JSON file is missing: $Path"
    }

    try {
        return ([System.IO.File]::ReadAllText($Path) | ConvertFrom-Json -ErrorAction Stop)
    } catch {
        throw "Required JSON file is not valid JSON: $Path. $($_.Exception.Message)"
    }
}

function Get-FileEvidence([string] $Path) {
    $item = Get-Item -LiteralPath $Path
    $relativePath = $item.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
    return [ordered]@{
        path = $relativePath.Replace('\', '/')
        byte_length = $item.Length
        sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Write-GateReport([string] $Name, [System.Collections.IDictionary] $Body) {
    $report = [ordered]@{
        schema_id = 'infinium.verification.m1-slice5/v1'
        schema_version = '1'
        gate = $Name
        result = 'passed'
        verified_at = [DateTimeOffset]::UtcNow.ToString('O')
    }
    foreach ($entry in $Body.GetEnumerator()) {
        $report[$entry.Key] = $entry.Value
    }
    $path = Join-Path $resolvedOutputRoot ($Name.ToLowerInvariant() + '.json')
    $json = $report | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($path, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Host "$Name gate passed: $path"
}

function Invoke-ContractsGate {
    $schemaRoot = Join-Path $repoRoot 'contracts/json-schema'
    $requiredSchemas = [ordered]@{
        'documentation-evidence.v1.schema.json' = 'infinium.documentation.evidence/v1'
        'documentation-claim-import.v1.schema.json' = 'infinium.documentation.claim-import/v1'
        'candidate-analysis.v1.schema.json' = 'infinium.analysis.candidate/v1'
        'finding-case.v1.schema.json' = 'infinium.analysis.finding-case/v1'
        'analysis-replay.v1.schema.json' = 'infinium.analysis.replay/v1'
        'analysis-execution-input.v1.schema.json' = 'infinium.analysis.execution-input/v1'
        'analyzer-declaration.v1.schema.json' = 'infinium.analyzer.declaration/v1'
        'effective-scan-configuration.v1.schema.json' = 'infinium.scan.effective-configuration/v1'
        'run-output.v1.schema.json' = 'infinium.run-output/v1'
        'cli-summary.v1.schema.json' = 'infinium.cli-summary/v1'
        'fixture-execution-input.v1.schema.json' = $null
        'fixture-oracle.v1.schema.json' = $null
        'replay-dependencies.v1.schema.json' = $null
        'evaluation-assertion-result.v1.schema.json' = 'infinium.evaluation.assertion-result/v1'
    }

    $schemaEvidence = @()
    foreach ($entry in $requiredSchemas.GetEnumerator()) {
        $path = Join-Path $schemaRoot $entry.Key
        $schema = Read-StrictJson $path
        if ($schema.type -cne 'object' -or $schema.additionalProperties -ne $false) {
            throw "Schema $($entry.Key) is not a closed top-level object."
        }
        if ($null -ne $entry.Value) {
            $schemaId = $schema.properties.schema_id.const
            if ($schemaId -cne $entry.Value) {
                throw "Schema identity mismatch for $($entry.Key): $schemaId"
            }
        }
        $schemaEvidence += Get-FileEvidence $path
    }

    Get-ChildItem -LiteralPath $schemaRoot -Filter '*.json' -File | ForEach-Object {
        $null = Read-StrictJson $_.FullName
    }

    $protoPaths = @(
        (Join-Path $repoRoot 'contracts/protobuf/infinium/domain/v1/analysis.proto'),
        (Join-Path $repoRoot 'contracts/protobuf/infinium/application/v1/application.proto')
    )
    foreach ($path in $protoPaths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required protobuf contract is missing: $path"
        }
    }

    $authorityPath = Join-Path $repoRoot 'docs/evaluation/repository-evaluation-authority.v1.json'
    $retirementPath = Join-Path $repoRoot 'docs/evaluation/retired-evaluation-assets.v1.json'
    $authority = Read-StrictJson $authorityPath
    $retirement = Read-StrictJson $retirementPath
    foreach ($surface in @($authority.surfaces)) {
        foreach ($relativePath in @($surface.paths)) {
            if (-not (Test-Path -LiteralPath (Join-Path $repoRoot ([string] $relativePath)))) {
                throw "Repository authority surface is missing: $relativePath"
            }
        }
    }

    $sourceCommit = [string] $retirement.source_commit
    $retiredEntries = @($retirement.entries)
    foreach ($entry in $retiredEntries) {
        $relativePath = [string] $entry.path
        if (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath)) {
            throw "Retired evaluation asset remains active in the working tree: $relativePath"
        }
        $actualBlob = @(& git -C $repoRoot rev-parse "$($sourceCommit):$relativePath" 2>&1)
        if ($LASTEXITCODE -ne 0 -or $actualBlob.Count -ne 1 -or
            ([string] $actualBlob[0]).Trim() -cne [string] $entry.git_blob) {
            throw "Retired evaluation asset Git identity mismatch: $relativePath"
        }
        if ($null -ne $entry.replacement -and
            -not (Test-Path -LiteralPath (Join-Path $repoRoot ([string] $entry.replacement)))) {
            throw "Retired evaluation asset replacement is missing: $($entry.replacement)"
        }
    }

    $solutionText = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'Infinium.sln'))
    if ($solutionText.IndexOf('Infinium.EvaluatorV2', [StringComparison]::Ordinal) -ge 0 -or
        $solutionText.IndexOf('Infinium.Protocol4RegressionTests', [StringComparison]::Ordinal) -ge 0) {
        throw 'The default solution reaches a historical evaluator project.'
    }

    Write-GateReport 'Contracts' ([ordered]@{
        schema_count = $schemaEvidence.Count
        parsed_json_schema_count = @(Get-ChildItem -LiteralPath $schemaRoot -Filter '*.json' -File).Count
        schemas = $schemaEvidence
        protobuf = @($protoPaths | ForEach-Object { Get-FileEvidence $_ })
        compatibility_shape = 'clean-break-single-current-shape'
        repository_authority_manifest = Get-FileEvidence $authorityPath
        retired_asset_manifest = Get-FileEvidence $retirementPath
        retired_git_blob_count = $retiredEntries.Count
        historical_evaluator_in_default_solution = $false
    })
}

function Invoke-DocumentationGate {
    $requiredPaths = @(
        'contracts/json-schema/documentation-claim-import.v1.schema.json',
        'contracts/json-schema/documentation-evidence.v1.schema.json',
        'src/Infinium.Analysis/Documentation/DocumentationEvidenceImporter.cs',
        'src/Infinium.Application/Documentation/DocumentationEvidencePhase.cs',
        'test-data/evaluation/m1-semantic/DOC-WP2-CORE-DEV',
        'test-data/evaluation/m1-semantic/DOC-WP2-ADVERSARIAL-VAL'
    )
    foreach ($relativePath in $requiredPaths) {
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath))) {
            throw "Required WP2 documentation surface is missing: $relativePath"
        }
    }

    $fixtureRoots = @(
        (Join-Path $repoRoot 'test-data/evaluation/m1-semantic/DOC-WP2-CORE-DEV'),
        (Join-Path $repoRoot 'test-data/evaluation/m1-semantic/DOC-WP2-ADVERSARIAL-VAL')
    )
    $fixtureEvidence = @()
    foreach ($fixtureRoot in $fixtureRoots) {
        foreach ($file in Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File | Sort-Object FullName) {
            if ($file.Extension -ceq '.json') {
                $null = Read-StrictJson $file.FullName
            }
            $fixtureEvidence += Get-FileEvidence $file.FullName
        }
    }

    $documentationSource = [System.IO.File]::ReadAllText(
        (Join-Path $repoRoot 'src/Infinium.Analysis/Documentation/DocumentationEvidenceImporter.cs'))
    foreach ($forbidden in @('HttpClient', 'OpenAIClient', 'NexusClient', 'Process.Start', 'PowerShell.Create')) {
        if ($documentationSource.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) {
            throw "WP2 deterministic documentation source reaches forbidden capability: $forbidden"
        }
    }

    $testCommands = @(
        @('test', 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~DocumentationSource|FullyQualifiedName~ClaimImport'),
        @('test', 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~DocumentationEvidence|FullyQualifiedName~CleanLayers'),
        @('test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~EvidenceTypes|FullyQualifiedName~ProvenanceLocal|FullyQualifiedName~UntrustedDocumentation')
    )
    $testResults = @()
    foreach ($arguments in $testCommands) {
        $testOutput = @(& dotnet @arguments 2>&1)
        $testOutput | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            throw "WP2 focused verification failed: dotnet $($arguments -join ' ')"
        }
        $testTranscript = $testOutput -join [Environment]::NewLine
        if ($testTranscript -notmatch 'Total:\s+[1-9][0-9]*') {
            throw "WP2 focused verification matched zero tests: dotnet $($arguments -join ' ')"
        }
        $testResults += "dotnet $($arguments -join ' ')"
    }

    Write-GateReport 'Documentation' ([ordered]@{
        deterministic_importer = Get-FileEvidence (Join-Path $repoRoot 'src/Infinium.Analysis/Documentation/DocumentationEvidenceImporter.cs')
        phase_adapter = Get-FileEvidence (Join-Path $repoRoot 'src/Infinium.Application/Documentation/DocumentationEvidencePhase.cs')
        fixture_file_count = $fixtureEvidence.Count
        fixture_files = $fixtureEvidence
        focused_test_commands = $testResults
        llm_involvement = 'none'
        provider_search_nexus_loot = 'not-used'
        private_fixture_access = 'not-used'
    })
}

Push-Location $repoRoot
try {
    if ($Gate -ceq 'Contracts') {
        Invoke-ContractsGate
    } else {
        Invoke-DocumentationGate
    }
} finally {
    Pop-Location
}
