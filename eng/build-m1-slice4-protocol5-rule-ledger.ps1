[CmdletBinding()]
param(
    [string]$OutputPath = 'docs/evaluation/specifications/m1-slice4-protocol-5-rule-coverage-ledger.json'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$basePath = Join-Path $root 'docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.json'
$successorPath = Join-Path $root 'docs/evaluation/specifications/m1-slice4-protocol-5-successor-model.json'
$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $root $OutputPath }

function Read-Json([string]$Path) {
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Copy-Value([object]$Value) {
    return $Value | ConvertTo-Json -Depth 100 -Compress | ConvertFrom-Json
}

function Get-OrdinalUnique([object[]]$Values) {
    $set=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($value in @($Values)){if($null-ne$value-and-not[string]::IsNullOrEmpty([string]$value)){[void]$set.Add([string]$value)}}
    $items=[string[]]@($set);[Array]::Sort($items,[StringComparer]::Ordinal);return $items
}

function Test-Condition([object]$State, [object[]]$Conditions) {
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

function Get-StateProduct([object]$Model, [string[]]$Dimensions, [int]$Index = 0, [object]$Prefix = $null) {
    if ($null -eq $Prefix) { $Prefix = [ordered]@{} }
    if ($Index -ge $Dimensions.Count) { [pscustomobject]$Prefix; return }
    $dimension = $Dimensions[$Index]
    foreach ($value in @($Model.dimensions.$dimension.values)) {
        $next = [ordered]@{}
        foreach ($key in $Prefix.Keys) { $next[$key] = $Prefix[$key] }
        $next[$dimension] = [string]$value
        Get-StateProduct $Model $Dimensions ($Index + 1) $next
    }
}

function Expand-FactTemplate([string]$Template) {
    $seed = $Template -replace '\{seg\([^}]+\)\}', 'x' -replace '\{[^}|]+_D4\}', '0000'
    $pending = [Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($seed)
    $output = [Collections.Generic.List[string]]::new()
    while ($pending.Count -gt 0) {
        $value = $pending.Dequeue()
        $match = [regex]::Match($value, '\{([^{}|]+(?:\|[^{}|]+)+)\}')
        if (-not $match.Success) { $output.Add($value); continue }
        foreach ($choice in $match.Groups[1].Value.Split('|')) {
            $pending.Enqueue($value.Substring(0, $match.Index) + $choice + $value.Substring($match.Index + $match.Length))
        }
    }
    return @($output)
}

function Split-FactId([string]$FactId) {
    $segments = @($FactId.Split('/'))
    $family = $segments[0]
    if ($family -ceq 'result') {
        return [pscustomobject]@{ object_id = 'root'; property_id = ($segments[1..($segments.Count - 1)] -join '/') }
    }
    $identitySegments = if ($family -ceq 'taxonomy') { 7 } elseif (@('gaps', 'result_gaps') -ccontains $family) { 2 } else { 1 }
    return [pscustomobject]@{
        object_id = ($segments[1..$identitySegments] -join '/')
        property_id = ($segments[($identitySegments + 1)..($segments.Count - 1)] -join '/')
    }
}

function Get-ConcreteState([object]$State) {
    $copy = [ordered]@{}
    foreach ($property in $State.PSObject.Properties) { $copy[$property.Name] = [string]$property.Value }
    return [pscustomobject]$copy
}

function Get-EffectValue([string]$Effect) {
    if ($Effect -ceq 'increment-two') { return 2 }
    if ($Effect -ceq 'increment-one') { return 1 }
    return 0
}

function Get-GapPair([object]$GapRule) {
    return [pscustomobject][ordered]@{
        population = (([string]$GapRule.population_template) -replace '\{[^}]+\}', 'x')
        missing_capability = [string]$GapRule.missing_capability
    }
}

function Test-GapOwnsPopulation([object[]]$Gaps, [string]$Population) {
    foreach ($gap in @($Gaps)) {
        $gapPopulation = [string]$gap.population
        if ($gapPopulation -ceq $Population) { return $true }
        if ($Population -ceq 'unsupported-records' -and $gapPopulation.StartsWith('unsupported-records:', [StringComparison]::Ordinal)) { return $true }
        if (@('npc-records','race-records','placed-reference-records') -ccontains $Population -and ($gapPopulation.StartsWith('unsupported-fields:', [StringComparison]::Ordinal) -or $gapPopulation.StartsWith('unsupported-shapes:', [StringComparison]::Ordinal))) { return $true }
    }
    return $false
}

function Get-CoverageLifecycle([int]$Denominator, [int]$Completed, [bool]$HasGap) {
    if ($Denominator -eq 0) { return 'completed' }
    if ($Completed -eq $Denominator -and -not $HasGap) { return 'completed' }
    if ($Completed -eq $Denominator -and $HasGap) { return 'completed_with_gaps' }
    if ($Completed -eq 0 -and $HasGap) { return 'unsupported' }
    if ($Completed -lt $Denominator -and $HasGap) { return 'completed_with_gaps' }
    return 'invalid'
}

function Get-FaceGenApplicability([string]$RuleId) {
    switch ($RuleId) {
        'P4-FACEGEN-DELETED' { return 'not_applicable_deleted_winner' }
        'P4-FACEGEN-TEMPLATE-UNKNOWN' { return 'unknown_template_traits_decision' }
        'P4-FACEGEN-TEMPLATE-TRAITS' { return 'not_applicable_template_traits' }
        'P4-FACEGEN-RACE-NULL' { return 'unknown_race' }
        'P4-FACEGEN-RACE-UNRESOLVED' { return 'unknown_race' }
        'P4-FACEGEN-RACE-NO-HEAD' { return 'not_applicable_race_without_face_gen_head' }
        default { return 'applicable' }
    }
}

function New-WitnessProperty([object]$Row, [string]$Family, [string]$RuleId, [object]$State, [string]$FactType) {
    $id=[string]$Row.property_id;$factId=[string]$Row.fact_id;$asset=if($factId-match'/mesh/'){'mesh'}elseif($factId-match'/tint/'){'tint'}else{$null}
    $availability=if($null-ne$State-and$null-ne$State.PSObject.Properties['asset_availability']){[string]$State.asset_availability}else{$null}
    if($Family-ceq'face_gen'-and$id-match'/(?:provider_ids)/'-and$availability-cne'present'){return $null}
    $type='string';$value='x'
    if($id-match'(^|/)(load_order|origin_local_id|raw_flags|configuration_flags|template_flags|ordinal|count|denominator|completed)$'-or$id-match'^taxonomy_version/(major|minor|patch)$'){$type='integer';$value=1}
    elseif($id-match'(^|/)(deleted|compressed|uses_template|templates_traits|ai_data_present|face_gen_head|present|exact_absence_known|snapshot_present|failure_present)$'){$type='boolean';$value=$true}
    elseif($id-match'^placement/(position|rotation)/(x|y|z)$'){$type='number';$value=[double]1.5}
    if($id-ceq'snapshot_present'){$type='boolean';$value=($RuleId-ceq'P4-RESULT-PUBLISHED')}
    elseif($id-ceq'failure_present'){$type='boolean';$value=($RuleId-ceq'P4-RESULT-NO-SNAPSHOT')}
    elseif($id-ceq'plugin_name'-or$id-match'(^|/)origin_plugin$'-or$id-match'(^|/)source_plugin$'){$type='string';$value='x.esp'}
    elseif($id-match'(^|/)form_key$'-or$id-ceq'npc_form_key'-or$id-ceq'target_form_key'){$type='string';$value='00000001:x.esp'}
    elseif($id-match'(^|/)signature$'){$type='string';$value=if($Family-match'race'){'RACE'}elseif($Family-match'reference'){'REFR'}else{'NPC_'}}
    elseif($id-ceq'master_style'){$type='string';$value='full'}
    elseif($id-ceq'kind'){$type='string';$value=if($Family-match'race'){'race'}elseif($Family-match'reference'){'reference'}else{'npc'}}
    elseif($id-ceq'field'){$type='string';$value='EDID'}
    elseif($id-ceq'state' -and $Family-notin @('coverage','result')){$type='string';$value='resolved'}
    elseif($id-ceq'component'){$type='null';$value=$null}
    elseif($Family-ceq'face_gen'-and$id-ceq'applicability'){$type='string';$value=Get-FaceGenApplicability $RuleId}
    elseif($Family-ceq'face_gen'-and$id-match'normalized_relative_path$'){$type='string';$value=if($asset-ceq'mesh'){'meshes/actors/character/facegendata/facegeom/x.esp/00000001.nif'}else{'textures/actors/character/facegendata/facetint/x.esp/00000001.dds'}}
    elseif($Family-ceq'face_gen'-and$id-match'provider_ids/0000$'){$type='string';$value='provider-a'}
    elseif($Family-ceq'face_gen'-and$id-match'winner_provider_id$'){if($availability-ceq'present'){$type='string';$value='provider-a'}else{$type='null';$value=$null}}
    elseif($Family-ceq'face_gen'-and$id-match'(^|/)present$'){$type='boolean';$value=($availability-ceq'present')}
    elseif($Family-ceq'face_gen'-and$id-match'exact_absence_known$'){$type='boolean';$value=($availability-ceq'absent')}
    elseif($Family-ceq'taxonomy'-and$id-ceq'code' -and $RuleId-ceq'P4-TAXONOMY-TYPED-NULL'){$type='null';$value=$null}
    elseif($Family-ceq'taxonomy'-and$id-ceq'code'){$type='string';$value='surface.plugin-data'}
    elseif($Family-ceq'taxonomy'-and$id-ceq'applicability'){$type='string';$value=if($RuleId-ceq'P4-TAXONOMY-TYPED-NULL'){'unknown'}else{'assigned'}}
    elseif($Family-ceq'taxonomy'-and$id-ceq'role'){$type='string';$value='observed'}
    return [pscustomobject][ordered]@{object_id=[string]$Row.object_id;property_id=$id;fact_id=$factId;constructor_group=[string]$Row.constructor_group;disposition=[string]$Row.disposition;fact_type=$FactType;value_type=$type;value=$value}
}

$base = Read-Json $basePath
$successor = Read-Json $successorPath
$model = Copy-Value $base
$model.model_id = [string]$successor.model_id
$model.version = [string]$successor.version
$model.gap_rules = @($model.gap_rules) + @($successor.delta.added_gap_rules)
foreach ($replacement in @($successor.delta.replaced_publication_rules)) {
    $family = @($model.fact_families | Where-Object { [string]$_.family -ceq [string]$replacement.family })[0]
    for ($i = 0; $i -lt @($family.rules).Count; $i++) {
        if ([string]$family.rules[$i].rule_id -ceq [string]$replacement.replaces_rule_id) {
            $family.rules[$i] = Copy-Value $replacement.rule
            break
        }
    }
}

$constructors = @{}
foreach ($family in @($model.fact_families)) {
    foreach ($constructor in @($family.constructor_groups)) { $constructors[[string]$constructor.id] = $constructor }
}

$entries = [Collections.Generic.List[object]]::new()
foreach ($family in @($model.fact_families)) {
    $admittedByRule = @{}
    $dimensions = @($family.dimensions_used | ForEach-Object { [string]$_ })
    $states = if ($dimensions.Count -eq 0) { @([pscustomobject]@{}) } else { @(Get-StateProduct $model $dimensions) }
    foreach ($state in $states) {
        $regions = @($family.state_space.admitted_regions | Where-Object { Test-Condition $state @($_.when) })
        $rules = @($family.rules | Where-Object { Test-Condition $state @($_.when) })
        if ($regions.Count -eq 1 -and $rules.Count -eq 1) {
            $id = [string]$rules[0].rule_id
            if (-not $admittedByRule.ContainsKey($id)) { $admittedByRule[$id] = [Collections.Generic.List[object]]::new() }
            $admittedByRule[$id].Add([pscustomobject][ordered]@{
                constraint_id = [string]$regions[0].constraint_id
                state = Get-ConcreteState $state
            })
        }
    }

    foreach ($rule in @($family.rules)) {
        $ruleId = [string]$rule.rule_id
        $isAdmitted = $admittedByRule.ContainsKey($ruleId)
        $bindings = [Collections.Generic.List[object]]::new()
        $factRows = [Collections.Generic.List[object]]::new()
        foreach ($outcome in @($rule.outcomes)) {
            foreach ($groupId in @($outcome.constructor_groups)) {
                $bindings.Add([pscustomobject][ordered]@{
                    constructor_group = [string]$groupId
                    family = [string]$family.family
                    disposition = [string]$outcome.disposition
                })
                if (@('exact_value', 'typed_null', 'accepted_unknown', 'mixed_by_constructor') -ccontains [string]$outcome.disposition) {
                    $constructor = $constructors[[string]$groupId]
                    foreach ($template in @($constructor.fact_id_templates)) {
                        foreach ($factId in @(Expand-FactTemplate ([string]$template))) {
                            $split = Split-FactId $factId
                            $factRows.Add([pscustomobject][ordered]@{
                                object_id = [string]$split.object_id
                                property_id = [string]$split.property_id
                                fact_id = [string]$factId
                                constructor_group = [string]$groupId
                                disposition = [string]$outcome.disposition
                            })
                        }
                    }
                }
            }
        }

        $canonicalState = $null
        if ($isAdmitted) { $canonicalState = Copy-Value @($admittedByRule[$ruleId])[0].state }
        switch ($ruleId) {
            'P4-COVERAGE-NO-SNAPSHOT' { $canonicalState = [pscustomobject][ordered]@{ publication='no-snapshot'; coverage_denominator='zero'; coverage_lifecycle='completed'; gap_scope='none' } }
            'P4-COVERAGE-ZERO' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; coverage_denominator='zero'; coverage_lifecycle='completed'; gap_scope='none' } }
            'P4-COVERAGE-COMPLETE' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; coverage_denominator='positive'; coverage_lifecycle='completed'; gap_scope='none' } }
            'P4-COVERAGE-INCOMPLETE' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; coverage_denominator='positive'; coverage_lifecycle='completed-with-gaps'; gap_scope='snapshot-and-result' } }
            'P4-GAPS-NONE' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; gap_scope='none' } }
            'P4-GAPS-EMIT' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; gap_scope='snapshot-and-result' } }
            'P4-GAPS-RESOLVED' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; gap_scope='none' } }
            'P4-RESULTGAPS-NONE' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; gap_scope='none' } }
            'P4-RESULTGAPS-WITH-SNAPSHOT' { $canonicalState = [pscustomobject][ordered]@{ publication='snapshot-published'; gap_scope='snapshot-and-result' } }
            'P4-RESULTGAPS-NO-SNAPSHOT' { $canonicalState = [pscustomobject][ordered]@{ publication='no-snapshot'; gap_scope='result' } }
            'P4-NPCCONTRIB-RESOLVED' { $canonicalState.link_state='resolved';$canonicalState.target_resolution='resolved' }
            'P4-REFRCONTRIB-RESOLVED' { $canonicalState.link_state='resolved';$canonicalState.target_resolution='resolved';$canonicalState.member_presence='present-once' }
            'P4-NPCS-RESOLVED' { $canonicalState.link_state='resolved';$canonicalState.target_resolution='resolved' }
            'P4-REFRS-RESOLVED' { $canonicalState.link_state='resolved';$canonicalState.target_resolution='resolved';$canonicalState.member_presence='present-once' }
        }

        $constructorTemplateInventory=@(Get-OrdinalUnique @($factRows.fact_id))
        $modelFactRows=@($factRows);$factRows=[Collections.Generic.List[object]]::new()
        foreach($row in $modelFactRows){$constructor=$constructors[[string]$row.constructor_group];$property=New-WitnessProperty $row ([string]$family.family) $ruleId $canonicalState ([string]@($constructor.fact_type)[0]);if($null-ne$property){$factRows.Add($property)}}
        if([string]$family.family-ceq'taxonomy'-and$factRows.Count-gt0){
            $taxonomySpecs=[Collections.Generic.List[object]]::new()
            if(@('P4-TAXONOMY-TECHNICAL-CORE','P4-TAXONOMY-PARTIAL-RACE','P4-TAXONOMY-UNSUPPORTED-RECORD')-ccontains$ruleId){
                $taxonomySpecs.Add([pscustomobject]@{axis='technical-modification-surface';facet='semantic-mechanism';code='surface.plugin-data';applicability='assigned';role='observed';subject_type='record-contribution'})
                $taxonomySpecs.Add([pscustomobject]@{axis='technical-modification-surface';facet='realization-and-delivery';code='delivery.plugin-container';applicability='assigned';role='observed';subject_type='record-contribution'})
            }elseif($ruleId-ceq'P4-TAXONOMY-SEMANTIC'){$taxonomySpecs.Add([pscustomobject]@{axis='affected-game-system-or-content-area';facet='affected-area';code='area.actors.ai-packages';applicability='assigned';role='established';subject_type='record-semantic-subject'})}
            elseif($ruleId-ceq'P4-TAXONOMY-TYPED-NULL'){$taxonomySpecs.Add([pscustomobject]@{axis='declared-purpose-and-intended-feature-area';facet='purpose-kind';code=$null;applicability='unknown';role='declared';subject_type='record-semantic-subject'})}
            $templateRows=@($factRows);$factRows=[Collections.Generic.List[object]]::new()
            foreach($spec in $taxonomySpecs){$codeSegment=if($null-eq$spec.code){'null'}else{[string]$spec.code};$objectId="x/$($spec.subject_type)/$($spec.axis)/$($spec.facet)/$codeSegment/$($spec.applicability)/$($spec.role)";foreach($templateRow in $templateRows){$property=Copy-Value $templateRow;$property.object_id=$objectId;$property.fact_id="taxonomy/$objectId/$($property.property_id)";switch([string]$property.property_id){'taxonomy_id'{$property.value="taxonomy:$objectId"}'canonical_subject'{$property.value='x'}'subject_type'{$property.value=[string]$spec.subject_type}'axis'{$property.value=[string]$spec.axis}'facet'{$property.value=[string]$spec.facet}'applicability'{$property.value=[string]$spec.applicability}'role'{$property.value=[string]$spec.role}'code'{if($null-eq$spec.code){$property.value_type='null';$property.value=$null}else{$property.value_type='string';$property.value=[string]$spec.code}}};$factRows.Add($property)}}
        }
        $objects=[Collections.Generic.List[object]]::new();foreach($objectId in @(Get-OrdinalUnique @($factRows.object_id))){$rows=@($factRows|Where-Object{[string]$_.object_id-ceq[string]$objectId});$objects.Add([pscustomobject][ordered]@{object_id=[string]$objectId;constructor_groups=@(Get-OrdinalUnique @($rows.constructor_group));property_templates=@(Get-OrdinalUnique @($rows.property_id));fact_templates=@(Get-OrdinalUnique @($rows.fact_id));properties=@($rows|ForEach-Object{[pscustomobject][ordered]@{property_id=[string]$_.property_id;fact_id=[string]$_.fact_id;constructor_group=[string]$_.constructor_group;disposition=[string]$_.disposition;fact_type=[string]$_.fact_type;value_type=[string]$_.value_type;value=$_.value}})})}

        $publication = if ($null -ne $canonicalState -and $null -ne $canonicalState.PSObject.Properties['publication']) { [string]$canonicalState.publication } else { 'snapshot-published' }
        $coverageEffects = @([pscustomobject][ordered]@{
            population = $rule.coverage_effect.population
            denominator = [string]$rule.coverage_effect.denominator
            completion = [string]$rule.coverage_effect.completion
            state_effect = [string]$rule.coverage_effect.state_effect
        })
        if ($null -ne $rule.coverage_effect.PSObject.Properties['additional_population_effects']) {
            foreach ($effect in @($rule.coverage_effect.additional_population_effects)) {
                $coverageEffects += [pscustomobject][ordered]@{
                    population = $effect.population
                    denominator = [string]$effect.denominator
                    completion = [string]$effect.completion
                    state_effect = [string]$effect.state_effect
                }
            }
        }
        $gapEffects = @($rule.gap_effects | ForEach-Object {
            [pscustomobject][ordered]@{
                gap_rule_id = [string]$_.gap_rule_id
                owner_id = [string]$_.owner_id
                affected_count = [string]$_.affected_count
                scope = [string]$_.scope
            }
        })
        $mirrorEffects = @($gapEffects | Where-Object { [string]$_.scope -ceq 'snapshot-and-result' } | ForEach-Object {
            [pscustomobject][ordered]@{ gap_rule_id=[string]$_.gap_rule_id; required=$true; scope='result' }
        })

        $supportCoverage = [Collections.Generic.List[object]]::new()
        $supportGaps = [Collections.Generic.List[object]]::new()
        $supportResultGaps = [Collections.Generic.List[object]]::new()
        $snapshot = ($publication -cne 'no-snapshot')
        if ($snapshot) {
            $rowValues = [ordered]@{}
            foreach ($population in @($model.coverage_registry.population)) { $rowValues[[string]$population] = [pscustomobject]@{ denominator=0; completed=0 } }
            if ([string]$family.family -ceq 'coverage') {
                if ($ruleId -ceq 'P4-COVERAGE-COMPLETE') { $rowValues['face-gen-loose-assets'].denominator=1; $rowValues['face-gen-loose-assets'].completed=1 }
                elseif ($ruleId -ceq 'P4-COVERAGE-INCOMPLETE') { $rowValues['face-gen-loose-assets'].denominator=2; $rowValues['face-gen-loose-assets'].completed=1 }
            } else {
                foreach ($effect in @($coverageEffects)) {
                    if ($null -eq $effect.population -or -not $rowValues.Contains([string]$effect.population)) { continue }
                    $rowValues[[string]$effect.population].denominator += Get-EffectValue ([string]$effect.denominator)
                    $rowValues[[string]$effect.population].completed += Get-EffectValue ([string]$effect.completion)
                }
            }
            foreach ($effect in @($gapEffects)) {
                $definition = @($model.gap_rules | Where-Object { [string]$_.rule_id -ceq [string]$effect.gap_rule_id })[0]
                $pair = Get-GapPair $definition
                if ([string]$effect.scope -cin @('snapshot','snapshot-and-result')) { $supportGaps.Add([pscustomobject][ordered]@{ publication_rule_id='P4-GAPS-EMIT'; gap_rule_id=[string]$effect.gap_rule_id; population=[string]$pair.population; missing_capability=[string]$pair.missing_capability; affected=1 }) }
                if ([string]$effect.scope -cin @('result','snapshot-and-result')) { $supportResultGaps.Add([pscustomobject][ordered]@{ publication_rule_id='P4-RESULTGAPS-WITH-SNAPSHOT'; gap_rule_id=[string]$effect.gap_rule_id; population=[string]$pair.population; missing_capability=[string]$pair.missing_capability; affected=1 }) }
            }
            if ([string]$family.family -ceq 'coverage' -and $ruleId -ceq 'P4-COVERAGE-INCOMPLETE') {
                $definition = @($model.gap_rules | Where-Object { [string]$_.rule_id -ceq 'P5-GAP-LOOSE-AVAILABILITY' })[0]; $pair=Get-GapPair $definition
                $supportGaps.Add([pscustomobject][ordered]@{ publication_rule_id='P4-GAPS-EMIT'; gap_rule_id='P5-GAP-LOOSE-AVAILABILITY'; population=[string]$pair.population; missing_capability=[string]$pair.missing_capability; affected=1 })
                $supportResultGaps.Add([pscustomobject][ordered]@{ publication_rule_id='P4-RESULTGAPS-WITH-SNAPSHOT'; gap_rule_id='P5-GAP-LOOSE-AVAILABILITY'; population=[string]$pair.population; missing_capability=[string]$pair.missing_capability; affected=1 })
            }
            if (([string]$family.family -ceq 'gaps' -and $ruleId -ceq 'P4-GAPS-EMIT') -or ([string]$family.family -ceq 'result_gaps' -and $ruleId -ceq 'P4-RESULTGAPS-WITH-SNAPSHOT')) {
                $definition = @($model.gap_rules | Where-Object { [string]$_.rule_id -ceq 'P4-GAP-LOCALIZED' })[0]; $pair=Get-GapPair $definition
                $supportGaps.Add([pscustomobject][ordered]@{ publication_rule_id='P4-GAPS-EMIT'; gap_rule_id='P4-GAP-LOCALIZED'; population=[string]$pair.population; missing_capability=[string]$pair.missing_capability; affected=1 })
                $supportResultGaps.Add([pscustomobject][ordered]@{ publication_rule_id='P4-RESULTGAPS-WITH-SNAPSHOT'; gap_rule_id='P4-GAP-LOCALIZED'; population=[string]$pair.population; missing_capability=[string]$pair.missing_capability; affected=1 })
            }
            foreach ($population in @($model.coverage_registry.population)) {
                $row = $rowValues[[string]$population]; $hasGap=Test-GapOwnsPopulation @($supportGaps) ([string]$population); $stateValue=Get-CoverageLifecycle $row.denominator $row.completed $hasGap
                $sourceRule = if ($row.denominator -eq 0) { 'P4-COVERAGE-ZERO' } elseif ($stateValue -ceq 'completed') { 'P4-COVERAGE-COMPLETE' } else { 'P4-COVERAGE-INCOMPLETE' }
                if ([string]$family.family -ceq 'coverage' -and $ruleId -ceq 'P4-COVERAGE-ZERO') { $sourceRule='P4-COVERAGE-ZERO' }
                $supportCoverage.Add([pscustomobject][ordered]@{ population=[string]$population; denominator=[int]$row.denominator; completed=[int]$row.completed; state=[string]$stateValue; publication_rule_id=$sourceRule })
            }
        } elseif ([string]$family.family -ceq 'result_gaps' -and $ruleId -ceq 'P4-RESULTGAPS-NO-SNAPSHOT') {
            $definition=@($model.gap_rules)[0];$pair=Get-GapPair $definition
            $supportResultGaps.Add([pscustomobject][ordered]@{ publication_rule_id='P4-RESULTGAPS-NO-SNAPSHOT'; gap_rule_id=[string]$definition.rule_id; population=[string]$pair.population; missing_capability=[string]$pair.missing_capability; affected=1 })
        }

        # Support-family witnesses use the exact concrete row selected above.  The
        # constructor inventory remains templated, while the witness fact ids and
        # values are the canonical projection that an implementation must emit.
        if ($factRows.Count -gt 0 -and [string]$family.family -ceq 'coverage') {
            $targets=@($supportCoverage|Where-Object{[string]$_.publication_rule_id-ceq$ruleId});if($targets.Count-lt1){throw "No concrete coverage target row for $ruleId."}
            $templateProperties=@($factRows);$factRows=[Collections.Generic.List[object]]::new();foreach($target in $targets){foreach($templateProperty in $templateProperties){$property=Copy-Value $templateProperty;$property.object_id=[string]$target.population;$property.fact_id="coverage/$($target.population)/$($property.property_id)";switch([string]$property.property_id){'population'{$property.value=[string]$target.population}'denominator'{$property.value_type='integer';$property.value=[int]$target.denominator}'completed'{$property.value_type='integer';$property.value=[int]$target.completed}'state'{$property.value=[string]$target.state}};$factRows.Add($property)}}
        }
        elseif ($factRows.Count -gt 0 -and [string]$family.family -ceq 'gaps') {
            $target=@($supportGaps|Where-Object{[string]$_.publication_rule_id-ceq$ruleId}|Select-Object -First 1)
            if($target.Count-ne1){throw "No concrete gap target row for $ruleId."};$target=$target[0];$objectId=("$($target.population)/$($target.missing_capability)").Replace(':','%3A')
            foreach($property in $factRows){$property.object_id=$objectId;$property.fact_id="gaps/$objectId/$($property.property_id)";switch([string]$property.property_id){'population'{$property.value=[string]$target.population}'missing_capability'{$property.value=[string]$target.missing_capability}'denominator'{$property.value_type='integer';$property.value=[int]$target.affected}}}
        }
        elseif ($factRows.Count -gt 0 -and [string]$family.family -ceq 'result_gaps') {
            $target=@($supportResultGaps|Where-Object{[string]$_.publication_rule_id-ceq$ruleId}|Select-Object -First 1)
            if($target.Count-ne1){throw "No concrete result-gap target row for $ruleId."};$target=$target[0];$objectId=("$($target.population)/$($target.missing_capability)").Replace(':','%3A')
            foreach($property in $factRows){$property.object_id=$objectId;$property.fact_id="result_gaps/$objectId/$($property.property_id)";switch([string]$property.property_id){'population'{$property.value=[string]$target.population}'missing_capability'{$property.value=[string]$target.missing_capability}'denominator'{$property.value_type='integer';$property.value=[int]$target.affected}}}
        }
        $objects=[Collections.Generic.List[object]]::new();foreach($objectId in @(Get-OrdinalUnique @($factRows.object_id))){$rows=@($factRows|Where-Object{[string]$_.object_id-ceq[string]$objectId});$objects.Add([pscustomobject][ordered]@{object_id=[string]$objectId;constructor_groups=@(Get-OrdinalUnique @($rows.constructor_group));property_templates=@(Get-OrdinalUnique @($rows.property_id));fact_templates=@(Get-OrdinalUnique @($rows.fact_id));properties=@($rows|ForEach-Object{[pscustomobject][ordered]@{property_id=[string]$_.property_id;fact_id=[string]$_.fact_id;constructor_group=[string]$_.constructor_group;disposition=[string]$_.disposition;fact_type=[string]$_.fact_type;value_type=[string]$_.value_type;value=$_.value}})})}

        $witnessId = if ($isAdmitted) { "WP1V-WITNESS-$ruleId" } else { $null }
        $rejectionId = if ($isAdmitted) { $null } else { "WP1V-REJECTION-$ruleId" }
        $admittedStates=[Collections.Generic.List[object]]::new();if($isAdmitted){foreach($admittedState in @($admittedByRule[$ruleId])){$admittedStates.Add($admittedState)}}
        $rejectionCondition=[Collections.Generic.List[object]]::new();if(-not$isAdmitted){foreach($condition in @($rule.when)){$rejectionCondition.Add($condition)}}
        $entries.Add([pscustomobject][ordered]@{
            rule_id = $ruleId
            family = [string]$family.family
            accepted_disposition = @($rule.outcomes | ForEach-Object { [string]$_.disposition })
            semantic_source = [pscustomobject][ordered]@{
                model = "$($successor.model_id)/$($successor.version)"
                authorities = @($rule.authorities | ForEach-Object { [string]$_ })
            }
            classification = if ($isAdmitted) { 'admitted' } else { 'terminal' }
            witness_id = $witnessId
            rejection_witness_id = $rejectionId
            admitted_states = @($admittedStates)
            rejection_condition = if ($isAdmitted) { $null } else { @($rejectionCondition) }
            constructor_fact_template_inventory = @($constructorTemplateInventory)
            exact_projection_objects = @($objects)
            exact_fact_templates = @(Get-OrdinalUnique @($factRows.fact_id))
            exact_property_templates = @(Get-OrdinalUnique @($factRows.property_id))
            constructor_bindings = @($bindings)
            coverage_effects = @($coverageEffects)
            gap_effects = @($gapEffects)
            result_gaps_mirror_effects = @($mirrorEffects)
            lifecycle = [pscustomobject][ordered]@{
                publication = $publication
                snapshot = ($publication -cne 'no-snapshot')
                coverage_lifecycle = if ($null -ne $canonicalState -and $null -ne $canonicalState.PSObject.Properties['coverage_lifecycle']) { [string]$canonicalState.coverage_lifecycle } else { $null }
                gap_scope = if ($null -ne $canonicalState -and $null -ne $canonicalState.PSObject.Properties['gap_scope']) { [string]$canonicalState.gap_scope } else { 'none' }
            }
            expected_canonical_result = [pscustomobject][ordered]@{
                state = $canonicalState
                exact_target_fact_ids = @(Get-OrdinalUnique @($factRows.fact_id))
                no_extra_target_facts = $true
                publishes = ($isAdmitted -and @($factRows).Count -gt 0)
                exact_no_fact = ($isAdmitted -and @($factRows).Count -eq 0)
                terminal_rejection = (-not $isAdmitted)
                coverage_rows = @($supportCoverage)
                gap_objects = @($supportGaps)
                result_gap_objects = @($supportResultGaps)
            }
            mutation_or_negative_check = "WP1V-MUTATION-$ruleId-OMITTED-OR-MISREPRESENTED"
        })
    }
}

$ledger = [pscustomobject][ordered]@{
    schema_id = 'infinium.m1-slice4.protocol-5-rule-coverage-ledger/1.0.0'
    work_id = 'M1/S4.5/PRE-B2/V5/WP1V'
    status = 'accepted-proof-input'
    semantic_model = [pscustomobject][ordered]@{
        model_id = [string]$successor.model_id
        version = [string]$successor.version
        sha256 = (Get-FileHash -LiteralPath $successorPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    rule_count = @($entries).Count
    entries = @($entries)
}

$directory = Split-Path -Parent $resolvedOutput
if (-not (Test-Path -LiteralPath $directory)) { [void](New-Item -ItemType Directory -Path $directory) }
$json = ($ledger | ConvertTo-Json -Depth 100 -Compress) + "`n"
[IO.File]::WriteAllText([IO.Path]::GetFullPath($resolvedOutput), $json, [Text.UTF8Encoding]::new($false))
Write-Output "Wrote $(@($entries).Count)-rule ledger to $resolvedOutput"
