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
$baseAuthorities = @{
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

function Get-Authorities([string] $Schema, [string] $Path) {
    $leaf = $Path.Split('.')[-1]
    switch ($Schema) {
        'provider-access-profile.v1.schema.json' {
            if ($leaf -in @('profile_id','generation_id','revocation_epoch','lifecycle_state','verification_state','intent_id','recovery_disposition','cleanup_disposition')) { return @('SEC-002','SEC-003','ADR-0016','ADR-0020') }
            return @('OPS-001','ADR-0016','ADR-0020')
        }
        'provider-operation.v1.schema.json' {
            if ($leaf -in @('command_id','requested_at','confirmed_at','owner_id','owner_kind','operation_kind','job_node_id')) { return @('OPS-001','ADR-0023','ADR-0025') }
            if ($Path -like '*capabilitySnapshot*') { return @('AI-004','OPS-001','ADR-0020','ADR-0023') }
            if ($Path -like '*price*' -or $Path -like '*calculated_nano_usd*') { return @('AI-004','AI-005','ADR-0020','ADR-0023') }
            if ($Path -like '*inputBoundProof*' -or $Path -like '*canonical_request*' -or $leaf -in @('dispatch_deadline','coordinator_fencing_epoch')) { return @('AI-004','ADR-0023','ADR-0025') }
            return @('OPS-001','OPS-002','ADR-0016','ADR-0023')
        }
        'provider-response.v1.schema.json' {
            if ($Path -like '*rateLimitFact*' -or $Path -like 'usage*' -or $leaf -in @('usage','rate_limit_facts')) { return @('AI-004','AI-005','ADR-0023','ADR-0025') }
            if ($leaf -in @('raw_response_payload','raw_response_bytes','response_headers_payload','response_headers_bytes','provider_request_id','provider_request_id_availability')) { return @('OPS-002','ADR-0015','ADR-0023','ADR-0025') }
            return @('OPS-002','ADR-0023','ADR-0025')
        }
        'source-claim-extraction.v1.schema.json' {
            if ($leaf -in @('acquisition_run_id','owner_kind','owner_id','parent_analysis_run_id','application_scope_id','cost_attribution_scope_id')) { return @('EVID-001','EVID-004','EVID-007','ADR-0013','ADR-0023') }
            return @('EVID-001','EVID-004','EVID-007','ADR-0001','ADR-0013')
        }
        'candidate-investigation.v1.schema.json' {
            if ($leaf -in @('owner_kind','owner_id','analysis_run_id')) { return @('AI-001','AI-003','ADR-0013','ADR-0023') }
            return @('AI-001','AI-002','AI-003','AI-007','ADR-0001','ADR-0013')
        }
        'run-output.v2.schema.json' {
            if ($leaf -in @('operation_id','operation_kind','acquisition_run_id','availability','live')) { return @('OPS-002','ADR-0019','ADR-0023','ADR-0025') }
            return @('OPS-002','SNAP-005','ADR-0002','ADR-0019')
        }
        'cli-summary.v2.schema.json' { return @('OPS-002','ADR-0019','ADR-0023') }
        default { return $baseAuthorities[$Schema] }
    }
}

function ConvertTo-Pascal([string] $Leaf) {
    return (($Leaf -split '_' | ForEach-Object { if ($_.Length -gt 0) { $_.Substring(0,1).ToUpperInvariant() + $_.Substring(1) } }) -join '')
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
                'command_id' { 'provider_operation_blocks.command_id' }
                'installation_snapshot_id' { 'provider_operation_blocks.installation_snapshot_id' }
                'analysis_context_id' { 'provider_operation_blocks.analysis_context_id' }
                'effective_configuration_id' { 'provider_operation_blocks.effective_configuration_id' }
                'resolved_input_manifest_id' { 'provider_operation_blocks.resolved_input_manifest_id' }
                'prompt_id' { 'provider_operation_blocks.prompt_id' }
                'prompt_fingerprint' { 'provider_operation_blocks.prompt_fingerprint' }
                'output_schema_id' { 'provider_operation_blocks.output_schema_id' }
                'output_schema_fingerprint' { 'provider_operation_blocks.output_schema_fingerprint' }
                'request_fingerprint' { 'provider_operation_blocks.request_fingerprint' }
                'canonical_request_payload' { @('provider_operation_blocks.canonical_request_payload_id','provider_operation_blocks.canonical_request_fingerprint') }
                'canonical_request_bytes' { 'provider_operation_blocks.canonical_request_bytes' }
                'settings_fingerprint' { 'provider_operation_blocks.settings_fingerprint' }
                'requested_at' { 'provider_operation_blocks.requested_at' }
                'confirmed_at' { 'provider_operation_blocks.confirmed_at' }
                'dispatch_deadline' { 'provider_operation_blocks.dispatch_deadline_utc' }
                'coordinator_fencing_epoch' { 'provider_operation_blocks.coordinator_fencing_epoch' }
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
            if ($Path -eq 'input_bound_proof') {
                $column = @('provider_requests.input_bound_policy_id','provider_requests.input_bound_policy_version','provider_requests.input_bound_proof_status')
            }
            elseif ($Path -like '`$defs.inputBoundProof.*') {
                $column = switch ($leaf) {
                    'policy_id' { 'provider_requests.input_bound_policy_id' }
                    'policy_version' { 'provider_requests.input_bound_policy_version' }
                    'status' { 'provider_requests.input_bound_proof_status' }
                }
            }
            elseif ($Path -eq 'usage') {
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
                    'billing_evidence_payload' { @('provider_responses.billing_evidence_payload_id','provider_responses.billing_evidence_fingerprint') }
                    'state' { 'provider_responses.response_state' }
                    'recorded_at' { 'provider_responses.created_at' }
                    { $_ -in @('schema_id','schema_version','availability') } { $null }
                    default { "provider_responses.$leaf" }
                }
            }
        }
        'source-claim-extraction.v1.schema.json' {
            $column = switch ($leaf) {
                'acquisition_run_id' { 'evidence_acquisition_runs.acquisition_run_id' }
                'owner_id' { 'evidence_acquisition_runs.acquisition_run_id' }
                'operation_id' { 'provider_semantic_proposals.operation_id' }
                'parent_analysis_run_id' { 'evidence_acquisition_runs.parent_analysis_run_id' }
                'application_scope_id' { 'evidence_acquisition_runs.application_scope_id' }
                'cost_attribution_scope_id' { 'evidence_acquisition_runs.cost_attribution_scope_id' }
                default { $null }
            }
        }
        'candidate-investigation.v1.schema.json' {
            $column = switch ($leaf) {
                'operation_id' { 'provider_semantic_proposals.operation_id' }
                'owner_id' { 'provider_operation_blocks.owner_id' }
                'analysis_run_id' { 'provider_operation_blocks.owner_id' }
                'candidate_id' { 'analysis_candidates.candidate_id' }
                default { $null }
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
        'run-output.v2.schema.json' { $column = switch ($leaf) { 'operation_id' {'provider_operation_projection.operation_id'} 'availability' {'provider_operation_projection.state'} default {$null} } }
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
            elseif ($Path -eq 'input_bound_proof') {
                return [ordered]@{ file='contracts/protobuf/infinium/application/v1/application.proto'; message='ProviderOperationPayload'; fields=@('input_bound_proof_status','input_bound_policy_id','input_bound_policy_version') }
            }
            elseif ($Path -eq 'usage') {
                return [ordered]@{ file='contracts/protobuf/infinium/application/v1/application.proto'; message='ProviderOperationPayload'; fields=@('dispatch_count','input_tokens','output_tokens','total_tokens','reasoning_tokens','cache_read_tokens','cache_write_tokens','calculated_nano_usd') }
            }
            elseif ($Path -like '`$defs.inputBoundProof.*') {
                $proofMap=@{ policy_id='input_bound_policy_id'; policy_version='input_bound_policy_version'; status='input_bound_proof_status' }
                if($proofMap.ContainsKey($leaf)){ $message='ProviderOperationPayload'; $field=$proofMap[$leaf] }
            }
        }
        elseif ($Schema -eq 'provider-response.v1.schema.json') {
            if ($Path -eq 'input_bound_proof') {
                return [ordered]@{ file='contracts/protobuf/infinium/application/v1/application.proto'; message='ProviderResponsePayload'; fields=@('input_bound_proof_status','input_bound_policy_id','input_bound_policy_version') }
            }
            elseif ($Path -eq 'usage') {
                return [ordered]@{ file='contracts/protobuf/infinium/application/v1/application.proto'; message='ProviderResponsePayload'; fields=@('dispatch_count','input_tokens','output_tokens','total_tokens','reasoning_tokens','cache_read_tokens','cache_write_tokens','priced_tool_calls','calculated_nano_usd','billing_availability','rate_availability','credit_availability') }
            }
            elseif ($Path -like '`$defs.inputBoundProof.*') {
                $proofMap=@{ policy_id='input_bound_policy_id'; policy_version='input_bound_policy_version'; status='input_bound_proof_status' }
                if($proofMap.ContainsKey($leaf)){ $message='ProviderResponsePayload'; $field=$proofMap[$leaf] }
            }
            elseif ($Path -like '`$defs.rateLimitFact.*') {
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
                    validation_state='validation_state'; admission_state='admission_state'; recorded_at='recorded_at';
                    availability='availability'; client_request_id='client_request_id'; billing_evidence_payload='billing_evidence'
                }
                if($responseMap.ContainsKey($Path)){ $message='ProviderResponsePayload'; $field=$responseMap[$Path] }
            }
        }
        elseif ($Schema -eq 'source-claim-extraction.v1.schema.json') {
            $map=@{ acquisition_run_id='acquisition_run_id'; operation_id='operation_id'; owner_kind='owner_kind'; owner_id='owner_id'; parent_analysis_run_id='parent_analysis_run_id'; application_scope_id='application_scope_id'; cost_attribution_scope_id='cost_attribution_scope_id' }
            if($map.ContainsKey($Path)){ $message='SourceClaimExtractionPayload'; $field=$map[$Path] }
        }
        elseif ($Schema -eq 'candidate-investigation.v1.schema.json') {
            $map=@{ operation_id='operation_id'; owner_kind='owner_kind'; owner_id='owner_id'; analysis_run_id='analysis_run_id'; candidate_id='candidate_id' }
            if($map.ContainsKey($Path)){ $message='CandidateInvestigationPayload'; $field=$map[$Path] }
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
            authorities = Get-Authorities $schemaName $path
            producer = [ordered]@{ file='src/Infinium.Domain/Contracts/ProviderOperationContracts.cs'; symbol=(ConvertTo-Pascal $path.Split('.')[-1]) }
            consumer = [ordered]@{ file='src/Infinium.Domain/Contracts/ProviderOperationContractInvariants.cs'; symbol=$(if($path -in @('schema_id','schema_version')){'RequireHeader'}else{ConvertTo-Pascal $path.Split('.')[-1]}) }
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
