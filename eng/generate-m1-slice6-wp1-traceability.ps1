[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$schemaNames = @(
    'provider-access-profile.v1.schema.json', 'provider-operation.v1.schema.json',
    'provider-response.v1.schema.json', 'source-claim-extraction.v1.schema.json',
    'candidate-investigation.v1.schema.json', 'provider-execution-input.v1.schema.json',
    'effective-scan-configuration.v2.schema.json', 'run-output.v2.schema.json',
    'cli-summary.v2.schema.json')

$types = @{
    'provider-access-profile.v1.schema.json' = 'ProviderAccessProfileDocument'
    'provider-operation.v1.schema.json' = 'ProviderOperationDocument'
    'provider-response.v1.schema.json' = 'ProviderResponseDocument'
    'source-claim-extraction.v1.schema.json' = 'SourceClaimExtractionDocument'
    'candidate-investigation.v1.schema.json' = 'CandidateInvestigationDocument'
    'provider-execution-input.v1.schema.json' = 'ProviderExecutionInputDocument'
    'effective-scan-configuration.v2.schema.json' = 'EffectiveScanConfigurationV2Document'
    'run-output.v2.schema.json' = 'RunOutputV2Document'
    'cli-summary.v2.schema.json' = 'CliSummaryV2Document'
}
$authorities = @{
    'provider-access-profile.v1.schema.json' = @('SEC-002','SEC-003','OPS-001','ADR-0016','ADR-0020')
    'provider-operation.v1.schema.json' = @('AI-004','OPS-001','OPS-002','ADR-0016','ADR-0020','ADR-0023','ADR-0025')
    'provider-response.v1.schema.json' = @('AI-004','AI-005','OPS-002','ADR-0015','ADR-0023','ADR-0025')
    'source-claim-extraction.v1.schema.json' = @('EVID-001','EVID-004','EVID-007','ADR-0001','ADR-0013')
    'candidate-investigation.v1.schema.json' = @('AI-001','AI-002','AI-003','AI-007','ADR-0001','ADR-0013')
    'provider-execution-input.v1.schema.json' = @('SNAP-005','SNAP-006','ADR-0002','ADR-0020','ADR-0023','ADR-0025')
    'effective-scan-configuration.v2.schema.json' = @('SCAN-003','SCAN-004','ADR-0002','ADR-0025')
    'run-output.v2.schema.json' = @('OPS-002','SNAP-005','ADR-0002','ADR-0015','ADR-0019')
    'cli-summary.v2.schema.json' = @('OPS-002','ADR-0019','ADR-0023')
}

function Get-DeclaredFields([object] $Node, [string] $Prefix, [System.Collections.Generic.List[string]] $Result) {
    $propertiesProperty = $Node.PSObject.Properties['properties']
    if ($null -ne $propertiesProperty) {
        foreach ($property in $propertiesProperty.Value.PSObject.Properties) {
            $path = if ($Prefix) { "$Prefix.$($property.Name)" } else { $property.Name }
            $Result.Add($path)
            Get-DeclaredFields $property.Value $path $Result
        }
    }
    $definitionsProperty = $Node.PSObject.Properties['$defs']
    if ($null -ne $definitionsProperty) {
        foreach ($definition in $definitionsProperty.Value.PSObject.Properties) {
            Get-DeclaredFields $definition.Value "`$defs.$($definition.Name)" $Result
        }
    }
}

function Get-Persistence([string] $Schema, [string] $Path) {
    $leaf = $Path.Split('.')[-1]
    $column = $null
    switch ($Schema) {
        'provider-access-profile.v1.schema.json' {
            $column = switch ($leaf) {
                'profile_id' { 'provider_access_profiles.profile_id' }
                'generation_id' { 'provider_generations.generation_id' }
                'generation_ordinal' { 'provider_generations.generation_ordinal' }
                'revocation_epoch' { 'provider_generations.revocation_epoch' }
                'provider' { 'provider_access_profiles.provider' }
                'purpose' { 'provider_access_profiles.purpose' }
                'display_label' { 'provider_access_profiles.display_label' }
                'account_identity_id' { 'provider_profile_projection.account_identity_id' }
                'billing_scope_identity_id' { 'provider_profile_projection.billing_scope_identity_id' }
                'lifecycle_state' { 'provider_profile_projection.lifecycle_state' }
                'verification_state' { 'provider_profile_projection.verification_state' }
                'capability_snapshot_id' { 'provider_profile_projection.capability_snapshot_id' }
                'intent_id' { 'provider_profile_projection.intent_id' }
                'recovery_disposition' { 'provider_credential_intents.recovery_disposition' }
                'cleanup_disposition' { 'provider_credential_intents.cleanup_disposition' }
                'recorded_at' { 'provider_profile_projection.updated_at' }
                default { $null }
            }
        }
        'provider-operation.v1.schema.json' {
            if ($Path -like '`$defs.capabilitySnapshot.*') {
                $column = if ($leaf -eq 'identity') { 'provider_capability_snapshots.capability_snapshot_id' }
                    elseif ($leaf -eq 'fingerprint') { 'provider_capability_snapshots.fingerprint' }
                    else { "provider_capability_snapshots.$leaf" }
            }
            elseif ($Path -like '`$defs.priceSnapshot.*') {
                $column = if ($leaf -eq 'identity') { 'provider_price_snapshots.price_snapshot_id' }
                    elseif ($leaf -eq 'fingerprint') { 'provider_price_snapshots.fingerprint' }
                    elseif ($leaf -eq 'rules') { $null }
                    else { "provider_price_snapshots.$leaf" }
            }
            elseif ($Path -like '`$defs.priceRule.*') {
                $priceRuleColumns = @('rule_id','context_band','cache_class','token_class','tool_class','region',
                    'numerator_nano_usd','denominator_tokens','revision')
                if ($leaf -in $priceRuleColumns) { $column = "provider_price_rules.$leaf" }
            }
            elseif ($Path -like '`$defs.inputBoundProof.*') { $column = "provider_operation_blocks.input_bound_$($leaf -replace '^policy_','policy_' -replace '^status$','proof_status')" }
            elseif ($Path -like '`$defs.finiteLimits.*') { $column = "provider_operation_blocks.$leaf" }
            else {
                $column = switch ($leaf) {
                    'operation_id' { 'provider_operation_blocks.operation_id' }
                    'owner_id' { 'provider_operation_blocks.owner_id' }
                    'owner_kind' { 'provider_operation_blocks.owner_kind' }
                    'operation_kind' { 'provider_operation_blocks.operation_kind' }
                    'job_node_id' { 'provider_operation_blocks.job_node_id' }
                    'profile_id' { 'provider_operation_blocks.profile_id' }
                    'generation_id' { 'provider_operation_blocks.generation_id' }
                    'revocation_epoch' { 'provider_operation_blocks.revocation_epoch' }
                    'state' { 'provider_operation_blocks.state' }
                    'recorded_at' { 'provider_operation_blocks.recorded_at' }
                    default { $null }
                }
            }
        }
        'provider-response.v1.schema.json' {
            if ($Path -eq 'usage') {
                $column = @(
                    'provider_usage_entries.dispatch_count_availability','provider_usage_entries.dispatch_count',
                    'provider_usage_entries.input_tokens_availability','provider_usage_entries.input_tokens',
                    'provider_usage_entries.output_tokens_availability','provider_usage_entries.output_tokens',
                    'provider_usage_entries.total_tokens_availability','provider_usage_entries.total_tokens',
                    'provider_usage_entries.reasoning_tokens_availability','provider_usage_entries.reasoning_tokens',
                    'provider_usage_entries.cache_read_tokens_availability','provider_usage_entries.cache_read_tokens',
                    'provider_usage_entries.cache_write_tokens_availability','provider_usage_entries.cache_write_tokens',
                    'provider_usage_entries.priced_tool_calls_availability','provider_usage_entries.priced_tool_calls',
                    'provider_usage_entries.calculated_nano_usd_availability','provider_usage_entries.calculated_nano_usd',
                    'provider_usage_entries.billing_availability','provider_usage_entries.rate_availability',
                    'provider_usage_entries.credit_availability')
            }
            elseif ($Path -eq 'rate_limit_facts') {
                $column = @('provider_rate_limit_facts.scope','provider_rate_limit_facts.dimension',
                    'provider_rate_limit_facts.availability','provider_rate_limit_facts.limit_value',
                    'provider_rate_limit_facts.remaining_value','provider_rate_limit_facts.observed_at',
                    'provider_rate_limit_facts.resets_at')
            }
            elseif ($Path -like 'usage.*') {
                $segments = $Path.Split('.')
                $quantity = $segments[1]
                if ($segments.Length -eq 2) {
                    $column = if ($quantity -in @('billing_availability','rate_availability','credit_availability')) {
                        "provider_usage_entries.$quantity"
                    } else { @("provider_usage_entries.${quantity}_availability", "provider_usage_entries.$quantity") }
                }
                elseif ($leaf -eq 'availability') { $column = "provider_usage_entries.${quantity}_availability" }
                elseif ($leaf -eq 'value') { $column = "provider_usage_entries.$quantity" }
            }
            elseif ($Path -like '`$defs.rateLimitFact.*') {
                $column = switch ($leaf) { 'limit' {'provider_rate_limit_facts.limit_value'} 'remaining' {'provider_rate_limit_facts.remaining_value'} default {"provider_rate_limit_facts.$leaf"} }
            }
            else {
                $column = switch ($leaf) {
                    'raw_response_payload' { @('provider_responses.raw_response_payload_id','provider_responses.raw_response_fingerprint') }
                    'response_headers_payload' { @('provider_responses.response_headers_payload_id','provider_responses.response_headers_fingerprint') }
                    'state' { 'provider_responses.response_state' }
                    'recorded_at' { 'provider_responses.created_at' }
                    { $_ -in @('schema_id','schema_version') } { $null }
                    default { "provider_responses.$leaf" }
                }
            }
        }
        'provider-execution-input.v1.schema.json' {
            if ($Path -like 'capability_snapshot.*') { $column = "provider_capability_snapshots.$leaf" }
            elseif ($Path -like 'price_snapshot.*') { $column = "provider_price_snapshots.$leaf" }
            elseif ($Path -like 'limits.*') { $column = "provider_operation_blocks.$leaf" }
            elseif ($Path -like 'input_bound_proof.*') { $column = "provider_operation_blocks.input_bound_$($leaf -replace '^status$','proof_status')" }
            else { $column = switch ($leaf) { 'operation_id' {'provider_operation_blocks.operation_id'} 'owner_id' {'provider_operation_blocks.owner_id'} 'profile_id' {'provider_operation_blocks.profile_id'} 'generation_id' {'provider_operation_blocks.generation_id'} 'operation_kind' {'provider_operation_blocks.operation_kind'} default {$null} } }
        }
        'effective-scan-configuration.v2.schema.json' {
            $column = switch ($leaf) { 'access_profile_id' {'provider_operation_blocks.profile_id'} 'generation_id' {'provider_operation_blocks.generation_id'} 'model' {'provider_capability_snapshots.model'} 'service_tier' {'provider_capability_snapshots.service_tier'} 'reasoning_effort' {'provider_capability_snapshots.reasoning_effort'} 'reasoning_context' {'provider_capability_snapshots.reasoning_context'} 'reasoning_mode' {'provider_capability_snapshots.reasoning_mode'} 'store' {'provider_capability_snapshots.store'} 'background' {'provider_capability_snapshots.background'} 'stream' {'provider_capability_snapshots.stream'} 'tool_choice' {'provider_capability_snapshots.tool_choice'} 'tool_count' {'provider_capability_snapshots.tool_count'} 'truncation' {'provider_capability_snapshots.truncation'} 'prompt_cache_mode' {'provider_capability_snapshots.prompt_cache_mode'} 'has_prompt_cache_key' {'provider_capability_snapshots.has_prompt_cache_key'} 'has_prompt_cache_breakpoint' {'provider_capability_snapshots.has_prompt_cache_breakpoint'} default {$null} }
        }
        'run-output.v2.schema.json' { $column = switch ($leaf) { 'operation_id' {'provider_operation_projection.operation_id'} 'availability' {'provider_operation_projection.state'} 'live' {'provider_operation_projection.unresolved_hold'} default {$null} } }
        'cli-summary.v2.schema.json' { $column = switch ($leaf) { 'provider_state' {'provider_operation_projection.state'} 'unresolved_hold' {'provider_operation_projection.unresolved_hold'} default {$null} } }
        default { $column = $null }
    }
    if ($null -eq $column) { return [ordered]@{ not_persisted_reason = "$Path is retained or derived at the $Schema contract boundary; no semantically identical schema-6 column exists while provider dispatch is authority-blocked." } }
    if ($column -is [array]) { return [ordered]@{ table_columns = $column } }
    return [ordered]@{ table_column = $column }
}

function Get-Projection([string] $Schema, [string] $Path, [bool] $Replay) {
    $leaf = $Path.Split('.')[-1]
    $message = $null; $field = $null
    if (-not $Replay) {
        if ($Schema -eq 'provider-access-profile.v1.schema.json') {
            $profileFields = @('profile_id','generation_id','generation_ordinal','revocation_epoch','lifecycle_state',
                'verification_state','account_identity_id','billing_scope_identity_id','capability_snapshot_id','intent_id',
                'recovery_disposition','cleanup_disposition')
            if ($Path -in $profileFields) { $message='ProviderProfilePayload'; $field=$leaf }
        }
        elseif ($Schema -eq 'provider-operation.v1.schema.json') {
            $map=@{ operation_id='operation_id'; operation_kind='operation_kind'; profile_id='profile_id'; generation_id='generation_id'; revocation_epoch='revocation_epoch'; owner_id='owner_id'; owner_kind='owner_kind'; job_node_id='job_node_id'; state='state'; settlement_state='settlement_state'; replay_state='replay_state' }
            if($map.ContainsKey($Path)){ $message='ProviderOperationPayload'; $field=$map[$Path] }
            elseif ($Path -like '`$defs.inputBoundProof.*') {
                $proofMap=@{ policy_id='input_bound_policy_id'; policy_version='input_bound_policy_version'; status='input_bound_proof_status' }
                if($proofMap.ContainsKey($leaf)){ $message='ProviderOperationPayload'; $field=$proofMap[$leaf] }
            }
        }
        elseif ($Schema -eq 'provider-response.v1.schema.json') {
            if ($Path -like '`$defs.rateLimitFact.*') {
                $message='ProviderRateLimitFact'; $field=$leaf
            }
            elseif ($Path -like 'usage.*') {
                $quantity=$Path.Split('.')[1]
                $message='ProviderResponsePayload'; $field=$quantity
            }
            else {
                $responseMap=@{
                    response_record_id='response_record_id'; request_id='request_id'; dispatch_fence_id='dispatch_fence_id';
                    raw_response_payload='raw_response'; raw_response_bytes='raw_response'; maximum_raw_response_bytes='maximum_raw_response_bytes';
                    response_headers_payload='response_headers'; response_headers_bytes='response_headers'; response_headers_availability='response_headers_availability';
                    http_status='http_status'; provider_response_id='provider_response_id'; provider_request_id='provider_request_id';
                    provider_request_id_availability='provider_request_id_availability'; state='response_state'; refusal_code='refusal_code';
                    incomplete_reason='incomplete_reason'; error_code='error_code'; requested_model='requested_model'; returned_model='returned_model';
                    requested_service_tier='requested_service_tier'; returned_service_tier='returned_service_tier'; reasoning_context='reasoning_context';
                    reasoning_mode='reasoning_mode'; prompt_cache_mode='prompt_cache_mode'; rate_limit_facts='rate_limit_facts';
                    validation_state='validation_state'; admission_state='admission_state'; recorded_at='recorded_at'
                }
                if($responseMap.ContainsKey($Path)){ $message='ProviderResponsePayload'; $field=$responseMap[$Path] }
            }
        }
    }
    else {
        if ($Schema -eq 'provider-operation.v1.schema.json') {
            $map=@{ operation_id='operation_id'; operation_kind='operation_kind'; profile_id='profile_id'; generation_id='generation_id'; revocation_epoch='revocation_epoch'; maximum_request_bytes='limits'; maximum_input_tokens='limits'; maximum_output_tokens='limits'; maximum_raw_response_bytes='limits'; maximum_dispatch_count='limits'; maximum_calculated_nano_usd='limits'; deadline_milliseconds='limits' }
            if($map.ContainsKey($leaf)){ $message='ProviderReplayPayload'; $field=$map[$leaf] }
        }
        elseif ($Schema -eq 'provider-response.v1.schema.json') {
            $map=@{ response_record_id='retained_response_id'; operation_id='operation_id'; request_id='request_id'; dispatch_fence_id='dispatch_fence_id' }
            if($map.ContainsKey($leaf)){ $message='ProviderReplayPayload'; $field=$map[$leaf] }
        }
    }
    if ($null -eq $message) { return [ordered]@{ omission_reason = "$Path has no semantically equivalent $(if($Replay){'network-free replay'}else{'public application output'}) field while WP1 remains Proposed and dispatch-blocked." } }
    return [ordered]@{ file='contracts/protobuf/infinium/application/v1/application.proto'; message=$message; field=$field }
}

$contracts = @()
foreach ($schemaName in $schemaNames) {
    $schema = Get-Content -LiteralPath (Join-Path $root "contracts/json-schema/$schemaName") -Raw | ConvertFrom-Json
    $fields = [System.Collections.Generic.List[string]]::new()
    Get-DeclaredFields $schema '' $fields
    $mappings = foreach ($path in $fields) {
        [ordered]@{
            path = $path
            authorities = $authorities[$schemaName]
            producer = [ordered]@{ file='src/Infinium.Domain/Contracts/ProviderOperationContracts.cs'; symbol=$types[$schemaName] }
            consumer = [ordered]@{ file='src/Infinium.Domain/Contracts/ProviderOperationContractInvariants.cs'; symbol="Validate($($types[$schemaName])" }
            persistence = Get-Persistence $schemaName $path
            output = Get-Projection $schemaName $path $false
            replay = Get-Projection $schemaName $path $true
        }
    }
    $contracts += [ordered]@{ schema=$schemaName; field_mappings=@($mappings) }
}
$result = [ordered]@{
    traceability_schema_version = 2
    maturity = 'Proposed'
    authority_boundary = 'M1/S6/WP1 blocked-only pre-proof contract closure'
    contracts = $contracts
}
$json = ($result | ConvertTo-Json -Depth 32) -replace "`r`n", "`n"
$target = Join-Path $root 'docs/plans/milestones/m1/slices/s6/wp1-contract-traceability.v1.json'
[System.IO.File]::WriteAllText($target, $json + "`n", [System.Text.UTF8Encoding]::new($false))
