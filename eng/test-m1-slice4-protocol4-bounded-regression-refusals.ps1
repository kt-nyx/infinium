[CmdletBinding()]
param(
    [string]$WrapperPath = 'eng/invoke-m1-slice4-protocol4-bounded-regression.ps1',
    [string]$ProfilePath = 'docs/evaluation/specifications/m1-slice4-protocol-4-bounded-regression-profile.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$wrapper = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $WrapperPath))
$profileFile = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ProfilePath))
$hostExecutable = if ($PSVersionTable.PSEdition -eq 'Core') {
    Join-Path $PSHOME 'pwsh.exe'
}
else {
    Join-Path $PSHOME 'powershell.exe'
}
if (-not (Test-Path -LiteralPath $hostExecutable -PathType Leaf)) {
    throw "Current PowerShell host executable is unavailable: $hostExecutable"
}

$temporaryRoot = Join-Path $repositoryRoot ("work/infinium-p4-refusals-" + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
$encoding = New-Object System.Text.UTF8Encoding($false)
$passed = 0

function Invoke-ExpectedRefusal([string]$Name, [string[]]$Arguments) {
    $allArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $wrapper) + $Arguments
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $hostExecutable
    $startInfo.Arguments = (($allArguments | ForEach-Object { '"' + ([string]$_).Replace('"', '\"') + '"' }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        $output = ($stdout + [Environment]::NewLine + $stderr)
    }
    finally {
        $process.Dispose()
    }
    if ($exitCode -eq 0) {
        throw "$Name unexpectedly succeeded"
    }
    if ($output.Contains('BOUNDED_REGRESSION_PASS')) {
        throw "$Name emitted the success terminal while refusing"
    }
    if (-not $output.Contains('BOUNDED_REGRESSION_REFUSED')) {
        throw "$Name did not fail through the wrapper refusal boundary: $output"
    }
    $script:passed++
}

function New-MutatedProfile([string]$Name, [scriptblock]$Mutation) {
    $profile = Get-Content -Raw -LiteralPath $profileFile | ConvertFrom-Json
    & $Mutation $profile
    $path = Join-Path $temporaryRoot "$Name.json"
    [IO.File]::WriteAllText($path, ($profile | ConvertTo-Json -Depth 100), $encoding)
    return $path
}

try {
    Invoke-ExpectedRefusal 'prohibited-mode' @('-Mode', 'score', '-ProfilePath', $profileFile)
    Invoke-ExpectedRefusal 'prohibited-claim' @('-Claim', 'private-held-out-pass', '-ProfilePath', $profileFile)
    Invoke-ExpectedRefusal 'external-profile-path' @('-ProfilePath', (Join-Path ([IO.Path]::GetTempPath()) 'outside-public-repository.json'))

    $path = New-MutatedProfile 'protocol-identity' { param($profile) $profile.historical_freeze.protocol_id = 'infinium.evaluator-v2/3' }
    Invoke-ExpectedRefusal 'protocol-identity' @('-ProfilePath', $path)

    $path = New-MutatedProfile 'freeze-hash' { param($profile) $profile.historical_freeze.manifest_sha256 = ('0' * 64) }
    Invoke-ExpectedRefusal 'freeze-hash' @('-ProfilePath', $path)

    $path = New-MutatedProfile 'extra-core-dependency-claim' { param($profile) $profile.current_reusable_core.required_file_count = 21 }
    Invoke-ExpectedRefusal 'extra-core-dependency-claim' @('-ProfilePath', $path)

    $path = New-MutatedProfile 'current-test-hash' { param($profile) $profile.current_public_regression.evolved_tests[0].sha256 = ('0' * 64) }
    Invoke-ExpectedRefusal 'current-test-hash' @('-ProfilePath', $path)

    $path = New-MutatedProfile 'test-provenance' { param($profile) $profile.current_public_regression.authorized_change_commit = ('0' * 40) }
    Invoke-ExpectedRefusal 'test-provenance' @('-ProfilePath', $path)

    $path = New-MutatedProfile 'extra-command' {
        param($profile)
        $profile.allowed_commands += [pscustomobject]@{ id = 'private-score'; command = 'score-corpus' }
    }
    Invoke-ExpectedRefusal 'extra-command' @('-ProfilePath', $path)

    $path = New-MutatedProfile 'missing-gap-exclusion' { param($profile) $profile.excluded_semantic_states = @() }
    Invoke-ExpectedRefusal 'missing-gap-exclusion' @('-ProfilePath', $path)

    $path = New-MutatedProfile 'missing-reserved-identity' {
        param($profile)
        $profile.retired_identity_reservations = @($profile.retired_identity_reservations | Select-Object -Skip 1)
    }
    Invoke-ExpectedRefusal 'missing-reserved-identity' @('-ProfilePath', $path)
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

if ($passed -ne 11) {
    throw "Expected 11 refusal cases, observed $passed"
}
Write-Output 'refusal_cases=11/11'
Write-Output 'REFUSAL_TESTS_PASS'
