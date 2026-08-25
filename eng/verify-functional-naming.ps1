[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$WriteBaseline,
    [switch]$SelfTest
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -eq $pwsh) {
        throw 'Functional naming verification requires PowerShell 7 or pwsh on PATH.'
    }

    $arguments = @('-NoProfile', '-File', $PSCommandPath)
    if (-not [string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $arguments += @('-RepositoryRoot', $RepositoryRoot)
    }
    if ($WriteBaseline) { $arguments += '-WriteBaseline' }
    if ($SelfTest) { $arguments += '-SelfTest' }
    & $pwsh.Source @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "PowerShell 7 functional naming verification failed with exit code $LASTEXITCODE."
    }
    return
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepositoryRoot = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    Split-Path -Parent $PSScriptRoot
} else {
    $RepositoryRoot
}
$repositoryRootPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path.TrimEnd('\')
$allowlistPath = Join-Path $repositoryRootPath 'eng/functional-naming-allowlist.json'

$tokenPattern = [regex]::new(
    '(?i)(?:(?<![A-Za-z0-9])M[0-9]+|Slice[._ -]?[0-9]+|(?<![A-Za-z0-9])S[0-9]+(?:\.[0-9]+)?|WP[._ -]?[0-9]+|Wave[._ -]?[A-Z]|PRE[._ -]?B[0-9]+|Campaign|Successor|Continuation|Pre[._ -]?Live|Post[._ -]?Success|Replacement[._ -]?Candidate|Approach)',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$structuredNameBearingPattern = [regex]::new(
    '(?i)\b(class|record|struct|interface|enum|namespace|const|static|public|internal|private|protected|function|param)\b|--[a-z]|INFINIUM_[A-Z]|schema_id|"\$id"',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
$contentExtensions = @('.cs', '.ps1', '.psm1', '.mjs', '.js', '.ts', '.tsx', '.proto', '.csproj', '.props', '.targets', '.json')
$structuredContentExtensions = @('.json')

function Get-TokenMatches {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Text)
    @($tokenPattern.Matches($Text) | ForEach-Object { $_.Value })
}

function Test-AllowlistMatch {
    param(
        [Parameter(Mandatory)]$Finding,
        [Parameter(Mandatory)]$Entry
    )
    $Finding.path -ceq $Entry.path -and
        $Finding.scope -ceq $Entry.scope -and
        $Finding.token -ceq $Entry.token
}

if ($SelfTest) {
    if (@(Get-TokenMatches 'ProviderUsageAccounting').Count -ne 0) {
        throw 'Self-test failed: functional name was rejected.'
    }
    if (@(Get-TokenMatches 'M1Slice6CampaignAccounting').Count -lt 2) {
        throw 'Self-test failed: representative planning name was not detected.'
    }
    $finding = [pscustomobject]@{ path = 'src/Example.cs'; scope = 'content'; token = 'Campaign' }
    $entry = [pscustomobject]@{ path = 'src/Example.cs'; scope = 'content'; token = 'Campaign' }
    if (-not (Test-AllowlistMatch -Finding $finding -Entry $entry)) {
        throw 'Self-test failed: exact exemption did not match.'
    }
    $entry.path = 'src/Other.cs'
    if (Test-AllowlistMatch -Finding $finding -Entry $entry) {
        throw 'Self-test failed: non-exact exemption matched.'
    }
    Write-Host 'Functional naming checker self-test passed.'
    return
}

$gitRoot = @(& git -C $repositoryRootPath rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0 -or $gitRoot.Count -ne 1 -or
    (Resolve-Path -LiteralPath $gitRoot[0]).Path.TrimEnd('\') -cne $repositoryRootPath) {
    throw 'RepositoryRoot must resolve to the exact Git root.'
}

$relativePaths = @(
    & git -C $repositoryRootPath ls-files --cached --others --exclude-standard |
        ForEach-Object { $_.Replace('\', '/') } |
        Where-Object {
            $_ -notmatch '^(docs|human-guide)/' -and
            $_ -notmatch '(^|/)(bin|obj|TestResults)/' -and
            $_ -notmatch '^\.packages/'
        } |
        Sort-Object -Unique
)
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate repository files.' }

$rawFindings = [System.Collections.Generic.List[object]]::new()
foreach ($relativePath in $relativePaths) {
    $absolutePath = Join-Path $repositoryRootPath $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) { continue }

    foreach ($token in Get-TokenMatches $relativePath) {
        $rawFindings.Add([pscustomobject]@{
            path = $relativePath
            scope = 'path'
            token = $token
            context = $relativePath
        })
    }

    $extension = [System.IO.Path]::GetExtension($relativePath)
    if ($contentExtensions -notcontains $extension) { continue }
    if ($relativePath -match '^fixtures/public/' -or
        $relativePath -ceq 'eng/functional-naming-allowlist.json') { continue }

    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($absolutePath)) {
        $lineNumber++
        if ($structuredContentExtensions -contains $extension -and
            -not $structuredNameBearingPattern.IsMatch($line)) { continue }
        foreach ($token in Get-TokenMatches $line) {
            $rawFindings.Add([pscustomobject]@{
                path = $relativePath
                scope = 'content'
                token = $token
                context = "line $lineNumber"
            })
        }
    }
}

$findings = @(
    $rawFindings |
        Group-Object path, scope, token |
        ForEach-Object {
            $first = $_.Group[0]
            [pscustomobject]@{
                path = $first.path
                scope = $first.scope
                token = $first.token
                contexts = @($_.Group.context | Sort-Object -Unique)
            }
        } |
        Sort-Object path, scope, token
)

if ($WriteBaseline) {
    $entries = @($findings | ForEach-Object {
        [ordered]@{
            path = $_.path
            scope = $_.scope
            token = $_.token
            symbol_or_context = ($_.contexts -join ', ')
            classification = 'unreviewed'
            reason = 'New exact finding generated for explicit compatibility or functional-domain review.'
            retained_consumer = 'Unknown until a reviewer identifies the current consumer.'
            review_condition = 'Classify the exception precisely or remove the planning-language token before acceptance.'
        }
    })
    $document = [ordered]@{
        schema_identity = 'infinium.repository.functional-naming-allowlist/1.0.0'
        status = 'implementation-active'
        generated_from_commit = (& git -C $repositoryRootPath rev-parse HEAD).Trim()
        generated_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        policy = 'docs/governance/functional-implementation-naming.md'
        entries = $entries
    }
    [System.IO.File]::WriteAllText(
        $allowlistPath,
        ($document | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
    Write-Host "Functional naming baseline written: $($entries.Count) exact entries."
    return
}

if (-not (Test-Path -LiteralPath $allowlistPath -PathType Leaf)) {
    throw "Functional naming allowlist is missing: $allowlistPath"
}
$allowlist = Get-Content -LiteralPath $allowlistPath -Raw | ConvertFrom-Json -Depth 16
$entries = @($allowlist.entries)
$failures = [System.Collections.Generic.List[string]]::new()
$findingKeys = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::Ordinal)
$entryKeys = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::Ordinal)

foreach ($finding in $findings) {
    $key = "$($finding.path)`u{001f}$($finding.scope)`u{001f}$($finding.token)"
    if ($findingKeys.ContainsKey($key)) {
        $findingKeys[$key]++
    } else {
        $findingKeys.Add($key, 1)
    }
}
foreach ($entry in $entries) {
    $key = "$($entry.path)`u{001f}$($entry.scope)`u{001f}$($entry.token)"
    if ($entryKeys.ContainsKey($key)) {
        $entryKeys[$key]++
    } else {
        $entryKeys.Add($key, 1)
    }
    foreach ($required in @('symbol_or_context', 'classification', 'reason', 'retained_consumer', 'review_condition')) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.$required)) {
            $failures.Add("Incomplete allowlist entry field '$required': $($entry.path) [$($entry.scope)] '$($entry.token)'")
        }
    }
    if ($entry.classification -ceq 'unreviewed') {
        $failures.Add("Unreviewed naming allowlist entry: $($entry.path) [$($entry.scope)] '$($entry.token)'")
    }
}
foreach ($finding in $findings) {
    $key = "$($finding.path)`u{001f}$($finding.scope)`u{001f}$($finding.token)"
    if (-not $entryKeys.ContainsKey($key) -or $entryKeys[$key] -ne 1) {
        $failures.Add("Unexplained naming finding: $($finding.path) [$($finding.scope)] '$($finding.token)'")
    }
}
foreach ($entry in $entries) {
    $key = "$($entry.path)`u{001f}$($entry.scope)`u{001f}$($entry.token)"
    if (-not $findingKeys.ContainsKey($key) -or $findingKeys[$key] -ne 1 -or $entryKeys[$key] -ne 1) {
        $failures.Add("Stale or duplicate allowlist entry: $($entry.path) [$($entry.scope)] '$($entry.token)'")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { [Console]::Error.WriteLine($_) }
    throw "Functional naming verification failed with $($failures.Count) finding(s)."
}

Write-Host "Functional naming verification passed: $($findings.Count) exact reviewed exceptions, zero unexplained findings."
