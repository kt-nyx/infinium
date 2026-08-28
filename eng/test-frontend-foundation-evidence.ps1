[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'frontend-foundation-evidence.ps1')

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('infinium-foundation-evidence-' + [Guid]::NewGuid().ToString('N'))
$passedMutations = 0

function Write-Utf8NoBom([string]$Path, [string]$Value) {
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function Write-Trx([string]$Path, [string]$Outcome, [bool]$IncludeRequiredTest = $true) {
    $result = if ($IncludeRequiredTest) {
        '<UnitTestResult executionId="execution-required" testId="test-required" testName="RequiredPasses" outcome="' + $Outcome + '" />'
    } else { '' }
    $definition = if ($IncludeRequiredTest) {
        '<UnitTest name="RequiredPasses" id="test-required"><TestMethod className="Example.Tests" name="RequiredPasses" /></UnitTest>'
    } else { '' }
    Write-Utf8NoBom $Path @"
<TestRun>
  <Results>$result</Results>
  <TestDefinitions>$definition</TestDefinitions>
</TestRun>
"@
}

function Confirm-ExpectedFailure([string]$Name, [scriptblock]$Action, [string]$MessagePattern) {
    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) {
            throw "Mutation '$Name' failed for the wrong reason: $($_.Exception.Message)"
        }
        $script:passedMutations++
        return
    }
    throw "Mutation '$Name' was incorrectly accepted."
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    $runStartedAt = [DateTimeOffset]::UtcNow.AddSeconds(-1)
    $validTrx = Join-Path $temporaryRoot 'valid.trx'
    Write-Trx $validTrx 'Passed'
    $proof = [pscustomobject]@{ fully_qualified_name = 'Example.Tests.RequiredPasses' }
    $validHash = Get-FoundationFileSha256 $validTrx
    $valid = Assert-FoundationTestProof `
        -Proof $proof `
        -Catalog @('Example.Tests.RequiredPasses', 'Example.Tests.ExistsButWasNotRun') `
        -SelectedTests @('Example.Tests.RequiredPasses') `
        -TrxPath $validTrx `
        -ExpectedTrxSha256 $validHash `
        -RunStartedAt $runStartedAt
    if ($valid.result -cne 'verified-passed') { throw 'The valid executable proof did not verify.' }

    Confirm-ExpectedFailure 'nonexistent-selector' {
        Assert-FoundationTestProof `
            -Proof ([pscustomobject]@{ fully_qualified_name = 'Example.Tests.Misspelled' }) `
            -Catalog @('Example.Tests.RequiredPasses') `
            -SelectedTests @('Example.Tests.Misspelled') `
            -TrxPath $validTrx `
            -ExpectedTrxSha256 $validHash `
            -RunStartedAt $runStartedAt
    } 'does not exist'

    Confirm-ExpectedFailure 'mismatched-test-project' {
        Assert-FoundationTestProjectBinding `
            ([pscustomobject]@{ proof_id = 'project-mismatch'; project = 'tests/Expected/Expected.csproj' }) `
            'tests/Executed/Executed.csproj'
    } 'does not match its executed test batch'

    Confirm-ExpectedFailure 'existing-test-not-executed' {
        Assert-FoundationTestProof `
            -Proof ([pscustomobject]@{ fully_qualified_name = 'Example.Tests.ExistsButWasNotRun' }) `
            -Catalog @('Example.Tests.ExistsButWasNotRun') `
            -SelectedTests @('Example.Tests.ExistsButWasNotRun') `
            -TrxPath $validTrx `
            -ExpectedTrxSha256 $validHash `
            -RunStartedAt $runStartedAt
    } 'was not executed'

    foreach ($outcome in @('Failed', 'NotExecuted')) {
        $outcomeTrx = Join-Path $temporaryRoot ($outcome.ToLowerInvariant() + '.trx')
        Write-Trx $outcomeTrx $outcome
        $outcomeHash = Get-FoundationFileSha256 $outcomeTrx
        Confirm-ExpectedFailure ($outcome.ToLowerInvariant() + '-required-test') {
            Assert-FoundationTestProof `
                -Proof $proof `
                -Catalog @('Example.Tests.RequiredPasses') `
                -SelectedTests @('Example.Tests.RequiredPasses') `
                -TrxPath $outcomeTrx `
                -ExpectedTrxSha256 $outcomeHash `
                -RunStartedAt $runStartedAt
        } 'did not pass'
    }

    Confirm-ExpectedFailure 'missing-required-test' {
        Assert-FoundationTestProof `
            -Proof $proof `
            -Catalog @('Example.Tests.RequiredPasses') `
            -SelectedTests @('Example.Tests.RequiredPasses') `
            -TrxPath (Join-Path $temporaryRoot 'missing.trx') `
            -ExpectedTrxSha256 '' `
            -RunStartedAt $runStartedAt
    } 'TRX is missing'

    Confirm-ExpectedFailure 'substituted-trx' {
        Assert-FoundationTestProof `
            -Proof $proof `
            -Catalog @('Example.Tests.RequiredPasses') `
            -SelectedTests @('Example.Tests.RequiredPasses') `
            -TrxPath $validTrx `
            -ExpectedTrxSha256 ('0' * 64) `
            -RunStartedAt $runStartedAt
    } 'hash does not match'

    $staleTrx = Join-Path $temporaryRoot 'stale.trx'
    [IO.File]::Copy($validTrx, $staleTrx)
    [IO.File]::SetLastWriteTimeUtc($staleTrx, [DateTime]::UtcNow.AddMinutes(-10))
    $staleHash = Get-FoundationFileSha256 $staleTrx
    Confirm-ExpectedFailure 'stale-trx' {
        Assert-FoundationTestProof `
            -Proof $proof `
            -Catalog @('Example.Tests.RequiredPasses') `
            -SelectedTests @('Example.Tests.RequiredPasses') `
            -TrxPath $staleTrx `
            -ExpectedTrxSha256 $staleHash `
            -RunStartedAt $runStartedAt
    } 'predates this acceptance run'

    $desktop = [pscustomobject]@{
        acceptance_run_id = 'run-current'
        candidate = [pscustomobject]@{ commit = ('a' * 40 -join ''); tree = ('b' * 40 -join '') }
        cleanup_survivor_count = 0
    }
    Confirm-ExpectedFailure 'stale-desktop-receipt' {
        Assert-FoundationDesktopReceiptBinding $desktop ('c' * 40 -join '') ('b' * 40 -join '') 'run-current'
    } 'stale, substituted'
    Confirm-ExpectedFailure 'substituted-desktop-receipt' {
        Assert-FoundationDesktopReceiptBinding $desktop ('a' * 40 -join '') ('b' * 40 -join '') 'run-substituted'
    } 'stale, substituted'

    $machinePath = Join-Path $temporaryRoot 'machine.json'
    Write-Utf8NoBom $machinePath '{"present":true}'
    $machineProof = [pscustomobject]@{
        path = [IO.Path]::GetFileName($machinePath)
        json_pointer = '/missing'
        predicate = [pscustomobject]@{ operator = 'equals'; expected = $true }
    }
    Confirm-ExpectedFailure 'missing-machine-field' {
        Assert-FoundationMachineEvidence $machineProof $temporaryRoot ''
    } 'field is missing'

    Confirm-ExpectedFailure 'dirty-candidate' {
        $repository = Join-Path $temporaryRoot 'candidate'
        New-Item -ItemType Directory -Path $repository | Out-Null
        & git -C $repository init --quiet
        & git -C $repository config user.name 'Infinium evidence test'
        & git -C $repository config user.email 'evidence-test@invalid.local'
        Write-Utf8NoBom (Join-Path $repository 'tracked.txt') 'tracked'
        & git -C $repository add tracked.txt
        & git -C $repository commit --quiet -m 'evidence fixture'
        $commit = (& git -C $repository rev-parse HEAD).Trim()
        $tree = (& git -C $repository rev-parse 'HEAD^{tree}').Trim()
        Write-Utf8NoBom (Join-Path $repository 'dirty.txt') 'dirty'
        Assert-FoundationCandidateSnapshot $repository $commit $tree 'mutation'
    } 'worktree is dirty'

    $cleanRepository = Join-Path $temporaryRoot 'clean-candidate'
    New-Item -ItemType Directory -Path $cleanRepository | Out-Null
    & git -C $cleanRepository init --quiet
    & git -C $cleanRepository config user.name 'Infinium evidence test'
    & git -C $cleanRepository config user.email 'evidence-test@invalid.local'
    Write-Utf8NoBom (Join-Path $cleanRepository 'tracked.txt') 'tracked'
    & git -C $cleanRepository add tracked.txt
    & git -C $cleanRepository commit --quiet -m 'evidence fixture'
    $cleanTree = (& git -C $cleanRepository rev-parse 'HEAD^{tree}').Trim()
    Confirm-ExpectedFailure 'mismatched-candidate' {
        Assert-FoundationCandidateSnapshot $cleanRepository ('f' * 40 -join '') $cleanTree 'mutation'
    } 'not expected commit'

    Confirm-ExpectedFailure 'unverified-workflow-step' {
        Assert-FoundationWorkflowStep @(
            [pscustomobject]@{
                proof_id = 'unverified'
                required = $true
                behavioral = $true
                result = 'unverified'
            }) 16
    } 'unverified required proof'

    Write-Output "Frontend foundation evidence mutation checks passed: $passedMutations"
}
finally {
    $resolvedTemporary = [IO.Path]::GetFullPath($temporaryRoot)
    $temporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ($resolvedTemporary.StartsWith($temporaryParent, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedTemporary).StartsWith('infinium-foundation-evidence-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force -ErrorAction SilentlyContinue
    }
}
