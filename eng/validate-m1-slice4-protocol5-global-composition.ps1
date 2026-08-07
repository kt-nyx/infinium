[CmdletBinding()]
param(
    [string]$ModelPath,
    [string]$SchemaPath,
    [string]$SummaryPath,
    [switch]$SkipMutationTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
if (-not $ModelPath) { $ModelPath = Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-successor-model.json' }
if (-not $SchemaPath) { $SchemaPath = Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-successor-model.schema.json' }

function Read-Json([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing $Label at '$Path'." }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { throw "Invalid $Label JSON at '$Path': $($_.Exception.Message)" }
}

function Get-Hash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TextHash([string[]]$Lines) {
    $text = [string]::Join("`n", @($Lines))
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Get-OrdinalStrings([object[]]$Values) {
    $items = [string[]]@($Values | ForEach-Object { [string]$_ })
    [System.Array]::Sort($items, [System.StringComparer]::Ordinal)
    return $items
}

function Copy-Object([object]$Value) {
    return $Value | ConvertTo-Json -Depth 100 | ConvertFrom-Json
}

function Has-Property([object]$Value, [string]$Name) {
    return $null -ne $Value -and $null -ne $Value.PSObject.Properties[$Name]
}

function Add-Issue([System.Collections.Generic.List[string]]$Issues, [string]$Code, [string]$Message) {
    $Issues.Add("$Code|$Message")
}

function Test-SameSet([object[]]$Left, [object[]]$Right) {
    $a = @($Left | ForEach-Object { [string]$_ } | Sort-Object)
    $b = @($Right | ForEach-Object { [string]$_ } | Sort-Object)
    if ($a.Count -ne $b.Count) { return $false }
    for ($i = 0; $i -lt $a.Count; $i++) { if ($a[$i] -cne $b[$i]) { return $false } }
    return $true
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
        Get-StateProduct -Model $Model -Dimensions $Dimensions -Index ($Index + 1) -Prefix $next
    }
}

function Convert-Increment([string]$Value) {
    switch ($Value) {
        'increment-one' { return 1 }
        'increment-two' { return 2 }
        'no-increment' { return 0 }
        'none' { return 0 }
        default { return $null }
    }
}

function Get-GapDefinition([object]$Model, [string]$RuleId) {
    $items = @($Model.gap_rules | Where-Object { [string]$_.rule_id -ceq $RuleId })
    if ($items.Count -eq 1) { return $items[0] }
    return $null
}

function Get-RuleEffects([object]$Model, [string]$Family, [object]$Rule, [string]$OccurrenceId, [System.Collections.Generic.List[string]]$Issues) {
    $coverage = [System.Collections.Generic.List[object]]::new()
    $gaps = [System.Collections.Generic.List[object]]::new()
    if (Has-Property $Rule 'coverage_effect') {
        $effects = @($Rule.coverage_effect)
        if (Has-Property $Rule.coverage_effect 'additional_population_effects') {
            $effects += @($Rule.coverage_effect.additional_population_effects)
        }
        foreach ($effect in $effects) {
            if ($null -eq $effect.population -or [string]$effect.population -ceq '' -or [string]$effect.denominator -ceq 'terminal') { continue }
            $denominator = Convert-Increment ([string]$effect.denominator)
            $completed = Convert-Increment ([string]$effect.completion)
            if ($null -eq $denominator -or $null -eq $completed) {
                Add-Issue $Issues 'EFFECT-TOKEN' "$Family/$($Rule.rule_id) uses an unknown coverage increment token."
                continue
            }
            $coverage.Add([pscustomobject]@{ population=[string]$effect.population; denominator=$denominator; completed=$completed; source="$Family/$($Rule.rule_id)"; occurrence=$OccurrenceId })
        }
    }
    foreach ($effect in @($Rule.gap_effects)) {
        $definition = Get-GapDefinition $Model ([string]$effect.gap_rule_id)
        if ($null -eq $definition) {
            Add-Issue $Issues 'GAP-REFERENCE' "$Family/$($Rule.rule_id) references missing gap '$($effect.gap_rule_id)'."
            continue
        }
        $gaps.Add([pscustomobject]@{
            rule_id=[string]$effect.gap_rule_id
            owner_id=[string]$effect.owner_id
            population=[string]$definition.population_template
            capability=[string]$definition.missing_capability
            scope=[string]$effect.scope
            affected=1
            snapshot_published=$true
            result_published=$true
            source="$Family/$($Rule.rule_id)"
            occurrence=$OccurrenceId
        })
    }
    return [pscustomobject]@{ coverage=@($coverage); gaps=@($gaps) }
}

function Test-SemanticGapPopulation([string]$Population, [object[]]$Patterns) {
    foreach ($pattern in @($Patterns)) {
        $text = [string]$pattern
        if ($text.EndsWith('*')) {
            if ($Population.StartsWith($text.Substring(0, $text.Length - 1), [System.StringComparison]::Ordinal)) { return $true }
        } elseif ($Population -ceq $text) { return $true }
    }
    return $false
}

function Test-Composition([object]$Model, [object]$Policy, [object[]]$EffectSets, [string]$WitnessId, [System.Collections.Generic.List[string]]$Issues) {
    $coverageRows = [ordered]@{}
    foreach ($population in @($Policy.fixed_coverage_rows)) {
        $coverageRows[[string]$population] = [ordered]@{ denominator=0; completed=0 }
    }
    $allCoverage = @($EffectSets | ForEach-Object { @($_.coverage) })
    $allGaps = @($EffectSets | ForEach-Object { @($_.gaps) })
    foreach ($set in @($EffectSets)) {
        if ((Has-Property $set 'fact_assertions') -and $null -ne $set.fact_assertions) {
            $fact = $set.fact_assertions
            if ([string]$fact.loose_state -cne 'unknown' -or [bool]$fact.present -or [bool]$fact.exact_absence_known) {
                Add-Issue $Issues 'INVENTED-HIGHER-FACT' "$WitnessId coerces accepted unknown loose availability."
            }
        }
        if ((Has-Property $set 'fixed_rows_override') -and @($set.fixed_rows_override).Count -gt 0 -and -not (Test-SameSet @($set.fixed_rows_override) @($Policy.fixed_coverage_rows))) {
            Add-Issue $Issues 'FIXED-ROW' "$WitnessId changes the exact fixed-row set."
        }
        if ((Has-Property $set 'atomic_invalid') -and [bool]$set.atomic_invalid -and (Has-Property $set 'publishes') -and [bool]$set.publishes) {
            Add-Issue $Issues 'ATOMIC-PUBLICATION' "$WitnessId publishes across an atomic rejection boundary."
        }
    }
    foreach ($effect in $allCoverage) {
        if (-not $coverageRows.Contains([string]$effect.population)) {
            Add-Issue $Issues 'FIXED-ROW' "$WitnessId references non-fixed coverage population '$($effect.population)'."
            continue
        }
        $coverageRows[[string]$effect.population].denominator += [int]$effect.denominator
        $coverageRows[[string]$effect.population].completed += [int]$effect.completed
    }
    foreach ($population in @($coverageRows.Keys)) {
        $row = $coverageRows[$population]
        if ($row.completed -gt $row.denominator -or $row.denominator -lt 0 -or $row.completed -lt 0) {
            Add-Issue $Issues 'ARITHMETIC' "$WitnessId has impossible $population arithmetic $($row.completed)/$($row.denominator)."
        }
        $related = @($allGaps | Where-Object {
            [string]$_.population -ceq [string]$population -or
            ([string]$population -ceq 'unsupported-records' -and [string]$_.population -like 'unsupported-records:*')
        })
        $isIncomplete = $row.denominator -gt $row.completed
        $completeGapAllowed = @($Policy.complete_with_gap_populations) -ccontains [string]$population
        $incompleteEffects = @($allCoverage | Where-Object { [string]$_.population -ceq [string]$population -and [int]$_.denominator -gt [int]$_.completed })
        $unowned = @($incompleteEffects | Where-Object {
            $occurrence = [string]$_.occurrence
            @($allGaps | Where-Object { [string]$_.occurrence -ceq $occurrence }).Count -eq 0
        })
        if ($row.denominator -gt 0 -and $isIncomplete -and $unowned.Count -gt 0) {
            Add-Issue $Issues 'INCOMPLETE-NO-GAP' "$WitnessId has $($unowned.Count) incomplete $population effect(s) without an owning gap."
        }
        if (-not $isIncomplete -and $related.Count -gt 0 -and -not $completeGapAllowed) {
            Add-Issue $Issues 'COMPLETE-WITH-GAP' "$WitnessId has complete $population with a gap."
        }
    }
    $ownerKeys = @($allGaps | ForEach-Object { "$($_.occurrence)|$($_.owner_id)|$($_.rule_id)" })
    if (@($ownerKeys | Group-Object | Where-Object Count -gt 1).Count -gt 0) {
        Add-Issue $Issues 'DUPLICATE-OWNER' "$WitnessId duplicates a gap owner for the same occurrence."
    }
    foreach ($gap in $allGaps) {
        $definition = Get-GapDefinition $Model ([string]$gap.rule_id)
        if ($null -eq $definition) { Add-Issue $Issues 'GAP-REFERENCE' "$WitnessId uses unknown gap '$($gap.rule_id)'." }
        elseif ([string]$definition.population_template -cne [string]$gap.population -or [string]$definition.missing_capability -cne [string]$gap.capability) {
            Add-Issue $Issues 'GAP-DEFINITION' "$WitnessId drifts the population/capability of '$($gap.rule_id)'."
        }
        if ([string]$gap.scope -cne 'snapshot-and-result') {
            Add-Issue $Issues 'GAP-SCOPE' "$WitnessId has gap '$($gap.rule_id)' outside snapshot-and-result."
        }
        if ((Has-Property $gap 'snapshot_published') -and -not [bool]$gap.snapshot_published) { Add-Issue $Issues 'SNAPSHOT-GAP' "$WitnessId omits snapshot gap publication." }
        if ((Has-Property $gap 'result_published') -and -not [bool]$gap.result_published) { Add-Issue $Issues 'RESULT-GAP' "$WitnessId omits result-gap mirroring." }
        $isFixed = $coverageRows.Contains([string]$gap.population)
        if (-not $isFixed -and [string]$gap.population -like 'unsupported-records:*') { $isFixed = $true }
        $isSemantic = Test-SemanticGapPopulation ([string]$gap.population) @($Policy.semantic_gap_only_populations)
        if (-not $isFixed -and -not $isSemantic) {
            Add-Issue $Issues 'UNRELATED-GAP' "$WitnessId has unrelated gap population '$($gap.population)'."
        }
    }
    if ($coverageRows.Contains('face-gen-loose-assets')) {
        $loose = $coverageRows['face-gen-loose-assets']
        $looseGaps = @($allGaps | Where-Object { [string]$_.population -ceq 'face-gen-loose-assets' -and [string]$_.capability -ceq 'exhaustive-byte-verified-loose-provider-index' })
        $expected = $loose.denominator - $loose.completed
        $actual = 0
        foreach ($gap in $looseGaps) { $actual += [int]$gap.affected }
        if ([int]$actual -ne [int]$expected) {
            Add-Issue $Issues 'LOOSE-GAP-COUNT' "$WitnessId loose gap count $actual does not equal incomplete loose obligations $expected."
        }
    }
    if ($coverageRows.Contains('face-gen-archive-assets')) {
        $archive = $coverageRows['face-gen-archive-assets']
        $archiveGaps = @($allGaps | Where-Object { [string]$_.population -ceq 'face-gen-archive-assets' -and [string]$_.capability -ceq 'archive-activation-and-member-precedence' })
        $expected = $archive.denominator - $archive.completed
        $actual = 0
        foreach ($gap in $archiveGaps) { $actual += [int]$gap.affected }
        if ([int]$actual -ne [int]$expected) {
            Add-Issue $Issues 'ARCHIVE-GAP-COUNT' "$WitnessId archive gap count $actual does not equal incomplete archive obligations $expected."
        }
    }
    foreach ($set in @($EffectSets)) {
        if ((Has-Property $set 'asserted_lifecycles') -and $null -ne $set.asserted_lifecycles) {
            foreach ($assertion in @($set.asserted_lifecycles)) {
                if (-not $coverageRows.Contains([string]$assertion.population)) { Add-Issue $Issues 'LIFECYCLE' "$WitnessId asserts lifecycle for an unknown row."; continue }
                $row = $coverageRows[[string]$assertion.population]
                $rowGaps = @($allGaps | Where-Object { [string]$_.population -ceq [string]$assertion.population })
                $derived = if ($row.denominator -eq 0) { 'completed' } elseif ($row.completed -eq $row.denominator -and $rowGaps.Count -eq 0) { 'completed' } elseif ($row.completed -eq 0 -and $rowGaps.Count -gt 0) { 'unsupported' } elseif ($row.completed -gt 0 -and $row.completed -lt $row.denominator -and $rowGaps.Count -gt 0) { 'completed_with_gaps' } else { 'invalid' }
                if ([string]$assertion.state -cne $derived) { Add-Issue $Issues 'LIFECYCLE' "$WitnessId asserts '$($assertion.state)' for $($assertion.population), expected '$derived'." }
            }
        }
    }
    $gapRows = @($allGaps | Group-Object { "$($_.population)|$($_.capability)" })
    if (@($gapRows | Where-Object { @($_.Group.owner_id | Sort-Object -Unique).Count -gt 1 -and @($_.Group.source | Sort-Object -Unique).Count -eq 1 }).Count -gt 0) {
        Add-Issue $Issues 'OVERLAPPING-OWNERSHIP' "$WitnessId has overlapping owners from one source for an aggregate gap pair."
    }
    return [pscustomobject]@{ rows=$coverageRows; gaps=$allGaps; gap_aggregates=$gapRows.Count }
}

function Apply-SuccessorOverlay([object]$Base, [object]$Overlay, [System.Collections.Generic.List[string]]$Issues) {
    $model = Copy-Object $Base
    $model.model_id = [string]$Overlay.model_id
    $model.version = [string]$Overlay.version
    $model.status = 'accepted'
    $model.work_id = [string]$Overlay.work_id
    $model.protocol.protocol_id = 'infinium.evaluator-v2/5'
    $model.gap_rules = @($model.gap_rules) + @($Overlay.delta.added_gap_rules)
    foreach ($replacement in @($Overlay.delta.replaced_publication_rules)) {
        $family = @($model.fact_families | Where-Object { [string]$_.family -ceq [string]$replacement.family })
        if ($family.Count -ne 1) { Add-Issue $Issues 'DELTA-FAMILY' "Replacement family '$($replacement.family)' is not unique."; continue }
        $index = -1
        for ($i=0; $i -lt @($family[0].rules).Count; $i++) { if ([string]$family[0].rules[$i].rule_id -ceq [string]$replacement.replaces_rule_id) { $index=$i; break } }
        if ($index -lt 0) { Add-Issue $Issues 'DELTA-RULE' "Replacement source '$($replacement.replaces_rule_id)' is missing."; continue }
        $family[0].rules[$index] = Copy-Object $replacement.rule
    }
    foreach ($update in @($Overlay.delta.updated_admitted_regions)) {
        $family = @($model.fact_families | Where-Object { [string]$_.family -ceq [string]$update.family })
        $region = @($family[0].state_space.admitted_regions | Where-Object { [string]$_.constraint_id -ceq [string]$update.constraint_id })
        if ($region.Count -ne 1) { Add-Issue $Issues 'DELTA-REGION' "Region '$($update.constraint_id)' is not unique."; continue }
        $region[0].required_gap_rule_ids = @($update.required_gap_rule_ids)
        $region[0].authorities = @($update.authorities)
    }
    foreach ($update in @($Overlay.delta.updated_coverage_registry)) {
        $row = @($model.coverage_registry | Where-Object { [string]$_.population -ceq [string]$update.population })
        if ($row.Count -ne 1) { Add-Issue $Issues 'DELTA-COVERAGE' "Coverage row '$($update.population)' is not unique."; continue }
        $row[0].incomplete_state_rule = [string]$update.incomplete_state_rule
        $row[0].authorities = @($update.authorities)
    }
    $model.cross_family_invariants | Add-Member -NotePropertyName 'facegen_loose_availability' -NotePropertyValue (Copy-Object $Overlay.delta.added_cross_family_invariants[0])
    return $model
}

function Invoke-Mutation([string]$Id, [scriptblock]$Mutation, [object]$BaselineModel, [object]$Policy) {
    $model = Copy-Object $BaselineModel
    $set = & $Mutation $model
    $issues = [System.Collections.Generic.List[string]]::new()
    if ($null -ne $set) { [void](Test-Composition $model $Policy @($set) "mutation/$Id" $issues) }
    return [pscustomobject]@{ id=$Id; rejected=($issues.Count -gt 0); issues=@($issues | Sort-Object) }
}

$issues = [System.Collections.Generic.List[string]]::new()
$overlay = Read-Json $ModelPath 'successor model'
$schema = Read-Json $SchemaPath 'successor schema'
$basePath = Join-Path $repoRoot ([string]$overlay.base_model.path)
$base = Read-Json $basePath 'immutable base model'

$frozenPaths = [ordered]@{
    base_model = $basePath
    base_contract = Join-Path $repoRoot ([string]$overlay.base_model.contract_path)
    base_schema = Join-Path $repoRoot ([string]$overlay.base_model.schema_path)
    base_attestation = Join-Path $repoRoot ([string]$overlay.base_model.attestation_path)
}
$expectedHashes = [ordered]@{
    base_model = [string]$overlay.base_model.sha256
    base_contract = [string]$overlay.base_model.contract_sha256
    base_schema = [string]$overlay.base_model.schema_sha256
    base_attestation = [string]$overlay.base_model.attestation_sha256
}
foreach ($name in @($frozenPaths.Keys)) {
    $actual = Get-Hash $frozenPaths[$name]
    if ($actual -cne $expectedHashes[$name]) { Add-Issue $issues 'BASE-HASH' "$name hash '$actual' does not match '$($expectedHashes[$name])'." }
}

$required = @('schema_id','model_id','version','status','work_id','protocol','authority','base_model','delta')
foreach ($name in $required) { if (-not (Has-Property $overlay $name)) { Add-Issue $issues 'SCHEMA' "Successor model is missing '$name'." } }
if ([string]$overlay.model_id -cne 'infinium.m1-slice4.protocol-5-evidence-contract' -or [string]$overlay.version -cne '1.0.0' -or [string]$overlay.protocol -cne '/5') { Add-Issue $issues 'IDENTITY' 'Successor semantic-model identity is not the accepted /5 identity.' }
if ([string]$overlay.base_model.acceptance_commit -cne '43d54accc1adbafc6ae6d0bb13e8f700461758c4' -or [string]$overlay.base_model.relationship -cne 'semantic-successor') { Add-Issue $issues 'LINEAGE' 'Predecessor acceptance commit or successor relationship drifted.' }
if (@($overlay.delta.added_gap_rules).Count -ne 1 -or @($overlay.delta.replaced_publication_rules).Count -ne 2 -or @($overlay.delta.updated_admitted_regions).Count -ne 2 -or @($overlay.delta.updated_coverage_registry).Count -ne 1 -or @($overlay.delta.added_cross_family_invariants).Count -ne 1) { Add-Issue $issues 'DELTA-SCOPE' 'Successor delta inventory is outside the authorized 1/2/2/1/1 shape.' }

$model = Apply-SuccessorOverlay $base $overlay $issues
$allRules = @($model.fact_families | ForEach-Object { $_.rules })
$ruleIds = @($allRules | ForEach-Object { [string]$_.rule_id })
$gapIds = @($model.gap_rules | ForEach-Object { [string]$_.rule_id })
if (@($ruleIds | Group-Object | Where-Object Count -ne 1).Count -gt 0 -or @($gapIds | Group-Object | Where-Object Count -ne 1).Count -gt 0) { Add-Issue $issues 'DUPLICATE-ID' 'Materialized rule or gap IDs are not unique.' }
if ($ruleIds -ccontains 'P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED' -or $ruleIds -ccontains 'P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED') { Add-Issue $issues 'DELTA-REPLACEMENT' 'A replaced P4 FaceGen rule remains active.' }
if ($gapIds -cnotcontains 'P5-GAP-LOOSE-AVAILABILITY' -or $gapIds.Count -ne 9 -or $ruleIds.Count -ne 77) { Add-Issue $issues 'INVENTORY' 'Materialized rule/gap inventories are not 77/9.' }

$policy = $overlay.delta.composition_policy
if (@($policy.fixed_coverage_rows).Count -ne 10 -or @($policy.fixed_coverage_rows | Sort-Object -Unique).Count -ne 10) { Add-Issue $issues 'FIXED-ROW' 'Composition policy must define ten unique fixed coverage rows.' }
$modelRows = @($model.coverage_registry | ForEach-Object { [string]$_.population })
if (-not (Test-SameSet @($policy.fixed_coverage_rows) $modelRows)) { Add-Issue $issues 'FIXED-ROW' 'Composition policy rows differ from the semantic coverage registry.' }

$rawStates = 0
$admittedStates = 0
$excludedStates = 0
$invalidStates = 0
$successfulWitnesses = 0
$uncoveredCompositions = 0
$overlappingStates = 0
$compositionLines = [System.Collections.Generic.List[string]]::new()
$ruleWitnesses = @{}
$effectInstances = [System.Collections.Generic.List[object]]::new()
$atomicBoundaryIds = @($model.atomic_boundaries | ForEach-Object { [string]$_.id })
foreach ($family in @($model.fact_families)) {
    foreach ($region in @($family.state_space.invalid_regions)) {
        if (-not (Has-Property $region 'atomic_boundary') -or $atomicBoundaryIds -cnotcontains [string]$region.atomic_boundary) {
            Add-Issue $issues 'ATOMIC-BOUNDARY' "$($family.family)/$($region.constraint_id) lacks one valid atomic boundary."
        }
    }
}

foreach ($family in @($model.fact_families)) {
    $dimensions = @($family.dimensions_used | ForEach-Object { [string]$_ })
    $states = if ($dimensions.Count -eq 0) { @([pscustomobject]@{}) } else { @(Get-StateProduct $model $dimensions) }
    foreach ($state in $states) {
        $rawStates++
        $admitted = @($family.state_space.admitted_regions | Where-Object { Test-Condition $state @($_.when) })
        $invalid = @($family.state_space.invalid_regions | Where-Object { Test-Condition $state @($_.when) })
        $excluded = @($family.state_space.excluded_regions | Where-Object { Test-Condition $state @($_.when) })
        $classificationCount = $admitted.Count + $invalid.Count + $excluded.Count
        if ($classificationCount -eq 0) { $uncoveredCompositions++; Add-Issue $issues 'UNCOVERED-STATE' "$($family.family) has an uncovered state."; continue }
        if ($classificationCount -gt 1) { $overlappingStates++; Add-Issue $issues 'OVERLAP-STATE' "$($family.family) has an overlapping state."; continue }
        if ($invalid.Count -eq 1) { $invalidStates++; continue }
        if ($excluded.Count -eq 1) { $excludedStates++; continue }
        $admittedStates++
        $rules = @($family.rules | Where-Object { Test-Condition $state @($_.when) })
        if ($rules.Count -ne 1) { $uncoveredCompositions++; Add-Issue $issues 'RULE-MAPPING' "$($family.family)/$($admitted[0].constraint_id) maps to $($rules.Count) rules."; continue }
        $rule = $rules[0]
        if ([string]$rule.state_class -cne [string]$admitted[0].state_class) { Add-Issue $issues 'STATE-CLASS' "$($family.family)/$($admitted[0].constraint_id) state class differs from $($rule.rule_id)." }
        $requiredGaps = @($admitted[0].required_gap_rule_ids)
        $actualGaps = @($rule.gap_effects | ForEach-Object { [string]$_.gap_rule_id })
        if (-not (Test-SameSet $requiredGaps $actualGaps)) { Add-Issue $issues 'REGION-GAPS' "$($family.family)/$($admitted[0].constraint_id) required gaps differ from $($rule.rule_id)." }
        $occurrence = "$($family.family):$admittedStates"
        $effects = if (@($policy.projection_only_families) -ccontains [string]$family.family) {
            [pscustomobject]@{ coverage=@(); gaps=@() }
        } else {
            Get-RuleEffects $model ([string]$family.family) $rule $occurrence $issues
        }
        $effectInstances.Add($effects)
        $before = $issues.Count
        [void](Test-Composition $model $policy @($effects) "$($family.family)/$($admitted[0].constraint_id)" $issues)
        if ($issues.Count -eq $before) { $successfulWitnesses++ }
        $ruleWitnesses[[string]$rule.rule_id] = $effects
        $stateText = @($state.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ','
        $compositionLines.Add("state|$($family.family)|$($admitted[0].constraint_id)|$($rule.rule_id)|$stateText")
    }
}

$pairwiseCompositions = 0
$effectRuleIds = @($ruleWitnesses.Keys | Where-Object { @($ruleWitnesses[$_].coverage).Count -gt 0 -or @($ruleWitnesses[$_].gaps).Count -gt 0 } | Sort-Object)
for ($i=0; $i -lt $effectRuleIds.Count; $i++) {
    for ($j=$i; $j -lt $effectRuleIds.Count; $j++) {
        $pairwiseCompositions++
        $left = $effectRuleIds[$i]; $right = $effectRuleIds[$j]
        $leftEffects = Copy-Object $ruleWitnesses[$left]
        $rightEffects = Copy-Object $ruleWitnesses[$right]
        foreach ($item in @($rightEffects.coverage) + @($rightEffects.gaps)) { $item.occurrence = "pair:$j" }
        $before = $issues.Count
        [void](Test-Composition $model $policy @($leftEffects,$rightEffects) "pair/$left+$right" $issues)
        if ($issues.Count -eq $before) { $successfulWitnesses++ }
        $compositionLines.Add("pair|$left|$right")
    }
}

$capabilityEventWitnesses = 0
foreach ($event in @(
    [pscustomobject]@{ id='localized-capability-event'; population='localized-strings'; gap='P4-GAP-LOCALIZED' },
    [pscustomobject]@{ id='discovery-capability-event'; population='automatic-environment-discovery'; gap='P4-GAP-DISCOVERY' }
)) {
    $definition = Get-GapDefinition $model $event.gap
    $effects = [pscustomobject]@{
        coverage=@([pscustomobject]@{population=$event.population;denominator=1;completed=0;source=$event.id;occurrence=$event.id})
        gaps=@([pscustomobject]@{rule_id=$event.gap;owner_id="GO-$($event.id.ToUpperInvariant())";population=[string]$definition.population_template;capability=[string]$definition.missing_capability;scope='snapshot-and-result';affected=1;snapshot_published=$true;result_published=$true;source=$event.id;occurrence=$event.id})
    }
    $before = $issues.Count
    [void](Test-Composition $model $policy @($effects) "event/$($event.id)" $issues)
    if ($issues.Count -eq $before) { $successfulWitnesses++; $capabilityEventWitnesses++ }
    $compositionLines.Add("event|$($event.id)|$($event.population)|$($event.gap)")
}

$mutations = [System.Collections.Generic.List[object]]::new()
if (-not $SkipMutationTests) {
    $looseGap = Get-GapDefinition $model 'P5-GAP-LOOSE-AVAILABILITY'
    $mkCoverage = { param($d,$c,$p='face-gen-loose-assets') [pscustomobject]@{ population=$p; denominator=$d; completed=$c; source='mutation'; occurrence='m1' } }
    $mkGap = { param($owner='GO-MUTATION',$pop='face-gen-loose-assets',$cap='exhaustive-byte-verified-loose-provider-index',$occ='m1') [pscustomobject]@{ rule_id='P5-GAP-LOOSE-AVAILABILITY'; owner_id=$owner; population=$pop; capability=$cap; scope='snapshot-and-result'; affected=1; snapshot_published=$true; result_published=$true; source='mutation'; occurrence=$occ } }
    $cases = [ordered]@{
        'omit-supported-loose-gap' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@() } }
        'omit-unsupported-loose-gap' = { param($m) [pscustomobject]@{ coverage=@((& $mkCoverage 1 0),(& $mkCoverage 1 0 'face-gen-archive-assets')); gaps=@([pscustomobject]@{rule_id='P4-GAP-ARCHIVE';owner_id='GO-ARCHIVE';population='face-gen-archive-assets';capability='archive-activation-and-member-precedence';scope='snapshot-and-result';affected=1;snapshot_published=$true;result_published=$true;source='mutation';occurrence='m1'}) } }
        'wrong-new-gap-rule-id' = { param($m) $g=& $mkGap; $g.rule_id='P5-GAP-WRONG'; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'wrong-loose-gap-population' = { param($m) $g=& $mkGap; $g.population='face-gen-archive-assets'; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'wrong-loose-gap-capability' = { param($m) $g=& $mkGap; $g.capability='archive-activation-and-member-precedence'; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'wrong-loose-gap-scope' = { param($m) $g=& $mkGap; $g.scope='snapshot-only'; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'zero-affected-count' = { param($m) $g=& $mkGap; $g.affected=0; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'multiple-count-one-path' = { param($m) $g=& $mkGap; $g.affected=2; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'duplicate-owner-declaration' = { param($m) $g=& $mkGap; [pscustomobject]@{ coverage=@(& $mkCoverage 2 0); gaps=@($g,(Copy-Object $g)) } }
        'duplicate-aggregate-pair' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@((& $mkGap),(& $mkGap 'GO-MUTATION-2' 'face-gen-loose-assets' 'exhaustive-byte-verified-loose-provider-index' 'm1')) } }
        'coerce-unknown-to-absent' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@(& $mkGap); fact_assertions=[pscustomobject]@{loose_state='absent';present=$false;exact_absence_known=$true} } }
        'coerce-unknown-to-present' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@(& $mkGap); fact_assertions=[pscustomobject]@{loose_state='present';present=$true;exact_absence_known=$false} } }
        'remove-loose-denominator' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 0 0); gaps=@(& $mkGap) } }
        'increment-loose-completion' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 1 1); gaps=@(& $mkGap) } }
        'drift-supported-archive' = { param($m) [pscustomobject]@{ coverage=@((& $mkCoverage 1 0),(& $mkCoverage 1 0 'face-gen-archive-assets')); gaps=@(& $mkGap) } }
        'drift-unsupported-archive' = { param($m) [pscustomobject]@{ coverage=@((& $mkCoverage 1 0),(& $mkCoverage 1 1 'face-gen-archive-assets')); gaps=@((& $mkGap),[pscustomobject]@{rule_id='P4-GAP-ARCHIVE';owner_id='GO-ARCHIVE';population='face-gen-archive-assets';capability='archive-activation-and-member-precedence';scope='snapshot-and-result';affected=1;snapshot_published=$true;result_published=$true;source='mutation';occurrence='m1'}) } }
        'misclassify-all-unknown' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@(& $mkGap); asserted_lifecycles=@([pscustomobject]@{population='face-gen-loose-assets';state='completed_with_gaps'}) } }
        'misclassify-mixed' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 2 1); gaps=@(& $mkGap); asserted_lifecycles=@([pscustomobject]@{population='face-gen-loose-assets';state='unsupported'}) } }
        'misclassify-complete' = { param($m) [pscustomobject]@{ coverage=@(& $mkCoverage 1 1); gaps=@(); asserted_lifecycles=@([pscustomobject]@{population='face-gen-loose-assets';state='completed_with_gaps'}) } }
        'omit-fixed-row' = { param($m) [pscustomobject]@{ coverage=@(); gaps=@(); fixed_rows_override=@($policy.fixed_coverage_rows | Where-Object { $_ -cne 'plugins' }) } }
        'add-duplicate-fixed-row' = { param($m) [pscustomobject]@{ coverage=@(); gaps=@(); fixed_rows_override=@($policy.fixed_coverage_rows) + @('plugins') } }
        'omit-snapshot-gap-publication' = { param($m) $g=& $mkGap; $g.snapshot_published=$false; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'omit-result-gap-mirroring' = { param($m) $g=& $mkGap; $g.result_published=$false; [pscustomobject]@{ coverage=@(& $mkCoverage 1 0); gaps=@($g) } }
        'publish-atomic-invalid-asset' = { param($m) [pscustomobject]@{ coverage=@(); gaps=@(); atomic_invalid=$true; publishes=$true } }
    }
    foreach ($id in @($cases.Keys)) { $mutations.Add((Invoke-Mutation $id $cases[$id] $model $policy)) }
    foreach ($result in @($mutations)) { if (-not $result.rejected) { Add-Issue $issues 'MUTATION-SURVIVED' "Mutation '$($result.id)' was not rejected." } }
}

$uniqueIssues = @($issues | Sort-Object -Unique)
$contradictions = @($uniqueIssues | Where-Object { $_ -notlike 'UNCOVERED-STATE*' -and $_ -notlike 'OVERLAP-STATE*' }).Count
$duplicateOwnership = @($uniqueIssues | Where-Object { $_ -like 'DUPLICATE-OWNER*' -or $_ -like 'OVERLAPPING-OWNERSHIP*' }).Count
$mutationRejected = @($mutations | Where-Object rejected).Count
$admittedRulesComposed = $ruleWitnesses.Count
$coverageEffectInstances = @($effectInstances | ForEach-Object { @($_.coverage) }).Count
$positiveCoverageEffects = @($effectInstances | ForEach-Object { @($_.coverage) } | Where-Object { [int]$_.denominator -gt 0 }).Count
$incompleteCoverageEffects = @($effectInstances | ForEach-Object { @($_.coverage) } | Where-Object { [int]$_.denominator -gt [int]$_.completed }).Count
$gapEffectInstances = @($effectInstances | ForEach-Object { @($_.gaps) }).Count
$gapBearingAdmittedStates = @($effectInstances | Where-Object { @($_.gaps).Count -gt 0 }).Count
$compositionDigest = Get-TextHash @(Get-OrdinalStrings $compositionLines)
$mutationDigest = Get-TextHash @(Get-OrdinalStrings @($mutations | ForEach-Object { "$($_.id)|$($_.rejected)|$([string]::Join(';', @(Get-OrdinalStrings @($_.issues))))" }))
$issueDigest = Get-TextHash @(Get-OrdinalStrings $uniqueIssues)
$success = $uniqueIssues.Count -eq 0 -and $mutationRejected -eq $mutations.Count

$summary = @"
{"schema_id":"infinium.m1-slice4.protocol-5-global-composition-summary/1.0.0","success":$($success.ToString().ToLowerInvariant()),"semantic_model_identity":"$($overlay.model_id)/$($overlay.version)","semantic_model_sha256":"$(Get-Hash $ModelPath)","semantic_model_schema_sha256":"$(Get-Hash $SchemaPath)","base_model_sha256":"$(Get-Hash $basePath)","families":$(@($model.fact_families).Count),"publication_rules":$($ruleIds.Count),"admitted_rules_composed":$admittedRulesComposed,"gap_rules":$($gapIds.Count),"coverage_populations":$(@($model.coverage_registry).Count),"atomic_boundaries":$($atomicBoundaryIds.Count),"raw_states":$rawStates,"admitted_states_composed":$admittedStates,"successful_witnesses":$successfulWitnesses,"pairwise_compositions":$pairwiseCompositions,"capability_event_witnesses":$capabilityEventWitnesses,"coverage_effect_instances":$coverageEffectInstances,"positive_coverage_effects":$positiveCoverageEffects,"incomplete_coverage_effects":$incompleteCoverageEffects,"gap_effect_instances":$gapEffectInstances,"gap_bearing_admitted_states":$gapBearingAdmittedStates,"excluded_states":$excludedStates,"invalid_states":$invalidStates,"uncovered_compositions":$uncoveredCompositions,"overlapping_states":$overlappingStates,"contradictions":$contradictions,"duplicate_or_overlapping_ownership":$duplicateOwnership,"mutations":$($mutations.Count),"mutations_rejected":$mutationRejected,"composition_digest":"$compositionDigest","mutation_digest":"$mutationDigest","issue_digest":"$issueDigest"}
"@
$summary = $summary.Trim() + "`n"
if ($SummaryPath) {
    $directory = Split-Path -Parent $SummaryPath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) { [void](New-Item -ItemType Directory -Path $directory) }
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($SummaryPath), $summary, [System.Text.UTF8Encoding]::new($false))
}

Write-Output "Protocol /5 global composition: success=$success admitted=$admittedStates witnesses=$successfulWitnesses pairs=$pairwiseCompositions contradictions=$contradictions uncovered=$uncoveredCompositions overlap=$overlappingStates mutations=$mutationRejected/$($mutations.Count)"
Write-Output "Composition digest: $compositionDigest"
Write-Output "Mutation digest: $mutationDigest"
if (-not $success) {
    foreach ($issue in $uniqueIssues) { Write-Output "ISSUE $issue" }
    throw "Protocol /5 global composition validation failed with $($uniqueIssues.Count) deterministic issue(s)."
}
