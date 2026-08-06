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

function Get-FamilyRule([object]$Model, [string]$FamilyName, [string]$RuleId) {
    $family = @($Model.fact_families | Where-Object { [string]$_.family -ceq $FamilyName })
    if ($family.Count -ne 1) { return $null }
    $rule = @($family[0].rules | Where-Object { [string]$_.rule_id -ceq $RuleId })
    if ($rule.Count -ne 1) { return $null }
    return $rule[0]
}

function Get-RuleOutcome([object]$Rule, [string]$ConstructorId) {
    if ($null -eq $Rule) { return $null }
    $outcome = @($Rule.outcomes | Where-Object { @($_.constructor_groups) -ccontains $ConstructorId })
    if ($outcome.Count -ne 1) { return $null }
    return $outcome[0]
}

function Convert-IncrementToCount([string]$Value) {
    switch ($Value) {
        'increment-one' { return 1 }
        'increment-two' { return 2 }
        'no-increment' { return 0 }
        'none' { return 0 }
        default { return $null }
    }
}

function Convert-DispositionToSummary([string]$Value) {
    if ($Value -ceq 'omit') { return 'omitted' }
    return $Value
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
    if ([string]$Model.status -cne 'accepted') { Add-Issue $issues 'model status must be accepted after WP4' }
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
            $constraints = @($_.state_space.admitted_regions) + @($_.state_space.invalid_regions) + @($_.state_space.excluded_regions)
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
        $constraintContainers = @()
        if (Test-HasProperty $family 'state_space') {
            $constraintContainers = @($family.state_space.admitted_regions) + @($family.state_space.invalid_regions) + @($family.state_space.excluded_regions)
            $conditionContainers += $constraintContainers
        }
        foreach ($constraint in $constraintContainers) {
            if (@($constraint.when).Count -eq 0) { Add-Issue $issues "constraint '$($constraint.constraint_id)' has an empty or catch-all predicate"; $checks.state_totality = $false }
            if (-not (Test-HasProperty $constraint 'authorities') -or @($constraint.authorities).Count -eq 0) { Add-Issue $issues "constraint '$($constraint.constraint_id)' has no authority references"; $checks.references_and_vocabularies = $false }
            foreach ($authority in @($constraint.authorities)) {
                if ($authorityIds -cnotcontains [string]$authority) { Add-Issue $issues "constraint '$($constraint.constraint_id)' references unknown authority '$authority'"; $checks.references_and_vocabularies = $false }
            }
        }
        foreach ($invalidRegion in @($family.state_space.invalid_regions)) {
            if ($boundaryIds -cnotcontains [string]$invalidRegion.atomic_boundary) { Add-Issue $issues "invalid region '$($invalidRegion.constraint_id)' references unknown atomic boundary '$($invalidRegion.atomic_boundary)'"; $checks.references_and_vocabularies = $false }
        }
        foreach ($container in $conditionContainers) {
            foreach ($condition in @($container.when)) {
                $dimension = [string]$condition.dimension
                if ($usedDimensions -cnotcontains $dimension -or $dimensionIds -cnotcontains $dimension) {
                    Add-Issue $issues "family '$familyName' condition references undeclared dimension '$dimension'"; $checks.references_and_vocabularies = $false
                    continue
                }
                if (@('equals','in','not-in') -cnotcontains [string]$condition.operator) { Add-Issue $issues "family '$familyName' condition uses unknown operator '$($condition.operator)'"; $checks.references_and_vocabularies = $false }
                if (@($condition.values).Count -eq 0) { Add-Issue $issues "family '$familyName' condition has no closed-vocabulary values"; $checks.references_and_vocabularies = $false }
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
        if (-not (Test-HasProperty $family 'state_space')) { continue }
        $counts = [ordered]@{ family = $familyName; raw = 0; admitted = 0; excluded = 0; invalid = 0; uncovered = 0; overlap = 0 }
        foreach ($state in @(Get-StateProduct -Model $Model -Dimensions $usedDimensions)) {
            $counts.raw++
            $admitted = @($family.state_space.admitted_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            $invalid = @($family.state_space.invalid_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            $excluded = @($family.state_space.excluded_regions | Where-Object { Get-ConditionMatches $state @($_.when) })
            $classificationMatches = $admitted.Count + $invalid.Count + $excluded.Count
            if ($classificationMatches -eq 0) { $counts.uncovered++; continue }
            if ($classificationMatches -gt 1) { $counts.overlap++; continue }
            if ($invalid.Count -eq 1) { $counts.invalid++; continue }
            if ($excluded.Count -eq 1) { $counts.excluded++; continue }
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

    $partialInvariant = $null
    if ((Test-HasProperty $Model 'cross_family_invariants') -and (Test-HasProperty $Model.cross_family_invariants 'partial_race_data')) {
        $partialInvariant = $Model.cross_family_invariants.partial_race_data
    }
    $partialOk = $null -ne $partialInvariant
    if ($partialOk) {
        foreach ($authority in @($partialInvariant.authorities)) {
            if ($authorityIds -cnotcontains [string]$authority) { $partialOk = $false; Add-Issue $issues "partial RACE/DATA invariant references unknown authority '$authority'" }
        }
        $contribution = $partialInvariant.contribution
        $fieldObligation = $partialInvariant.allowlisted_field
        $raceObligation = $partialInvariant.resolved_race
        $taxonomyObligation = $partialInvariant.taxonomy
        $gapObligation = $partialInvariant.gap
        $partialRule = Get-FamilyRule $Model ([string]$contribution.family) ([string]$contribution.rule_id)
        $fieldRule = Get-FamilyRule $Model ([string]$fieldObligation.family) ([string]$fieldObligation.rule_id)
        $raceRule = Get-FamilyRule $Model ([string]$raceObligation.family) ([string]$raceObligation.rule_id)
        $taxonomyRule = Get-FamilyRule $Model ([string]$taxonomyObligation.family) ([string]$taxonomyObligation.rule_id)
        $commonOutcome = Get-RuleOutcome $partialRule ([string]$contribution.common_constructor)
        $faceOutcome = Get-RuleOutcome $partialRule ([string]$contribution.face_gen_constructor)
        $fieldOutcome = Get-RuleOutcome $fieldRule ([string]$fieldObligation.constructor)
        $raceOutcome = Get-RuleOutcome $raceRule ([string]$raceObligation.constructor)
        $taxonomyOutcome = Get-RuleOutcome $taxonomyRule 'FC-TAXONOMY-TUPLE'
        $partialGap = @($partialRule.gap_effects)
        $shapeGap = @($Model.gap_rules | Where-Object { [string]$_.rule_id -ceq [string]$contribution.gap_rule_id })
        $ownerClaims = @($Model.fact_families | ForEach-Object { $_.rules } | ForEach-Object { $_.gap_effects } | Where-Object { [string]$_.owner_id -ceq [string]$gapObligation.owner_id })
        $resolvedPopulation = $null
        if ($shapeGap.Count -eq 1) { $resolvedPopulation = ([string]$shapeGap[0].population_template).Replace('{signature_lower}', 'race').Replace('{field_lower}', 'data') }
        $assignments = @($taxonomyObligation.assignments)
        $assignmentKeys = @($assignments | ForEach-Object { '{0}|{1}|{2}' -f $_.axis, $_.facet, $_.code })
        $requiredAssignmentKeys = @('technical-modification-surface|semantic-mechanism|surface.plugin-data','technical-modification-surface|realization-and-delivery|delivery.plugin-container')
        $partialOk = $partialOk -and
            [string]$partialInvariant.invariant_id -ceq 'INV-PARTIAL-RACE-DATA' -and
            $null -ne $commonOutcome -and [string]$commonOutcome.disposition -ceq [string]$contribution.common_disposition -and [string]$contribution.common_disposition -ceq 'exact_value' -and
            $null -ne $faceOutcome -and [string]$faceOutcome.disposition -ceq [string]$contribution.face_gen_disposition -and [string]$contribution.face_gen_disposition -ceq 'omit' -and
            $null -ne $partialRule -and [string]$partialRule.coverage_effect.population -ceq [string]$contribution.coverage_population -and [string]$contribution.coverage_population -ceq 'race-records' -and
            [string]$partialRule.coverage_effect.denominator -ceq [string]$contribution.coverage_denominator -and [string]$contribution.coverage_denominator -ceq 'increment-one' -and
            [string]$partialRule.coverage_effect.completion -ceq [string]$contribution.coverage_completion -and [string]$contribution.coverage_completion -ceq 'no-increment' -and
            $partialGap.Count -eq 1 -and [string]$partialGap[0].gap_rule_id -ceq [string]$contribution.gap_rule_id -and [string]$partialGap[0].owner_id -ceq [string]$contribution.gap_owner_id -and
            [string]$partialGap[0].scope -ceq [string]$contribution.gap_scope -and [int]$partialGap[0].affected_count_value -eq [int]$contribution.affected_count -and
            $null -ne $fieldOutcome -and [string]$fieldOutcome.disposition -ceq [string]$fieldObligation.disposition -and [string]$fieldObligation.disposition -ceq 'omit' -and
            [string]$fieldObligation.field -ceq 'DATA' -and [string]$fieldObligation.occurrence_state -ceq 'structural-only' -and [string]$fieldObligation.count_publication -ceq 'omitted' -and
            $null -ne $raceOutcome -and [string]$raceOutcome.disposition -ceq [string]$raceObligation.disposition -and [string]$raceObligation.disposition -ceq 'omit' -and
            $null -ne $taxonomyOutcome -and [string]$taxonomyOutcome.disposition -ceq 'exact_value' -and [int]$taxonomyObligation.subject_count -eq 1 -and
            $null -ne $taxonomyRule -and [string]$taxonomyRule.coverage_effect.population -ceq [string]$taxonomyObligation.coverage_population -and [string]$taxonomyObligation.coverage_population -ceq 'taxonomy-subjects' -and
            [string]$taxonomyRule.coverage_effect.denominator -ceq [string]$taxonomyObligation.coverage_denominator -and [string]$taxonomyObligation.coverage_denominator -ceq 'increment-one' -and
            [string]$taxonomyRule.coverage_effect.completion -ceq [string]$taxonomyObligation.coverage_completion -and [string]$taxonomyObligation.coverage_completion -ceq 'increment-one' -and
            (Test-SameStringSet $assignmentKeys $requiredAssignmentKeys) -and $assignments.Count -eq 2 -and [string]$taxonomyObligation.forbidden_semantic_source -ceq 'DATA' -and
            $shapeGap.Count -eq 1 -and [string]$resolvedPopulation -ceq [string]$gapObligation.population -and [string]$gapObligation.population -ceq 'unsupported-shapes:race:data' -and
            [string]$shapeGap[0].missing_capability -ceq [string]$gapObligation.missing_capability -and [string]$gapObligation.missing_capability -ceq 'allowlisted-record-shape-semantics' -and
            [string]$gapObligation.scope -ceq [string]$contribution.gap_scope -and [int]$gapObligation.affected_count -eq [int]$contribution.affected_count -and
            [string]$gapObligation.owner_id -ceq [string]$contribution.gap_owner_id -and [int]$gapObligation.aggregation_count -eq 1 -and $ownerClaims.Count -eq 1
    }
    if (-not $partialOk) { Add-Issue $issues 'structured partial RACE/DATA cross-family invariant drifted'; $checks.partial_race_data = $false }

    return [pscustomobject][ordered]@{ checks = [pscustomobject]$checks; issues = @($issues); partial_race_data = $partialInvariant }
}

$model = Read-Json $ModelPath 'model'
$schema = Read-Json $SchemaPath 'schema'
$script:familyCountBuffer = [System.Collections.Generic.List[object]]::new()
$validation = Invoke-ModelValidation $model $schema
$families = @($script:familyCountBuffer | Sort-Object family)

$selfTestResults = [System.Collections.Generic.List[object]]::new()
if (-not $SkipSelfTests) {
    $tests = @(
        @{ name = 'omitted-admitted-region-and-rule'; expect = "family 'result' has uncovered=1"; mutate = { param($m) $f=$m.fact_families[0]; $f.state_space.admitted_regions=@($f.state_space.admitted_regions|Where-Object constraint_id -cne 'SC-RESULT-PUBLISHED'); $f.rules=@($f.rules|Where-Object rule_id -cne 'P4-RESULT-PUBLISHED') } },
        @{ name = 'omitted-invalid-region'; expect = "family 'result' has uncovered=1"; mutate = { param($m) $f=$m.fact_families[0]; $f.state_space.invalid_regions=@($f.state_space.invalid_regions|Where-Object constraint_id -cne 'SC-RESULT-SNAPSHOT-WITH-FAILURE') } },
        @{ name = 'omitted-explicit-excluded-region'; expect = "family 'plugins' has uncovered=[1-9]"; mutate = { param($m) $f=$m.fact_families[1]; $f.state_space.excluded_regions=@($f.state_space.excluded_regions|Select-Object -Skip 1) } },
        @{ name = 'overlapping-admitted-regions'; expect = "family 'result' has uncovered=0, overlap=1"; mutate = { param($m) $f=$m.fact_families[0]; $copy=Copy-JsonObject $f.state_space.admitted_regions[0]; $copy.constraint_id='SC-RESULT-ADMITTED-OVERLAP-SELFTEST'; $f.state_space.admitted_regions=@($f.state_space.admitted_regions)+@($copy) } },
        @{ name = 'overlapping-invalid-regions'; expect = "family 'result' has uncovered=0, overlap=1"; mutate = { param($m) $f=$m.fact_families[0]; $copy=Copy-JsonObject $f.state_space.invalid_regions[0]; $copy.constraint_id='SC-RESULT-INVALID-OVERLAP-SELFTEST'; $f.state_space.invalid_regions=@($f.state_space.invalid_regions)+@($copy) } },
        @{ name = 'admitted-excluded-overlap'; expect = "family 'result' has uncovered=0, overlap=1"; mutate = { param($m) $f=$m.fact_families[0]; $source=$f.state_space.admitted_regions[0]; $copy=[pscustomobject][ordered]@{constraint_id='SC-RESULT-ADMITTED-EXCLUDED-SELFTEST';when=Copy-JsonObject $source.when;reason='self-test overlap';authorities=@('adr0029')}; $f.state_space.excluded_regions=@($f.state_space.excluded_regions)+@($copy) } },
        @{ name = 'invalid-excluded-overlap'; expect = "family 'result' has uncovered=0, overlap=1"; mutate = { param($m) $f=$m.fact_families[0]; $source=$f.state_space.invalid_regions[0]; $copy=[pscustomobject][ordered]@{constraint_id='SC-RESULT-INVALID-EXCLUDED-SELFTEST';when=Copy-JsonObject $source.when;reason='self-test overlap';authorities=@('adr0029')}; $f.state_space.excluded_regions=@($f.state_space.excluded_regions)+@($copy) } },
        @{ name = 'empty-catch-all-excluded-predicate'; expect = 'empty or catch-all predicate'; mutate = { param($m) $f=$m.fact_families[0]; $copy=[pscustomobject][ordered]@{constraint_id='SC-RESULT-CATCHALL-SELFTEST';when=@();reason='self-test catch-all';authorities=@('adr0029')}; $f.state_space.excluded_regions=@($f.state_space.excluded_regions)+@($copy) } },
        @{ name = 'unknown-invalid-atomic-boundary'; expect = 'references unknown atomic boundary'; mutate = { param($m) $m.fact_families[0].state_space.invalid_regions[0].atomic_boundary='AB-UNKNOWN-SELFTEST' } },
        @{ name = 'unknown-constraint-authority'; expect = 'references unknown authority'; mutate = { param($m) $m.fact_families[0].state_space.admitted_regions[0].authorities[0]='unknown-selftest-authority' } },
        @{ name = 'duplicate-stable-id'; expect = 'duplicate rule ID'; mutate = { param($m) $m.fact_families[0].rules = @($m.fact_families[0].rules) + @(Copy-JsonObject $m.fact_families[0].rules[0]) } },
        @{ name = 'overlapping-rules'; expect = 'matches 2 rules'; mutate = { param($m) $copy = Copy-JsonObject $m.fact_families[0].rules[0]; $copy.rule_id = 'P4-RESULT-OVERLAP-SELFTEST'; $m.fact_families[0].rules = @($m.fact_families[0].rules) + @($copy) } },
        @{ name = 'invalid-evidence-layer-dependency'; expect = 'lacks semantic evidence'; mutate = { param($m) (@($m.fact_families | Where-Object family -ceq 'taxonomy')[0].rules | Where-Object rule_id -ceq 'P4-TAXONOMY-SEMANTIC').minimum_layer = 'structural' } },
        @{ name = 'unknown-dimension'; expect = 'undeclared dimension'; mutate = { param($m) $m.fact_families[0].rules[0].when[0].dimension = 'undeclared-selftest' } },
        @{ name = 'unknown-closed-vocabulary-value'; expect = 'outside dimension'; mutate = { param($m) $m.fact_families[0].rules[0].when[0].values[0] = 'outside-selftest-vocabulary' } },
        @{ name = 'unknown-constructor-reference'; expect = 'references unknown constructor'; mutate = { param($m) $m.fact_families[0].rules[0].outcomes[0].constructor_groups[0] = 'FC-UNKNOWN-SELFTEST' } },
        @{ name = 'unknown-rule-authority-reference'; expect = 'references unknown authority'; mutate = { param($m) $m.fact_families[0].rules[0].authorities[0] = 'unknown-selftest-authority' } },
        @{ name = 'inconsistent-coverage-arithmetic'; expect = 'partial RACE/DATA contribution arithmetic'; mutate = { param($m) (Get-FamilyRule $m 'race_contributions' 'P4-RACECONTRIB-PARTIAL-DATA').coverage_effect.completion = 'increment-one' } },
        @{ name = 'missing-required-gap'; expect = 'partial RACE/DATA contribution arithmetic'; mutate = { param($m) (Get-FamilyRule $m 'race_contributions' 'P4-RACECONTRIB-PARTIAL-DATA').gap_effects = @() } },
        @{ name = 'duplicate-gap-ownership'; expect = 'double-counted'; mutate = { param($m) $rules = @($m.fact_families | ForEach-Object { $_.rules } | Where-Object { @($_.gap_effects).Count -gt 0 }); $rules[1].gap_effects[0].owner_id = [string]$rules[0].gap_effects[0].owner_id } },
        @{ name = 'invalid-partial-race-data-arithmetic'; expect = 'partial RACE/DATA taxonomy arithmetic'; mutate = { param($m) (Get-FamilyRule $m 'taxonomy' 'P4-TAXONOMY-PARTIAL-RACE').coverage_effect.completion = 'no-increment' } },
        @{ name = 'invalid-partial-race-data-assignment'; expect = 'structured partial RACE/DATA'; mutate = { param($m) $m.cross_family_invariants.partial_race_data.taxonomy.assignments[0].code='surface.invalid-selftest' } },
        @{ name = 'invalid-partial-race-data-field-publication'; expect = 'structured partial RACE/DATA'; mutate = { param($m) (Get-FamilyRule $m 'allowlisted_fields' 'P4-FIELDS-STRUCTURAL-ONLY').outcomes[0].disposition='exact_value' } },
        @{ name = 'invalid-partial-race-data-gap-resolution'; expect = 'structured partial RACE/DATA'; mutate = { param($m) $m.cross_family_invariants.partial_race_data.gap.population='unsupported-shapes:race:wrong' } }
    )
    foreach ($test in $tests) {
        $mutation = Copy-JsonObject $model
        & $test.mutate $mutation
        $script:familyCountBuffer = [System.Collections.Generic.List[object]]::new()
        $mutated = Invoke-ModelValidation $mutation $schema
        $issueText = @($mutated.issues) -join "`n"
        $rejected = @($mutated.issues).Count -gt 0 -and $issueText -match [string]$test.expect
        $matchedEvidence = @($mutated.issues | Where-Object { [string]$_ -match [string]$test.expect } | Select-Object -First 1)
        $selfTestResults.Add([pscustomobject][ordered]@{ name = [string]$test.name; result = $(if ($rejected) { 'rejected' } else { 'unexpectedly-passed' }); evidence = $(if ($matchedEvidence.Count) { [string]$matchedEvidence[0] } else { '' }) })
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
    excluded_regions = @($model.fact_families | ForEach-Object { $_.state_space.excluded_regions }).Count
}
$allSelfTestsPassed = @($selfTestResults | Where-Object result -cne 'rejected').Count -eq 0
$passed = @($validation.issues).Count -eq 0 -and $allSelfTestsPassed
$validatedPartial = $validation.partial_race_data
$partialSummary = $null
if ($null -ne $validatedPartial) {
    $partialSummary = [pscustomobject][ordered]@{
        invariant_id = [string]$validatedPartial.invariant_id
        race_records_denominator = Convert-IncrementToCount ([string]$validatedPartial.contribution.coverage_denominator)
        race_records_completion = Convert-IncrementToCount ([string]$validatedPartial.contribution.coverage_completion)
        taxonomy_subjects_denominator = Convert-IncrementToCount ([string]$validatedPartial.taxonomy.coverage_denominator)
        taxonomy_subjects_completion = Convert-IncrementToCount ([string]$validatedPartial.taxonomy.coverage_completion)
        taxonomy_subject_count = [int]$validatedPartial.taxonomy.subject_count
        generic_technical_assignments = @($validatedPartial.taxonomy.assignments)
        data_count = [string]$validatedPartial.allowlisted_field.count_publication
        face_gen_head = Convert-DispositionToSummary ([string]$validatedPartial.contribution.face_gen_disposition)
        resolved_race = Convert-DispositionToSummary ([string]$validatedPartial.resolved_race.disposition)
        gap_count = [int]$validatedPartial.gap.aggregation_count
        gap_population = [string]$validatedPartial.gap.population
        missing_capability = [string]$validatedPartial.gap.missing_capability
        gap_scope = [string]$validatedPartial.gap.scope
        affected_count = [int]$validatedPartial.gap.affected_count
        gap_owner_id = [string]$validatedPartial.gap.owner_id
    }
}
$summary = [pscustomobject][ordered]@{
    validator = 'infinium.m1-slice4.protocol-4-totality-validator/v2'
    model_id = [string]$model.model_id
    model_version = [string]$model.version
    status = $(if ($passed) { 'passed' } else { 'failed' })
    totals = [pscustomobject]$totals
    families = $families
    checks = $validation.checks
    inventory = [pscustomobject]$inventory
    partial_race_data = $partialSummary
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
Write-Output "Inventory: families=$($inventory.families) coverage=$($inventory.coverage_populations) dimensions=$($inventory.dimensions) vocabularies=$($inventory.vocabularies) constructors=$($inventory.constructor_groups) rules=$($inventory.publication_rules) gaps=$($inventory.gap_rules) authorities=$($inventory.authorities) boundaries=$($inventory.atomic_boundaries) excluded_regions=$($inventory.excluded_regions)"
foreach ($test in @($selfTestResults)) { Write-Output "Self-test $($test.name): $($test.result); evidence=$($test.evidence)" }
if (@($validation.issues).Count -gt 0) { foreach ($issue in @($validation.issues)) { Write-Output "ERROR: $issue" } }
if (-not $passed) { exit 1 }
