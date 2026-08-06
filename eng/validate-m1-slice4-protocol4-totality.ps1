[CmdletBinding()]
param(
    [string]$ModelPath,
    [string]$SchemaPath,
    [string]$SummaryPath,
    [switch]$SkipSelfTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ModelPath) { $ModelPath = Join-Path $scriptRoot '..\docs\evaluation\specifications\m1-slice4-protocol-4-totality-model.json' }
if (-not $SchemaPath) { $SchemaPath = Join-Path $scriptRoot '..\docs\evaluation\specifications\m1-slice4-protocol-4-totality-model.schema.json' }

function Read-Json([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Protocol /4 totality validation failed: missing $Label at '$Path'"
    }
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Protocol /4 totality validation failed: invalid $Label JSON at '$Path': $($_.Exception.Message)"
    }
}

function Copy-JsonObject([object]$Value) {
    return $Value | ConvertTo-Json -Depth 100 | ConvertFrom-Json
}

function Add-Issue([System.Collections.Generic.List[string]]$Issues, [string]$Message) {
    if ($Issues.Count -lt 100) {
        $Issues.Add($Message)
    }
}

function Test-HasProperty([object]$Object, [string]$Name) {
    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}

function Assert-RequiredProperties([object]$Object, [object[]]$Names, [string]$Path, [System.Collections.Generic.List[string]]$Issues) {
    foreach ($name in @($Names)) {
        if (-not (Test-HasProperty $Object ([string]$name))) {
            Add-Issue $Issues "$Path is missing schema-required property '$name'"
        }
    }
}

function Get-ConditionMatches([object]$State, [object[]]$Conditions) {
    foreach ($condition in @($Conditions)) {
        $property = $State.PSObject.Properties[[string]$condition.dimension]
        if ($null -eq $property) {
            return $false
        }
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

function Get-StateProduct([object]$Model, [string[]]$Dimensions, [int]$Index = 0, [object]$Prefix = $null) {
    if ($null -eq $Prefix) {
        $Prefix = [ordered]@{}
    }
    if ($Index -ge $Dimensions.Count) {
        [pscustomobject]$Prefix
        return
    }
    $dimension = $Dimensions[$Index]
    foreach ($value in @($Model.dimensions.$dimension.values)) {
        $next = [ordered]@{}
        foreach ($key in $Prefix.Keys) { $next[$key] = $Prefix[$key] }
        $next[$dimension] = [string]$value
        Get-StateProduct -Model $Model -Dimensions $Dimensions -Index ($Index + 1) -Prefix $next
    }
}

function Get-SortedStrings([object[]]$Values) {
    return @($Values | ForEach-Object { [string]$_ } | Sort-Object)
}

function Test-SameStringSet([object[]]$Left, [object[]]$Right) {
    $a = @(Get-SortedStrings $Left)
    $b = @(Get-SortedStrings $Right)
    if ($a.Count -ne $b.Count) { return $false }
    for ($i = 0; $i -lt $a.Count; $i++) {
        if ($a[$i] -cne $b[$i]) { return $false }
    }
    return $true
}

function Invoke-ModelValidation([object]$Model, [object]$Schema) {
    $issues = [System.Collections.Generic.List[string]]::new()
    $checks = [ordered]@{
        schema_contract = $true
        stable_ids = $true
        references_and_vocabularies = $true
        evidence_dependencies = $true
        state_totality = $true
        publication_dependencies = $true
        coverage_consistency = $true
        gap_ownership = $true
        partial_race_data = $true
        inventories = $true
    }

    Assert-RequiredProperties $Model @($Schema.required) '$' $issues
    if ((Test-HasProperty $Schema 'properties') -and (Test-HasProperty $Schema.properties 'version')) {
        if ([string]$Model.version -cne [string]$Schema.properties.version.const) {
            Add-Issue $issues "model version does not equal schema const '$($Schema.properties.version.const)'"
        }
    }
    if ([string]$Model.status -cne 'proposed') { Add-Issue $issues 'model status must remain proposed until WP4' }
    if ([string]$Model.protocol.protocol_id -cne 'infinium.evaluator-v2/4') { Add-Issue $issues 'protocol identity is not /4' }
    if ([string]$Model.protocol.evaluator_commit -cne '3693d19563c636cd2879804633ca4ce52448d2c1') { Add-Issue $issues 'evaluator commit drifted' }
    if ([string]$Model.protocol.candidate_commit -cne 'a98d648bd0adb2751ee0c09828e0227b1583950f') { Add-Issue $issues 'candidate commit drifted' }

    $familyRequired = @($Schema.'$defs'.family.required)
    foreach ($family in @($Model.fact_families)) {
        Assert-RequiredProperties $family $familyRequired "family/$($family.family)" $issues
        if ([string]$family.unstated_default -cne 'prohibited') { Add-Issue $issues "family '$($family.family)' permits an unstated default" }
        if (Test-HasProperty $family 'state_space') {
            Assert-RequiredProperties $family.state_space @($Schema.'$defs'.state_space.required) "family/$($family.family)/state_space" $issues
        }
    }
    if ($issues.Count -gt 0) { $checks.schema_contract = $false }

    $authorityIds = @($Model.authorities.PSObject.Properties.Name)
    $dimensionIds = @($Model.dimensions.PSObject.Properties.Name)
    $stateClassIds = @($Model.state_classes | ForEach-Object { [string]$_.id })
    $boundaryIds = @($Model.atomic_boundaries | ForEach-Object { [string]$_.id })
    $gapRuleIds = @($Model.gap_rules | ForEach-Object { [string]$_.rule_id })
    $populationIds = @($Model.coverage_registry | ForEach-Object { [string]$_.population })
    $ruleIds = @($Model.fact_families | ForEach-Object { $_.rules } | ForEach-Object { [string]$_.rule_id })
    $constructorIds = @($Model.fact_families | ForEach-Object { $_.constructor_groups } | ForEach-Object { [string]$_.id })
    $constraintIds = @($Model.fact_families | ForEach-Object {
        if (Test-HasProperty $_ 'state_space') {
            $constraints = @($_.state_space.admitted_regions) + @($_.state_space.invalid_regions)
            if (Test-HasProperty $_.state_space 'excluded_constraint') { $constraints += @($_.state_space.excluded_constraint) }
            $constraints
        }
    } | ForEach-Object { if ($null -ne $_) { [string]$_.constraint_id } })

    foreach ($entry in @(
        @{ Values = $ruleIds; Label = 'rule ID' },
        @{ Values = $constructorIds; Label = 'constructor ID' },
        @{ Values = $constraintIds; Label = 'constraint ID' },
        @{ Values = $boundaryIds; Label = 'atomic boundary ID' },
        @{ Values = $gapRuleIds; Label = 'gap rule ID' },
        @{ Values = $populationIds; Label = 'coverage population ID' }
    )) {
        $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($value in @($entry.Values)) {
            if (-not $seen.Add([string]$value)) { Add-Issue $issues "duplicate $($entry.Label) '$value'"; $checks.stable_ids = $false }
        }
    }

    $layerOrdinals = @{ none = 0 }
    foreach ($layer in @($Model.evidence_layers)) { $layerOrdinals[[string]$layer.id] = [int]$layer.ordinal }
    if (@($Model.evidence_layers).Count -ne 5 -or -not (Test-SameStringSet @($layerOrdinals.Keys) @('none','structural','observed','decoded','resolved','semantic'))) {
        Add-Issue $issues 'evidence-layer inventory is not the exact five-layer progression'; $checks.evidence_dependencies = $false
    }
    foreach ($layer in @($Model.evidence_layers)) {
        foreach ($prerequisite in @($layer.prerequisites)) {
            if (-not $layerOrdinals.ContainsKey([string]$prerequisite) -or $layerOrdinals[[string]$prerequisite] -ge [int]$layer.ordinal) {
                Add-Issue $issues "evidence layer '$($layer.id)' has an invalid prerequisite '$prerequisite'"; $checks.evidence_dependencies = $false
            }
        }
    }

    $factTypes = @($Model.vocabularies.fact_types)
    $valueTypes = @($Model.vocabularies.value_types)
    $familyVocabulary = @($Model.vocabularies.fact_families)
    $requiredStateClasses = @('not-observed','observed-undecodable','decoded-null','decoded-unresolved','resolved','semantic-applicable','unsupported','not-applicable','terminal-rejection')
    if (-not (Test-SameStringSet $stateClassIds $requiredStateClasses)) {
        Add-Issue $issues 'state-class inventory is not the exact declared set of nine'; $checks.inventories = $false
    }
    if (@($Model.fact_families).Count -ne 15 -or -not (Test-SameStringSet $familyVocabulary @($Model.fact_families.family))) {
        Add-Issue $issues 'fact-family inventory is not the exact declared set of fifteen'; $checks.inventories = $false
    }
    if (@($Model.coverage_registry).Count -ne 10 -or -not (Test-SameStringSet @($Model.vocabularies.coverage_populations) $populationIds)) {
        Add-Issue $issues 'coverage population inventory is not the exact declared set of ten'; $checks.inventories = $false
    }
    foreach ($collection in @($Model.evidence_layers, $Model.atomic_boundaries, $Model.gap_rules, $Model.coverage_registry, $Model.normalization_rules)) {
        foreach ($item in @($collection)) {
            if (Test-HasProperty $item 'authorities') {
                foreach ($authority in @($item.authorities)) {
                    if ($authorityIds -cnotcontains [string]$authority) { Add-Issue $issues "public inventory references unknown authority '$authority'"; $checks.references_and_vocabularies = $false }
                }
            }
            if ((Test-HasProperty $item 'atomic_boundary') -and $boundaryIds -cnotcontains [string]$item.atomic_boundary) { Add-Issue $issues "public inventory references unknown atomic boundary '$($item.atomic_boundary)'"; $checks.references_and_vocabularies = $false }
        }
    }

    $allGapOwners = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($family in @($Model.fact_families)) {
        $familyName = [string]$family.family
        $usedDimensions = @($family.dimensions_used | ForEach-Object { [string]$_ })
        foreach ($dimension in $usedDimensions) {
            if ($dimensionIds -cnotcontains $dimension) { Add-Issue $issues "family '$familyName' uses unknown dimension '$dimension'"; $checks.references_and_vocabularies = $false }
        }
        $familyConstructors = @($family.constructor_groups | ForEach-Object { [string]$_.id })
        foreach ($constructor in @($family.constructor_groups)) {
            if (-not $layerOrdinals.ContainsKey([string]$constructor.minimum_layer)) { Add-Issue $issues "constructor '$($constructor.id)' has unknown minimum layer"; $checks.evidence_dependencies = $false }
            foreach ($factType in @($constructor.fact_type)) { if ($factTypes -cnotcontains [string]$factType) { Add-Issue $issues "constructor '$($constructor.id)' uses unknown fact type '$factType'"; $checks.references_and_vocabularies = $false } }
            foreach ($valueType in @($constructor.value_types)) { if ($valueTypes -cnotcontains [string]$valueType) { Add-Issue $issues "constructor '$($constructor.id)' uses unknown value type '$valueType'"; $checks.references_and_vocabularies = $false } }
            foreach ($authority in @($constructor.authorities)) { if ($authorityIds -cnotcontains [string]$authority) { Add-Issue $issues "constructor '$($constructor.id)' references unknown authority '$authority'"; $checks.references_and_vocabularies = $false } }
        }

        $conditionContainers = @($family.rules)
        if (Test-HasProperty $family 'state_space') { $conditionContainers += @($family.state_space.admitted_regions) + @($family.state_space.invalid_regions) }
        foreach ($container in $conditionContainers) {
            foreach ($condition in @($container.when)) {
                $dimension = [string]$condition.dimension
                if ($usedDimensions -cnotcontains $dimension -or $dimensionIds -cnotcontains $dimension) {
                    Add-Issue $issues "family '$familyName' condition references undeclared dimension '$dimension'"; $checks.references_and_vocabularies = $false
                    continue
                }
                $allowed = @($Model.dimensions.$dimension.values | ForEach-Object { [string]$_ })
                foreach ($value in @($condition.values)) {
                    if ($allowed -cnotcontains [string]$value) { Add-Issue $issues "family '$familyName' condition uses closed-vocabulary value '$value' outside dimension '$dimension'"; $checks.references_and_vocabularies = $false }
                }
            }
        }

        foreach ($rule in @($family.rules)) {
            if ($stateClassIds -cnotcontains [string]$rule.state_class) { Add-Issue $issues "rule '$($rule.rule_id)' uses unknown state class"; $checks.references_and_vocabularies = $false }
            if ($boundaryIds -cnotcontains [string]$rule.atomic_boundary) { Add-Issue $issues "rule '$($rule.rule_id)' references unknown atomic boundary"; $checks.references_and_vocabularies = $false }
            foreach ($authority in @($rule.authorities)) { if ($authorityIds -cnotcontains [string]$authority) { Add-Issue $issues "rule '$($rule.rule_id)' references unknown authority '$authority'"; $checks.references_and_vocabularies = $false } }
            if (-not $layerOrdinals.ContainsKey([string]$rule.minimum_layer)) { Add-Issue $issues "rule '$($rule.rule_id)' uses unknown evidence layer"; $checks.evidence_dependencies = $false; continue }
            if ([string]$rule.state_class -ceq 'decoded-null' -and $layerOrdinals[[string]$rule.minimum_layer] -lt 3) { Add-Issue $issues "decoded-null rule '$($rule.rule_id)' lacks decoded evidence"; $checks.publication_dependencies = $false }
            if ([string]$rule.state_class -ceq 'decoded-unresolved' -and $layerOrdinals[[string]$rule.minimum_layer] -lt 4) { Add-Issue $issues "decoded-unresolved rule '$($rule.rule_id)' lacks resolved evidence"; $checks.publication_dependencies = $false }
            if ([string]$rule.state_class -ceq 'semantic-applicable' -and $layerOrdinals[[string]$rule.minimum_layer] -lt 5) { Add-Issue $issues "semantic rule '$($rule.rule_id)' lacks semantic evidence"; $checks.publication_dependencies = $false }
            foreach ($outcome in @($rule.outcomes)) {
                foreach ($constructorId in @($outcome.constructor_groups)) {
                    if ($familyConstructors -cnotcontains [string]$constructorId) { Add-Issue $issues "rule '$($rule.rule_id)' references unknown constructor '$constructorId'"; $checks.references_and_vocabularies = $false; continue }
                    $constructor = @($family.constructor_groups | Where-Object { [string]$_.id -ceq [string]$constructorId })[0]
                    if (@('exact_value','typed_null','accepted_unknown','mixed_by_constructor') -ccontains [string]$outcome.disposition) {
                        if ($layerOrdinals[[string]$rule.minimum_layer] -lt $layerOrdinals[[string]$constructor.minimum_layer]) { Add-Issue $issues "rule '$($rule.rule_id)' publishes constructor '$constructorId' below its evidence layer"; $checks.publication_dependencies = $false }
                    }
                }
            }
            $effect = $rule.coverage_effect
            if ($null -ne $effect.population -and $populationIds -cnotcontains [string]$effect.population) { Add-Issue $issues "rule '$($rule.rule_id)' references unknown coverage population '$($effect.population)'"; $checks.references_and_vocabularies = $false }
            if ([string]$effect.denominator -ceq 'none' -and @('increment-one','increment-two') -ccontains [string]$effect.completion) { Add-Issue $issues "rule '$($rule.rule_id)' increments completion without a denominator effect"; $checks.coverage_consistency = $false }
            $additionalEffects = @()
            if (Test-HasProperty $effect 'additional_population_effects') { $additionalEffects = @($effect.additional_population_effects) }
            foreach ($extra in $additionalEffects) {
                if ($populationIds -cnotcontains [string]$extra.population) { Add-Issue $issues "rule '$($rule.rule_id)' references unknown additional coverage population"; $checks.references_and_vocabularies = $false }
            }
            foreach ($gap in @($rule.gap_effects)) {
                if ($gapRuleIds -cnotcontains [string]$gap.gap_rule_id) { Add-Issue $issues "rule '$($rule.rule_id)' references unknown gap rule '$($gap.gap_rule_id)'"; $checks.references_and_vocabularies = $false }
                if (-not (Test-HasProperty $gap 'owner_id') -or -not $allGapOwners.Add([string]$gap.owner_id)) { Add-Issue $issues "gap owner '$($gap.owner_id)' is absent or double-counted"; $checks.gap_ownership = $false }
            }
            if ([string]$rule.rule_id -ceq 'P4-RACECONTRIB-PARTIAL-DATA') {
                $partialGaps = @($rule.gap_effects)
                if ([string]$effect.population -cne 'race-records' -or [string]$effect.denominator -cne 'increment-one' -or [string]$effect.completion -cne 'no-increment' -or $partialGaps.Count -ne 1 -or [string]$partialGaps[0].gap_rule_id -cne 'P4-GAP-UNSUPPORTED-SHAPE') {
                    Add-Issue $issues 'partial RACE/DATA contribution arithmetic or gap obligation drifted'; $checks.partial_race_data = $false
                }
            }
            if ([string]$rule.rule_id -ceq 'P4-TAXONOMY-PARTIAL-RACE') {
                if ([string]$effect.population -cne 'taxonomy-subjects' -or [string]$effect.denominator -cne 'increment-one' -or [string]$effect.completion -cne 'increment-one') {
                    Add-Issue $issues 'partial RACE/DATA taxonomy arithmetic drifted'; $checks.partial_race_data = $false
                }
            }
        }

        if ($issues.Count -gt 0) { continue }
        if (-not (Test-HasProperty $family 'state_space') -or -not (Test-HasProperty $family.state_space 'excluded_constraint')) { continue }
        $counts = [ordered]@{ family = $familyName; raw = 0; admitted = 0; excluded = 0; invalid = 0; uncovered = 0; overlap = 0 }
        foreach ($state in @(Get-StateProduct -Model $Model -Dimensions $usedDimensions)) {
            $counts.raw++
            $admitted = @($family.state_space.admitted_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            $invalid = @($family.state_space.invalid_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            if ($admitted.Count -gt 1 -or ($admitted.Count -gt 0 -and $invalid.Count -gt 0)) { $counts.overlap++; continue }
            if ($invalid.Count -gt 0) { $counts.invalid++; continue }
            if ($admitted.Count -eq 0) { if (Test-HasProperty $family.state_space 'excluded_constraint') { $counts.excluded++ } else { $counts.uncovered++ }; continue }
            $counts.admitted++
            $region = $admitted[0]
            $matchingRules = @($family.rules | Where-Object { Get-ConditionMatches $state @($_.when) })
            if ($matchingRules.Count -ne 1) { Add-Issue $issues "admitted state '$($region.constraint_id)' in '$familyName' matches $($matchingRules.Count) rules"; $checks.state_totality = $false; continue }
            $matched = $matchingRules[0]
            if ([string]$matched.state_class -cne [string]$region.state_class) { Add-Issue $issues "admitted region '$($region.constraint_id)' and rule '$($matched.rule_id)' disagree on state class"; $checks.state_totality = $false }
            $coveredConstructors = @($matched.outcomes | ForEach-Object { $_.constructor_groups } | ForEach-Object { [string]$_ })
            if (-not (Test-SameStringSet $coveredConstructors $familyConstructors)) { Add-Issue $issues "rule '$($matched.rule_id)' does not dispose every constructor exactly once"; $checks.state_totality = $false }
            $actualGaps = @($matched.gap_effects | ForEach-Object { [string]$_.gap_rule_id })
            if (-not (Test-SameStringSet $actualGaps @($region.required_gap_rule_ids))) { Add-Issue $issues "region '$($region.constraint_id)' and rule '$($matched.rule_id)' disagree on required gaps"; $checks.gap_ownership = $false }
        }
        if ($counts.uncovered -ne 0 -or $counts.overlap -ne 0) { $checks.state_totality = $false; Add-Issue $issues "family '$familyName' has uncovered=$($counts.uncovered), overlap=$($counts.overlap)" }
        $script:familyCountBuffer.Add([pscustomobject]$counts)
    }

    foreach ($trace in @($Model.manual_traces)) {
        foreach ($ruleId in @($trace.rules)) { if ($ruleIds -cnotcontains [string]$ruleId) { Add-Issue $issues "trace '$($trace.trace_id)' references unknown rule '$ruleId'"; $checks.references_and_vocabularies = $false } }
    }

    $partial = @($Model.fact_families | Where-Object family -ceq 'race_contributions')[0].rules | Where-Object rule_id -ceq 'P4-RACECONTRIB-PARTIAL-DATA'
    $partialTaxonomy = @($Model.fact_families | Where-Object family -ceq 'taxonomy')[0].rules | Where-Object rule_id -ceq 'P4-TAXONOMY-PARTIAL-RACE'
    $partialGap = @($partial.gap_effects)
    $shapeGap = @($Model.gap_rules | Where-Object rule_id -ceq 'P4-GAP-UNSUPPORTED-SHAPE')[0]
    $partialOk = $null -ne $partial -and [string]$partial.coverage_effect.population -ceq 'race-records' -and [string]$partial.coverage_effect.denominator -ceq 'increment-one' -and [string]$partial.coverage_effect.completion -ceq 'no-increment' -and $partialGap.Count -eq 1 -and [string]$partialGap[0].owner_id -ceq 'GO-RACECONTRIB-PARTIAL-DATA' -and [string]$partialGap[0].gap_rule_id -ceq 'P4-GAP-UNSUPPORTED-SHAPE' -and [string]$shapeGap.population_template -ceq 'unsupported-shapes:{signature_lower}:{field_lower}' -and [string]$shapeGap.missing_capability -ceq 'allowlisted-record-shape-semantics' -and $null -ne $partialTaxonomy -and [string]$partialTaxonomy.coverage_effect.population -ceq 'taxonomy-subjects' -and [string]$partialTaxonomy.coverage_effect.denominator -ceq 'increment-one' -and [string]$partialTaxonomy.coverage_effect.completion -ceq 'increment-one'
    $faceOutcome = @($partial.outcomes | Where-Object { @($_.constructor_groups) -ccontains 'FC-RACECONTRIB-FACEGEN' })
    if ($faceOutcome.Count -ne 1 -or [string]$faceOutcome[0].disposition -cne 'omit') { $partialOk = $false }
    if (-not $partialOk) { Add-Issue $issues 'partial RACE/DATA arithmetic or publication boundary drifted'; $checks.partial_race_data = $false }

    return [pscustomobject][ordered]@{ checks = [pscustomobject]$checks; issues = @($issues) }
}

$model = Read-Json $ModelPath 'model'
$schema = Read-Json $SchemaPath 'schema'
$script:familyCountBuffer = [System.Collections.Generic.List[object]]::new()
$validation = Invoke-ModelValidation $model $schema
$families = @($script:familyCountBuffer | Sort-Object family)

$selfTestResults = [System.Collections.Generic.List[object]]::new()
if (-not $SkipSelfTests) {
    $tests = @(
        @{ name = 'missing-uncovered-disposition'; mutate = { param($m) $m.fact_families[0].state_space.PSObject.Properties.Remove('excluded_constraint') } },
        @{ name = 'duplicate-stable-id'; mutate = { param($m) $m.fact_families[0].rules = @($m.fact_families[0].rules) + @(Copy-JsonObject $m.fact_families[0].rules[0]) } },
        @{ name = 'overlapping-rules'; mutate = { param($m) $copy = Copy-JsonObject $m.fact_families[0].rules[0]; $copy.rule_id = 'P4-RESULT-OVERLAP-SELFTEST'; $m.fact_families[0].rules = @($m.fact_families[0].rules) + @($copy) } },
        @{ name = 'invalid-evidence-layer-dependency'; mutate = { param($m) (@($m.fact_families | Where-Object family -ceq 'taxonomy')[0].rules | Where-Object rule_id -ceq 'P4-TAXONOMY-SEMANTIC').minimum_layer = 'structural' } },
        @{ name = 'unknown-dimension'; mutate = { param($m) $m.fact_families[0].rules[0].when[0].dimension = 'undeclared-selftest' } },
        @{ name = 'unknown-closed-vocabulary-value'; mutate = { param($m) $m.fact_families[0].rules[0].when[0].values[0] = 'outside-selftest-vocabulary' } },
        @{ name = 'unknown-constructor-reference'; mutate = { param($m) $m.fact_families[0].rules[0].outcomes[0].constructor_groups[0] = 'FC-UNKNOWN-SELFTEST' } },
        @{ name = 'unknown-authority-reference'; mutate = { param($m) $m.fact_families[0].rules[0].authorities[0] = 'unknown-selftest-authority' } },
        @{ name = 'inconsistent-coverage-arithmetic'; mutate = { param($m) (@($m.fact_families | Where-Object family -ceq 'race_contributions')[0].rules | Where-Object rule_id -ceq 'P4-RACECONTRIB-PARTIAL-DATA').coverage_effect.completion = 'increment-one' } },
        @{ name = 'missing-required-gap'; mutate = { param($m) (@($m.fact_families | Where-Object family -ceq 'race_contributions')[0].rules | Where-Object rule_id -ceq 'P4-RACECONTRIB-PARTIAL-DATA').gap_effects = @() } },
        @{ name = 'duplicate-gap-ownership'; mutate = { param($m) $rules = @($m.fact_families | ForEach-Object { $_.rules } | Where-Object { @($_.gap_effects).Count -gt 0 }); $rules[1].gap_effects[0].owner_id = [string]$rules[0].gap_effects[0].owner_id } },
        @{ name = 'invalid-partial-race-data-arithmetic'; mutate = { param($m) (@($m.fact_families | Where-Object family -ceq 'taxonomy')[0].rules | Where-Object rule_id -ceq 'P4-TAXONOMY-PARTIAL-RACE').coverage_effect.completion = 'no-increment' } }
    )
    foreach ($test in $tests) {
        $mutation = Copy-JsonObject $model
        & $test.mutate $mutation
        $script:familyCountBuffer = [System.Collections.Generic.List[object]]::new()
        $mutated = Invoke-ModelValidation $mutation $schema
        $rejected = @($mutated.issues).Count -gt 0
        $selfTestResults.Add([pscustomobject][ordered]@{ name = [string]$test.name; result = $(if ($rejected) { 'rejected' } else { 'unexpectedly-passed' }) })
    }
}

$totals = [ordered]@{ raw = 0; admitted = 0; excluded = 0; invalid = 0; uncovered = 0; overlap = 0 }
foreach ($family in $families) { foreach ($key in @($totals.Keys)) { $totals[$key] += [int]$family.$key } }
$inventory = [ordered]@{
    families = @($model.fact_families).Count
    coverage_populations = @($model.coverage_registry).Count
    dimensions = @($model.dimensions.PSObject.Properties).Count
    vocabularies = @($model.vocabularies.PSObject.Properties).Count
    constructor_groups = @($model.fact_families | ForEach-Object { $_.constructor_groups }).Count
    publication_rules = @($model.fact_families | ForEach-Object { $_.rules }).Count
    gap_rules = @($model.gap_rules).Count
    authorities = @($model.authorities.PSObject.Properties).Count
    atomic_boundaries = @($model.atomic_boundaries).Count
}
$allSelfTestsPassed = @($selfTestResults | Where-Object result -cne 'rejected').Count -eq 0
$passed = @($validation.issues).Count -eq 0 -and $allSelfTestsPassed
$summary = [pscustomobject][ordered]@{
    validator = 'infinium.m1-slice4.protocol-4-totality-validator/v1'
    model_id = [string]$model.model_id
    model_version = [string]$model.version
    status = $(if ($passed) { 'passed' } else { 'failed' })
    totals = [pscustomobject]$totals
    families = $families
    checks = $validation.checks
    inventory = [pscustomobject]$inventory
    partial_race_data = [pscustomobject][ordered]@{
        race_records_denominator = 1
        race_records_completion = 0
        taxonomy_subjects_denominator = 1
        taxonomy_subjects_completion = 1
        data_count = 'omitted'
        face_gen_head = 'omitted'
        resolved_race = 'omitted'
        gap_count = 1
        gap_population = 'unsupported-shapes:race:data'
        missing_capability = 'allowlisted-record-shape-semantics'
    }
    self_tests = @($selfTestResults)
    issues = @($validation.issues)
}

$json = $summary | ConvertTo-Json -Depth 20
if ($SummaryPath) {
    $parent = Split-Path -Parent $SummaryPath
    if ($parent -and -not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($SummaryPath), $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

Write-Output "Protocol /4 evidence-contract totality validation: $($summary.status)"
Write-Output "Totals: raw=$($totals.raw) admitted=$($totals.admitted) excluded=$($totals.excluded) invalid=$($totals.invalid) uncovered=$($totals.uncovered) overlap=$($totals.overlap)"
foreach ($family in $families) {
    Write-Output ("Family {0}: raw={1} admitted={2} excluded={3} invalid={4} uncovered={5} overlap={6}" -f $family.family, $family.raw, $family.admitted, $family.excluded, $family.invalid, $family.uncovered, $family.overlap)
}
Write-Output "Inventory: families=$($inventory.families) coverage=$($inventory.coverage_populations) dimensions=$($inventory.dimensions) vocabularies=$($inventory.vocabularies) constructors=$($inventory.constructor_groups) rules=$($inventory.publication_rules) gaps=$($inventory.gap_rules) authorities=$($inventory.authorities) boundaries=$($inventory.atomic_boundaries)"
foreach ($test in @($selfTestResults)) { Write-Output "Self-test $($test.name): $($test.result)" }
if (@($validation.issues).Count -gt 0) { foreach ($issue in @($validation.issues)) { Write-Output "ERROR: $issue" } }
if (-not $passed) { exit 1 }
