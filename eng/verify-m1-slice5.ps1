[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Contracts', 'Documentation', 'Candidates', 'CandidateScale', 'Cases', 'Replay', 'Output', 'Safety')]
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

function Invoke-FocusedTests([object[]] $Commands, [string] $FailurePrefix) {
    $results = @()
    foreach ($arguments in $Commands) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $testOutput = @(& dotnet @arguments 2>&1)
        $stopwatch.Stop()
        $testOutput | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            throw "$FailurePrefix failed: dotnet $($arguments -join ' ')"
        }
        $testTranscript = $testOutput -join [Environment]::NewLine
        $totalMatch = [regex]::Match($testTranscript, 'Total:\s+([1-9][0-9]*)')
        if (-not $totalMatch.Success) {
            throw "$FailurePrefix matched zero tests: dotnet $($arguments -join ' ')"
        }
        $passedMatch = [regex]::Match($testTranscript, 'Passed:\s+([0-9]+)')
        $results += [ordered]@{
            command = "dotnet $($arguments -join ' ')"
            elapsed_milliseconds = $stopwatch.ElapsedMilliseconds
            matched_tests = [int]$totalMatch.Groups[1].Value
            passed_tests = if ($passedMatch.Success) { [int]$passedMatch.Groups[1].Value } else { $null }
        }
    }
    return $results
}

function Invoke-ContractsGate {
    $schemaRoot = Join-Path $repoRoot 'contracts/json-schema'
    $requiredSchemas = [ordered]@{
        'documentation-evidence.v1.schema.json' = 'infinium.documentation.evidence/v1'
        'documentation-claim-import.v1.schema.json' = 'infinium.documentation.claim-import/v1'
        'candidate-analysis.v1.schema.json' = 'infinium.analysis.candidate/v1'
        'candidate-delivered-input.v1.schema.json' = 'infinium.analysis.candidate-delivered-input/v1'
        'candidate-delivered-expansion.v1.schema.json' = 'infinium.analysis.candidate-delivered-expansion/v1'
        'finding-case.v1.schema.json' = 'infinium.analysis.finding-case/v1'
        'finding-case-input.v1.schema.json' = 'infinium.analysis.finding-case-input/v1'
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

function Invoke-CandidatesGate {
    $fixtureRoot = Join-Path $repoRoot 'docs/evaluation/fixtures/m1-slice5-wp3-candidates-v1'
    $semanticRoot = Join-Path $fixtureRoot 'CAND-WP3-SEMANTIC-DEV-v1'
    $requiredPaths = @(
        'contracts/json-schema/candidate-analysis.v1.schema.json',
        'contracts/json-schema/candidate-delivered-input.v1.schema.json',
        'contracts/json-schema/candidate-delivered-expansion.v1.schema.json',
        'src/Infinium.Analysis/Candidates/CandidatePipeline.cs',
        'src/Infinium.Application/Candidates/DeliveredIndexCandidatePopulationSource.cs',
        'src/Infinium.Application/Candidates/CandidateDeliveredInputExpander.cs',
        'src/Infinium.Application/Candidates/CandidateAnalysisPhase.cs',
        'src/Infinium.Persistence/AuthoritativeStore.Candidates.cs',
        'docs/evaluation/fixtures/m1-slice5-wp3-candidates-v1/CAND-WP3-SEMANTIC-DEV-v1/public-manifest.json',
        'docs/evaluation/fixtures/m1-slice5-wp3-candidates-v1/CAND-WP3-SEMANTIC-DEV-v1/inputs/candidate-delivered-input.json',
        'docs/evaluation/fixtures/m1-slice5-wp3-candidates-v1/CAND-WP3-SEMANTIC-DEV-v1/oracle/semantic-population-projection.json'
    )
    foreach ($relativePath in $requiredPaths) {
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath))) {
            throw "Required WP3 candidate surface is missing: $relativePath"
        }
    }

    $manifestHash = (Get-FileHash -LiteralPath (Join-Path $semanticRoot 'public-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($manifestHash -cne '94799a0d9fd5c90594d5da7074297fe257e44aad69b98487bdc7ea5619370afb') {
        throw "Frozen WP3 semantic manifest hash mismatch: $manifestHash"
    }

    $candidateSource = @(
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Infinium.Analysis/Candidates') -File -Filter '*.cs'
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Infinium.Application/Candidates') -File -Filter '*.cs'
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Persistence/AuthoritativeStore.Candidates.cs')
    ) | ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }
    $candidateSourceText = $candidateSource -join [Environment]::NewLine
    foreach ($forbidden in @(
        'HttpClient', 'OpenAIClient', 'NexusClient', 'Process.Start', 'PowerShell.Create',
        'CAND-WP3-SEMANTIC-DEV-v1', 'CAND-WP3-SCALE-VAL-v1', 'CAND-WP3-STRESS-DEV-v1',
        'expected_disposition', 'expected_candidates', 'semantic-population-projection')) {
        if ($candidateSourceText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "WP3 product candidate graph reaches forbidden capability or fixture vocabulary: $forbidden"
        }
    }

    $testCommands = @(
        @('test', 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~CandidateDeliveredInputContractTests|FullyQualifiedName~Slice5Contract'),
        @('test', 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~CandidateSelector|FullyQualifiedName~DeliveredCandidateSource|FullyQualifiedName~FindingThreshold'),
        @('test', 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~CandidatePipeline|FullyQualifiedName~CandidateCheckpoint'),
        @('test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~SemanticPackageMatchesTheFrozenIndependentProjectionExactly')
    )
    $testResults = Invoke-FocusedTests $testCommands 'WP3 candidate focused verification'
    $driver = [System.Diagnostics.Process]::GetCurrentProcess()
    $driver.Refresh()

    Write-GateReport 'Candidates' ([ordered]@{
        fixture_identity = 'CAND-WP3-SEMANTIC-DEV-v1'
        fixture_version = '1.0.0'
        fixture_partition = 'development'
        public_manifest_sha256 = $manifestHash
        factual_population = 16
        admitted_count = 5
        ambiguous_count = 5
        resolved_negative_count = 4
        unsupported_count = 2
        candidate_count = 10
        hypothesis_count = 10
        abstention_count = 7
        metamorph_classes = @('rename', 'reorder', 'relevant-evidence', 'rank-only', 'unrelated-insertion', 'true-dependency')
        checkpoint_invalidation = 'run-execution-analyzer-policy-threshold-limit-frontier-and-member-fingerprints'
        focused_tests = $testResults
        verification_driver_peak_working_set_bytes = $driver.PeakWorkingSet64
        provider_model_private_fixture = 'not-used'
    })
}

function Invoke-CandidateScaleGate {
    $fixtureRoot = Join-Path $repoRoot 'docs/evaluation/fixtures/m1-slice5-wp3-candidates-v1'
    $scaleManifest = (Get-FileHash -LiteralPath (Join-Path $fixtureRoot 'CAND-WP3-SCALE-VAL-v1/public-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    $stressManifest = (Get-FileHash -LiteralPath (Join-Path $fixtureRoot 'CAND-WP3-STRESS-DEV-v1/public-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($scaleManifest -cne '98e1f3bcb88e40c52abbbddc62ed9f3d613e90d09c4a15d51be081bc8a1bf2c8' -or
        $stressManifest -cne '5b5507622d217223aa2a28a049d5c82b7e411238aaa6c10f415f27c594d1ebbf') {
        throw 'Frozen WP3 scale/stress manifest hash mismatch.'
    }
    $testCommands = @(
        @('test', 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~FrozenCandidatePackagesHaveExactClosedManifestsAndProductInputs'),
        @('test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~ValidationScaleUsesTheRealSourceAndStaysWithinThePublicationBoundary|FullyQualifiedName~StressPackageStreamsTheSameRecipeWithoutMaterializingThePopulation')
    )
    $testResults = Invoke-FocusedTests $testCommands 'WP3 candidate scale verification'
    $driver = [System.Diagnostics.Process]::GetCurrentProcess()
    $driver.Refresh()
    Write-GateReport 'CandidateScale' ([ordered]@{
        fixture_identity = 'CAND-WP3-SCALE-VAL-v1'
        fixture_version = '1.0.0'
        fixture_partition = 'validation'
        public_manifest_sha256 = $scaleManifest
        stress_fixture_identity = 'CAND-WP3-STRESS-DEV-v1'
        stress_public_manifest_sha256 = $stressManifest
        profiles = @(
            [ordered]@{
                profile_id = 'validation-scale'; factual_rows = 3200
                admitted = 940; ambiguous = 820; resolved_negative = 940; unsupported = 500
                candidates = 1760; hypotheses = 1760; abstentions = 1320
                semantic_stream_sha256 = 'b3e51f9a61042cf5038b0ac25e353929db86e96381606c3599cd63f7175cdb25'
                independent_projection_bytes = 2047092
            },
            [ordered]@{
                profile_id = 'streaming-stress'; factual_rows = 1000000
                admitted = 293750; ambiguous = 256250; resolved_negative = 293750; unsupported = 156250
                candidates = 550000; hypotheses = 550000; abstentions = 412500
                semantic_stream_sha256 = '89bee1f740818d905e8dd2e7b8b549e94574c2514c18a8562714e21bcbad5df5'
            }
        )
        focused_tests = $testResults
        verification_driver_peak_working_set_bytes = $driver.PeakWorkingSet64
        stress_execution = 'same-product-expansion-recipe-independent-count-and-streaming-semantic-hash'
        scale_execution = 'full-delivered-index-source-selection-and-aggregate-serialization-under-64mib'
    })
}

function Invoke-CasesGate {
    $fixtureRoot = Join-Path $repoRoot 'docs/evaluation/fixtures/m1-slice5-wp4-cases-v1'
    $requiredPaths = @(
        'contracts/json-schema/finding-case-input.v1.schema.json',
        'contracts/json-schema/finding-case.v1.schema.json',
        'contracts/json-schema/analyzer-declaration.v1.schema.json',
        'contracts/json-schema/candidate-analysis.v1.schema.json',
        'src/Infinium.Analysis/Conclusions/FindingConclusionProducer.cs',
        'src/Infinium.Analysis/Cases/FindingCasePipeline.cs',
        'src/Infinium.Application/FindingCases/FindingCaseAnalysisPhase.cs',
        'src/Infinium.Application/Evaluation/AnalyzerDeclarationJsonCodec.cs',
        'src/Infinium.Persistence/AuthoritativeStore.FindingCases.cs',
        'docs/evaluation/fixtures/m1-slice5-wp4-cases-v1/README.md',
        'docs/evaluation/fixtures/m1-slice5-wp4-cases-v1/wp4-independent-truth.v1.0.3.json',
        'docs/evaluation/fixtures/m1-slice5-wp4-cases-v1/independent-review.md'
    )
    foreach ($relativePath in $requiredPaths) {
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath))) {
            throw "Required WP4 finding/case surface is missing: $relativePath"
        }
    }
    $truthPath = Join-Path $fixtureRoot 'wp4-independent-truth.v1.0.3.json'
    $reviewPath = Join-Path $fixtureRoot 'independent-review.md'
    $truth = Read-StrictJson $truthPath
    $truthHash = (Get-FileHash -LiteralPath $truthPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $review = [System.IO.File]::ReadAllText($reviewPath)
    if ($truth.package_registry.Count -ne 4 -or
        $truthHash -cne '528bed0cd3ce399b54ae99f2ebb12e63981f292228c5c972191098c535e90fa2' -or
        $review.IndexOf('Verdict: `ACCEPT`', [StringComparison]::Ordinal) -lt 0 -or
        $review.IndexOf($truthHash, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw 'WP4 semantic truth is not a four-package independently accepted frozen handoff.'
    }

    $productSources = @(
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Infinium.Domain/Contracts') -File -Filter '*.cs'
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Analysis/Candidates/CandidatePipeline.cs')
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Infinium.Analysis/Conclusions') -Recurse -File -Filter '*.cs'
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Infinium.Analysis/Cases') -Recurse -File -Filter '*.cs'
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Infinium.Application/FindingCases') -Recurse -File -Filter '*.cs'
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Application/Evaluation/Slice5ContractJsonCodecs.cs')
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Application/Evaluation/AnalyzerDeclarationJsonCodec.cs')
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Persistence/AuthoritativeStore.cs')
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Persistence/AuthoritativeStore.FindingCases.cs')
        Get-Item -LiteralPath (Join-Path $repoRoot 'contracts/json-schema/finding-case-input.v1.schema.json')
        Get-Item -LiteralPath (Join-Path $repoRoot 'contracts/json-schema/finding-case.v1.schema.json')
        Get-Item -LiteralPath (Join-Path $repoRoot 'contracts/json-schema/analyzer-declaration.v1.schema.json')
        Get-Item -LiteralPath (Join-Path $repoRoot 'contracts/json-schema/candidate-analysis.v1.schema.json')
    ) | ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }
    $productSourceText = $productSources -join [Environment]::NewLine
    foreach ($forbidden in @(
        'expected_typed_output', 'infinium.m1s5.wp4.', 'wp4-independent-truth',
        'HttpClient', 'OpenAIClient', 'NexusClient', 'Process.Start', 'PowerShell.Create')) {
        if ($productSourceText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "WP4 product graph reaches forbidden capability or fixture vocabulary: $forbidden"
        }
    }

    $testCommands = @(
        @('test', 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'TestCategory=M1Cases'),
        @('test', 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'TestCategory=M1Cases'),
        @('test', 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'TestCategory=M1Cases'),
        @('test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'TestCategory=M1Cases')
    )
    $testResults = Invoke-FocusedTests $testCommands 'WP4 finding/case focused verification'
    $expectedTestCounts = @(2, 7, 4, 5)
    for ($index = 0; $index -lt $expectedTestCounts.Count; $index++) {
        if ($testResults[$index].matched_tests -ne $expectedTestCounts[$index] -or
            $testResults[$index].passed_tests -ne $expectedTestCounts[$index]) {
            throw "WP4 focused test count mismatch at project index $index; expected $($expectedTestCounts[$index]) exact passing tests."
        }
    }
    Write-GateReport 'Cases' ([ordered]@{
        fixture_versions = @($truth.package_registry | ForEach-Object { "$($_.package_id)/$($_.package_version)" })
        truth_sha256 = $truthHash
        independent_review = 'accepted'
        promotion_predicates = @('present','plausible-or-better','support','no-defeating-contradiction','no-missing-information','closed-severity','closed-identity')
        reconciliation_gates = @('causal','applicability','dependency','producer')
        reconciliation_outcomes = @('exact-continuation','analytical-revision','related-follow-up','new-distinct','ambiguous','unknown','not-observed','not-evaluated')
        review_state_carryover = 'none'
        coverage_presentation = 'exact-labeled-populations-no-combined-percentage-no-safety-claim'
        product_executed_fixture_packages = @('causal-conclusions','reconciliation-lineage','taxonomy-history','coverage-boundaries')
        focused_tests = $testResults
        provider_model_private_fixture = 'not-used'
    })
}

function Get-Wp5FixtureEvidence([string] $ReceiptPath) {
    $fixtureRoot = Join-Path $repoRoot 'docs/evaluation/fixtures/m1-slice5-wp5-operational-cases-v1'
    $manifestPath = Join-Path $fixtureRoot 'fixture-manifest.v1.json'
    $manifest = Read-StrictJson $manifestPath
    if ([string] $manifest.status -notmatch 'accepted|independently-reviewed') {
        throw "WP5 operational fixture package is not independently accepted: $($manifest.status)"
    }
    $files = @()
    foreach ($entry in @($manifest.files)) {
        $path = Join-Path $fixtureRoot ([string] $entry.path)
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -cne [string] $entry.sha256) {
            throw "WP5 frozen fixture digest mismatch: $($entry.path)"
        }
        if ([System.IO.Path]::GetExtension($path) -ceq '.json') {
            $null = Read-StrictJson $path
        }
        $files += Get-FileEvidence $path
    }
    $harnessPath = Join-Path $fixtureRoot 'harness-envelope.v1.json'
    $harness = Read-StrictJson $harnessPath
    $projectionSchemaPath = Join-Path $fixtureRoot 'ordinary-product-projection.schema.json'
    $schemaHash = (Get-FileHash -LiteralPath $projectionSchemaPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $retainedReceipts = Read-StrictJson $ReceiptPath
    $validationReceipts = @($retainedReceipts.projection_validation_receipts)
    $topologyReceipts = @($retainedReceipts.topology_capability_receipts)
    $expectedCases = @($harness.case_bindings | ForEach-Object { [string] $_.case_id } | Sort-Object)
    $actualCases = @($validationReceipts | ForEach-Object { [string] $_.CaseId } | Sort-Object)
    if ([string] $retainedReceipts.schema_id -cne 'infinium.verification.wp5-projection-validation-receipts/v1' -or
        (Compare-Object $expectedCases $actualCases) -or
        @($validationReceipts | Where-Object {
            [string] $_.SchemaSha256 -cne $schemaHash -or
            [string] $_.Disposition -cne 'closed-schema-and-answer-isolation-validated-before-product-dispatch'
        }).Count -ne 0) {
        throw 'WP5 retained pre-dispatch projection validation receipts are incomplete or inconsistent.'
    }
    $fixtureCount = @($manifest.fixture_manifests).Count
    $caseCount = @($harness.case_bindings).Count
    if ($fixtureCount -ne 2 -or $caseCount -ne 12 -or $validationReceipts.Count -ne 12) {
        throw "WP5 operational fixture cardinality changed: $fixtureCount packages, $caseCount cases."
    }
    return [ordered]@{
        registry_id = [string] $manifest.fixture_registry_id
        registry_version = [string] $manifest.fixture_registry_version
        fixture_count = $fixtureCount
        case_count = $caseCount
        manifest = Get-FileEvidence $manifestPath
        frozen_files = $files
        projection_validation_receipts = $validationReceipts
        projection_validation_receipt_file = Get-FileEvidence $ReceiptPath
        topology_capability_receipts = $topologyReceipts
        independent_review_status = [string] $manifest.answer_isolation_review.independent_review_status
    }
}

function Invoke-Wp5FocusedTestsWithReceiptCapture([object[]] $Commands, [string] $FailurePrefix) {
    $receiptPath = Join-Path $resolvedOutputRoot 'wp5-projection-validation-receipts.json'
    if ([System.IO.File]::Exists($receiptPath)) {
        [System.IO.File]::Delete($receiptPath)
    }
    $priorReceiptPath = [Environment]::GetEnvironmentVariable('INFINIUM_WP5_VALIDATION_RECEIPT_PATH', 'Process')
    [Environment]::SetEnvironmentVariable('INFINIUM_WP5_VALIDATION_RECEIPT_PATH', $receiptPath, 'Process')
    try {
        $tests = Invoke-FocusedTests $Commands $FailurePrefix
    } finally {
        [Environment]::SetEnvironmentVariable('INFINIUM_WP5_VALIDATION_RECEIPT_PATH', $priorReceiptPath, 'Process')
    }
    if (-not [System.IO.File]::Exists($receiptPath)) {
        throw 'The WP5 product comparison did not retain its pre-dispatch validation receipts.'
    }
    return [pscustomobject]@{ Tests = $tests; ReceiptPath = $receiptPath }
}

function Invoke-ReplayGate {
    $capture = Invoke-Wp5FocusedTestsWithReceiptCapture @(
        @('test', 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~AnalysisReplay|FullyQualifiedName~Slice5FailureRecovery'),
        @('test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~CleanIncrementalReplay')
    ) 'WP5 replay verification'
    $fixtures = Get-Wp5FixtureEvidence $capture.ReceiptPath
    Write-GateReport 'Replay' ([ordered]@{
        fixtures = $fixtures
        focused_tests = $capture.Tests
        execution_modes = @('clean', 'incremental', 'retained-downstream-replay')
        identity_drift = 'fail-closed'
        publication = 'coordinator-owned-atomic'
        recovery = 'stale-attempt-fenced-and-retryable'
        provider_model_credential_live_billable = 'not-used'
    })
}

function Invoke-OutputGate {
    $capture = Invoke-Wp5FocusedTestsWithReceiptCapture @(
        @('test', 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~Slice5Output|FullyQualifiedName~Slice5Query'),
        @('test', 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~Slice5Cli|FullyQualifiedName~FrozenWp5OperationalCasesAreBoundToProductExecutionBeforeOracleComparison'),
        @('test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~Slice5Operational')
    ) 'WP5 output verification'
    $fixtures = Get-Wp5FixtureEvidence $capture.ReceiptPath
    $cliProject = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'src/Infinium.Cli/Infinium.Cli.csproj'))
    $cliSource = [System.IO.File]::ReadAllText((Join-Path $repoRoot 'src/Infinium.Cli/Program.cs'))
    foreach ($forbidden in @('Infinium.Persistence', 'Microsoft.Data.Sqlite', 'SqliteConnection')) {
        if ($cliProject.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0 -or
            $cliSource.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) {
            throw "WP5 CLI reaches a forbidden direct database surface: $forbidden"
        }
    }
    Write-GateReport 'Output' ([ordered]@{
        fixtures = $fixtures
        focused_tests = $capture.Tests
        query_boundary = 'typed-application-grpc-only'
        pagination = 'authenticated-bounded-keyset-cursor'
        json_contract = 'infinium.run-output/v1'
        human_json_semantics = 'shared-run-owned-projection'
        frozen_product_comparison_cases = 12
        direct_cli_database_access = $false
        safety_guarantee = 'none'
    })
}

function Invoke-SafetyGate {
    $capture = Invoke-Wp5FocusedTestsWithReceiptCapture @(
        @('test', 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~AnalysisReplayLeavesProtectedRootCanaries|FullyQualifiedName~AnalysisReplayManagedWorker|FullyQualifiedName~Slice5FailureRecovery|FullyQualifiedName~FrozenWp5OperationalCasesAreBoundToProductExecutionBeforeOracleComparison'),
        @('test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj', '-c', 'Release', '--no-build', '--nologo', '--filter', 'FullyQualifiedName~Slice5Operational')
    ) 'WP5 safety verification'
    $fixtures = Get-Wp5FixtureEvidence $capture.ReceiptPath
    $wp5Sources = @(
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src/Infinium.Application/Analysis') -File -Filter '*.cs'
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Persistence/AuthoritativeStore.AnalysisPublication.cs')
        Get-Item -LiteralPath (Join-Path $repoRoot 'src/Infinium.Coordinator/ManagedRunExecutor.cs')
    ) | ForEach-Object { [System.IO.File]::ReadAllText($_.FullName) }
    $source = $wp5Sources -join [Environment]::NewLine
    foreach ($forbidden in @('HttpClient', 'OpenAIClient', 'NexusClient', 'PowerShell.Create')) {
        if ($source.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) {
            throw "WP5 local publication graph reaches forbidden external capability: $forbidden"
        }
    }
    Write-GateReport 'Safety' ([ordered]@{
        fixtures = $fixtures
        focused_tests = $capture.Tests
        authorized_write_classes = @('database', 'payload-store', 'staging', 'trace', 'run-output')
        protected_root_canaries = 'unchanged'
        provider_model_credential_live_billable = 'not-used'
        export = 'not-implemented'
        external_process = 'contained-managed-worker-only'
        setup_game_mo2_writes = 0
        frozen_product_comparison_cases = 12
        native_topology_qualification = 'bounded-subset-with-explicit-capability-gaps'
    })
}

Push-Location $repoRoot
try {
    switch ($Gate) {
        'Contracts' { Invoke-ContractsGate }
        'Documentation' { Invoke-DocumentationGate }
        'Candidates' { Invoke-CandidatesGate }
        'CandidateScale' { Invoke-CandidateScaleGate }
        'Cases' { Invoke-CasesGate }
        'Replay' { Invoke-ReplayGate }
        'Output' { Invoke-OutputGate }
        'Safety' { Invoke-SafetyGate }
    }
} finally {
    Pop-Location
}
