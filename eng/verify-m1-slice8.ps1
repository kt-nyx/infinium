[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InputManifest,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    & $pwsh.Source -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
        -InputManifest $InputManifest -OutputRoot $OutputRoot -Configuration $Configuration
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = [IO.Path]::GetFullPath($InputManifest)
$resolvedOutput = [IO.Path]::GetFullPath($OutputRoot)
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'The exact Slice 8 input manifest does not exist.'
}
if (Test-Path -LiteralPath $resolvedOutput) {
    if ($null -ne (Get-ChildItem -LiteralPath $resolvedOutput -Force | Select-Object -First 1)) {
        throw 'Slice 8 verification requires a fresh empty output root.'
    }
} else {
    New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
}

$candidateCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
$candidateTree = (& git -C $repoRoot rev-parse 'HEAD^{tree}').Trim()
if ($LASTEXITCODE -ne 0) { throw 'The candidate Git identity could not be resolved.' }
$manifestFingerprint = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
$commands = [Collections.Generic.List[object]]::new()

function Invoke-Slice8Test(
    [string] $Project,
    [string] $Filter,
    [bool] $ControlledReal = $false) {
    $start = [Diagnostics.Stopwatch]::StartNew()
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet'
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @('test', $Project, '-c', $Configuration, '--no-build', '--nologo',
            '--filter', $Filter, '--logger', 'console;verbosity=minimal')) {
        [void] $startInfo.ArgumentList.Add($argument)
    }
    if ($ControlledReal) {
        $startInfo.Environment['INFINIUM_SLICE8_INPUT_MANIFEST'] = $manifestPath
        $startInfo.Environment['INFINIUM_SLICE8_OUTPUT_ROOT'] = $resolvedOutput
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw "Could not start Slice 8 test project $Project." }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(600000)) {
        try { $process.Kill($true) } finally { $process.WaitForExit() }
        throw "Slice 8 test project $Project timed out after 600000 ms."
    }
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $start.Stop()
    $combined = $stdout + [Environment]::NewLine + $stderr
    $combined -split "`r?`n" | Where-Object Length | ForEach-Object { Write-Host $_ }
    if ($process.ExitCode -ne 0) { throw "Slice 8 test project $Project failed." }
    $match = [regex]::Match($combined,
        'Failed:\s+(?<failed>\d+),\s+Passed:\s+(?<passed>\d+),\s+Skipped:\s+(?<skipped>\d+),\s+Total:\s+(?<total>\d+)')
    if (-not $match.Success) { throw "Slice 8 test project $Project emitted no parseable test count." }
    $total = [int] $match.Groups['total'].Value
    $skipped = [int] $match.Groups['skipped'].Value
    if ($total -eq 0 -or $skipped -ne 0) {
        throw "Slice 8 test project $Project discovered zero tests or skipped a mandatory test."
    }
    $commands.Add([ordered]@{
        command = "dotnet test $Project -c $Configuration --no-build --filter `"$Filter`""
        exit_code = $process.ExitCode
        passed = [int] $match.Groups['passed'].Value
        failed = [int] $match.Groups['failed'].Value
        skipped = $skipped
        total = $total
        duration_ms = $start.ElapsedMilliseconds
    })
}

Invoke-Slice8Test 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' `
    'FullyQualifiedName~ScopeReversionV2|FullyQualifiedName~ControlledRealInputAdmission'
Invoke-Slice8Test 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' `
    'FullyQualifiedName~ScopeReversionContractTests'
Invoke-Slice8Test 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' `
    'FullyQualifiedName~ScopeReversionV2PersistenceIntegrationTests|FullyQualifiedName~ScopeReversionPersistenceIntegrationTests'
Invoke-Slice8Test 'tests/Infinium.SecurityTests/Infinium.SecurityTests.csproj' `
    'FullyQualifiedName~ScopeReversionSecurityTests'
Invoke-Slice8Test 'tests/Infinium.FaultTests/Infinium.FaultTests.csproj' `
    'FullyQualifiedName~ScopeReversionFaultTests'
Invoke-Slice8Test 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' `
    'FullyQualifiedName~ControlledRealScopeReversionEvaluationTests|FullyQualifiedName~ScopeReversionConformanceEvaluationTests' $true

$outputFiles = @(Get-ChildItem -LiteralPath $resolvedOutput -File -Recurse | Sort-Object FullName)
if ($outputFiles.Count -eq 0) { throw 'Slice 8 verification produced no controlled output evidence.' }
$controlledReceiptPath = Join-Path $resolvedOutput 'controlled-real-results.json'
if (-not (Test-Path -LiteralPath $controlledReceiptPath -PathType Leaf)) {
    throw 'Slice 8 verification produced no controlled-real result receipt.'
}
$controlledReceipt = Get-Content -LiteralPath $controlledReceiptPath -Raw | ConvertFrom-Json
if (@($controlledReceipt.cases).Count -ne 4 -or @($controlledReceipt.public_manifests).Count -ne 3 -or
    @($controlledReceipt.controlled_inputs).Count -ne 26 -or [bool]$controlledReceipt.third_party_payload_bytes_written -or
    @($controlledReceipt.prohibited_boundaries).Count -ne 11 -or
    @($controlledReceipt.prohibited_boundaries | Where-Object state -ne 'NotUsed').Count -ne 0) {
    throw 'Slice 8 controlled-real receipt is incomplete or crosses a prohibited boundary.'
}
$outputMaterial = [Text.StringBuilder]::new()
foreach ($file in $outputFiles) {
    [void] $outputMaterial.Append([IO.Path]::GetRelativePath($resolvedOutput, $file.FullName).Replace('\', '/'))
    [void] $outputMaterial.Append('|')
    [void] $outputMaterial.Append((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
    [void] $outputMaterial.Append("`n")
}
$outputFingerprint = [Convert]::ToHexStringLower(
    [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($outputMaterial.ToString())))
$dirty = $null -ne (& git -C $repoRoot status --porcelain=v1 | Select-Object -First 1)
$totalTests = 0
$totalSkipped = 0
foreach ($commandResult in $commands) {
    $totalTests += [int] $commandResult['total']
    $totalSkipped += [int] $commandResult['skipped']
}
$receipt = [ordered]@{
    schema = 'infinium-m1-slice8-verification-receipt/1'
    status = 'passed'
    input_manifest_sha256 = $manifestFingerprint
    handoff_id = [string]$controlledReceipt.handoff_id
    public_manifests = @($controlledReceipt.public_manifests)
    controlled_input_count = @($controlledReceipt.controlled_inputs).Count
    candidate_commit = $candidateCommit
    candidate_tree = $candidateTree
    candidate_worktree_dirty = $dirty
    output_fingerprint = $outputFingerprint
    commands = @($commands)
    mandatory_command_count = $commands.Count
    total_tests = $totalTests
    total_skipped = $totalSkipped
    network_used = $false
    hosted_search_used = $false
    nexus_used = $false
    loot_used = $false
    credentials_used = $false
    provider_used = $false
    private_fixture_used = $false
    evaluator_private_used = $false
    semantic_oracle_used = $false
    archive_used = $false
    publication_used = $false
    push_used = $false
    external_effects_used = $false
    third_party_payload_bytes_written = $false
}
$receiptPath = Join-Path $resolvedOutput 'slice8-verification-receipt.json'
[IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))
Write-Host "Slice 8 focused verification passed: $($receipt.total_tests) tests; receipt=$receiptPath"
