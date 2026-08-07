[CmdletBinding()]
param(
    [string]$RepresentationModelPath,
    [string]$RepresentationSchemaPath,
    [string]$DocumentSchemaPath,
    [string]$LedgerPath,
    [string]$SummaryPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
if (-not $RepresentationModelPath) { $RepresentationModelPath = Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-model.json' }
if (-not $RepresentationSchemaPath) { $RepresentationSchemaPath = Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-model.schema.json' }
if (-not $DocumentSchemaPath) { $DocumentSchemaPath = Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-projection-document.schema.json' }
if (-not $LedgerPath) { $LedgerPath = Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-rule-coverage-ledger.json' }

function Read-Json([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing $Label at '$Path'." }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json }
    catch { throw "Invalid $Label JSON at '$Path': $($_.Exception.Message)" }
}
function Get-Hash([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Copy-Object([object]$Value) { return $Value | ConvertTo-Json -Depth 100 -Compress | ConvertFrom-Json }
function Has-Property([object]$Value, [string]$Name) { return $null -ne $Value -and $null -ne $Value.PSObject.Properties[$Name] }
function Add-Issue([Collections.Generic.List[string]]$Issues, [string]$Code, [string]$Message) { $Issues.Add("$Code|$Message") }
function Get-OrdinalStrings([object[]]$Values) {
    $items = [string[]]@($Values | ForEach-Object { [string]$_ })
    [Array]::Sort($items, [StringComparer]::Ordinal)
    return $items
}
function Test-SameOrdered([object[]]$Left, [object[]]$Right) {
    $a=@($Left); $b=@($Right); if($a.Count-ne$b.Count){return $false}
    for($i=0;$i-lt$a.Count;$i++){if([string]$a[$i]-cne[string]$b[$i]){return $false}}
    return $true
}
function Test-SameSet([object[]]$Left, [object[]]$Right) {
    $a=@(Get-OrdinalStrings $Left);$b=@(Get-OrdinalStrings $Right)
    return Test-SameOrdered $a $b
}
function Get-TextHash([string[]]$Lines) {
    $bytes=[Text.Encoding]::UTF8.GetBytes([string]::Join("`n",@($Lines)))
    $sha=[Security.Cryptography.SHA256]::Create()
    try{return([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
}
function Get-CanonicalJson([object]$Value) { return ($Value | ConvertTo-Json -Depth 100 -Compress) }

# A deliberately bounded JSON-Schema evaluator.  It implements every keyword used by the
# two public projection schemas, so Windows PowerShell 5.1 does not silently skip schema
# application.  PowerShell 7 additionally runs its native Test-Json implementation below.
function Resolve-LocalRef([object]$Root, [string]$Ref) {
    if(-not $Ref.StartsWith('#/')){throw "Only local JSON Schema references are permitted: $Ref"}
    $node=$Root
    foreach($raw in $Ref.Substring(2).Split('/')){
        $name=$raw.Replace('~1','/').Replace('~0','~')
        $property=$node.PSObject.Properties[$name]
        if($null-eq$property){throw "Unresolved JSON Schema reference '$Ref'."}
        $node=$property.Value
    }
    return $node
}
function Test-JsonType([object]$Value,[string]$Type) {
    switch($Type){
        'null' { return $null-eq$Value }
        'object' { return $null-ne$Value -and $Value-isnot[string] -and $Value-isnot[Array] -and $Value-isnot[ValueType] }
        'array' { return $Value-is[Array] }
        'string' { return $Value-is[string] }
        'boolean' { return $Value-is[bool] }
        'integer' { return $Value-is[sbyte] -or $Value-is[byte] -or $Value-is[int16] -or $Value-is[uint16] -or $Value-is[int32] -or $Value-is[uint32] -or $Value-is[int64] -or $Value-is[uint64] }
        'number' { return $Value-is[ValueType] -and $Value-isnot[bool] }
        default { return $false }
    }
}
function Test-SchemaNode([object]$Value,[object]$Schema,[object]$Root,[string]$At,[Collections.Generic.List[string]]$Errors) {
    if($Schema-is[bool]){if(-not[bool]$Schema){$Errors.Add("$At rejected by false schema")};return}
    if(Has-Property $Schema '$ref'){Test-SchemaNode $Value (Resolve-LocalRef $Root ([string]$Schema.'$ref')) $Root $At $Errors}
    if(Has-Property $Schema 'allOf'){foreach($child in @($Schema.allOf)){Test-SchemaNode $Value $child $Root $At $Errors}}
    if(Has-Property $Schema 'oneOf'){
        $matches=0
        foreach($child in @($Schema.oneOf)){$trial=[Collections.Generic.List[string]]::new();Test-SchemaNode $Value $child $Root $At $trial;if($trial.Count-eq0){$matches++}}
        if($matches-ne1){$Errors.Add("$At matches $matches oneOf branches")}
    }
    if(Has-Property $Schema 'if'){
        $trial=[Collections.Generic.List[string]]::new();Test-SchemaNode $Value $Schema.if $Root $At $trial
        if($trial.Count-eq0 -and (Has-Property $Schema 'then')){Test-SchemaNode $Value $Schema.then $Root $At $Errors}
        elseif($trial.Count-ne0 -and (Has-Property $Schema 'else')){Test-SchemaNode $Value $Schema.else $Root $At $Errors}
    }
    if(Has-Property $Schema 'const'){
        if((Get-CanonicalJson $Value)-cne(Get-CanonicalJson $Schema.const)){$Errors.Add("$At violates const")}
    }
    if(Has-Property $Schema 'enum'){
        $actual=Get-CanonicalJson $Value;$found=$false
        foreach($candidate in @($Schema.enum)){if($actual-ceq(Get-CanonicalJson $candidate)){$found=$true;break}}
        if(-not$found){$Errors.Add("$At violates enum")}
    }
    if(Has-Property $Schema 'type'){
        $types=@($Schema.type|ForEach-Object{[string]$_});$ok=$false
        foreach($type in $types){if(Test-JsonType $Value $type){$ok=$true;break}}
        if(-not$ok){$Errors.Add("$At has wrong type");return}
    }
    if($Value-is[string]){
        if((Has-Property $Schema 'minLength')-and$Value.Length-lt[int]$Schema.minLength){$Errors.Add("$At is shorter than minLength")}
        if((Has-Property $Schema 'maxLength')-and$Value.Length-gt[int]$Schema.maxLength){$Errors.Add("$At is longer than maxLength")}
        if((Has-Property $Schema 'pattern')-and-not[regex]::IsMatch($Value,[string]$Schema.pattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant)){$Errors.Add("$At violates pattern")}
    }
    if($Value-is[ValueType]-and$Value-isnot[bool]){
        if((Has-Property $Schema 'minimum')-and[decimal]$Value-lt[decimal]$Schema.minimum){$Errors.Add("$At is below minimum")}
    }
    if($Value-is[Array]){
        $items=@($Value)
        if((Has-Property $Schema 'minItems')-and$items.Count-lt[int]$Schema.minItems){$Errors.Add("$At has too few items")}
        if((Has-Property $Schema 'maxItems')-and$items.Count-gt[int]$Schema.maxItems){$Errors.Add("$At has too many items")}
        if((Has-Property $Schema 'uniqueItems')-and[bool]$Schema.uniqueItems){$seen=@{};foreach($item in $items){$key=Get-CanonicalJson $item;if($seen.ContainsKey($key)){$Errors.Add("$At has duplicate items");break};$seen[$key]=$true}}
        if(Has-Property $Schema 'prefixItems'){
            $prefix=@($Schema.prefixItems);for($i=0;$i-lt[Math]::Min($items.Count,$prefix.Count);$i++){Test-SchemaNode $items[$i] $prefix[$i] $Root "$At[$i]" $Errors}
        }
        if(Has-Property $Schema 'items'){
            if($Schema.items-is[bool]){if(-not[bool]$Schema.items-and$items.Count-gt@(if(Has-Property $Schema 'prefixItems'){$Schema.prefixItems}else{@()}).Count){$Errors.Add("$At has forbidden trailing items")}}
            else{for($i=0;$i-lt$items.Count;$i++){Test-SchemaNode $items[$i] $Schema.items $Root "$At[$i]" $Errors}}
        }
        if(Has-Property $Schema 'contains'){
            $found=$false;foreach($item in $items){$trial=[Collections.Generic.List[string]]::new();Test-SchemaNode $item $Schema.contains $Root "$At[contains]" $trial;if($trial.Count-eq0){$found=$true;break}}
            if(-not$found){$Errors.Add("$At violates contains")}
        }
    }
    if($null-ne$Value-and$Value-isnot[string]-and$Value-isnot[Array]-and$Value-isnot[ValueType]){
        $names=@($Value.PSObject.Properties.Name)
        if(Has-Property $Schema 'required'){foreach($name in @($Schema.required)){if($names-cnotcontains[string]$name){$Errors.Add("$At missing required '$name'")}}}
        if(Has-Property $Schema 'properties'){
            foreach($propertySchema in $Schema.properties.PSObject.Properties){$actual=$Value.PSObject.Properties[$propertySchema.Name];if($null-ne$actual){Test-SchemaNode $actual.Value $propertySchema.Value $Root "$At.$($propertySchema.Name)" $Errors}}
        }
        if((Has-Property $Schema 'additionalProperties')-and$Schema.additionalProperties-is[bool]-and-not[bool]$Schema.additionalProperties){
            $allowed=if(Has-Property $Schema 'properties'){@($Schema.properties.PSObject.Properties.Name)}else{@()}
            foreach($name in $names){if($allowed-cnotcontains$name){$Errors.Add("$At has additional property '$name'")}}
        }
    }
}
function Test-AgainstSchema([object]$Value,[object]$Schema,[string]$Label,[Collections.Generic.List[string]]$Issues) {
    $errors=[Collections.Generic.List[string]]::new();Test-SchemaNode $Value $Schema $Schema '$' $errors
    if($errors.Count-ne0){Add-Issue $Issues 'SCHEMA' "$Label failed manual schema validation: $(@(Get-OrdinalStrings $errors)[0..([Math]::Min(4,$errors.Count-1))]-join'; ')";return $false}
    return $true
}

function Test-Condition([object]$State,[object[]]$Conditions){
    foreach($condition in @($Conditions)){
        $property=$State.PSObject.Properties[[string]$condition.dimension];if($null-eq$property){return $false}
        $actual=[string]$property.Value;$values=@($condition.values|ForEach-Object{[string]$_})
        switch([string]$condition.operator){
            'equals'{if($values.Count-ne1-or$actual-cne$values[0]){return $false}}
            'in'{if($values-cnotcontains$actual){return $false}}
            'not-in'{if($values-ccontains$actual){return $false}}
            default{return $false}
        }
    }
    return $true
}
function Get-StateProduct([object]$Model,[string[]]$Dimensions,[int]$Index=0,[object]$Prefix=$null){
    if($null-eq$Prefix){$Prefix=[ordered]@{}}
    if($Index-ge$Dimensions.Count){[pscustomobject]$Prefix;return}
    $dimension=$Dimensions[$Index]
    foreach($value in @($Model.dimensions.$dimension.values)){$next=[ordered]@{};foreach($key in $Prefix.Keys){$next[$key]=$Prefix[$key]};$next[$dimension]=[string]$value;Get-StateProduct $Model $Dimensions ($Index+1) $next}
}
function Expand-FactTemplate([string]$Template){
    $seed=$Template -replace '\{seg\([^}]+\)\}','x' -replace '\{[^}|]+_D4\}','0000'
    $pending=[Collections.Generic.Queue[string]]::new();$pending.Enqueue($seed);$output=[Collections.Generic.List[string]]::new()
    while($pending.Count-gt0){$value=$pending.Dequeue();$match=[regex]::Match($value,'\{([^{}|]+(?:\|[^{}|]+)+)\}');if(-not$match.Success){$output.Add($value);continue};foreach($choice in $match.Groups[1].Value.Split('|')){$pending.Enqueue($value.Substring(0,$match.Index)+$choice+$value.Substring($match.Index+$match.Length))}}
    return @($output)
}
function Split-FactId([string]$FactId){
    $segments=@($FactId.Split('/'));$family=$segments[0]
    if($family-ceq'result'){return [pscustomobject]@{family=$family;object_id='root';property_id=($segments[1..($segments.Count-1)]-join'/')}}
    $identitySegments=if($family-ceq'taxonomy'){7}elseif(@('gaps','result_gaps')-ccontains$family){2}else{1}
    $objectId=$segments[1..$identitySegments]-join'/'
    $propertyId=$segments[($identitySegments+1)..($segments.Count-1)]-join'/'
    return [pscustomobject]@{family=$family;object_id=$objectId;property_id=$propertyId}
}
function New-Property([string]$RelativeId,[string]$RuleId,[object]$Constructor,[string]$Disposition){
    $type=[string]@($Constructor.value_types)[0];if($Disposition-ceq'typed_null'){$type='null'}
    $value=switch($type){'string'{'x'}'integer'{1}'number'{[double]1.5}'boolean'{$true}'null'{$null}default{'x'}}
    return [pscustomobject][ordered]@{property_id=$RelativeId;source_rule_id=$RuleId;fact_type=[string]@($Constructor.fact_type)[0];value_type=$type;value=$value}
}
function Get-RuleProjection([object]$Rule,[hashtable]$ConstructorObjects){
    $groups=[Collections.Generic.List[string]]::new();$properties=[Collections.Generic.List[object]]::new();$templates=[Collections.Generic.List[string]]::new();$objectIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($outcome in @($Rule.outcomes)){
        if(@('exact_value','typed_null','accepted_unknown','mixed_by_constructor')-cnotcontains[string]$outcome.disposition){continue}
        foreach($groupId in @($outcome.constructor_groups)){
            if(-not$ConstructorObjects.ContainsKey([string]$groupId)){continue}
            if($groups-cnotcontains[string]$groupId){$groups.Add([string]$groupId)}
            $constructor=$ConstructorObjects[[string]$groupId]
            foreach($template in @($constructor.fact_id_templates)){
                foreach($path in @(Expand-FactTemplate ([string]$template))){$split=Split-FactId $path;$templates.Add($path);[void]$objectIds.Add([string]$split.object_id);$properties.Add((New-Property ([string]$split.property_id) ([string]$Rule.rule_id) $constructor ([string]$outcome.disposition)))}
            }
        }
    }
    return [pscustomobject]@{groups=@($groups);properties=@($properties);templates=@($templates);object_ids=@($objectIds)}
}
function New-ProjectionObject([string]$ObjectId,[string[]]$RuleIds,[string[]]$Groups,[object[]]$Properties){
    return [pscustomobject][ordered]@{object_id=$ObjectId;rule_ids=@($RuleIds);constructor_groups=@($Groups);properties=@($Properties)}
}
function New-ResultObject([string]$RuleId,[bool]$Snapshot){
    $properties=@(
        [pscustomobject][ordered]@{property_id='snapshot_present';source_rule_id=$RuleId;fact_type='state';value_type='boolean';value=$Snapshot},
        [pscustomobject][ordered]@{property_id='failure_present';source_rule_id=$RuleId;fact_type='state';value_type='boolean';value=(-not$Snapshot)}
    )
    return New-ProjectionObject 'root' @($RuleId) @('FC-RESULT-STATE') $properties
}
function Convert-LedgerProjectionObject([object]$Object,[string]$RuleId){
    $properties=@($Object.properties|ForEach-Object{[pscustomobject][ordered]@{property_id=[string]$_.property_id;source_rule_id=$RuleId;fact_type=[string]$_.fact_type;value_type=[string]$_.value_type;value=$_.value}})
    return New-ProjectionObject ([string]$Object.object_id) @($RuleId) @($Object.constructor_groups|ForEach-Object{[string]$_}) $properties
}
function Get-PropertyWitnessKey([string]$Family,[object]$Object,[object]$Property){
    $factId=if($Family-ceq'result'){"result/$($Property.property_id)"}else{"$Family/$($Object.object_id)/$($Property.property_id)"}
    return "$factId|$($Property.fact_type)|$($Property.value_type)|$(Get-CanonicalJson $Property.value)"
}
function Test-ExpectedPropertyType([object]$Property){
    $id=[string]$Property.property_id;$type=[string]$Property.value_type
    if($id-match'(^|/)(load_order|origin_local_id|raw_flags|configuration_flags|template_flags|ordinal|count|denominator|completed)$'-or$id-match'^taxonomy_version/(major|minor|patch)$'){return ($type -ceq 'integer')}
    if($id-match'(^|/)(deleted|compressed|uses_template|templates_traits|ai_data_present|face_gen_head|present|exact_absence_known|snapshot_present|failure_present)$'){return ($type -ceq 'boolean')}
    if($id-match'^placement/(position|rotation)/(x|y|z)$'){return ($type -ceq 'number')}
    if($id-match'winner_provider_id$'){return ($type -cin @('string','null'))}
    if($id-ceq'component'-or$id-ceq'code'){return ($type -cin @('string','null'))}
    return ($type -ceq 'string')
}
function Test-FaceGenWitness([object]$Entry){
    if([string]$Entry.family-cne'face_gen'-or[string]$Entry.classification-cne'admitted'-or[bool]$Entry.expected_canonical_result.exact_no_fact){return $true}
    $objects=@($Entry.exact_projection_objects);if($objects.Count-ne1){return $false};$props=@($objects[0].properties);$state=$Entry.expected_canonical_result.state
    $availability=if(Has-Property $state 'asset_availability'){[string]$state.asset_availability}else{$null}
    foreach($asset in @('mesh','tint')){
        $provider=@($props|Where-Object{[string]$_.property_id-match"^$asset/provider_ids/"});$winner=@($props|Where-Object{[string]$_.property_id-ceq"$asset/winner_provider_id"});$present=@($props|Where-Object{[string]$_.property_id-ceq"$asset/present"});$absence=@($props|Where-Object{[string]$_.property_id-ceq"$asset/exact_absence_known"})
        if($winner.Count-ne1-or$present.Count-ne1-or$absence.Count-ne1){return $false}
        if($availability-ceq'present'){if($provider.Count-lt1-or[string]$winner[0].value_type-cne'string'-or-not[bool]$present[0].value-or[bool]$absence[0].value){return $false}}
        else{if($provider.Count-ne0-or[string]$winner[0].value_type-cne'null'-or$null-ne$winner[0].value-or[bool]$present[0].value-or([bool]$absence[0].value-ne($availability-ceq'absent'))){return $false}}
    }
    return $true
}
function Get-LedgerClosureProblems([object]$Candidate,[object]$Model,[object]$Successor,[hashtable]$Constructors,[string[]]$SemanticRules,[string]$SuccessorHash){
    $problems=[Collections.Generic.List[string]]::new();$entries=@($Candidate.entries);$byRule=@{};$duplicates=0
    foreach($entry in $entries){$id=[string]$entry.rule_id;if($byRule.ContainsKey($id)){$duplicates++}else{$byRule[$id]=$entry}}
    if([int]$Candidate.rule_count-ne77-or$entries.Count-ne77-or$byRule.Count-ne77-or$duplicates-ne0){$problems.Add('cardinality')}
    if(-not(Test-SameSet @($byRule.Keys) $SemanticRules)){$problems.Add('rule-inventory')}
    if([string]$Candidate.semantic_model.model_id-cne[string]$Successor.model_id-or[string]$Candidate.semantic_model.version-cne[string]$Successor.version-or[string]$Candidate.semantic_model.sha256-cne$SuccessorHash){$problems.Add('semantic-identity')}
    foreach($family in @($Model.fact_families)){foreach($rule in @($family.rules)){$id=[string]$rule.rule_id;if(-not$byRule.ContainsKey($id)){continue};$entry=$byRule[$id];$projection=Get-RuleProjection $rule $Constructors
        if([string]$entry.family-cne[string]$family.family){$problems.Add("$id/family")}
        if(-not(Test-SameSet @($entry.constructor_fact_template_inventory) @($projection.templates))){$problems.Add("$id/constructor-inventory")}
        if(-not(Test-SameOrdered @($entry.accepted_disposition) @($rule.outcomes|ForEach-Object{[string]$_.disposition}))){$problems.Add("$id/disposition")}
        if([string]$entry.semantic_source.model-cne"$($Successor.model_id)/$($Successor.version)"-or-not(Test-SameOrdered @($entry.semantic_source.authorities) @($rule.authorities))){$problems.Add("$id/semantic-source")}
        $expectedBindings=[Collections.Generic.List[string]]::new();foreach($outcome in @($rule.outcomes)){foreach($group in @($outcome.constructor_groups)){$expectedBindings.Add("$group|$($family.family)|$($outcome.disposition)")}};$actualBindings=@($entry.constructor_bindings|ForEach-Object{"$($_.constructor_group)|$($_.family)|$($_.disposition)"});if(-not(Test-SameOrdered $actualBindings @($expectedBindings))){$problems.Add("$id/constructor-bindings")}
        $expectedCoverage=[Collections.Generic.List[string]]::new();foreach($effect in @(Get-CoverageEffects $rule)){$expectedCoverage.Add("$($effect.population)|$($effect.denominator)|$($effect.completion)|$($effect.state_effect)")};$actualCoverage=@($entry.coverage_effects|ForEach-Object{"$($_.population)|$($_.denominator)|$($_.completion)|$($_.state_effect)"});if(-not(Test-SameOrdered $actualCoverage @($expectedCoverage))){$problems.Add("$id/coverage-effects")}
        $expectedGaps=@($rule.gap_effects|ForEach-Object{"$($_.gap_rule_id)|$($_.owner_id)|$($_.affected_count)|$($_.scope)"});$actualGaps=@($entry.gap_effects|ForEach-Object{"$($_.gap_rule_id)|$($_.owner_id)|$($_.affected_count)|$($_.scope)"});if(-not(Test-SameOrdered $actualGaps $expectedGaps)){$problems.Add("$id/gap-effects")}
        $propertyFacts=[Collections.Generic.List[string]]::new();$propertyIds=[Collections.Generic.List[string]]::new()
        foreach($object in @($entry.exact_projection_objects)){$objectFacts=[Collections.Generic.List[string]]::new();$objectProperties=[Collections.Generic.List[string]]::new()
            foreach($property in @($object.properties)){$fact=[string]$property.fact_id;$propertyFacts.Add($fact);$objectFacts.Add($fact);$propertyIds.Add([string]$property.property_id);$objectProperties.Add([string]$property.property_id)
                $expectedFact=if([string]$family.family-ceq'result'){"result/$($property.property_id)"}else{"$($family.family)/$($object.object_id)/$($property.property_id)"};if($fact-cne$expectedFact){$problems.Add("$id/fact-id")}
                $constructorId=[string]$property.constructor_group;if(-not$Constructors.ContainsKey($constructorId)-or@($entry.constructor_bindings.constructor_group)-cnotcontains$constructorId){$problems.Add("$id/property-constructor")}else{$constructor=$Constructors[$constructorId];if(@($constructor.fact_type)-cnotcontains[string]$property.fact_type-or@($constructor.value_types)-cnotcontains[string]$property.value_type){$problems.Add("$id/property-contract")}}
                if(-not(Test-JsonType $property.value ([string]$property.value_type))-or-not(Test-ExpectedPropertyType $property)){$problems.Add("$id/property-value-type")}
            }
            if(-not(Test-SameSet @($object.fact_templates) @($objectFacts))-or-not(Test-SameSet @($object.property_templates) @($objectProperties))){$problems.Add("$id/object-inventory")}
        }
        if(@($propertyFacts).Count-ne@($propertyFacts|Select-Object -Unique).Count-or-not(Test-SameSet @($entry.exact_fact_templates) @($propertyFacts))-or-not(Test-SameSet @($entry.exact_property_templates) @($propertyIds|Select-Object -Unique))-or-not(Test-SameSet @($entry.expected_canonical_result.exact_target_fact_ids) @($propertyFacts))){$problems.Add("$id/exact-properties")}
        if([string]$entry.classification-ceq'admitted'){if([string]::IsNullOrWhiteSpace([string]$entry.witness_id)-or@($entry.admitted_states).Count-lt1){$problems.Add("$id/admitted-witness")}}
        elseif([string]$entry.classification-ceq'terminal'){if([string]::IsNullOrWhiteSpace([string]$entry.rejection_witness_id)-or[bool]$entry.expected_canonical_result.publishes-or-not[bool]$entry.expected_canonical_result.terminal_rejection){$problems.Add("$id/terminal-witness")}}else{$problems.Add("$id/classification")}
        if(-not(Test-FaceGenWitness $entry)){$problems.Add("$id/facegen-semantics")}
    }}
    return @($problems|Select-Object -Unique)
}
function Get-EffectValue([string]$Effect){if($Effect-ceq'increment-two'){return 2};if($Effect-ceq'increment-one'){return 1};return 0}
function Get-CoverageEffects([object]$Rule){
    $effects=[Collections.Generic.List[object]]::new();$effects.Add($Rule.coverage_effect)
    if(Has-Property $Rule.coverage_effect 'additional_population_effects'){foreach($effect in @($Rule.coverage_effect.additional_population_effects)){$effects.Add($effect)}}
    return @($effects)
}
function Get-GapPair([object]$GapRule){
    $population=([string]$GapRule.population_template) -replace '\{[^}]+\}','x'
    return [pscustomobject]@{population=$population;missing_capability=[string]$GapRule.missing_capability}
}
function Test-RuleGapOwnsPopulation([object]$Rule,[string]$Population,[object[]]$GapRules){
    foreach($effect in @($Rule.gap_effects)){
        $gap=@($GapRules|Where-Object{[string]$_.rule_id-ceq[string]$effect.gap_rule_id})
        if($gap.Count-ne1){continue};$gapPopulation=[string]$gap[0].population_template
        if($gapPopulation-ceq$Population){return $true}
        if(@('npc-records','race-records','placed-reference-records','unsupported-records')-ccontains$Population){return $true}
    }
    return $false
}
function Get-Lifecycle([int]$Denominator,[int]$Completed,[bool]$HasGap){
    if($Denominator-eq0){return 'completed'}
    if($Completed-eq$Denominator-and-not$HasGap){return 'completed'}
    if($Completed-eq$Denominator-and$HasGap){return 'completed_with_gaps'}
    if($Completed-eq0-and$HasGap){return 'unsupported'}
    if($Completed-lt$Denominator-and$HasGap){return 'completed_with_gaps'}
    return 'invalid'
}
function New-CoverageObject([string]$Population,[int]$Denominator,[int]$Completed,[string]$Lifecycle,[string]$RuleId){
    $properties=@(
        [pscustomobject][ordered]@{property_id='population';source_rule_id=$RuleId;fact_type='coverage';value_type='string';value=$Population},
        [pscustomobject][ordered]@{property_id='denominator';source_rule_id=$RuleId;fact_type='coverage';value_type='integer';value=$Denominator},
        [pscustomobject][ordered]@{property_id='completed';source_rule_id=$RuleId;fact_type='coverage';value_type='integer';value=$Completed},
        [pscustomobject][ordered]@{property_id='state';source_rule_id=$RuleId;fact_type='coverage';value_type='string';value=$Lifecycle}
    )
    return New-ProjectionObject $Population @($RuleId) @('FC-COVERAGE-ROW') $properties
}
function New-GapObject([string]$Family,[object]$Pair,[int]$Count,[string]$RuleId,[string]$Constructor){
    $id=("$($Pair.population)/$($Pair.missing_capability)").Replace(':','%3A')
    $properties=@(
        [pscustomobject][ordered]@{property_id='population';source_rule_id=$RuleId;fact_type='gap';value_type='string';value=[string]$Pair.population},
        [pscustomobject][ordered]@{property_id='missing_capability';source_rule_id=$RuleId;fact_type='gap';value_type='string';value=[string]$Pair.missing_capability},
        [pscustomobject][ordered]@{property_id='denominator';source_rule_id=$RuleId;fact_type='gap';value_type='integer';value=$Count}
    )
    return New-ProjectionObject $id @($RuleId) @($Constructor) $properties
}
function Get-ProjectionPropertyValue([object]$Object,[string]$PropertyId){return @($Object.properties|Where-Object{[string]$_.property_id-ceq$PropertyId})[0].value}
function New-EmptyFamilies([string[]]$FamilyOrder){$families=[ordered]@{};foreach($family in $FamilyOrder){$families[$family]=@()};return $families}
function Test-ProjectionDocumentManual([object]$Document,[object]$DocumentSchema,[object]$Representation,[object]$Successor,[string]$SuccessorHash,[string]$RepresentationHash,[string[]]$FamilyOrder,[string[]]$CoverageOrder,[hashtable]$FamilyRuleMap){
    if([string]$Document.schema_id-cne[string]$DocumentSchema.properties.schema_id.const-or[string]$Document.protocol_id-cne[string]$DocumentSchema.properties.protocol_id.const-or[string]$Document.projection_id-cne[string]$DocumentSchema.properties.projection_id.const-or[string]$Document.projection_version-cne[string]$DocumentSchema.properties.projection_version.const){return $false}
    if([string]$Document.semantic_model.model_id-cne[string]$Successor.model_id-or[string]$Document.semantic_model.version-cne[string]$Successor.version-or[string]$Document.semantic_model.sha256-cne$SuccessorHash){return $false}
    if([string]$Document.representation_model.model_id-cne[string]$Representation.model_id-or[string]$Document.representation_model.version-cne[string]$Representation.version-or[string]$Document.representation_model.sha256-cne$RepresentationHash){return $false}
    if(@('completed','completed_with_gaps','invalid_input','changed_during_read','failed')-cnotcontains[string]$Document.state){return $false}
    if(-not(Test-SameSet @($Document.families.PSObject.Properties.Name) $FamilyOrder)){return $false}
    if(@($Document.families.result).Count-ne1){return $false};$noSnapshot=@($Document.families.result[0].rule_ids)-ccontains'P4-RESULT-NO-SNAPSHOT'
    if($noSnapshot){foreach($family in $FamilyOrder){if(@('result','result_gaps')-ccontains$family){continue};if(@($Document.families.$family).Count-ne0){return $false}}}else{if(@($Document.families.coverage).Count-ne10){return $false};if(-not(Test-SameOrdered @($Document.families.coverage.object_id) $CoverageOrder)){return $false}}
    $pathPattern=[string]$DocumentSchema.'$defs'.canonical_path.pattern;$factIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($family in $FamilyOrder){$objectIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);foreach($object in @($Document.families.$family)){
        if(-not[regex]::IsMatch([string]$object.object_id,$pathPattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant)-or-not$objectIds.Add([string]$object.object_id)){return $false}
        if(@($object.rule_ids).Count-lt1-or@($object.constructor_groups).Count-lt1-or@($object.properties).Count-lt1){return $false}
        if(@($object.rule_ids).Count-ne@($object.rule_ids|Select-Object -Unique).Count-or@($object.constructor_groups).Count-ne@($object.constructor_groups|Select-Object -Unique).Count){return $false}
        $propertyRules=@(Get-OrdinalStrings @($object.properties.source_rule_id|Select-Object -Unique));if(-not(Test-SameOrdered @($object.rule_ids) $propertyRules)){return $false}
        foreach($ruleId in @($object.rule_ids)){if(@($FamilyRuleMap[$family])-cnotcontains[string]$ruleId){return $false}}
        foreach($property in @($object.properties)){
            if(-not[regex]::IsMatch([string]$property.property_id,$pathPattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant)){return $false}
            if(@('string','integer','number','boolean','null')-cnotcontains[string]$property.value_type){return $false}
            if([string]$property.value_type-ceq'null'){if($null-ne$property.value){return $false}}
            else{$typeOk=Test-JsonType $property.value ([string]$property.value_type);if(-not$typeOk){return $false}}
            $factId=if($family-ceq'result'){"result/$($property.property_id)"}else{"$family/$($object.object_id)/$($property.property_id)"};if(-not$factIds.Add($factId)){return $false}
        }
    }}
    return $true
}
function Test-ExactDocumentAgainstLedger([object]$Document,[object]$Entry,[string]$Family,[object]$DocumentSchema,[object]$Representation,[object]$Successor,[string]$SuccessorHash,[string]$RepresentationHash,[string[]]$FamilyOrder,[string[]]$CoverageOrder,[hashtable]$FamilyRuleMap){
    if(-not(Test-ProjectionDocumentManual $Document $DocumentSchema $Representation $Successor $SuccessorHash $RepresentationHash $FamilyOrder $CoverageOrder $FamilyRuleMap)){return $false}
    $actual=[Collections.Generic.List[string]]::new();foreach($object in @($Document.families.$Family)){foreach($property in @($object.properties|Where-Object{[string]$_.source_rule_id-ceq[string]$Entry.rule_id})){$actual.Add((Get-PropertyWitnessKey $Family $object $property))}}
    $expected=[Collections.Generic.List[string]]::new();foreach($object in @($Entry.exact_projection_objects)){foreach($property in @($object.properties)){$expected.Add((Get-PropertyWitnessKey $Family $object $property))}}
    if(-not(Test-SameSet @($actual) @($expected))){return $false}
    $actualCoverage=@($Document.families.coverage|ForEach-Object{"$($_.object_id)|$(Get-ProjectionPropertyValue $_ 'denominator')|$(Get-ProjectionPropertyValue $_ 'completed')|$(Get-ProjectionPropertyValue $_ 'state')|$($_.rule_ids[0])"});$expectedCoverage=@($Entry.expected_canonical_result.coverage_rows|ForEach-Object{"$($_.population)|$($_.denominator)|$($_.completed)|$($_.state)|$($_.publication_rule_id)"});if(-not(Test-SameOrdered $actualCoverage $expectedCoverage)){return $false}
    $actualGaps=@($Document.families.gaps|ForEach-Object{"$($_.rule_ids[0])|$(Get-ProjectionPropertyValue $_ 'population')|$(Get-ProjectionPropertyValue $_ 'missing_capability')|$(Get-ProjectionPropertyValue $_ 'denominator')"});$expectedGaps=@($Entry.expected_canonical_result.gap_objects|ForEach-Object{"$($_.publication_rule_id)|$($_.population)|$($_.missing_capability)|$($_.affected)"});if(-not(Test-SameSet $actualGaps $expectedGaps)){return $false}
    $actualResultGaps=@($Document.families.result_gaps|ForEach-Object{"$($_.rule_ids[0])|$(Get-ProjectionPropertyValue $_ 'population')|$(Get-ProjectionPropertyValue $_ 'missing_capability')|$(Get-ProjectionPropertyValue $_ 'denominator')"});$expectedResultGaps=@($Entry.expected_canonical_result.result_gap_objects|ForEach-Object{"$($_.publication_rule_id)|$($_.population)|$($_.missing_capability)|$($_.affected)"});if(-not(Test-SameSet $actualResultGaps $expectedResultGaps)){return $false}
    $isNoSnapshot=(-not[bool]$Entry.lifecycle.snapshot);if(($isNoSnapshot-and[string]$Document.state-cne'failed')-or(-not$isNoSnapshot-and[string]$Document.state-ceq'failed')){return $false}
    return $true
}

$issues=[Collections.Generic.List[string]]::new()
$representation=Read-Json $RepresentationModelPath 'representation model'
$representationSchema=Read-Json $RepresentationSchemaPath 'representation-model schema'
$documentSchema=Read-Json $DocumentSchemaPath 'projection-document schema'
$ledger=Read-Json $LedgerPath 'WP1V rule-coverage ledger'
$successorPath=Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-successor-model.json'
$successorSchemaPath=Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-successor-model.schema.json'
$successor=Read-Json $successorPath 'successor semantic model'
$basePath=Join-Path $repoRoot ([string]$successor.base_model.path)
$base=Read-Json $basePath 'immutable predecessor model'
$compositionPath=Join-Path $repoRoot 'docs/evaluation/specifications/m1-slice4-protocol-5-global-composition-summary.json'
$composition=Read-Json $compositionPath 'global composition summary'
$globalValidatorPath=Join-Path $repoRoot 'eng/validate-m1-slice4-protocol5-global-composition.ps1'

[void](Test-AgainstSchema $representation $representationSchema 'representation model' $issues)
if(Get-Command Test-Json -ErrorAction SilentlyContinue){
    try{$nativeOk=Test-Json -Json (Get-Content -LiteralPath $RepresentationModelPath -Raw) -Schema (Get-Content -LiteralPath $RepresentationSchemaPath -Raw) -ErrorAction Stop;if(-not$nativeOk){Add-Issue $issues 'NATIVE-SCHEMA' 'PowerShell Test-Json rejected the representation model.'}}
    catch{Add-Issue $issues 'NATIVE-SCHEMA' "PowerShell Test-Json could not validate the representation model: $($_.Exception.Message)"}
}

$requiredCompositionFields=@('schema_id','success','semantic_model_identity','semantic_model_sha256','semantic_model_schema_sha256','base_model_sha256','validator_sha256','required_runtimes','runs_per_runtime','byte_for_byte_runtime_agreement_required','families','publication_rules','admitted_rules_composed','projection_rule_effect_witnesses','effectless_bypasses','gap_rules','coverage_populations','atomic_boundaries','raw_states','admitted_states_composed','complete_snapshot_witnesses','no_snapshot_witnesses','global_composition_witnesses','facegen_asset_pair_witnesses','successful_witnesses','pairwise_compositions','capability_event_witnesses','coverage_effect_instances','positive_coverage_effects','incomplete_coverage_effects','gap_effect_instances','gap_bearing_admitted_states','snapshot_gap_aggregates','result_gap_aggregates','constructor_assignments','fact_templates_composed','excluded_states','invalid_states','uncovered_compositions','overlapping_states','contradictions','duplicate_or_overlapping_ownership','mutations','model_derived_mutations','mutations_rejected','composition_digest','mutation_digest','issue_digest')
foreach($name in $requiredCompositionFields){if(-not(Has-Property $composition $name)){Add-Issue $issues 'COMPOSITION-FIELD' "Global composition summary lacks '$name'."}}
if((Has-Property $composition 'schema_id')-and[string]$composition.schema_id-cne'infinium.m1-slice4.protocol-5-global-composition-summary/1.2.0'){Add-Issue $issues 'COMPOSITION-IDENTITY' 'Global composition summary schema identity is not exact.'}
if((Has-Property $composition 'semantic_model_identity')-and[string]$composition.semantic_model_identity-cne"$($successor.model_id)/$($successor.version)"){Add-Issue $issues 'COMPOSITION-IDENTITY' 'Global composition summary semantic identity is not exact.'}
if((Has-Property $composition 'base_model_sha256')-and[string]$composition.base_model_sha256-cne'09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5'){Add-Issue $issues 'COMPOSITION-BASE' 'Global composition summary does not bind immutable predecessor model 1.2.0.'}
$expectedCompositionCounts=[ordered]@{families=15;publication_rules=77;admitted_rules_composed=63;projection_rule_effect_witnesses=10;effectless_bypasses=0;gap_rules=9;coverage_populations=10;atomic_boundaries=11;raw_states=23660;admitted_states_composed=110;complete_snapshot_witnesses=109;no_snapshot_witnesses=1;successful_witnesses=869;pairwise_compositions=741;global_composition_witnesses=1;facegen_asset_pair_witnesses=15;capability_event_witnesses=2;coverage_effect_instances=65;positive_coverage_effects=47;incomplete_coverage_effects=14;gap_effect_instances=20;gap_bearing_admitted_states=19;snapshot_gap_aggregates=20;result_gap_aggregates=20;constructor_assignments=183;fact_templates_composed=732;excluded_states=6180;invalid_states=17370;uncovered_compositions=0;overlapping_states=0;contradictions=0;duplicate_or_overlapping_ownership=0;mutations=35;model_derived_mutations=35;mutations_rejected=35}
foreach($entry in $expectedCompositionCounts.GetEnumerator()){if((Has-Property $composition ([string]$entry.Key))-and[int64]$composition.($entry.Key)-ne[int64]$entry.Value){Add-Issue $issues 'COMPOSITION-COUNTER' "$($entry.Key) expected $($entry.Value), got $($composition.($entry.Key))."}}
if((Has-Property $composition 'success')-and-not[bool]$composition.success){Add-Issue $issues 'COMPOSITION' 'Global composition proof did not pass.'}
foreach($name in @('uncovered_compositions','overlapping_states','contradictions','duplicate_or_overlapping_ownership')){if((Has-Property $composition $name)-and[int64]$composition.$name-ne0){Add-Issue $issues 'COMPOSITION-COUNTER' "$name must be zero."}}
if((Has-Property $composition 'mutations')-and(Has-Property $composition 'mutations_rejected')-and[int]$composition.mutations-ne[int]$composition.mutations_rejected){Add-Issue $issues 'COMPOSITION-MUTATION' 'Every global-composition mutation must be rejected.'}
if((Has-Property $composition 'model_derived_mutations')-and[int]$composition.model_derived_mutations-lt1){Add-Issue $issues 'COMPOSITION-MUTATION' 'Global proof has no model-derived mutation.'}
if((Has-Property $composition 'facegen_asset_pair_witnesses')-and[int]$composition.facegen_asset_pair_witnesses-ne15){Add-Issue $issues 'COMPOSITION-FACEGEN' 'Global proof must contain exactly 15 unordered FaceGen asset-pair witnesses.'}
if((Has-Property $composition 'validator_sha256')-and[string]$composition.validator_sha256-cne(Get-Hash $globalValidatorPath)){Add-Issue $issues 'COMPOSITION-VALIDATOR' 'Global summary does not bind the exact validator bytes.'}
$runtimeNames=@('Windows PowerShell 5.1','PowerShell 7')
if((Has-Property $composition 'required_runtimes')-and-not(Test-SameOrdered @($composition.required_runtimes) $runtimeNames)){Add-Issue $issues 'COMPOSITION-RUNTIMES' 'Global proof runtime inventory/order is not exact.'}
if((Has-Property $composition 'runs_per_runtime')-and[int]$composition.runs_per_runtime-ne2){Add-Issue $issues 'COMPOSITION-RUNTIMES' 'Global proof must require two runs per runtime.'}
if((Has-Property $composition 'byte_for_byte_runtime_agreement_required')-and-not[bool]$composition.byte_for_byte_runtime_agreement_required){Add-Issue $issues 'COMPOSITION-RUNTIMES' 'Global proof must require byte-for-byte runtime agreement.'}
if((Has-Property $composition 'semantic_model_sha256')-and[string]$composition.semantic_model_sha256-cne(Get-Hash $successorPath)){Add-Issue $issues 'COMPOSITION-IDENTITY' 'Global composition summary does not bind the current successor model.'}
if((Has-Property $composition 'semantic_model_schema_sha256')-and[string]$composition.semantic_model_schema_sha256-cne(Get-Hash $successorSchemaPath)){Add-Issue $issues 'COMPOSITION-IDENTITY' 'Global composition summary does not bind the current successor schema.'}
if(-not[bool]$representation.composition_authority.mandatory-or[string]$representation.composition_authority.sha256-cne(Get-Hash $compositionPath)-or[string]$representation.composition_authority.composition_digest-cne[string]$composition.composition_digest){Add-Issue $issues 'COMPOSITION-AUTHORITY' 'Representation model does not pin the whole mandatory global composition summary and digest.'}
if(-not[bool]$representation.rule_ledger_authority.mandatory-or[int]$representation.rule_ledger_authority.rules-ne77-or[string]$representation.rule_ledger_authority.sha256-cne(Get-Hash $LedgerPath)){Add-Issue $issues 'LEDGER-AUTHORITY' 'Representation model does not pin the exact mandatory 77-rule WP1V ledger.'}
if([string]$representation.semantic_authority.model_id-cne[string]$successor.model_id-or[string]$representation.semantic_authority.version-cne[string]$successor.version-or[string]$representation.semantic_authority.sha256-cne(Get-Hash $successorPath)){Add-Issue $issues 'SEMANTIC-AUTHORITY' 'Representation model does not bind the exact accepted successor model.'}
if([string]$representation.schema_id-cne[string]$representationSchema.properties.schema_id.const-or[string]$representation.version-cne[string]$representationSchema.properties.version.const-or[string]$representation.status-cne'accepted'){Add-Issue $issues 'REPRESENTATION-IDENTITY' 'Representation model/schema identity or status drifted.'}
if([string]$documentSchema.'$id'-cne[string]$representation.protocol.document_schema_id-or[string]$documentSchema.properties.protocol_id.const-cne'infinium.evaluator-v2/5'-or[string]$documentSchema.properties.projection_version.const-cne[string]$representation.protocol.projection_version){Add-Issue $issues 'DOCUMENT-IDENTITY' 'Projection document identity does not match the representation model.'}
if([string]$documentSchema.properties.semantic_model.properties.model_id.const-cne[string]$successor.model_id-or[string]$documentSchema.properties.semantic_model.properties.version.const-cne[string]$successor.version){Add-Issue $issues 'DOCUMENT-AUTHORITY' 'Projection document schema does not pin the accepted successor model identity.'}
if([string]$documentSchema.properties.representation_model.properties.model_id.const-cne[string]$representation.model_id-or[string]$documentSchema.properties.representation_model.properties.version.const-cne[string]$representation.version){Add-Issue $issues 'DOCUMENT-REPRESENTATION' 'Projection document schema does not pin the accepted representation model identity.'}

$model=Copy-Object $base;$model.model_id=[string]$successor.model_id;$model.version=[string]$successor.version;$model.gap_rules=@($model.gap_rules)+@($successor.delta.added_gap_rules)
foreach($replacement in @($successor.delta.replaced_publication_rules)){$family=@($model.fact_families|Where-Object{[string]$_.family-ceq[string]$replacement.family});$index=-1;for($i=0;$i-lt@($family[0].rules).Count;$i++){if([string]$family[0].rules[$i].rule_id-ceq[string]$replacement.replaces_rule_id){$index=$i;break}};if($index-lt0){Add-Issue $issues 'DELTA' "Missing replacement source '$($replacement.replaces_rule_id)'."}else{$family[0].rules[$index]=Copy-Object $replacement.rule}}

if([string]$successor.authority.contract-cne'infinium.m1-slice4.protocol-5-evidence-contract/1.0.1'){Add-Issue $issues 'AUTHORITY-CONTRACT' 'Successor model does not point to its exact 1.0.1 authority contract.'}
if([string]$ledger.schema_id-cne'infinium.m1-slice4.protocol-5-rule-coverage-ledger/1.0.0'-or[string]$ledger.work_id-cne'M1/S4.5/PRE-B2/V5/WP1V'){Add-Issue $issues 'LEDGER-IDENTITY' 'Rule ledger identity is not the accepted WP1V identity.'}
if([string]$ledger.semantic_model.model_id-cne[string]$successor.model_id-or[string]$ledger.semantic_model.version-cne[string]$successor.version-or[string]$ledger.semantic_model.sha256-cne(Get-Hash $successorPath)){Add-Issue $issues 'LEDGER-AUTHORITY' 'Rule ledger does not bind the exact accepted successor model bytes.'}

$expectedFamilies=@($model.fact_families|ForEach-Object{[string]$_.family})
if(-not(Test-SameOrdered @($representation.family_order) $expectedFamilies)-or$expectedFamilies.Count-ne15){Add-Issue $issues 'FAMILIES' 'Representation family order differs from the exact 15-family semantic order.'}
$familyContracts=@($representation.family_contracts)
if(-not(Test-SameSet @($familyContracts|ForEach-Object{[string]$_.family}) $expectedFamilies)-or$familyContracts.Count-ne15){Add-Issue $issues 'FAMILY-CONTRACTS' 'Family contracts are missing, extra, or duplicated.'}
$familyRuleMap=@{};foreach($contract in $familyContracts){$familyRuleMap[[string]$contract.family]=@($contract.source_rules|ForEach-Object{[string]$_})}

$constructorObjects=@{};$constructorFamilies=@{};$allSemanticRules=[Collections.Generic.List[string]]::new()
foreach($family in @($model.fact_families)){foreach($constructor in @($family.constructor_groups)){$id=[string]$constructor.id;$constructorObjects[$id]=$constructor;$constructorFamilies[$id]=[string]$family.family};foreach($rule in @($family.rules)){$allSemanticRules.Add([string]$rule.rule_id)}}
$ledgerEntries=@($ledger.entries);$ledgerByRule=@{};$duplicateLedgerRules=0
foreach($entry in $ledgerEntries){$id=[string]$entry.rule_id;if($ledgerByRule.ContainsKey($id)){$duplicateLedgerRules++}else{$ledgerByRule[$id]=$entry}}
$ledgerClosureProblems=@(Get-LedgerClosureProblems $ledger $model $successor $constructorObjects @($allSemanticRules) (Get-Hash $successorPath));foreach($problem in $ledgerClosureProblems){Add-Issue $issues 'LEDGER-CLOSURE' $problem}
if([int]$ledger.rule_count-ne77-or$ledgerEntries.Count-ne77-or$ledgerByRule.Count-ne77-or$duplicateLedgerRules-ne0){Add-Issue $issues 'LEDGER-CARDINALITY' "Ledger must contain 77 unique rules exactly once; declared/entries/unique/duplicates=$($ledger.rule_count)/$($ledgerEntries.Count)/$($ledgerByRule.Count)/$duplicateLedgerRules."}
if(-not(Test-SameSet @($ledgerByRule.Keys) @($allSemanticRules))){Add-Issue $issues 'LEDGER-RULES' 'Ledger rules are missing, unknown, or unbound relative to the accepted successor model.'}
foreach($family in @($model.fact_families)){foreach($rule in @($family.rules)){
    $id=[string]$rule.rule_id;if(-not$ledgerByRule.ContainsKey($id)){continue};$entry=$ledgerByRule[$id]
    if([string]$entry.family-cne[string]$family.family){Add-Issue $issues 'LEDGER-FAMILY' "$id claims '$($entry.family)' instead of '$($family.family)'."}
    $projection=Get-RuleProjection $rule $constructorObjects
    if(-not(Test-SameSet @($entry.constructor_fact_template_inventory) @($projection.templates))){Add-Issue $issues 'LEDGER-FACTS' "$id constructor fact-template inventory differs from accepted constructors."}
    if([string]$entry.semantic_source.model-cne"$($successor.model_id)/$($successor.version)"-or-not(Test-SameOrdered @($entry.semantic_source.authorities) @($rule.authorities))){Add-Issue $issues 'LEDGER-SOURCE' "$id semantic source or authority differs from the accepted rule."}
    $dispositions=@($rule.outcomes|ForEach-Object{[string]$_.disposition})
    if(-not(Test-SameOrdered @($entry.accepted_disposition) $dispositions)){Add-Issue $issues 'LEDGER-DISPOSITION' "$id disposition sequence differs from accepted authority."}
    $expectedBindings=[Collections.Generic.List[string]]::new();foreach($outcome in @($rule.outcomes)){foreach($group in @($outcome.constructor_groups)){$expectedBindings.Add("$group|$($family.family)|$($outcome.disposition)")}}
    $actualBindings=@($entry.constructor_bindings|ForEach-Object{"$($_.constructor_group)|$($_.family)|$($_.disposition)"})
    if(-not(Test-SameOrdered $actualBindings @($expectedBindings))){Add-Issue $issues 'LEDGER-CONSTRUCTOR' "$id constructor binding differs from accepted authority."}
    $expectedCoverage=[Collections.Generic.List[string]]::new();$effects=@($rule.coverage_effect);if(Has-Property $rule.coverage_effect 'additional_population_effects'){$effects+=@($rule.coverage_effect.additional_population_effects)};foreach($effect in $effects){$expectedCoverage.Add("$($effect.population)|$($effect.denominator)|$($effect.completion)|$($effect.state_effect)")}
    $actualCoverage=@($entry.coverage_effects|ForEach-Object{"$($_.population)|$($_.denominator)|$($_.completion)|$($_.state_effect)"})
    if(-not(Test-SameOrdered $actualCoverage @($expectedCoverage))){Add-Issue $issues 'LEDGER-COVERAGE' "$id coverage effects differ from accepted authority."}
    $expectedGaps=@($rule.gap_effects|ForEach-Object{"$($_.gap_rule_id)|$($_.owner_id)|$($_.affected_count)|$($_.scope)"});$actualGaps=@($entry.gap_effects|ForEach-Object{"$($_.gap_rule_id)|$($_.owner_id)|$($_.affected_count)|$($_.scope)"})
    if(-not(Test-SameOrdered $actualGaps $expectedGaps)){Add-Issue $issues 'LEDGER-GAPS' "$id gap effects differ from accepted authority."}
    $expectedMirrors=@($rule.gap_effects|Where-Object{[string]$_.scope-ceq'snapshot-and-result'}|ForEach-Object{"$($_.gap_rule_id)|true|result"});$actualMirrors=@($entry.result_gaps_mirror_effects|ForEach-Object{"$($_.gap_rule_id)|$(([bool]$_.required).ToString().ToLowerInvariant())|$($_.scope)"})
    if(-not(Test-SameOrdered $actualMirrors $expectedMirrors)){Add-Issue $issues 'LEDGER-RESULT-GAPS' "$id result_gaps mirror effects differ from accepted gap scope."}
    if([string]::IsNullOrWhiteSpace([string]$entry.mutation_or_negative_check)){Add-Issue $issues 'LEDGER-NEGATIVE' "$id lacks its omission/misrepresentation negative check."}
}}
$bindings=@($representation.constructor_bindings)
if(-not(Test-SameSet @($bindings|ForEach-Object{[string]$_.constructor_group}) @($constructorObjects.Keys))-or$bindings.Count-ne24){Add-Issue $issues 'CONSTRUCTORS' 'Constructor bindings differ from exactly 24 semantic constructor groups.'}
foreach($binding in $bindings){$id=[string]$binding.constructor_group;if(-not$constructorFamilies.ContainsKey($id)-or[string]$binding.family-cne[string]$constructorFamilies[$id]){Add-Issue $issues 'CONSTRUCTOR-FAMILY' "Constructor '$id' is bound to the wrong family."}}
$overrideBinding=@($bindings|Where-Object{[string]$_.constructor_group-ceq'FC-OVERRIDE-CONTRIBUTIONS'})
if($overrideBinding.Count-ne1-or[string]$overrideBinding[0].completion-cne'repeatable-one-or-more'){Add-Issue $issues 'OVERRIDE-CARDINALITY' 'Override contributions must be repeatable-one-or-more.'}
$layerRank=@{none=0;structural=1;observed=2;decoded=3;resolved=4;semantic=5};$acceptedDispositions=@('exact_value','typed_null','accepted_unknown','mixed_by_constructor','omit','no_fact','terminal_rejection');$ruleMaterializationLines=[Collections.Generic.List[string]]::new();$rulesMaterialized=0;$constructorsMaterialized=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($family in @($model.fact_families)){foreach($rule in @($family.rules)){$assigned=[Collections.Generic.List[string]]::new();foreach($outcome in @($rule.outcomes)){if($acceptedDispositions-cnotcontains[string]$outcome.disposition){Add-Issue $issues 'RULE-DISPOSITION' "$($rule.rule_id) has an unaccepted disposition."};foreach($constructorId in @($outcome.constructor_groups)){$assigned.Add([string]$constructorId);[void]$constructorsMaterialized.Add([string]$constructorId);if(-not$constructorObjects.ContainsKey([string]$constructorId)){Add-Issue $issues 'RULE-CONSTRUCTOR' "$($rule.rule_id) references unknown constructor '$constructorId'."}elseif(@('exact_value','typed_null','accepted_unknown','mixed_by_constructor')-ccontains[string]$outcome.disposition-and$layerRank[[string]$constructorObjects[[string]$constructorId].minimum_layer]-gt$layerRank[[string]$rule.minimum_layer]){Add-Issue $issues 'RULE-LAYER' "$($rule.rule_id) invents constructor '$constructorId' above its evidence layer."}}};if(@($assigned).Count-ne@($assigned|Select-Object -Unique).Count){Add-Issue $issues 'RULE-CONSTRUCTOR' "$($rule.rule_id) assigns a constructor more than once."};if($assigned-ccontains'FC-OVERRIDE-WINNER'-and$assigned-cnotcontains'FC-OVERRIDE-CONTRIBUTIONS'){Add-Issue $issues 'OVERRIDE-WINNER' "$($rule.rule_id) emits a winner without contributions."};$projection=Get-RuleProjection $rule $constructorObjects;$ruleMaterializationLines.Add("$($family.family)|$($rule.rule_id)|layer=$($rule.minimum_layer)|atomic=$($rule.atomic_boundary)|constructors=$(@(Get-OrdinalStrings $assigned)-join',')|facts=$(@(Get-OrdinalStrings @($projection.templates))-join',')");$rulesMaterialized++}}
if($rulesMaterialized-ne77-or$constructorsMaterialized.Count-ne24){Add-Issue $issues 'MATERIALIZATION' "Expected to materialize 77 rules and 24 constructors; got $rulesMaterialized/$($constructorsMaterialized.Count)."}

$allGapRules=@($model.gap_rules|ForEach-Object{[string]$_.rule_id});$coveragePopulations=@($model.coverage_registry|ForEach-Object{[string]$_.population});$atomicIds=@($model.atomic_boundaries|ForEach-Object{[string]$_.id})
$expectedExtraAtomic=@{
    result=@('AB-RESULT');plugins=@('AB-FORMKEY','AB-RESULT');override_chains=@('AB-RECORD-FRAMING','AB-FORMKEY','AB-FACTSET');npc_contributions=@('AB-RECORD-FRAMING','AB-LINK');race_contributions=@('AB-RECORD-FRAMING','AB-TYPED-VALUE','AB-FACTSET');placed_reference_contributions=@('AB-RECORD-FRAMING','AB-LINK','AB-PLACEMENT');allowlisted_fields=@('AB-FACTSET','AB-GAP');npcs=@('AB-FACTSET','AB-LINK');races=@('AB-FACTSET','AB-TYPED-VALUE','AB-RECORD-FRAMING');placed_references=@('AB-FACTSET','AB-LINK','AB-PLACEMENT');face_gen=@('AB-ASSET');taxonomy=@('AB-TAXONOMY');coverage=@('AB-COVERAGE','AB-RESULT');gaps=@('AB-GAP');result_gaps=@('AB-GAP','AB-RESULT')
}
$expectedGapDependencies=@{
    result=@();plugins=@();override_chains=@('P4-GAP-UNSUPPORTED-RECORD','P4-GAP-UNSUPPORTED-SHAPE');npc_contributions=@('P4-GAP-UNSUPPORTED-FIELD','P4-GAP-UNSUPPORTED-SHAPE','P4-GAP-LOCALIZED');race_contributions=@('P4-GAP-UNSUPPORTED-FIELD','P4-GAP-UNSUPPORTED-SHAPE');placed_reference_contributions=@('P4-GAP-UNSUPPORTED-FIELD','P4-GAP-UNSUPPORTED-SHAPE','P4-GAP-LOCALIZED');allowlisted_fields=@('P4-GAP-UNSUPPORTED-FIELD','P4-GAP-UNSUPPORTED-SHAPE');npcs=@();races=@();placed_references=@();face_gen=@('P4-GAP-ARCHIVE','P4-GAP-TEMPLATE','P4-GAP-RACE','P5-GAP-LOOSE-AVAILABILITY');taxonomy=@();coverage=@();gaps=$allGapRules;result_gaps=$allGapRules
}
foreach($family in @($model.fact_families)){
    $name=[string]$family.family;$contract=@($familyContracts|Where-Object{[string]$_.family-ceq$name})
    if($contract.Count-ne1){continue};$contract=$contract[0]
    if(-not(Test-SameSet @($contract.source_rules) @($family.rules.rule_id))){Add-Issue $issues 'FAMILY-RULES' "$name source-rule inventory is not exact."}
    $groups=@($family.constructor_groups.id);$contractGroups=@($contract.required_base_groups)+@($contract.conditional_groups);if(-not(Test-SameSet $contractGroups $groups)){Add-Issue $issues 'FAMILY-CONSTRUCTORS' "$name constructor inventory is not exact."}
    $populations=[Collections.Generic.List[string]]::new();$gaps=[Collections.Generic.List[string]]::new()
    foreach($rule in @($family.rules)){foreach($effect in @(Get-CoverageEffects $rule)){if($effect.population){$populations.Add([string]$effect.population)}};foreach($effect in @($rule.gap_effects)){$gaps.Add([string]$effect.gap_rule_id)};if($atomicIds-cnotcontains[string]$rule.atomic_boundary){Add-Issue $issues 'RULE-ATOMIC' "$($rule.rule_id) names an unknown atomic boundary."}}
    $gaps.Clear();foreach($gapId in @($expectedGapDependencies[$name])){$gaps.Add([string]$gapId)}
    if($name-ceq'coverage'){$populations.Clear();foreach($population in $coveragePopulations){$populations.Add($population)}}
    if(-not(Test-SameSet @($contract.coverage_populations) @($populations|Select-Object -Unique))){Add-Issue $issues 'FAMILY-COVERAGE' "$name coverage-population inventory is not exact."}
    if(-not(Test-SameSet @($contract.gap_rules) @($gaps|Select-Object -Unique))){Add-Issue $issues 'FAMILY-GAPS' "$name gap-rule inventory is not exact."}
    if(-not(Test-SameSet @($contract.atomic_groups) @($expectedExtraAtomic[$name]))){Add-Issue $issues 'FAMILY-ATOMIC' "$name atomic-boundary inventory is not exact."}
}

# The canonical path grammar is tested independently from the document witnesses.
$pathPattern=[string]$documentSchema.'$defs'.canonical_path.pattern
$validPaths=@('root','a','a/b','a.b/c_d-1~x','%3A','a%3Ab','%C3%A9','a/%E2%82%AC','a/%F0%9F%98%80')
$invalidPaths=@('','.', '..','a/.','a/..','/a','a/','a//b','%2F','%5C','%00','%41','%FF','%C2','%c3%a9','a\b',[string][char]1)
$pathPass=0;$pathReject=0
foreach($path in $validPaths){if([regex]::IsMatch($path,$pathPattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant)){$pathPass++}else{Add-Issue $issues 'PATH-VALID' "Canonical path rejected valid '$path'."}}
foreach($path in $invalidPaths){if(-not[regex]::IsMatch($path,$pathPattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant)){$pathReject++}else{Add-Issue $issues 'PATH-INVALID' "Canonical path accepted invalid '$path'."}}

$raw=0;$admitted=0;$uncovered=0;$overlap=0;$schemaWitnesses=0;$noSnapshotWitnesses=0;$snapshotWitnesses=0;$exactFactWitnesses=0;$factTemplates=0;$constructorAssignments=0
$witnessLines=[Collections.Generic.List[string]]::new();$admittedRuleIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$witnessedRuleIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$documents=[Collections.Generic.List[object]]::new();$documentRuleIds=[Collections.Generic.List[string]]::new()
foreach($family in @($model.fact_families)){
    $dimensions=@($family.dimensions_used|ForEach-Object{[string]$_});$states=if($dimensions.Count-eq0){@([pscustomobject]@{})}else{@(Get-StateProduct $model $dimensions)}
    foreach($state in $states){
        $raw++;$regions=@($family.state_space.admitted_regions|Where-Object{Test-Condition $state @($_.when)});if($regions.Count-eq0){continue};if($regions.Count-gt1){$overlap++;continue};$admitted++
        $rules=@($family.rules|Where-Object{Test-Condition $state @($_.when)});if($rules.Count-ne1){$uncovered++;continue};$rule=$rules[0];[void]$admittedRuleIds.Add([string]$rule.rule_id)
        if(-not$witnessedRuleIds.Add([string]$rule.rule_id)){continue};$ledgerEntry=$ledgerByRule[[string]$rule.rule_id]
        $projection=Get-RuleProjection $rule $constructorObjects;$constructorAssignments+=@($projection.groups).Count;$factTemplates+=@($projection.templates).Count
        $families=New-EmptyFamilies $expectedFamilies;$isNoSnapshot=(-not[bool]$ledgerEntry.lifecycle.snapshot);$resultRule=if([string]$family.family-ceq'result'){[string]$rule.rule_id}elseif($isNoSnapshot){'P4-RESULT-NO-SNAPSHOT'}else{'P4-RESULT-PUBLISHED'}
        if([string]$family.family-ceq'result' -and @($ledgerEntry.exact_projection_objects).Count-gt0){$families.result=@($ledgerEntry.exact_projection_objects|ForEach-Object{Convert-LedgerProjectionObject $_ ([string]$rule.rule_id)})}else{$families.result=@(New-ResultObject $resultRule (-not$isNoSnapshot))}
        $gapAggregates=@{};foreach($effect in @($rule.gap_effects)){$gapRule=@($model.gap_rules|Where-Object{[string]$_.rule_id-ceq[string]$effect.gap_rule_id})[0];$pair=Get-GapPair $gapRule;$key="$($pair.population)|$($pair.missing_capability)";if(-not$gapAggregates.ContainsKey($key)){$gapAggregates[$key]=[pscustomobject]@{pair=$pair;count=0}};$gapAggregates[$key].count++}
        if(-not$isNoSnapshot){
            $rowValues=@{};foreach($population in $coveragePopulations){$rowValues[$population]=[pscustomobject]@{denominator=0;completed=0}}
            if([string]$family.family-ceq'coverage'){
                if([string]$rule.rule_id-ceq'P4-COVERAGE-COMPLETE'){$rowValues['face-gen-loose-assets'].denominator=1;$rowValues['face-gen-loose-assets'].completed=1}
                elseif([string]$rule.rule_id-ceq'P4-COVERAGE-INCOMPLETE'){$rowValues['face-gen-loose-assets'].denominator=2;$rowValues['face-gen-loose-assets'].completed=1;$gapRule=@($model.gap_rules|Where-Object{[string]$_.rule_id-ceq'P5-GAP-LOOSE-AVAILABILITY'})[0];$pair=Get-GapPair $gapRule;$gapAggregates["$($pair.population)|$($pair.missing_capability)"]=[pscustomobject]@{pair=$pair;count=1}}
            }else{$effects=@(Get-CoverageEffects $rule);foreach($effect in $effects){if(-not$effect.population){continue};$row=$rowValues[[string]$effect.population];$row.denominator+=Get-EffectValue([string]$effect.denominator);$row.completed+=Get-EffectValue([string]$effect.completion)}}
            if([string]$family.family-ceq'gaps'-and[string]$rule.rule_id-ceq'P4-GAPS-EMIT'){$gapRule=@($model.gap_rules|Where-Object{[string]$_.rule_id-ceq'P4-GAP-LOCALIZED'})[0];$pair=Get-GapPair $gapRule;$gapAggregates["$($pair.population)|$($pair.missing_capability)"]=[pscustomobject]@{pair=$pair;count=1}}
            if([string]$family.family-ceq'result_gaps'-and[string]$rule.rule_id-ceq'P4-RESULTGAPS-WITH-SNAPSHOT'){$gapRule=@($model.gap_rules|Where-Object{[string]$_.rule_id-ceq'P4-GAP-LOCALIZED'})[0];$pair=Get-GapPair $gapRule;$gapAggregates["$($pair.population)|$($pair.missing_capability)"]=[pscustomobject]@{pair=$pair;count=1}}
            foreach($population in $coveragePopulations){$row=$rowValues[$population];$hasGap=if([string]$family.family-ceq'coverage'){$row.denominator-gt0-and($gapAggregates.Values|Where-Object{[string]$_.pair.population-ceq$population}|Select-Object -First 1)}else{$row.denominator-gt0-and(Test-RuleGapOwnsPopulation $rule $population @($model.gap_rules))};$lifecycle=Get-Lifecycle $row.denominator $row.completed ([bool]$hasGap);if($lifecycle-ceq'invalid'){$uncovered++;Add-Issue $issues 'COVERAGE-LIFECYCLE' "$($rule.rule_id) creates incomplete $population without its owning gap.";$lifecycle='failed'};$coverageRule=if($row.denominator-eq0){'P4-COVERAGE-ZERO'}elseif($lifecycle-ceq'completed'){'P4-COVERAGE-COMPLETE'}else{'P4-COVERAGE-INCOMPLETE'};if([string]$family.family-ceq'coverage' -and [string]$rule.rule_id-ceq'P4-COVERAGE-ZERO'){$coverageRule='P4-COVERAGE-ZERO'};$families.coverage+=@(New-CoverageObject $population $row.denominator $row.completed $lifecycle $coverageRule)}
            foreach($key in @(Get-OrdinalStrings @($gapAggregates.Keys))){$aggregate=$gapAggregates[$key];$families.gaps+=@(New-GapObject 'gaps' $aggregate.pair $aggregate.count 'P4-GAPS-EMIT' 'FC-GAPS-ROW');$families.result_gaps+=@(New-GapObject 'result_gaps' $aggregate.pair $aggregate.count 'P4-RESULTGAPS-WITH-SNAPSHOT' 'FC-RESULT-GAPS-ROW')}
            $snapshotPairs=@($families.gaps|ForEach-Object{"$($_.properties[0].value)|$($_.properties[1].value)|$($_.properties[2].value)"});$resultPairs=@($families.result_gaps|ForEach-Object{"$($_.properties[0].value)|$($_.properties[1].value)|$($_.properties[2].value)"});if(-not(Test-SameSet $snapshotPairs $resultPairs)){Add-Issue $issues 'GAP-MIRROR' "$($rule.rule_id) snapshot/result gap collections differ."}
            if(@('result','coverage','gaps','result_gaps')-cnotcontains[string]$family.family-and@($ledgerEntry.exact_projection_objects).Count-gt0){$families[[string]$family.family]=@($ledgerEntry.exact_projection_objects|ForEach-Object{Convert-LedgerProjectionObject $_ ([string]$rule.rule_id)})}
            $snapshotWitnesses++
        }else{if([string]$family.family-ceq'result_gaps'-and[string]$rule.rule_id-ceq'P4-RESULTGAPS-NO-SNAPSHOT'){$pair=Get-GapPair @($model.gap_rules)[0];$families.result_gaps=@(New-GapObject 'result_gaps' $pair 1 'P4-RESULTGAPS-NO-SNAPSHOT' 'FC-RESULT-GAPS-ROW')};$noSnapshotWitnesses++}
        $document=[pscustomobject][ordered]@{schema_id=[string]$documentSchema.properties.schema_id.const;protocol_id='infinium.evaluator-v2/5';projection_id=[string]$representation.protocol.projection_id;projection_version=[string]$representation.protocol.projection_version;semantic_model=[pscustomobject][ordered]@{model_id=[string]$successor.model_id;version=[string]$successor.version;sha256=(Get-Hash $successorPath)};representation_model=[pscustomobject][ordered]@{model_id=[string]$representation.model_id;version=[string]$representation.version;sha256=(Get-Hash $RepresentationModelPath)};state=if($isNoSnapshot){'failed'}elseif($families.gaps.Count-gt0){'completed_with_gaps'}else{'completed'};families=[pscustomobject]$families}
        if(Test-ProjectionDocumentManual $document $documentSchema $representation $successor (Get-Hash $successorPath) (Get-Hash $RepresentationModelPath) $expectedFamilies $coveragePopulations $familyRuleMap){$schemaWitnesses++}else{$uncovered++;Add-Issue $issues 'DOCUMENT-SCHEMA' "$($family.family)/$($rule.rule_id) failed deterministic manual document-schema validation."}
        foreach($objectFamily in $expectedFamilies){foreach($object in @($document.families.$objectFamily)){$propertyRuleIds=[Collections.Generic.List[string]]::new();foreach($property in @($object.properties)){if(Has-Property $property 'source_rule_id'){$propertyRuleIds.Add([string]$property.source_rule_id)}else{Add-Issue $issues 'PROVENANCE' "$($family.family)/$($regions[0].constraint_id)/$objectFamily property lacks source_rule_id."}};if(-not(Test-SameSet @($object.rule_ids) @($propertyRuleIds|Select-Object -Unique))){Add-Issue $issues 'PROVENANCE' "$($family.family)/$($regions[0].constraint_id)/$objectFamily object rule_ids differ from property source_rule_id set."}}}
        $actualRows=@($document.families.coverage|ForEach-Object{"$($_.object_id)|$(Get-ProjectionPropertyValue $_ 'denominator')|$(Get-ProjectionPropertyValue $_ 'completed')|$(Get-ProjectionPropertyValue $_ 'state')|$($_.rule_ids[0])"});$expectedRows=@($ledgerEntry.expected_canonical_result.coverage_rows|ForEach-Object{"$($_.population)|$($_.denominator)|$($_.completed)|$($_.state)|$($_.publication_rule_id)"})
        if(-not(Test-SameOrdered $actualRows $expectedRows)){$uncovered++;Add-Issue $issues 'EXACT-COVERAGE-WITNESS' "$($rule.rule_id) coverage rows, arithmetic, lifecycle, or publication ownership differ from the ledger."}
        $actualGaps=@($document.families.gaps|ForEach-Object{"$($_.rule_ids[0])|$(Get-ProjectionPropertyValue $_ 'population')|$(Get-ProjectionPropertyValue $_ 'missing_capability')|$(Get-ProjectionPropertyValue $_ 'denominator')"});$expectedGaps=@($ledgerEntry.expected_canonical_result.gap_objects|ForEach-Object{"$($_.publication_rule_id)|$($_.population)|$($_.missing_capability)|$($_.affected)"})
        if(-not(Test-SameSet $actualGaps $expectedGaps)){$uncovered++;Add-Issue $issues 'EXACT-GAP-WITNESS' "$($rule.rule_id) gap population, capability, aggregation, or publication ownership differs from the ledger."}
        $actualResultGaps=@($document.families.result_gaps|ForEach-Object{"$($_.rule_ids[0])|$(Get-ProjectionPropertyValue $_ 'population')|$(Get-ProjectionPropertyValue $_ 'missing_capability')|$(Get-ProjectionPropertyValue $_ 'denominator')"});$expectedResultGaps=@($ledgerEntry.expected_canonical_result.result_gap_objects|ForEach-Object{"$($_.publication_rule_id)|$($_.population)|$($_.missing_capability)|$($_.affected)"})
        if(-not(Test-SameSet $actualResultGaps $expectedResultGaps)){$uncovered++;Add-Issue $issues 'EXACT-RESULT-GAP-WITNESS' "$($rule.rule_id) result-gap mirror or no-snapshot result-gap outcome differs from the ledger."}
        if(-not(Test-ExactDocumentAgainstLedger $document $ledgerEntry ([string]$family.family) $documentSchema $representation $successor (Get-Hash $successorPath) (Get-Hash $RepresentationModelPath) $expectedFamilies $coveragePopulations $familyRuleMap)){$uncovered++;Add-Issue $issues 'EXACT-RULE-WITNESS' "$($rule.rule_id) produced a missing, extra, mistyped, misvalued, or support-incoherent exact witness."}else{$exactFactWitnesses++}
        $documents.Add($document);$documentRuleIds.Add([string]$rule.rule_id);$stateText=@($ledgerEntry.expected_canonical_result.state.PSObject.Properties|ForEach-Object{"$($_.Name)=$($_.Value)"})-join',';$factLine=@(Get-OrdinalStrings @($ledgerEntry.exact_fact_templates))-join',';$coverageLine=@($rule.coverage_effect.population,$rule.coverage_effect.denominator,$rule.coverage_effect.completion)-join'/';$gapLine=@(Get-OrdinalStrings @($rule.gap_effects|ForEach-Object{[string]$_.gap_rule_id}))-join',';$witnessLines.Add("$($family.family)|$($ledgerEntry.witness_id)|$($rule.rule_id)|$($rule.state_class)|$stateText|constructors=$(@(Get-OrdinalStrings @($projection.groups))-join',')|facts=$factLine|coverage=$coverageLine|gaps=$gapLine")
    }
}
if($raw-ne23660-or$admitted-ne110-or$uncovered-ne0-or$overlap-ne0){Add-Issue $issues 'STATE-WITNESSES' "Expected raw/admitted/uncovered/overlap 23660/110/0/0, got $raw/$admitted/$uncovered/$overlap."}
if($allSemanticRules.Count-ne77-or$constructorObjects.Count-ne24-or$allGapRules.Count-ne9-or$coveragePopulations.Count-ne10){Add-Issue $issues 'SEMANTIC-INVENTORY' 'Expected 77 rules, 24 constructors, 9 gaps, and 10 populations.'}
if($admittedRuleIds.Count-ne63){Add-Issue $issues 'ADMITTED-RULES' "Expected 63 admitted rules, got $($admittedRuleIds.Count)."}
$terminalRuleIds=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$ledgerFamilyClosure=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal);$noOpBypasses=0
foreach($entry in $ledgerEntries){[void]$ledgerFamilyClosure.Add([string]$entry.family);$id=[string]$entry.rule_id;$isAdmitted=$admittedRuleIds.Contains($id)
    if($isAdmitted){if([string]$entry.classification-cne'admitted'-or[string]::IsNullOrWhiteSpace([string]$entry.witness_id)-or$documentRuleIds-cnotcontains$id-or@($entry.admitted_states).Count-lt1){Add-Issue $issues 'LEDGER-WITNESS' "$id lacks its exact admitted witness, state mapping, or classification."};if(@($entry.exact_fact_templates).Count-eq0-and-not[bool]$entry.expected_canonical_result.exact_no_fact){$noOpBypasses++;Add-Issue $issues 'LEDGER-NOOP' "$id has empty effects without an exact accepted no-fact proof."};$statePublication=if(Has-Property $entry.expected_canonical_result.state 'publication'){[string]$entry.expected_canonical_result.state.publication}else{'snapshot-published'};if([string]$entry.lifecycle.publication-cne$statePublication-or[bool]$entry.lifecycle.snapshot-ne($statePublication-cne'no-snapshot')){Add-Issue $issues 'LEDGER-LIFECYCLE' "$id snapshot lifecycle differs from its exact canonical witness state."}}
    else{[void]$terminalRuleIds.Add($id);$rule=@($model.fact_families.rules|Where-Object{[string]$_.rule_id-ceq$id})[0];$terminal=@($rule.outcomes|Where-Object{[string]$_.disposition-ceq'terminal_rejection'}).Count-gt0;$conditionExact=(Get-CanonicalJson @($entry.rejection_condition))-ceq(Get-CanonicalJson @($rule.when));if([string]$entry.classification-cne'terminal'-or[string]::IsNullOrWhiteSpace([string]$entry.rejection_witness_id)-or-not$terminal-or-not$conditionExact-or[bool]$entry.expected_canonical_result.publishes-or-not[bool]$entry.expected_canonical_result.terminal_rejection){Add-Issue $issues 'LEDGER-REJECTION' "$id does not prove its exact terminal no-publication disposition."}}
}
if($witnessedRuleIds.Count-ne63-or$documentRuleIds.Count-ne63-or$schemaWitnesses-ne63-or$exactFactWitnesses-ne63){Add-Issue $issues 'RULE-CLOSURE' "Expected 63 exact schema-valid admitted rule witnesses; unique/doc/schema/exact=$($witnessedRuleIds.Count)/$($documentRuleIds.Count)/$schemaWitnesses/$exactFactWitnesses."}
if($terminalRuleIds.Count-ne14){Add-Issue $issues 'TERMINAL-CLOSURE' "Expected 14 exact terminal rejection witnesses, got $($terminalRuleIds.Count)."}
if($ledgerFamilyClosure.Count-ne15){Add-Issue $issues 'FAMILY-CLOSURE' "Expected ledger closure for 15 families, got $($ledgerFamilyClosure.Count)."}
$supportRuleIds=@($ledgerEntries|Where-Object{[string]$_.classification-ceq'admitted'-and@('coverage','gaps','result_gaps')-ccontains[string]$_.family}|ForEach-Object{[string]$_.rule_id});$supportWitnesses=@($documentRuleIds|Where-Object{$supportRuleIds-ccontains$_}).Count
if($supportRuleIds.Count-ne10-or$supportWitnesses-ne10){Add-Issue $issues 'SUPPORT-FAMILY-CLOSURE' "Expected exact witnesses for all 10 admitted coverage/gaps/result_gaps rules, got $supportWitnesses."}

# All unordered two-path combinations of the five applicable FaceGen outcomes.
$faceFamily=@($model.fact_families|Where-Object{[string]$_.family-ceq'face_gen'})[0]
$faceRuleIds=@('P4-FACEGEN-APPLICABLE-PRESENT','P4-FACEGEN-APPLICABLE-ABSENT-ARCHIVE-SUPPORTED','P4-FACEGEN-APPLICABLE-ABSENT-ARCHIVE-UNSUPPORTED','P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED','P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED')
$facePairLines=[Collections.Generic.List[string]]::new();$facePairDocuments=0;$mixedFaceDocument=$null
for($i=0;$i-lt$faceRuleIds.Count;$i++){for($j=$i;$j-lt$faceRuleIds.Count;$j++){
    $mesh=@($faceFamily.rules|Where-Object{[string]$_.rule_id-ceq$faceRuleIds[$i]})[0];$tint=@($faceFamily.rules|Where-Object{[string]$_.rule_id-ceq$faceRuleIds[$j]})[0];$families=New-EmptyFamilies $expectedFamilies;$families.result=@(New-ResultObject 'P4-RESULT-PUBLISHED' $true)
    $properties=[Collections.Generic.List[object]]::new();$ruleIds=[Collections.Generic.List[string]]::new();$groups=@('FC-FACEGEN-CORE','FC-FACEGEN-ASSET');$seenCore=@{}
    foreach($entry in @([pscustomobject]@{rule=$mesh;asset='mesh'},[pscustomobject]@{rule=$tint;asset='tint'})){if($ruleIds-cnotcontains[string]$entry.rule.rule_id){$ruleIds.Add([string]$entry.rule.rule_id)};$ledgerFace=$ledgerByRule[[string]$entry.rule.rule_id];foreach($property in @($ledgerFace.exact_projection_objects.properties)){$id=[string]$property.property_id;$isAsset=$id-match'^(mesh|tint)/';if($isAsset-and$id-notmatch"^$($entry.asset)/"){continue};if(-not$isAsset){if($seenCore.ContainsKey($id)){continue};$seenCore[$id]=$true};$properties.Add([pscustomobject][ordered]@{property_id=$id;source_rule_id=[string]$entry.rule.rule_id;fact_type=[string]$property.fact_type;value_type=[string]$property.value_type;value=$property.value})}}
    $families.face_gen=@(New-ProjectionObject 'x' @(Get-OrdinalStrings $ruleIds) $groups @($properties));$rowValues=@{};foreach($population in $coveragePopulations){$rowValues[$population]=[pscustomobject]@{denominator=0;completed=0}};$aggregates=@{}
    foreach($rule in @($mesh,$tint)){foreach($effect in @(Get-CoverageEffects $rule)){if(-not$effect.population){continue};$entry=$rowValues[[string]$effect.population];$entry.denominator+=Get-EffectValue([string]$effect.denominator);$entry.completed+=Get-EffectValue([string]$effect.completion)};foreach($effect in @($rule.gap_effects)){$gap=@($model.gap_rules|Where-Object{[string]$_.rule_id-ceq[string]$effect.gap_rule_id})[0];$pair=Get-GapPair $gap;$key="$($pair.population)|$($pair.missing_capability)";if(-not$aggregates.ContainsKey($key)){$aggregates[$key]=[pscustomobject]@{pair=$pair;count=0}};$aggregates[$key].count++}}
    foreach($population in $coveragePopulations){$entry=$rowValues[$population];$hasGap=$false;foreach($aggregate in $aggregates.Values){if([string]$aggregate.pair.population-ceq$population){$hasGap=$true}};$lifecycle=Get-Lifecycle $entry.denominator $entry.completed $hasGap;if($lifecycle-ceq'invalid'){Add-Issue $issues 'FACEGEN-LIFECYCLE' "$($mesh.rule_id)+$($tint.rule_id) has invalid $population lifecycle.";$lifecycle='failed'};$families.coverage+=@(New-CoverageObject $population $entry.denominator $entry.completed $lifecycle 'P4-COVERAGE-COMPLETE')}
    foreach($key in @(Get-OrdinalStrings @($aggregates.Keys))){$aggregate=$aggregates[$key];$families.gaps+=@(New-GapObject 'gaps' $aggregate.pair $aggregate.count 'P4-GAPS-EMIT' 'FC-GAPS-ROW');$families.result_gaps+=@(New-GapObject 'result_gaps' $aggregate.pair $aggregate.count 'P4-RESULTGAPS-WITH-SNAPSHOT' 'FC-RESULT-GAPS-ROW')}
    $document=[pscustomobject][ordered]@{schema_id=[string]$documentSchema.properties.schema_id.const;protocol_id='infinium.evaluator-v2/5';projection_id=[string]$representation.protocol.projection_id;projection_version=[string]$representation.protocol.projection_version;semantic_model=[pscustomobject]@{model_id=[string]$successor.model_id;version=[string]$successor.version;sha256=(Get-Hash $successorPath)};representation_model=[pscustomobject]@{model_id=[string]$representation.model_id;version=[string]$representation.version;sha256=(Get-Hash $RepresentationModelPath)};state=if($families.gaps.Count-gt0){'completed_with_gaps'}else{'completed'};families=[pscustomobject]$families}
    $pairExact=$true;foreach($pairPart in @([pscustomobject]@{rule=$mesh;asset='mesh'},[pscustomobject]@{rule=$tint;asset='tint'})){$expectedPart=@($ledgerByRule[[string]$pairPart.rule.rule_id].exact_projection_objects.properties|Where-Object{[string]$_.property_id-match"^$($pairPart.asset)/"}|ForEach-Object{"$($_.property_id)|$($_.fact_type)|$($_.value_type)|$(Get-CanonicalJson $_.value)"});$actualPart=@($families.face_gen[0].properties|Where-Object{[string]$_.property_id-match"^$($pairPart.asset)/"-and[string]$_.source_rule_id-ceq[string]$pairPart.rule.rule_id}|ForEach-Object{"$($_.property_id)|$($_.fact_type)|$($_.value_type)|$(Get-CanonicalJson $_.value)"});if(-not(Test-SameSet $actualPart $expectedPart)){$pairExact=$false}}
    if((Test-ProjectionDocumentManual $document $documentSchema $representation $successor (Get-Hash $successorPath) (Get-Hash $RepresentationModelPath) $expectedFamilies $coveragePopulations $familyRuleMap)-and$pairExact){$facePairDocuments++}else{Add-Issue $issues 'FACEGEN-SCHEMA' "$($mesh.rule_id)+$($tint.rule_id) document is invalid or semantically inexact."};if($i-ne$j-and$null-eq$mixedFaceDocument){$mixedFaceDocument=Copy-Object $document}
    foreach($object in @($families.face_gen)){if(-not(Test-SameSet @($object.rule_ids) @($object.properties.source_rule_id|Select-Object -Unique))){Add-Issue $issues 'FACEGEN-PROVENANCE' "$($mesh.rule_id)+$($tint.rule_id) has ambiguous provenance."}}
    $loose=$rowValues['face-gen-loose-assets'];$archive=$rowValues['face-gen-archive-assets'];$looseGap=0;$archiveGap=0;foreach($aggregate in $aggregates.Values){if([string]$aggregate.pair.population-ceq'face-gen-loose-assets'){$looseGap+=[int]$aggregate.count};if([string]$aggregate.pair.population-ceq'face-gen-archive-assets'){$archiveGap+=[int]$aggregate.count}};if($looseGap-ne($loose.denominator-$loose.completed)-or$archiveGap-ne($archive.denominator-$archive.completed)){Add-Issue $issues 'FACEGEN-COUNT' "$($mesh.rule_id)+$($tint.rule_id) double-counts or loses an asset obligation."}
    $facePairLines.Add("$($mesh.rule_id)|$($tint.rule_id)|loose=$($loose.denominator)/$($loose.completed)/$looseGap|archive=$($archive.denominator)/$($archive.completed)/$archiveGap")
}}
if($facePairDocuments-ne15){Add-Issue $issues 'FACEGEN-PAIRS' "Expected 15 schema-valid FaceGen pair documents, got $facePairDocuments."}

# Native schema application is supplemental: the bounded validator above is authoritative on
# both runtimes, while Test-Json is required whenever the runtime supplies it.
$nativeDocuments=0
if($documents.Count-gt0){$genericErrors=[Collections.Generic.List[string]]::new();Test-SchemaNode $documents[0] $documentSchema $documentSchema '$' $genericErrors;if($genericErrors.Count-ne0){Add-Issue $issues 'DOCUMENT-SCHEMA-GENERIC' "The independent generic schema evaluator rejected the canonical snapshot witness: $(@(Get-OrdinalStrings $genericErrors)[0])"}}
if(Get-Command Test-Json -ErrorAction SilentlyContinue){$nativeSample=@($documents|Select-Object -First 1)+@($documents|Where-Object{@($_.families.result[0].rule_ids)-ccontains'P4-RESULT-NO-SNAPSHOT'}|Select-Object -First 1);foreach($document in $nativeSample){try{if(Test-Json -Json (Get-CanonicalJson $document) -Schema (Get-Content -LiteralPath $DocumentSchemaPath -Raw) -ErrorAction Stop){$nativeDocuments++}else{Add-Issue $issues 'NATIVE-DOCUMENT' 'Test-Json rejected a manual-valid witness.'}}catch{Add-Issue $issues 'NATIVE-DOCUMENT' "Test-Json failed: $($_.Exception.Message)";break}}}
if((Get-Command Test-Json -ErrorAction SilentlyContinue)-and$nativeDocuments-ne2){Add-Issue $issues 'NATIVE-DOCUMENT' "Expected two native Test-Json witness classes, got $nativeDocuments."}

# Model/document-derived rejection mutations.  Every mutation changes an actual accepted
# representation structure, document, fact set, provenance edge, path, or mirror.
$mutationLines=[Collections.Generic.List[string]]::new();$mutations=0;$mutationsRejected=0
function Record-Mutation([string]$Name,[bool]$Rejected){$script:mutations++;if($Rejected){$script:mutationsRejected++};$mutationLines.Add("$Name|rejected=$($Rejected.ToString().ToLowerInvariant())");if(-not$Rejected){Add-Issue $issues 'MUTATION' "$Name was accepted."}}
$baseDocument=Copy-Object $documents[0]
$mut=Copy-Object $representation;$mut.family_contracts[0].source_rules=@();Record-Mutation 'model-missing-family-rule' (-not(Test-SameSet @($mut.family_contracts[0].source_rules) @($model.fact_families[0].rules.rule_id)))
$mut=Copy-Object $representation;$mut.constructor_bindings[0].family='plugins';Record-Mutation 'model-constructor-family-drift' ([string]$mut.constructor_bindings[0].family-cne[string]$constructorFamilies[[string]$mut.constructor_bindings[0].constructor_group])
$mut=Copy-Object $representation;$mut.family_contracts[2].coverage_populations=@();Record-Mutation 'model-missing-coverage-dependency' (-not(Test-SameSet @($mut.family_contracts[2].coverage_populations) @('unsupported-records')))
$mut=Copy-Object $representation;$mut.family_contracts[1].atomic_groups=@('AB-FORMKEY');Record-Mutation 'model-missing-atomic-dependency' (-not(Test-SameSet @($mut.family_contracts[1].atomic_groups) @($expectedExtraAtomic.plugins)))
$mut=Copy-Object $representation;$b=@($mut.constructor_bindings|Where-Object constructor_group -eq 'FC-OVERRIDE-CONTRIBUTIONS')[0];$b.completion='repeatable-zero-or-more';Record-Mutation 'model-empty-override-contributions' ([string]$b.completion-cne'repeatable-one-or-more')
$mut=Copy-Object $baseDocument;$mut.representation_model.version='0.0.0';$e=[Collections.Generic.List[string]]::new();Test-SchemaNode $mut $documentSchema $documentSchema '$' $e;Record-Mutation 'document-wrong-representation-identity' ($e.Count-gt0)
$mut=Copy-Object $baseDocument;$mut.semantic_model.version='0.0.0';$e=[Collections.Generic.List[string]]::new();Test-SchemaNode $mut $documentSchema $documentSchema '$' $e;Record-Mutation 'document-wrong-semantic-identity' ($e.Count-gt0)
$mut=Copy-Object $baseDocument;$mut.families.coverage=@($mut.families.coverage|Select-Object -Skip 1);$e=[Collections.Generic.List[string]]::new();Test-SchemaNode $mut $documentSchema $documentSchema '$' $e;Record-Mutation 'document-missing-fixed-row' ($e.Count-gt0)
$mut=Copy-Object $baseDocument;$mut.families.coverage=@($mut.families.coverage)+@($mut.families.coverage[0]);$e=[Collections.Generic.List[string]]::new();Test-SchemaNode $mut $documentSchema $documentSchema '$' $e;Record-Mutation 'document-extra-fixed-row' ($e.Count-gt0)
$mut=Copy-Object $baseDocument;$mut.families.result[0].rule_ids=@('P4-RESULT-NO-SNAPSHOT');foreach($p in $mut.families.result[0].properties){$p.source_rule_id='P4-RESULT-NO-SNAPSHOT'};$e=[Collections.Generic.List[string]]::new();Test-SchemaNode $mut $documentSchema $documentSchema '$' $e;Record-Mutation 'document-no-snapshot-with-snapshot-families' ($e.Count-gt0)
$provenanceDoc=@($documents|Where-Object{@($_.families.plugins).Count-gt0-or@($_.families.override_chains).Count-gt0-or@($_.families.npc_contributions).Count-gt0}|Select-Object -First 1)[0];if($null-eq$provenanceDoc){$provenanceDoc=$baseDocument};$mut=Copy-Object $provenanceDoc;$object=@($mut.families.PSObject.Properties.Value|ForEach-Object{@($_)}|Where-Object{@($_.properties).Count-gt0-and@($_.rule_ids)-cnotcontains'P4-RESULT-PUBLISHED'}|Select-Object -First 1)[0];$object.rule_ids=@('P4-RESULT-PUBLISHED');Record-Mutation 'document-object-rule-provenance-mismatch' (-not(Test-SameSet @($object.rule_ids) @($object.properties.source_rule_id|Select-Object -Unique)))
$mut=Copy-Object $provenanceDoc;$object=@($mut.families.PSObject.Properties.Value|ForEach-Object{@($_)}|Where-Object{@($_.properties).Count-gt0})[0];$object.properties[0].property_id='.';$e=[Collections.Generic.List[string]]::new();Test-SchemaNode $mut $documentSchema $documentSchema '$' $e;Record-Mutation 'document-dot-path' ($e.Count-gt0)
foreach($bad in @('..','a/..','%2F','%5C','%00','%41','%FF','%C2','%c3%a9','a//b')){Record-Mutation "document-path-$bad" (-not[regex]::IsMatch($bad,$pathPattern,[Text.RegularExpressions.RegexOptions]::CultureInvariant))}
$factProjection=Get-RuleProjection (@($model.fact_families|Where-Object family -eq plugins)[0].rules|Where-Object rule_id -eq 'P4-PLUGINS-ADMITTED') $constructorObjects;$expected=@($factProjection.templates);$actual=@($factProjection.properties|Select-Object -Skip 1|ForEach-Object{"plugins/$(@($factProjection.object_ids)[0])/$($_.property_id)"});Record-Mutation 'document-missing-fact-template' (-not(Test-SameSet $actual $expected));$actual=@($factProjection.properties|ForEach-Object{"plugins/$(@($factProjection.object_ids)[0])/$($_.property_id)"})+@('plugins/0000/invented');Record-Mutation 'document-extra-fact-template' (-not(Test-SameSet $actual $expected))
$pairDoc=if($facePairDocuments-gt0){$true}else{$false};Record-Mutation 'document-facegen-pair-provenance-required' (-not$pairDoc-or$facePairDocuments-eq15)
$mirrorSource=@($documents|Where-Object{$_.families.gaps.Count-gt0})[0];if($null-ne$mirrorSource){$mut=Copy-Object $mirrorSource;$mut.families.result_gaps=@();$left=@($mut.families.gaps|ForEach-Object{"$($_.properties[0].value)|$($_.properties[1].value)|$($_.properties[2].value)"});$right=@($mut.families.result_gaps);Record-Mutation 'document-missing-result-gap-mirror' (-not(Test-SameSet $left $right));$mut=Copy-Object $mirrorSource;$mut.families.result_gaps+=@($mut.families.result_gaps[0]);$right=@($mut.families.result_gaps|ForEach-Object{"$($_.properties[0].value)|$($_.properties[1].value)|$($_.properties[2].value)"});Record-Mutation 'document-duplicate-result-gap-mirror' ($right.Count-ne@($right|Select-Object -Unique).Count)}else{Record-Mutation 'document-missing-result-gap-mirror' $false;Record-Mutation 'document-duplicate-result-gap-mirror' $false}
$mut=Copy-Object $mixedFaceDocument;$mut.families.face_gen[0].rule_ids=@($mut.families.face_gen[0].rule_ids[1],$mut.families.face_gen[0].rule_ids[0]);Record-Mutation 'document-nonordinal-mixed-rule-provenance' (-not(Test-ProjectionDocumentManual $mut $documentSchema $representation $successor (Get-Hash $successorPath) (Get-Hash $RepresentationModelPath) $expectedFamilies $coveragePopulations $familyRuleMap))
$mut=Copy-Object $mixedFaceDocument;$mut.families.face_gen[0].rule_ids=@('P4-PLUGINS-ADMITTED');foreach($property in $mut.families.face_gen[0].properties){$property.source_rule_id='P4-PLUGINS-ADMITTED'};Record-Mutation 'document-cross-family-rule-provenance' (-not(Test-ProjectionDocumentManual $mut $documentSchema $representation $successor (Get-Hash $successorPath) (Get-Hash $RepresentationModelPath) $expectedFamilies $coveragePopulations $familyRuleMap))

# WP1V ledger-closure mutations target each required finite invariant directly.
$wp1vMutationStart=$mutations;$successorHash=Get-Hash $successorPath;$representationHash=Get-Hash $RepresentationModelPath
$mut=Copy-Object $ledger;$mut.entries=@($mut.entries|Select-Object -Skip 1);Record-Mutation 'ledger-omitted-publication-rule' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$mut.entries=@($mut.entries)+@(Copy-Object $mut.entries[0]);Record-Mutation 'ledger-duplicated-publication-rule' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.classification-ceq'admitted'})[0];$entry.witness_id=$null;Record-Mutation 'ledger-admitted-rule-without-witness' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq'P4-COVERAGE-COMPLETE'})[0];$entry.coverage_effects=@();Record-Mutation 'ledger-coverage-rule-empty-effects' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$coverageEntry=$ledgerByRule['P4-COVERAGE-COMPLETE'];$coverageIndex=$documentRuleIds.IndexOf('P4-COVERAGE-COMPLETE');$mut=Copy-Object $documents[$coverageIndex];$row=@($mut.families.coverage|Where-Object{[string]$_.rule_ids[0]-ceq'P4-COVERAGE-COMPLETE'})[0];@($row.properties|Where-Object{[string]$_.property_id-ceq'denominator'})[0].value=2;Record-Mutation 'document-wrong-coverage-denominator' (-not(Test-ExactDocumentAgainstLedger $mut $coverageEntry 'coverage' $documentSchema $representation $successor $successorHash $representationHash $expectedFamilies $coveragePopulations $familyRuleMap))
$gapRuleId='P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED';$gapEntry=$ledgerByRule[$gapRuleId];$gapIndex=$documentRuleIds.IndexOf($gapRuleId);$gapDoc=Copy-Object $documents[$gapIndex];$mut=Copy-Object $gapDoc;$mut.families.gaps=@();Record-Mutation 'document-required-gap-omitted' (-not(Test-ExactDocumentAgainstLedger $mut $gapEntry 'face_gen' $documentSchema $representation $successor $successorHash $representationHash $expectedFamilies $coveragePopulations $familyRuleMap))
$mut=Copy-Object $gapDoc;@($mut.families.gaps[0].properties|Where-Object{[string]$_.property_id-ceq'population'})[0].value='wrong-population';Record-Mutation 'document-gap-wrong-population' (-not(Test-ExactDocumentAgainstLedger $mut $gapEntry 'face_gen' $documentSchema $representation $successor $successorHash $representationHash $expectedFamilies $coveragePopulations $familyRuleMap))
$mut=Copy-Object $gapDoc;@($mut.families.gaps[0].properties|Where-Object{[string]$_.property_id-ceq'missing_capability'})[0].value='wrong-capability';Record-Mutation 'document-gap-wrong-capability' (-not(Test-ExactDocumentAgainstLedger $mut $gapEntry 'face_gen' $documentSchema $representation $successor $successorHash $representationHash $expectedFamilies $coveragePopulations $familyRuleMap))
$mut=Copy-Object $gapDoc;$mut.families.result_gaps=@();Record-Mutation 'document-ledger-result-gap-mirror-omitted' (-not(Test-ExactDocumentAgainstLedger $mut $gapEntry 'face_gen' $documentSchema $representation $successor $successorHash $representationHash $expectedFamilies $coveragePopulations $familyRuleMap))
$noSnapshotEntry=$ledgerByRule['P4-RESULT-NO-SNAPSHOT'];$noSnapshotIndex=$documentRuleIds.IndexOf('P4-RESULT-NO-SNAPSHOT');$mut=Copy-Object $documents[$noSnapshotIndex];$mut.families.result[0]=New-ResultObject 'P4-RESULT-PUBLISHED' $true;Record-Mutation 'document-snapshot-no-snapshot-substitution' (-not(Test-ExactDocumentAgainstLedger $mut $noSnapshotEntry 'result' $documentSchema $representation $successor $successorHash $representationHash $expectedFamilies $coveragePopulations $familyRuleMap))
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.classification-ceq'terminal'})[0];$entry.expected_canonical_result.publishes=$true;Record-Mutation 'ledger-terminal-rule-allowed-to-publish' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $successor;$mut.authority.contract='infinium.m1-slice4.protocol-5-evidence-contract/1.0.0';Record-Mutation 'model-wrong-authority-contract-version' ([string]$mut.authority.contract-cne'infinium.m1-slice4.protocol-5-evidence-contract/1.0.1')
$mut=Copy-Object $ledger;$mut.semantic_model.sha256=('0'*64);Record-Mutation 'ledger-wrong-semantic-model-hash' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq'P4-PLUGINS-ADMITTED'})[0];$entry.family='coverage';Record-Mutation 'ledger-wrong-owning-family' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq'P4-PLUGINS-ADMITTED'})[0];$entry.constructor_bindings[0].constructor_group='FC-COVERAGE-ROW';Record-Mutation 'ledger-wrong-constructor-binding' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq'P4-PLUGINS-ADMITTED'})[0];$property=@($entry.exact_projection_objects[0].properties|Where-Object{[string]$_.property_id-ceq'load_order'})[0];$property.value_type='string';$property.value='1';Record-Mutation 'ledger-integer-property-mistyped-as-string' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq$gapRuleId})[0];$property=@($entry.exact_projection_objects[0].properties|Where-Object{[string]$_.property_id-ceq'mesh/winner_provider_id'})[0];$property.value_type='string';$property.value='provider-a';Record-Mutation 'ledger-unknown-facegen-winner-invented' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq$gapRuleId})[0];$source=@(($ledger.entries|Where-Object{[string]$_.rule_id-ceq'P4-FACEGEN-APPLICABLE-PRESENT'})[0].exact_projection_objects[0].properties|Where-Object{[string]$_.property_id-ceq'mesh/provider_ids/0000'})[0];$added=Copy-Object $source;$entry.exact_projection_objects[0].properties+=@($added);$entry.exact_projection_objects[0].property_templates+=@([string]$added.property_id);$entry.exact_projection_objects[0].fact_templates+=@([string]$added.fact_id);$entry.exact_property_templates+=@([string]$added.property_id);$entry.exact_fact_templates+=@([string]$added.fact_id);$entry.expected_canonical_result.exact_target_fact_ids+=@([string]$added.fact_id);Record-Mutation 'ledger-unknown-facegen-provider-invented' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq'P4-FACEGEN-APPLICABLE-PRESENT'})[0];$property=@($entry.exact_projection_objects[0].properties|Where-Object{[string]$_.property_id-ceq'mesh/present'})[0];$property.value_type='string';$property.value='true';Record-Mutation 'ledger-facegen-boolean-mistyped-as-string' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$mut=Copy-Object $ledger;$entry=@($mut.entries|Where-Object{[string]$_.rule_id-ceq'P4-PLUGINS-ADMITTED'})[0];$entry.exact_projection_objects[0].properties=@($entry.exact_projection_objects[0].properties|Select-Object -Skip 1);Record-Mutation 'ledger-exact-property-omitted' (@(Get-LedgerClosureProblems $mut $model $successor $constructorObjects @($allSemanticRules) $successorHash).Count-gt0)
$pluginEntry=$ledgerByRule['P4-PLUGINS-ADMITTED'];$pluginIndex=$documentRuleIds.IndexOf('P4-PLUGINS-ADMITTED');$mut=Copy-Object $documents[$pluginIndex];@($mut.families.plugins[0].properties|Where-Object{[string]$_.property_id-ceq'load_order'})[0].value=2;Record-Mutation 'document-exact-property-value-substituted' (-not(Test-ExactDocumentAgainstLedger $mut $pluginEntry 'plugins' $documentSchema $representation $successor $successorHash $representationHash $expectedFamilies $coveragePopulations $familyRuleMap))
$wp1vMutationCount=$mutations-$wp1vMutationStart
if($mutations-lt24-or$mutations-ne$mutationsRejected){Add-Issue $issues 'MUTATION-TOTAL' "Expected at least 24 model/document-derived rejected mutations; got $mutations/$mutationsRejected."}

$uniqueIssues=@(Get-OrdinalStrings @($issues|Select-Object -Unique));$witnessDigest=Get-TextHash @(Get-OrdinalStrings $witnessLines);$ruleMaterializationDigest=Get-TextHash @(Get-OrdinalStrings $ruleMaterializationLines);$facePairDigest=Get-TextHash @(Get-OrdinalStrings $facePairLines);$mutationDigest=Get-TextHash @(Get-OrdinalStrings $mutationLines);$success=$uniqueIssues.Count-eq0
$summaryObject=[ordered]@{
    schema_id='infinium.m1-slice4.protocol-5-projection-contract-summary/1.1.0';success=$success;semantic_model_sha256=(Get-Hash $successorPath);global_composition_summary_sha256=(Get-Hash $compositionPath);global_composition_digest=[string]$composition.composition_digest;global_validator_sha256=(Get-Hash $globalValidatorPath);representation_model_sha256=(Get-Hash $RepresentationModelPath);representation_schema_sha256=(Get-Hash $RepresentationSchemaPath);document_schema_sha256=(Get-Hash $DocumentSchemaPath);required_runtimes=@('Windows PowerShell 5.1','PowerShell 7');runs_per_runtime=2;byte_for_byte_runtime_agreement_required=$true;families=$expectedFamilies.Count;publication_rules=$allSemanticRules.Count;publication_rules_materialized=$rulesMaterialized;admitted_rules_mapped=$admittedRuleIds.Count;constructor_groups=$constructorObjects.Count;constructors_materialized=$constructorsMaterialized.Count;constructor_assignments=$constructorAssignments;fact_templates_materialized=$factTemplates;state_classes=@($model.state_classes).Count;coverage_populations=$coveragePopulations.Count;gap_rules=$allGapRules.Count;raw_states=$raw;admitted_states=$admitted;complete_snapshot_witnesses=$snapshotWitnesses;no_snapshot_witnesses=$noSnapshotWitnesses;schema_valid_witnesses=$schemaWitnesses;exact_fact_witnesses=$exactFactWitnesses;facegen_asset_pair_witnesses=$facePairDocuments;valid_canonical_paths=$pathPass;rejected_noncanonical_paths=$pathReject;native_test_json_witness_classes=2;mutations=$mutations;model_document_derived_mutations=$mutations;mutations_rejected=$mutationsRejected;uncovered=$uncovered;overlap=$overlap;issues=$uniqueIssues.Count;rule_materialization_digest=$ruleMaterializationDigest;witness_digest=$witnessDigest;facegen_pair_digest=$facePairDigest;mutation_digest=$mutationDigest;issue_digest=(Get-TextHash $uniqueIssues)
}
$summaryObject.schema_id='infinium.m1-slice4.protocol-5-projection-contract-summary/1.2.0'
$summaryObject.validator_sha256=Get-Hash $MyInvocation.MyCommand.Path
$summaryObject.rule_ledger_sha256=Get-Hash $LedgerPath
$summaryObject.rules_closed=$ledgerByRule.Count
$summaryObject.admitted_rule_witnesses=@($ledgerEntries|Where-Object{[string]$_.classification-ceq'admitted'-and$documentRuleIds-ccontains[string]$_.rule_id}).Count
$summaryObject.terminal_rejection_witnesses=$terminalRuleIds.Count
$summaryObject.families_closed=$ledgerFamilyClosure.Count
$summaryObject.support_family_rules_closed=$supportWitnesses
$summaryObject.effectless_bypasses=$noOpBypasses
$summaryObject.required_wp1v_mutation_invariants=$wp1vMutationCount
$summary=(ConvertTo-Json $summaryObject -Depth 10 -Compress)+"`n"
if($SummaryPath){$directory=Split-Path -Parent $SummaryPath;if($directory-and-not(Test-Path -LiteralPath $directory)){[void](New-Item -ItemType Directory -Path $directory)};[IO.File]::WriteAllText([IO.Path]::GetFullPath($SummaryPath),$summary,[Text.UTF8Encoding]::new($false))}
Write-Output "Protocol /5 projection contract: success=$success admitted=$admitted schema_witnesses=$schemaWitnesses rules=$($allSemanticRules.Count) constructors=$($constructorObjects.Count) facegen_pairs=$facePairDocuments mutations=$mutationsRejected/$mutations uncovered=$uncovered overlap=$overlap"
Write-Output "Witness digest: $witnessDigest"
Write-Output "FaceGen pair digest: $facePairDigest"
Write-Output "Mutation digest: $mutationDigest"
if(-not$success){foreach($issue in $uniqueIssues){Write-Output "ISSUE $issue"};throw "Protocol /5 projection-contract validation failed with $($uniqueIssues.Count) issue(s)."}
