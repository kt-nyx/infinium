[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ExpectedCandidateCommit,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$ExpectedCandidateTree,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'frontend-foundation-evidence.ps1')
$foundationRoots = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'docs/plans/transitions') -Directory -Recurse |
    Where-Object { $_.Name -eq 'frontend-application-foundation' })
if ($foundationRoots.Count -ne 1) { throw 'The exact frontend application foundation planning root could not be resolved.' }
$foundationRoot = $foundationRoots[0].FullName
$manifestPath = Join-Path $foundationRoot 'frontend-foundation-acceptance.v1.json'
$artifactRoot = Join-Path $repositoryRoot 'artifacts/frontend-foundation-acceptance'
$acceptanceRunId = [Guid]::NewGuid().ToString('N')
$runStartedAt = [DateTimeOffset]::UtcNow

function Invoke-TestBatch([string]$Name, [string]$Project, [string[]]$FullyQualifiedNames) {
    $selected = @($FullyQualifiedNames | Sort-Object -Unique)
    $trxName = $Name + '-' + $acceptanceRunId + '.trx'
    $trxPath = Join-Path $artifactRoot $trxName
    $filter = ($selected | ForEach-Object { 'FullyQualifiedName=' + $_ }) -join '|'
    try {
        Invoke-FoundationCheckedCommand 'dotnet' @(
            'test', $Project, '-c', $Configuration, '--no-build', '--no-restore', '--nologo',
            '--filter', $filter, '--logger', "trx;LogFileName=$trxName", '--results-directory', $artifactRoot) 'Acceptance test batch' | Out-Null
    }
    finally {
        $survivors = Stop-FoundationRepositoryOwnedTestProcess $repositoryRoot
        Write-Host "Repository-owned dotnet/testhost/vstest processes remaining after ${Name}: $survivors"
    }
    [pscustomobject][ordered]@{
        name = $Name
        project = $Project.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        selection_filter = $filter
        selected_tests = $selected
        trx_path = $trxPath.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        trx_sha256 = Get-FoundationFileSha256 $trxPath
        passed = Get-FoundationTrxCount $trxPath 'passed'
        failed = Get-FoundationTrxCount $trxPath 'failed'
        skipped = Get-FoundationTrxCount $trxPath 'notExecuted'
        total = Get-FoundationTrxCount $trxPath 'total'
        tests = @(Get-FoundationTrxResults $trxPath)
    }
}

function Get-BatchByName([object[]]$Batches, [string]$Name) {
    $matches = @($Batches | Where-Object { $_.name -ceq $Name })
    if ($matches.Count -ne 1) { throw "Expected one test batch named $Name; found $($matches.Count)." }
    $matches[0]
}

Push-Location $repositoryRoot
try {
    $startCandidate = Assert-FoundationCandidateSnapshot $repositoryRoot $ExpectedCandidateCommit $ExpectedCandidateTree 'acceptance-start'
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    $receiptPath = Join-Path $artifactRoot 'summary.json'
    if (Test-Path -LiteralPath $receiptPath) {
        Remove-Item -LiteralPath $receiptPath -Force
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $steps = @($manifest.workflow)
    $nextMilestoneName = 'M' + '2'
    $milestoneStateProperty = 'm' + '2_state'
    if ($manifest.PSObject.Properties[$milestoneStateProperty].Value -ne 'inactive') { throw "The acceptance manifest must keep $nextMilestoneName inactive." }
    if ($steps.Count -ne 16 -or (($steps | ForEach-Object { [int]$_.step }) -join ',') -ne ((1..16) -join ',')) {
        throw 'The acceptance manifest must contain the exact ordered 16-step workflow.'
    }
    $allProofs = @($steps | ForEach-Object { @($_.proofs) })
    if (@($allProofs.proof_id | Sort-Object -Unique).Count -ne $allProofs.Count) { throw 'Every workflow proof identity must be globally unique.' }

    $contractProject = Join-Path $repositoryRoot 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj'
    $integrationProject = Join-Path $repositoryRoot 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj'
    $desktopProject = Join-Path $repositoryRoot 'tests/Infinium.DesktopTests/Infinium.DesktopTests.csproj'
    foreach ($project in @($contractProject, $integrationProject, $desktopProject)) {
        Invoke-FoundationDotNetCommand $repositoryRoot @('build', $project, '-c', $Configuration, '--no-restore', '--nologo') ([IO.Path]::GetFileNameWithoutExtension($project) + '-build')
    }
    $contractAuthorityNames = @(
        'Infinium.Tests.ApplicationFoundationAuthorityContractTests.IntegratedAcceptanceWorkflowIsExactOfflineAndPreservesNativeOnlyAuthority',
        'Infinium.Tests.ApplicationFoundationAuthorityContractTests.AcceptanceEvidenceBindingRejectsUnverifiedOrSubstitutedProofs',
        'Infinium.Tests.ApplicationFoundationAuthorityContractTests.ApplicationInventoryIsStrictCompleteAndMatchesImplementedService',
        'Infinium.Tests.ApplicationFoundationAuthorityContractTests.CapabilityMatrixIsStrictFullyOwnedAndDeniesGenericRendererAuthority')
    $contractProofNames = @($allProofs | Where-Object { $_.kind -ceq 'executable-test' -and $_.batch -ceq 'contract-authority' } | ForEach-Object { [string]$_.fully_qualified_name })
    $nativeProofNames = @($allProofs | Where-Object { $_.kind -ceq 'executable-test' -and $_.batch -ceq 'native-integrated-workflow' } | ForEach-Object { [string]$_.fully_qualified_name })
    $testBatches = @(
        Invoke-TestBatch 'contract-authority' $contractProject @($contractAuthorityNames + $contractProofNames)
        Invoke-TestBatch 'native-integrated-workflow' $integrationProject $nativeProofNames)

    Invoke-FoundationCheckedCommand 'powershell' @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $repositoryRoot 'eng/qualify-desktop.ps1'),
        '-Configuration', $Configuration, '-ExpectedCandidateCommit', $ExpectedCandidateCommit,
        '-ExpectedCandidateTree', $ExpectedCandidateTree, '-AcceptanceRunId', $acceptanceRunId) 'Desktop qualification' | Out-Null

    $desktopSummaryPath = Join-Path $repositoryRoot 'artifacts/desktop-qualification/summary.json'
    $desktopSummary = Get-Content -LiteralPath $desktopSummaryPath -Raw | ConvertFrom-Json
    Assert-FoundationDesktopReceiptBinding $desktopSummary $ExpectedCandidateCommit $ExpectedCandidateTree $acceptanceRunId | Out-Null
    $desktopSummaryHash = Get-FoundationFileSha256 $desktopSummaryPath
    $desktopBatches = @($desktopSummary.test_batches)
    $expectedDesktopBatches = @(
        'desktop-lifecycle',
        'desktop-state-preparation',
        'live-desktop',
        'ordinary-desktop')
    $actualDesktopBatches = @($desktopBatches.name | Sort-Object)
    if (($actualDesktopBatches -join ',') -cne ($expectedDesktopBatches -join ',')) {
        throw 'Desktop qualification receipt does not contain the exact four required test batches.'
    }
    foreach ($desktopBatch in $desktopBatches) {
        $expectedDesktopTrxPath = 'artifacts/desktop-qualification/test-results/' +
            [string]$desktopBatch.name + '-' + $acceptanceRunId + '.trx'
        if ([string]$desktopBatch.trx_path -cne $expectedDesktopTrxPath) {
            throw "Desktop TRX path is not bound to this acceptance run: $($desktopBatch.trx_path)"
        }
        $desktopTrx = Join-Path $repositoryRoot $expectedDesktopTrxPath
        if ((Get-FoundationFileSha256 $desktopTrx) -cne [string]$desktopBatch.trx_sha256) {
            throw "Desktop TRX hash does not match its qualification receipt: $($desktopBatch.trx_path)"
        }
        if ((Get-Item -LiteralPath $desktopTrx).LastWriteTimeUtc -lt $runStartedAt.UtcDateTime) { throw "Desktop TRX predates this acceptance run: $($desktopBatch.trx_path)" }
    }

    $workflowReceipt = @()
    foreach ($step in $steps) {
        $proofReceipts = @()
        foreach ($proof in @($step.proofs)) {
            switch ([string]$proof.kind) {
                'executable-test' {
                    $batch = Get-BatchByName $testBatches ([string]$proof.batch)
                    Assert-FoundationTestProjectBinding $proof ([string]$batch.project) | Out-Null
                    $trxPath = Join-Path $repositoryRoot ([string]$batch.trx_path)
                    $verified = Assert-FoundationTestProof $proof @([string]$proof.fully_qualified_name) @($batch.selected_tests) $trxPath ([string]$batch.trx_sha256) $runStartedAt
                    $proofReceipts += [pscustomobject][ordered]@{
                        proof_id = [string]$proof.proof_id; kind = [string]$proof.kind; required = [bool]$proof.required; behavioral = $true
                        result = $verified.result; batch = [string]$proof.batch; project = [string]$proof.project
                        fully_qualified_name = $verified.fully_qualified_name; outcome = $verified.outcome
                        test_id = $verified.test_id; execution_id = $verified.execution_id
                        selection_filter = $batch.selection_filter; trx_path = $batch.trx_path
                        trx_sha256 = $verified.trx_sha256
                    }
                }
                'desktop-qualification-test' {
                    $batch = Get-BatchByName $desktopBatches ([string]$proof.batch)
                    Assert-FoundationTestProjectBinding $proof ([string]$batch.project) | Out-Null
                    $trxPath = Join-Path $repositoryRoot ([string]$batch.trx_path)
                    $verified = Assert-FoundationTestProof $proof @([string]$proof.fully_qualified_name) @([string]$proof.fully_qualified_name) $trxPath ([string]$batch.trx_sha256) $runStartedAt
                    $assertionReceipts = @()
                    foreach ($assertion in @($proof.receipt_assertions)) {
                        $observed = Get-FoundationJsonPointerValue $desktopSummary ([string]$assertion.json_pointer)
                        if (-not (Test-FoundationEvidencePredicate $observed $assertion.predicate)) { throw "Desktop receipt assertion failed for proof $($proof.proof_id) at $($assertion.json_pointer)." }
                        $assertionReceipts += [pscustomobject][ordered]@{ json_pointer = [string]$assertion.json_pointer; predicate = $assertion.predicate; observed = $observed }
                    }
                    $proofReceipts += [pscustomobject][ordered]@{
                        proof_id = [string]$proof.proof_id; kind = [string]$proof.kind; required = [bool]$proof.required; behavioral = $true
                        result = $verified.result; batch = [string]$proof.batch; project = [string]$proof.project
                        fully_qualified_name = $verified.fully_qualified_name; outcome = $verified.outcome
                        test_id = $verified.test_id; execution_id = $verified.execution_id
                        selection_filter = $batch.filter; trx_path = $batch.trx_path; trx_sha256 = $verified.trx_sha256
                        desktop_summary_sha256 = $desktopSummaryHash
                        receipt_assertions = $assertionReceipts
                    }
                }
                'machine-evidence' {
                    $expectedHash = if ([string]$proof.path -ceq 'artifacts/desktop-qualification/summary.json') { $desktopSummaryHash } else { '' }
                    $verified = Assert-FoundationMachineEvidence $proof $repositoryRoot $expectedHash
                    $proofReceipts += [pscustomobject][ordered]@{
                        proof_id = [string]$proof.proof_id; kind = [string]$proof.kind; required = [bool]$proof.required; behavioral = $true
                        result = $verified.result; path = [string]$proof.path; source_sha256 = $verified.source_sha256
                        json_pointer = $verified.json_pointer; predicate = $verified.predicate; observed = $verified.observed
                    }
                }
                'reference' {
                    $referencePath = Join-Path $repositoryRoot ([string]$proof.path)
                    $proofReceipts += [pscustomobject][ordered]@{
                        proof_id = [string]$proof.proof_id; kind = [string]$proof.kind; required = [bool]$proof.required; behavioral = $false
                        result = 'reference-only'; path = [string]$proof.path; selector = [string]$proof.selector
                        source_sha256 = Get-FoundationFileSha256 $referencePath
                    }
                }
                default { throw "Unknown workflow proof kind: $($proof.kind)" }
            }
        }
        $stepResult = Assert-FoundationWorkflowStep $proofReceipts ([int]$step.step)
        $workflowReceipt += [pscustomobject][ordered]@{
            step = [int]$step.step; action = $step.action; consumer = $step.consumer; authority = $step.authority
            surface_maturity = $step.surface_maturity; result = $stepResult; proofs = $proofReceipts
        }
    }
    if (@($workflowReceipt | Where-Object { $_.result -cne 'passed' }).Count -ne 0) {
        throw 'The integrated workflow contains an unverified step.'
    }
    $overallResult = 'passed'

    $finalSurvivors = Stop-FoundationRepositoryOwnedTestProcess $repositoryRoot
    $endCandidate = Assert-FoundationCandidateSnapshot $repositoryRoot $ExpectedCandidateCommit $ExpectedCandidateTree 'acceptance-end'
    $receipt = [ordered]@{
        schema = 'infinium.frontend-foundation-acceptance-receipt/v2'
        acceptance_run_id = $acceptanceRunId
        recorded_at = [DateTimeOffset]::UtcNow.ToString('O')
        result = $overallResult
        candidate = [ordered]@{
            commit = $endCandidate.commit; tree = $endCandidate.tree
            expected_commit = $ExpectedCandidateCommit; expected_tree = $ExpectedCandidateTree
            worktree_clean_before = [bool]$startCandidate.clean; worktree_clean_after = [bool]$endCandidate.clean
        }
        checkpoint_d_commit = $manifest.checkpoint_d_commit
        classification = $nextMilestoneName + '-ready contract candidate'
        claim_boundary = "Offline integrated foundation evidence only. Measurements inform later $nextMilestoneName planning and are not production guarantees. This receipt grants no new renderer, provider, credential, path, command, network, evaluator, private-fixture, semantic-oracle, or $nextMilestoneName authority."
        declared_execution_boundaries = [ordered]@{
            classification = 'Declared scope boundaries, not ambient-system observations.'
            network = 'prohibited; frontend dependency restore uses the accepted offline task'
            provider = 'prohibited; no live or billable provider test is selected'
            credentials = 'prohibited; no credential entry or secure-store operation is selected'
            private_evaluator_material = 'prohibited and outside repository authority'
            archives = 'prohibited and outside this workflow'
        }
        enforced_execution_controls = [ordered]@{
            candidate = 'exact clean committed HEAD and tree checked before and after all evidence production'
            dotnet = 'all acceptance tests and builds use --no-restore'
            frontend = 'desktop qualification invokes the accepted RestoreOffline task before generation, drift, type, lint, build, and tests'
            receipt_freshness = 'run-specific identities and TRX filenames plus candidate, tree, timestamp, and SHA-256 verification'
        }
        manifest_sha256 = Get-FoundationFileSha256 $manifestPath
        evaluation_ids = @($manifest.evaluation_ids)
        workflow = $workflowReceipt
        focused_test_batches = $testBatches
        focused_test_totals = [ordered]@{
            passed = [int](($testBatches | Measure-Object passed -Sum).Sum)
            failed = [int](($testBatches | Measure-Object failed -Sum).Sum)
            skipped = [int](($testBatches | Measure-Object skipped -Sum).Sum)
            total = [int](($testBatches | Measure-Object total -Sum).Sum)
        }
        desktop_qualification = [ordered]@{
            acceptance_run_id = $desktopSummary.acceptance_run_id
            candidate_commit = $desktopSummary.candidate.commit; candidate_tree = $desktopSummary.candidate.tree
            summary_path = 'artifacts/desktop-qualification/summary.json'; summary_sha256 = $desktopSummaryHash
            test_batches = @($desktopBatches | ForEach-Object { [ordered]@{
                name = $_.name; project = $_.project; filter = $_.filter; trx_path = $_.trx_path; trx_sha256 = $_.trx_sha256
                passed = [int]$_.passed; failed = [int]$_.failed; skipped = [int]$_.skipped; total = [int]$_.total
            } })
            cleanup_survivor_count = [int]$desktopSummary.cleanup_survivor_count
            browser_ready_milliseconds = $desktopSummary.launch.browser_ready_milliseconds
            bridge_milliseconds = $desktopSummary.bridge_milliseconds
            private_working_set_bytes = $desktopSummary.private_working_set_bytes
            observed_message_bytes = $desktopSummary.observed_message_bytes
            package_file_count = $desktopSummary.package_file_count; package_bytes = $desktopSummary.package_bytes
            installed_webview_runtime_file_count = $desktopSummary.installed_webview_runtime_file_count
            installed_webview_runtime_bytes = $desktopSummary.installed_webview_runtime_bytes
        }
        final_repository_owned_test_process_survivors = $finalSurvivors
    }
    $receipt[$milestoneStateProperty] = 'inactive'
    [IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 32), [Text.UTF8Encoding]::new($false))
    Write-Output "Frontend foundation acceptance summary: $receiptPath"
    Write-Output "Candidate commit: $($endCandidate.commit)"
    Write-Output "Candidate tree: $($endCandidate.tree)"
    Write-Output "Final repository-owned dotnet/testhost/vstest survivor count: $finalSurvivors"
}
finally {
    try {
        $cleanupSurvivors = Stop-FoundationRepositoryOwnedTestProcess $repositoryRoot
        Write-Host "Repository-owned dotnet/testhost/vstest processes remaining after acceptance cleanup: $cleanupSurvivors"
    }
    finally { Pop-Location }
}
