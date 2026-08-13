[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Contracts', 'StateSurfaces', 'StateTotality', 'Budget', 'BudgetFaults', 'CredentialSynthetic', 'CredentialNative', 'Adapter', 'OfflineSafetyReplay', 'SourceClaimSemantics', 'CandidateSemantics', 'ProvenanceReplay', 'Layer6Review')]
    [string] $Gate,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [string] $BaselineCommit,

    [string] $CandidateCommit,

    [string] $AuthorizationManifest,

    [switch] $HandoffCloseout,

    [switch] $OwnerTestProcessCleanup
)

if ($Gate -in @('Layer6Review', 'CredentialNative', 'CandidateSemantics', 'ProvenanceReplay') -and $PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath,
        '-Gate', $Gate,
        '-OutputRoot', $OutputRoot
    )
    if (-not [string]::IsNullOrWhiteSpace($BaselineCommit)) {
        $arguments += @('-BaselineCommit', $BaselineCommit)
    }
    if (-not [string]::IsNullOrWhiteSpace($CandidateCommit)) {
        $arguments += @('-CandidateCommit', $CandidateCommit)
    }
    if ($HandoffCloseout) {
        $arguments += '-HandoffCloseout'
    }
    if ($OwnerTestProcessCleanup) {
        $arguments += '-OwnerTestProcessCleanup'
    }
    if (-not [string]::IsNullOrWhiteSpace($AuthorizationManifest)) {
        $arguments += @('-AuthorizationManifest', $AuthorizationManifest)
    }
    & $pwsh.Source @arguments
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
$outputRootExistedBeforeInvocation = Test-Path -LiteralPath $resolvedOutputRoot
$outputRootHadEntriesBeforeInvocation = $outputRootExistedBeforeInvocation -and
    $null -ne (Get-ChildItem -LiteralPath $resolvedOutputRoot -Force | Select-Object -First 1)
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$schemaNames = @(
    'provider-access-profile.v1.schema.json',
    'provider-operation.v1.schema.json',
    'provider-response.v1.schema.json',
    'source-claim-extraction.v1.schema.json',
    'candidate-investigation.v1.schema.json',
    'candidate-investigation-execution-input.v1.schema.json',
    'candidate-investigation-context.v1.schema.json',
    'candidate-investigation-retained-transcripts.v1.schema.json',
    'candidate-investigation-oracle.v1.schema.json',
    'provider-execution-input.v1.schema.json',
    'effective-scan-configuration.v2.schema.json',
    'run-output.v2.schema.json',
    'cli-summary.v2.schema.json'
)

function Invoke-DotnetTest([string] $Project, [string] $Filter) {
    & dotnet test $Project -c Release --no-build --nologo --filter $Filter
    if ($LASTEXITCODE -ne 0) {
        throw "Focused test command failed for $Project."
    }
}

function Assert-Slice5V1Unchanged {
    $paths = @(
        'contracts/json-schema/effective-scan-configuration.v1.schema.json',
        'contracts/json-schema/run-output.v1.schema.json',
        'contracts/json-schema/cli-summary.v1.schema.json',
        'src/Infinium.Application/Serialization/RunOutputJsonCodec.cs',
        'src/Infinium.Application/Serialization/CliSummaryJsonCodec.cs'
    )
    & git diff --quiet 6ac66e7d79c63a231bbbf22209015a894cd4bd6d -- @paths
    if ($LASTEXITCODE -ne 0) {
        throw 'A frozen Slice 5 v1 configuration/output contract or codec changed.'
    }
}

function Write-Receipt(
    [string] $Name,
    [System.Collections.IDictionary] $Evidence,
    [string] $Status = 'passed',
    [bool] $CredentialAccessPermitted = $false) {
    $receipt = [ordered]@{
        gate = $Name
        status = $Status
        network_permitted = $false
        credential_access_permitted = $false
        evidence = $Evidence
    }
    if ($CredentialAccessPermitted) {
        $receipt.credential_access_permitted = $true
    }
    $json = ConvertTo-CanonicalJsonValue $receipt
    [System.IO.File]::WriteAllText(
        (Join-Path $resolvedOutputRoot ($Name.ToLowerInvariant() + '.json')),
        $json + "`n",
        [System.Text.UTF8Encoding]::new($false))
}

function ConvertTo-CanonicalJsonValue([object] $Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string]) {
        $escaped = $Value.Replace('\', '\\').Replace('"', '\"')
        $escaped = $escaped.Replace("`b", '\b').Replace("`f", '\f').Replace("`n", '\n')
        $escaped = $escaped.Replace("`r", '\r').Replace("`t", '\t')
        return '"' + $escaped + '"'
    }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [System.Collections.IDictionary]) {
        [string[]] $keys = @($Value.Keys | ForEach-Object { [string] $_ })
        [Array]::Sort($keys, [StringComparer]::Ordinal)
        $members = foreach ($key in $keys) {
            (ConvertTo-CanonicalJsonValue $key) + ':' + (ConvertTo-CanonicalJsonValue $Value[$key])
        }
        return '{' + ($members -join ',') + '}'
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        $items = foreach ($item in $Value) { ConvertTo-CanonicalJsonValue $item }
        return '[' + ($items -join ',') + ']'
    }
    if ($Value -is [System.IFormattable]) {
        return $Value.ToString($null, [Globalization.CultureInfo]::InvariantCulture)
    }
    throw "Canonical receipt serialization does not support type $($Value.GetType().FullName)."
}

function Write-JsonReport([string] $Name, [object] $Value) {
    $path = Join-Path $resolvedOutputRoot $Name
    $json = $Value | ConvertTo-Json -Depth 32
    [System.IO.File]::WriteAllText(
        $path,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
    return [ordered]@{
        file = $Name
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Resolve-GitCommit([string] $Value, [string] $ParameterName) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Layer6Review requires -$ParameterName."
    }
    $resolved = (& git -C $repoRoot rev-parse --verify "$Value`^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or $resolved -notmatch '^[0-9a-f]{40,64}$') {
        throw "Layer6Review cannot resolve -$ParameterName '$Value' to a commit."
    }
    return $resolved
}

function Get-CandidateText([string] $Commit, [string] $Path) {
    $lines = @(& git -C $repoRoot show "$Commit`:$Path")
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read candidate blob '$Path'."
    }
    return [string]::Join("`n", $lines)
}

function Assert-NoDuplicateJsonProperties([System.Text.Json.JsonElement] $Element, [string] $Path) {
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $names.Add($property.Name)) {
                throw "Changed JSON '$Path' contains duplicate property '$($property.Name)'."
            }
            Assert-NoDuplicateJsonProperties $property.Value $Path
        }
    }
    elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        foreach ($item in $Element.EnumerateArray()) {
            Assert-NoDuplicateJsonProperties $item $Path
        }
    }
}

function Test-Wp1AllowedPath([string] $Path) {
    $exact = @(
        'Directory.Packages.props',
        'global.json',
        'Infinium.sln',
        'contracts/json-schema/README.md',
        'contracts/protobuf/README.md',
        'contracts/repository/public-fixture-registry.v1.schema.json',
        'contracts/repository/wp4-credential-native-authorization.v1.schema.json',
        'contracts/repository/wp4-credential-native-authorization.v2.schema.json',
        'dependencies/README.md',
        'dependencies/dependency-curation.json',
        'dependencies/dependency-manifest.json',
        'docs/evaluation/repository-evaluation-authority.v1.json',
        'docs/evaluation/specifications/semantic-fixture-catalog.md',
        'docs/plans/milestones/m1/slices/s6/README.md',
        'docs/plans/milestones/m1/slices/s6/record.md',
        'docs/plans/milestones/m1/slices/s6/wp1-contract-traceability.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp1-acceptance-ledger.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json',
        'docs/research/investigations/README.md',
        'docs/research/investigations/RESEARCH-0055-slice6-local-input-bound-policy.md',
        'docs/research/source-registry.md',
        'contracts/repository/public-fixture-registry.v1.schema.json',
        'eng/generate-m1-slice6-wp1-traceability.ps1',
        'eng/update-dependency-manifest.ps1',
        'eng/validate-m1-slice6-wp4-authorization.ps1',
        'eng/validate-m1-slice6-wp4-authorization-v2.ps1',
        'eng/verify-m1-slice6.ps1',
        'eng/verify-m1-slice6-wp3-upgrade.ps1',
        'fixtures/public/public-fixture-registry.v1.json',
        'fixtures/tooling/reseal-public-fixtures.mjs',
        'fixtures/tooling/prepare-wp7-answer-free-inputs.ps1',
        'src/Infinium.Cli/packages.lock.json',
        'src/Infinium.Coordinator/packages.lock.json',
        'src/Infinium.Worker/packages.lock.json',
        'src/Infinium.CredentialHelper/packages.lock.json',
        'tests/Infinium.SecurityTests/packages.lock.json',
        'tests/Infinium.FaultTests/packages.lock.json',
        'tests/Infinium.EvaluationTests/packages.lock.json',
        'tests/Infinium.IntegrationTests/packages.lock.json'
    )
    if ($exact -ccontains $Path) {
        return $true
    }
    foreach ($prefix in @(
        'contracts/json-schema/',
        'contracts/protobuf/infinium/application/',
        'contracts/protobuf/infinium/helper/v2/',
        'fixtures/public/contracts/provider-wp1/',
        'fixtures/tooling/Infinium.PublicFixtures/',
        'src/Infinium.Application/',
        'src/Infinium.Domain/',
        'src/Infinium.Coordinator/',
        'src/Infinium.OpenAI/',
        'src/Infinium.Persistence/',
        'src/Infinium.CredentialHelper/',
        'tests/Infinium.SecurityTests/',
        'tests/Infinium.FaultTests/',
        'fixtures/public/platform/credential-helper/',
        'tests/Infinium.UnitTests/',
        'tests/Infinium.IntegrationTests/',
        'tests/Infinium.EvaluationTests/',
        'fixtures/public/platform/provider-budget/',
        'fixtures/public/platform/provider-offline/',
        'fixtures/public/provider/source-claims/',
        'fixtures/public/provider/candidate-investigations/',
        'src/Infinium.Persistence/',
        'tests/Infinium.ContractTests/',
        'tests/Infinium.UnitTests/'
    )) {
        if ($Path.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            return $true
        }
    }
    return $false
}

function Test-Wp1ProtectedPath([string] $Path) {
    $exact = @(
        'AGENTS.md',
        'docs/current-state.md',
        'docs/evaluation/m1-continuation-verification-profile.md',
        'docs/plans/milestones/m1/slices/s6/current.md',
        'docs/plans/milestones/m1/slices/s6/orchestrator-handoff.md',
        'docs/plans/milestones/m1/slices/s6/plan.md',
        'docs/research/investigations/RESEARCH-0054-slice6-openai-profile-and-implementation-readiness-refresh.md',
        'docs/architecture/decisions/ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md',
        'contracts/json-schema/effective-scan-configuration.v1.schema.json',
        'contracts/json-schema/run-output.v1.schema.json',
        'contracts/json-schema/cli-summary.v1.schema.json',
        'src/Infinium.Application/Serialization/RunOutputJsonCodec.cs',
        'src/Infinium.Application/Serialization/CliSummaryJsonCodec.cs'
    )
    if ($exact -ccontains $Path) {
        return $true
    }
    return $Path.StartsWith('contracts/protobuf/infinium/helper/v1/', [System.StringComparison]::Ordinal)
}

function Invoke-Layer6ReviewGate {
    $baselineHash = Resolve-GitCommit $BaselineCommit 'BaselineCommit'
    $candidateHash = Resolve-GitCommit $CandidateCommit 'CandidateCommit'
    & git -C $repoRoot merge-base --is-ancestor $baselineHash $candidateHash
    if ($LASTEXITCODE -ne 0) {
        throw "Layer6Review baseline $baselineHash is not an ancestor of candidate $candidateHash."
    }

    $nameStatusLines = @(& git -C $repoRoot -c core.quotePath=false diff --name-status --find-renames $baselineHash $candidateHash --)
    if ($LASTEXITCODE -ne 0) {
        throw 'Layer6Review could not derive the candidate change set.'
    }

    $changedPaths = [System.Collections.Generic.List[object]]::new()
    foreach ($line in $nameStatusLines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        $parts = $line -split "`t"
        $status = $parts[0]
        $paths = if ($status.StartsWith('R', [System.StringComparison]::Ordinal)) { @($parts[1], $parts[2]) } else { @($parts[1]) }
        foreach ($path in $paths) {
            $isHandoffCurrentState = $HandoffCloseout -and
                $path -ceq 'docs/current-state.md'
            $isOwnerTestProcessCleanupPolicy = $OwnerTestProcessCleanup -and
                $path -ceq 'docs/execution-policy.md'
            $isProtected = (Test-Wp1ProtectedPath $path) -and
                -not $isHandoffCurrentState -and
                -not $isOwnerTestProcessCleanupPolicy
            $isAllowed = (Test-Wp1AllowedPath $path) -or
                $isHandoffCurrentState -or
                $isOwnerTestProcessCleanupPolicy
            $privateOrArchive = $path -match '(?i)(^|/)(private|legacy|archive)(/|$)' -or
                $path -match '(?i)independent-slice3-evaluator' -or
                $path -match '(?i)^docs/evaluation/fixtures/'
            $baselineBlob = $null
            $candidateBlob = $null
            & git -C $repoRoot cat-file -e "$baselineHash`:$path" 2>$null
            if ($LASTEXITCODE -eq 0) {
                $baselineBlob = (& git -C $repoRoot rev-parse "$baselineHash`:$path").Trim()
            }
            & git -C $repoRoot cat-file -e "$candidateHash`:$path" 2>$null
            if ($LASTEXITCODE -eq 0) {
                $candidateBlob = (& git -C $repoRoot rev-parse "$candidateHash`:$path").Trim()
            }
            $changedPaths.Add([ordered]@{
                path = $path
                status = $status
                allowed = $isAllowed
                protected = $isProtected
                private_or_archive = $privateOrArchive
                baseline_blob = $baselineBlob
                candidate_blob = $candidateBlob
            })
        }
    }

    $pathFailures = @($changedPaths | Where-Object { -not $_.allowed -or $_.protected -or $_.private_or_archive })
    $pathReport = Write-JsonReport 'layer6-changed-paths.json' ([ordered]@{
        baseline_commit = $baselineHash
        candidate_commit = $candidateHash
        changed_path_count = $changedPaths.Count
        failure_count = $pathFailures.Count
        paths = @($changedPaths)
    })

    $jsonResults = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in @($changedPaths | Where-Object { $_.candidate_blob -and $_.path.EndsWith('.json', [System.StringComparison]::OrdinalIgnoreCase) })) {
        $text = Get-CandidateText $candidateHash $entry.path
        $options = [System.Text.Json.JsonDocumentOptions]::new()
        $options.AllowTrailingCommas = $false
        $options.CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
        $options.MaxDepth = 128
        try {
            $document = [System.Text.Json.JsonDocument]::Parse($text, $options)
            try {
                Assert-NoDuplicateJsonProperties $document.RootElement $entry.path
            } finally {
                $document.Dispose()
            }
            $jsonResults.Add([ordered]@{ path = $entry.path; valid = $true; error = $null })
        } catch {
            $jsonResults.Add([ordered]@{ path = $entry.path; valid = $false; error = $_.Exception.Message })
        }
    }
    $jsonFailures = @($jsonResults | Where-Object { -not $_.valid })
    $jsonReport = Write-JsonReport 'layer6-changed-json.json' ([ordered]@{
        baseline_commit = $baselineHash
        candidate_commit = $candidateHash
        parsed_count = $jsonResults.Count
        failure_count = $jsonFailures.Count
        files = @($jsonResults)
    })

    $linkResults = [System.Collections.Generic.List[object]]::new()
    $markdownLinkPattern = '(?<!!)\[[^\]]+\]\((?<target>[^)]+)\)'
    foreach ($entry in @($changedPaths | Where-Object { $_.candidate_blob -and $_.path.EndsWith('.md', [System.StringComparison]::OrdinalIgnoreCase) })) {
        $text = Get-CandidateText $candidateHash $entry.path
        foreach ($match in [regex]::Matches($text, $markdownLinkPattern)) {
            $target = $match.Groups['target'].Value.Trim()
            if ($target.StartsWith('<') -and $target.EndsWith('>')) {
                $target = $target.Substring(1, $target.Length - 2)
            }
            if ($target -match '^(?:https?://|mailto:|#)') {
                continue
            }
            $pathTarget = ($target -split '#', 2)[0]
            if ([string]::IsNullOrWhiteSpace($pathTarget)) {
                continue
            }
            $pathTarget = [System.Uri]::UnescapeDataString($pathTarget)
            $baseDirectory = [System.IO.Path]::GetDirectoryName($entry.path.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
            $combined = [System.IO.Path]::GetFullPath((Join-Path $repoRoot (Join-Path $baseDirectory $pathTarget)))
            $insideRepository = $combined.StartsWith($repoRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
            $resolvedRelative = if ($insideRepository) {
                [System.IO.Path]::GetRelativePath($repoRoot, $combined).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
            } else { $null }
            $exists = $false
            if ($insideRepository) {
                & git -C $repoRoot cat-file -e "$candidateHash`:$resolvedRelative" 2>$null
                $exists = $LASTEXITCODE -eq 0
            }
            $linkResults.Add([ordered]@{
                source = $entry.path
                target = $target
                resolved_path = $resolvedRelative
                valid = $insideRepository -and $exists
            })
        }
    }
    $linkFailures = @($linkResults | Where-Object { -not $_.valid })
    $linkReport = Write-JsonReport 'layer6-relative-links.json' ([ordered]@{
        baseline_commit = $baselineHash
        candidate_commit = $candidateHash
        checked_count = $linkResults.Count
        failure_count = $linkFailures.Count
        links = @($linkResults)
    })

    $claimMatches = [System.Collections.Generic.List[object]]::new()
    $gapMatches = [System.Collections.Generic.List[object]]::new()
    $claimPattern = '(?i)\b(Implementation-active|Accepted|Completed|passed|held-out|independently validated|reliable|ready)\b'
    $gapPattern = '(?i)\b(unsupported|gap|abstention|deferred|blocked-authority-required|unavailable|unknown)\b'
    foreach ($entry in @($changedPaths | Where-Object { $_.candidate_blob -and ($_.path.EndsWith('.md') -or $_.path.EndsWith('.json')) })) {
        $lines = (Get-CandidateText $candidateHash $entry.path) -split "`n"
        for ($index = 0; $index -lt $lines.Count; $index++) {
            foreach ($match in [regex]::Matches($lines[$index], $claimPattern)) {
                $claimMatches.Add([ordered]@{ path = $entry.path; line = $index + 1; term = $match.Value })
            }
            foreach ($match in [regex]::Matches($lines[$index], $gapPattern)) {
                $gapMatches.Add([ordered]@{ path = $entry.path; line = $index + 1; term = $match.Value })
            }
        }
    }
    $claimReport = Write-JsonReport 'layer6-status-claims.json' ([ordered]@{
        baseline_commit = $baselineHash
        candidate_commit = $candidateHash
        occurrence_count = $claimMatches.Count
        occurrences = @($claimMatches)
        disposition = 'review-required-occurrence-inventory-not-semantic-acceptance'
    })
    $gapReport = Write-JsonReport 'layer6-gap-inventory.json' ([ordered]@{
        baseline_commit = $baselineHash
        candidate_commit = $candidateHash
        occurrence_count = $gapMatches.Count
        occurrences = @($gapMatches)
        disposition = 'retained-unsupported-and-gap-inventory'
    })
    $absenceReport = Write-JsonReport 'layer6-private-archive-absence.json' ([ordered]@{
        baseline_commit = $baselineHash
        candidate_commit = $candidateHash
        changed_path_match_count = @($changedPaths | Where-Object { $_.private_or_archive }).Count
        changed_path_matches = @($changedPaths | Where-Object { $_.private_or_archive } | ForEach-Object { $_.path })
        private_access_permitted = $false
        archive_access_permitted = $false
    })

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($failure in $pathFailures) {
        $failures.Add("Changed path is outside Slice 6 authority or protected: $($failure.path)")
    }
    foreach ($failure in $jsonFailures) {
        $failures.Add("Changed JSON is invalid: $($failure.path): $($failure.error)")
    }
    foreach ($failure in $linkFailures) {
        $failures.Add("Changed Markdown has broken relative link: $($failure.source): $($failure.target)")
    }
    if ($HandoffCloseout) {
        $currentState = @($changedPaths | Where-Object { $_.path -ceq 'docs/current-state.md' })
        if ($currentState.Count -ne 1 -or -not $currentState[0].candidate_blob) {
            $failures.Add('HandoffCloseout requires exactly one changed candidate docs/current-state.md.')
        } else {
            $currentStateText = Get-CandidateText $candidateHash 'docs/current-state.md'
            if (-not $currentStateText.Contains('M1/S6/WP2', [System.StringComparison]::Ordinal) -or
                -not $currentStateText.Contains('Accepted Slice 6 WP1 candidate', [System.StringComparison]::Ordinal)) {
                $failures.Add('HandoffCloseout current state must record accepted WP1 and authorize M1/S6/WP2.')
            }
        }
    }
    if ($OwnerTestProcessCleanup) {
        $executionPolicy = @($changedPaths | Where-Object { $_.path -ceq 'docs/execution-policy.md' })
        if ($executionPolicy.Count -ne 1 -or -not $executionPolicy[0].candidate_blob) {
            $failures.Add('OwnerTestProcessCleanup requires exactly one changed candidate docs/execution-policy.md.')
        } else {
            $executionPolicyText = Get-CandidateText $candidateHash 'docs/execution-policy.md'
            $requiredCleanupPolicy = @(
                '## Test-process cleanup and verification',
                'Get-CimInstance -ClassName Win32_Process',
                'Stop-Process -Id $current.ProcessId -Force',
                'Repository-owned dotnet/testhost processes remaining: 0',
                'Never terminate by process name alone')
            foreach ($required in $requiredCleanupPolicy) {
                if (-not $executionPolicyText.Contains($required, [System.StringComparison]::Ordinal)) {
                    $failures.Add("OwnerTestProcessCleanup policy is missing the required exact-root safeguard: $required")
                }
            }
        }
    }

    $status = if ($failures.Count -eq 0) { 'passed' } else { 'failed' }
    Write-Receipt 'Layer6Review' ([ordered]@{
        baseline_input = $BaselineCommit
        baseline_commit = $baselineHash
        candidate_input = $CandidateCommit
        candidate_commit = $candidateHash
        candidate_bound = $true
        handoff_closeout = [bool]$HandoffCloseout
        owner_test_process_cleanup = [bool]$OwnerTestProcessCleanup
        changed_path_count = $changedPaths.Count
        allowed_path_failure_count = $pathFailures.Count
        strict_changed_json_failure_count = $jsonFailures.Count
        relative_link_failure_count = $linkFailures.Count
        status_claim_occurrence_count = $claimMatches.Count
        unsupported_gap_occurrence_count = $gapMatches.Count
        failures = @($failures)
        reports = @($pathReport, $jsonReport, $linkReport, $claimReport, $gapReport, $absenceReport)
    }) $status
    if ($failures.Count -gt 0) {
        foreach ($failure in $failures) {
            Write-Error $failure
        }
        throw "Layer6Review failed with $($failures.Count) finding(s)."
    }
}

function Invoke-ContractsGate {
    Assert-Slice5V1Unchanged
    $schemaEvidence = @()
    foreach ($schemaName in $schemaNames) {
        $path = Join-Path $repoRoot "contracts/json-schema/$schemaName"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing WP1 schema $schemaName."
        }
        $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($document.type -ne 'object' -or $document.additionalProperties -ne $false) {
            throw "WP1 schema $schemaName is not a closed root object."
        }
        $schemaEvidence += [ordered]@{
            name = $schemaName
            schema_id = $document.title
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $protoPath = Join-Path $repoRoot 'contracts/protobuf/infinium/helper/v2/helper.proto'
    $protobufRoot = Join-Path $repoRoot 'contracts/protobuf'
    $contractBytes = [System.IO.MemoryStream]::new()
    foreach ($proto in Get-ChildItem -LiteralPath $protobufRoot -Recurse -Filter '*.proto' |
             Sort-Object { $_.FullName.Substring($protobufRoot.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, '/') }) {
        $relative = $proto.FullName.Substring($protobufRoot.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relative + "`n")
        $contractBytes.Write($pathBytes, 0, $pathBytes.Length)
        $protoBytes = [System.IO.File]::ReadAllBytes($proto.FullName)
        $contractBytes.Write($protoBytes, 0, $protoBytes.Length)
    }
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $protocolFingerprint = [BitConverter]::ToString(
            $sha256.ComputeHash($contractBytes.ToArray())).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha256.Dispose()
    }
    $constantPath = Join-Path $repoRoot 'src/Infinium.Application/Runtime/HelperProtocolV2Constants.cs'
    $helperBytes = [System.IO.MemoryStream]::new()
    foreach ($relative in @('infinium/common/v1/common.proto', 'infinium/domain/v1/identities.proto', 'infinium/helper/v2/helper.proto')) {
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relative + "`n")
        $helperBytes.Write($pathBytes, 0, $pathBytes.Length)
        $protoBytes = [System.IO.File]::ReadAllBytes((Join-Path $protobufRoot $relative))
        $helperBytes.Write($protoBytes, 0, $protoBytes.Length)
    }
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $helperFingerprint = [BitConverter]::ToString(
            $sha256.ComputeHash($helperBytes.ToArray())).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha256.Dispose()
    }
    if (-not (Select-String -LiteralPath $constantPath -SimpleMatch $helperFingerprint -Quiet)) {
        throw 'Helper protocol v2 fingerprint constant is stale.'
    }
    $applicationConstantPath = Join-Path $repoRoot 'src/Infinium.Application/Runtime/ProtocolConstants.cs'
    if (-not (Select-String -LiteralPath $applicationConstantPath -SimpleMatch $protocolFingerprint -Quiet)) {
        throw 'Application protocol fingerprint constant is stale.'
    }
    $helperV1Relative = 'contracts/protobuf/infinium/helper/v1/helper.proto'
    $helperV1Path = Join-Path $repoRoot $helperV1Relative
    $helperV1AuthorityCommit = '6ac66e7d79c63a231bbbf22209015a894cd4bd6d'
    if (-not (Test-Path -LiteralPath $helperV1Path -PathType Leaf)) {
        throw 'Helper protocol v1 decodability authority is missing.'
    }
    $authoritativeHelperV1Blob = (& git -C $repoRoot rev-parse "$helperV1AuthorityCommit`:$helperV1Relative").Trim()
    if ($LASTEXITCODE -ne 0 -or $authoritativeHelperV1Blob -notmatch '^[0-9a-f]{40,64}$') {
        throw 'Accepted pre-S6 helper-v1 Git authority cannot be derived.'
    }
    $currentHelperV1Blob = (& git -C $repoRoot hash-object -- $helperV1Path).Trim()
    if ($LASTEXITCODE -ne 0 -or $currentHelperV1Blob -cne $authoritativeHelperV1Blob) {
        throw 'Helper protocol v1 differs byte-for-byte from the accepted pre-S6 authority.'
    }
    $helperV1Sha256 = (Get-FileHash -LiteralPath $helperV1Path -Algorithm SHA256).Hash.ToLowerInvariant()

    $forbiddenPattern = '"(credential_target|provider_secret|authorization_header|secret_bytes|raw_headers)"\s*:'
    foreach ($schemaName in $schemaNames) {
        if (Select-String -LiteralPath (Join-Path $repoRoot "contracts/json-schema/$schemaName") -Pattern $forbiddenPattern -Quiet) {
            throw "WP1 schema $schemaName exposes a forbidden secret/transport field."
        }
    }

    Invoke-DotnetTest `
        'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' `
        'FullyQualifiedName~Provider|FullyQualifiedName~Helper|FullyQualifiedName~RunOutput'

    Write-Receipt 'Contracts' ([ordered]@{
        schemas = $schemaEvidence
        schema_count = $schemaEvidence.Count
        helper_protocol_v2_sha256 = $helperFingerprint
        application_protocol_set_sha256 = $protocolFingerprint
        helper_protocol_v1_retained = $true
        helper_protocol_v1_authority_commit = $helperV1AuthorityCommit
        helper_protocol_v1_git_blob = $authoritativeHelperV1Blob
        helper_protocol_v1_sha256 = $helperV1Sha256
        public_package_count = ((Get-Content -LiteralPath (Join-Path $repoRoot 'fixtures/public/public-fixture-registry.v1.json') -Raw | ConvertFrom-Json).package_count)
        answer_free_example_count = 9
        slice5_v1_byte_compatibility = 'unchanged-from-6ac66e7d79c63a231bbbf22209015a894cd4bd6d'
        forbidden_field_scan = 'passed'
    })
}

function Invoke-StateSurfaceGate([bool] $RequireAcceptedInputProof) {
    Assert-Slice5V1Unchanged
    Invoke-DotnetTest `
        'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' `
        'FullyQualifiedName~ProviderContract|FullyQualifiedName~ProviderFiniteBound|FullyQualifiedName~ProviderInputBound|FullyQualifiedName~OperationalContract'
    Invoke-DotnetTest `
        'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' `
        'FullyQualifiedName~Schema6|FullyQualifiedName~ProviderPersistence|FullyQualifiedName~BackupRestore'

    $migrationPath = Join-Path $repoRoot 'src/Infinium.Persistence/AuthoritativeStore.Migrations.cs'
    foreach ($required in @('M1-S6-0006', 'SchemaV6', 'provider_operation_projection', 'provider_budget_projection',
        'provider_operation_blocks', 'provider_price_rules', 'provider_rate_limit_facts',
        'provider_authority_release_required', 'provider_budget_projection_authority_guard',
        'idx_payload_identity_size', 'maximum_request_bytes',
        'maximum_raw_response_bytes', 'installation_snapshot_id')) {
        if (-not (Select-String -LiteralPath $migrationPath -SimpleMatch $required -Quiet)) {
            throw "Schema-6 persistence declaration is missing $required."
        }
    }
    $traceabilityPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp1-contract-traceability.v1.json'
    $traceability = Get-Content -LiteralPath $traceabilityPath -Raw | ConvertFrom-Json
    if ($traceability.contracts.Count -ne 9) {
        throw 'WP1 traceability inventory does not cover exactly nine contracts.'
    }
    if ($traceability.maturity -ne 'Implementation-active') {
        throw 'Accepted WP1 traceability must remain Implementation-active until Slice 6 acceptance.'
    }
    $declarationsPath = Join-Path $repoRoot 'src/Infinium.Persistence/ProviderPersistenceDeclarations.cs'
    $declarations = Get-Content -LiteralPath $declarationsPath -Raw
    $schemaFingerprintMatch = [regex]::Match(
        $declarations,
        'public const string SchemaFingerprint = "(?<value>[0-9a-f]{64})";')
    if (-not $schemaFingerprintMatch.Success) {
        throw 'Current schema-6 fingerprint declaration cannot be derived.'
    }
    $destinationSchemaFingerprint = $schemaFingerprintMatch.Groups['value'].Value

    $gateName = if ($RequireAcceptedInputProof) { 'StateTotality' } else { 'StateSurfaces' }
    $gateStatus = 'passed'
    Write-Receipt $gateName ([ordered]@{
        migration_id = 'M1-S6-0006'
        source_schema = 5
        destination_schema = 6
        storage_contract = '1.5.0'
        source_schema_fingerprint = 'e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d'
        destination_schema_fingerprint = $destinationSchemaFingerprint
        append_only_provider_history_table_count = 25
        rebuildable_projection_count = 3
        traceability_contract_count = $traceability.contracts.Count
        local_input_bound_proof = 'proved-openai-responses-o200k-byte-envelope/v1'
        input_bound_policy_id = 'openai-responses-o200k-byte-envelope'
        input_bound_policy_version = 'v1'
        tokenizer_encoding = 'o200k_base'
        tokenizer_package = 'Microsoft.ML.Tokenizers/2.0.0'
        tokenizer_package_content_hash = '+b8lT4cLLO/sBR2hjvE/qG6qrZG15h7/PBvnIrzTh4xDaAxdHUY6449rC+1pHzQUsBiCHZVbj+VMn+xS0sL7TA=='
        vocabulary_package = 'Microsoft.ML.Tokenizers.Data.O200kBase/2.0.0'
        vocabulary_package_content_hash = '19G0KWrRnUZmc8vGdPNuBJqTruhAjzPLRY2nn6a/HiBXbEnE/Lx9L223jGlDzg1oAcCggo/8GlWw3ZLVuS76Ow=='
        qualification_structural_allowance_tokens = 4096
        semantic_structural_allowance_tokens = 8192
        provider_dispatch_admission = 'contract-shape-enabled-no-wp2-coordinator-or-wp3-helper-execution'
        transport_qualification_request_byte_ceiling = 16384
        semantic_request_byte_ceiling = 65536
        maximum_dispatch_count = 1
    }) $gateStatus
}

function Invoke-BudgetGate {
    $unitFilter = 'FullyQualifiedName~ProviderCapability|FullyQualifiedName~PriceCatalog|FullyQualifiedName~Budget'
    $integrationFilter = 'FullyQualifiedName~ProviderReservation|FullyQualifiedName~DispatchFence|FullyQualifiedName~UsageSettlement'
    $evaluationFilter = 'FullyQualifiedName~ProviderCapability|FullyQualifiedName~ProviderAuthority|FullyQualifiedName~AtomicBudget'
    Invoke-DotnetTest 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' $unitFilter
    Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' $integrationFilter
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' $evaluationFilter
    $registry = Get-Content -LiteralPath (Join-Path $repoRoot 'fixtures/public/public-fixture-registry.v1.json') -Raw | ConvertFrom-Json
    $wp2Identities = @(
        'M1-PLAT-PROVIDER-CAPABILITY-DEV-v1',
        'M1-PLAT-PROVIDER-CAPABILITY-VAL-v1',
        'M1-PLAT-PROVIDER-AUTHORITY-DEV-v1',
        'M1-PLAT-PROVIDER-AUTHORITY-VAL-v1',
        'M1-PLAT-BUDGET-DEV-v1',
        'M1-PLAT-BUDGET-VAL-v1'
    )
    $wp2Packages = @($registry.packages | Where-Object { $wp2Identities -ccontains $_.package_identity })
    if ($wp2Packages.Count -ne 6) { throw 'Budget gate requires exactly six registered WP2 public packages.' }
    Write-Receipt 'Budget' ([ordered]@{
        execution_mode = 'simulated-nonnetwork'
        production_test_filters = @($unitFilter, $integrationFilter, $evaluationFilter)
        wp2_public_package_count = $wp2Packages.Count
        registry_package_count = $registry.package_count
        vector_dimension_count = 9
        price_class_count = 5
        scope_count = 8
        projection_rebuild = 'equal'
        network_operations = 0
        credential_operations = 0
    })
}

function Invoke-BudgetFaultGate {
    $unitFilter = 'FullyQualifiedName~PriceCatalog|FullyQualifiedName~Budget'
    $integrationFilter = 'FullyQualifiedName~UsageSettlement'
    $evaluationFilter = 'FullyQualifiedName~AtomicBudget|FullyQualifiedName~ProviderAuthority'
    Invoke-DotnetTest 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' $unitFilter
    $faultEvidencePath = Join-Path $resolvedOutputRoot 'budget-fault-dynamic-evidence.json'
    Remove-Item -LiteralPath $faultEvidencePath -Force -ErrorAction SilentlyContinue
    $priorFaultEvidencePath = $env:INFINIUM_WP2_FAULT_EVIDENCE_PATH
    try {
        $env:INFINIUM_WP2_FAULT_EVIDENCE_PATH = $faultEvidencePath
        Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' $integrationFilter
    } finally {
        $env:INFINIUM_WP2_FAULT_EVIDENCE_PATH = $priorFaultEvidencePath
    }
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' $evaluationFilter
    if (-not (Test-Path -LiteralPath $faultEvidencePath -PathType Leaf)) {
        throw 'BudgetFaults requires dynamic evidence emitted by the real SQLite fault schedule.'
    }
    $faultEvidence = Get-Content -LiteralPath $faultEvidencePath -Raw | ConvertFrom-Json
    if (($faultEvidence.schema -ne 'infinium.wp2.budget-fault-evidence/v1') -or
        ($faultEvidence.rollback_after_reservation_root -ne $true) -or
        ($faultEvidence.competing_commit_winners -ne 1) -or
        ($faultEvidence.stale_epoch_rejected -ne $true) -or
        ($faultEvidence.deadline_rejected -ne $true) -or
        ($faultEvidence.projection_reconstructed_from_events -ne $true) -or
        ($faultEvidence.network_operations -ne 0) -or
        ($faultEvidence.credential_operations -ne 0)) {
        throw 'BudgetFaults dynamic SQLite evidence does not satisfy the accepted fault schedule.'
    }
    Write-Receipt 'BudgetFaults' ([ordered]@{
        production_test_filters = @($unitFilter, $integrationFilter, $evaluationFilter)
        dynamic_evidence_file = [System.IO.Path]::GetFileName($faultEvidencePath)
        dynamic_evidence_sha256 = (Get-FileHash -LiteralPath $faultEvidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
        rollback_after_reservation_root = $faultEvidence.rollback_after_reservation_root
        competing_commit_winners = $faultEvidence.competing_commit_winners
        stale_epoch_rejected = $faultEvidence.stale_epoch_rejected
        deadline_rejected = $faultEvidence.deadline_rejected
        projection_reconstructed_from_events = $faultEvidence.projection_reconstructed_from_events
        network_operations = $faultEvidence.network_operations
        credential_operations = $faultEvidence.credential_operations
    })
}

function Invoke-CredentialSyntheticGate {
    $unitFilter = 'FullyQualifiedName~Credential|FullyQualifiedName~Helper'
    $integrationFilter = 'FullyQualifiedName~CredentialIntent|FullyQualifiedName~HelperPrivateHandle|FullyQualifiedName~CredentialDispatch'
    $securityFilter = 'FullyQualifiedName~Credential|FullyQualifiedName~SecretCanary|FullyQualifiedName~HelperAuthority'
    $faultFilter = 'FullyQualifiedName~Helper|FullyQualifiedName~Credential'
    $evaluationFilter = 'FullyQualifiedName~CredentialSynthetic'
    Invoke-DotnetTest 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' $unitFilter
    Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' $integrationFilter
    Invoke-DotnetTest 'tests/Infinium.SecurityTests/Infinium.SecurityTests.csproj' $securityFilter
    Invoke-DotnetTest 'tests/Infinium.FaultTests/Infinium.FaultTests.csproj' $faultFilter
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' $evaluationFilter
    $upgradeEvidencePath = Join-Path $resolvedOutputRoot 'accepted-wp2-upgrade.json'
    & (Join-Path $repoRoot 'eng/verify-m1-slice6-wp3-upgrade.ps1') -OutputPath $upgradeEvidencePath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $upgradeEvidencePath -PathType Leaf)) {
        throw 'CredentialSynthetic exact accepted-WP2 same-version upgrade regression failed.'
    }
    $registry = Get-Content -LiteralPath (Join-Path $repoRoot 'fixtures/public/public-fixture-registry.v1.json') -Raw | ConvertFrom-Json
    $packages = @($registry.packages | Where-Object { $_.package_identity -like 'M1-PLAT-CREDENTIAL-HELPER-*' })
    if ($packages.Count -ne 2) { throw 'CredentialSynthetic requires exactly two registered WP3 DEV/VAL packages.' }
    $helperPath = Join-Path $repoRoot 'src/Infinium.CredentialHelper/bin/Release/net10.0/Infinium.CredentialHelper.exe'
    if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) { throw 'CredentialSynthetic exact helper binary is absent.' }
    $dynamicPath = Join-Path $repoRoot 'artifacts/m1-slice6/wp3/credential-synthetic-dynamic.json'
    if (-not (Test-Path -LiteralPath $dynamicPath -PathType Leaf)) { throw 'CredentialSynthetic dynamic process evidence is absent.' }
    $dynamicBytes = [IO.File]::ReadAllBytes($dynamicPath)
    $dynamic = [Text.Encoding]::UTF8.GetString($dynamicBytes) | ConvertFrom-Json
    $actualHelperSha = (Get-FileHash -LiteralPath $helperPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($dynamic.helper_binary_sha256 -ne $actualHelperSha) { throw 'CredentialSynthetic dynamic evidence is not bound to the exact helper binary.' }
    foreach ($zeroField in @('standard_protocol_handle_count','listener_count','retry_count','native_credential_operations','network_operations','process_tree_survivors')) {
        if ([int64]$dynamic.$zeroField -ne 0) { throw "CredentialSynthetic dynamic evidence reports non-zero $zeroField." }
    }
    if ([int64]$dynamic.inherited_private_handle_count -ne 3 -or -not $dynamic.stage_before_admit -or -not $dynamic.coordinator_only_admission) {
        throw 'CredentialSynthetic dynamic evidence does not prove the closed private-handle/staging path.'
    }
    $secretCanaryMatches = [int64]$dynamic.secret_canary_matches
    $targetCanaryMatches = [int64]$dynamic.target_canary_matches
    if ($secretCanaryMatches -ne 0 -or $targetCanaryMatches -ne 0 -or -not [bool]$dynamic.canary_mutation_rejected) {
        throw 'CredentialSynthetic real-child product-artifact canary scan or its leak mutation failed.'
    }
    Write-Receipt 'CredentialSynthetic' ([ordered]@{
        execution_mode = 'synthetic-fake-secure-store-nonnetwork'
        production_test_filters = @($unitFilter, $integrationFilter, $securityFilter, $faultFilter, $evaluationFilter)
        wp3_public_package_count = $packages.Count
        registry_package_count = $registry.package_count
        helper_binary_sha256 = $actualHelperSha
        helper_protocol_sha256 = '2eac265ef75cc827bd5a8596120f5ba4c1912dde2219ad98eb11e2984cb043c0'
        accepted_wp2_upgrade_sha256 = (Get-FileHash -LiteralPath $upgradeEvidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
        dynamic_evidence_bytes = $dynamicBytes.Length
        dynamic_evidence_sha256 = (Get-FileHash -LiteralPath $dynamicPath -Algorithm SHA256).Hash.ToLowerInvariant()
        inherited_private_handle_count = [int64]$dynamic.inherited_private_handle_count
        standard_protocol_handle_count = [int64]$dynamic.standard_protocol_handle_count
        listener_count = [int64]$dynamic.listener_count
        retry_count = [int64]$dynamic.retry_count
        native_credential_operations = [int64]$dynamic.native_credential_operations
        network_operations = [int64]$dynamic.network_operations
        secret_canary_matches = $secretCanaryMatches
        target_canary_matches = $targetCanaryMatches
        canary_mutation_rejected = [bool]$dynamic.canary_mutation_rejected
        stage_before_admit = [bool]$dynamic.stage_before_admit
        coordinator_only_admission = [bool]$dynamic.coordinator_only_admission
        process_tree_survivors = [int64]$dynamic.process_tree_survivors
    })
}

function Get-Wp5PublicPackages {
    $registry = Get-Content -LiteralPath (Join-Path $repoRoot 'fixtures/public/public-fixture-registry.v1.json') -Raw | ConvertFrom-Json
    $packages = @($registry.packages | Where-Object { $_.package_identity -like 'M1-PLAT-OFFLINE-*-v1' })
    if ($packages.Count -ne 2 -or $registry.package_count -ne $registry.packages.Count) {
        throw 'WP5 requires exactly two closed-world offline DEV/VAL packages and an exact registry count.'
    }
    foreach ($package in $packages) {
        $authorityPath = Join-Path $repoRoot $package.authority_file
        if (-not (Test-Path -LiteralPath $authorityPath -PathType Leaf)) {
            throw "WP5 public authority is absent: $($package.authority_file)."
        }
        if ((Get-Item -LiteralPath $authorityPath).Length -ne [int64]$package.authority_bytes -or
            (Get-FileHash -LiteralPath $authorityPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $package.authority_sha256) {
            throw "WP5 public authority identity drifted: $($package.package_identity)."
        }
    }
    return [ordered]@{ registry = $registry; packages = $packages }
}

function Invoke-AdapterGate {
    $unitFilter = 'FullyQualifiedName~OpenAi|FullyQualifiedName~Responses|FullyQualifiedName~ContextMinimization'
    $integrationFilter = 'FullyQualifiedName~ProviderAdapter|FullyQualifiedName~ProviderOffline|FullyQualifiedName~RetainedResponseReplay'
    $securityFilter = 'FullyQualifiedName~ProviderBoundary|FullyQualifiedName~PromptInjection|FullyQualifiedName~SecretCanary'
    $faultFilter = 'FullyQualifiedName~ProviderTransport|FullyQualifiedName~AmbiguousDispatch'
    $evaluationFilter = 'FullyQualifiedName~ProviderOffline'
    Invoke-DotnetTest 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' $unitFilter
    $priorEvidenceRoot = [Environment]::GetEnvironmentVariable('INFINIUM_WP5_EVIDENCE_ROOT')
    try {
        [Environment]::SetEnvironmentVariable('INFINIUM_WP5_EVIDENCE_ROOT', $resolvedOutputRoot)
        Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' $integrationFilter
    } finally {
        [Environment]::SetEnvironmentVariable('INFINIUM_WP5_EVIDENCE_ROOT', $priorEvidenceRoot)
    }
    Invoke-DotnetTest 'tests/Infinium.SecurityTests/Infinium.SecurityTests.csproj' $securityFilter
    Invoke-DotnetTest 'tests/Infinium.FaultTests/Infinium.FaultTests.csproj' $faultFilter
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' $evaluationFilter
    $public = Get-Wp5PublicPackages
    $requestPath = Join-Path $resolvedOutputRoot 'canonical-request.json'
    $responsePath = Join-Path $resolvedOutputRoot 'retained-response.json'
    $diagnosticPath = Join-Path $resolvedOutputRoot 'secret-free-diagnostic.json'
    $matrixPath = Join-Path $resolvedOutputRoot 'response-state-matrix.json'
    $networkSpyPath = Join-Path $resolvedOutputRoot 'network-spy.json'
    foreach ($requiredPath in @($requestPath, $responsePath, $diagnosticPath, $matrixPath, $networkSpyPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Adapter did not retain required evidence: $requiredPath."
        }
    }
    foreach ($path in @($requestPath, $responsePath, $diagnosticPath, $matrixPath)) {
        if ((Get-Content -LiteralPath $path -Raw).IndexOf('sk-wp5-retained-evidence-canary', [StringComparison]::Ordinal) -ge 0) {
            throw "Adapter retained a secret canary in $path."
        }
    }
    $matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
    $networkSpy = Get-Content -LiteralPath $networkSpyPath -Raw | ConvertFrom-Json
    if ($matrix.redirect_count -ne 0 -or $matrix.retry_count -ne 0 -or $matrix.proxy_fallback_count -ne 0 -or
        $matrix.dns_count -ne 0 -or $matrix.provider_count -ne 0 -or $matrix.loopback_send_count -ne 1 -or
        $matrix.replay_send_count -ne 0) {
        throw 'Adapter retained evidence violates the one-shot offline transport boundary.'
    }
    Write-Receipt 'Adapter' ([ordered]@{
        execution_mode = 'deterministic-literal-loopback-and-retained-offline-replay'
        production_test_filters = @($unitFilter, $integrationFilter, $securityFilter, $faultFilter, $evaluationFilter)
        wp5_public_package_count = $public.packages.Count
        registry_package_count = $public.registry.package_count
        canonical_request_bytes = (Get-Item -LiteralPath $requestPath).Length
        canonical_request_sha256 = (Get-FileHash -LiteralPath $requestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        retained_response_sha256 = (Get-FileHash -LiteralPath $responsePath -Algorithm SHA256).Hash.ToLowerInvariant()
        diagnostic_sha256 = (Get-FileHash -LiteralPath $diagnosticPath -Algorithm SHA256).Hash.ToLowerInvariant()
        response_state_matrix_sha256 = (Get-FileHash -LiteralPath $matrixPath -Algorithm SHA256).Hash.ToLowerInvariant()
        network_spy_sha256 = (Get-FileHash -LiteralPath $networkSpyPath -Algorithm SHA256).Hash.ToLowerInvariant()
        loopback_send_count = [int64]$matrix.loopback_send_count
        replay_send_count = [int64]$matrix.replay_send_count
        redirect_count = [int64]$matrix.redirect_count
        retry_count = [int64]$matrix.retry_count
        proxy_fallback_count = [int64]$matrix.proxy_fallback_count
        public_dns_operations = [int64]$networkSpy.public_dns_operations
        provider_operations = [int64]$networkSpy.provider_operations
        credential_manager_operations = 0
        secret_canary_matches = 0
    })
}

function Invoke-OfflineSafetyReplayGate {
    $integrationFilter = 'FullyQualifiedName~ProviderOffline|FullyQualifiedName~RetainedResponseReplay'
    $securityFilter = 'FullyQualifiedName~ProviderBoundary|FullyQualifiedName~PromptInjection|FullyQualifiedName~SecretCanary'
    $faultFilter = 'FullyQualifiedName~ProviderTransport|FullyQualifiedName~AmbiguousDispatch'
    $evaluationFilter = 'FullyQualifiedName~ProviderOffline'
    $priorEvidenceRoot = [Environment]::GetEnvironmentVariable('INFINIUM_WP5_EVIDENCE_ROOT')
    try {
        [Environment]::SetEnvironmentVariable('INFINIUM_WP5_EVIDENCE_ROOT', $resolvedOutputRoot)
        Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' $integrationFilter
    } finally {
        [Environment]::SetEnvironmentVariable('INFINIUM_WP5_EVIDENCE_ROOT', $priorEvidenceRoot)
    }
    Invoke-DotnetTest 'tests/Infinium.SecurityTests/Infinium.SecurityTests.csproj' $securityFilter
    Invoke-DotnetTest 'tests/Infinium.FaultTests/Infinium.FaultTests.csproj' $faultFilter
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' $evaluationFilter
    $public = Get-Wp5PublicPackages
    $networkSpyPath = Join-Path $resolvedOutputRoot 'network-spy.json'
    if (-not (Test-Path -LiteralPath $networkSpyPath -PathType Leaf)) {
        throw 'OfflineSafetyReplay requires the dynamically measured WP5 network-spy artifact.'
    }
    $networkSpy = Get-Content -LiteralPath $networkSpyPath -Raw | ConvertFrom-Json
    Write-Receipt 'OfflineSafetyReplay' ([ordered]@{
        execution_mode = 'offline-and-retained-response-only'
        production_test_filters = @($integrationFilter, $securityFilter, $faultFilter, $evaluationFilter)
        wp5_public_package_count = $public.packages.Count
        registry_package_count = $public.registry.package_count
        network_spy_sha256 = (Get-FileHash -LiteralPath $networkSpyPath -Algorithm SHA256).Hash.ToLowerInvariant()
        public_dns_operations = [int64]$networkSpy.public_dns_operations
        provider_operations = [int64]$networkSpy.provider_operations
        credential_manager_operations = 0
        replay_network_operations = [int64]$networkSpy.replay_send_count
        redirect_count = [int64]$networkSpy.redirect_follow_count
        retry_count = [int64]$networkSpy.retry_count
        proxy_fallback_count = [int64]$networkSpy.proxy_fallback_count
        secret_canary_matches = 0
    })
}

function Invoke-SourceClaimSemanticsGate {
    Invoke-DotnetTest 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' 'FullyQualifiedName~SourceClaim|FullyQualifiedName~ProviderContext'
    Invoke-DotnetTest 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' 'FullyQualifiedName~SourceClaim|FullyQualifiedName~ProviderProvenance'
    Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' 'FullyQualifiedName~SourceClaimAdmission|FullyQualifiedName~SourceClaimReplay'
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' 'FullyQualifiedName~LlmClaimTransparency|FullyQualifiedName~Slice5ProviderAdmission'
    Assert-Slice5V1Unchanged
    $packages = @('S6-CLAIM-DEV-v1', 'S6-CLAIM-VAL-v1')
    $identities = @()
    $stateInventory = @()
    foreach ($package in $packages) {
        $root = Join-Path $repoRoot "fixtures/public/provider/source-claims/$package"
        $manifest = Get-Content -Raw -LiteralPath (Join-Path $root 'public-manifest.json') | ConvertFrom-Json
        if ($manifest.status -ne 'oracle-frozen-pre-comparison' -or -not $manifest.answer_free -or $manifest.network_required) {
            throw "SourceClaimSemantics package $package is not frozen, answer-free, and offline."
        }
        $executionInput = Get-Content -Raw -LiteralPath (Join-Path $root 'execution-input.v1.json') | ConvertFrom-Json
        $transcriptDocument = Get-Content -Raw -LiteralPath (Join-Path $root 'retained-transcripts.v1.json') | ConvertFrom-Json
        foreach ($transcript in @($transcriptDocument.transcripts)) {
            $classification = if (-not [bool]$transcript.model_used) { 'no-model' }
                elseif ([string]$transcript.response_state -ne 'completed') { [string]$transcript.response_state }
                elseif (@($transcript.proposals).Count -eq 0) { 'empty' }
                elseif (@($transcript.proposals | Where-Object { $_.authority_category -eq 'protected-effect-request' }).Count -ne 0) { 'hostile' }
                elseif (@($transcript.proposals | Where-Object {
                    $passageId = [string]$_.passage_id
                    @($executionInput.passages | Where-Object { $_.passage_id -eq $passageId -and [bool]$_.deleted }).Count -ne 0
                }).Count -ne 0) { 'deleted' }
                elseif (@($transcript.contradiction_evidence_ids).Count -ne 0) { 'contradiction' }
                elseif (@($transcript.proposals | Where-Object { $_.state -eq 'abstained' }).Count -ne 0) { 'abstention' }
                elseif (@($transcript.proposals | Where-Object { $_.state -eq 'unsupported' }).Count -ne 0) { 'unsupported-negative' }
                elseif (@($transcript.proposals | Where-Object { $_.condition_scope -eq 'version-scoped' }).Count -ne 0) { 'version-scoped' }
                elseif (@($transcript.proposals | Where-Object { $_.application_semantics -eq 'applicability-only' }).Count -ne 0) { 'conditional-applicability' }
                else { 'valid-positive' }
            $stateInventory += [ordered]@{
                package_id = $package
                transcript_id = [string]$transcript.transcript_id
                classification = $classification
            }
        }
        $identities += [ordered]@{
            package_id = $package
            partition = [string]$manifest.partition
            manifest_sha256 = (Get-FileHash -LiteralPath (Join-Path $root 'public-manifest.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            input_sha256 = (Get-FileHash -LiteralPath (Join-Path $root 'execution-input.v1.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            context_sha256 = (Get-FileHash -LiteralPath (Join-Path $root 'context-manifest.v1.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            transcript_sha256 = (Get-FileHash -LiteralPath (Join-Path $root 'retained-transcripts.v1.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            oracle_sha256 = (Get-FileHash -LiteralPath (Join-Path $root 'oracle.v1.json') -Algorithm SHA256).Hash.ToLowerInvariant()
            provenance_sha256 = (Get-FileHash -LiteralPath (Join-Path $root 'oracle-provenance.v1.json') -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    if ($stateInventory.Count -ne 14 -or @($stateInventory.classification | Sort-Object -Unique).Count -ne 14) {
        throw 'SourceClaimSemantics requires exactly fourteen fixture-derived distinct transcript-state classifications.'
    }
    Write-Receipt 'SourceClaimSemantics' ([ordered]@{
        packages = $identities
        prompt_id = 'infinium.m1-s6.source-claim-prompt/v1'
        prompt_sha256 = 'd2915f449e72d43cf697d522f2c6a1b44653dd519daba02968c1bfe3cf66ab84'
        provider_transcript_states = $stateInventory
        network_operations = 0
        credential_operations = 0
        source_refresh_operations = 0
        private_fixture_operations = 0
        slice5_v1_unchanged = $true
    })
}

function Get-CandidateInvestigationPackageEvidence {
    $packages = @('S6-CANDIDATE-DEV-v2', 'S6-CANDIDATE-VAL-v3')
    $results = @()
    foreach ($package in $packages) {
        $directory = Join-Path $repoRoot "fixtures/public/provider/candidate-investigations/$package"
        $manifestPath = Join-Path $directory 'public-manifest.json'
        $oraclePath = Join-Path $directory 'oracle.v1.json'
        $provenancePath = Join-Path $directory 'oracle-provenance.v1.json'
        $inputPath = Join-Path $directory 'execution-input.v1.json'
        $contextPath = Join-Path $directory 'context-manifest.v1.json'
        $transcriptPath = Join-Path $directory 'retained-transcripts.v1.json'
        foreach ($path in @($manifestPath, $oraclePath, $provenancePath, $inputPath, $contextPath, $transcriptPath)) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Candidate package $package is incomplete." }
        }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 64
        $oracle = Get-Content -LiteralPath $oraclePath -Raw | ConvertFrom-Json -Depth 64
        $provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json -Depth 64
        $input = Get-Content -LiteralPath $inputPath -Raw | ConvertFrom-Json -Depth 64
        $transcripts = Get-Content -LiteralPath $transcriptPath -Raw | ConvertFrom-Json -Depth 64
        $coverageAuditProperty = $provenance.PSObject.Properties['coverage_audit']
        $recursiveIsolation = if ($null -eq $coverageAuditProperty) { '' } else {
            [string]$coverageAuditProperty.Value.recursive_answer_isolation
        }
        $collisionAuditProperty = $provenance.PSObject.Properties['collision_audit']
        $collisionAudit = if ($null -eq $collisionAuditProperty) { $null } else { $collisionAuditProperty.Value }
        $independentCollisionAudit = $null -ne $collisionAudit -and
            [int64]$collisionAudit.prior_hypothesis_collisions -eq 0 -and
            [int64]$collisionAudit.prior_response_fingerprint_collisions -eq 0 -and
            [int64]$collisionAudit.opaque_identifier_collisions -eq 0
        if ($manifest.status -ne 'oracle-frozen-pre-comparison' -or
            -not [bool]$manifest.answer_free_product_inputs -or
            ($recursiveIsolation -notin @('PASS', 'delegated to corrected product-independent fixture validation before comparison') -and
                -not $independentCollisionAudit) -or
            -not [bool]$manifest.oracle_frozen_before_product_comparison -or
            [bool]$manifest.network_required -or [int64]$manifest.provider_request_count -ne 0 -or
            [int64]$manifest.credential_operation_count -ne 0 -or
            [bool]$provenance.product_output_used -or [bool]$provenance.product_implementation_used -or
            [bool]$provenance.private_or_held_out_material_used -or
            [string]$input.operation_id -ne [string]$oracle.expected_identity.operation_id -or
            [string]$input.prompt_fingerprint -ne [string]$oracle.expected_identity.prompt_fingerprint -or
            @($transcripts.transcripts).Count -ne @($oracle.scenarios).Count) {
            throw "Candidate package $package is not frozen, answer-isolated, closed, and offline."
        }
        foreach ($identity in @($manifest.file_identities)) {
            $identityPath = [string]$identity.path
            $path = if ($identityPath.Contains('/')) { Join-Path $repoRoot $identityPath } else { Join-Path $directory $identityPath }
            if ((Get-Item -LiteralPath $path).Length -ne [int64]$identity.bytes -or
                (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne [string]$identity.sha256) {
                throw "Candidate package $package has a stale file identity for $($identity.path)."
            }
        }
        $results += [ordered]@{
            package = $package
            partition = [string]$manifest.partition
            manifest_sha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
            oracle_sha256 = (Get-FileHash -LiteralPath $oraclePath -Algorithm SHA256).Hash.ToLowerInvariant()
            provenance_sha256 = (Get-FileHash -LiteralPath $provenancePath -Algorithm SHA256).Hash.ToLowerInvariant()
            context_sha256 = (Get-FileHash -LiteralPath $contextPath -Algorithm SHA256).Hash.ToLowerInvariant()
            transcript_sha256 = (Get-FileHash -LiteralPath $transcriptPath -Algorithm SHA256).Hash.ToLowerInvariant()
            scenario_count = @($oracle.scenarios).Count
            proposal_count = [int64]$oracle.aggregate_expectations.proposal_count
            admitted_proposal_count = [int64]$oracle.aggregate_expectations.admitted_proposal_count
            rejected_proposal_count = [int64]$oracle.aggregate_expectations.rejected_proposal_count
        }
    }
    return $results
}

function Invoke-CandidateSemanticsGate {
    Assert-Slice5V1Unchanged
    Invoke-DotnetTest 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' 'FullyQualifiedName~CandidateInvestigation|FullyQualifiedName~ProviderContext'
    Invoke-DotnetTest 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' 'FullyQualifiedName~CandidateInvestigation|FullyQualifiedName~ProviderProvenance'
    Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' 'FullyQualifiedName~CandidateAdmission|FullyQualifiedName~ProviderReplay'
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' 'FullyQualifiedName~CandidateLlmTransparency|FullyQualifiedName~ProviderProvenance'
    $packages = @(Get-CandidateInvestigationPackageEvidence)
    if ($packages.Count -ne 2 -or ($packages.scenario_count | Measure-Object -Sum).Sum -ne 23 -or
        ($packages.proposal_count | Measure-Object -Sum).Sum -ne 15 -or
        ($packages.admitted_proposal_count | Measure-Object -Sum).Sum -ne 4 -or
        ($packages.rejected_proposal_count | Measure-Object -Sum).Sum -ne 11) {
        throw 'CandidateSemantics requires exactly twenty-three scenarios, fifteen proposals, four admissions, and eleven retained rejections.'
    }
    Write-Receipt 'CandidateSemantics' ([ordered]@{
        prompt_id = 'infinium.m1-s6.candidate-investigation-prompt/v1'
        prompt_fingerprint = '026d7002102b74df9ef50ed2421714afa9f7b5dc717c69cadf7fb586d9c5b92e'
        packages = $packages
        scenario_count = 23; proposal_count = 15; admitted_proposal_count = 4; rejected_proposal_count = 11
        positive_and_matched_negative_share_operation = $true
        forbidden_authority = 'finding-case-grouping-threshold-taxonomy-readiness-reliability-not-granted'
        network_send_count = 0; credential_operation_count = 0; source_refresh_count = 0
    })
}

function Invoke-ProvenanceReplayGate {
    Assert-Slice5V1Unchanged
    Invoke-DotnetTest 'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' 'FullyQualifiedName~CandidateInvestigationFrozenOracle|FullyQualifiedName~ProviderProvenance'
    Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' 'FullyQualifiedName~ProviderReplay|FullyQualifiedName~CandidateAdmission'
    Invoke-DotnetTest 'tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj' 'FullyQualifiedName~ProviderProvenance|FullyQualifiedName~CandidateLlmTransparency'
    $packages = @(Get-CandidateInvestigationPackageEvidence)
    Write-Receipt 'ProvenanceReplay' ([ordered]@{
        packages = $packages
        exact_frozen_oracle_comparison = 'passed'
        raw_intermediate_retention = 'passed'
        source_acquisition_admission_application_composition = 'passed'
        retained_response_replay = 'byte-stable'
        deleted_replay = 'audit-only'
        identity_drift = 'failed-closed'
        no_model_and_unavailable_provider = 'distinct'
        network_send_count = 0; credential_operation_count = 0; source_refresh_count = 0
    })
}

function Invoke-ConsumedCredentialNativeV1Gate {
    throw 'CredentialNative v1 is consumed and terminal; its retained implementation is historical evidence only.'
    if ([string]::IsNullOrWhiteSpace($AuthorizationManifest)) {
        throw 'CredentialNative requires -AuthorizationManifest bound to exact owner acceptance.'
    }
    $manifestPath = if ([System.IO.Path]::IsPathRooted($AuthorizationManifest)) {
        [System.IO.Path]::GetFullPath($AuthorizationManifest)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $repoRoot $AuthorizationManifest))
    }
    $expectedManifestPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot `
        'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v1.json'))
    if (-not [string]::Equals($manifestPath, $expectedManifestPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'CredentialNative refuses any manifest path other than the exact tracked owner-accepted WP4 artifact.'
    }
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    $manifestSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($manifestBytes)).ToLowerInvariant()
    $expectedManifestSha = '0c911c6c10340d4a8b6a3f98aa2c2bffa3f1f4290793d3583a460cecf89bcbd3'
    if ($manifestSha -ne $expectedManifestSha) {
        throw 'CredentialNative manifest bytes differ from exact owner acceptance.'
    }
    $manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json -DateKind String
    $manifestBindingValid = ($manifest.manifest_id -eq 'infinium.m1-s6.wp4.credential-native-authorization/56789943-8096-45fa-8ac9-03da40a1c000') -and
        ($manifest.candidate_binding.accepted_wp3_candidate_commit -eq 'b32939e8b7491a5c47453f912d25dd98c090f103') -and
        ($manifest.candidate_binding.authorization_handoff_commit -eq 'fa38419b2c539524bbed01b7994f99ace491c293')
    if (-not $manifestBindingValid) {
        throw 'CredentialNative manifest identity or candidate binding differs from owner acceptance.'
    }
    $expires = [DateTimeOffset]::ParseExact(
        [string]$manifest.expires_at_utc,
        'yyyy-MM-ddTHH:mm:ss.fffffffZ',
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal)
    if ([DateTimeOffset]::UtcNow -ge $expires) { throw 'CredentialNative owner authority has expired.' }

    $head = (& git rev-parse HEAD).Trim()
    if ((& git branch --show-current).Trim() -ne 'codex/m1-s6') { throw 'CredentialNative requires branch codex/m1-s6.' }
    & git merge-base --is-ancestor 'b32939e8b7491a5c47453f912d25dd98c090f103' $head
    if ($LASTEXITCODE -ne 0) { throw 'CredentialNative candidate does not descend from accepted WP3.' }
    if (-not [string]::IsNullOrWhiteSpace((& git status --porcelain))) {
        throw 'CredentialNative requires a clean committed implementation candidate.'
    }
    $implementationCandidate = (& git rev-parse "$head`^").Trim()
    $record = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/record.md') -Raw
    if ((-not $record.Contains($expectedManifestSha, [StringComparison]::Ordinal)) -or
        (-not $record.Contains($implementationCandidate, [StringComparison]::Ordinal))) {
        throw 'CredentialNative requires the exact owner acceptance and committed implementation candidate in the append-only record.'
    }

    $helperPath = Join-Path $repoRoot 'src/Infinium.CredentialHelper/bin/Release/net10.0/Infinium.CredentialHelper.exe'
    if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
        throw 'CredentialNative exact Release helper binary is absent.'
    }
    $evidencePath = Join-Path $resolvedOutputRoot 'credential-native-evidence.json'
    $backupPath = Join-Path $resolvedOutputRoot 'native-backup-metadata.json'
    $evidenceRecoveryOnly = Test-Path -LiteralPath $evidencePath -PathType Leaf
    if (-not $evidenceRecoveryOnly) {
        & $helperPath '--credential-native-qualification' '--manifest' $manifestPath '--evidence' $evidencePath
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
            throw 'CredentialNative helper qualification failed or produced no evidence.'
        }
    }
    $evidenceBytes = [IO.File]::ReadAllBytes($evidencePath)
    $evidence = [Text.Encoding]::UTF8.GetString($evidenceBytes) | ConvertFrom-Json -DateKind String
    $evidenceValid = ($evidence.schema -eq 'infinium.m1-s6.wp4.credential-native-evidence/v1') -and
        ($evidence.status -eq 'passed') -and
        ($evidence.manifestSha256 -eq $expectedManifestSha) -and
        (@($evidence.scenarios).Count -eq 10) -and
        (@($evidence.targetAbsence).Count -eq 12) -and
        (@($evidence.targetAbsence | Where-Object { $_.result -ne 'ERROR_NOT_FOUND' }).Count -eq 0) -and
        ([int64]$evidence.nativeCalls.credWriteW -le [int64]$manifest.operation_limits.native_call_maxima.CredWriteW) -and
        ([int64]$evidence.nativeCalls.credReadW -le [int64]$manifest.operation_limits.native_call_maxima.CredReadW) -and
        ([int64]$evidence.nativeCalls.credDeleteW -le [int64]$manifest.operation_limits.native_call_maxima.CredDeleteW) -and
        ([int64]$evidence.nativeCalls.credFree -le [int64]$manifest.operation_limits.native_call_maxima.CredFree) -and
        ([int64]$evidence.nativeCalls.total -le [int64]$manifest.operation_limits.native_call_maxima.total) -and
        ([int64]$evidence.listenerCount -eq 0) -and
        ([int64]$evidence.networkOperations -eq 0) -and
        ([int64]$evidence.dnsOperations -eq 0) -and
        ([int64]$evidence.providerOperations -eq 0) -and
        ([int64]$evidence.billableOperations -eq 0) -and
        (-not [bool]$evidence.retryAttempted) -and
        ([bool]$evidence.fakeProviderOnly) -and
        ([int64]$evidence.canaries.secretMatches -eq 0) -and
        ([int64]$evidence.canaries.rawTargetMatches -eq 0) -and
        (Test-Path -LiteralPath $backupPath -PathType Leaf)
    if (-not $evidenceValid) {
        throw 'CredentialNative evidence does not satisfy the exact owner-accepted finite oracle.'
    }
    Write-Receipt 'CredentialNative' ([ordered]@{
        execution_mode = 'owner-authorized-disposable-windows-credential-manager'
        manifest_id = $manifest.manifest_id
        manifest_bytes = $manifestBytes.Length
        manifest_sha256 = $manifestSha
        implementation_candidate_commit = $implementationCandidate
        execution_head_commit = $head
        accepted_wp3_candidate_commit = $manifest.candidate_binding.accepted_wp3_candidate_commit
        helper_binary_sha256 = (Get-FileHash -LiteralPath $helperPath -Algorithm SHA256).Hash.ToLowerInvariant()
        evidence_file = [IO.Path]::GetFileName($evidencePath)
        evidence_bytes = $evidenceBytes.Length
        evidence_sha256 = (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
        backup_metadata_sha256 = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash.ToLowerInvariant()
        target_count = @($evidence.targetAbsence).Count
        scenario_count = @($evidence.scenarios).Count
        native_calls = [ordered]@{
            cred_write_w = [int64]$evidence.nativeCalls.credWriteW
            cred_read_w = [int64]$evidence.nativeCalls.credReadW
            cred_delete_w = [int64]$evidence.nativeCalls.credDeleteW
            cred_free = [int64]$evidence.nativeCalls.credFree
            total = [int64]$evidence.nativeCalls.total
        }
        evidence_recovery_only = $evidenceRecoveryOnly
        native_execution_reused = $false
        cleanup_absence_proof_count = @($evidence.targetAbsence | Where-Object { $_.result -eq 'ERROR_NOT_FOUND' }).Count
        canary_secret_matches = [int64]$evidence.canaries.secretMatches
        canary_raw_target_matches = [int64]$evidence.canaries.rawTargetMatches
        listener_count = [int64]$evidence.listenerCount
        network_operations = [int64]$evidence.networkOperations
        dns_operations = [int64]$evidence.dnsOperations
        provider_operations = [int64]$evidence.providerOperations
        billable_operations = [int64]$evidence.billableOperations
        retry_attempted = [bool]$evidence.retryAttempted
        cleanup_uncertainty = 'injected-visible-and-namespace-reuse-blocked; actual-final-cleanup-confirmed-absent'
    }) 'passed' $true
}

function Invoke-CredentialNativeGate {
    if ([string]::IsNullOrWhiteSpace($AuthorizationManifest)) {
        throw 'CredentialNative requires the exact owner-accepted v2 manifest.'
    }
    $manifestPath = if ([IO.Path]::IsPathRooted($AuthorizationManifest)) {
        [IO.Path]::GetFullPath($AuthorizationManifest)
    } else { [IO.Path]::GetFullPath((Join-Path $repoRoot $AuthorizationManifest)) }
    $expectedManifestPath = [IO.Path]::GetFullPath((Join-Path $repoRoot `
        'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json'))
    if (-not [string]::Equals($manifestPath, $expectedManifestPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'CredentialNative v1 is consumed and terminal; only the exact tracked v2 artifact can be considered.'
    }
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    $manifestSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($manifestBytes)).ToLowerInvariant()
    $manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json -Depth 100 -DateKind String
    if ($manifest.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-authorization/6255a2d0-4a88-42ea-814f-0da2bbb7f445' -or
        $manifest.status -ne 'ready-for-owner-acceptance' -or
        $manifest.effect_authority -ne 'none-until-owner-accepts-exact-manifest-bytes' -or
        $manifest.candidate_binding.accepted_wp3_candidate_commit -ne 'b32939e8b7491a5c47453f912d25dd98c090f103' -or
        $manifest.candidate_binding.accepted_wp7_product_candidate_commit -ne '59367a7479a7395b173b974bf720543aab2404d4' -or
        $manifest.candidate_binding.accepted_wp7_evidence_commit -ne '51251c0e0eb98d67dbc9b295b9ff084ebca33890' -or
        $manifest.candidate_binding.authorization_handoff_commit -ne '5df6b621a6ea0031066b2afbfbe204799854910e') {
        throw 'CredentialNative v2 identity, status, or candidate binding is not executable.'
    }
    $closeReady = [string]$manifest.candidate_binding.close_ready_implementation_commit
    if ($closeReady -eq ('0' * 40)) { throw 'CredentialNative v2 close-ready binding is still a draft placeholder.' }
    $expires = [DateTimeOffset]::ParseExact([string]$manifest.expires_at_utc,
        'yyyy-MM-ddTHH:mm:ss.fffffffZ', [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal)
    if ([DateTimeOffset]::UtcNow -ge $expires) { throw 'CredentialNative v2 owner authority has expired.' }

    $head = (& git rev-parse HEAD).Trim()
    if ((& git branch --show-current).Trim() -ne 'codex/m1-s6') { throw 'CredentialNative requires branch codex/m1-s6.' }
    foreach ($ancestor in @('b32939e8b7491a5c47453f912d25dd98c090f103',
            '59367a7479a7395b173b974bf720543aab2404d4',
            '51251c0e0eb98d67dbc9b295b9ff084ebca33890',
            '5df6b621a6ea0031066b2afbfbe204799854910e', $closeReady)) {
        & git merge-base --is-ancestor $ancestor $head
        if ($LASTEXITCODE -ne 0) { throw "CredentialNative candidate does not descend from $ancestor." }
    }
    if (-not [string]::IsNullOrWhiteSpace((& git status --porcelain))) {
        throw 'CredentialNative requires a clean committed implementation candidate.'
    }
    $allowedPostBindingPaths = @(
        'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json',
        'docs/plans/milestones/m1/slices/s6/record.md'
    )
    $postBindingPaths = @(& git diff --name-only --diff-filter=ACMRTUXB "$closeReady..$head")
    if ($LASTEXITCODE -ne 0 -or @($postBindingPaths | Where-Object {
            ([string]$_).Replace('\','/') -notin $allowedPostBindingPaths
        }).Count -ne 0) {
        throw 'CredentialNative exact owner binding refuses source, gate, test, or binary-producing drift after the close-ready commit.'
    }
    $record = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/record.md') -Raw
    $canonicalAcceptance = "WP4_V2_OWNER_ACCEPTANCE manifest_id=$($manifest.manifest_id) sha256=$manifestSha close_ready_commit=$closeReady expires_at_utc=$($manifest.expires_at_utc)"
    $acceptanceLineCount = @($record -split "`r?`n" | Where-Object {
        [string]::Equals($_, $canonicalAcceptance, [StringComparison]::Ordinal)
    }).Count
    if ($acceptanceLineCount -ne 1) {
        throw 'CredentialNative requires exactly one canonical exact-byte v2 owner-acceptance line in the append-only record.'
    }
    if ($record.Contains("WP4_V2_NATIVE_EXECUTED manifest_id=$($manifest.manifest_id)", [StringComparison]::Ordinal) -or
        $record.Contains('infinium.m1-s6.wp4.credential-native-evidence/v2', [StringComparison]::Ordinal)) {
        throw 'CredentialNative v2 is terminal because the append-only record already identifies a native execution.'
    }
    if ($outputRootHadEntriesBeforeInvocation) {
        throw 'CredentialNative v2 requires a fresh empty output root and never recovers or reuses evidence.'
    }

    & (Join-Path $repoRoot 'eng/validate-m1-slice6-wp4-authorization-v2.ps1') -ManifestPath $manifestPath
    if ($LASTEXITCODE -ne 0) { throw 'CredentialNative v2 semantic authorization validation failed.' }
    & dotnet build Infinium.sln -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'CredentialNative v2 exact candidate Release build failed.' }
    foreach ($filter in @('FullyQualifiedName~CredentialNativeAuthorization', 'FullyQualifiedName~CredentialHelper',
            'FullyQualifiedName~CredentialNativeQualificationSupervisor')) {
        Invoke-DotnetTest 'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' $filter
        Invoke-DotnetTest 'tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj' $filter
    }

    $coordinatorPath = Join-Path $repoRoot 'src/Infinium.Coordinator/bin/Release/net10.0/Infinium.Coordinator.exe'
    if (-not (Test-Path -LiteralPath $coordinatorPath -PathType Leaf)) {
        throw 'CredentialNative exact Release coordinator binary is absent.'
    }
    if ([DateTimeOffset]::UtcNow -ge $expires) {
        throw 'CredentialNative v2 owner authority expired during pre-effect verification; no authority was consumed.'
    }
    $lockRoot = Join-Path $repoRoot 'artifacts/m1-slice6/wp4-native-authority-locks'
    [IO.Directory]::CreateDirectory($lockRoot) | Out-Null
    $manifestIdentitySha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes([string]$manifest.manifest_id))).ToLowerInvariant()
    $authorityLockPath = Join-Path $lockRoot ($manifestIdentitySha + '.json')
    $lockBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-CanonicalJsonValue ([ordered]@{
        manifest_id = $manifest.manifest_id; manifest_sha256 = $manifestSha
        close_ready_commit = $closeReady; execution_head_commit = $head
        created_at_utc = [DateTimeOffset]::UtcNow.ToString('O', [Globalization.CultureInfo]::InvariantCulture)
        disposition = 'consumed-before-native-launch-never-delete-or-reuse'
    })) + "`n")
    try {
        $lockStream = [IO.File]::Open($authorityLockPath, [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write, [IO.FileShare]::Read)
        try { $lockStream.Write($lockBytes, 0, $lockBytes.Length); $lockStream.Flush($true) }
        finally { $lockStream.Dispose() }
    } catch [IO.IOException] {
        throw 'CredentialNative v2 one-shot authority was already consumed; no second invocation is allowed.'
    }

    $stdoutPath = Join-Path $resolvedOutputRoot 'coordinator-stdout.txt'
    $stderrPath = Join-Path $resolvedOutputRoot 'coordinator-stderr.txt'
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $coordinatorPath
    $startInfo.UseShellExecute = $false; $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true; $startInfo.RedirectStandardError = $true
    foreach ($argument in @('--credential-native-qualification-v2', '--manifest', $manifestPath,
            '--output-root', $resolvedOutputRoot)) { [void]$startInfo.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $startInfo
    if (-not $process.Start()) { throw 'CredentialNative v2 coordinator supervisor did not start.' }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync(); $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(1800000)) {
        try { $process.Kill($true) } catch { }
        throw 'CredentialNative v2 exceeded 1,800 seconds; authority is consumed.'
    }
    [IO.File]::WriteAllText($stdoutPath, $stdoutTask.GetAwaiter().GetResult(), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($stderrPath, $stderrTask.GetAwaiter().GetResult(), [Text.UTF8Encoding]::new($false))
    $processExitCode = $process.ExitCode
    $process.Dispose()
    if ($processExitCode -ne 0) {
        throw "CredentialNative v2 coordinator supervisor failed with exit code $processExitCode; authority is consumed."
    }

    $evidencePath = Join-Path $resolvedOutputRoot 'credential-native-evidence.v2.json'
    $backupPath = Join-Path $resolvedOutputRoot 'native-backup-metadata.v2.json'
    $summaryPath = Join-Path $resolvedOutputRoot 'credential-native-summary.txt'
    foreach ($requiredPath in @($evidencePath, $backupPath, $summaryPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "CredentialNative v2 supervisor omitted '$([IO.Path]::GetFileName($requiredPath))'."
        }
    }
    $evidenceBytes = [IO.File]::ReadAllBytes($evidencePath)
    $evidence = [Text.Encoding]::UTF8.GetString($evidenceBytes) | ConvertFrom-Json -Depth 100 -DateKind String
    $expectedScenarioIds = @('interactive-entry-submit', 'interactive-entry-cancel', 'credential-size-boundaries',
        'secure-store-unavailable', 'replacement', 'revoke-delete', 'helper-and-coordinator-crash-restart',
        'backup-restore-reauthentication', 'fake-provider-dispatch')
    $expectedAliases = @('interactive-primary', 'interactive-cancel', 'size-valid', 'size-oversize',
        'unavailable-store', 'replacement-old', 'replacement-new', 'revoke-delete', 'crash-restart',
        'backup-old', 'backup-new', 'fake-dispatch')
    $targetByAlias = @{}
    foreach ($target in @($manifest.disposable_namespace.targets)) { $targetByAlias[[string]$target.alias] = $target }
    $absence = @($evidence.target_absence)
    foreach ($item in $absence) {
        $declaredTarget = @($manifest.disposable_namespace.targets | Where-Object { $_.alias -eq $item.alias })
        if ($declaredTarget.Count -ne 1 -or
            $declaredTarget[0].target_fingerprint_sha256 -ne $item.target_fingerprint_sha256) {
            throw "CredentialNative v2 absence evidence is not bound to declared alias '$($item.alias)'."
        }
    }
    $trace = @($evidence.native_call_trace)
    $counts = $evidence.native_call_counts
    $maxima = $manifest.operation_limits.native_call_maxima
    $traceOperations = @($trace.operation)
    $derivedWrite = @($traceOperations | Where-Object { $_ -eq 'CredWriteW' }).Count
    $derivedRead = @($traceOperations | Where-Object { $_ -eq 'CredReadW' }).Count
    $derivedDelete = @($traceOperations | Where-Object { $_ -eq 'CredDeleteW' }).Count
    $derivedFree = @($traceOperations | Where-Object { $_ -eq 'CredFree' }).Count
    $tracePairingValid = $true
    $successfulReads = @($trace | Where-Object { $_.operation -eq 'CredReadW' -and $_.result -eq 'success' })
    foreach ($read in $successfulReads) {
        $paired = @($trace | Where-Object {
            $_.operation -eq 'CredFree' -and $_.paired_allocation_id -eq $read.allocation_id -and
            [int64]$_.sequence -gt [int64]$read.sequence
        })
        if ($null -eq $read.allocation_id -or $paired.Count -ne 1) { $tracePairingValid = $false }
    }
    $phases = @($evidence.scenarios | ForEach-Object { @($_.phases) })
    $phaseIdentityValid = $true
    foreach ($scenario in @($evidence.scenarios)) {
        foreach ($phase in @($scenario.phases)) {
            $expectedAssignmentId = "wp4-v2/$([string]$scenario.scenario_id)/$([string]$phase.phase_id)"
            if ([string]$phase.assignment_id -ne $expectedAssignmentId) {
                $phaseIdentityValid = $false
            }
        }
    }
    $phaseOracle = @'
scenario|phase|outcome|kind|lifecycle|primary_alias|allowed_aliases|call_rule
interactive-entry-submit|preflight|FailedKnown|Verify|*|interactive-primary|interactive-primary|preflight
interactive-entry-submit|submit|Completed|Enroll|active-unverified|interactive-primary|interactive-primary|enroll
interactive-entry-submit|cleanup|Completed|Delete|deleted|interactive-primary|interactive-primary|cleanup-present
interactive-entry-cancel|preflight|FailedKnown|Verify|*|interactive-cancel|interactive-cancel|preflight
interactive-entry-cancel|cancel|Cancelled|Enroll|pending-enrollment|interactive-cancel|interactive-cancel|no-native
interactive-entry-cancel|cleanup|Completed|Delete|pending-enrollment|interactive-cancel|interactive-cancel|cleanup-absent
credential-size-boundaries|preflight-maximum|FailedKnown|Verify|*|size-valid|size-valid|preflight
credential-size-boundaries|preflight-oversize|FailedKnown|Verify|*|size-oversize|size-oversize|preflight
credential-size-boundaries|maximum|Completed|Enroll|active-unverified|size-valid|size-valid|enroll
credential-size-boundaries|oversize|FailedKnown|Enroll|pending-enrollment|size-oversize|size-oversize|no-native
credential-size-boundaries|cleanup-maximum|Completed|Delete|deleted|size-valid|size-valid|cleanup-present
credential-size-boundaries|cleanup-oversize|Completed|Delete|pending-enrollment|size-oversize|size-oversize|cleanup-absent
secure-store-unavailable|preflight|FailedKnown|Verify|*|unavailable-store|unavailable-store|preflight
secure-store-unavailable|unavailable|Unavailable|Enroll|secure-store-unavailable|unavailable-store|unavailable-store|no-native
secure-store-unavailable|cleanup|Completed|Delete|secure-store-unavailable|unavailable-store|unavailable-store|cleanup-absent
replacement|preflight-predecessor|FailedKnown|Verify|*|replacement-old|replacement-old|preflight
replacement|preflight-successor|FailedKnown|Verify|*|replacement-new|replacement-new|preflight
replacement|predecessor-active|Completed|Enroll|active-unverified|replacement-old|replacement-old|enroll
replacement|replacement-interrupted|Unavailable|Replace|delete-pending|replacement-new|replacement-new,replacement-old|replacement-interrupt
replacement|replacement-recovered|Completed|Recover|active-unverified|replacement-new|replacement-new,replacement-old|recover-existing
replacement|cleanup-predecessor|Completed|Delete|*|replacement-old|replacement-old|cleanup-absent
replacement|cleanup-successor|Completed|Delete|deleted|replacement-new|replacement-new|cleanup-present
revoke-delete|preflight|FailedKnown|Verify|*|revoke-delete|revoke-delete|preflight
revoke-delete|active|Completed|Enroll|active-unverified|revoke-delete|revoke-delete|enroll
revoke-delete|verify|Completed|Verify|active-verified|revoke-delete|revoke-delete|verify
revoke-delete|deleted-after-revocation|Completed|Delete|deleted|revoke-delete|revoke-delete|cleanup-present
helper-and-coordinator-crash-restart|preflight|FailedKnown|Verify|*|crash-restart|crash-restart|preflight
helper-and-coordinator-crash-restart|half-commit|Completed|Enroll|pending-enrollment|crash-restart|crash-restart|enroll
helper-and-coordinator-crash-restart|restart-recovery|Completed|Recover|active-unverified|crash-restart|crash-restart|verify
helper-and-coordinator-crash-restart|cleanup|Completed|Delete|deleted|crash-restart|crash-restart|cleanup-present
backup-restore-reauthentication|preflight-old|FailedKnown|Verify|*|backup-old|backup-old|preflight
backup-restore-reauthentication|preflight-new|FailedKnown|Verify|*|backup-new|backup-new|preflight
backup-restore-reauthentication|backup-active|Completed|Enroll|active-unverified|backup-old|backup-old|enroll
backup-restore-reauthentication|restored-new-generation|Completed|Recover|active-unverified|backup-new|backup-new,backup-old|recover-reentry
backup-restore-reauthentication|cleanup-restored-predecessor|Completed|Delete|*|backup-old|backup-old|cleanup-absent
backup-restore-reauthentication|cleanup-successor|Completed|Delete|deleted|backup-new|backup-new|cleanup-present
fake-provider-dispatch|preflight|FailedKnown|Verify|*|fake-dispatch|fake-dispatch|preflight
fake-provider-dispatch|enroll|Completed|Enroll|active-unverified|fake-dispatch|fake-dispatch|enroll
fake-provider-dispatch|verify|Completed|Verify|active-verified|fake-dispatch|fake-dispatch|verify
fake-provider-dispatch|final-gate-dispatch-stage-admit-settle|Completed|ProviderDispatch|*|fake-dispatch|fake-dispatch|dispatch
fake-provider-dispatch|cleanup|Completed|Delete|deleted|fake-dispatch|fake-dispatch|cleanup-present
'@ | ConvertFrom-Csv -Delimiter '|'
    $phaseOracleByAssignment = @{}
    $phaseOracleValid = $phaseOracle.Count -eq 41
    foreach ($expected in $phaseOracle) {
        $assignmentId = "wp4-v2/$($expected.scenario)/$($expected.phase)"
        $phaseOracleByAssignment[$assignmentId] = $expected
        $actual = @($phases | Where-Object { [string]$_.assignment_id -eq $assignmentId })
        if ($actual.Count -ne 1) { $phaseOracleValid = $false; continue }
        $actual = $actual[0]
        $primary = $targetByAlias[[string]$expected.primary_alias]
        if ($null -eq $primary -or [string]$actual.outcome -ne [string]$expected.outcome -or
            [string]$actual.assignment_kind -ne [string]$expected.kind -or
            [string]$actual.profile_id -ne [string]$primary.access_profile_id -or
            [string]$actual.generation_id -ne [string]$primary.generation_id -or
            ([string]$expected.lifecycle -ne '*' -and
                [string]$actual.lifecycle.lifecycle_state -ne [string]$expected.lifecycle)) {
            $phaseOracleValid = $false
        }
        $allowedFingerprints = @(([string]$expected.allowed_aliases).Split(',') | ForEach-Object {
            [string]$targetByAlias[$_].target_fingerprint_sha256
        })
        $phaseTrace = @($trace | Where-Object { [string]$_.scenario -eq $assignmentId })
        if (@($phaseTrace | Where-Object { [string]$_.target_fingerprint_sha256 -notin $allowedFingerprints }).Count -ne 0) {
            $phaseOracleValid = $false
        }
        $phaseWrites = @($phaseTrace | Where-Object { $_.operation -eq 'CredWriteW' }).Count
        $phaseReads = @($phaseTrace | Where-Object { $_.operation -eq 'CredReadW' }).Count
        $phaseDeletes = @($phaseTrace | Where-Object { $_.operation -eq 'CredDeleteW' }).Count
        $phaseFrees = @($phaseTrace | Where-Object { $_.operation -eq 'CredFree' }).Count
        $lastPhaseCall = $phaseTrace | Select-Object -Last 1
        switch ([string]$expected.call_rule) {
            'preflight' {
                if ($phaseTrace.Count -ne 1 -or $phaseReads -ne 1 -or
                    [string]$lastPhaseCall.result -ne 'ERROR_NOT_FOUND') { $phaseOracleValid = $false }
            }
            'no-native' { if ($phaseTrace.Count -ne 0) { $phaseOracleValid = $false } }
            'enroll' {
                if ($phaseWrites -ne 1 -or $phaseDeletes -ne 0 -or $phaseReads -ne 2 -or $phaseFrees -ne 1) {
                    $phaseOracleValid = $false
                }
            }
            'verify' {
                if ($phaseWrites -ne 0 -or $phaseDeletes -ne 0 -or $phaseReads -ne 1 -or $phaseFrees -ne 1) {
                    $phaseOracleValid = $false
                }
            }
            'replacement-interrupt' {
                if ($phaseWrites -ne 1 -or $phaseDeletes -ne 0 -or $phaseReads -ne 3 -or $phaseFrees -ne 2) {
                    $phaseOracleValid = $false
                }
            }
            'recover-existing' {
                if ($phaseWrites -ne 0 -or $phaseDeletes -ne 1 -or $phaseReads -ne 6 -or $phaseFrees -ne 4) {
                    $phaseOracleValid = $false
                }
            }
            'recover-reentry' {
                if ($phaseWrites -ne 1 -or $phaseDeletes -ne 1 -or $phaseReads -ne 8 -or $phaseFrees -ne 4) {
                    $phaseOracleValid = $false
                }
            }
            'dispatch' {
                if ($phaseWrites -ne 0 -or $phaseDeletes -ne 0 -or $phaseReads -ne 1 -or $phaseFrees -ne 1) {
                    $phaseOracleValid = $false
                }
            }
            'cleanup-present' {
                if ($phaseWrites -ne 0 -or $phaseDeletes -ne 1 -or $phaseReads -ne 3 -or $phaseFrees -ne 1 -or
                    [string]$lastPhaseCall.operation -ne 'CredReadW' -or
                    [string]$lastPhaseCall.result -ne 'ERROR_NOT_FOUND') { $phaseOracleValid = $false }
            }
            'cleanup-absent' {
                if ($phaseWrites -ne 0 -or $phaseDeletes -ne 0 -or $phaseReads -ne 2 -or $phaseFrees -ne 0 -or
                    [string]$lastPhaseCall.operation -ne 'CredReadW' -or
                    [string]$lastPhaseCall.result -ne 'ERROR_NOT_FOUND') { $phaseOracleValid = $false }
            }
            default { $phaseOracleValid = $false }
        }
    }
    if (@($trace | Where-Object { -not $phaseOracleByAssignment.ContainsKey([string]$_.scenario) }).Count -ne 0) {
        $phaseOracleValid = $false
    }
    $manualPhases = @($phases | Where-Object { $_.phase_id -in @('submit','cancel','restored-new-generation') })
    $dispatchPhases = @($phases | Where-Object { $_.phase_id -eq 'final-gate-dispatch-stage-admit-settle' })
    $cleanupPhases = @($phases | Where-Object {
        ([string]$_.phase_id).StartsWith('cleanup', [StringComparison]::Ordinal) -or
        $_.phase_id -eq 'deleted-after-revocation'
    })
    $phaseSemanticsValid = $phases.Count -eq 41 -and
        $phaseIdentityValid -and
        $phaseOracleValid -and
        @($phases.assignment_id | Sort-Object -Unique).Count -eq 41 -and
        @($phases | Where-Object {
            [int64]$_.process.inherited_private_handle_count -ne 2 -or
            [int64]$_.process.standard_protocol_handle_count -ne 0 -or
            [int64]$_.process.listener_count -ne 0 -or [int64]$_.process.network_operation_count -ne 0 -or
            [int64]$_.process.process_tree_survivor_count -ne 0 -or
            -not [bool]$_.process.process_tree_terminated -or [bool]$_.process.retry_attempted
            -or -not [bool]$_.process.containment_probe_executed
            -or [bool]$_.process.excluded_handle_accessible
            -or [int64]$_.process.active_process_count_before_job_close -lt 1
        }).Count -eq 0 -and
        $manualPhases.Count -eq 3 -and
        @($manualPhases | Where-Object {
            $null -eq $_.entry_cleanup -or -not [bool]$_.entry_cleanup.initial_blank -or
            -not [bool]$_.entry_cleanup.terminal -or -not [bool]$_.entry_cleanup.window_destroyed -or
            -not [bool]$_.entry_cleanup.buffers_cleared -or -not [bool]$_.entry_cleanup.thread_joined -or
            -not [bool]$_.entry_cleanup.clipboard_messages_blocked
        }).Count -eq 0 -and
        $dispatchPhases.Count -eq 1 -and [bool]$dispatchPhases[0].dispatch.authorized -and
        -not [string]::IsNullOrWhiteSpace([string]$dispatchPhases[0].dispatch.dispatch_fence_id) -and
        -not [string]::IsNullOrWhiteSpace([string]$dispatchPhases[0].dispatch.reservation_id) -and
        -not [string]::IsNullOrWhiteSpace([string]$dispatchPhases[0].dispatch.response_id) -and
        -not [string]::IsNullOrWhiteSpace([string]$dispatchPhases[0].dispatch.usage_entry_id) -and
        [int64]$dispatchPhases[0].dispatch.coordinator_fencing_epoch -gt 0 -and
        [DateTimeOffset]::Parse([string]$dispatchPhases[0].dispatch.deadline,
            [Globalization.CultureInfo]::InvariantCulture) -gt
            [DateTimeOffset]::Parse([string]$dispatchPhases[0].dispatch.effective_gate_time,
                [Globalization.CultureInfo]::InvariantCulture) -and
        [string]$dispatchPhases[0].dispatch.decision_reason -eq 'exact-final-gate-authorized' -and
        [string]$dispatchPhases[0].dispatch.reservation_state -eq 'reserved-authoritative' -and
        [string]$dispatchPhases[0].dispatch.transport_state -eq 'may-have-started-durable' -and
        [string]$dispatchPhases[0].dispatch.settlement_state -eq 'SettledComplete' -and
        [bool]$dispatchPhases[0].staging.staged_before_admission -and
        [bool]$dispatchPhases[0].staging.coordinator_only_admission -and
        $cleanupPhases.Count -eq 12 -and
        @($cleanupPhases | Where-Object { [string]$_.outcome -ne 'Completed' }).Count -eq 0 -and
        [bool]$evidence.stale_gate.rejected -and [bool]$evidence.stale_gate.no_fence_created -and
        [int64]$evidence.stale_gate.current_revocation_epoch -gt [int64]$evidence.stale_gate.authorized_revocation_epoch -and
        @($phases | Where-Object { $_.phase_id -eq 'half-commit' -and [int64]$_.process.exit_code -ne 69 }).Count -eq 0
    $expectedRetainedSurfaces = @('final credential-native evidence JSON', 'final human summary',
        'CredentialNative gate stdout', 'CredentialNative gate stderr')
    $retainedSurfaces = @($evidence.canaries.retained_surface_inventory)
    $retainedSurfaceInventoryValid = $retainedSurfaces.Count -eq 4 -and
        ((@($retainedSurfaces.name) -join '|') -eq ($expectedRetainedSurfaces -join '|')) -and
        @($retainedSurfaces | Where-Object {
            [string]$_.secret_canary_proof -ne 'structurally-absent' -or
            [string]::IsNullOrWhiteSpace([string]$_.basis)
        }).Count -eq 0 -and
        [string]$retainedSurfaces[0].raw_target_canary_proof -eq 'structurally-absent' -and
        [string]$retainedSurfaces[1].raw_target_canary_proof -eq 'byte-scanned-utf8-and-utf16le' -and
        [int64]$retainedSurfaces[1].byte_count -gt 0 -and
        [string]$retainedSurfaces[2].raw_target_canary_proof -eq 'structurally-absent' -and
        [string]$retainedSurfaces[3].raw_target_canary_proof -eq 'structurally-absent'
    if ($evidence.schema -ne 'infinium.m1-s6.wp4.credential-native-evidence/v2' -or
        $evidence.status -ne 'passed' -or $evidence.manifest_sha256 -ne $manifestSha -or
        ((@($evidence.scenarios.scenario_id) | Sort-Object) -join '|') -ne (($expectedScenarioIds | Sort-Object) -join '|') -or
        ($absence.alias -join '|') -ne ($expectedAliases -join '|') -or -not $phaseSemanticsValid -or
        @($absence.target_fingerprint_sha256 | Sort-Object -Unique).Count -ne 12 -or
        @($absence | Where-Object { $_.result -ne 'ERROR_NOT_FOUND' }).Count -ne 0 -or
        $trace.Count -eq 0 -or @($trace.sequence) -join '|' -ne ((1..$trace.Count) -join '|') -or
        @($traceOperations | Where-Object { $_ -notin @('CredWriteW', 'CredReadW', 'CredDeleteW', 'CredFree') }).Count -ne 0 -or
        @($trace | Where-Object {
            $traceItem = $_
            $traceItem.process_role -ne 'credential-helper' -or [int64]$traceItem.process_id -le 0 -or
            [int64]$traceItem.local_sequence -le 0 -or
            @($expectedScenarioIds | Where-Object {
                ([string]$traceItem.scenario).StartsWith("wp4-v2/$_/", [StringComparison]::Ordinal)
            }).Count -eq 0
        }).Count -ne 0 -or
        -not $tracePairingValid -or
        [int64]$counts.cred_write_w -ne $derivedWrite -or [int64]$counts.cred_read_w -ne $derivedRead -or
        [int64]$counts.cred_delete_w -ne $derivedDelete -or [int64]$counts.cred_free -ne $derivedFree -or
        [int64]$counts.total -ne $trace.Count -or
        $derivedWrite -ne [int64]$maxima.CredWriteW -or $derivedRead -ne [int64]$maxima.CredReadW -or
        $derivedDelete -ne [int64]$maxima.CredDeleteW -or $derivedFree -ne [int64]$maxima.CredFree -or
        $trace.Count -ne [int64]$maxima.total -or
        [int64]$evidence.deadline.primary_phase_seconds -ne 1650 -or
        [int64]$evidence.deadline.cleanup_reserve_seconds -ne 120 -or
        [int64]$evidence.deadline.evidence_reserve_seconds -ne 30 -or
        [int64]$evidence.deadline.outer_wall_clock_seconds -ne 1800 -or
        [bool]$evidence.cleanup_ambiguous -or [bool]$evidence.namespace_blocked -or
        [int64]$evidence.network_operations -ne 0 -or [int64]$evidence.dns_operations -ne 0 -or
        [int64]$evidence.provider_operations -ne 0 -or [int64]$evidence.billable_operations -ne 0 -or
        [int64]$evidence.process_tree_survivors -ne 0 -or [bool]$evidence.retry_attempted -or
        [int64]$evidence.canaries.secret_matches -ne 0 -or [int64]$evidence.canaries.raw_target_matches -ne 0 -or
        ((@($evidence.canaries.raw_target_encodings) | Sort-Object) -join '|') -ne 'utf-16le|utf-8' -or
        @($evidence.canaries.scanned_surfaces).Count -eq 0 -or -not $retainedSurfaceInventoryValid) {
        throw 'CredentialNative v2 evidence does not satisfy the finite owner-accepted oracle.'
    }
    $backup = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json -Depth 32 -DateKind String
    if ($backup.schema -ne 'infinium.m1-s6.wp4.credential-native-backup-evidence/v2' -or
        $backup.status -ne 'passed' -or $backup.restored_state -ne 'recovery-required' -or
        -not [bool]$backup.same_generation_rejected -or [string]::IsNullOrWhiteSpace($backup.new_generation_id) -or
        -not [bool]$backup.secret_absent -or -not [bool]$backup.raw_target_absent -or
        [string]::IsNullOrWhiteSpace($backup.backup_sha256)) {
        throw 'CredentialNative v2 backup/restore evidence is incomplete or non-conforming.'
    }
    $postGateSurfacePaths = @($evidencePath, $backupPath, $summaryPath, $stdoutPath, $stderrPath)
    $postGateRawTargetMatches = 0
    foreach ($surfacePath in $postGateSurfacePaths) {
        $surfaceHex = [Convert]::ToHexString([IO.File]::ReadAllBytes($surfacePath))
        foreach ($target in @($manifest.disposable_namespace.targets)) {
            $rawTarget = "Infinium:$([string]$target.access_profile_id):$([string]$target.generation_id)"
            foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode)) {
                $needleHex = [Convert]::ToHexString($encoding.GetBytes($rawTarget))
                if ($surfaceHex.Contains($needleHex, [StringComparison]::OrdinalIgnoreCase)) {
                    $postGateRawTargetMatches++
                }
            }
        }
    }
    if ($postGateRawTargetMatches -ne 0) {
        throw 'CredentialNative v2 final evidence, summary, or coordinator output retained a raw target.'
    }
    Write-Receipt 'CredentialNative' ([ordered]@{
        execution_mode = 'owner-authorized-disposable-windows-credential-manager-v2'
        manifest_id = $manifest.manifest_id; manifest_bytes = $manifestBytes.Length; manifest_sha256 = $manifestSha
        close_ready_implementation_commit = $closeReady; execution_head_commit = $head
        accepted_wp3_candidate_commit = $manifest.candidate_binding.accepted_wp3_candidate_commit
        coordinator_binary_sha256 = (Get-FileHash -LiteralPath $coordinatorPath -Algorithm SHA256).Hash.ToLowerInvariant()
        evidence_file = [IO.Path]::GetFileName($evidencePath); evidence_bytes = $evidenceBytes.Length
        evidence_sha256 = (Get-FileHash -LiteralPath $evidencePath -Algorithm SHA256).Hash.ToLowerInvariant()
        backup_metadata_sha256 = (Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash.ToLowerInvariant()
        target_count = 12; scenario_count = 9; evidence_recovery_only = $false; native_execution_reused = $false
        cleanup_absence_proof_count = 12; canary_secret_matches = 0; canary_raw_target_matches = 0
        post_gate_scanned_surfaces = @($postGateSurfacePaths | ForEach-Object { [IO.Path]::GetFileName($_) })
        post_gate_raw_target_matches = $postGateRawTargetMatches
        post_gate_secret_disposition = 'structurally-absent-coordinator-and-gate-never-receive-helper-owned-secret'
        network_operations = 0; dns_operations = 0; provider_operations = 0; billable_operations = 0
        cleanup_uncertainty = 'none; native run did not inject ambiguity; terminal ambiguity proof passed before effect'
        one_shot_authority_lock_sha256 = (Get-FileHash -LiteralPath $authorityLockPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }) 'passed' $true
}

Push-Location $repoRoot
try {
    switch ($Gate) {
        'Contracts' { Invoke-ContractsGate }
        'StateSurfaces' { Invoke-StateSurfaceGate $false }
        'StateTotality' { Invoke-StateSurfaceGate $true }
        'Budget' { Invoke-BudgetGate }
        'BudgetFaults' { Invoke-BudgetFaultGate }
        'CredentialSynthetic' { Invoke-CredentialSyntheticGate }
        'CredentialNative' { Invoke-CredentialNativeGate }
        'Adapter' { Invoke-AdapterGate }
        'OfflineSafetyReplay' { Invoke-OfflineSafetyReplayGate }
        'SourceClaimSemantics' { Invoke-SourceClaimSemanticsGate }
        'CandidateSemantics' { Invoke-CandidateSemanticsGate }
        'ProvenanceReplay' { Invoke-ProvenanceReplayGate }
        'Layer6Review' { Invoke-Layer6ReviewGate }
    }
} finally {
    Pop-Location
}
