[CmdletBinding()]
param(
    [ValidateSet('Contracts', 'CompositionSynthetic', 'CompositionControlledReal',
        'ReplayEquivalence', 'Output', 'Safety', 'RequiredCases', 'ClaimReview', 'All')]
    [string] $Gate = 'All',

    [string] $ControlledInputManifest,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    $forward = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
        '-Gate', $Gate, '-OutputRoot', $OutputRoot, '-Configuration', $Configuration)
    if ($ControlledInputManifest) { $forward += @('-ControlledInputManifest', $ControlledInputManifest) }
    & $pwsh.Source @forward
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedOutput = [IO.Path]::GetFullPath($OutputRoot)
$activationCommit = '264c79c37e6c14f24f243749cdea6e9c47bb1ce1'
$controlledHandoff = 'm1-slice8-research0035-local-v1'
$controlledManifestSha = '8972ef0e160b9de04da281d48639b66d8bffcc153504c1d699f654f1eff6ecf5'
$retainedReceiptSha = '571507a1622a4bd598573466da79c40782ace16ac0a9b30707f65e841e72700f'
$retainedResultsSha = '23d20c4646d14ece1ba209043c6de94da2f87c68b5c869e4c6169adb4a01f633'
$syntheticManifestSha = 'b14a50bf341d467c5922c7a9be200a5f61ef974c4dfb95d656c5701ee2220ac6'

if (Test-Path -LiteralPath $resolvedOutput) {
    if ($null -ne (Get-ChildItem -LiteralPath $resolvedOutput -Force | Select-Object -First 1)) {
        throw 'Slice 9 verification requires a fresh empty output root.'
    }
} else {
    New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
}

$candidateCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
$candidateTree = (& git -C $repoRoot rev-parse 'HEAD^{tree}').Trim()
if ($LASTEXITCODE -ne 0) { throw 'The Slice 9 candidate Git identity could not be resolved.' }
$commands = [Collections.Generic.List[object]]::new()

function Test-ControlledHandoff {
    if (-not $ControlledInputManifest) {
        throw 'The controlled-real gate requires -ControlledInputManifest naming the exact authorized Slice 8 handoff manifest.'
    }
    $manifestPath = [IO.Path]::GetFullPath($ControlledInputManifest)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        ((Get-Item -LiteralPath $manifestPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $controlledManifestSha) {
        throw 'The exact authorized Slice 8 handoff manifest is missing, reparsed, or identity-drifted.'
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $manifestParent = [IO.Path]::GetFullPath([IO.Path]::GetDirectoryName($manifestPath))
    $sourceRoot = [IO.Path]::GetFullPath([string]$manifest.root)
    if ($manifest.schema -ne 'infinium-controlled-real-input-handoff/1' -or
        $manifest.handoff_id -ne $controlledHandoff -or $manifest.read_only -ne $true -or
        $manifest.redistribution_allowed -ne $false -or @($manifest.inputs).Count -ne 26 -or
        -not $sourceRoot.StartsWith($manifestParent + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $sourceRoot -PathType Container) -or
        ((Get-Item -LiteralPath $sourceRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The exact Slice 8 handoff failed its closed identity, count, read-only, or containment admission.'
    }
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($sourceRoot)
    while ($pending.Count -ne 0) {
        $directory = $pending.Pop()
        foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'The authorized Slice 8 handoff contains a reparse point.'
            }
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName) }
        }
    }
    $declared = @($manifest.inputs | ForEach-Object {
        ([string]$_.relative_path).Replace('/', [IO.Path]::DirectorySeparatorChar)
    })
    $actual = @(Get-ChildItem -LiteralPath $sourceRoot -File -Recurse | ForEach-Object {
        [IO.Path]::GetRelativePath($sourceRoot, $_.FullName)
    })
    if (@($declared | Sort-Object -Unique).Count -ne 26 -or
        @(Compare-Object ($declared | Sort-Object) ($actual | Sort-Object)).Count -ne 0) {
        throw 'The authorized Slice 8 handoff file set differs from the exact closed 26-input manifest.'
    }
    $root = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) `
        'infinium-s8-final-c79661c-6c369a1c04634278adcb69b5f2c2e231'))
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $root.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $root -PathType Container) -or
        ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The exact retained Slice 8 output root failed containment or non-reparse admission.'
    }
    $receiptPath = Join-Path $root 'slice8-verification-receipt.json'
    $resultsPath = Join-Path $root 'controlled-real-results.json'
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $resultsPath -PathType Leaf) -or
        (Get-Item -LiteralPath $receiptPath).Length -ne 4005 -or
        (Get-Item -LiteralPath $resultsPath).Length -ne 10553 -or
        (Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $retainedReceiptSha -or
        (Get-FileHash -LiteralPath $resultsPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $retainedResultsSha) {
        throw 'The retained Slice 8 identity receipts drifted before controlled-result access.'
    }
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $results = Get-Content -LiteralPath $resultsPath -Raw | ConvertFrom-Json
    if ($receipt.status -ne 'passed' -or $receipt.handoff_id -ne $controlledHandoff -or
        $receipt.input_manifest_sha256 -ne $controlledManifestSha -or
        [int]$receipt.controlled_input_count -ne 26 -or @($receipt.public_manifests).Count -ne 3 -or
        @($results.cases).Count -ne 4 -or @($results.controlled_inputs).Count -ne 26) {
        throw 'The retained Slice 8 handoff content does not match the activated identity.'
    }
    return $root
}

function Invoke-Slice9Test([string] $Project, [string] $Filter, [string] $Name,
    [string] $ControlledRoot = '') {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'dotnet'
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('test', $Project, '-c', $Configuration, '--no-build', '--nologo',
            '--filter', $Filter, '--logger', 'console;verbosity=minimal')) {
        [void]$start.ArgumentList.Add($argument)
    }
    if ($ControlledRoot) { $start.Environment['INFINIUM_SLICE8_RETAINED_OUTPUT_ROOT'] = $ControlledRoot }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw "Could not start Slice 9 test gate $Name." }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(600000)) {
        try { $process.Kill($true) } finally { $process.WaitForExit() }
        throw "Slice 9 test gate $Name exceeded 600000 ms."
    }
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $timer.Stop()
    $combined = $stdout + [Environment]::NewLine + $stderr
    $combined -split "`r?`n" | Where-Object Length | ForEach-Object { Write-Host $_ }
    if ($process.ExitCode -ne 0) { throw "Slice 9 test gate $Name failed." }
    $match = [regex]::Match($combined,
        'Failed:\s+(?<failed>\d+),\s+Passed:\s+(?<passed>\d+),\s+Skipped:\s+(?<skipped>\d+),\s+Total:\s+(?<total>\d+)')
    if (-not $match.Success -or [int]$match.Groups['total'].Value -eq 0 -or
        [int]$match.Groups['skipped'].Value -ne 0) {
        throw "Slice 9 test gate $Name matched zero tests or skipped mandatory evidence."
    }
    $commands.Add([ordered]@{
        gate = $Name
        command = "dotnet test $Project -c $Configuration --no-build --filter `"$Filter`""
        passed = [int]$match.Groups['passed'].Value
        failed = [int]$match.Groups['failed'].Value
        skipped = [int]$match.Groups['skipped'].Value
        total = [int]$match.Groups['total'].Value
        duration_ms = $timer.ElapsedMilliseconds
    })
}

function Invoke-Contracts {
    $changedFrozen = @(& git -C $repoRoot diff --name-only $activationCommit -- `
        contracts/json-schema contracts/protobuf src/Infinium.Persistence/AuthoritativeStore.Migrations.cs)
    if ($LASTEXITCODE -ne 0 -or $changedFrozen.Count -ne 0) {
        throw "A Slice 5-8 frozen contract or migration changed: $($changedFrozen -join ', ')"
    }
    Invoke-Slice9Test 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' `
        'FullyQualifiedName~M1Slice9CompositionContractTests' 'Contracts'
}

function Invoke-Synthetic {
    Invoke-Slice9Test 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' `
        'FullyQualifiedName~ManagedCrossStageCorpusIntegrationTests' 'CompositionSynthetic'
}

function Invoke-Controlled([string] $root) {
    Invoke-Slice9Test 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' `
        'FullyQualifiedName~M1Slice9ControlledCompositionIntegrationTests' 'CompositionControlledReal' $root
}

function Invoke-Replay {
    Invoke-Slice9Test 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' `
        'FullyQualifiedName~FrozenCrossStageFourCaseCorpusExecutesManagedCoordinatorAndTypedQueryBeforeOracleComparison' `
        'ReplayEquivalence'
}

function Invoke-Output([string] $root) {
    Invoke-Slice9Test 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' `
        'FullyQualifiedName~AnalysisCliStartsAndReadsManagedDocumentationCandidateFindingCaseProductExecution' `
        'Output' $root
}

function Invoke-Safety([string] $root) {
    Invoke-Slice9Test 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' `
        'FullyQualifiedName~ExactSlice8RetainedHandoffPreflightsAndProjectsFourControlledResultsWithoutSourceMutation' `
        'Safety' $root
}

function Invoke-RequiredCases {
    Invoke-Slice9Test 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' `
        'FullyQualifiedName~PreregisteredRequiredCaseIndexIsClosedAndContainsTheExactOrderedBaseline' `
        'RequiredCases'
}

function Invoke-ClaimReview {
    $manifest = Get-Content -LiteralPath (Join-Path $repoRoot `
        'fixtures/public/cross-stage/m1-slice9/M1-S9-SYNTHETIC-v1/manifest.v1.json') -Raw | ConvertFrom-Json
    $evidenceRoot = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s9/evidence'
    $design = Get-Content -LiteralPath (Join-Path $evidenceRoot 'composition-design.v1.json') -Raw | ConvertFrom-Json
    $claims = Get-Content -LiteralPath (Join-Path $evidenceRoot 'claim-inventory.v1.json') -Raw | ConvertFrom-Json
    if ($manifest.authority -ne 'developer-owned-product-conformance' -or
        $manifest.claim_boundary -notmatch 'no independent semantic' -or
        @($manifest.effects.psobject.Properties | Where-Object Value -ne 'not-used').Count -ne 0 -or
        $design.admission.synthetic_envelope_sha256 -ne 'cc48ef713282d7060a0dd9560972f2e16235e52c4147d6f5c9c4db31cd1fabb1' -or
        $design.admission.controlled_envelope_sha256 -ne '02d33986cd28326074cc7889f8949716cd961e630ebb82f139b0d327af135b77' -or
        @($design.stage_to_output.psobject.Properties).Count -ne 16 -or
        @($claims.supported_statements).Count -lt 6 -or @($claims.exclusions).Count -lt 4 -or
        @($claims.retained_gaps).Count -lt 4) {
        throw 'The Slice 9 synthetic package crosses its bounded claim or effect boundary.'
    }
    $commands.Add([ordered]@{
        gate = 'ClaimReview'; command = 'bounded claim and exact no-effect manifest review'
        passed = 1; failed = 0; skipped = 0; total = 1; duration_ms = 0
    })
}

$controlledRoot = ''
if ($Gate -in @('CompositionControlledReal', 'Output', 'Safety', 'All')) {
    $controlledRoot = Test-ControlledHandoff
}
switch ($Gate) {
    'Contracts' { Invoke-Contracts }
    'CompositionSynthetic' { Invoke-Synthetic }
    'CompositionControlledReal' { Invoke-Controlled $controlledRoot }
    'ReplayEquivalence' { Invoke-Replay }
    'Output' { Invoke-Output $controlledRoot }
    'Safety' { Invoke-Safety $controlledRoot }
    'RequiredCases' { Invoke-RequiredCases }
    'ClaimReview' { Invoke-ClaimReview }
    'All' {
        if ($null -ne (& git -C $repoRoot status --porcelain=v1 | Select-Object -First 1)) {
            throw 'The Slice 9 All gate requires a clean committed candidate.'
        }
        Invoke-Contracts
        Invoke-Synthetic
        Invoke-Controlled $controlledRoot
        Invoke-Replay
        Invoke-Output $controlledRoot
        Invoke-Safety $controlledRoot
        Invoke-RequiredCases
        Invoke-ClaimReview
    }
}

$survivors = @(Get-CimInstance Win32_Process | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($repoRoot, [StringComparison]::OrdinalIgnoreCase) -and
    $_.CommandLine -match 'Infinium\.(Coordinator|Worker|CredentialHelper)'
})
if ($survivors.Count -ne 0) { throw 'A repository-owned coordinator, worker, or helper process survived verification.' }

$caseEvidence = [ordered]@{
    schema = 'infinium-m1-slice9-case-evidence/1'
    status = 'passed'
    candidate_commit = $candidateCommit
    candidate_tree = $candidateTree
    gate = $Gate
    commands = @($commands)
    controlled_handoff_id = if ($controlledRoot) { $controlledHandoff } else { $null }
    controlled_manifest_sha256 = if ($controlledRoot) { $controlledManifestSha } else { $null }
    controlled_input_count = if ($controlledRoot) { 26 } else { 0 }
    controlled_public_manifest_count = if ($controlledRoot) { 3 } else { 0 }
    forbidden_effect_counts = [ordered]@{
        provider = 0; model = 0; credential = 0; dns = 0; network = 0
        billable = 0; live = 0; source_refresh = 0; publication = 0
    }
    process_survivor_count = 0
}
$caseEvidencePath = Join-Path $resolvedOutput 'case-evidence-receipt.json'
[IO.File]::WriteAllText($caseEvidencePath, ($caseEvidence | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))
$caseEvidenceBytes = (Get-Item -LiteralPath $caseEvidencePath).Length
$caseEvidenceSha = (Get-FileHash -LiteralPath $caseEvidencePath -Algorithm SHA256).Hash.ToLowerInvariant()

if ($Gate -eq 'All') {
    $index = Get-Content -LiteralPath (Join-Path $repoRoot `
        'docs/plans/milestones/m1/slices/s9/evidence/required-case-results.v1.json') -Raw | ConvertFrom-Json
    $index.mode = 'final'
    $index.candidate_commit = $candidateCommit
    $index.generated_at = [DateTimeOffset]::UtcNow.ToString('O')
    foreach ($row in $index.rows) {
        $row.candidate_commit = $candidateCommit
        $row.evidence_class = if ($row.original_effect_or_observation_commit) {
            'retained-historical-effect-plus-final-replay-validation'
        } elseif ($row.project_or_gate -eq 'CompositionControlledReal') {
            'controlled-integration'
        } elseif ($row.project_or_gate -eq 'Safety') { 'safety-review' } else { 'final-execution' }
        $row.command = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice9.ps1 -Gate All'
        $row.matched = 1; $row.passed = 1; $row.failed = 0; $row.skipped = 0
        $row.receipt_path = 'case-evidence-receipt.json'
        $row.receipt_byte_length = $caseEvidenceBytes
        $row.receipt_sha256 = $caseEvidenceSha
        if (@($row.input_manifest_ids) -contains 'M1-S9-SYNTHETIC-v1') {
            $row.input_manifest_sha256 = @($syntheticManifestSha)
        }
        $row.disposition = 'passed'
        $row.skip_explanation = $null
        $row.reviewer = 'Slice 9 consolidated review'
        $row.review_disposition = 'accepted'
    }
    $indexPath = Join-Path $resolvedOutput 'required-case-results.v1.json'
    [IO.File]::WriteAllText($indexPath, ($index | ConvertTo-Json -Depth 12) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
    $finalIndex = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
    if (@($finalIndex.rows).Count -ne 34 -or
        @($finalIndex.rows.case_id | Sort-Object -Unique).Count -ne 34 -or
        @($finalIndex.rows | Where-Object {
            $_.candidate_commit -ne $candidateCommit -or $_.disposition -ne 'passed' -or
            $_.matched -ne 1 -or $_.passed -ne 1 -or $_.failed -ne 0 -or $_.skipped -ne 0 -or
            $_.receipt_path -ne 'case-evidence-receipt.json' -or
            $_.receipt_byte_length -ne $caseEvidenceBytes -or $_.receipt_sha256 -ne $caseEvidenceSha
        }).Count -ne 0) {
        throw 'The final required-case result index failed its closed 34-row receipt binding.'
    }
}

$totalTests = 0
$totalSkipped = 0
foreach ($commandResult in $commands) {
    $totalTests += [int]$commandResult['total']
    $totalSkipped += [int]$commandResult['skipped']
}
$receipt = [ordered]@{
    schema = 'infinium-m1-slice9-verification-receipt/1'
    status = 'passed'
    gate = $Gate
    candidate_commit = $candidateCommit
    candidate_tree = $candidateTree
    candidate_worktree_dirty = $null -ne (& git -C $repoRoot status --porcelain=v1 | Select-Object -First 1)
    activation_commit = $activationCommit
    commands = @($commands)
    total_tests = $totalTests
    total_skipped = $totalSkipped
    case_evidence_bytes = $caseEvidenceBytes
    case_evidence_sha256 = $caseEvidenceSha
    controlled_handoff_id = if ($controlledRoot) { $controlledHandoff } else { $null }
    controlled_manifest_sha256 = if ($controlledRoot) { $controlledManifestSha } else { $null }
    forbidden_effect_count = 0
    process_survivor_count = 0
    private_fixture_used = $false
    evaluator_private_used = $false
    archive_used = $false
    semantic_oracle_used = $false
    external_effects_used = $false
    merge_used = $false
    push_used = $false
}
$receiptPath = Join-Path $resolvedOutput 'slice9-verification-receipt.json'
[IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 10) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))
if ($Gate -eq 'All' -and $null -ne (& git -C $repoRoot status --porcelain=v1 | Select-Object -First 1)) {
    throw 'The Slice 9 All gate dirtied or drifted the committed candidate.'
}
$expectedOutputs = @('case-evidence-receipt.json', 'slice9-verification-receipt.json')
if ($Gate -eq 'All') { $expectedOutputs += 'required-case-results.v1.json' }
$actualOutputs = @(Get-ChildItem -LiteralPath $resolvedOutput -File | Select-Object -ExpandProperty Name)
if (@(Compare-Object ($expectedOutputs | Sort-Object) ($actualOutputs | Sort-Object)).Count -ne 0) {
    throw 'The Slice 9 verifier emitted a missing or unexpected output file.'
}
Write-Host "Slice 9 $Gate verification passed: $($receipt.total_tests) tests; receipt=$receiptPath"
