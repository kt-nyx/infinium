[CmdletBinding()]
param(
    [string]$ModelPath,
    [string]$SchemaPath,
    [string]$ArtifactPath,
    [string]$SummaryPath,
    [switch]$ValidateOnly,
    [switch]$SkipSelfTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:ForbiddenArtifactProperties = @(
    'expected_facts',
    'expected_output',
    'oracle_path',
    'candidate_output',
    'product_contribution_id',
    'product_participant_id',
    'private_path',
    'private_member_id'
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ModelPath) {
    $ModelPath = Join-Path $scriptRoot '..\docs\evaluation\specifications\m1-slice4-protocol-4-totality-model.json'
}
if (-not $SchemaPath) {
    $SchemaPath = Join-Path $scriptRoot '..\docs\evaluation\fixtures\protocol-4-oracle-authorability\generated-state-coverage.schema.json'
}
if (-not $ArtifactPath) {
    $ArtifactPath = Join-Path $scriptRoot '..\docs\evaluation\fixtures\protocol-4-oracle-authorability\generated-state-coverage.json'
}

function Read-Json([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Protocol /4 WP3 state coverage failed: missing $Label at '$Path'"
    }
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Protocol /4 WP3 state coverage failed: invalid $Label JSON at '$Path': $($_.Exception.Message)"
    }
}

function Copy-JsonObject([object]$Value) {
    return $Value | ConvertTo-Json -Depth 100 | ConvertFrom-Json
}

function Test-HasProperty([object]$Object, [string]$Name) {
    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}

function Add-Issue([System.Collections.Generic.List[string]]$Issues, [string]$Message) {
    if ($Issues.Count -lt 100) {
        $Issues.Add($Message)
    }
}

function Get-OrdinalStrings([object[]]$Values) {
    [string[]]$items = @($Values | ForEach-Object { [string]$_ })
    [Array]::Sort($items, [System.StringComparer]::Ordinal)
    return $items
}

function Test-OrdinalSequence([object[]]$Values) {
    [string[]]$actual = @($Values | ForEach-Object { [string]$_ })
    [string[]]$sorted = @(Get-OrdinalStrings $actual)
    if ($actual.Count -ne $sorted.Count) { return $false }
    for ($index = 0; $index -lt $actual.Count; $index++) {
        if ($actual[$index] -cne $sorted[$index]) { return $false }
    }
    return $true
}

function Test-SameStringSet([object[]]$Left, [object[]]$Right) {
    [string[]]$a = @(Get-OrdinalStrings $Left)
    [string[]]$b = @(Get-OrdinalStrings $Right)
    if ($a.Count -ne $b.Count) { return $false }
    for ($index = 0; $index -lt $a.Count; $index++) {
        if ($a[$index] -cne $b[$index]) { return $false }
    }
    return $true
}

function Test-JsonEquivalent([object]$Left, [object]$Right) {
    return ($Left | ConvertTo-Json -Depth 100 -Compress) -ceq ($Right | ConvertTo-Json -Depth 100 -Compress)
}

function Get-MappingByProperty([object[]]$Items, [string]$Property, [string]$Value) {
    return @($Items | Where-Object { [string]$_.$Property -ceq $Value })
}

function Get-Sha256Text([string]$Text) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($Text)
        return ([System.BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-ConditionMatches([object]$State, [object[]]$Conditions) {
    foreach ($condition in @($Conditions)) {
        $property = $State.PSObject.Properties[[string]$condition.dimension]
        if ($null -eq $property) { return $false }
        $actual = [string]$property.Value
        $values = @($condition.values | ForEach-Object { [string]$_ })
        switch ([string]$condition.operator) {
            'equals' { if ($values.Count -ne 1 -or $actual -cne $values[0]) { return $false } }
            'in' { if ($values -cnotcontains $actual) { return $false } }
            'not-in' { if ($values -ccontains $actual) { return $false } }
            default { return $false }
        }
    }
    return $true
}

function Get-StateProduct([object]$Model, [string[]]$Dimensions) {
    $states = [System.Collections.Generic.List[object]]::new()
    $states.Add([ordered]@{})
    foreach ($dimension in $Dimensions) {
        $next = [System.Collections.Generic.List[object]]::new()
        foreach ($prefix in $states) {
            foreach ($value in @($Model.dimensions.$dimension.values)) {
                $copy = [ordered]@{}
                foreach ($key in $prefix.Keys) { $copy[$key] = $prefix[$key] }
                $copy[$dimension] = [string]$value
                $next.Add($copy)
            }
        }
        $states = $next
    }
    return @($states | ForEach-Object { [pscustomobject]$_ })
}

function Get-StateKey([object]$State, [string[]]$Dimensions) {
    return (($Dimensions | ForEach-Object { "$_=$([string]$State.$_)" }) -join ';')
}

function Get-PairTokens([string]$Family, [object]$State, [string[]]$Dimensions) {
    $tokens = [System.Collections.Generic.List[string]]::new()
    for ($left = 0; $left -lt $Dimensions.Count; $left++) {
        for ($right = $left + 1; $right -lt $Dimensions.Count; $right++) {
            $a = $Dimensions[$left]
            $b = $Dimensions[$right]
            $tokens.Add("$Family|$a=$([string]$State.$a)|$b=$([string]$State.$b)")
        }
    }
    return $tokens.ToArray()
}

function Get-HammingDistance([object]$Left, [object]$Right, [string[]]$Dimensions) {
    $distance = 0
    foreach ($dimension in $Dimensions) {
        if ([string]$Left.$dimension -cne [string]$Right.$dimension) { $distance++ }
    }
    return $distance
}

function Get-FamilySlug([string]$Family) {
    return ($Family.Replace('_', '-').ToUpperInvariant())
}

function Add-SelectionReason(
    [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]$Selections,
    [string]$Key,
    [string]$Reason) {
    if (-not $Selections.ContainsKey($Key)) {
        $Selections[$Key] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    }
    [void]$Selections[$Key].Add($Reason)
}

function New-TruthModel([object]$Model) {
    $familyRecords = [ordered]@{}
    $rawByGlobalKey = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    $constraintCounts = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::Ordinal)
    $constraintRepresentatives = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    $totals = [ordered]@{ raw = 0; admitted = 0; excluded = 0; invalid = 0; uncovered = 0; overlap = 0 }
    $familySummaries = [System.Collections.Generic.List[object]]::new()
    $stateDigestLines = [System.Collections.Generic.List[string]]::new()

    foreach ($family in @($Model.fact_families)) {
        $familyName = [string]$family.family
        [string[]]$dimensions = @($family.dimensions_used | ForEach-Object { [string]$_ })
        $records = [System.Collections.Generic.List[object]]::new()
        $counts = [ordered]@{ family = $familyName; raw = 0; admitted = 0; excluded = 0; invalid = 0; uncovered = 0; overlap = 0 }
        $familyDigestLines = [System.Collections.Generic.List[string]]::new()

        foreach ($state in @(Get-StateProduct -Model $Model -Dimensions $dimensions)) {
            $counts.raw++
            $totals.raw++
            $admitted = @($family.state_space.admitted_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            $invalid = @($family.state_space.invalid_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            $excluded = @($family.state_space.excluded_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            $matchCount = $admitted.Count + $invalid.Count + $excluded.Count
            $classification = $null
            $constraint = $null
            $stateClass = $null
            $ruleId = $null
            $atomicBoundary = $null

            if ($matchCount -eq 0) {
                $classification = 'uncovered'
                $counts.uncovered++
                $totals.uncovered++
            }
            elseif ($matchCount -gt 1) {
                $classification = 'overlap'
                $counts.overlap++
                $totals.overlap++
            }
            elseif ($invalid.Count -eq 1) {
                $classification = 'invalid'
                $constraint = $invalid[0]
                $atomicBoundary = [string]$constraint.atomic_boundary
                $counts.invalid++
                $totals.invalid++
            }
            elseif ($excluded.Count -eq 1) {
                $classification = 'excluded'
                $constraint = $excluded[0]
                $counts.excluded++
                $totals.excluded++
            }
            else {
                $classification = 'admitted'
                $constraint = $admitted[0]
                $stateClass = [string]$constraint.state_class
                $rules = @($family.rules | Where-Object { Get-ConditionMatches $state @($_.when) })
                if ($rules.Count -ne 1) {
                    throw "Protocol /4 WP3 state coverage failed: admitted state in '$familyName' matches $($rules.Count) publication rules"
                }
                $ruleId = [string]$rules[0].rule_id
                $atomicBoundary = [string]$rules[0].atomic_boundary
                $counts.admitted++
                $totals.admitted++
            }

            $stateKey = Get-StateKey -State $state -Dimensions $dimensions
            $globalKey = "$familyName||$stateKey"
            $constraintId = if ($null -eq $constraint) { '' } else { [string]$constraint.constraint_id }
            $record = [pscustomobject][ordered]@{
                global_key = $globalKey
                family = $familyName
                state_key = $stateKey
                dimensions = $state
                classification = $classification
                constraint_id = $constraintId
                state_class = $stateClass
                rule_id = $ruleId
                atomic_boundary = $atomicBoundary
                pair_tokens = @(Get-PairTokens -Family $familyName -State $state -Dimensions $dimensions)
            }
            $records.Add($record)
            $rawByGlobalKey.Add($globalKey, $record)
            if ($constraintId) {
                if (-not $constraintCounts.ContainsKey($constraintId)) { $constraintCounts[$constraintId] = 0 }
                $constraintCounts[$constraintId]++
                if (-not $constraintRepresentatives.ContainsKey($constraintId)) {
                    $constraintRepresentatives[$constraintId] = $globalKey
                }
            }
            $line = "$familyName|$stateKey|$classification|$constraintId|$ruleId"
            $familyDigestLines.Add($line)
            $stateDigestLines.Add($line)
        }

        $familyRecords[$familyName] = $records.ToArray()
        $counts['state_digest_sha256'] = Get-Sha256Text (($familyDigestLines -join "`n") + "`n")
        $familySummaries.Add([pscustomobject]$counts)
    }

    if ($totals.uncovered -ne 0 -or $totals.overlap -ne 0) {
        throw "Protocol /4 WP3 state coverage failed: source model is not total (uncovered=$($totals.uncovered), overlap=$($totals.overlap))"
    }

    return [pscustomobject][ordered]@{
        family_records = $familyRecords
        raw_by_global_key = $rawByGlobalKey
        constraint_counts = $constraintCounts
        constraint_representatives = $constraintRepresentatives
        totals = [pscustomobject]$totals
        family_summaries = $familySummaries.ToArray()
        aggregate_state_digest_sha256 = Get-Sha256Text (($stateDigestLines -join "`n") + "`n")
    }
}

function Get-LexicalInputTable {
    return [ordered]@{
        'P4-NORM-FORMKEY' = [ordered]@{ values = @('0000002a:alpha.esm', '00000822:compact.esl'); negative = @('2a:alpha.esm', '00000822:compact.esl:extra') }
        'P4-NORM-SEGMENT' = [ordered]@{ values = @('entity/value with space|=:%', '0000002a:alpha.esm'); negative = @('entity+value', '%2f') }
        'P4-NORM-ORDER' = [ordered]@{ sequences = @('manifest', 'masters', 'contributions', 'providers', 'link-occurrences'); set_order = 'ordinal' }
        'P4-NORM-CASE' = [ordered]@{ plugin = 'Alpha.ESM'; provider = 'Entity/Layer'; signature = 'NPC_'; path = 'Meshes\Actors/Generic.NIF' }
        'P4-NORM-NUMBER' = [ordered]@{ integers = @('0', '9223372036854775807'); numbers = @('10', '10.0', '1e1', '-0.0'); negative = @('NaN', 'Infinity') }
        'P4-NORM-CONTRIBUTION' = [ordered]@{ source = 'alpha.esm'; order = 2; record = '0000002a:alpha.esm'; signature = 'npc_'; flags = '00040000'; deleted = $false; compressed = $true }
        'P4-NORM-LINK' = [ordered]@{ fields = @('TPLT', 'PKID', 'XLKR'); components = @('value', 'linked-reference', 'keyword'); ordinal = 0; states = @('null', 'resolved', 'unresolved') }
        'P4-NORM-PATH' = [ordered]@{ values = @('Meshes\Actors/Generic/0000002A.NIF', 'textures/actors/generic/0000002a.dds') }
        'P4-NORM-TAXONOMY' = [ordered]@{ subject = 'generic-subject'; subject_type = 'record-contribution'; code_states = @('assigned', 'null') }
        'P4-NORM-GAP' = [ordered]@{ population = 'unsupported-shapes:race:data'; missing_capability = 'allowlisted-record-shape-semantics'; affected_counts = @(1, 2) }
    }
}

function Get-Wp3Mutations {
    return @(
        [pscustomobject][ordered]@{ id = 'missing-state-case'; category = 'missing-case'; operation = 'remove a referenced generated state case' },
        [pscustomobject][ordered]@{ id = 'false-rule-coverage-claim'; category = 'false-coverage'; operation = 'map a publication rule to a case that does not match it' },
        [pscustomobject][ordered]@{ id = 'unstable-case-order'; category = 'ordering'; operation = 'reverse two generated state cases' },
        [pscustomobject][ordered]@{ id = 'duplicate-rule-mapping'; category = 'duplicate-mapping'; operation = 'repeat one case in a rule mapping' },
        [pscustomobject][ordered]@{ id = 'duplicate-case-id'; category = 'duplicate-case'; operation = 'reuse one stable case ID' },
        [pscustomobject][ordered]@{ id = 'missing-constraint-mapping'; category = 'missing-region'; operation = 'remove one admitted, invalid, or excluded constraint mapping' },
        [pscustomobject][ordered]@{ id = 'missing-pairwise-mapping'; category = 'pairwise'; operation = 'remove one required pairwise mapping' },
        [pscustomobject][ordered]@{ id = 'unknown-case-reference'; category = 'mapping-integrity'; operation = 'reference a case ID outside the generated case registry' },
        [pscustomobject][ordered]@{ id = 'broken-matched-negative'; category = 'matched-negative'; operation = 'map an admitted case to another admitted case' },
        [pscustomobject][ordered]@{ id = 'answer-bearing-property'; category = 'answer-isolation'; operation = 'add an expected_facts property to the tracked artifact' },
        [pscustomobject][ordered]@{ id = 'duplicate-gap-owner'; category = 'gap-ownership'; operation = 'repeat the partial RACE/DATA gap owner' },
        [pscustomobject][ordered]@{ id = 'partial-race-rule-omission'; category = 'higher-order'; operation = 'remove one required rule-to-case binding from the partial RACE/DATA exercise' },
        [pscustomobject][ordered]@{ id = 'state-digest-drift'; category = 'state-proof'; operation = 'replace the aggregate raw-state classification digest' },
        [pscustomobject][ordered]@{ id = 'wrong-family-mapping-case'; category = 'family-mapping'; operation = 'replace a family case with a nonexistent case' },
        [pscustomobject][ordered]@{ id = 'wrong-state-class-case'; category = 'state-class-mapping'; operation = 'replace a state-class case with a nonexistent case' },
        [pscustomobject][ordered]@{ id = 'wrong-disposition-case'; category = 'disposition-mapping'; operation = 'replace a disposition case with a nonexistent case' },
        [pscustomobject][ordered]@{ id = 'wrong-constructor-case'; category = 'constructor-mapping'; operation = 'replace a constructor case with a nonexistent case' },
        [pscustomobject][ordered]@{ id = 'wrong-atomic-boundary-case'; category = 'atomic-boundary'; operation = 'replace an atomic-boundary case with a case from another boundary' },
        [pscustomobject][ordered]@{ id = 'changed-lexical-inputs'; category = 'lexical-input'; operation = 'change a normalization exercise input' },
        [pscustomobject][ordered]@{ id = 'missing-lexical-inputs'; category = 'lexical-input'; operation = 'remove the generic inputs from a normalization exercise' },
        [pscustomobject][ordered]@{ id = 'changed-gap-population'; category = 'gap-exercise'; operation = 'change a gap population template' },
        [pscustomobject][ordered]@{ id = 'changed-gap-capability'; category = 'gap-exercise'; operation = 'change a gap missing capability' },
        [pscustomobject][ordered]@{ id = 'changed-gap-scope'; category = 'gap-exercise'; operation = 'change a gap scope' },
        [pscustomobject][ordered]@{ id = 'wrong-coverage-case'; category = 'coverage-exercise'; operation = 'replace a zero-coverage case with a nonexistent case' },
        [pscustomobject][ordered]@{ id = 'wrong-transition-rule'; category = 'transition'; operation = 'replace a transition rule with a rule outside the trace' },
        [pscustomobject][ordered]@{ id = 'wrong-transition-case'; category = 'transition'; operation = 'replace a transition case with a nonexistent case' },
        [pscustomobject][ordered]@{ id = 'nonexistent-partial-rule-case'; category = 'higher-order'; operation = 'replace a partial RACE/DATA rule case with a nonexistent case' },
        [pscustomobject][ordered]@{ id = 'category-coverage-drift'; category = 'category-coverage'; operation = 'change a category coverage reference' },
        [pscustomobject][ordered]@{ id = 'missing-existing-mutation-id'; category = 'existing-mutation-registry'; operation = 'remove one retained authorability mutation ID' },
        [pscustomobject][ordered]@{ id = 'extra-existing-mutation-id'; category = 'existing-mutation-registry'; operation = 'add an unknown retained authorability mutation ID' },
        [pscustomobject][ordered]@{ id = 'summary-count-drift'; category = 'summary'; operation = 'change a derived coverage summary count' },
        [pscustomobject][ordered]@{ id = 'weakened-forbidden-registry'; category = 'answer-isolation'; operation = 'remove expected_facts from the registry and add that answer-bearing property' },
        [pscustomobject][ordered]@{ id = 'simultaneous-multi-surface-corruption'; category = 'multi-surface'; operation = 'corrupt constructor, state-class, disposition, lexical, gap, coverage, transition, and higher-order claims together' }
    )
}

function New-CoverageArtifact([object]$Model, [object]$Truth, [string]$ModelHash) {
    $selections = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new([System.StringComparer]::Ordinal)
    $matchedNegatives = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)

    foreach ($family in @($Model.fact_families)) {
        $familyName = [string]$family.family
        $records = @($Truth.family_records[$familyName])
        foreach ($record in $records) {
            if ([string]$record.classification -ceq 'admitted') {
                Add-SelectionReason $selections ([string]$record.global_key) 'admitted-state'
            }
        }
        $constraintIds = @(
            @($family.state_space.admitted_regions) | ForEach-Object { [string]$_.constraint_id }
            @($family.state_space.invalid_regions) | ForEach-Object { [string]$_.constraint_id }
            @($family.state_space.excluded_regions) | ForEach-Object { [string]$_.constraint_id }
        )
        foreach ($constraintId in $constraintIds) {
            $key = $Truth.constraint_representatives[[string]$constraintId]
            Add-SelectionReason $selections $key 'constraint-representative'
        }

        [string[]]$dimensions = @($family.dimensions_used | ForEach-Object { [string]$_ })
        $admitted = @($records | Where-Object { [string]$_.classification -ceq 'admitted' })
        $negative = @($records | Where-Object { [string]$_.classification -cne 'admitted' })
        foreach ($positive in $admitted) {
            $best = $null
            $bestDistance = [int]::MaxValue
            foreach ($candidate in $negative) {
                $distance = Get-HammingDistance $positive.dimensions $candidate.dimensions $dimensions
                if ($distance -lt $bestDistance -or
                    ($distance -eq $bestDistance -and $null -ne $best -and [System.StringComparer]::Ordinal.Compare([string]$candidate.state_key, [string]$best.state_key) -lt 0)) {
                    $best = $candidate
                    $bestDistance = $distance
                }
            }
            if ($null -eq $best) { throw "Protocol /4 WP3 state coverage failed: '$familyName' has no matched negative for admitted state '$($positive.state_key)'" }
            $matchedNegatives[[string]$positive.global_key] = [string]$best.global_key
            Add-SelectionReason $selections ([string]$best.global_key) 'matched-negative'
        }

        $uncoveredPairs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($record in $records) {
            foreach ($token in @($record.pair_tokens)) { [void]$uncoveredPairs.Add([string]$token) }
        }
        foreach ($record in $records) {
            if ($selections.ContainsKey([string]$record.global_key)) {
                foreach ($token in @($record.pair_tokens)) { [void]$uncoveredPairs.Remove([string]$token) }
            }
        }
        while ($uncoveredPairs.Count -gt 0) {
            $best = $null
            $bestCount = -1
            foreach ($candidate in $records) {
                if ($selections.ContainsKey([string]$candidate.global_key)) { continue }
                $count = 0
                foreach ($token in @($candidate.pair_tokens)) {
                    if ($uncoveredPairs.Contains([string]$token)) { $count++ }
                }
                if ($count -gt $bestCount -or
                    ($count -eq $bestCount -and $count -gt 0 -and $null -ne $best -and [System.StringComparer]::Ordinal.Compare([string]$candidate.state_key, [string]$best.state_key) -lt 0)) {
                    $best = $candidate
                    $bestCount = $count
                }
            }
            if ($null -eq $best -or $bestCount -le 0) {
                throw "Protocol /4 WP3 state coverage failed: pairwise selection stalled for '$familyName' with $($uncoveredPairs.Count) uncovered pairs"
            }
            Add-SelectionReason $selections ([string]$best.global_key) 'pairwise'
            foreach ($token in @($best.pair_tokens)) { [void]$uncoveredPairs.Remove([string]$token) }
        }
    }

    $caseIdByGlobalKey = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    foreach ($family in @($Model.fact_families)) {
        $familyName = [string]$family.family
        [string[]]$keys = @($Truth.family_records[$familyName] | Where-Object { $selections.ContainsKey([string]$_.global_key) } | ForEach-Object { [string]$_.state_key })
        [Array]::Sort($keys, [System.StringComparer]::Ordinal)
        for ($index = 0; $index -lt $keys.Count; $index++) {
            $globalKey = "$familyName||$($keys[$index])"
            $caseIdByGlobalKey[$globalKey] = ('P4-WP3-STATE-{0}-{1:D4}' -f (Get-FamilySlug $familyName), ($index + 1))
        }
    }

    $stateCases = [System.Collections.Generic.List[object]]::new()
    foreach ($family in @($Model.fact_families)) {
        $familyName = [string]$family.family
        [string[]]$keys = @($Truth.family_records[$familyName] | Where-Object { $selections.ContainsKey([string]$_.global_key) } | ForEach-Object { [string]$_.state_key })
        [Array]::Sort($keys, [System.StringComparer]::Ordinal)
        foreach ($stateKey in $keys) {
            $globalKey = "$familyName||$stateKey"
            $record = $Truth.raw_by_global_key[$globalKey]
            $matched = $null
            if ($matchedNegatives.ContainsKey($globalKey)) { $matched = $caseIdByGlobalKey[$matchedNegatives[$globalKey]] }
            $stateCases.Add([pscustomobject][ordered]@{
                case_id = $caseIdByGlobalKey[$globalKey]
                family = $familyName
                classification = [string]$record.classification
                constraint_id = [string]$record.constraint_id
                state_class = $record.state_class
                rule_id = $record.rule_id
                atomic_boundary = $record.atomic_boundary
                dimensions = $record.dimensions
                selection_reasons = @(Get-OrdinalStrings @($selections[$globalKey]))
                matched_negative_case_id = $matched
            })
        }
    }

    $caseById = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    foreach ($case in $stateCases) { $caseById.Add([string]$case.case_id, $case) }

    $ruleMappings = [System.Collections.Generic.List[object]]::new()
    $ruleCaseIds = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    foreach ($family in @($Model.fact_families)) {
        foreach ($rule in @($family.rules)) {
            $ids = @(Get-OrdinalStrings @($stateCases | Where-Object {
                [string]$_.family -ceq [string]$family.family -and (Get-ConditionMatches $_.dimensions @($rule.when))
            } | ForEach-Object { [string]$_.case_id }))
            if ($ids.Count -eq 0) { throw "Protocol /4 WP3 state coverage failed: publication rule '$($rule.rule_id)' has no generated exercise" }
            $ruleCaseIds[[string]$rule.rule_id] = $ids
            $ruleMappings.Add([pscustomobject][ordered]@{ rule_id = [string]$rule.rule_id; family = [string]$family.family; case_ids = $ids })
        }
    }

    $constraintMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($family in @($Model.fact_families)) {
        foreach ($group in @(
            [pscustomobject]@{ name = 'admitted'; values = @($family.state_space.admitted_regions) },
            [pscustomobject]@{ name = 'invalid'; values = @($family.state_space.invalid_regions) },
            [pscustomobject]@{ name = 'excluded'; values = @($family.state_space.excluded_regions) })) {
            foreach ($constraint in @($group.values)) {
                $globalKey = $Truth.constraint_representatives[[string]$constraint.constraint_id]
                $stateClass = $null
                if (Test-HasProperty $constraint 'state_class') { $stateClass = [string]$constraint.state_class }
                $boundary = $null
                if (Test-HasProperty $constraint 'atomic_boundary') { $boundary = [string]$constraint.atomic_boundary }
                $constraintMappings.Add([pscustomobject][ordered]@{
                    constraint_id = [string]$constraint.constraint_id
                    family = [string]$family.family
                    classification = [string]$group.name
                    state_class = $stateClass
                    atomic_boundary = $boundary
                    state_count = [int]$Truth.constraint_counts[[string]$constraint.constraint_id]
                    representative_case_id = $caseIdByGlobalKey[$globalKey]
                })
            }
        }
    }

    $admittedMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($case in $stateCases) {
        if ([string]$case.classification -ceq 'admitted') {
            $admittedMappings.Add([pscustomobject][ordered]@{
                family = [string]$case.family
                state_key = Get-StateKey $case.dimensions @((@($Model.fact_families | Where-Object { [string]$_.family -ceq [string]$case.family }))[0].dimensions_used)
                constraint_id = [string]$case.constraint_id
                rule_id = [string]$case.rule_id
                case_id = [string]$case.case_id
                matched_negative_case_id = [string]$case.matched_negative_case_id
            })
        }
    }

    $pairMappings = [System.Collections.Generic.List[object]]::new()
    $pairOwner = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    foreach ($case in $stateCases) {
        $family = (@($Model.fact_families | Where-Object { [string]$_.family -ceq [string]$case.family }))[0]
        foreach ($token in @(Get-PairTokens -Family ([string]$case.family) -State $case.dimensions -Dimensions @($family.dimensions_used))) {
            if (-not $pairOwner.ContainsKey([string]$token)) { $pairOwner[[string]$token] = [string]$case.case_id }
        }
    }
    [string[]]$pairTokens = @($pairOwner.Keys)
    [Array]::Sort($pairTokens, [System.StringComparer]::Ordinal)
    foreach ($token in $pairTokens) {
        $pairMappings.Add([pscustomobject][ordered]@{ pair = $token; case_id = $pairOwner[$token] })
    }

    $constructorMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($family in @($Model.fact_families)) {
        foreach ($constructor in @($family.constructor_groups)) {
            $ids = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
            foreach ($rule in @($family.rules)) {
                $uses = $false
                foreach ($outcome in @($rule.outcomes)) {
                    if (@($outcome.constructor_groups) -ccontains [string]$constructor.id) { $uses = $true }
                }
                if ($uses) { foreach ($caseId in @($ruleCaseIds[[string]$rule.rule_id])) { [void]$ids.Add([string]$caseId) } }
            }
            $constructorMappings.Add([pscustomobject][ordered]@{ constructor_id = [string]$constructor.id; family = [string]$family.family; case_ids = @(Get-OrdinalStrings @($ids)) })
        }
    }

    $stateClassMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($stateClass in @($Model.state_classes)) {
        $ids = @($stateCases | Where-Object {
            [string]$_.state_class -ceq [string]$stateClass.id -or
            ([string]$stateClass.id -ceq 'terminal-rejection' -and [string]$_.classification -ceq 'invalid')
        } | ForEach-Object { [string]$_.case_id })
        $stateClassMappings.Add([pscustomobject][ordered]@{ state_class = [string]$stateClass.id; case_ids = @(Get-OrdinalStrings $ids) })
    }

    $dispositions = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new([System.StringComparer]::Ordinal)
    foreach ($family in @($Model.fact_families)) {
        foreach ($rule in @($family.rules)) {
            foreach ($outcome in @($rule.outcomes)) {
                $name = [string]$outcome.disposition
                if (-not $dispositions.ContainsKey($name)) { $dispositions[$name] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal) }
                foreach ($caseId in @($ruleCaseIds[[string]$rule.rule_id])) { [void]$dispositions[$name].Add([string]$caseId) }
            }
        }
    }
    $dispositionMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($name in @(Get-OrdinalStrings @($dispositions.Keys))) {
        $dispositionMappings.Add([pscustomobject][ordered]@{ disposition = $name; case_ids = @(Get-OrdinalStrings @($dispositions[$name])) })
    }

    $lexicalInputs = Get-LexicalInputTable
    $lexicalCases = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt @($Model.normalization_rules).Count; $index++) {
        $rule = $Model.normalization_rules[$index]
        $ruleId = [string]$rule.rule_id
        if (-not $lexicalInputs.Contains($ruleId)) { throw "Protocol /4 WP3 state coverage failed: normalization rule '$ruleId' has no generic lexical exercise" }
        $lexicalCases.Add([pscustomobject][ordered]@{
            case_id = ('P4-WP3-LEX-{0:D2}' -f ($index + 1))
            rule_id = $ruleId
            atomic_boundary = [string]$rule.atomic_boundary
            generic_inputs = $lexicalInputs[$ruleId]
        })
    }

    $gapExercises = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt @($Model.gap_rules).Count; $index++) {
        $gap = $Model.gap_rules[$index]
        $bindings = [ordered]@{}
        if ([string]$gap.population_template -like '*{signature_lower}*') { $bindings['signature_lower'] = 'genr' }
        if ([string]$gap.population_template -like '*{field_lower}*') { $bindings['field_lower'] = 'data' }
        $gapExercises.Add([pscustomobject][ordered]@{
            case_id = ('P4-WP3-GAP-{0:D2}' -f ($index + 1))
            gap_rule_id = [string]$gap.rule_id
            population_template = [string]$gap.population_template
            missing_capability = [string]$gap.missing_capability
            scope = [string]$gap.scope
            generic_bindings = $bindings
        })
    }

    function First-RuleCase([string]$RuleId) {
        if (-not $ruleCaseIds.ContainsKey($RuleId)) { return $null }
        return @($ruleCaseIds[$RuleId])[0]
    }

    $coverageExercises = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt @($Model.coverage_registry).Count; $index++) {
        $coverage = $Model.coverage_registry[$index]
        $coverageExercises.Add([pscustomobject][ordered]@{
            case_id = ('P4-WP3-COVERAGE-{0:D2}' -f ($index + 1))
            population = [string]$coverage.population
            zero_case_id = First-RuleCase 'P4-COVERAGE-ZERO'
            complete_case_id = First-RuleCase 'P4-COVERAGE-COMPLETE'
            incomplete_case_id = First-RuleCase 'P4-COVERAGE-INCOMPLETE'
            no_snapshot_case_id = First-RuleCase 'P4-COVERAGE-NO-SNAPSHOT'
            invalid_case_id = First-RuleCase 'P4-COVERAGE-INVALID'
        })
    }

    $transitionMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($trace in @($Model.manual_traces)) {
        $bindings = [System.Collections.Generic.List[object]]::new()
        foreach ($ruleId in @($trace.rules)) {
            $bindings.Add([pscustomobject][ordered]@{ rule_id = [string]$ruleId; case_id = First-RuleCase ([string]$ruleId) })
        }
        $transitionMappings.Add([pscustomobject][ordered]@{ trace_id = [string]$trace.trace_id; boundary = [string]$trace.boundary; rule_cases = $bindings.ToArray() })
    }

    $partialRules = @($Model.manual_traces | Where-Object { [string]$_.trace_id -ceq 'TRACE-PARTIAL-RACE-DATA' } | ForEach-Object { $_.rules })
    $partialBindings = [System.Collections.Generic.List[object]]::new()
    foreach ($ruleId in $partialRules) { $partialBindings.Add([pscustomobject][ordered]@{ rule_id = [string]$ruleId; case_id = First-RuleCase ([string]$ruleId) }) }
    $higherOrderCases = @([pscustomobject][ordered]@{
        case_id = 'P4-WP3-HIGHER-ORDER-PARTIAL-RACE-DATA'
        invariant_id = [string]$Model.cross_family_invariants.partial_race_data.invariant_id
        trace_id = 'TRACE-PARTIAL-RACE-DATA'
        rule_cases = $partialBindings.ToArray()
        gap_owner_ids = @([string]$Model.cross_family_invariants.partial_race_data.gap.owner_id)
        obligations = Copy-JsonObject $Model.cross_family_invariants.partial_race_data
    })

    $atomicMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($boundary in @($Model.atomic_boundaries)) {
        $stateIds = @($stateCases | Where-Object { [string]$_.atomic_boundary -ceq [string]$boundary.id } | ForEach-Object { [string]$_.case_id })
        $lexIds = @($lexicalCases | Where-Object { [string]$_.atomic_boundary -ceq [string]$boundary.id } | ForEach-Object { [string]$_.case_id })
        $atomicMappings.Add([pscustomobject][ordered]@{ atomic_boundary = [string]$boundary.id; state_case_ids = @(Get-OrdinalStrings $stateIds); lexical_case_ids = @(Get-OrdinalStrings $lexIds) })
    }

    $familyMappings = [System.Collections.Generic.List[object]]::new()
    foreach ($family in @($Model.fact_families)) {
        $ids = @($stateCases | Where-Object { [string]$_.family -ceq [string]$family.family } | ForEach-Object { [string]$_.case_id })
        $familyMappings.Add([pscustomobject][ordered]@{ family = [string]$family.family; case_ids = @(Get-OrdinalStrings $ids) })
    }

    $categoryCoverage = [ordered]@{
        positive_and_matched_negative = [ordered]@{ admitted_case_count = @($admittedMappings).Count; mapping = 'admitted_state_mappings' }
        observed_versus_not_observed = [ordered]@{ dimensions = @('occurrence_state=observed-exact', 'occurrence_state=not-observed'); mapping = 'pairwise_mappings' }
        supported_versus_unsupported_shape = [ordered]@{ dimensions = @('shape_support=supported', 'shape_support=unsupported'); mapping = 'pairwise_mappings' }
        decoded_null_unresolved_unknown_omission_rejection = [ordered]@{ state_classes = @('decoded-null', 'decoded-unresolved'); dispositions = @('exact_value', 'accepted_unknown', 'omit', 'terminal_rejection'); mappings = @('state_class_mappings', 'disposition_mappings') }
        missing_versus_present = [ordered]@{ dimensions = @('member_presence=not-observed', 'member_presence=present-once'); mapping = 'pairwise_mappings' }
        supported_versus_unsupported_capabilities = [ordered]@{ dimensions = @('archive_capability=supported', 'archive_capability=unsupported', 'localization_capability=supported', 'localization_capability=unsupported', 'discovery_state=completed', 'discovery_state=unavailable'); mapping = 'pairwise_mappings' }
        coverage_complete_incomplete_zero = [ordered]@{ mapping = 'coverage_exercises' }
        singular_versus_duplicate_gap_ownership = [ordered]@{ positive = 'P4-WP3-HIGHER-ORDER-PARTIAL-RACE-DATA'; negative_mutation = 'duplicate-gap-owner' }
        duplicate_fact_and_taxonomy_identity = [ordered]@{ existing_mutations = @('duplicate_fact_id', 'duplicate_taxonomy_tuple') }
        aggregation_and_ordering = [ordered]@{ lexical_cases = @('P4-WP3-LEX-03', 'P4-WP3-LEX-10'); negative_mutation = 'unstable-case-order' }
        pairwise_interactions = [ordered]@{ mapping = 'pairwise_mappings'; required_pair_count = @($pairMappings).Count }
        exact_partial_race_data = [ordered]@{ higher_order_case = 'P4-WP3-HIGHER-ORDER-PARTIAL-RACE-DATA' }
    }

    $existingMutations = @((Read-Json (Join-Path $scriptRoot '..\docs\evaluation\fixtures\protocol-4-oracle-authorability\coverage-ledger.json') 'existing authorability coverage ledger').mutations | ForEach-Object { [string]$_ })
    $familyProofs = [System.Collections.Generic.List[object]]::new()
    foreach ($summary in @($Truth.family_summaries)) {
        $familyProofs.Add([pscustomobject][ordered]@{
            family = [string]$summary.family
            raw = [int]$summary.raw
            admitted = [int]$summary.admitted
            excluded = [int]$summary.excluded
            invalid = [int]$summary.invalid
            uncovered = [int]$summary.uncovered
            overlap = [int]$summary.overlap
            state_digest_sha256 = [string]$summary.state_digest_sha256
        })
    }
    $orderedStateCases = [System.Collections.Generic.List[object]]::new()
    foreach ($caseId in @(Get-OrdinalStrings @($caseById.Keys))) {
        $orderedStateCases.Add($caseById[$caseId])
    }

    return [pscustomobject][ordered]@{
        '$schema' = 'generated-state-coverage.schema.json'
        schema_id = 'infinium.evaluation.protocol-4-model-derived-state-coverage/v1'
        artifact_version = '1.1.0'
        status = 'answer-free-generated-coverage'
        work_id = 'M1/S4.5/PRE-B2/WP3'
        source_model = [pscustomobject][ordered]@{
            model_id = [string]$Model.model_id
            model_version = [string]$Model.version
            model_status = [string]$Model.status
            sha256 = $ModelHash
            protocol_id = [string]$Model.protocol.protocol_id
            projection_version = [string]$Model.protocol.projection_version
        }
        generation = [pscustomobject][ordered]@{
            generator = 'eng/generate-m1-slice4-protocol4-state-coverage.ps1'
            generator_version = '1.1.0'
            deterministic = $true
            answer_bearing_outputs_tracked = $false
            selection_strategy = 'all admitted states; one representative per admitted, invalid, and excluded region; nearest matched negative per admitted state; deterministic greedy pairwise completion'
        }
        state_space_proof = [pscustomobject][ordered]@{
            totals = $Truth.totals
            aggregate_state_digest_sha256 = [string]$Truth.aggregate_state_digest_sha256
            families = $familyProofs.ToArray()
        }
        state_cases = $orderedStateCases.ToArray()
        admitted_state_mappings = $admittedMappings.ToArray()
        constraint_mappings = $constraintMappings.ToArray()
        pairwise_mappings = $pairMappings.ToArray()
        family_mappings = $familyMappings.ToArray()
        state_class_mappings = $stateClassMappings.ToArray()
        rule_mappings = $ruleMappings.ToArray()
        disposition_mappings = $dispositionMappings.ToArray()
        constructor_mappings = $constructorMappings.ToArray()
        atomic_boundary_mappings = $atomicMappings.ToArray()
        lexical_cases = $lexicalCases.ToArray()
        gap_exercises = $gapExercises.ToArray()
        coverage_exercises = $coverageExercises.ToArray()
        transition_mappings = $transitionMappings.ToArray()
        higher_order_cases = $higherOrderCases
        category_coverage = [pscustomobject]$categoryCoverage
        existing_authorability_mutations = $existingMutations
        wp3_mutations = @(Get-Wp3Mutations)
        answer_isolation = [pscustomobject][ordered]@{
            forbidden_properties = @($script:ForbiddenArtifactProperties)
            expected_facts_location = 'ignored work/ only'
            product_or_candidate_source = 'prohibited'
        }
        summary = [pscustomobject][ordered]@{
            state_case_count = $stateCases.Count
            admitted_state_case_count = @($stateCases | Where-Object { [string]$_.classification -ceq 'admitted' }).Count
            invalid_state_case_count = @($stateCases | Where-Object { [string]$_.classification -ceq 'invalid' }).Count
            excluded_state_case_count = @($stateCases | Where-Object { [string]$_.classification -ceq 'excluded' }).Count
            matched_negative_count = $matchedNegatives.Count
            constraint_mapping_count = $constraintMappings.Count
            pairwise_mapping_count = $pairMappings.Count
            family_mapping_count = $familyMappings.Count
            state_class_mapping_count = $stateClassMappings.Count
            rule_mapping_count = $ruleMappings.Count
            disposition_mapping_count = $dispositionMappings.Count
            constructor_mapping_count = $constructorMappings.Count
            atomic_boundary_mapping_count = $atomicMappings.Count
            lexical_case_count = $lexicalCases.Count
            gap_exercise_count = $gapExercises.Count
            coverage_exercise_count = $coverageExercises.Count
            transition_mapping_count = $transitionMappings.Count
            higher_order_case_count = @($higherOrderCases).Count
            existing_mutation_count = $existingMutations.Count
            wp3_mutation_count = @(Get-Wp3Mutations).Count
            uncovered_required_obligations = 0
        }
    }
}

function Get-ForbiddenPropertyHits([object]$Node, [string[]]$Forbidden, [string]$Path = '$') {
    $hits = [System.Collections.Generic.List[string]]::new()
    if ($null -eq $Node) { return $hits }
    if ($Node -is [System.Collections.IDictionary]) {
        foreach ($key in $Node.Keys) {
            $name = [string]$key
            if ($Forbidden -ccontains $name) { $hits.Add("$Path.$name") }
            foreach ($hit in Get-ForbiddenPropertyHits $Node[$key] $Forbidden "$Path.$name") { $hits.Add($hit) }
        }
        return $hits
    }
    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Node.PSObject.Properties) {
            if ($Forbidden -ccontains $property.Name) { $hits.Add("$Path.$($property.Name)") }
            foreach ($hit in Get-ForbiddenPropertyHits $property.Value $Forbidden "$Path.$($property.Name)") { $hits.Add($hit) }
        }
        return $hits
    }
    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [string]) {
        $index = 0
        foreach ($item in $Node) {
            foreach ($hit in Get-ForbiddenPropertyHits $item $Forbidden "$Path[$index]") { $hits.Add($hit) }
            $index++
        }
    }
    return $hits
}

function Invoke-ArtifactValidation([object]$Model, [object]$Truth, [object]$Schema, [object]$Artifact, [object]$Expected, [string]$ModelHash) {
    $issues = [System.Collections.Generic.List[string]]::new()
    $coverageDrift = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($required in @($Schema.required)) {
        if (-not (Test-HasProperty $Artifact ([string]$required))) { Add-Issue $issues "artifact is missing schema-required property '$required'" }
    }
    if ([string]$Artifact.schema_id -cne 'infinium.evaluation.protocol-4-model-derived-state-coverage/v1') { Add-Issue $issues 'artifact schema identity drifted' }
    if ([string]$Artifact.source_model.sha256 -cne $ModelHash) { Add-Issue $issues 'source model hash drifted' }
    if ([string]$Artifact.source_model.model_status -cne 'proposed') { Add-Issue $issues 'source model must remain proposed until WP4' }
    if ([string]$Artifact.source_model.protocol_id -cne 'infinium.evaluator-v2/4') { Add-Issue $issues 'protocol identity drifted' }

    $declaredForbidden = @($Artifact.answer_isolation.forbidden_properties | ForEach-Object { [string]$_ })
    if (-not (Test-SameStringSet $declaredForbidden $script:ForbiddenArtifactProperties) -or -not (Test-JsonEquivalent $declaredForbidden $script:ForbiddenArtifactProperties)) {
        Add-Issue $issues 'answer-isolation forbidden-property registry drifted from the fixed validator registry'
    }
    $hits = @(Get-ForbiddenPropertyHits $Artifact $script:ForbiddenArtifactProperties)
    if ($hits.Count -gt 0) { Add-Issue $issues "answer-bearing property found at $($hits[0])" }

    $stateCases = @($Artifact.state_cases)
    $caseIds = @($stateCases | ForEach-Object { [string]$_.case_id })
    if (-not (Test-OrdinalSequence $caseIds)) { Add-Issue $issues 'state cases are not in stable ordinal case-ID order' }
    $caseById = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    foreach ($case in $stateCases) {
        if ($caseById.ContainsKey([string]$case.case_id)) { Add-Issue $issues "duplicate generated case ID '$($case.case_id)'"; continue }
        $caseById[[string]$case.case_id] = $case
        $family = @($Model.fact_families | Where-Object { [string]$_.family -ceq [string]$case.family })
        if ($family.Count -ne 1) { Add-Issue $issues "case '$($case.case_id)' references unknown family"; continue }
        $stateKey = Get-StateKey $case.dimensions @($family[0].dimensions_used)
        $globalKey = "$($case.family)||$stateKey"
        if (-not $Truth.raw_by_global_key.ContainsKey($globalKey)) { Add-Issue $issues "case '$($case.case_id)' references an unknown raw state"; continue }
        $truthRecord = $Truth.raw_by_global_key[$globalKey]
        if ([string]$case.classification -cne [string]$truthRecord.classification -or [string]$case.constraint_id -cne [string]$truthRecord.constraint_id -or [string]$case.rule_id -cne [string]$truthRecord.rule_id) {
            Add-Issue $issues "case '$($case.case_id)' makes a false state or rule coverage claim"
        }
    }
    if (-not (Test-JsonEquivalent $stateCases @($Expected.state_cases))) { [void]$coverageDrift.Add('state_cases'); Add-Issue $issues 'generated state-case inventory drifted from recomputed model coverage' }

    if ([int]$Artifact.state_space_proof.totals.raw -ne [int]$Truth.totals.raw -or
        [int]$Artifact.state_space_proof.totals.admitted -ne [int]$Truth.totals.admitted -or
        [int]$Artifact.state_space_proof.totals.excluded -ne [int]$Truth.totals.excluded -or
        [int]$Artifact.state_space_proof.totals.invalid -ne [int]$Truth.totals.invalid -or
        [int]$Artifact.state_space_proof.totals.uncovered -ne 0 -or [int]$Artifact.state_space_proof.totals.overlap -ne 0) {
        Add-Issue $issues 'raw state-space totals do not match the model'
    }
    if ([string]$Artifact.state_space_proof.aggregate_state_digest_sha256 -cne [string]$Truth.aggregate_state_digest_sha256) { Add-Issue $issues 'aggregate state classification digest drifted' }
    if (-not (Test-JsonEquivalent $Artifact.state_space_proof $Expected.state_space_proof)) { [void]$coverageDrift.Add('state_space_proof'); Add-Issue $issues 'state-space proof drifted from reconstructed truth' }

    $admittedExpected = @($Truth.raw_by_global_key.Values | Where-Object { [string]$_.classification -ceq 'admitted' })
    $admittedMappings = @($Artifact.admitted_state_mappings)
    if ($admittedMappings.Count -ne $admittedExpected.Count) { Add-Issue $issues 'admitted state mapping count is incomplete' }
    $seenAdmitted = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($mapping in $admittedMappings) {
        $globalKey = "$($mapping.family)||$($mapping.state_key)"
        if (-not $seenAdmitted.Add($globalKey)) { Add-Issue $issues "duplicate admitted state mapping '$globalKey'" }
        if (-not $Truth.raw_by_global_key.ContainsKey($globalKey) -or [string]$Truth.raw_by_global_key[$globalKey].classification -cne 'admitted') { Add-Issue $issues "admitted mapping '$globalKey' is not an admitted model state" }
        if (-not $caseById.ContainsKey([string]$mapping.case_id)) { Add-Issue $issues "admitted mapping references unknown case '$($mapping.case_id)'" }
        if (-not $caseById.ContainsKey([string]$mapping.matched_negative_case_id)) { Add-Issue $issues "matched negative references unknown case '$($mapping.matched_negative_case_id)'" }
        elseif ([string]$caseById[[string]$mapping.matched_negative_case_id].classification -ceq 'admitted') { Add-Issue $issues "matched negative for '$($mapping.case_id)' is admitted" }
    }
    if (-not (Test-JsonEquivalent $admittedMappings @($Expected.admitted_state_mappings))) { [void]$coverageDrift.Add('admitted_state_mappings'); Add-Issue $issues 'admitted-state mappings drifted from recomputed coverage' }

    $expectedConstraintIds = @($Model.fact_families | ForEach-Object { @($_.state_space.admitted_regions) + @($_.state_space.invalid_regions) + @($_.state_space.excluded_regions) } | ForEach-Object { [string]$_.constraint_id })
    $constraintMappings = @($Artifact.constraint_mappings)
    if (-not (Test-SameStringSet @($constraintMappings.constraint_id) $expectedConstraintIds)) { Add-Issue $issues 'constraint mapping inventory is incomplete' }
    foreach ($mapping in $constraintMappings) {
        if (-not $caseById.ContainsKey([string]$mapping.representative_case_id)) { Add-Issue $issues "constraint '$($mapping.constraint_id)' references unknown case"; continue }
        $case = $caseById[[string]$mapping.representative_case_id]
        if ([string]$case.constraint_id -cne [string]$mapping.constraint_id -or [int]$mapping.state_count -ne [int]$Truth.constraint_counts[[string]$mapping.constraint_id]) { Add-Issue $issues "constraint '$($mapping.constraint_id)' has a false representative or state count" }
    }
    if (-not (Test-JsonEquivalent $constraintMappings @($Expected.constraint_mappings))) { [void]$coverageDrift.Add('constraint_mappings'); Add-Issue $issues 'constraint mappings drifted from recomputed coverage' }

    $expectedPairs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($family in @($Model.fact_families)) { foreach ($record in @($Truth.family_records[[string]$family.family])) { foreach ($token in @($record.pair_tokens)) { [void]$expectedPairs.Add([string]$token) } } }
    $pairMappings = @($Artifact.pairwise_mappings)
    if (-not (Test-SameStringSet @($pairMappings.pair) @($expectedPairs))) { Add-Issue $issues 'pairwise mapping inventory is incomplete' }
    foreach ($mapping in $pairMappings) {
        if (-not $caseById.ContainsKey([string]$mapping.case_id)) { Add-Issue $issues "pairwise mapping references unknown case '$($mapping.case_id)'"; continue }
        $case = $caseById[[string]$mapping.case_id]
        $family = (@($Model.fact_families | Where-Object { [string]$_.family -ceq [string]$case.family }))[0]
        if (@(Get-PairTokens ([string]$case.family) $case.dimensions @($family.dimensions_used)) -cnotcontains [string]$mapping.pair) { Add-Issue $issues "pairwise mapping '$($mapping.pair)' makes a false coverage claim" }
    }
    if (-not (Test-JsonEquivalent $pairMappings @($Expected.pairwise_mappings))) { [void]$coverageDrift.Add('pairwise_mappings'); Add-Issue $issues 'pairwise mappings drifted from recomputed coverage' }

    $familyMappings = @($Artifact.family_mappings)
    if (-not (Test-SameStringSet @($familyMappings.family) @($Model.fact_families.family))) { [void]$coverageDrift.Add('family_mappings'); Add-Issue $issues 'family mapping inventory is incomplete' }
    foreach ($mapping in $familyMappings) {
        $expectedMapping = @(Get-MappingByProperty @($Expected.family_mappings) 'family' ([string]$mapping.family))
        if ($expectedMapping.Count -ne 1 -or -not (Test-SameStringSet @($mapping.case_ids) @($expectedMapping[0].case_ids))) { [void]$coverageDrift.Add('family_mappings'); Add-Issue $issues "family mapping '$($mapping.family)' does not contain exactly its generated cases" }
        foreach ($caseId in @($mapping.case_ids)) {
            if (-not $caseById.ContainsKey([string]$caseId)) { Add-Issue $issues "family mapping '$($mapping.family)' references unknown case '$caseId'" }
            elseif ([string]$caseById[[string]$caseId].family -cne [string]$mapping.family) { Add-Issue $issues "family mapping '$($mapping.family)' references case '$caseId' from another family" }
        }
    }

    $stateClassMappings = @($Artifact.state_class_mappings)
    if (-not (Test-SameStringSet @($stateClassMappings.state_class) @($Model.state_classes.id))) { [void]$coverageDrift.Add('state_class_mappings'); Add-Issue $issues 'state-class mapping inventory is incomplete' }
    foreach ($mapping in $stateClassMappings) {
        $expectedMapping = @(Get-MappingByProperty @($Expected.state_class_mappings) 'state_class' ([string]$mapping.state_class))
        if ($expectedMapping.Count -ne 1 -or -not (Test-SameStringSet @($mapping.case_ids) @($expectedMapping[0].case_ids))) { [void]$coverageDrift.Add('state_class_mappings'); Add-Issue $issues "state-class mapping '$($mapping.state_class)' does not contain exactly its generated cases" }
        foreach ($caseId in @($mapping.case_ids)) {
            if (-not $caseById.ContainsKey([string]$caseId)) { Add-Issue $issues "state-class mapping '$($mapping.state_class)' references unknown case '$caseId'"; continue }
            $case = $caseById[[string]$caseId]
            $supports = [string]$case.state_class -ceq [string]$mapping.state_class -or ([string]$mapping.state_class -ceq 'terminal-rejection' -and [string]$case.classification -ceq 'invalid')
            if (-not $supports) { Add-Issue $issues "state-class mapping '$($mapping.state_class)' references unsupported case '$caseId'" }
        }
    }

    $expectedRules = @($Model.fact_families | ForEach-Object { $_.rules } | ForEach-Object { [string]$_.rule_id })
    if (-not (Test-SameStringSet @($Artifact.rule_mappings.rule_id) $expectedRules)) { Add-Issue $issues 'publication rule mapping inventory is incomplete' }
    foreach ($mapping in @($Artifact.rule_mappings)) {
        $family = (@($Model.fact_families | Where-Object { [string]$_.family -ceq [string]$mapping.family }))[0]
        $rule = (@($family.rules | Where-Object { [string]$_.rule_id -ceq [string]$mapping.rule_id }))[0]
        $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($caseId in @($mapping.case_ids)) {
            if (-not $seen.Add([string]$caseId)) { Add-Issue $issues "rule '$($mapping.rule_id)' has a duplicate case mapping" }
            if (-not $caseById.ContainsKey([string]$caseId)) { Add-Issue $issues "rule '$($mapping.rule_id)' references unknown case '$caseId'" }
            elseif ([string]$caseById[[string]$caseId].family -cne [string]$mapping.family -or -not (Get-ConditionMatches $caseById[[string]$caseId].dimensions @($rule.when))) { Add-Issue $issues "rule '$($mapping.rule_id)' makes a false coverage claim with case '$caseId'" }
        }
    }
    if (-not (Test-JsonEquivalent @($Artifact.rule_mappings) @($Expected.rule_mappings))) { [void]$coverageDrift.Add('rule_mappings'); Add-Issue $issues 'publication rule mappings drifted from recomputed coverage' }

    $dispositionMappings = @($Artifact.disposition_mappings)
    if (-not (Test-SameStringSet @($dispositionMappings.disposition) @($Expected.disposition_mappings.disposition))) { [void]$coverageDrift.Add('disposition_mappings'); Add-Issue $issues 'disposition mapping inventory is incomplete' }
    foreach ($mapping in $dispositionMappings) {
        $expectedMapping = @(Get-MappingByProperty @($Expected.disposition_mappings) 'disposition' ([string]$mapping.disposition))
        if ($expectedMapping.Count -ne 1 -or -not (Test-SameStringSet @($mapping.case_ids) @($expectedMapping[0].case_ids))) { [void]$coverageDrift.Add('disposition_mappings'); Add-Issue $issues "disposition mapping '$($mapping.disposition)' does not contain exactly its rule-matching cases" }
        foreach ($caseId in @($mapping.case_ids)) {
            if (-not $caseById.ContainsKey([string]$caseId)) { Add-Issue $issues "disposition mapping '$($mapping.disposition)' references unknown case '$caseId'"; continue }
            $case = $caseById[[string]$caseId]
            $family = (@($Model.fact_families | Where-Object { [string]$_.family -ceq [string]$case.family }))[0]
            $supports = @($family.rules | Where-Object { (Get-ConditionMatches $case.dimensions @($_.when)) -and @($_.outcomes.disposition) -ccontains [string]$mapping.disposition }).Count -gt 0
            if (-not $supports) { Add-Issue $issues "disposition mapping '$($mapping.disposition)' references unsupported case '$caseId'" }
        }
    }

    $expectedConstructors = @($Model.fact_families | ForEach-Object { $_.constructor_groups } | ForEach-Object { [string]$_.id })
    if (-not (Test-SameStringSet @($Artifact.constructor_mappings.constructor_id) $expectedConstructors)) { [void]$coverageDrift.Add('constructor_mappings'); Add-Issue $issues 'constructor mapping inventory is incomplete' }
    foreach ($mapping in @($Artifact.constructor_mappings)) {
        $expectedMapping = @(Get-MappingByProperty @($Expected.constructor_mappings) 'constructor_id' ([string]$mapping.constructor_id))
        if ($expectedMapping.Count -ne 1 -or -not (Test-SameStringSet @($mapping.case_ids) @($expectedMapping[0].case_ids))) { [void]$coverageDrift.Add('constructor_mappings'); Add-Issue $issues "constructor mapping '$($mapping.constructor_id)' does not contain exactly its rule-matching cases" }
        foreach ($caseId in @($mapping.case_ids)) {
            if (-not $caseById.ContainsKey([string]$caseId)) { Add-Issue $issues "constructor mapping '$($mapping.constructor_id)' references unknown case '$caseId'"; continue }
            $case = $caseById[[string]$caseId]
            $family = (@($Model.fact_families | Where-Object { [string]$_.family -ceq [string]$mapping.family }))[0]
            $supports = [string]$case.family -ceq [string]$mapping.family -and @($family.rules | Where-Object { (Get-ConditionMatches $case.dimensions @($_.when)) -and @($_.outcomes.constructor_groups) -ccontains [string]$mapping.constructor_id }).Count -gt 0
            if (-not $supports) { Add-Issue $issues "constructor mapping '$($mapping.constructor_id)' references unsupported case '$caseId'" }
        }
    }

    $expectedNorm = @($Model.normalization_rules | ForEach-Object { [string]$_.rule_id })
    if (-not (Test-SameStringSet @($Artifact.lexical_cases.rule_id) $expectedNorm)) { [void]$coverageDrift.Add('lexical_cases'); Add-Issue $issues 'normalization-rule lexical coverage is incomplete' }
    foreach ($lexical in @($Artifact.lexical_cases)) {
        $expectedLexical = @(Get-MappingByProperty @($Expected.lexical_cases) 'case_id' ([string]$lexical.case_id))
        if ($expectedLexical.Count -ne 1 -or -not (Test-JsonEquivalent $lexical $expectedLexical[0])) { [void]$coverageDrift.Add('lexical_cases'); Add-Issue $issues "lexical exercise '$($lexical.case_id)' drifted from its stable rule, boundary, or generic inputs" }
    }
    $expectedBoundaries = @($Model.atomic_boundaries | ForEach-Object { [string]$_.id })
    if (-not (Test-SameStringSet @($Artifact.atomic_boundary_mappings.atomic_boundary) $expectedBoundaries)) { [void]$coverageDrift.Add('atomic_boundary_mappings'); Add-Issue $issues 'atomic-boundary coverage is incomplete' }
    foreach ($mapping in @($Artifact.atomic_boundary_mappings)) {
        $expectedMapping = @(Get-MappingByProperty @($Expected.atomic_boundary_mappings) 'atomic_boundary' ([string]$mapping.atomic_boundary))
        if ($expectedMapping.Count -ne 1 -or -not (Test-SameStringSet @($mapping.state_case_ids) @($expectedMapping[0].state_case_ids)) -or -not (Test-SameStringSet @($mapping.lexical_case_ids) @($expectedMapping[0].lexical_case_ids))) { [void]$coverageDrift.Add('atomic_boundary_mappings'); Add-Issue $issues "atomic-boundary mapping '$($mapping.atomic_boundary)' does not contain exactly its exercises" }
        foreach ($caseId in @($mapping.state_case_ids)) {
            if (-not $caseById.ContainsKey([string]$caseId)) { Add-Issue $issues "atomic-boundary mapping '$($mapping.atomic_boundary)' references unknown state case '$caseId'" }
            elseif ([string]$caseById[[string]$caseId].atomic_boundary -cne [string]$mapping.atomic_boundary) { Add-Issue $issues "atomic-boundary mapping '$($mapping.atomic_boundary)' references state case '$caseId' from another boundary" }
        }
        foreach ($caseId in @($mapping.lexical_case_ids)) {
            $lexical = @(Get-MappingByProperty @($Artifact.lexical_cases) 'case_id' ([string]$caseId))
            if ($lexical.Count -ne 1) { Add-Issue $issues "atomic-boundary mapping '$($mapping.atomic_boundary)' references unknown lexical case '$caseId'" }
            elseif ([string]$lexical[0].atomic_boundary -cne [string]$mapping.atomic_boundary) { Add-Issue $issues "atomic-boundary mapping '$($mapping.atomic_boundary)' references lexical case '$caseId' from another boundary" }
        }
    }
    if (-not (Test-SameStringSet @($Artifact.gap_exercises.gap_rule_id) @($Model.gap_rules.rule_id))) { [void]$coverageDrift.Add('gap_exercises'); Add-Issue $issues 'gap-rule exercise inventory is incomplete' }
    foreach ($exercise in @($Artifact.gap_exercises)) {
        $expectedExercise = @(Get-MappingByProperty @($Expected.gap_exercises) 'case_id' ([string]$exercise.case_id))
        if ($expectedExercise.Count -ne 1 -or -not (Test-JsonEquivalent $exercise $expectedExercise[0])) { [void]$coverageDrift.Add('gap_exercises'); Add-Issue $issues "gap exercise '$($exercise.case_id)' drifted from its model rule, population, capability, scope, or bindings" }
    }
    if (-not (Test-SameStringSet @($Artifact.coverage_exercises.population) @($Model.coverage_registry.population))) { [void]$coverageDrift.Add('coverage_exercises'); Add-Issue $issues 'coverage-population exercise inventory is incomplete' }
    $coverageRuleByProperty = [ordered]@{ zero_case_id = 'P4-COVERAGE-ZERO'; complete_case_id = 'P4-COVERAGE-COMPLETE'; incomplete_case_id = 'P4-COVERAGE-INCOMPLETE'; no_snapshot_case_id = 'P4-COVERAGE-NO-SNAPSHOT'; invalid_case_id = 'P4-COVERAGE-INVALID' }
    $coverageFamily = (@($Model.fact_families | Where-Object { [string]$_.family -ceq 'coverage' }))[0]
    foreach ($exercise in @($Artifact.coverage_exercises)) {
        $expectedExercise = @(Get-MappingByProperty @($Expected.coverage_exercises) 'population' ([string]$exercise.population))
        if ($expectedExercise.Count -ne 1 -or -not (Test-JsonEquivalent $exercise $expectedExercise[0])) { [void]$coverageDrift.Add('coverage_exercises'); Add-Issue $issues "coverage exercise '$($exercise.population)' drifted from its intended rule cases" }
        foreach ($property in $coverageRuleByProperty.Keys) {
            $caseId = [string]$exercise.$property
            if (-not $caseById.ContainsKey($caseId)) { Add-Issue $issues "coverage exercise '$($exercise.population)' references unknown $property case '$caseId'"; continue }
            $rule = (@($coverageFamily.rules | Where-Object { [string]$_.rule_id -ceq [string]$coverageRuleByProperty[$property] }))[0]
            if ([string]$caseById[$caseId].family -cne 'coverage' -or -not (Get-ConditionMatches $caseById[$caseId].dimensions @($rule.when))) { Add-Issue $issues "coverage exercise '$($exercise.population)' has a case that does not match $($rule.rule_id)" }
        }
    }
    if (-not (Test-SameStringSet @($Artifact.transition_mappings.trace_id) @($Model.manual_traces.trace_id))) { [void]$coverageDrift.Add('transition_mappings'); Add-Issue $issues 'evidence-layer transition mapping is incomplete' }
    foreach ($transition in @($Artifact.transition_mappings)) {
        $trace = (@($Model.manual_traces | Where-Object { [string]$_.trace_id -ceq [string]$transition.trace_id }))[0]
        $expectedTransition = @(Get-MappingByProperty @($Expected.transition_mappings) 'trace_id' ([string]$transition.trace_id))
        if ($expectedTransition.Count -ne 1 -or -not (Test-JsonEquivalent $transition $expectedTransition[0])) { [void]$coverageDrift.Add('transition_mappings'); Add-Issue $issues "transition mapping '$($transition.trace_id)' drifted from the model trace or rule cases" }
        if ($null -ne $trace -and -not (Test-SameStringSet @($transition.rule_cases.rule_id) @($trace.rules))) { Add-Issue $issues "transition mapping '$($transition.trace_id)' has the wrong rule inventory" }
        foreach ($binding in @($transition.rule_cases)) {
            if (-not $caseById.ContainsKey([string]$binding.case_id)) { Add-Issue $issues "transition mapping '$($transition.trace_id)' references unknown case '$($binding.case_id)'"; continue }
            $case = $caseById[[string]$binding.case_id]
            $family = (@($Model.fact_families | Where-Object { @($_.rules.rule_id) -ccontains [string]$binding.rule_id }))[0]
            $rule = (@($family.rules | Where-Object { [string]$_.rule_id -ceq [string]$binding.rule_id }))[0]
            if ($null -eq $rule -or [string]$case.family -cne [string]$family.family -or -not (Get-ConditionMatches $case.dimensions @($rule.when))) { Add-Issue $issues "transition mapping '$($transition.trace_id)' has invalid rule/case binding '$($binding.rule_id)'/'$($binding.case_id)'" }
        }
    }

    $partial = @($Artifact.higher_order_cases | Where-Object { [string]$_.invariant_id -ceq 'INV-PARTIAL-RACE-DATA' })
    $requiredPartialRules = @($Model.manual_traces | Where-Object { [string]$_.trace_id -ceq 'TRACE-PARTIAL-RACE-DATA' } | ForEach-Object { $_.rules })
    if ($partial.Count -ne 1 -or -not (Test-SameStringSet @($partial[0].rule_cases.rule_id) $requiredPartialRules)) { [void]$coverageDrift.Add('higher_order_cases'); Add-Issue $issues 'partial RACE/DATA higher-order rule coverage is incomplete' }
    elseif (@($partial[0].rule_cases | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.case_id) }).Count -gt 0) { Add-Issue $issues 'partial RACE/DATA higher-order case has an unmapped rule' }
    if ($partial.Count -eq 1) {
        foreach ($binding in @($partial[0].rule_cases)) {
            if (-not $caseById.ContainsKey([string]$binding.case_id)) { Add-Issue $issues "partial RACE/DATA higher-order mapping references unknown case '$($binding.case_id)'"; continue }
            $case = $caseById[[string]$binding.case_id]
            $family = (@($Model.fact_families | Where-Object { @($_.rules.rule_id) -ccontains [string]$binding.rule_id }))[0]
            $rule = (@($family.rules | Where-Object { [string]$_.rule_id -ceq [string]$binding.rule_id }))[0]
            if ($null -eq $rule -or [string]$case.family -cne [string]$family.family -or -not (Get-ConditionMatches $case.dimensions @($rule.when))) { Add-Issue $issues "partial RACE/DATA higher-order mapping has invalid rule/case binding '$($binding.rule_id)'/'$($binding.case_id)'" }
        }
        if (@($partial[0].gap_owner_ids).Count -ne 1 -or [string]$partial[0].gap_owner_ids[0] -cne [string]$Model.cross_family_invariants.partial_race_data.gap.owner_id) { Add-Issue $issues 'partial RACE/DATA gap ownership is not singular' }
        if (($partial[0].obligations | ConvertTo-Json -Depth 30 -Compress) -cne ($Model.cross_family_invariants.partial_race_data | ConvertTo-Json -Depth 30 -Compress)) { Add-Issue $issues 'partial RACE/DATA obligation mapping drifted from the model' }
        if (-not (Test-JsonEquivalent $partial[0] $Expected.higher_order_cases[0])) { [void]$coverageDrift.Add('higher_order_cases'); Add-Issue $issues 'partial RACE/DATA higher-order mapping drifted from recomputed coverage' }
    }

    if (-not (Test-JsonEquivalent $Artifact.category_coverage $Expected.category_coverage)) { [void]$coverageDrift.Add('category_coverage'); Add-Issue $issues 'category coverage drifted from recomputed mapping, case, mutation, or dimension/value references' }
    $categoryMappingNames = @(
        [string]$Artifact.category_coverage.positive_and_matched_negative.mapping,
        [string]$Artifact.category_coverage.observed_versus_not_observed.mapping,
        [string]$Artifact.category_coverage.supported_versus_unsupported_shape.mapping,
        [string]$Artifact.category_coverage.missing_versus_present.mapping,
        [string]$Artifact.category_coverage.supported_versus_unsupported_capabilities.mapping,
        [string]$Artifact.category_coverage.coverage_complete_incomplete_zero.mapping,
        [string]$Artifact.category_coverage.pairwise_interactions.mapping
    )
    $categoryMappingNames += @($Artifact.category_coverage.decoded_null_unresolved_unknown_omission_rejection.mappings | ForEach-Object { [string]$_ })
    foreach ($mappingName in @($categoryMappingNames)) {
        if (-not (Test-HasProperty $Artifact ([string]$mappingName))) { Add-Issue $issues "category coverage references unknown mapping '$mappingName'" }
    }
    $dimensionTokens = @($Artifact.category_coverage.observed_versus_not_observed.dimensions) + @($Artifact.category_coverage.supported_versus_unsupported_shape.dimensions) + @($Artifact.category_coverage.missing_versus_present.dimensions) + @($Artifact.category_coverage.supported_versus_unsupported_capabilities.dimensions)
    foreach ($token in $dimensionTokens) {
        $tokenText = [string]$token
        $separatorIndex = $tokenText.IndexOf('=', [System.StringComparison]::Ordinal)
        $dimensionName = if ($separatorIndex -gt 0) { $tokenText.Substring(0, $separatorIndex) } else { '' }
        $dimensionValue = if ($separatorIndex -gt 0 -and $separatorIndex -lt ($tokenText.Length - 1)) { $tokenText.Substring($separatorIndex + 1) } else { '' }
        $dimension = if ($dimensionName) { $Model.dimensions.PSObject.Properties[$dimensionName] } else { $null }
        if ($null -eq $dimension -or @($dimension.Value.values) -cnotcontains $dimensionValue) { Add-Issue $issues "category coverage references undeclared dimension/value token '$token'" }
    }
    foreach ($stateClass in @($Artifact.category_coverage.decoded_null_unresolved_unknown_omission_rejection.state_classes)) {
        if (@($Artifact.state_class_mappings.state_class) -cnotcontains [string]$stateClass) { Add-Issue $issues "category coverage references unknown state class '$stateClass'" }
    }
    foreach ($disposition in @($Artifact.category_coverage.decoded_null_unresolved_unknown_omission_rejection.dispositions)) {
        if (@($Artifact.disposition_mappings.disposition) -cnotcontains [string]$disposition) { Add-Issue $issues "category coverage references unknown disposition '$disposition'" }
    }
    $allMutationIds = @($Artifact.existing_authorability_mutations) + @($Artifact.wp3_mutations.id)
    foreach ($mutationId in @($Artifact.category_coverage.singular_versus_duplicate_gap_ownership.negative_mutation, $Artifact.category_coverage.aggregation_and_ordering.negative_mutation) + @($Artifact.category_coverage.duplicate_fact_and_taxonomy_identity.existing_mutations)) {
        if ($allMutationIds -cnotcontains [string]$mutationId) { Add-Issue $issues "category coverage references unknown mutation '$mutationId'" }
    }
    foreach ($caseId in @($Artifact.category_coverage.aggregation_and_ordering.lexical_cases)) {
        if (@($Artifact.lexical_cases.case_id) -cnotcontains [string]$caseId) { Add-Issue $issues "category coverage references unknown lexical case '$caseId'" }
    }
    foreach ($caseId in @($Artifact.category_coverage.singular_versus_duplicate_gap_ownership.positive, $Artifact.category_coverage.exact_partial_race_data.higher_order_case)) {
        if (@($Artifact.higher_order_cases.case_id) -cnotcontains [string]$caseId) { Add-Issue $issues "category coverage references unknown higher-order case '$caseId'" }
    }
    $ledgerPath = Join-Path $scriptRoot '..\docs\evaluation\fixtures\protocol-4-oracle-authorability\coverage-ledger.json'
    $ledgerMutations = @((Read-Json $ledgerPath 'existing authorability coverage ledger').mutations | ForEach-Object { [string]$_ })
    if (-not (Test-JsonEquivalent @($Artifact.existing_authorability_mutations) $ledgerMutations)) { [void]$coverageDrift.Add('existing_authorability_mutations'); Add-Issue $issues 'existing authorability mutation registry drifted from the retained coverage ledger' }
    $expectedWp3Mutations = @(Get-Wp3Mutations | ForEach-Object { [string]$_.id })
    if (-not (Test-SameStringSet @($Artifact.wp3_mutations.id) $expectedWp3Mutations) -or -not (Test-JsonEquivalent @($Artifact.wp3_mutations) @($Expected.wp3_mutations))) { [void]$coverageDrift.Add('wp3_mutations'); Add-Issue $issues 'WP3 mutation registry drifted' }

    $computedUncovered = $coverageDrift.Count
    if ([int]$Artifact.summary.uncovered_required_obligations -ne $computedUncovered) { Add-Issue $issues "summary uncovered_required_obligations is not computed (reported=$($Artifact.summary.uncovered_required_obligations), computed=$computedUncovered)" }
    if (-not (Test-JsonEquivalent $Artifact.summary $Expected.summary)) { Add-Issue $issues 'artifact summary counts drifted from recomputed coverage' }

    return [pscustomobject][ordered]@{ passed = ($issues.Count -eq 0); issues = $issues.ToArray() }
}

function Invoke-MutationSelfTests([object]$Model, [object]$Truth, [object]$Schema, [object]$Artifact, [object]$Expected, [string]$ModelHash) {
    $tests = @(
        @{ id = 'missing-state-case'; expect = 'unknown case'; mutate = { param($a) $a.state_cases = @($a.state_cases | Select-Object -Skip 1) } },
        @{ id = 'false-rule-coverage-claim'; expect = 'false coverage claim'; mutate = { param($a) $a.rule_mappings[0].case_ids[0] = [string]$a.rule_mappings[1].case_ids[0] } },
        @{ id = 'unstable-case-order'; expect = 'not in stable ordinal'; mutate = { param($a) $first=$a.state_cases[0];$a.state_cases[0]=$a.state_cases[1];$a.state_cases[1]=$first } },
        @{ id = 'duplicate-rule-mapping'; expect = 'duplicate case mapping'; mutate = { param($a) $a.rule_mappings[0].case_ids = @($a.rule_mappings[0].case_ids) + @($a.rule_mappings[0].case_ids[0]) } },
        @{ id = 'duplicate-case-id'; expect = 'duplicate generated case ID'; mutate = { param($a) $a.state_cases[1].case_id = [string]$a.state_cases[0].case_id } },
        @{ id = 'missing-constraint-mapping'; expect = 'constraint mapping inventory'; mutate = { param($a) $a.constraint_mappings = @($a.constraint_mappings | Select-Object -Skip 1) } },
        @{ id = 'missing-pairwise-mapping'; expect = 'pairwise mapping inventory'; mutate = { param($a) $a.pairwise_mappings = @($a.pairwise_mappings | Select-Object -Skip 1) } },
        @{ id = 'unknown-case-reference'; expect = 'references unknown case'; mutate = { param($a) $a.pairwise_mappings[0].case_id = 'P4-WP3-STATE-UNKNOWN-9999' } },
        @{ id = 'broken-matched-negative'; expect = 'matched negative.*admitted'; mutate = { param($a) $a.admitted_state_mappings[0].matched_negative_case_id = [string]$a.admitted_state_mappings[1].case_id } },
        @{ id = 'answer-bearing-property'; expect = 'answer-bearing property'; mutate = { param($a) $a | Add-Member -NotePropertyName expected_facts -NotePropertyValue @() } },
        @{ id = 'duplicate-gap-owner'; expect = 'gap ownership is not singular'; mutate = { param($a) $a.higher_order_cases[0].gap_owner_ids = @($a.higher_order_cases[0].gap_owner_ids) + @($a.higher_order_cases[0].gap_owner_ids[0]) } },
        @{ id = 'partial-race-rule-omission'; expect = 'partial RACE/DATA'; mutate = { param($a) $a.higher_order_cases[0].rule_cases = @($a.higher_order_cases[0].rule_cases | Select-Object -Skip 1) } },
        @{ id = 'state-digest-drift'; expect = 'classification digest drifted'; mutate = { param($a) $a.state_space_proof.aggregate_state_digest_sha256 = ('0' * 64) } },
        @{ id = 'wrong-family-mapping-case'; expect = 'family mapping.*unknown case'; mutate = { param($a) $a.family_mappings[0].case_ids[0] = 'P4-WP3-STATE-NONEXISTENT-0001' } },
        @{ id = 'wrong-state-class-case'; expect = 'state-class mapping.*unknown case'; mutate = { param($a) $a.state_class_mappings[0].case_ids[0] = 'P4-WP3-STATE-NONEXISTENT-0001' } },
        @{ id = 'wrong-disposition-case'; expect = 'disposition mapping.*unknown case'; mutate = { param($a) $a.disposition_mappings[0].case_ids[0] = 'P4-WP3-STATE-NONEXISTENT-0001' } },
        @{ id = 'wrong-constructor-case'; expect = 'constructor mapping.*unknown case'; mutate = { param($a) $a.constructor_mappings[0].case_ids[0] = 'P4-WP3-STATE-NONEXISTENT-0001' } },
        @{ id = 'wrong-atomic-boundary-case'; expect = 'atomic-boundary mapping.*another boundary'; mutate = { param($a) $a.atomic_boundary_mappings[0].state_case_ids[0] = [string]$a.atomic_boundary_mappings[1].state_case_ids[0] } },
        @{ id = 'changed-lexical-inputs'; expect = 'lexical exercise.*generic inputs'; mutate = { param($a) $a.lexical_cases[0].generic_inputs.values[0] = 'bogus-input' } },
        @{ id = 'missing-lexical-inputs'; expect = 'lexical exercise.*generic inputs'; mutate = { param($a) $a.lexical_cases[0].PSObject.Properties.Remove('generic_inputs') } },
        @{ id = 'changed-gap-population'; expect = 'gap exercise.*population'; mutate = { param($a) $a.gap_exercises[0].population_template = 'bogus:{signature_lower}' } },
        @{ id = 'changed-gap-capability'; expect = 'gap exercise.*capability'; mutate = { param($a) $a.gap_exercises[0].missing_capability = 'bogus-capability' } },
        @{ id = 'changed-gap-scope'; expect = 'gap exercise.*scope'; mutate = { param($a) $a.gap_exercises[0].scope = 'result-only' } },
        @{ id = 'wrong-coverage-case'; expect = 'coverage exercise.*unknown zero_case_id'; mutate = { param($a) $a.coverage_exercises[0].zero_case_id = 'P4-WP3-STATE-NONEXISTENT-0001' } },
        @{ id = 'wrong-transition-rule'; expect = 'transition mapping.*wrong rule inventory'; mutate = { param($a) $a.transition_mappings[0].rule_cases[0].rule_id = 'P4-RESULT-PUBLISHED' } },
        @{ id = 'wrong-transition-case'; expect = 'transition mapping.*unknown case'; mutate = { param($a) $a.transition_mappings[0].rule_cases[0].case_id = 'P4-WP3-STATE-NONEXISTENT-0001' } },
        @{ id = 'nonexistent-partial-rule-case'; expect = 'partial RACE/DATA.*unknown case'; mutate = { param($a) $a.higher_order_cases[0].rule_cases[0].case_id = 'P4-WP3-STATE-NONEXISTENT-0001' } },
        @{ id = 'category-coverage-drift'; expect = 'category coverage drifted'; mutate = { param($a) $a.category_coverage.pairwise_interactions.mapping = 'family_mappings' } },
        @{ id = 'missing-existing-mutation-id'; expect = 'existing authorability mutation registry drifted'; mutate = { param($a) $a.existing_authorability_mutations = @($a.existing_authorability_mutations | Select-Object -Skip 1) } },
        @{ id = 'extra-existing-mutation-id'; expect = 'existing authorability mutation registry drifted'; mutate = { param($a) $a.existing_authorability_mutations = @($a.existing_authorability_mutations) + @('unknown-authorability-mutation') } },
        @{ id = 'summary-count-drift'; expect = 'summary counts drifted'; mutate = { param($a) $a.summary.constructor_mapping_count++ } },
        @{ id = 'weakened-forbidden-registry'; expect = 'answer-bearing property'; mutate = { param($a) $a.answer_isolation.forbidden_properties = @($a.answer_isolation.forbidden_properties | Where-Object { [string]$_ -cne 'expected_facts' }); $a | Add-Member -NotePropertyName expected_facts -NotePropertyValue @() } },
        @{ id = 'simultaneous-multi-surface-corruption'; expect = 'state-class mapping.*unknown case'; mutate = { param($a) $a.constructor_mappings[0].case_ids=@('P4-WP3-STATE-NONEXISTENT-0001');$a.state_class_mappings[0].case_ids=@('P4-WP3-STATE-NONEXISTENT-0001');$a.disposition_mappings[0].case_ids=@('P4-WP3-STATE-NONEXISTENT-0001');$a.lexical_cases[0].generic_inputs=[pscustomobject]@{bogus='value'};$a.gap_exercises[0].missing_capability='bogus-capability';$a.coverage_exercises[0].zero_case_id='P4-WP3-STATE-NONEXISTENT-0001';$a.transition_mappings[0].rule_cases[0].case_id='P4-WP3-STATE-NONEXISTENT-0001';$a.higher_order_cases[0].rule_cases[0].case_id='P4-WP3-STATE-NONEXISTENT-0001' } }
    )
    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($test in $tests) {
        $mutation = Copy-JsonObject $Artifact
        & $test.mutate $mutation
        $validation = Invoke-ArtifactValidation $Model $Truth $Schema $mutation $Expected $ModelHash
        $matching = @($validation.issues | Where-Object { [string]$_ -match [string]$test.expect } | Select-Object -First 1)
        $rejected = -not $validation.passed -and $matching.Count -eq 1
        $results.Add([pscustomobject][ordered]@{ id = [string]$test.id; result = $(if ($rejected) { 'rejected' } else { 'unexpectedly-passed' }); evidence = $(if ($matching.Count) { [string]$matching[0] } else { '' }) })
    }
    return $results.ToArray()
}

$model = Read-Json $ModelPath 'proposed totality model'
$schema = Read-Json $SchemaPath 'generated coverage schema'
$modelHash = (Get-FileHash -LiteralPath $ModelPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ([string]$model.status -cne 'proposed' -or [string]$model.version -cne '1.2.0') {
    throw 'Protocol /4 WP3 state coverage failed: source model must be proposed version 1.2.0'
}
$truth = New-TruthModel $model
$expectedArtifact = New-CoverageArtifact $model $truth $modelHash
if ($ValidateOnly) {
    $artifact = Read-Json $ArtifactPath 'generated state coverage artifact'
}
else {
    $artifact = $expectedArtifact
    $json = ($artifact | ConvertTo-Json -Depth 100 -Compress).Replace("`r`n", "`n") + "`n"
    $parent = Split-Path -Parent $ArtifactPath
    if ($parent -and -not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($ArtifactPath), $json, [System.Text.UTF8Encoding]::new($false))
    $artifact = Read-Json $ArtifactPath 'generated state coverage artifact'
}

$validation = Invoke-ArtifactValidation $model $truth $schema $artifact $expectedArtifact $modelHash
$selfTests = @()
if (-not $SkipSelfTests) { $selfTests = @(Invoke-MutationSelfTests $model $truth $schema $artifact $expectedArtifact $modelHash) }
$selfTestsPassed = @($selfTests | Where-Object { [string]$_.result -cne 'rejected' }).Count -eq 0
$passed = $validation.passed -and $selfTestsPassed
$partialInvariant = $model.cross_family_invariants.partial_race_data
$raceDenominator = switch ([string]$partialInvariant.contribution.coverage_denominator) { 'increment-one' { 1 } 'no-increment' { 0 } default { throw 'Protocol /4 WP3 state coverage failed: unknown race denominator effect' } }
$raceCompletion = switch ([string]$partialInvariant.contribution.coverage_completion) { 'increment-one' { 1 } 'no-increment' { 0 } default { throw 'Protocol /4 WP3 state coverage failed: unknown race completion effect' } }
$taxonomyDenominator = switch ([string]$partialInvariant.taxonomy.coverage_denominator) { 'increment-one' { 1 } 'no-increment' { 0 } default { throw 'Protocol /4 WP3 state coverage failed: unknown taxonomy denominator effect' } }
$taxonomyCompletion = switch ([string]$partialInvariant.taxonomy.coverage_completion) { 'increment-one' { 1 } 'no-increment' { 0 } default { throw 'Protocol /4 WP3 state coverage failed: unknown taxonomy completion effect' } }
$summary = [pscustomobject][ordered]@{
    validator = 'infinium.evaluation.protocol-4-model-derived-state-coverage-validator/v1'
    generator_version = '1.1.0'
    status = $(if ($passed) { 'passed' } else { 'failed' })
    source_model_sha256 = $modelHash
    artifact_sha256 = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
    totals = $truth.totals
    coverage = $artifact.summary
    aggregate_state_digest_sha256 = [string]$truth.aggregate_state_digest_sha256
    partial_race_data = [pscustomobject][ordered]@{
        invariant_id = [string]$partialInvariant.invariant_id
        race_records_denominator = $raceDenominator
        race_records_completion = $raceCompletion
        taxonomy_subjects_denominator = $taxonomyDenominator
        taxonomy_subjects_completion = $taxonomyCompletion
        assignment_count = @($partialInvariant.taxonomy.assignments).Count
        data_count = [string]$partialInvariant.allowlisted_field.count_publication
        face_gen_head = [string]$partialInvariant.contribution.face_gen_disposition
        resolved_race = [string]$partialInvariant.resolved_race.disposition
        gap_population = [string]$partialInvariant.gap.population
        missing_capability = [string]$partialInvariant.gap.missing_capability
        gap_scope = [string]$partialInvariant.gap.scope
        affected_count = [int]$partialInvariant.gap.affected_count
        gap_owner_id = [string]$partialInvariant.gap.owner_id
    }
    self_tests = $selfTests
    issues = @($validation.issues)
}

if ($SummaryPath) {
    $summaryParent = Split-Path -Parent $SummaryPath
    if ($summaryParent -and -not (Test-Path -LiteralPath $summaryParent)) { New-Item -ItemType Directory -Path $summaryParent -Force | Out-Null }
    $summaryJson = ($summary | ConvertTo-Json -Depth 30 -Compress).Replace("`r`n", "`n") + "`n"
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($SummaryPath), $summaryJson, [System.Text.UTF8Encoding]::new($false))
}

Write-Output "Protocol /4 model-derived state coverage: $($summary.status)"
Write-Output "Raw state proof: raw=$($truth.totals.raw) admitted=$($truth.totals.admitted) excluded=$($truth.totals.excluded) invalid=$($truth.totals.invalid) uncovered=$($truth.totals.uncovered) overlap=$($truth.totals.overlap)"
Write-Output "Generated coverage: cases=$($artifact.summary.state_case_count) admitted=$($artifact.summary.admitted_state_case_count) invalid=$($artifact.summary.invalid_state_case_count) excluded=$($artifact.summary.excluded_state_case_count) matched-negatives=$($artifact.summary.matched_negative_count) constraints=$($artifact.summary.constraint_mapping_count) pairwise=$($artifact.summary.pairwise_mapping_count) rules=$($artifact.summary.rule_mapping_count) constructors=$($artifact.summary.constructor_mapping_count)"
foreach ($test in $selfTests) { Write-Output "WP3 mutation $($test.id): $($test.result); evidence=$($test.evidence)" }
foreach ($issue in @($validation.issues)) { Write-Output "ERROR: $issue" }
if (-not $passed) { exit 1 }
