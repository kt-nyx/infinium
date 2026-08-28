Set-StrictMode -Version Latest

function Get-FoundationFileSha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required evidence file is missing: $Path"
    }

    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-FoundationCandidateSnapshot([string]$RepositoryRoot) {
    $commit = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
        throw 'Unable to resolve the candidate commit.'
    }

    $tree = (& git -C $RepositoryRoot rev-parse 'HEAD^{tree}').Trim()
    if ($LASTEXITCODE -ne 0 -or $tree -notmatch '^[0-9a-f]{40}$') {
        throw 'Unable to resolve the candidate tree.'
    }

    $status = @(& git -C $RepositoryRoot status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the candidate worktree.'
    }

    [pscustomobject][ordered]@{
        commit = $commit
        tree = $tree
        clean = $status.Count -eq 0
        changes = @($status)
    }
}

function Assert-FoundationCandidateSnapshot(
    [string]$RepositoryRoot,
    [string]$ExpectedCommit,
    [string]$ExpectedTree,
    [string]$Stage
) {
    if ($ExpectedCommit -notmatch '^[0-9a-f]{40}$' -or $ExpectedTree -notmatch '^[0-9a-f]{40}$') {
        throw 'Expected candidate commit and tree identities must be exact lowercase Git object IDs.'
    }

    $snapshot = Get-FoundationCandidateSnapshot $RepositoryRoot
    if (-not $snapshot.clean) {
        throw "The $Stage candidate worktree is dirty and cannot produce acceptance evidence: $($snapshot.changes -join '; ')"
    }
    if ($snapshot.commit -cne $ExpectedCommit) {
        throw "The $Stage candidate commit is $($snapshot.commit), not expected commit $ExpectedCommit."
    }
    if ($snapshot.tree -cne $ExpectedTree) {
        throw "The $Stage candidate tree is $($snapshot.tree), not expected tree $ExpectedTree."
    }

    $snapshot
}

function Get-FoundationTrxResults([string]$Path) {
    [xml]$document = [IO.File]::ReadAllText($Path)
    $definitions = @{}
    foreach ($unitTest in @($document.SelectNodes("//*[local-name()='UnitTest']"))) {
        $method = $unitTest.SelectSingleNode("./*[local-name()='TestMethod']")
        if ($null -eq $method) { continue }
        $testId = [string]$unitTest.id
        $className = [string]$method.className
        $methodName = [string]$method.name
        if ([string]::IsNullOrWhiteSpace($testId) -or
            [string]::IsNullOrWhiteSpace($className) -or
            [string]::IsNullOrWhiteSpace($methodName)) {
            continue
        }
        $definitions[$testId] = $className + '.' + $methodName
    }

    $results = @()
    foreach ($result in @($document.SelectNodes("//*[local-name()='UnitTestResult']"))) {
        $testId = [string]$result.testId
        $fullyQualifiedName = $definitions[$testId]
        if ([string]::IsNullOrWhiteSpace($fullyQualifiedName)) {
            throw "TRX result $testId has no fully qualified test definition in $Path."
        }
        $results += [pscustomobject][ordered]@{
            fully_qualified_name = $fullyQualifiedName
            outcome = ([string]$result.outcome).ToLowerInvariant()
            test_id = $testId
            execution_id = [string]$result.executionId
        }
    }
    $results
}

function Assert-FoundationTestProof(
    [object]$Proof,
    [string[]]$Catalog,
    [string[]]$SelectedTests,
    [string]$TrxPath,
    [string]$ExpectedTrxSha256,
    [DateTimeOffset]$RunStartedAt
) {
    $identity = [string]$Proof.fully_qualified_name
    if ($Catalog -cnotcontains $identity) {
        throw "Executable proof selector does not exist in the declared project: $identity"
    }
    if ($SelectedTests -cnotcontains $identity) {
        throw "Executable proof test exists but was not selected: $identity"
    }
    if (-not (Test-Path -LiteralPath $TrxPath -PathType Leaf)) {
        throw "Required TRX is missing for ${identity}: $TrxPath"
    }
    if ((Get-Item -LiteralPath $TrxPath).LastWriteTimeUtc -lt $RunStartedAt.UtcDateTime) {
        throw "Required TRX predates this acceptance run: $TrxPath"
    }

    $actualHash = Get-FoundationFileSha256 $TrxPath
    if ($ExpectedTrxSha256 -and $actualHash -cne $ExpectedTrxSha256) {
        throw "Required TRX hash does not match its producer receipt: $TrxPath"
    }

    $matches = @(Get-FoundationTrxResults $TrxPath | Where-Object {
        $_.fully_qualified_name -ceq $identity
    })
    if ($matches.Count -eq 0) {
        throw "Required test exists and was selected but was not executed in the retained TRX: $identity"
    }
    if ($matches.Count -ne 1) {
        throw "Required test has $($matches.Count) retained TRX results instead of exactly one: $identity"
    }
    if ($matches[0].outcome -cne 'passed') {
        throw "Required test did not pass: $identity ($($matches[0].outcome))."
    }

    [pscustomobject][ordered]@{
        result = 'verified-passed'
        fully_qualified_name = $identity
        outcome = $matches[0].outcome
        test_id = $matches[0].test_id
        execution_id = $matches[0].execution_id
        trx_sha256 = $actualHash
    }
}

function Assert-FoundationTestProjectBinding([object]$Proof, [string]$ExecutedProject) {
    if ([string]$Proof.project -cne $ExecutedProject) {
        throw "Proof project does not match its executed test batch: $($Proof.proof_id)"
    }
    $true
}

function Get-FoundationJsonPointerValue([object]$Document, [string]$Pointer) {
    if ($Pointer -eq '') { return $Document }
    if (-not $Pointer.StartsWith('/', [StringComparison]::Ordinal)) {
        throw "JSON Pointer must be empty or begin with '/': $Pointer"
    }

    $current = $Document
    foreach ($encodedToken in $Pointer.Substring(1).Split('/')) {
        $token = $encodedToken.Replace('~1', '/').Replace('~0', '~')
        if ($current -is [Array]) {
            $index = 0
            if (-not [int]::TryParse($token, [Globalization.NumberStyles]::None,
                    [Globalization.CultureInfo]::InvariantCulture, [ref]$index) -or
                $index -lt 0 -or $index -ge $current.Count) {
                throw "JSON Pointer array index is missing: $Pointer"
            }
            $current = $current[$index]
            continue
        }

        $property = $current.PSObject.Properties[$token]
        if ($null -eq $property) {
            throw "Required JSON evidence field is missing: $Pointer"
        }
        $current = $property.Value
    }
    $current
}

function Test-FoundationEvidencePredicate([object]$Observed, [object]$Predicate) {
    $operator = [string]$Predicate.operator
    switch ($operator) {
        'equals' {
            $expected = $Predicate.expected
            if ($expected -is [ValueType] -and $Observed -is [ValueType]) {
                return ([string]$Observed -ceq [string]$expected)
            }
            return [object]::Equals($Observed, $expected)
        }
        'at-least' { return [decimal]$Observed -ge [decimal]$Predicate.expected }
        'at-most' { return [decimal]$Observed -le [decimal]$Predicate.expected }
        'matches' { return [string]$Observed -cmatch [string]$Predicate.expected }
        'nonempty' { return -not [string]::IsNullOrWhiteSpace([string]$Observed) }
        default { throw "Unknown evidence predicate operator: $operator" }
    }
}

function Assert-FoundationMachineEvidence(
    [object]$Proof,
    [string]$RepositoryRoot,
    [string]$ExpectedSourceSha256
) {
    $sourcePath = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot ([string]$Proof.path)))
    if (-not $sourcePath.StartsWith(
            $RepositoryRoot.TrimEnd('\') + '\',
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Machine-evidence path escapes the repository: $($Proof.path)"
    }
    $sourceHash = Get-FoundationFileSha256 $sourcePath
    if ($ExpectedSourceSha256 -and $sourceHash -cne $ExpectedSourceSha256) {
        throw "Machine-evidence source hash does not match its producer receipt: $($Proof.path)"
    }
    $document = Get-Content -LiteralPath $sourcePath -Raw | ConvertFrom-Json
    $observed = Get-FoundationJsonPointerValue $document ([string]$Proof.json_pointer)
    if (-not (Test-FoundationEvidencePredicate $observed $Proof.predicate)) {
        throw "Machine-evidence predicate failed at $($Proof.path)$($Proof.json_pointer)."
    }

    [pscustomobject][ordered]@{
        result = 'verified-passed'
        source_sha256 = $sourceHash
        json_pointer = [string]$Proof.json_pointer
        predicate = $Proof.predicate
        observed = $observed
    }
}

function Assert-FoundationDesktopReceiptBinding(
    [object]$Summary,
    [string]$ExpectedCommit,
    [string]$ExpectedTree,
    [string]$ExpectedRunId
) {
    if ([string]$Summary.acceptance_run_id -cne $ExpectedRunId -or
        [string]$Summary.candidate.commit -cne $ExpectedCommit -or
        [string]$Summary.candidate.tree -cne $ExpectedTree) {
        throw 'Desktop qualification receipt is stale, substituted, or bound to another candidate.'
    }
    if ([int]$Summary.cleanup_survivor_count -ne 0) {
        throw 'Desktop qualification left a repository-launched process survivor.'
    }
    $true
}

function Assert-FoundationWorkflowStep([object[]]$ProofResults, [int]$Step) {
    $requiredBehavioral = @($ProofResults | Where-Object {
        $_.required -and $_.behavioral
    })
    if ($requiredBehavioral.Count -eq 0) {
        throw "Workflow step $Step has no required behavioral proof."
    }
    $unverified = @($requiredBehavioral | Where-Object { $_.result -cne 'verified-passed' })
    if ($unverified.Count -ne 0) {
        throw "Workflow step $Step has an unverified required proof: $($unverified.proof_id -join ', ')."
    }
    'passed'
}
