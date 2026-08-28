[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$ExpectedCandidateCommit,
    [string]$ExpectedCandidateTree,
    [string]$AcceptanceRunId
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'frontend-foundation-evidence.ps1')
$repositoryNeedle = $repositoryRoot.TrimEnd('\') + '\'
$coordinatorProject = Join-Path $repositoryRoot 'src/Infinium.Coordinator/Infinium.Coordinator.csproj'
$desktopTestProject = Join-Path $repositoryRoot 'tests/Infinium.DesktopTests/Infinium.DesktopTests.csproj'
$integrationTestProject = Join-Path $repositoryRoot 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$qualificationSession = [Guid]::NewGuid().ToString('N')
$qualificationRoot = Join-Path $temporaryRoot ('infinium-desktop-qualification-' + $qualificationSession)
$artifactRoot = Join-Path $repositoryRoot 'artifacts\desktop-qualification'
$runtimeMeasurements = Join-Path $artifactRoot 'runtime-measurements.json'
$summaryPath = Join-Path $artifactRoot 'summary.json'
$testResultRoot = Join-Path $artifactRoot 'test-results'
$testBatches = @()
$priorRoot = $env:INFINIUM_DESKTOP_QUALIFICATION_ROOT
$priorMeasurements = $env:INFINIUM_DESKTOP_QUALIFICATION_MEASUREMENTS
$priorSecretCanary = $env:INFINIUM_DESKTOP_SECRET_CANARY
$priorPreflightEvidence = $env:INFINIUM_DESKTOP_PREFLIGHT_TESTS_PASSED
$privilegedWebViewVariables = @(
    'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER',
    'WEBVIEW2_USER_DATA_FOLDER',
    'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS',
    'WEBVIEW2_RELEASE_CHANNEL_PREFERENCE',
    'WEBVIEW2_CHANNEL_SEARCH_KIND',
    'WEBVIEW2_RELEASE_CHANNELS',
    'WEBVIEW2_WAIT_FOR_SCRIPT_DEBUGGER',
    'WEBVIEW2_PIPE_FOR_SCRIPT_DEBUGGER'
)

function Invoke-Checked([string]$FileName, [string[]]$ArgumentList) {
    $commandOutput = @(& $FileName @ArgumentList 2>&1)
    $exitCode = $LASTEXITCODE
    $commandOutput | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) {
        throw "Qualification command failed with exit code ${exitCode}: $FileName $($ArgumentList -join ' ')"
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
    Write-Host 'Repository-owned dotnet/testhost/vstest processes remaining after desktop test batch: 0'
}

function Get-TrxCount([string]$Path, [string]$Name) {
    [xml]$document = [IO.File]::ReadAllText($Path)
    $counters = $document.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters -or $null -eq $counters.Attributes[$Name]) {
        throw "The $Name counter is absent from $Path"
    }
    [int]$counters.Attributes[$Name].Value
}

function Invoke-TestBatch([string]$Name, [string]$Project, [string]$Filter) {
    $trxName = $Name + '-' + $AcceptanceRunId + '.trx'
    $trxPath = Join-Path $testResultRoot $trxName
    try {
        Invoke-Checked 'dotnet' @(
            'test', $Project, '-c', $Configuration, '--no-build', '--no-restore', '--nologo',
            '--filter', $Filter,
            '--logger', "trx;LogFileName=$trxName",
            '--results-directory', $testResultRoot)
    }
    finally {
        Stop-RepositoryOwnedTestProcess
    }

    [pscustomobject][ordered]@{
        name = $Name
        project = $Project.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        filter = $Filter
        trx_path = $trxPath.Substring($repositoryRoot.Length + 1).Replace('\', '/')
        trx_sha256 = Get-FoundationFileSha256 $trxPath
        passed = Get-TrxCount $trxPath 'passed'
        failed = Get-TrxCount $trxPath 'failed'
        skipped = Get-TrxCount $trxPath 'notExecuted'
        total = Get-TrxCount $trxPath 'total'
        tests = @(Get-FoundationTrxResults $trxPath)
    }
}

function Write-Utf8NoBom([string]$Path, [string]$Value) {
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function Get-DescendantProcesses([int]$RootProcessId) {
    $all = @(Get-CimInstance Win32_Process)
    $frontier = [Collections.Generic.Queue[int]]::new()
    $frontier.Enqueue($RootProcessId)
    $descendants = [Collections.Generic.List[object]]::new()
    while ($frontier.Count -gt 0) {
        $parent = $frontier.Dequeue()
        foreach ($child in @($all | Where-Object { [int]$_.ParentProcessId -eq $parent })) {
            $descendants.Add($child)
            $frontier.Enqueue([int]$child.ProcessId)
        }
    }
    return @($descendants)
}

function Measure-DesktopLaunch {
    param([string]$Executable, [string]$QualificationSession)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $hostProcess = Start-Process -FilePath $Executable -WorkingDirectory (Split-Path $Executable) -ArgumentList @('--qualification-session', $QualificationSession) -PassThru -WindowStyle Hidden
    $readyMilliseconds = $null
    $browserProcesses = @()
    try {
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            Start-Sleep -Milliseconds 250
            $hostProcess.Refresh()
            $browserProcesses = @(Get-DescendantProcesses $hostProcess.Id | Where-Object { $_.Name -eq 'msedgewebview2.exe' })
            if ($null -eq $readyMilliseconds -and $browserProcesses.Count -gt 0) {
                $readyMilliseconds = $timer.ElapsedMilliseconds
            }
            if ($null -ne $readyMilliseconds -and $attempt -ge 12) { break }
        }
        if ($null -eq $readyMilliseconds) { throw 'The release desktop host did not start its protected WebView2 runtime.' }
        $hostProcess.Refresh()
        $forbiddenMarkers = @(
            '--remote-debugging-port',
            'WEBVIEW2_PIPE_FOR_SCRIPT_DEBUGGER',
            'WEBVIEW2_WAIT_FOR_SCRIPT_DEBUGGER',
            'script-debugger',
            $env:INFINIUM_DESKTOP_SECRET_CANARY
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        $commandText = (($browserProcesses | ForEach-Object { [string]$_.CommandLine }) -join "`n")
        $overrideMarkersAbsent = @($forbiddenMarkers | Where-Object {
            $commandText.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -ge 0
        }).Count -eq 0
        $stableEvergreenPaths = $browserProcesses.Count -gt 0 -and @($browserProcesses | Where-Object {
            [string]::IsNullOrWhiteSpace([string]$_.ExecutablePath) -or
            ([string]$_.ExecutablePath).IndexOf('\Microsoft\EdgeWebView\Application\', [StringComparison]::OrdinalIgnoreCase) -lt 0 -or
            ([string]$_.ExecutablePath).IndexOf($QualificationSession, [StringComparison]::OrdinalIgnoreCase) -ge 0
        }).Count -eq 0
        [pscustomobject]@{
            browser_ready_milliseconds = $readyMilliseconds
            host_private_bytes = $hostProcess.PrivateMemorySize64
            browser_process_count = $browserProcesses.Count
            browser_private_bytes = (($browserProcesses | ForEach-Object { [uint64]$_.PrivatePageCount } | Measure-Object -Sum).Sum)
            override_markers_absent = $overrideMarkersAbsent
            stable_evergreen_process_paths = $stableEvergreenPaths
        }
    }
    finally {
        $exactDescendants = @(Get-DescendantProcesses $hostProcess.Id)
        if (-not $hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id -Force -ErrorAction SilentlyContinue }
        foreach ($process in $exactDescendants) { Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }
    }
}

function Measure-Distribution([double[]]$Values) {
    $ordered = @($Values | Sort-Object)
    if ($ordered.Count -eq 0) { return $null }
    $p50 = $ordered[[Math]::Max(0, [Math]::Ceiling($ordered.Count * 0.50) - 1)]
    $p95 = $ordered[[Math]::Max(0, [Math]::Ceiling($ordered.Count * 0.95) - 1)]
    [ordered]@{ count = $ordered.Count; p50 = $p50; p95 = $p95; maximum = $ordered[-1] }
}

try {
    Set-Location -LiteralPath $repositoryRoot
    if ([string]::IsNullOrWhiteSpace($AcceptanceRunId)) {
        $AcceptanceRunId = [Guid]::NewGuid().ToString('N')
    }
    if ($AcceptanceRunId -notmatch '^[0-9a-f]{32}$') {
        throw 'The acceptance run identity must be an exact lowercase 128-bit hexadecimal value.'
    }
    $hasExpectedBinding = -not [string]::IsNullOrWhiteSpace($ExpectedCandidateCommit) -or
        -not [string]::IsNullOrWhiteSpace($ExpectedCandidateTree)
    if ($hasExpectedBinding -and
        ([string]::IsNullOrWhiteSpace($ExpectedCandidateCommit) -or
         [string]::IsNullOrWhiteSpace($ExpectedCandidateTree))) {
        throw 'Candidate commit and tree must be supplied together.'
    }
    $candidate = if ($hasExpectedBinding) {
        Assert-FoundationCandidateSnapshot `
            $repositoryRoot $ExpectedCandidateCommit $ExpectedCandidateTree 'desktop-qualification-start'
    } else {
        Get-FoundationCandidateSnapshot $repositoryRoot
    }
    foreach ($variable in $privilegedWebViewVariables) {
        $inheritedValue = [Environment]::GetEnvironmentVariable($variable)
        if ($null -ne $inheritedValue -and $inheritedValue.Length -ne 0) {
            throw 'Desktop qualification requires an override-free inherited WebView2 environment.'
        }
    }
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $testResultRoot -Force | Out-Null
    foreach ($staleReceipt in @($summaryPath, $runtimeMeasurements)) {
        if (Test-Path -LiteralPath $staleReceipt) {
            Remove-Item -LiteralPath $staleReceipt -Force
        }
    }
    $env:INFINIUM_DESKTOP_QUALIFICATION_ROOT = $qualificationRoot
    $env:INFINIUM_DESKTOP_QUALIFICATION_MEASUREMENTS = $runtimeMeasurements
    $env:INFINIUM_DESKTOP_SECRET_CANARY = 'INFINIUM-DESKTOP-QUALIFICATION-SECRET-CANARY-7B711839'

    Invoke-Checked 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'eng/invoke-frontend.ps1', '-Task', 'RestoreOffline')
    foreach ($frontendTask in @('CheckGenerated', 'CheckDesktop', 'TypeCheck', 'Lint', 'Test')) {
        Invoke-Checked 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'eng/invoke-frontend.ps1', '-Task', $frontendTask)
    }
    Invoke-Checked 'dotnet' @('build', $coordinatorProject, '-c', $Configuration, '--no-restore', '--nologo')
    Invoke-Checked 'dotnet' @('build', $desktopTestProject, '-c', $Configuration, '--no-restore', '--nologo')
    Invoke-Checked 'dotnet' @('build', $integrationTestProject, '-c', $Configuration, '--no-restore', '--nologo')
    $testBatches += Invoke-TestBatch `
        'ordinary-desktop' `
        $desktopTestProject `
        'TestCategory!=DesktopQualification&TestCategory!=DesktopLifecycleQualification'
    $env:INFINIUM_DESKTOP_PREFLIGHT_TESTS_PASSED = '1'
    $testBatches += Invoke-TestBatch `
        'desktop-state-preparation' `
        $integrationTestProject `
        'TestCategory=DesktopStatePreparation'
    $testBatches += Invoke-TestBatch `
        'live-desktop' `
        $desktopTestProject `
        'TestCategory=DesktopQualification'
    $testBatches += Invoke-TestBatch `
        'desktop-lifecycle' `
        $desktopTestProject `
        'TestCategory=DesktopLifecycleQualification'

    $hostOutput = Join-Path $repositoryRoot "src\Infinium.DesktopHost\bin\$Configuration\net10.0-windows"
    $hostExecutable = Join-Path $hostOutput 'Infinium.DesktopHost.exe'
    $launchSamples = @()
    for ($sample = 0; $sample -lt 6; $sample++) {
        $launchSamples += Measure-DesktopLaunch -Executable $hostExecutable -QualificationSession $qualificationSession
    }
    if (@($launchSamples | Where-Object { -not $_.override_markers_absent -or -not $_.stable_evergreen_process_paths }).Count -ne 0) {
        throw 'The repository-launched WebView2 process tree did not retain the stable override-free runtime boundary.'
    }
    $packageFiles = @(Get-ChildItem -LiteralPath $hostOutput -File -Recurse)
    $assetFiles = @(Get-ChildItem -LiteralPath (Join-Path $hostOutput 'Assets') -File -Recurse)
    $runtime = Get-Content -LiteralPath $runtimeMeasurements -Raw | ConvertFrom-Json
    $bridgeDistributions = [ordered]@{}
    foreach ($property in $runtime.milliseconds.psobject.Properties) {
        $bridgeDistributions[$property.Name] = Measure-Distribution @($property.Value)
    }
    $idleTotals = @($runtime.private_working_set_bytes.idle | ForEach-Object { [double]$_.Total })
    $activeTotals = @($runtime.private_working_set_bytes.active | ForEach-Object { [double]$_.Total })
    $runtimeFolder = Join-Path ${env:ProgramFiles(x86)} "Microsoft\EdgeWebView\Application\$($runtime.webview2_runtime)"
    $runtimeFiles = if (Test-Path -LiteralPath $runtimeFolder) { @(Get-ChildItem -LiteralPath $runtimeFolder -File -Recurse) } else { @() }
    $summary = [ordered]@{
        schema = 'infinium.desktop-qualification-summary/v1'
        recorded_at = [DateTimeOffset]::UtcNow.ToString('O')
        acceptance_run_id = $AcceptanceRunId
        candidate = [ordered]@{
            commit = $candidate.commit
            tree = $candidate.tree
        }
        test_batches = $testBatches
        reference_machine = [ordered]@{
            os = $runtime.os
            processor = $runtime.processor
            logical_processors = $runtime.logical_processors
            webview2_runtime = $runtime.webview2_runtime
        }
        contract = [ordered]@{
            renderer = $runtime.renderer_contract
            registry = $runtime.registry_version
            registry_sha256 = $runtime.registry_sha256
        }
        definitions = [ordered]@{
            process_to_browser_ready = 'Elapsed from Infinium.DesktopHost.exe process creation until its exclusive WebView2 user-data process tree is first observed.'
            window_show_to_bootstrap = 'Elapsed in the STA qualification host from WPF Show through exact-origin navigation, transport session establishment acknowledgement, and accepted real GetApplicationBootstrap rendering.'
            bridge_operation = 'Elapsed from a renderer button dispatch until the renderer records completion after the generated request, named-pipe application round trip, generated projection, exact bridge response, and React state update.'
            private_working_set = 'Private working set for the qualification WPF process plus every WebView2 process reported by its exclusive CoreWebView2Environment.'
        }
        launch = [ordered]@{
            cold = $launchSamples[0]
            warm = @($launchSamples | Select-Object -Skip 1)
            browser_ready_milliseconds = Measure-Distribution @($launchSamples | ForEach-Object { [double]$_.browser_ready_milliseconds })
        }
        bridge_raw_milliseconds = $runtime.milliseconds
        bridge_milliseconds = $bridgeDistributions
        private_working_set_raw_bytes = $runtime.private_working_set_bytes
        private_working_set_bytes = [ordered]@{
            idle = Measure-Distribution $idleTotals
            active = Measure-Distribution $activeTotals
        }
        observed_message_bytes = $runtime.observed_message_bytes
        maximum_message_bytes = $runtime.maximum_message_bytes
        maximum_chunk_bytes = $runtime.maximum_chunk_bytes
        maximum_queue_items = $runtime.maximum_queue_items
        package_file_count = $packageFiles.Count
        package_bytes = (($packageFiles | Measure-Object Length -Sum).Sum)
        packaged_asset_file_count = $assetFiles.Count
        packaged_asset_bytes = (($assetFiles | Measure-Object Length -Sum).Sum)
        largest_packaged_message_asset_bytes = (($assetFiles | Measure-Object Length -Maximum).Maximum)
        installed_webview_runtime_file_count = $runtimeFiles.Count
        installed_webview_runtime_bytes = (($runtimeFiles | Measure-Object Length -Sum).Sum)
        coverage = [ordered]@{
            inherited_override_preflight = [bool]$runtime.coverage.inherited_override_preflight
            stable_only_runtime_selection = [bool]$runtime.coverage.stable_only_runtime_selection
            recovery_revalidation = [bool]$runtime.coverage.recovery_revalidation
            no_override_value_echo = [bool]$runtime.coverage.no_override_value_echo
            repository_launched_process_tree_override_markers_absent = $true
            repository_launched_process_tree_stable_evergreen_paths = $true
        }
    }
    if ($hasExpectedBinding) {
        Assert-FoundationCandidateSnapshot `
            $repositoryRoot $ExpectedCandidateCommit $ExpectedCandidateTree 'desktop-qualification-end' | Out-Null
    }
    Write-Utf8NoBom $summaryPath ($summary | ConvertTo-Json -Depth 12)
    Write-Output "Desktop qualification summary: $summaryPath"
}
finally {
    $env:INFINIUM_DESKTOP_QUALIFICATION_ROOT = $priorRoot
    $env:INFINIUM_DESKTOP_QUALIFICATION_MEASUREMENTS = $priorMeasurements
    $env:INFINIUM_DESKTOP_SECRET_CANARY = $priorSecretCanary
    $env:INFINIUM_DESKTOP_PREFLIGHT_TESTS_PASSED = $priorPreflightEvidence
    $resolvedQualification = [IO.Path]::GetFullPath($qualificationRoot)
    if ([IO.Path]::GetDirectoryName($resolvedQualification) -ne $temporaryRoot -or
        -not [IO.Path]::GetFileName($resolvedQualification).StartsWith('infinium-desktop-qualification-', [StringComparison]::Ordinal)) {
        throw "Refusing unexpected qualification cleanup target: $resolvedQualification"
    }
    $exactRootProcesses = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('Infinium.Coordinator.exe', 'Infinium.DesktopHost.exe', 'msedgewebview2.exe') -and
        $_.CommandLine -and
        ($_.CommandLine.IndexOf($resolvedQualification, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
         $_.CommandLine.IndexOf($qualificationSession, [StringComparison]::OrdinalIgnoreCase) -ge 0)
    })
    foreach ($process in $exactRootProcesses) { Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 250
    $survivors = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('Infinium.Coordinator.exe', 'Infinium.DesktopHost.exe', 'msedgewebview2.exe') -and
        $_.CommandLine -and
        ($_.CommandLine.IndexOf($resolvedQualification, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
         $_.CommandLine.IndexOf($qualificationSession, [StringComparison]::OrdinalIgnoreCase) -ge 0)
    })
    if ($survivors.Count -eq 0 -and (Test-Path -LiteralPath $resolvedQualification)) {
        Remove-Item -LiteralPath $resolvedQualification -Recurse -Force
    }
    if (Test-Path -LiteralPath $summaryPath) {
        $recordedSummary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
        $recordedSummary | Add-Member -NotePropertyName cleanup_survivor_count -NotePropertyValue $survivors.Count -Force
        Write-Utf8NoBom $summaryPath ($recordedSummary | ConvertTo-Json -Depth 12)
    }
    Write-Output "Exact desktop qualification survivor count: $($survivors.Count)"
}
