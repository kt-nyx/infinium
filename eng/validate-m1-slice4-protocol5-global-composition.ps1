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

function Test-SameSequence([object[]]$Left, [object[]]$Right) {
    $a = @($Left | ForEach-Object { [string]$_ })
    $b = @($Right | ForEach-Object { [string]$_ })
    if ($a.Count -ne $b.Count) { return $false }
    for ($i = 0; $i -lt $a.Count; $i++) { if ($a[$i] -cne $b[$i]) { return $false } }
    return $true
}

function Convert-CanonicalJson([object]$Value) {
    return ($Value | ConvertTo-Json -Depth 100 -Compress)
}

function Test-CanonicalEqual([object]$Left, [object]$Right) {
    return (Convert-CanonicalJson $Left) -ceq (Convert-CanonicalJson $Right)
}

function Get-LayerRank([string]$Layer) {
    switch ($Layer) {
        'none' { return 0 }
        'structural' { return 1 }
        'observed' { return 2 }
        'decoded' { return 3 }
        'resolved' { return 4 }
        'semantic' { return 5 }
        default { return -1 }
    }
}

function Get-ObjectPropertyNames([object]$Value) {
    if ($null -eq $Value) { return @() }
    return @($Value.PSObject.Properties | ForEach-Object { [string]$_.Name })
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
    $snapshotGaps = [System.Collections.Generic.List[object]]::new()
    $resultGaps = [System.Collections.Generic.List[object]]::new()
    $constructors = [System.Collections.Generic.List[object]]::new()
    $facts = [System.Collections.Generic.List[string]]::new()
    $terminal = $false
    if (Has-Property $Rule 'coverage_effect') {
        $effects = @($Rule.coverage_effect)
        if (Has-Property $Rule.coverage_effect 'additional_population_effects') {
            $effects += @($Rule.coverage_effect.additional_population_effects)
        }
        foreach ($effect in $effects) {
            if ([string]$effect.completion -ceq 'terminal') { $terminal = $true }
            if ($null -eq $effect.population -or [string]$effect.population -ceq '') { continue }
            $denominator = Convert-Increment ([string]$effect.denominator)
            $completed = Convert-Increment ([string]$effect.completion)
            if ($null -eq $denominator -or $null -eq $completed) {
                Add-Issue $Issues 'EFFECT-TOKEN' "$Family/$($Rule.rule_id) uses an unknown coverage increment token."
                continue
            }
            $coverage.Add([pscustomobject]@{ population=[string]$effect.population; denominator=$denominator; completed=$completed; source="$Family/$($Rule.rule_id)"; occurrence=$OccurrenceId })
        }
    }
    $familyDefinition = @($Model.fact_families | Where-Object { [string]$_.family -ceq $Family })[0]
    foreach ($outcome in @($Rule.outcomes)) {
        foreach ($constructorId in @($outcome.constructor_groups)) {
            $constructor = @($familyDefinition.constructor_groups | Where-Object { [string]$_.id -ceq [string]$constructorId })
            if ($constructor.Count -ne 1) {
                Add-Issue $Issues 'CONSTRUCTOR-REFERENCE' "$Family/$($Rule.rule_id) references missing constructor '$constructorId'."
                continue
            }
            $constructors.Add([pscustomobject]@{
                id=[string]$constructorId
                disposition=[string]$outcome.disposition
                value_rule=[string]$outcome.value_rule
                minimum_layer=[string]$constructor[0].minimum_layer
            })
            if (@('exact_value','typed_null','accepted_unknown','mixed_by_constructor') -ccontains [string]$outcome.disposition) {
                foreach ($template in @($constructor[0].fact_id_templates)) {
                    $facts.Add("$Family|$($Rule.rule_id)|$constructorId|$($outcome.disposition)|$template")
                }
            }
        }
    }
    foreach ($effect in @($Rule.gap_effects)) {
        $definition = Get-GapDefinition $Model ([string]$effect.gap_rule_id)
        if ($null -eq $definition) {
            Add-Issue $Issues 'GAP-REFERENCE' "$Family/$($Rule.rule_id) references missing gap '$($effect.gap_rule_id)'."
            continue
        }
        $definitionPopulation = [string]$definition.population_template
        $owned = @($coverage | Where-Object {
            [int]$_.denominator -gt [int]$_.completed -and (
                [string]$_.population -ceq $definitionPopulation -or
                ([string]$_.population -ceq 'unsupported-records' -and $definitionPopulation.StartsWith('unsupported-records:', [System.StringComparison]::Ordinal)) -or
                (([string]$_.population -cin @('npc-records','race-records','placed-reference-records')) -and ($definitionPopulation.StartsWith('unsupported-fields:', [System.StringComparison]::Ordinal) -or $definitionPopulation.StartsWith('unsupported-shapes:', [System.StringComparison]::Ordinal)))
            )
        })
        $gap = [pscustomobject]@{
            rule_id=[string]$effect.gap_rule_id
            owner_id=[string]$effect.owner_id
            member_id="$OccurrenceId|$($effect.owner_id)"
            population=$definitionPopulation
            capability=[string]$definition.missing_capability
            scope=[string]$effect.scope
            affected=1
            owns_population=if ($owned.Count -eq 1) { [string]$owned[0].population } else { $null }
            source="$Family/$($Rule.rule_id)"
            occurrence=$OccurrenceId
        }
        if ([string]$effect.scope -cin @('snapshot','snapshot-and-result')) { $snapshotGaps.Add((Copy-Object $gap)) }
        if ([string]$effect.scope -cin @('result','snapshot-and-result')) { $resultGaps.Add((Copy-Object $gap)) }
    }
    return [pscustomobject]@{
        family=$Family
        rule_id=[string]$Rule.rule_id
        occurrence=$OccurrenceId
        coverage=@($coverage)
        snapshot_gaps=@($snapshotGaps)
        result_gaps=@($resultGaps)
        constructors=@($constructors)
        fact_templates=@(Get-OrdinalStrings $facts)
        terminal=$terminal
        publishes=(-not $terminal -and $facts.Count -gt 0)
    }
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

function Get-GapAggregateLines([object[]]$Gaps) {
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($group in @($Gaps | Group-Object { "$($_.population)|$($_.capability)" })) {
        $affected = 0
        foreach ($member in @($group.Group)) { $affected += [int]$member.affected }
        $memberIds = @(Get-OrdinalStrings @($group.Group | ForEach-Object { [string]$_.member_id }))
        $lines.Add("$($group.Name)|$affected|$([string]::Join(',', $memberIds))")
    }
    return @(Get-OrdinalStrings $lines)
}

function Test-Composition([object]$Model, [object]$Policy, [object[]]$EffectSets, [string]$WitnessId, [System.Collections.Generic.List[string]]$Issues) {
    $coverageRows = [ordered]@{}
    foreach ($population in @($Policy.fixed_coverage_rows)) {
        $coverageRows[[string]$population] = [ordered]@{ population=[string]$population; denominator=0; completed=0; state='completed' }
    }
    $allCoverage = @($EffectSets | ForEach-Object { @($_.coverage) })
    $snapshotGaps = @($EffectSets | ForEach-Object { if (Has-Property $_ 'snapshot_gaps') { @($_.snapshot_gaps) } })
    $resultGaps = @($EffectSets | ForEach-Object { if (Has-Property $_ 'result_gaps') { @($_.result_gaps) } })
    $noSnapshot = @($EffectSets | Where-Object { (Has-Property $_ 'no_snapshot') -and [bool]$_.no_snapshot }).Count -gt 0

    foreach ($set in @($EffectSets)) {
        if ((Has-Property $set 'fact_assertions') -and $null -ne $set.fact_assertions) {
            $fact = $set.fact_assertions
            if ([string]$fact.loose_state -cne 'unknown' -or [bool]$fact.present -or [bool]$fact.exact_absence_known) {
                Add-Issue $Issues 'INVENTED-HIGHER-FACT' "$WitnessId coerces accepted unknown loose availability."
            }
        }
        if ((Has-Property $set 'fixed_rows_override') -and -not (Test-SameSequence @($set.fixed_rows_override) @($Policy.fixed_coverage_rows))) {
            Add-Issue $Issues 'FIXED-ROW' "$WitnessId changes the exact fixed-row order or membership."
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
        $incompleteEffects = @($allCoverage | Where-Object { [string]$_.population -ceq [string]$population -and [int]$_.denominator -gt [int]$_.completed })
        foreach ($effect in $incompleteEffects) {
            $owners = @($snapshotGaps | Where-Object {
                [string]$_.occurrence -ceq [string]$effect.occurrence -and
                [string]$_.source -ceq [string]$effect.source -and
                [string]$_.owns_population -ceq [string]$effect.population
            })
            if ($owners.Count -eq 0) { Add-Issue $Issues 'INCOMPLETE-NO-GAP' "$WitnessId has incomplete $population effect '$($effect.occurrence)' without its declared owning gap." }
            if ($owners.Count -gt 1) { Add-Issue $Issues 'DUPLICATE-OWNER' "$WitnessId has $($owners.Count) owners for incomplete $population effect '$($effect.occurrence)'." }
        }
        $ownedGaps = @($snapshotGaps | Where-Object { [string]$_.owns_population -ceq [string]$population })
        $isIncomplete = $row.denominator -gt $row.completed
        if (-not $isIncomplete -and $ownedGaps.Count -gt 0 -and @($Policy.complete_with_gap_populations) -cnotcontains [string]$population) {
            Add-Issue $Issues 'COMPLETE-WITH-GAP' "$WitnessId has complete $population with an owning capability gap."
        }
        $derived = if ($row.denominator -eq 0) {
            'completed'
        } elseif ($row.completed -eq $row.denominator -and $ownedGaps.Count -eq 0) {
            'completed'
        } elseif ($row.completed -eq 0 -and $ownedGaps.Count -gt 0) {
            'unsupported'
        } elseif ($row.completed -gt 0 -and $row.completed -lt $row.denominator -and $ownedGaps.Count -gt 0) {
            'completed_with_gaps'
        } else {
            'invalid'
        }
        $row.state = $derived
        if ($derived -ceq 'invalid') { Add-Issue $Issues 'LIFECYCLE' "$WitnessId cannot derive a legal lifecycle for $population $($row.completed)/$($row.denominator)." }
    }

    foreach ($gap in $snapshotGaps) {
        $definition = Get-GapDefinition $Model ([string]$gap.rule_id)
        if ($null -eq $definition) { Add-Issue $Issues 'GAP-REFERENCE' "$WitnessId uses unknown gap '$($gap.rule_id)'."; continue }
        if ([string]$definition.population_template -cne [string]$gap.population -or [string]$definition.missing_capability -cne [string]$gap.capability) {
            Add-Issue $Issues 'GAP-DEFINITION' "$WitnessId drifts the population/capability of '$($gap.rule_id)'."
        }
        if ([string]$gap.scope -cne 'snapshot-and-result') { Add-Issue $Issues 'GAP-SCOPE' "$WitnessId has gap '$($gap.rule_id)' outside snapshot-and-result." }
        if ([int]$gap.affected -lt 1) { Add-Issue $Issues 'GAP-AFFECTED' "$WitnessId has non-positive affected count for '$($gap.rule_id)'." }
        $isFixed = $coverageRows.Contains([string]$gap.population) -or ([string]$gap.population).StartsWith('unsupported-records:', [System.StringComparison]::Ordinal)
        $isSemantic = Test-SemanticGapPopulation ([string]$gap.population) @($Policy.semantic_gap_only_populations)
        if (-not $isFixed -and -not $isSemantic) { Add-Issue $Issues 'UNRELATED-GAP' "$WitnessId has unrelated gap population '$($gap.population)'." }
        if ($null -ne $gap.owns_population -and [string]$gap.owns_population -cne '') {
            $ownedEffect = @($allCoverage | Where-Object {
                [string]$_.occurrence -ceq [string]$gap.occurrence -and
                [string]$_.source -ceq [string]$gap.source -and
                [string]$_.population -ceq [string]$gap.owns_population -and
                [int]$_.denominator -gt [int]$_.completed
            })
            if ($ownedEffect.Count -ne 1) { Add-Issue $Issues 'GAP-WITHOUT-INCOMPLETE' "$WitnessId gap member '$($gap.member_id)' does not own exactly one incomplete population effect." }
        }
    }

    $snapshotAggregateLines = @(Get-GapAggregateLines $snapshotGaps)
    $resultAggregateLines = @(Get-GapAggregateLines $resultGaps)
    if (-not (Test-SameSequence $snapshotAggregateLines $resultAggregateLines)) { Add-Issue $Issues 'GAP-MIRROR' "$WitnessId snapshot and result gap aggregates differ." }
    $snapshotMemberIds = @(Get-OrdinalStrings @($snapshotGaps | ForEach-Object { [string]$_.member_id }))
    $resultMemberIds = @(Get-OrdinalStrings @($resultGaps | ForEach-Object { [string]$_.member_id }))
    if (-not (Test-SameSequence $snapshotMemberIds $resultMemberIds)) { Add-Issue $Issues 'GAP-MIRROR-MEMBER' "$WitnessId snapshot and result gap members differ." }
    if (@($snapshotMemberIds | Group-Object | Where-Object Count -gt 1).Count -gt 0 -or @($resultMemberIds | Group-Object | Where-Object Count -gt 1).Count -gt 0) {
        Add-Issue $Issues 'DUPLICATE-OWNER' "$WitnessId duplicates a gap owner member for one obligation."
    }

    foreach ($populationAndCapability in @(
        [pscustomobject]@{ population='face-gen-loose-assets'; capability='exhaustive-byte-verified-loose-provider-index'; code='LOOSE-GAP-COUNT' },
        [pscustomobject]@{ population='face-gen-archive-assets'; capability='archive-activation-and-member-precedence'; code='ARCHIVE-GAP-COUNT' }
    )) {
        $row = $coverageRows[[string]$populationAndCapability.population]
        $actual = 0
        foreach ($gap in @($snapshotGaps | Where-Object { [string]$_.population -ceq [string]$populationAndCapability.population -and [string]$_.capability -ceq [string]$populationAndCapability.capability })) { $actual += [int]$gap.affected }
        $expected = $row.denominator - $row.completed
        if ($actual -ne $expected) { Add-Issue $Issues ([string]$populationAndCapability.code) "$WitnessId gap affected count $actual does not equal incomplete $($populationAndCapability.population) obligations $expected." }
    }

    foreach ($set in @($EffectSets)) {
        if ((Has-Property $set 'asserted_lifecycles') -and $null -ne $set.asserted_lifecycles) {
            foreach ($assertion in @($set.asserted_lifecycles)) {
                if (-not $coverageRows.Contains([string]$assertion.population)) { Add-Issue $Issues 'LIFECYCLE' "$WitnessId asserts lifecycle for an unknown row."; continue }
                if ([string]$assertion.state -cne [string]$coverageRows[[string]$assertion.population].state) { Add-Issue $Issues 'LIFECYCLE' "$WitnessId asserts '$($assertion.state)' for $($assertion.population), expected '$($coverageRows[[string]$assertion.population].state)'." }
            }
        }
    }

    $publishedSets = @($EffectSets | Where-Object { (Has-Property $_ 'publishes') -and [bool]$_.publishes -and [string]$_.family -cne 'result' })
    if ($noSnapshot) {
        if ($publishedSets.Count -gt 0 -or $allCoverage.Count -gt 0 -or $snapshotGaps.Count -gt 0 -or $resultGaps.Count -gt 0) { Add-Issue $Issues 'NO-SNAPSHOT-PUBLICATION' "$WitnessId publishes snapshot material for a no-snapshot result." }
    }
    $familyRows = [ordered]@{}
    foreach ($family in @($Model.fact_families | ForEach-Object { [string]$_.family })) { $familyRows[$family] = @() }
    if (-not $noSnapshot) {
        foreach ($set in @($EffectSets)) {
            if ((Has-Property $set 'family') -and $familyRows.Contains([string]$set.family)) {
                $familyRows[[string]$set.family] = @([pscustomobject]@{ rule_id=[string]$set.rule_id; constructors=@($set.constructors); fact_templates=@($set.fact_templates) })
            }
        }
        if (@($familyRows['result']).Count -eq 0) {
            $familyRows['result'] = @([pscustomobject]@{ rule_id='P4-RESULT-PUBLISHED'; constructors=@('FC-RESULT-STATE:exact_value'); fact_templates=@('result|P4-RESULT-PUBLISHED|FC-RESULT-STATE|exact_value|result/snapshot_present','result|P4-RESULT-PUBLISHED|FC-RESULT-STATE|exact_value|result/failure_present') })
        }
        $familyRows['coverage'] = @($coverageRows.Values)
        $familyRows['gaps'] = @($snapshotAggregateLines)
        $familyRows['result_gaps'] = @($resultAggregateLines)
        if ($familyRows.Count -ne 15 -or @($familyRows['result']).Count -ne 1) { Add-Issue $Issues 'COMPLETE-SNAPSHOT' "$WitnessId does not materialize all 15 families with exactly one published result." }
        if (@($familyRows['coverage']).Count -ne 10 -or -not (Test-SameSequence @($familyRows['coverage'] | ForEach-Object population) @($Policy.fixed_coverage_rows))) {
            Add-Issue $Issues 'FIXED-ROW' "$WitnessId does not materialize exactly ten ordered coverage rows."
        }
    } else {
        foreach ($family in @($familyRows.Keys)) { if (@($familyRows[$family]).Count -ne 0) { Add-Issue $Issues 'NO-SNAPSHOT-FAMILY' "$WitnessId no-snapshot witness contains '$family'." } }
    }
    return [pscustomobject]@{
        rows=$coverageRows
        snapshot_gaps=$snapshotGaps
        result_gaps=$resultGaps
        gap_aggregates=$snapshotAggregateLines.Count
        snapshot_family_count=$familyRows.Count
        no_snapshot=$noSnapshot
        family_rows=$familyRows
    }
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

function Test-OverlaySchema([object]$Overlay, [object]$Schema, [string]$ModelFile, [string]$SchemaFile, [System.Collections.Generic.List[string]]$Issues) {
    $expectedTop = @('schema_id','model_id','version','status','work_id','protocol','authority','base_model','delta')
    if (-not (Test-SameSet @(Get-ObjectPropertyNames $Overlay) $expectedTop)) { Add-Issue $Issues 'SCHEMA-TOP' 'Successor model top-level properties differ from the closed schema.' }
    if ([string]$Schema.properties.schema_id.const -cne 'infinium.m1-slice4.protocol-5-successor-model.schema/1.0.1' -or [string]$Schema.properties.version.const -cne '1.0.1') { Add-Issue $Issues 'SCHEMA-IDENTITY' 'Successor schema const identity is not 1.0.1.' }
    if ([string]$Overlay.schema_id -cne [string]$Schema.properties.schema_id.const -or [string]$Overlay.model_id -cne [string]$Schema.properties.model_id.const -or [string]$Overlay.version -cne [string]$Schema.properties.version.const -or [string]$Overlay.status -cne [string]$Schema.properties.status.const -or [string]$Overlay.work_id -cne [string]$Schema.properties.work_id.const -or [string]$Overlay.protocol -cne [string]$Schema.properties.protocol.const) {
        Add-Issue $Issues 'SCHEMA-CONST' 'Successor model violates one or more schema identity consts.'
    }
    $expectedDelta = @('added_gap_rules','replaced_publication_rules','updated_admitted_regions','updated_coverage_registry','added_cross_family_invariants','composition_policy')
    if (-not (Test-SameSet @(Get-ObjectPropertyNames $Overlay.delta) $expectedDelta)) { Add-Issue $Issues 'SCHEMA-DELTA' 'Successor delta properties differ from the closed schema.' }
    $expectedPolicy = @('fixed_coverage_rows','coverage_owned_gap_populations','semantic_gap_only_populations','complete_with_gap_populations','projection_only_families','snapshot_gap_mirroring','atomic_rejection')
    if (-not (Test-SameSet @(Get-ObjectPropertyNames $Overlay.delta.composition_policy) $expectedPolicy)) { Add-Issue $Issues 'SCHEMA-POLICY' 'Composition policy properties differ from the closed schema.' }
    $testJson = Get-Command Test-Json -ErrorAction SilentlyContinue
    if ($null -ne $testJson) {
        $valid = Get-Content -LiteralPath $ModelFile -Raw | Test-Json -SchemaFile $SchemaFile -ErrorAction SilentlyContinue
        if (-not $valid) { Add-Issue $Issues 'JSON-SCHEMA' 'Successor model fails the declared JSON Schema.' }
    }
}

function Get-CoverageShape([object]$Coverage) {
    $items = [System.Collections.Generic.List[object]]::new()
    foreach ($effect in @($Coverage) + @(if (Has-Property $Coverage 'additional_population_effects') { @($Coverage.additional_population_effects) })) {
        $items.Add([ordered]@{ population=$effect.population; denominator=[string]$effect.denominator; completion=[string]$effect.completion })
    }
    return @($items)
}

function Test-BoundedDelta([object]$Base, [object]$Overlay, [System.Collections.Generic.List[string]]$Issues) {
    $facegen = @($Base.fact_families | Where-Object { [string]$_.family -ceq 'face_gen' })[0]
    $expectedSources = @('P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED','P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED')
    if (-not (Test-SameSequence @($Overlay.delta.replaced_publication_rules | ForEach-Object replaces_rule_id) $expectedSources)) { Add-Issue $Issues 'DELTA-REPLACEMENTS' 'Replacement source IDs or order differ from the authorized two-rule delta.' }
    foreach ($replacement in @($Overlay.delta.replaced_publication_rules)) {
        $old = @($facegen.rules | Where-Object { [string]$_.rule_id -ceq [string]$replacement.replaces_rule_id })
        if ($old.Count -ne 1) { Add-Issue $Issues 'DELTA-SOURCE' "Missing unique predecessor '$($replacement.replaces_rule_id)'."; continue }
        $new = $replacement.rule
        foreach ($field in @('state_class','when','minimum_layer','prerequisites','outcomes','atomic_boundary')) {
            if (-not (Test-CanonicalEqual $old[0].$field $new.$field)) { Add-Issue $Issues 'DELTA-SEMANTICS' "$($replacement.replaces_rule_id) changes inherited '$field'." }
        }
        if (-not (Test-CanonicalEqual (Get-CoverageShape $old[0].coverage_effect) (Get-CoverageShape $new.coverage_effect))) { Add-Issue $Issues 'DELTA-COVERAGE' "$($replacement.replaces_rule_id) changes inherited coverage arithmetic." }
        $newGapIds = @($new.gap_effects | ForEach-Object { [string]$_.gap_rule_id })
        $oldGapIds = @($old[0].gap_effects | ForEach-Object { [string]$_.gap_rule_id })
        if (-not (Test-SameSet $newGapIds (@($oldGapIds) + @('P5-GAP-LOOSE-AVAILABILITY')))) { Add-Issue $Issues 'DELTA-GAPS' "$($replacement.replaces_rule_id) does not add exactly the authorized loose gap." }
        foreach ($oldGap in @($old[0].gap_effects)) {
            $same = @($new.gap_effects | Where-Object { [string]$_.gap_rule_id -ceq [string]$oldGap.gap_rule_id })
            if ($same.Count -ne 1 -or -not (Test-CanonicalEqual $oldGap $same[0])) { Add-Issue $Issues 'DELTA-GAP-INHERITANCE' "$($replacement.replaces_rule_id) changes an inherited gap effect." }
        }
    }
    $gap = @($Overlay.delta.added_gap_rules)[0]
    if ([string]$gap.rule_id -cne 'P5-GAP-LOOSE-AVAILABILITY' -or [string]$gap.population_template -cne 'face-gen-loose-assets' -or [string]$gap.missing_capability -cne 'exhaustive-byte-verified-loose-provider-index' -or [string]$gap.aggregation_key -cne 'population+missing_capability' -or [string]$gap.affected_count -cne 'exact independently identified affected paths' -or [string]$gap.scope -cne 'snapshot-and-result') { Add-Issue $Issues 'DELTA-GAP-DEFINITION' 'Added loose-availability gap differs from the owner-authorized identity.' }
    $expectedRegionIds = @('SC-FACEGEN-UNKNOWN-SUPPORTED','SC-FACEGEN-UNKNOWN-UNSUPPORTED')
    if (-not (Test-SameSequence @($Overlay.delta.updated_admitted_regions | ForEach-Object constraint_id) $expectedRegionIds)) { Add-Issue $Issues 'DELTA-REGIONS' 'Admitted-region update inventory drifted.' }
    foreach ($update in @($Overlay.delta.updated_admitted_regions)) {
        $oldRegion = @($facegen.state_space.admitted_regions | Where-Object { [string]$_.constraint_id -ceq [string]$update.constraint_id })
        if ($oldRegion.Count -ne 1) { Add-Issue $Issues 'DELTA-REGION-SOURCE' "Missing predecessor region '$($update.constraint_id)'."; continue }
        if ([string]$update.family -cne 'face_gen' -or -not (Test-SameSet @($update.required_gap_rule_ids) (@($oldRegion[0].required_gap_rule_ids) + @('P5-GAP-LOOSE-AVAILABILITY')))) { Add-Issue $Issues 'DELTA-REGION-GAPS' "$($update.constraint_id) does not add exactly the authorized loose gap." }
    }
    $coverageUpdate = @($Overlay.delta.updated_coverage_registry)[0]
    if ([string]$coverageUpdate.population -cne 'face-gen-loose-assets' -or [string]$coverageUpdate.incomplete_state_rule -cne 'Unknown loose availability prevents completion and is owned exactly by P5-GAP-LOOSE-AVAILABILITY; archive authority is independent.') { Add-Issue $Issues 'DELTA-COVERAGE-TEXT' 'Coverage registry update differs from the authorized loose-incomplete rule.' }
    $invariant = @($Overlay.delta.added_cross_family_invariants)[0]
    if ([string]$invariant.invariant_id -cne 'INV-FACEGEN-LOOSE-AVAILABILITY' -or [string]$invariant.population -cne 'face-gen-loose-assets' -or [string]$invariant.gap_rule_id -cne 'P5-GAP-LOOSE-AVAILABILITY' -or [string]$invariant.missing_capability -cne 'exhaustive-byte-verified-loose-provider-index' -or [string]$invariant.per_path_coverage -cne 'denominator+1,completed+0' -or [string]$invariant.gap_affected_count -cne 'denominator-minus-completed' -or -not [bool]$invariant.archive_independence -or [string]$invariant.lifecycle.zero -cne 'completed' -or [string]$invariant.lifecycle.all_incomplete_positive -cne 'unsupported' -or [string]$invariant.lifecycle.mixed -cne 'completed_with_gaps' -or [string]$invariant.lifecycle.complete_without_gap -cne 'completed' -or [string]$invariant.result_state -cne 'completed_with_gaps') { Add-Issue $Issues 'DELTA-INVARIANT' 'Added loose-availability cross-family invariant drifted.' }
}

function Test-RuleStructures([object]$Model, [System.Collections.Generic.List[string]]$Issues) {
    $validDispositions = @('exact_value','typed_null','accepted_unknown','mixed_by_constructor','omit','no_fact','terminal_rejection')
    $boundaryIds = @($Model.atomic_boundaries | ForEach-Object { [string]$_.id })
    foreach ($family in @($Model.fact_families)) {
        $expectedConstructors = @($family.constructor_groups | ForEach-Object { [string]$_.id })
        foreach ($rule in @($family.rules)) {
            $assigned = @($rule.outcomes | ForEach-Object { @($_.constructor_groups) } | ForEach-Object { [string]$_ })
            if (-not (Test-SameSet $assigned $expectedConstructors) -or @($assigned | Group-Object | Where-Object Count -gt 1).Count -gt 0) { Add-Issue $Issues 'CONSTRUCTOR-TOTALITY' "$($family.family)/$($rule.rule_id) does not assign every family constructor exactly once." }
            if (@($rule.prerequisites | Group-Object | Where-Object Count -gt 1).Count -gt 0) { Add-Issue $Issues 'PREREQUISITE' "$($family.family)/$($rule.rule_id) duplicates a prerequisite." }
            if ($boundaryIds -cnotcontains [string]$rule.atomic_boundary) { Add-Issue $Issues 'ATOMIC-BOUNDARY' "$($family.family)/$($rule.rule_id) lacks a valid atomic boundary." }
            foreach ($outcome in @($rule.outcomes)) {
                if ($validDispositions -cnotcontains [string]$outcome.disposition) { Add-Issue $Issues 'DISPOSITION' "$($family.family)/$($rule.rule_id) has unknown disposition '$($outcome.disposition)'." }
                foreach ($constructorId in @($outcome.constructor_groups)) {
                    $constructor = @($family.constructor_groups | Where-Object { [string]$_.id -ceq [string]$constructorId })
                    if ($constructor.Count -ne 1) { continue }
                    if (@('exact_value','typed_null','accepted_unknown','mixed_by_constructor') -ccontains [string]$outcome.disposition -and (Get-LayerRank ([string]$constructor[0].minimum_layer)) -gt (Get-LayerRank ([string]$rule.minimum_layer))) {
                        Add-Issue $Issues 'INVENTED-LAYER' "$($family.family)/$($rule.rule_id) emits $constructorId above its available evidence layer."
                    }
                }
            }
        }
    }
}

function Test-AcceptedPolicy([object]$Policy, [object]$Model, [System.Collections.Generic.List[string]]$Issues) {
    $expectedRows = @('plugins','npc-records','race-records','placed-reference-records','unsupported-records','face-gen-loose-assets','face-gen-archive-assets','localized-strings','automatic-environment-discovery','taxonomy-subjects')
    if (-not (Test-SameSequence @($Policy.fixed_coverage_rows) $expectedRows)) { Add-Issue $Issues 'FIXED-ROW' 'Composition policy must preserve the exact inherited ten-row order.' }
    if (-not (Test-SameSequence @($Policy.projection_only_families) @('coverage','gaps','result_gaps'))) { Add-Issue $Issues 'PROJECTION-ONLY' 'Composition policy projection-only families must be exactly coverage, gaps, result_gaps in inherited order.' }
    if (-not (Test-SameSequence @($Policy.coverage_owned_gap_populations) @('face-gen-loose-assets','face-gen-archive-assets','npc-records','race-records','placed-reference-records','localized-strings','automatic-environment-discovery'))) { Add-Issue $Issues 'OWNED-GAP-POPULATIONS' 'Coverage-owned gap population policy drifted.' }
    if (-not (Test-SameSequence @($Policy.semantic_gap_only_populations) @('face-gen-applicability:template','face-gen-applicability:race','unsupported-fields:*','unsupported-shapes:*'))) { Add-Issue $Issues 'SEMANTIC-GAP-POPULATIONS' 'Semantic-only gap population policy drifted.' }
    if (-not (Test-SameSequence @($Policy.complete_with_gap_populations) @('unsupported-records'))) { Add-Issue $Issues 'COMPLETE-WITH-GAP-POPULATIONS' 'Inherited complete-with-gap exception policy drifted.' }
    $modelRows = @($Model.coverage_registry | ForEach-Object { [string]$_.population })
    if (-not (Test-SameSequence @($Policy.fixed_coverage_rows) $modelRows)) { Add-Issue $Issues 'FIXED-ROW' 'Composition policy row order differs from the semantic coverage registry.' }
    if (-not [bool]$Policy.snapshot_gap_mirroring -or [string]$Policy.atomic_rejection -cne 'no-partial-publication') { Add-Issue $Issues 'COMPOSITION-POLICY' 'Gap mirroring or atomic rejection policy drifted.' }
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
$validatorPath = [System.IO.Path]::GetFullPath($MyInvocation.MyCommand.Path)
Test-OverlaySchema $overlay $schema $ModelPath $SchemaPath $issues
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
if ([string]$overlay.model_id -cne 'infinium.m1-slice4.protocol-5-evidence-contract' -or [string]$overlay.version -cne '1.0.1' -or [string]$overlay.protocol -cne '/5') { Add-Issue $issues 'IDENTITY' 'Successor semantic-model identity is not the proof-corrected /5 identity.' }
if ([string]$overlay.base_model.acceptance_commit -cne '43d54accc1adbafc6ae6d0bb13e8f700461758c4' -or [string]$overlay.base_model.relationship -cne 'semantic-successor') { Add-Issue $issues 'LINEAGE' 'Predecessor acceptance commit or successor relationship drifted.' }
if (@($overlay.delta.added_gap_rules).Count -ne 1 -or @($overlay.delta.replaced_publication_rules).Count -ne 2 -or @($overlay.delta.updated_admitted_regions).Count -ne 2 -or @($overlay.delta.updated_coverage_registry).Count -ne 1 -or @($overlay.delta.added_cross_family_invariants).Count -ne 1) { Add-Issue $issues 'DELTA-SCOPE' 'Successor delta inventory is outside the authorized 1/2/2/1/1 shape.' }
Test-BoundedDelta $base $overlay $issues

$model = Apply-SuccessorOverlay $base $overlay $issues
Test-RuleStructures $model $issues
$allRules = @($model.fact_families | ForEach-Object { $_.rules })
$ruleIds = @($allRules | ForEach-Object { [string]$_.rule_id })
$gapIds = @($model.gap_rules | ForEach-Object { [string]$_.rule_id })
if (@($ruleIds | Group-Object | Where-Object Count -ne 1).Count -gt 0 -or @($gapIds | Group-Object | Where-Object Count -ne 1).Count -gt 0) { Add-Issue $issues 'DUPLICATE-ID' 'Materialized rule or gap IDs are not unique.' }
if ($ruleIds -ccontains 'P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED' -or $ruleIds -ccontains 'P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED') { Add-Issue $issues 'DELTA-REPLACEMENT' 'A replaced P4 FaceGen rule remains active.' }
if ($gapIds -cnotcontains 'P5-GAP-LOOSE-AVAILABILITY' -or $gapIds.Count -ne 9 -or $ruleIds.Count -ne 77) { Add-Issue $issues 'INVENTORY' 'Materialized rule/gap inventories are not 77/9.' }

$policy = $overlay.delta.composition_policy
Test-AcceptedPolicy $policy $model $issues

$rawStates = 0
$admittedStates = 0
$excludedStates = 0
$invalidStates = 0
$successfulWitnesses = 0
$completeSnapshotWitnesses = 0
$noSnapshotWitnesses = 0
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
        $effects = Get-RuleEffects $model ([string]$family.family) $rule $occurrence $issues
        if ([string]$rule.rule_id -ceq 'P4-RESULT-NO-SNAPSHOT') { $effects | Add-Member -NotePropertyName no_snapshot -NotePropertyValue $true -Force }
        $effectInstances.Add($effects)
        $before = $issues.Count
        [void](Test-Composition $model $policy @($effects) "$($family.family)/$($admitted[0].constraint_id)" $issues)
        if ($issues.Count -eq $before) {
            $successfulWitnesses++
            if ((Has-Property $effects 'no_snapshot') -and [bool]$effects.no_snapshot) { $noSnapshotWitnesses++ } else { $completeSnapshotWitnesses++ }
        }
        $ruleWitnesses[[string]$rule.rule_id] = $effects
        $stateText = @($state.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ','
        $constructorText = @($effects.constructors | ForEach-Object { "$($_.id):$($_.disposition):$($_.value_rule)" }) -join ';'
        $factText = [string]::Join(';', @($effects.fact_templates))
        $compositionLines.Add("state|$($family.family)|$($admitted[0].constraint_id)|$($rule.rule_id)|$stateText|constructors=$constructorText|facts=$factText")
    }
}

$pairwiseCompositions = 0
$effectRuleIds = @($ruleWitnesses.Keys | Where-Object { @($ruleWitnesses[$_].coverage).Count -gt 0 -or @($ruleWitnesses[$_].snapshot_gaps).Count -gt 0 -or @($ruleWitnesses[$_].result_gaps).Count -gt 0 } | Sort-Object)
for ($i=0; $i -lt $effectRuleIds.Count; $i++) {
    for ($j=$i; $j -lt $effectRuleIds.Count; $j++) {
        $pairwiseCompositions++
        $left = $effectRuleIds[$i]; $right = $effectRuleIds[$j]
        $leftEffects = Copy-Object $ruleWitnesses[$left]
        $rightEffects = Copy-Object $ruleWitnesses[$right]
        foreach ($item in @($rightEffects.coverage)) { $item.occurrence = "pair:$j" }
        foreach ($item in @($rightEffects.snapshot_gaps) + @($rightEffects.result_gaps)) { $item.occurrence = "pair:$j"; $item.member_id = "pair:$j|$($item.owner_id)" }
        $rightEffects.occurrence = "pair:$j"
        $before = $issues.Count
        [void](Test-Composition $model $policy @($leftEffects,$rightEffects) "pair/$left+$right" $issues)
        if ($issues.Count -eq $before) { $successfulWitnesses++ }
        $compositionLines.Add("pair|$left|$right")
    }
}

$projectionRuleEffectWitnesses = 0
foreach ($ruleId in @(Get-OrdinalStrings @($ruleWitnesses.Keys))) {
    $set = $ruleWitnesses[$ruleId]
    if (@($policy.projection_only_families) -cnotcontains [string]$set.family) { continue }
    $rule = @((@($model.fact_families | Where-Object { [string]$_.family -ceq [string]$set.family })[0]).rules | Where-Object { [string]$_.rule_id -ceq $ruleId })[0]
    $expectedFacts = @($rule.outcomes | Where-Object { @('exact_value','typed_null','accepted_unknown','mixed_by_constructor') -ccontains [string]$_.disposition }).Count -gt 0
    $hasFacts = @($set.fact_templates).Count -gt 0
    $hasConstructors = @($set.constructors).Count -gt 0
    if (-not $hasConstructors -or $expectedFacts -ne $hasFacts) {
        Add-Issue $issues 'PROJECTION-RULE-EFFECT' "$($set.family)/$ruleId did not retain its exact constructor/fact disposition."
    } else {
        $projectionRuleEffectWitnesses++
    }
}

$capabilityEventWitnesses = 0
foreach ($event in @(
    [pscustomobject]@{ id='localized-capability-event'; population='localized-strings'; gap='P4-GAP-LOCALIZED' },
    [pscustomobject]@{ id='discovery-capability-event'; population='automatic-environment-discovery'; gap='P4-GAP-DISCOVERY' }
)) {
    $definition = Get-GapDefinition $model $event.gap
    $effects = [pscustomobject]@{
        family='coverage'; rule_id=$event.id; occurrence=$event.id
        coverage=@([pscustomobject]@{population=$event.population;denominator=1;completed=0;source=$event.id;occurrence=$event.id})
        snapshot_gaps=@([pscustomobject]@{rule_id=$event.gap;owner_id="GO-$($event.id.ToUpperInvariant())";member_id="$($event.id)|GO-$($event.id.ToUpperInvariant())";population=[string]$definition.population_template;capability=[string]$definition.missing_capability;scope='snapshot-and-result';affected=1;owns_population=$event.population;source=$event.id;occurrence=$event.id})
        result_gaps=@([pscustomobject]@{rule_id=$event.gap;owner_id="GO-$($event.id.ToUpperInvariant())";member_id="$($event.id)|GO-$($event.id.ToUpperInvariant())";population=[string]$definition.population_template;capability=[string]$definition.missing_capability;scope='snapshot-and-result';affected=1;owns_population=$event.population;source=$event.id;occurrence=$event.id})
        constructors=@(); fact_templates=@(); terminal=$false; publishes=$false
    }
    $before = $issues.Count
    [void](Test-Composition $model $policy @($effects) "event/$($event.id)" $issues)
    if ($issues.Count -eq $before) { $successfulWitnesses++; $capabilityEventWitnesses++ }
    $compositionLines.Add("event|$($event.id)|$($event.population)|$($event.gap)")
}

$globalCompositionWitnesses = 0
$globalSets = [System.Collections.Generic.List[object]]::new()
$globalIndex = 0
foreach ($ruleId in @(Get-OrdinalStrings @($ruleWitnesses.Keys))) {
    $candidate = Copy-Object $ruleWitnesses[$ruleId]
    if ([string]$candidate.rule_id -ceq 'P4-RESULT-NO-SNAPSHOT') { continue }
    $globalIndex++
    $candidate.occurrence = "global:$globalIndex"
    foreach ($effect in @($candidate.coverage)) { $effect.occurrence = $candidate.occurrence }
    foreach ($gap in @($candidate.snapshot_gaps) + @($candidate.result_gaps)) { $gap.occurrence = $candidate.occurrence; $gap.member_id = "$($candidate.occurrence)|$($gap.owner_id)" }
    $globalSets.Add($candidate)
}
$before = $issues.Count
[void](Test-Composition $model $policy @($globalSets) 'global/all-admitted-rules-independent-occurrences' $issues)
if ($issues.Count -eq $before) { $successfulWitnesses++; $globalCompositionWitnesses++ }
$compositionLines.Add("global|rules=$($globalSets.Count)|families=15|rows=10")

$facegenAssetPairWitnesses = 0
$facegenKinds = @(
    'P4-FACEGEN-APPLICABLE-PRESENT',
    'P4-FACEGEN-APPLICABLE-ABSENT-ARCHIVE-SUPPORTED',
    'P4-FACEGEN-APPLICABLE-ABSENT-ARCHIVE-UNSUPPORTED',
    'P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED',
    'P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED'
)
$facegenFamily = @($model.fact_families | Where-Object { [string]$_.family -ceq 'face_gen' })[0]
for ($i = 0; $i -lt $facegenKinds.Count; $i++) {
    for ($j = $i; $j -lt $facegenKinds.Count; $j++) {
        $meshRule = @($facegenFamily.rules | Where-Object { [string]$_.rule_id -ceq $facegenKinds[$i] })[0]
        $tintRule = @($facegenFamily.rules | Where-Object { [string]$_.rule_id -ceq $facegenKinds[$j] })[0]
        $mesh = Get-RuleEffects $model 'face_gen' $meshRule "facegen-pair:${i}:${j}:mesh" $issues
        $tint = Get-RuleEffects $model 'face_gen' $tintRule "facegen-pair:${i}:${j}:tint" $issues
        $before = $issues.Count
        [void](Test-Composition $model $policy @($mesh,$tint) "facegen-pair/$($facegenKinds[$i])+$($facegenKinds[$j])" $issues)
        if ($issues.Count -eq $before) { $successfulWitnesses++; $facegenAssetPairWitnesses++ }
        $compositionLines.Add("facegen-pair|mesh=$($facegenKinds[$i])|tint=$($facegenKinds[$j])")
    }
}

$mutations = [System.Collections.Generic.List[object]]::new()
if (-not $SkipMutationTests) {
    $supportedRule = @($facegenFamily.rules | Where-Object { [string]$_.rule_id -ceq 'P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED' })[0]
    $unsupportedRule = @($facegenFamily.rules | Where-Object { [string]$_.rule_id -ceq 'P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED' })[0]
    $presentRule = @($facegenFamily.rules | Where-Object { [string]$_.rule_id -ceq 'P4-FACEGEN-APPLICABLE-PRESENT' })[0]
    $recordMutation = {
        param([string]$Id, [System.Collections.Generic.List[string]]$MutationIssues, [bool]$ModelDerived)
        $mutations.Add([pscustomobject]@{ id=$Id; rejected=($MutationIssues.Count -gt 0); model_derived=$ModelDerived; issues=@($MutationIssues | Sort-Object -Unique) })
    }
    $effectCases = [ordered]@{
        'omit-supported-loose-gap' = { param($s) $s.snapshot_gaps=@();$s.result_gaps=@();$s }
        'omit-snapshot-gap' = { param($s) $s.snapshot_gaps=@();$s }
        'omit-result-gap' = { param($s) $s.result_gaps=@();$s }
        'wrong-gap-population' = { param($s) $s.snapshot_gaps[0].population='face-gen-archive-assets';$s.result_gaps[0].population='face-gen-archive-assets';$s }
        'wrong-gap-capability' = { param($s) $s.snapshot_gaps[0].capability='archive-activation-and-member-precedence';$s.result_gaps[0].capability='archive-activation-and-member-precedence';$s }
        'wrong-gap-scope' = { param($s) $s.snapshot_gaps[0].scope='snapshot';$s.result_gaps[0].scope='snapshot';$s }
        'zero-affected-count' = { param($s) $s.snapshot_gaps[0].affected=0;$s.result_gaps[0].affected=0;$s }
        'multiple-count-one-path' = { param($s) $s.snapshot_gaps[0].affected=2;$s.result_gaps[0].affected=2;$s }
        'duplicate-owner-member' = { param($s) $s.snapshot_gaps=@($s.snapshot_gaps)+@((Copy-Object $s.snapshot_gaps[0]));$s.result_gaps=@($s.result_gaps)+@((Copy-Object $s.result_gaps[0]));$s }
        'coerce-unknown-to-absent' = { param($s) $s|Add-Member fact_assertions ([pscustomobject]@{loose_state='absent';present=$false;exact_absence_known=$true}) -Force;$s }
        'coerce-unknown-to-present' = { param($s) $s|Add-Member fact_assertions ([pscustomobject]@{loose_state='present';present=$true;exact_absence_known=$false}) -Force;$s }
        'remove-loose-denominator' = { param($s) $s.coverage[0].denominator=0;$s }
        'increment-loose-completion' = { param($s) $s.coverage[0].completed=1;$s }
        'drift-supported-archive' = { param($s) $s.coverage[1].completed=0;$s }
        'wrong-owned-population' = { param($s) $s.snapshot_gaps[0].owns_population='face-gen-archive-assets';$s.result_gaps[0].owns_population='face-gen-archive-assets';$s }
        'remove-owned-population' = { param($s) $s.snapshot_gaps[0].owns_population=$null;$s.result_gaps[0].owns_population=$null;$s }
        'misclassify-all-unknown' = { param($s) $s|Add-Member asserted_lifecycles @([pscustomobject]@{population='face-gen-loose-assets';state='completed_with_gaps'}) -Force;$s }
        'omit-fixed-row' = { param($s) $s|Add-Member fixed_rows_override @($policy.fixed_coverage_rows|Where-Object{$_ -cne 'plugins'}) -Force;$s }
        'reorder-fixed-rows' = { param($s) $rows=@($policy.fixed_coverage_rows);$tmp=$rows[0];$rows[0]=$rows[1];$rows[1]=$tmp;$s|Add-Member fixed_rows_override $rows -Force;$s }
        'publish-atomic-invalid' = { param($s) $s|Add-Member atomic_invalid $true -Force;$s.publishes=$true;$s }
    }
    foreach ($id in @($effectCases.Keys)) {
        $set = Get-RuleEffects $model 'face_gen' $supportedRule "mutation:$id" ([System.Collections.Generic.List[string]]::new())
        $set = & $effectCases[$id] $set
        $mutationIssues = [System.Collections.Generic.List[string]]::new()
        [void](Test-Composition $model $policy @($set) "mutation/$id" $mutationIssues)
        & $recordMutation $id $mutationIssues $true
    }

    $mixedUnknown = Get-RuleEffects $model 'face_gen' $supportedRule 'mutation:mixed:unknown' ([System.Collections.Generic.List[string]]::new())
    $mixedPresent = Get-RuleEffects $model 'face_gen' $presentRule 'mutation:mixed:present' ([System.Collections.Generic.List[string]]::new())
    $mixedUnknown | Add-Member -NotePropertyName asserted_lifecycles -NotePropertyValue @([pscustomobject]@{population='face-gen-loose-assets';state='unsupported'}) -Force
    $mixed = @($mixedUnknown, $mixedPresent)
    $mutationIssues = [System.Collections.Generic.List[string]]::new(); [void](Test-Composition $model $policy $mixed 'mutation/misclassify-mixed' $mutationIssues); & $recordMutation 'misclassify-mixed' $mutationIssues $true

    $noSnapshotSet = Copy-Object $ruleWitnesses['P4-RESULT-NO-SNAPSHOT']
    $publishedSet = Get-RuleEffects $model 'face_gen' $presentRule 'mutation:no-snapshot:asset' ([System.Collections.Generic.List[string]]::new())
    $mutationIssues = [System.Collections.Generic.List[string]]::new(); [void](Test-Composition $model $policy @($noSnapshotSet,$publishedSet) 'mutation/no-snapshot-with-family' $mutationIssues); & $recordMutation 'no-snapshot-with-family' $mutationIssues $true

    $reuseA = Get-RuleEffects $model 'face_gen' $supportedRule 'mutation:reused-path' ([System.Collections.Generic.List[string]]::new())
    $reuseB = Get-RuleEffects $model 'face_gen' $supportedRule 'mutation:reused-path' ([System.Collections.Generic.List[string]]::new())
    $mutationIssues = [System.Collections.Generic.List[string]]::new(); [void](Test-Composition $model $policy @($reuseA,$reuseB) 'mutation/reused-obligation-owner' $mutationIssues); & $recordMutation 'reused-obligation-owner' $mutationIssues $true

    foreach ($policyMutation in @('extra-projection-family','swapped-fixed-row-order')) {
        $mutatedPolicy = Copy-Object $policy
        if ($policyMutation -ceq 'extra-projection-family') { $mutatedPolicy.projection_only_families = @($mutatedPolicy.projection_only_families) + @('face_gen') }
        else { $rows=@($mutatedPolicy.fixed_coverage_rows);$tmp=$rows[0];$rows[0]=$rows[1];$rows[1]=$tmp;$mutatedPolicy.fixed_coverage_rows=$rows }
        $mutationIssues = [System.Collections.Generic.List[string]]::new(); Test-AcceptedPolicy $mutatedPolicy $model $mutationIssues; & $recordMutation $policyMutation $mutationIssues $true
    }

    foreach ($ruleMutation in @('missing-constructor','duplicate-constructor','unknown-disposition','invented-higher-layer','bad-atomic-boundary')) {
        $mutatedModel = Copy-Object $model
        $targetFamily = @($mutatedModel.fact_families | Where-Object { [string]$_.family -ceq 'face_gen' })[0]
        $targetRule = @($targetFamily.rules | Where-Object { [string]$_.rule_id -ceq 'P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED' })[0]
        switch ($ruleMutation) {
            'missing-constructor' { $targetRule.outcomes = @($targetRule.outcomes | Where-Object { @($_.constructor_groups) -cnotcontains 'FC-FACEGEN-ASSET' }) }
            'duplicate-constructor' { $targetRule.outcomes[0].constructor_groups = @($targetRule.outcomes[0].constructor_groups) + @('FC-FACEGEN-ASSET') }
            'unknown-disposition' { $targetRule.outcomes[0].disposition = 'invented' }
            'invented-higher-layer' { $targetRule.minimum_layer = 'structural' }
            'bad-atomic-boundary' { $targetRule.atomic_boundary = 'AB-NOT-DECLARED' }
        }
        $mutationIssues = [System.Collections.Generic.List[string]]::new(); Test-RuleStructures $mutatedModel $mutationIssues; & $recordMutation $ruleMutation $mutationIssues $true
    }

    foreach ($overlayMutation in @('change-inherited-outcome','change-coverage-arithmetic','remove-inherited-archive-gap','change-schema-version','add-delta-property')) {
        $mutatedOverlay = Copy-Object $overlay
        switch ($overlayMutation) {
            'change-inherited-outcome' { $mutatedOverlay.delta.replaced_publication_rules[0].rule.outcomes[1].value_rule = 'invented replacement' }
            'change-coverage-arithmetic' { $mutatedOverlay.delta.replaced_publication_rules[0].rule.coverage_effect.completion = 'increment-one' }
            'remove-inherited-archive-gap' { $mutatedOverlay.delta.replaced_publication_rules[1].rule.gap_effects = @($mutatedOverlay.delta.replaced_publication_rules[1].rule.gap_effects | Where-Object { [string]$_.gap_rule_id -cne 'P4-GAP-ARCHIVE' }) }
            'change-schema-version' { $mutatedOverlay.version = '1.0.0' }
            'add-delta-property' { $mutatedOverlay.delta | Add-Member -NotePropertyName unauthorized -NotePropertyValue $true }
        }
        $mutationIssues = [System.Collections.Generic.List[string]]::new(); Test-OverlaySchema $mutatedOverlay $schema $ModelPath $SchemaPath $mutationIssues; Test-BoundedDelta $base $mutatedOverlay $mutationIssues; & $recordMutation $overlayMutation $mutationIssues $true
    }
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
$gapEffectInstances = @($effectInstances | ForEach-Object { @($_.snapshot_gaps) }).Count
$gapBearingAdmittedStates = @($effectInstances | Where-Object { @($_.snapshot_gaps).Count -gt 0 }).Count
$snapshotGapAggregates = $gapEffectInstances
$resultGapAggregates = @($effectInstances | ForEach-Object { @($_.result_gaps) }).Count
$constructorAssignments = @($effectInstances | ForEach-Object { @($_.constructors) }).Count
$factTemplatesComposed = @($effectInstances | ForEach-Object { @($_.fact_templates) }).Count
$modelDerivedMutations = @($mutations | Where-Object { [bool]$_.model_derived }).Count
$compositionDigest = Get-TextHash @(Get-OrdinalStrings $compositionLines)
$mutationDigest = Get-TextHash @(Get-OrdinalStrings @($mutations | ForEach-Object { "$($_.id)|$($_.rejected)|$([string]::Join(';', @(Get-OrdinalStrings @($_.issues))))" }))
$issueDigest = Get-TextHash @(Get-OrdinalStrings $uniqueIssues)
$success = $uniqueIssues.Count -eq 0 -and $mutationRejected -eq $mutations.Count

$summary = @"
{"schema_id":"infinium.m1-slice4.protocol-5-global-composition-summary/1.1.0","success":$($success.ToString().ToLowerInvariant()),"semantic_model_identity":"$($overlay.model_id)/$($overlay.version)","semantic_model_sha256":"$(Get-Hash $ModelPath)","semantic_model_schema_sha256":"$(Get-Hash $SchemaPath)","base_model_sha256":"$(Get-Hash $basePath)","validator_sha256":"$(Get-Hash $validatorPath)","required_runtimes":["Windows PowerShell 5.1","PowerShell 7"],"runs_per_runtime":2,"byte_for_byte_runtime_agreement_required":true,"families":$(@($model.fact_families).Count),"publication_rules":$($ruleIds.Count),"admitted_rules_composed":$admittedRulesComposed,"gap_rules":$($gapIds.Count),"coverage_populations":$(@($model.coverage_registry).Count),"atomic_boundaries":$($atomicBoundaryIds.Count),"raw_states":$rawStates,"admitted_states_composed":$admittedStates,"complete_snapshot_witnesses":$completeSnapshotWitnesses,"no_snapshot_witnesses":$noSnapshotWitnesses,"successful_witnesses":$successfulWitnesses,"pairwise_compositions":$pairwiseCompositions,"global_composition_witnesses":$globalCompositionWitnesses,"facegen_asset_pair_witnesses":$facegenAssetPairWitnesses,"capability_event_witnesses":$capabilityEventWitnesses,"coverage_effect_instances":$coverageEffectInstances,"positive_coverage_effects":$positiveCoverageEffects,"incomplete_coverage_effects":$incompleteCoverageEffects,"gap_effect_instances":$gapEffectInstances,"gap_bearing_admitted_states":$gapBearingAdmittedStates,"snapshot_gap_aggregates":$snapshotGapAggregates,"result_gap_aggregates":$resultGapAggregates,"constructor_assignments":$constructorAssignments,"fact_templates_composed":$factTemplatesComposed,"excluded_states":$excludedStates,"invalid_states":$invalidStates,"uncovered_compositions":$uncoveredCompositions,"overlapping_states":$overlappingStates,"contradictions":$contradictions,"duplicate_or_overlapping_ownership":$duplicateOwnership,"mutations":$($mutations.Count),"model_derived_mutations":$modelDerivedMutations,"mutations_rejected":$mutationRejected,"composition_digest":"$compositionDigest","mutation_digest":"$mutationDigest","issue_digest":"$issueDigest"}
"@
$summaryObject = $summary | ConvertFrom-Json
$summaryObject.schema_id = 'infinium.m1-slice4.protocol-5-global-composition-summary/1.2.0'
$summaryObject | Add-Member -NotePropertyName projection_rule_effect_witnesses -NotePropertyValue $projectionRuleEffectWitnesses
$summaryObject | Add-Member -NotePropertyName effectless_bypasses -NotePropertyValue 0
$summary = ($summaryObject | ConvertTo-Json -Depth 10 -Compress) + "`n"
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
