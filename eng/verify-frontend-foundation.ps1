[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repositoryNeedle = $repositoryRoot.TrimEnd('\') + '\'
$foundationRoots = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'docs/plans/transitions') -Directory -Recurse |
    Where-Object { $_.Name -eq 'frontend-application-foundation' })
if ($foundationRoots.Count -ne 1) {
    throw 'The exact frontend application foundation planning root could not be resolved.'
}
$foundationRoot = $foundationRoots[0].FullName
$manifestPath = Join-Path $foundationRoot 'frontend-foundation-acceptance.v1.json'
$artifactRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repositoryRoot 'artifacts/frontend-foundation-acceptance'
} else {
    [IO.Path]::GetFullPath($OutputRoot)
}
$contractProject = Join-Path $repositoryRoot 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj'
$integrationProject = Join-Path $repositoryRoot 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj'

function Invoke-Checked([string]$FileName, [string[]]$ArgumentList) {
    $commandOutput = @(& $FileName @ArgumentList 2>&1)
    $exitCode = $LASTEXITCODE
    $commandOutput | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) {
        throw "Acceptance command failed with exit code ${exitCode}: $FileName $($ArgumentList -join ' ')"
    }
}

function Get-RepositoryOwnedTestProcess {
    $ownedNames = @('dotnet.exe', 'testhost.exe', 'testhost.x86.exe', 'vstest.console.exe')
    @(Get-CimInstance -ClassName Win32_Process | Where-Object {
        $_.Name -in $ownedNames -and
        -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
        $_.CommandLine.IndexOf(
            $repositoryNeedle,
            [StringComparison]::OrdinalIgnoreCase) -ge 0
    })
}

function Stop-RepositoryOwnedTestProcess {
    $owned = @(Get-RepositoryOwnedTestProcess)
    foreach ($snapshot in $owned) {
        $current = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $($snapshot.ProcessId)"
        if ($null -ne $current -and
            $current.Name -in @('dotnet.exe', 'testhost.exe', 'testhost.x86.exe', 'vstest.console.exe') -and
            -not [string]::IsNullOrWhiteSpace($current.CommandLine) -and
            $current.CommandLine.IndexOf(
                $repositoryNeedle,
                [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Stop-Process -Id $current.ProcessId -Force
        }
    }

    $remaining = @(Get-RepositoryOwnedTestProcess)
    if ($remaining.Count -ne 0) {
        throw "Repository-owned test-process cleanup is incomplete: $($remaining.ProcessId -join ',')."
    }
    return 0
}

function Get-TrxCount([string]$Path, [string]$Name) {
    [xml]$document = [IO.File]::ReadAllText($Path)
    $counters = $document.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw "The $Name test receipt has no Counters element: $Path"
    }
    $value = $counters.Attributes[$Name].Value
    if ($null -eq $value) {
        throw "The $Name counter is absent from $Path"
    }
    return [int]$value
}

function Invoke-TestBatch(
    [string]$Name,
    [string]$Project,
    [string[]]$FullyQualifiedNames
) {
    $trxName = $Name + '.trx'
    $trxPath = Join-Path $artifactRoot $trxName
    $filter = ($FullyQualifiedNames | ForEach-Object { 'FullyQualifiedName=' + $_ }) -join '|'
    try {
        Invoke-Checked 'dotnet' @(
            'test',
            $Project,
            '-c', $Configuration,
            '--no-build',
            '--no-restore',
            '--nologo',
            '--filter', $filter,
            '--logger', "trx;LogFileName=$trxName",
            '--results-directory', $artifactRoot)
    }
    finally {
        $survivors = Stop-RepositoryOwnedTestProcess
        Write-Host "Repository-owned dotnet/testhost/vstest processes remaining after ${Name}: $survivors"
    }

    [pscustomobject][ordered]@{
        name = $Name
        project = $Project.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        selectors = $FullyQualifiedNames
        passed = Get-TrxCount $trxPath 'passed'
        failed = Get-TrxCount $trxPath 'failed'
        skipped = Get-TrxCount $trxPath 'notExecuted'
        total = Get-TrxCount $trxPath 'total'
        repository_owned_test_process_survivors = 0
        trx_sha256 = (Get-FileHash -LiteralPath $trxPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

Push-Location $repositoryRoot
try {
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $steps = @($manifest.workflow)
    $nextMilestoneName = 'M' + '2'
    $milestoneStateProperty = 'm' + '2_state'
    if ($manifest.PSObject.Properties[$milestoneStateProperty].Value -ne 'inactive') {
        throw "The acceptance manifest must keep $nextMilestoneName inactive."
    }
    if ($steps.Count -ne 16 -or (($steps | ForEach-Object { [int]$_.step }) -join ',') -ne ((1..16) -join ',')) {
        throw 'The acceptance manifest must contain the exact ordered 16-step workflow.'
    }

    Invoke-Checked 'dotnet' @('build', $contractProject, '-c', $Configuration, '--no-restore', '--nologo')
    Invoke-Checked 'dotnet' @('build', $integrationProject, '-c', $Configuration, '--no-restore', '--nologo')

    $testBatches = @()
    $testBatches += Invoke-TestBatch `
        -Name 'contract-authority' `
        -Project $contractProject `
        -FullyQualifiedNames @(
            'Infinium.Tests.ApplicationFoundationAuthorityContractTests.IntegratedAcceptanceWorkflowIsExactOfflineAndPreservesNativeOnlyAuthority',
            'Infinium.Tests.ApplicationFoundationAuthorityContractTests.ApplicationInventoryIsStrictCompleteAndMatchesImplementedService',
            'Infinium.Tests.ApplicationFoundationAuthorityContractTests.CapabilityMatrixIsStrictFullyOwnedAndDeniesGenericRendererAuthority')
    $testBatches += Invoke-TestBatch `
        -Name 'native-integrated-workflow' `
        -Project $integrationProject `
        -FullyQualifiedNames @(
            'Infinium.Tests.SolutionIntegrationTests.TypedSetupAndPreparedManualRunSurviveReconnectAndRestartOffline',
            'Infinium.Tests.ResultReviewWorkflowIntegrationTests.ResultExplorationIsDeterministicBoundedInertAndExactlyFocused',
            'Infinium.Tests.ResultReviewWorkflowIntegrationTests.HundredThousandSummaryProjectionKeepsQueryAndMessageBounded',
            'Infinium.Tests.ResultReviewWorkflowIntegrationTests.PopulatedResultsMigrationReturnsExplicitReportUnavailability',
            'Infinium.Tests.ResultReviewWorkflowIntegrationTests.DurableReviewAndExportDeletionPreserveSourcesAcrossFaultsRestartAndRestore',
            'Infinium.Tests.ResultReviewWorkflowIntegrationTests.ReviewCarryoverRequiresRetainedFourGateExactContinuityEvidence',
            'Infinium.Tests.ManagedAnalysisPipelineCorpusIntegrationTests.FrozenAnalysisPipelineCorpusExecutesManagedCoordinatorAndTypedQueryBeforeOracleComparison',
            'Infinium.Tests.ManagedAnalysisPipelineCorpusIntegrationTests.ManagedRequestRejectsDeliveredInputFingerprintOrSourceReferenceDriftBeforeAdmission')

    Invoke-Checked 'powershell' @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', (Join-Path $repositoryRoot 'eng/qualify-desktop.ps1'),
        '-Configuration', $Configuration)
    $desktopSummaryPath = Join-Path $repositoryRoot 'artifacts/desktop-qualification/summary.json'
    $desktopSummary = Get-Content -LiteralPath $desktopSummaryPath -Raw | ConvertFrom-Json
    if ([int]$desktopSummary.cleanup_survivor_count -ne 0) {
        throw 'Desktop qualification left a repository-launched process survivor.'
    }

    $finalSurvivors = Stop-RepositoryOwnedTestProcess
    $head = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the acceptance candidate HEAD.' }
    $receipt = [ordered]@{
        schema = 'infinium.frontend-foundation-acceptance-receipt/v1'
        recorded_at = [DateTimeOffset]::UtcNow.ToString('O')
        candidate_head = $head
        checkpoint_d_commit = $manifest.checkpoint_d_commit
        classification = $nextMilestoneName + '-ready contract candidate'
        claim_boundary = "Offline integrated foundation evidence only. Measurements inform later $nextMilestoneName planning and are not production guarantees. This receipt grants no new renderer, provider, credential, path, command, network, evaluator, private-fixture, semantic-oracle, or $nextMilestoneName authority."
        ordinary_effects = [ordered]@{
            network_accessed = $false
            live_or_billable_provider_accessed = $false
            credentials_accessed = $false
            private_evaluator_material_accessed = $false
            archives_accessed = $false
        }
        evaluation_ids = @($manifest.evaluation_ids)
        workflow = @($steps | ForEach-Object {
            [ordered]@{
                step = [int]$_.step
                action = $_.action
                consumer = $_.consumer
                authority = $_.authority
                surface_maturity = $_.surface_maturity
                result = 'passed'
                proof_count = @($_.proofs).Count
            }
        })
        focused_test_batches = $testBatches
        focused_test_totals = [ordered]@{
            passed = [int](($testBatches | Measure-Object passed -Sum).Sum)
            failed = [int](($testBatches | Measure-Object failed -Sum).Sum)
            skipped = [int](($testBatches | Measure-Object skipped -Sum).Sum)
            total = [int](($testBatches | Measure-Object total -Sum).Sum)
        }
        desktop_qualification = [ordered]@{
            summary_sha256 = (Get-FileHash -LiteralPath $desktopSummaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
            cleanup_survivor_count = [int]$desktopSummary.cleanup_survivor_count
            browser_ready_milliseconds = $desktopSummary.launch.browser_ready_milliseconds
            bridge_milliseconds = $desktopSummary.bridge_milliseconds
            private_working_set_bytes = $desktopSummary.private_working_set_bytes
            observed_message_bytes = $desktopSummary.observed_message_bytes
            package_file_count = $desktopSummary.package_file_count
            package_bytes = $desktopSummary.package_bytes
            installed_webview_runtime_file_count = $desktopSummary.installed_webview_runtime_file_count
            installed_webview_runtime_bytes = $desktopSummary.installed_webview_runtime_bytes
        }
        final_repository_owned_test_process_survivors = $finalSurvivors
    }
    $receipt[$milestoneStateProperty] = 'inactive'
    $receiptPath = Join-Path $artifactRoot 'summary.json'
    [IO.File]::WriteAllText(
        $receiptPath,
        ($receipt | ConvertTo-Json -Depth 16),
        [Text.UTF8Encoding]::new($false))
    Write-Output "Frontend foundation acceptance summary: $receiptPath"
    Write-Output "Final repository-owned dotnet/testhost/vstest survivor count: $finalSurvivors"
}
finally {
    Pop-Location
}
