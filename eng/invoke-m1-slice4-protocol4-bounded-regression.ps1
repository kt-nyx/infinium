[CmdletBinding()]
param(
    [string]$Mode = 'bounded-public-regression',
    [string]$Claim = 'historical-and-current-public-regression-health-only',
    [string]$ProfilePath = 'docs/evaluation/specifications/m1-slice4-protocol-4-bounded-regression-profile.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Refuse([string]$Message) {
    throw "BOUNDED_REGRESSION_REFUSED: $Message"
}

function Get-FileIdentity([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Refuse "required file is missing: $Path"
    }
    $item = Get-Item -LiteralPath $Path
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    return [pscustomobject]@{ ByteLength = [long]$item.Length; Sha256 = $hash }
}

function Get-BytesIdentity([byte[]]$Bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
    return [pscustomobject]@{ ByteLength = [long]$Bytes.Length; Sha256 = $hash }
}

function Get-GitBlobBytes([string]$RepositoryRoot, [string]$Commit, [string]$RelativePath) {
    $spec = "$Commit`:$RelativePath"
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'git'
    $startInfo.Arguments = 'cat-file blob "' + $spec + '"'
    $startInfo.WorkingDirectory = $RepositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $buffer = New-Object System.IO.MemoryStream
    try {
        $process.StandardOutput.BaseStream.CopyTo($buffer)
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            Refuse "historical Git blob is unavailable: $spec ($errorText)"
        }
        return $buffer.ToArray()
    }
    finally {
        $buffer.Dispose()
        $process.Dispose()
    }
}

function Assert-ExactStrings([object[]]$Actual, [string[]]$Expected, [string]$Label) {
    $actualStrings = @($Actual | ForEach-Object { [string]$_ })
    if ($actualStrings.Count -ne $Expected.Count) {
        Refuse "$Label count drifted: expected $($Expected.Count), observed $($actualStrings.Count)"
    }
    if (@($actualStrings | Select-Object -Unique).Count -ne $actualStrings.Count) {
        Refuse "$Label contains duplicates"
    }
    $actualSorted = @($actualStrings | Sort-Object)
    $expectedSorted = @($Expected | Sort-Object)
    for ($index = 0; $index -lt $expectedSorted.Count; $index++) {
        if ($actualSorted[$index] -cne $expectedSorted[$index]) {
            Refuse "$Label drifted"
        }
    }
}

function Assert-Identity([object]$Actual, [long]$ExpectedLength, [string]$ExpectedHash, [string]$Label) {
    if ([long]$Actual.ByteLength -ne $ExpectedLength -or [string]$Actual.Sha256 -cne $ExpectedHash) {
        Refuse "$Label identity drifted: expected $ExpectedLength/$ExpectedHash, observed $($Actual.ByteLength)/$($Actual.Sha256)"
    }
}

function Invoke-Dotnet([string[]]$Arguments, [string]$RepositoryRoot, [string]$Label) {
    Push-Location $RepositoryRoot
    try {
        $output = @(& dotnet @Arguments 2>&1 | ForEach-Object { [string]$_ })
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($exitCode -ne 0) {
        Refuse "$Label failed with exit code $exitCode`n$($output -join [Environment]::NewLine)"
    }
    return $output
}

if ($Mode -cne 'bounded-public-regression') {
    Refuse "mode '$Mode' is prohibited; only bounded-public-regression is permitted"
}
if ($Claim -cne 'historical-and-current-public-regression-health-only') {
    Refuse "claim '$Claim' is prohibited"
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedProfile = if ([IO.Path]::IsPathRooted($ProfilePath)) {
    [IO.Path]::GetFullPath($ProfilePath)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ProfilePath))
}
$repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedProfile.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    Refuse 'profile path must remain inside the public repository'
}
$profileParent = Get-Item -LiteralPath (Split-Path -Parent $resolvedProfile)
while ($profileParent.FullName.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    if (($profileParent.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        Refuse 'profile path may not traverse a reparse point'
    }
    $profileParent = $profileParent.Parent
}
$profileIdentity = Get-FileIdentity $resolvedProfile
if ([long]$profileIdentity.ByteLength -ne 4693 -or
    [string]$profileIdentity.Sha256 -cne '44ea3e8720056bbd35229a4bb727699a44b86ea155dab4218c0aaaae332cf803') {
    Refuse 'bounded-regression profile bytes drifted'
}
$profile = Get-Content -Raw -LiteralPath $resolvedProfile | ConvertFrom-Json

if ([string]$profile.schema_id -cne 'infinium.m1-slice4.protocol-4-bounded-regression-profile/1' -or
    [string]$profile.schema_version -cne '1.0.0' -or
    [string]$profile.status -cne 'accepted-bounded-historical-regression' -or
    [string]$profile.mode -cne $Mode -or
    [string]$profile.claim -cne $Claim) {
    Refuse 'profile identity, status, mode, or claim drifted'
}

$freeze = $profile.historical_freeze
if ([string]$freeze.manifest_path -cne 'docs/evaluation/evaluator-v2-stage-a-final-bounded-freeze.json' -or
    [long]$freeze.manifest_byte_length -ne 6972 -or
    [string]$freeze.manifest_sha256 -cne '2e30980f9e8628bf88c519e12c510c86a9c3ff2f6a7374b796fd8e6b769907d6' -or
    [string]$freeze.evaluator_commit -cne '3693d19563c636cd2879804633ca4ce52448d2c1' -or
    [string]$freeze.protocol_id -cne 'infinium.evaluator-v2/4' -or
    [string]$freeze.protocol_version -cne '4.0.0' -or
    [string]$freeze.projection_id -cne 'infinium.evaluator-v2.slice4-semantic-projection' -or
    [string]$freeze.projection_version -cne '3.0.0' -or
    [int]$freeze.required_blob_count -ne 23) {
    Refuse 'historical freeze identity drifted'
}

$manifestPath = Join-Path $repositoryRoot ([string]$freeze.manifest_path)
$manifestIdentity = Get-FileIdentity $manifestPath
Assert-Identity $manifestIdentity ([long]$freeze.manifest_byte_length) ([string]$freeze.manifest_sha256) 'freeze manifest'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ([string]$manifest.frozen_public_evaluator_commit -cne [string]$freeze.evaluator_commit -or
    [string]$manifest.identities.protocol_id -cne [string]$freeze.protocol_id -or
    [string]$manifest.identities.protocol_version -cne [string]$freeze.protocol_version -or
    [string]$manifest.identities.projection_id -cne [string]$freeze.projection_id -or
    [string]$manifest.identities.projection_version -cne [string]$freeze.projection_version) {
    Refuse 'freeze manifest protocol or projection identity drifted'
}

$requiredFiles = @($manifest.required_public_files)
if ($requiredFiles.Count -ne 23 -or (@($requiredFiles.relative_path | Select-Object -Unique)).Count -ne 23) {
    Refuse 'freeze manifest required-public-file inventory is not the exact 23-file set'
}

$commitCheck = @(& git -C $repositoryRoot cat-file -e "$($freeze.evaluator_commit)^{commit}" 2>&1)
if ($LASTEXITCODE -ne 0) {
    Refuse "frozen evaluator commit is unavailable: $($commitCheck -join ' ')"
}

foreach ($file in $requiredFiles) {
    $blob = Get-GitBlobBytes $repositoryRoot ([string]$freeze.evaluator_commit) ([string]$file.relative_path)
    $identity = Get-BytesIdentity $blob
    Assert-Identity $identity ([long]$file.byte_length) ([string]$file.sha256) "historical blob $($file.relative_path)"
}

$core = $profile.current_reusable_core
if ([string]$core.classification -cne 'all historical required_public_files paths outside tests/' -or
    [int]$core.required_file_count -ne 20 -or
    [string]$core.identity_source -cne 'historical freeze manifest required_public_files' -or
    -not [bool]$core.exact_current_bytes_required -or
    [bool]$core.extra_claimed_dependencies_permitted) {
    Refuse 'current reusable-core classification or dependency policy drifted'
}
$coreFiles = @($requiredFiles | Where-Object { -not ([string]$_.relative_path).StartsWith('tests/', [StringComparison]::Ordinal) })
$frozenTestFiles = @($requiredFiles | Where-Object { ([string]$_.relative_path).StartsWith('tests/', [StringComparison]::Ordinal) })
if ($coreFiles.Count -ne 20 -or $frozenTestFiles.Count -ne 3) {
    Refuse 'manifest path classification no longer yields exact 20-core/3-test layers'
}
foreach ($file in $coreFiles) {
    $currentPath = Join-Path $repositoryRoot ([string]$file.relative_path)
    $identity = Get-FileIdentity $currentPath
    Assert-Identity $identity ([long]$file.byte_length) ([string]$file.sha256) "current reusable core $($file.relative_path)"
}

$protocol = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'tools/evaluation/Infinium.EvaluatorV2/protocol/protocol.json') | ConvertFrom-Json
if ([string]$protocol.protocol_id -cne [string]$freeze.protocol_id -or
    [string]$protocol.version -cne [string]$freeze.protocol_version -or
    [string]$protocol.projection.id -cne [string]$freeze.projection_id -or
    [string]$protocol.projection.version -cne [string]$freeze.projection_version) {
    Refuse 'current protocol declaration identity drifted'
}

$regression = $profile.current_public_regression
if ([string]$regression.classification -cne 'current public regression evidence, never frozen qualification bytes' -or
    [string]$regression.authorized_change_commit -cne 'a98d648bd0adb2751ee0c09828e0227b1583950f') {
    Refuse 'current public-regression classification or provenance drifted'
}
$evolvedTests = @($regression.evolved_tests)
Assert-ExactStrings @($evolvedTests.relative_path) @($frozenTestFiles.relative_path) 'evolved public-test paths'
foreach ($test in $evolvedTests) {
    $identity = Get-FileIdentity (Join-Path $repositoryRoot ([string]$test.relative_path))
    Assert-Identity $identity ([long]$test.byte_length) ([string]$test.sha256) "current public regression test $($test.relative_path)"
    $changes = @(& git -C $repositoryRoot log --format=%H "$($freeze.evaluator_commit)..HEAD" -- ([string]$test.relative_path))
    if ($LASTEXITCODE -ne 0 -or $changes.Count -ne 1 -or [string]$changes[0] -cne [string]$regression.authorized_change_commit) {
        Refuse "current public regression test provenance drifted: $($test.relative_path)"
    }
}

$expectedTests = @(
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.PublicCalibrationDiscriminatesEveryDeclaredMutation',
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.TypedFactValidationFollowsDeclaredSemanticTypeThroughTheScorer',
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.CanonicalizerUsesIdFirstFormKeysAndNormalizesEmbeddedIdentities',
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.OracleAuthorityMatrixAndProjectorDeclareTheSameActiveFactFamilies',
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.ResultWriterRejectsEscapeAndOverwrite',
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.PassAttestationRetainsAndValidatesRequiredNullFailureStage',
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.ProductionSourcesContainNoEvaluatorFixtureOrPartitionPolicy',
    'Infinium.Tests.EvaluatorV2PublicProtocolTests.EvaluatorIdentityBindsExecutingRootDependenciesAndProtocolBytes'
)
Assert-ExactStrings @($regression.focused_test_cases) $expectedTests 'focused public regression tests'
Assert-ExactStrings @($profile.allowed_commands | ForEach-Object { [string]$_.id }) @('public-calibration', 'focused-public-regression') 'allowed commands'
Assert-ExactStrings @($profile.excluded_semantic_states | ForEach-Object { [string]$_.id }) @('accepted-partial-race-data-object-retention') 'excluded semantic states'
Assert-ExactStrings @($profile.prohibited_modes) @('adapt', 'score', 'compare-prepared', 'score-corpus', 'private-corpus', 'held-out', 'full-current-semantics') 'prohibited modes'
Assert-ExactStrings @($profile.prohibited_claims) @('frozen-qualification-suite-pass', 'complete-current-semantic-pass', 'private-held-out-pass', 'slice-4.5-pass', 'current-product-verdict', 'm1-pass', 'reliable-product') 'prohibited claims'
Assert-ExactStrings @($profile.retired_identity_reservations) @(
    'infinium.evaluator-v2/5',
    'infinium.m1-slice4.protocol-5-evidence-contract/1.0.1',
    'infinium.m1-slice4.protocol-5-projection-representation/1.2.0',
    'infinium.evaluator-v2.slice4-semantic-projection/5.1.0',
    'infinium.evaluator-v2.slice4-projection-document.schema/v5.2'
) 'retired identity reservations'

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("infinium-p4-bounded-" + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
try {
    $calibrationDirectory = Join-Path $temporaryRoot 'calibration'
    $calibrationOutput = @(Invoke-Dotnet @(
        'run', '--project', 'tools/evaluation/Infinium.EvaluatorV2/Infinium.EvaluatorV2.csproj',
        '--configuration', 'Release', '--', 'calibrate', '--result-dir', $calibrationDirectory
    ) $repositoryRoot 'public calibration')
    if ($calibrationOutput.Count -eq 0 -or $calibrationOutput[-1] -cne 'PASS') {
        Refuse 'public calibration did not terminate with its historical PASS token'
    }
    $calibrationPath = Join-Path $calibrationDirectory 'calibration-results.json'
    $calibrationIdentity = Get-FileIdentity $calibrationPath
    Assert-Identity $calibrationIdentity ([long]$regression.calibration.result_byte_length) ([string]$regression.calibration.result_sha256) 'public calibration result'
    $calibration = Get-Content -Raw -LiteralPath $calibrationPath | ConvertFrom-Json
    if ([string]$calibration.suite_id -cne [string]$regression.calibration.suite_id -or
        @($calibration.cases).Count -ne [int]$regression.calibration.case_count -or
        -not [bool]$calibration.passed -or
        @($calibration.cases | Where-Object { -not [bool]$_.passed }).Count -ne 0) {
        Refuse 'public calibration cases, identity, or result drifted'
    }

    $testResults = Join-Path $temporaryRoot 'tests'
    [IO.Directory]::CreateDirectory($testResults) | Out-Null
    $filter = ($expectedTests | ForEach-Object { "FullyQualifiedName=$_" }) -join '|'
    $testOutput = Invoke-Dotnet @(
        'test', 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj',
        '--configuration', 'Release', '--nologo', '--filter', $filter,
        '--logger', 'trx;LogFileName=bounded-regression.trx', '--results-directory', $testResults
    ) $repositoryRoot 'focused public regression'
    $trxPath = Join-Path $testResults 'bounded-regression.trx'
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        Refuse 'focused public regression did not produce its required TRX result'
    }
    [xml]$trx = Get-Content -Raw -LiteralPath $trxPath
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters -or
        [int]$counters.total -ne $expectedTests.Count -or
        [int]$counters.executed -ne $expectedTests.Count -or
        [int]$counters.passed -ne $expectedTests.Count -or
        [int]$counters.failed -ne 0 -or
        [int]$counters.error -ne 0 -or
        [int]$counters.notExecuted -ne 0) {
        Refuse 'focused public regression did not pass exactly the allowlisted tests'
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

$currentCommit = @(& git -C $repositoryRoot rev-parse HEAD)
if ($LASTEXITCODE -ne 0 -or $currentCommit.Count -ne 1) {
    Refuse 'current commit identity is unavailable'
}
Write-Output "profile_sha256=$($profileIdentity.Sha256)"
Write-Output "historical_freeze_blobs=23/23 commit=$($freeze.evaluator_commit)"
Write-Output 'current_reusable_core=20/20 exact-frozen-bytes'
Write-Output "current_public_regression_commit=$($currentCommit[0]) evolved_tests=3/3 focused_tests=$($expectedTests.Count)/$($expectedTests.Count) calibration_cases=$($regression.calibration.case_count)/$($regression.calibration.case_count)"
Write-Output 'excluded_semantic_state=accepted-partial-race-data-object-retention'
Write-Output 'claim_boundary=historical-core-and-current-public-regression-health-only; no frozen-suite, complete-current, held-out, Slice4.5, M1, reliability, or product verdict'
Write-Output 'BOUNDED_REGRESSION_PASS'
