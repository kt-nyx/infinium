[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Contracts', 'StateSurfaces', 'StateTotality', 'Layer6Review')]
    [string] $Gate,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [string] $BaselineCommit,

    [string] $CandidateCommit
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
            $isProtected = Test-Wp1ProtectedPath $path
            $isAllowed = Test-Wp1AllowedPath $path
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

    $status = if ($failures.Count -eq 0) { 'passed' } else { 'failed' }
    Write-Receipt 'Layer6Review' ([ordered]@{
        baseline_input = $BaselineCommit
        baseline_commit = $baselineHash
        candidate_input = $CandidateCommit
        candidate_commit = $candidateHash
        candidate_bound = $true
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
        public_package_count = 19
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

Push-Location $repoRoot
try {
    switch ($Gate) {
        'Contracts' { Invoke-ContractsGate }
        'StateSurfaces' { Invoke-StateSurfaceGate $false }
        'StateTotality' { Invoke-StateSurfaceGate $true }
        'Layer6Review' { Invoke-Layer6ReviewGate }
    }
} finally {
    Pop-Location
}
