[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedOutput,

    [Parameter(Mandatory = $true)]
    [string]$ZeroDenominatorExpectedOutput
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    throw "Protocol /4 authorability validation failed: $Message"
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "required JSON file is absent: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-UniqueOrdinal([object[]]$Values, [string]$Label) {
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($value in $Values) {
        $text = [string]$value
        if (-not $seen.Add($text)) {
            Fail "duplicate $Label '$text'"
        }
    }
}

function Assert-ClosedValue([object]$Value, [string[]]$Allowed, [string]$Label) {
    if ($null -eq $Value) {
        return
    }

    $text = [string]$Value
    if ($Allowed -cnotcontains $text) {
        Fail "$Label '$text' is outside the closed vocabulary"
    }
}

function Assert-FiniteNumber([double]$Value, [string]$Label) {
    if ([double]::IsNaN($Value) -or [double]::IsInfinity($Value)) {
        Fail "$Label is nonfinite"
    }
}

function Assert-FaceGenTransport([bool]$Present, [bool]$ExactAbsenceKnown, [object]$Winner, [string]$Label) {
    if ($Present -and $ExactAbsenceKnown) {
        Fail "$Label claims both presence and exact absence"
    }
    if ($Present -and $null -eq $Winner) {
        Fail "$Label is present without a winner"
    }
    if (-not $Present -and $null -ne $Winner) {
        Fail "$Label is non-present with a winner"
    }
}

function Assert-ExactCoveragePopulations([string[]]$Actual, [string[]]$Required, [string]$Label) {
    Assert-UniqueOrdinal -Values $Actual -Label "$Label population"
    if ($Actual.Count -ne $Required.Count) {
        Fail "$Label contains $($Actual.Count) populations instead of $($Required.Count)"
    }
    foreach ($population in $Required) {
        if ($Actual -cnotcontains $population) {
            Fail "$Label is missing fixed population '$population'"
        }
    }
}

function Assert-TaxonomyEvidenceBasis([string]$EvidenceBasis, [string]$Axis) {
    if ($EvidenceBasis -ceq 'edid_only' -and $Axis -cne 'technical-modification-surface') {
        Fail "EDID-only evidence cannot establish taxonomy axis '$Axis'"
    }
}

function Invoke-RejectionSelfCheck([string]$Name, [scriptblock]$Action) {
    $rejected = $false
    try {
        & $Action
    }
    catch {
        $rejected = $true
    }
    if (-not $rejected) {
        Fail "mutation self-check '$Name' did not reject"
    }
    return 'rejected'
}

function Get-ForbiddenPropertyHits([object]$Node, [string[]]$Forbidden, [string]$Path = '$') {
    $hits = [System.Collections.Generic.List[string]]::new()
    if ($null -eq $Node) {
        return $hits
    }

    if ($Node -is [System.Collections.IDictionary]) {
        foreach ($key in $Node.Keys) {
            $name = [string]$key
            if ($Forbidden -ccontains $name) {
                $hits.Add("$Path.$name")
            }
            foreach ($hit in Get-ForbiddenPropertyHits -Node $Node[$key] -Forbidden $Forbidden -Path "$Path.$name") {
                $hits.Add($hit)
            }
        }
        return $hits
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Node.PSObject.Properties) {
            if ($Forbidden -ccontains $property.Name) {
                $hits.Add("$Path.$($property.Name)")
            }
            foreach ($hit in Get-ForbiddenPropertyHits -Node $property.Value -Forbidden $Forbidden -Path "$Path.$($property.Name)") {
                $hits.Add($hit)
            }
        }
        return $hits
    }

    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [string]) {
        $index = 0
        foreach ($item in $Node) {
            foreach ($hit in Get-ForbiddenPropertyHits -Node $item -Forbidden $Forbidden -Path "$Path[$index]") {
                $hits.Add($hit)
            }
            $index++
        }
    }
    return $hits
}

$package = (Resolve-Path -LiteralPath $PackageRoot).Path
$outputPath = (Resolve-Path -LiteralPath $ExpectedOutput).Path
$manifestPath = Join-Path $package 'execution-manifest.json'
$bytesPath = Join-Path $package 'synthetic-byte-input.json'
$ledgerPath = Join-Path $package 'coverage-ledger.json'
$zeroManifestPath = Join-Path $package 'zero-denominator-execution-manifest.json'
$zeroBytesPath = Join-Path $package 'zero-denominator-byte-input.json'
$zeroOutputPath = (Resolve-Path -LiteralPath $ZeroDenominatorExpectedOutput).Path

$manifest = Read-Json $manifestPath
$bytes = Read-Json $bytesPath
$ledger = Read-Json $ledgerPath
$output = Read-Json $outputPath
$zeroManifest = Read-Json $zeroManifestPath
$zeroBytes = Read-Json $zeroBytesPath
$zeroOutput = Read-Json $zeroOutputPath

if ($manifest.schema_id -cne 'infinium.evaluation.protocol-4-oracle-authorability-input/v1') {
    Fail 'unexpected answer-free manifest schema'
}
if ($bytes.schema_id -cne 'infinium.evaluation.protocol-4-oracle-authorability-byte-ledger/v1') {
    Fail 'unexpected synthetic byte-ledger schema'
}
if ($ledger.schema_id -cne 'infinium.evaluation.protocol-4-oracle-authorability-coverage/v1') {
    Fail 'unexpected coverage-ledger schema'
}
if ($manifest.fixture_id -cne $bytes.fixture_id -or $manifest.fixture_id -cne $ledger.fixture_id) {
    Fail 'package fixture identities differ'
}
if ($manifest.expected_output_present -ne $false -or $manifest.product_candidate_present -ne $false) {
    Fail 'answer-free manifest claims an expected output or product candidate'
}

$forbidden = @($ledger.answer_fields_forbidden | ForEach-Object { [string]$_ })
$hits = @()
$hits += Get-ForbiddenPropertyHits -Node $manifest -Forbidden $forbidden
$hits += Get-ForbiddenPropertyHits -Node $bytes -Forbidden $forbidden
$hits += Get-ForbiddenPropertyHits -Node $zeroManifest -Forbidden $forbidden
$hits += Get-ForbiddenPropertyHits -Node $zeroBytes -Forbidden $forbidden
if ($hits.Count -gt 0) {
    Fail "answer-bearing package properties found: $($hits -join ', ')"
}

Assert-UniqueOrdinal -Values @($manifest.plugins | ForEach-Object { $_.plugin_name.ToLowerInvariant() }) -Label 'plugin name'
Assert-UniqueOrdinal -Values @($manifest.plugins | ForEach-Object { [string]$_.load_order }) -Label 'plugin load order'
Assert-UniqueOrdinal -Values @($manifest.loose_provider_chains | ForEach-Object { $_.normalized_relative_path.Replace('\', '/').ToLowerInvariant() }) -Label 'provider-chain path'
foreach ($chain in $manifest.loose_provider_chains) {
    $providerIds = @($chain.providers | ForEach-Object { $_.local_installed_entity_id.ToLowerInvariant() })
    Assert-UniqueOrdinal -Values $providerIds -Label 'provider ID within a chain'
    if ($providerIds -cnotcontains $chain.winner_local_installed_entity_id.ToLowerInvariant()) {
        Fail "provider-chain winner is outside its chain: $($chain.normalized_relative_path)"
    }
}

foreach ($event in @($bytes.unsupported_semantic_events)) {
    if ($event.PSObject.Properties.Name -ccontains 'affected_members') {
        Fail 'unsupported semantic events must enumerate members, not anonymous aggregate counts'
    }
    $members = @($event.members)
    if ($members.Count -lt 1) {
        Fail "unsupported $($event.kind) event has no explicit member"
    }
    $memberKeys = @($members | ForEach-Object { "$($_.source_plugin.ToLowerInvariant())|$($_.load_order)|$($_.form_key.ToLowerInvariant())" })
    Assert-UniqueOrdinal -Values $memberKeys -Label "unsupported $($event.kind) event member"
    foreach ($member in $members) {
        if ($member.admitted -ne $true) {
            Fail "unsupported $($event.kind) event contains a non-admitted member"
        }
        if ($event.kind -ceq 'field' -and $member.successfully_decoded -ne $true) {
            Fail 'unsupported field exercise must identify a decoded contribution carrying the field gap'
        }
        if ($event.kind -ceq 'shape' -and $member.successfully_decoded -ne $false) {
            Fail 'unsupported shape exercise must identify an admitted undecoded contribution'
        }
    }
}

$requiredFamilies = @(
    'result', 'plugins', 'override_chains', 'npc_contributions',
    'race_contributions', 'placed_reference_contributions',
    'allowlisted_fields', 'npcs', 'races', 'placed_references', 'face_gen',
    'taxonomy', 'coverage', 'gaps', 'result_gaps'
)
$ledgerFamilies = @($ledger.active_fact_families.PSObject.Properties.Name)
if ([System.Linq.Enumerable]::SequenceEqual([string[]]$requiredFamilies, [string[]]$ledgerFamilies) -ne $true) {
    Fail 'coverage ledger does not declare the exact fifteen active families in canonical order'
}

if ($output.schema_id -cne 'infinium.evaluator-v2.expected-semantic-output/v4' -or
    $output.protocol_id -cne 'infinium.evaluator-v2/4' -or
    $output.projection_id -cne 'infinium.evaluator-v2.slice4-semantic-projection' -or
    $output.projection_version -cne '3.0.0') {
    Fail 'review output identity does not bind frozen protocol /4 projection 3.0.0'
}
if ($output.corpus_id -cne $manifest.fixture_id -or $output.corpus_version -cne $manifest.fixture_version) {
    Fail 'review output does not bind the answer-free fixture identity'
}
Assert-ClosedValue $output.state @('completed', 'completed_with_gaps', 'invalid_input', 'changed_during_read', 'failed') 'output state'

$facts = @($output.facts)
if ($facts.Count -lt 1) {
    Fail 'review output contains no facts'
}
$factIds = @($facts | ForEach-Object { [string]$_.fact_id })
Assert-UniqueOrdinal -Values $factIds -Label 'fact ID'
[string[]]$sorted = @($factIds)
[Array]::Sort($sorted, [System.StringComparer]::Ordinal)
for ($index = 0; $index -lt $factIds.Count; $index++) {
    if ($factIds[$index] -cne $sorted[$index]) {
        Fail "facts are not in ordinal fact-ID order at index $index"
    }
}

$factTypes = @('state', 'plugin', 'winner', 'override_chain', 'contribution', 'record_identity', 'form_key', 'link', 'ownership', 'placement', 'field', 'npc', 'race', 'reference', 'face_gen', 'taxonomy', 'coverage', 'gap', 'failure')
$valueTypes = @('string', 'integer', 'number', 'boolean', 'null')
foreach ($fact in $facts) {
    if ([string]::IsNullOrWhiteSpace([string]$fact.fact_id)) {
        Fail 'blank fact ID'
    }
    Assert-ClosedValue $fact.fact_type $factTypes "fact type for $($fact.fact_id)"
    Assert-ClosedValue $fact.value_type $valueTypes "value type for $($fact.fact_id)"
    switch ([string]$fact.value_type) {
        'null' {
            if ($null -ne $fact.value) { Fail "null fact has a non-null value: $($fact.fact_id)" }
        }
        'boolean' {
            if ($fact.value -isnot [bool]) { Fail "boolean fact has a non-boolean value: $($fact.fact_id)" }
        }
        'string' {
            if ($fact.value -isnot [string]) { Fail "string fact has a non-string value: $($fact.fact_id)" }
        }
        'integer' {
            if ($fact.value -isnot [byte] -and $fact.value -isnot [int16] -and $fact.value -isnot [int32] -and $fact.value -isnot [int64]) {
                Fail "integer fact is not represented as an integral JSON value: $($fact.fact_id)"
            }
        }
        'number' {
            $numeric = [double]$fact.value
            Assert-FiniteNumber -Value $numeric -Label "number fact $($fact.fact_id)"
        }
    }
}

foreach ($family in $requiredFamilies) {
    if (@($factIds | Where-Object { $_.StartsWith("$family/", [System.StringComparison]::Ordinal) }).Count -eq 0) {
        Fail "active family '$family' has no authored fact"
    }
}

$masterStyles = @($facts | Where-Object fact_id -Like 'plugins/*/master_style' | ForEach-Object value)
foreach ($value in $masterStyles) { Assert-ClosedValue $value @('full', 'light') 'master style' }

$linkStates = @($facts | Where-Object { ($_.fact_type -eq 'link' -or $_.fact_type -eq 'ownership') -and $_.fact_id.EndsWith('/state', [System.StringComparison]::Ordinal) } | ForEach-Object value)
foreach ($value in $linkStates) { Assert-ClosedValue $value @('null', 'resolved', 'unresolved') 'link state' }

$linkFields = @($facts | Where-Object { ($_.fact_type -eq 'link' -or $_.fact_type -eq 'ownership') -and $_.fact_id.EndsWith('/field', [System.StringComparison]::Ordinal) } | ForEach-Object value)
foreach ($value in $linkFields) { Assert-ClosedValue $value @('TPLT', 'RNAM', 'HCLF', 'PKID', 'PNAM', 'NAME', 'XLKR', 'XLRL', 'XOWN') 'link field' }
$linkComponents = @($facts | Where-Object { ($_.fact_type -eq 'link' -or $_.fact_type -eq 'ownership') -and $_.fact_id.EndsWith('/component', [System.StringComparison]::Ordinal) } | ForEach-Object value)
foreach ($value in $linkComponents) { Assert-ClosedValue $value @('linked-reference', 'keyword') 'link component' }

$faceApplicability = @($facts | Where-Object { $_.fact_id -Like 'face_gen/*/applicability' } | ForEach-Object value)
foreach ($value in $faceApplicability) {
    Assert-ClosedValue $value @('applicable', 'not_applicable_deleted_winner', 'unknown_template_traits_decision', 'not_applicable_template_traits', 'unknown_race', 'not_applicable_race_without_face_gen_head') 'FaceGen applicability'
}
$facePresentFacts = @($facts | Where-Object { $_.fact_id -Like 'face_gen/*/*/present' })
foreach ($presentFact in $facePresentFacts) {
    $assetRoot = $presentFact.fact_id.Substring(0, $presentFact.fact_id.Length - '/present'.Length)
    $absenceFacts = @($facts | Where-Object { $_.fact_id -ceq "$assetRoot/exact_absence_known" })
    $winnerFacts = @($facts | Where-Object { $_.fact_id -ceq "$assetRoot/winner_provider_id" })
    if ($absenceFacts.Count -ne 1 -or $winnerFacts.Count -ne 1) {
        Fail "FaceGen asset '$assetRoot' lacks its exact transport triplet"
    }
    Assert-FaceGenTransport -Present ([bool]$presentFact.value) -ExactAbsenceKnown ([bool]$absenceFacts[0].value) -Winner $winnerFacts[0].value -Label $assetRoot
}

$taxonomyApplicability = @($facts | Where-Object { $_.fact_id -Like 'taxonomy/*/applicability' } | ForEach-Object value)
foreach ($value in $taxonomyApplicability) { Assert-ClosedValue $value @('assigned', 'unknown', 'unsupported', 'unmapped', 'not-applicable') 'taxonomy applicability' }
$taxonomyRoles = @($facts | Where-Object { $_.fact_id -Like 'taxonomy/*/role' } | ForEach-Object value)
foreach ($value in $taxonomyRoles) { Assert-ClosedValue $value @('declared', 'observed', 'predicted', 'established') 'taxonomy role' }
$taxonomyIds = @($facts | Where-Object { $_.fact_id -Like 'taxonomy/*/taxonomy_id' } | ForEach-Object value)
foreach ($value in $taxonomyIds) {
    if ($value -cne 'infinium.skyrim-se.mod-impact-taxonomy') { Fail "noncanonical taxonomy ID '$value'" }
}
$subjectTypes = @($facts | Where-Object { $_.fact_id -Like 'taxonomy/*/subject_type' } | ForEach-Object value)
foreach ($value in $subjectTypes) { Assert-ClosedValue $value @('record-contribution', 'record-semantic-subject', 'unsupported-record') 'taxonomy subject type' }

$coveragePopulations = @($facts | Where-Object { $_.fact_id -Like 'coverage/*/population' } | ForEach-Object { [string]$_.value })
$requiredPopulations = @('plugins', 'npc-records', 'race-records', 'placed-reference-records', 'unsupported-records', 'face-gen-loose-assets', 'face-gen-archive-assets', 'localized-strings', 'automatic-environment-discovery', 'taxonomy-subjects')
Assert-ExactCoveragePopulations -Actual $coveragePopulations -Required $requiredPopulations -Label 'coverage'
$coverageStates = @($facts | Where-Object { $_.fact_id -Like 'coverage/*/state' } | ForEach-Object value)
foreach ($value in $coverageStates) { Assert-ClosedValue $value @('completed', 'completed_with_gaps', 'failed', 'skipped_by_configuration', 'skipped_by_limit', 'unsupported') 'coverage state' }

$gapCapabilities = @($facts | Where-Object { ($_.fact_id -Like 'gaps/*/missing_capability' -or $_.fact_id -Like 'result_gaps/*/missing_capability') } | ForEach-Object value)
$allowedCapabilities = @('allowlisted-record-family-semantics', 'allowlisted-record-field-semantics', 'allowlisted-record-shape-semantics', 'localized-string-resolution', 'archive-activation-and-member-precedence', 'automatic-environment-discovery', 'complete-template-traits-decision', 'resolved-winning-race')
foreach ($value in $gapCapabilities) { Assert-ClosedValue $value $allowedCapabilities 'gap capability' }

if ($zeroManifest.schema_id -cne $manifest.schema_id -or $zeroBytes.schema_id -cne $bytes.schema_id) {
    Fail 'zero-denominator variant uses an unexpected package schema'
}
if ($zeroManifest.fixture_id -cne $zeroBytes.fixture_id -or
    $zeroManifest.fixture_id -cne 'P4-AUTHORABILITY-ZERO-001' -or
    $zeroManifest.expected_output_present -ne $false -or
    $zeroManifest.product_candidate_present -ne $false) {
    Fail 'zero-denominator variant identity or answer-isolation declaration is invalid'
}
if ($zeroOutput.schema_id -cne 'infinium.evaluator-v2.expected-semantic-output/v4' -or
    $zeroOutput.protocol_id -cne 'infinium.evaluator-v2/4' -or
    $zeroOutput.projection_id -cne 'infinium.evaluator-v2.slice4-semantic-projection' -or
    $zeroOutput.projection_version -cne '3.0.0' -or
    $zeroOutput.corpus_id -cne $zeroManifest.fixture_id -or
    $zeroOutput.corpus_version -cne $zeroManifest.fixture_version) {
    Fail 'zero-denominator review output identity is invalid'
}
$zeroFacts = @($zeroOutput.facts)
$zeroFactIds = @($zeroFacts | ForEach-Object { [string]$_.fact_id })
Assert-UniqueOrdinal -Values $zeroFactIds -Label 'zero-denominator fact ID'
[string[]]$zeroSorted = @($zeroFactIds)
[Array]::Sort($zeroSorted, [System.StringComparer]::Ordinal)
for ($index = 0; $index -lt $zeroFactIds.Count; $index++) {
    if ($zeroFactIds[$index] -cne $zeroSorted[$index]) {
        Fail "zero-denominator facts are not in ordinal fact-ID order at index $index"
    }
}
foreach ($factId in $zeroFactIds) {
    if (-not $factId.StartsWith('result/', [System.StringComparison]::Ordinal) -and
        -not $factId.StartsWith('coverage/', [System.StringComparison]::Ordinal)) {
        Fail "zero-denominator variant publishes non-result/non-coverage fact '$factId'"
    }
}
$zeroPopulationFacts = @($zeroFacts | Where-Object { $_.fact_id -Like 'coverage/*/population' })
$zeroPopulations = @($zeroPopulationFacts | ForEach-Object { [string]$_.value })
Assert-ExactCoveragePopulations -Actual $zeroPopulations -Required $requiredPopulations -Label 'zero-denominator coverage'
foreach ($populationFact in $zeroPopulationFacts) {
    $rowRoot = $populationFact.fact_id.Substring(0, $populationFact.fact_id.Length - '/population'.Length)
    $denominator = @($zeroFacts | Where-Object { $_.fact_id -ceq "$rowRoot/denominator" })
    $completed = @($zeroFacts | Where-Object { $_.fact_id -ceq "$rowRoot/completed" })
    $state = @($zeroFacts | Where-Object { $_.fact_id -ceq "$rowRoot/state" })
    if ($denominator.Count -ne 1 -or $completed.Count -ne 1 -or $state.Count -ne 1 -or
        [int64]$denominator[0].value -ne 0 -or [int64]$completed[0].value -ne 0 -or
        [string]$state[0].value -cne 'completed') {
        Fail "zero-denominator coverage row '$($populationFact.value)' is incomplete or nonzero"
    }
}

$serializedOutput = Get-Content -LiteralPath $outputPath -Raw
foreach ($forbiddenToken in @('contribution_id', 'participant_id', 'winner_contribution_id', 'assignment_id', 'analyzer_or_adjudicator_id', 'evidence_fields', 'gap_id', 'gap_ids', 'snapshot_id', 'provider-topology', 'unspecified')) {
    if ($serializedOutput.IndexOf($forbiddenToken, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Fail "review output contains forbidden product/unpublished token '$forbiddenToken'"
    }
}

# Mechanical validator mutation self-checks. These verify that the checker does
# not silently accept the generic mutation classes; they supply no expected
# semantic value.
$selfChecks = [ordered]@{
    duplicate_fact_id = Invoke-RejectionSelfCheck 'duplicate_fact_id' { Assert-UniqueOrdinal @('fact/a', 'fact/a') 'fact ID' }
    duplicate_taxonomy_tuple = Invoke-RejectionSelfCheck 'duplicate_taxonomy_tuple' { Assert-UniqueOrdinal @('subject|axis|facet|code|assigned|observed', 'subject|axis|facet|code|assigned|observed') 'taxonomy tuple' }
    duplicate_provider_id = Invoke-RejectionSelfCheck 'duplicate_provider_id' { Assert-UniqueOrdinal @('entity/provider', 'entity/provider') 'provider ID' }
    invalid_link_state = Invoke-RejectionSelfCheck 'invalid_link_state' { Assert-ClosedValue 'unspecified' @('null', 'resolved', 'unresolved') 'link state' }
    invalid_xlkr_component = Invoke-RejectionSelfCheck 'invalid_xlkr_component' { Assert-ClosedValue 'value' @('linked-reference', 'keyword') 'XLKR component' }
    invalid_face_gen_transport = Invoke-RejectionSelfCheck 'invalid_face_gen_transport' { Assert-FaceGenTransport $true $true 'entity/provider' 'FaceGen transport' }
    nonfinite_placement = Invoke-RejectionSelfCheck 'nonfinite_placement' { Assert-FiniteNumber ([double]::NaN) 'placement number' }
    missing_coverage_row = Invoke-RejectionSelfCheck 'missing_coverage_row' { Assert-ExactCoveragePopulations $requiredPopulations[0..8] $requiredPopulations 'coverage' }
    extra_provider_topology_subject = Invoke-RejectionSelfCheck 'extra_provider_topology_subject' { Assert-ClosedValue 'provider-topology' @('record-contribution', 'record-semantic-subject', 'unsupported-record') 'subject type' }
    edid_area_leak = Invoke-RejectionSelfCheck 'edid_area_leak' { Assert-TaxonomyEvidenceBasis 'edid_only' 'affected-game-system-or-content-area' }
}
$declaredMutations = @($ledger.mutations | ForEach-Object { [string]$_ })
$checkedMutations = @($selfChecks.Keys | ForEach-Object { [string]$_ })
if ([System.Linq.Enumerable]::SequenceEqual([string[]]$declaredMutations, [string[]]$checkedMutations) -ne $true) {
    Fail 'mutation self-check registry differs from the answer-free ledger'
}

$familyCounts = [ordered]@{}
foreach ($family in $requiredFamilies) {
    $familyCounts[$family] = @($factIds | Where-Object { $_.StartsWith("$family/", [System.StringComparison]::Ordinal) }).Count
}

$summary = [ordered]@{
    schema_id = 'infinium.evaluation.protocol-4-oracle-authorability-validation/v1'
    fixture_id = $manifest.fixture_id
    fixture_version = $manifest.fixture_version
    protocol_id = $output.protocol_id
    projection_version = $output.projection_version
    fact_count = $facts.Count
    fact_families = $familyCounts
    duplicate_fact_ids = 0
    fixed_coverage_population_count = $coveragePopulations.Count
    mutation_self_checks = $selfChecks
    package_files = [ordered]@{
        execution_manifest_sha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        synthetic_byte_input_sha256 = (Get-FileHash -LiteralPath $bytesPath -Algorithm SHA256).Hash.ToLowerInvariant()
        coverage_ledger_sha256 = (Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash.ToLowerInvariant()
        expected_output_sha256 = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
        zero_denominator_execution_manifest_sha256 = (Get-FileHash -LiteralPath $zeroManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        zero_denominator_byte_input_sha256 = (Get-FileHash -LiteralPath $zeroBytesPath -Algorithm SHA256).Hash.ToLowerInvariant()
        zero_denominator_expected_output_sha256 = (Get-FileHash -LiteralPath $zeroOutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    result = 'PASS'
}

$summary | ConvertTo-Json -Depth 10
