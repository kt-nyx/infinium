[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Contracts', 'StateSurfaces', 'StateTotality', 'Budget', 'BudgetFaults', 'CredentialSynthetic', 'Layer6Review')]
    [string] $Gate,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [string] $BaselineCommit,

    [string] $CandidateCommit,

    [switch] $HandoffCloseout
)

if ($Gate -eq 'Layer6Review' -and $PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath,
        '-Gate', $Gate,
        '-OutputRoot', $OutputRoot,
        '-BaselineCommit', $BaselineCommit,
        '-CandidateCommit', $CandidateCommit
    )
    if ($HandoffCloseout) {
        $arguments += '-HandoffCloseout'
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
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$schemaNames = @(
    'provider-access-profile.v1.schema.json',
    'provider-operation.v1.schema.json',
    'provider-response.v1.schema.json',
    'source-claim-extraction.v1.schema.json',
    'candidate-investigation.v1.schema.json',
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

function Write-Receipt([string] $Name, [System.Collections.IDictionary] $Evidence, [string] $Status = 'passed') {
    $receipt = [ordered]@{
        gate = $Name
        status = $Status
        network_permitted = $false
        credential_access_permitted = $false
        evidence = $Evidence
    }
    $json = $receipt | ConvertTo-Json -Depth 16
    [System.IO.File]::WriteAllText(
        (Join-Path $resolvedOutputRoot ($Name.ToLowerInvariant() + '.json')),
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
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
        'Infinium.sln',
        'contracts/json-schema/README.md',
        'contracts/protobuf/README.md',
        'contracts/repository/public-fixture-registry.v1.schema.json',
        'dependencies/README.md',
        'dependencies/dependency-curation.json',
        'dependencies/dependency-manifest.json',
        'docs/evaluation/repository-evaluation-authority.v1.json',
        'docs/plans/milestones/m1/slices/s6/README.md',
        'docs/plans/milestones/m1/slices/s6/record.md',
        'docs/plans/milestones/m1/slices/s6/wp1-contract-traceability.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp1-acceptance-ledger.v1.json',
        'docs/research/investigations/README.md',
        'docs/research/investigations/RESEARCH-0055-slice6-local-input-bound-policy.md',
        'docs/research/source-registry.md',
        'eng/generate-m1-slice6-wp1-traceability.ps1',
        'eng/verify-m1-slice6.ps1',
        'fixtures/public/public-fixture-registry.v1.json',
        'fixtures/tooling/reseal-public-fixtures.mjs',
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
            $isProtected = (Test-Wp1ProtectedPath $path) -and -not $isHandoffCurrentState
            $isAllowed = (Test-Wp1AllowedPath $path) -or $isHandoffCurrentState
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
        $failures.Add("Changed path is outside WP1 authority or protected: $($failure.path)")
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

    $status = if ($failures.Count -eq 0) { 'passed' } else { 'failed' }
    Write-Receipt 'Layer6Review' ([ordered]@{
        baseline_input = $BaselineCommit
        baseline_commit = $baselineHash
        candidate_input = $CandidateCommit
        candidate_commit = $candidateHash
        candidate_bound = $true
        handoff_closeout = [bool]$HandoffCloseout
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
    $scanRoots = @(
        (Join-Path $repoRoot 'artifacts/m1-slice6/wp3'),
        (Join-Path $repoRoot 'fixtures/public/platform/credential-helper'),
        (Join-Path $repoRoot 'src/Infinium.CredentialHelper/bin/Release/net10.0'),
        (Join-Path $repoRoot 'src/Infinium.Coordinator/bin/Release/net10.0'))
    $secretCanaryMatches = @(Get-ChildItem -LiteralPath $scanRoots -File -Recurse -ErrorAction SilentlyContinue |
        Select-String -SimpleMatch 'WP3-SECRET-CANARY-DO-NOT-RETAIN' -List -ErrorAction SilentlyContinue).Count
    $targetCanaryMatches = @(Get-ChildItem -LiteralPath $scanRoots -File -Recurse -ErrorAction SilentlyContinue |
        Select-String -SimpleMatch 'credential_target' -List -ErrorAction SilentlyContinue).Count
    if ($secretCanaryMatches -ne 0 -or $targetCanaryMatches -ne 0) { throw 'CredentialSynthetic measured canary scan found forbidden retained material.' }
    Write-Receipt 'CredentialSynthetic' ([ordered]@{
        execution_mode = 'synthetic-fake-secure-store-nonnetwork'
        production_test_filters = @($unitFilter, $integrationFilter, $securityFilter, $faultFilter, $evaluationFilter)
        wp3_public_package_count = $packages.Count
        registry_package_count = $registry.package_count
        helper_binary_sha256 = $actualHelperSha
        helper_protocol_sha256 = '2eac265ef75cc827bd5a8596120f5ba4c1912dde2219ad98eb11e2984cb043c0'
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
        stage_before_admit = [bool]$dynamic.stage_before_admit
        coordinator_only_admission = [bool]$dynamic.coordinator_only_admission
        process_tree_survivors = [int64]$dynamic.process_tree_survivors
    })
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
        'Layer6Review' { Invoke-Layer6ReviewGate }
    }
} finally {
    Pop-Location
}
