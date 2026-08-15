[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [switch] $RequireReady,
    [switch] $MutationTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-ExactProperties($Object, [string[]] $Expected, [string] $Name) {
    $actual = @($Object.PSObject.Properties.Name)
    if (($actual -join "`n") -cne ($Expected -join "`n")) {
        throw "$Name does not have the exact ordered closed property set."
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = (Resolve-Path -LiteralPath $AuthorizationManifest).Path
$expectedManifestPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
if ($MutationTest -and $env:INFINIUM_WP9_VALIDATOR_MUTATION_TEST -cne '1') {
    throw 'WP9 validator mutation mode is available only to the explicit non-live contract harness.'
}
if (-not $MutationTest -and $manifestPath -cne $expectedManifestPath) {
    throw 'WP9 production-profile validation accepts only the exact repository manifest path.'
}
$schemaPath = Join-Path $repoRoot 'contracts/repository/wp9-production-profile-authorization.v1.schema.json'
$manifestText = [IO.File]::ReadAllText($manifestPath, [Text.UTF8Encoding]::new($false))
if (-not ($manifestText | Test-Json -SchemaFile $schemaPath -ErrorAction Stop)) {
    throw 'WP9 production-profile manifest violates its closed repository schema.'
}
$m = $manifestText | ConvertFrom-Json -Depth 100 -DateKind String

Assert-ExactProperties $m @('schema_identity','manifest_id','packet_kind','status','effect_authority','prepared_at_utc','expires_at_utc','candidate_binding','predecessor_binding','owner_authorization','provider_intent','official_document_refresh','profile','native_boundary','m1_entry_surface','future_product_ux','durable_state','output','stop_conditions','execution') 'manifest root'
Assert-ExactProperties $m.profile @('mode','access_profile_id','generation_id','generation_ordinal','revocation_epoch','display_label','credential_target','target_derivation','target_fingerprint_sha256','target_encoding','preflight_requirement') 'profile'
Assert-ExactProperties $m.native_boundary @('exact_call_order','exact_results','maximum_calls','enumeration','fallback','overwrite','delete') 'native boundary'
Assert-ExactProperties $m.native_boundary.maximum_calls @('CredWriteW','CredReadW','CredDeleteW','CredFree','total') 'native call maxima'
Assert-ExactProperties $m.durable_state @('product_state_root_relative','initial_state','success_state','verification_state','required_intent_sequence','active_unverified_request_gate','cancel_state','unavailable_or_ambiguous_state','retention') 'durable state'

$expectedTarget = "Infinium:$($m.profile.access_profile_id):$($m.profile.generation_id)"
$targetHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($expectedTarget))).ToLowerInvariant()
if ($m.profile.mode -cne 'new-only' -or $m.profile.credential_target -cne $expectedTarget -or
    $m.profile.target_fingerprint_sha256 -cne $targetHash -or $m.profile.generation_ordinal -ne 1 -or
    $m.profile.revocation_epoch -ne 0) {
    throw 'WP9 production profile target, identity, generation, or fingerprint is not exact.'
}
if ((@($m.native_boundary.exact_call_order) -join '|') -cne 'CredReadW|CredWriteW|CredReadW|CredFree' -or
    (@($m.native_boundary.exact_results) -join '|') -cne 'ERROR_NOT_FOUND|success|success|success' -or
    $m.native_boundary.maximum_calls.CredWriteW -ne 1 -or $m.native_boundary.maximum_calls.CredReadW -ne 2 -or
    $m.native_boundary.maximum_calls.CredDeleteW -ne 0 -or $m.native_boundary.maximum_calls.CredFree -ne 1 -or
    $m.native_boundary.maximum_calls.total -ne 4) {
    throw 'WP9 production profile native grammar or finite call bounds changed.'
}
if ((@($m.durable_state.required_intent_sequence) -join '|') -cne
        'enroll:pending-enrollment->active-unverified|verify:active-unverified->active-verified' -or
    $m.durable_state.success_state -cne 'active-verified' -or
    $m.durable_state.verification_state -cne 'available' -or
    $m.durable_state.active_unverified_request_gate -cne 'reject') {
    throw 'WP9 enrollment must durably finish at the exact verified generation.'
}
if (-not $m.m1_entry_surface.masked -or -not $m.m1_entry_surface.paste_permitted -or
    $m.m1_entry_surface.renderer_receives_or_retains_secret -or
    $m.m1_entry_surface.purpose -cne 'm1-native-boundary-production-enrollment-only-not-final-product-settings-ui' -or
    $m.future_product_ux.flow -cne 'Settings -> Add/Replace API key -> WPF-parented helper-owned masked modal' -or
    $m.future_product_ux.implemented_by_wp9) {
    throw 'WP9 M1 entry surface or future M2 Settings UX boundary changed.'
}
if ($m.provider_intent.provider_request_permitted -or
    $m.official_document_refresh.drift_follow_up.profile_packet_blocked -or
    -not $m.official_document_refresh.drift_follow_up.provider_request_packet_blocked -or
    $m.official_document_refresh.drift_follow_up.credential_profile_change -cne 'none-closed-provider-profile-unchanged') {
    throw 'Official-document drift disposition must preserve profile preparation and block request-packet materialization.'
}

$expectedDocuments = @(
    'model|3707|124cce0f52e97d87bca8d5c383dc9912bdfbcd8b5c3b54a7f209dc8383f9a4ad',
    'latest-model|18668|7591e641abc3cb124b2173843a03d40ea05ee421c8a036f04dda44c79188953e',
    'prompt-caching|27997|2402d5a0bc2643daa28100121fa0397f1893d3e30552e9d0317ebf18288e8348',
    'reasoning|45218|237067018b227133a45f5465b545fd06596631c6a96bd6adec5835450354d7b1',
    'structured-outputs|86127|e894b773b2aa124f07baf3d3e232abf4cd8bed2e3d80f789078f98fed06b55db',
    'safety-best-practices|7626|109a4729274e9a27435f8f1f0dc9f70fdd0f83eec7766c49ea661af94879f403'
)
$actualDocuments = @($m.official_document_refresh.documents | ForEach-Object {
    Assert-ExactProperties $_ @('name','url','final_url','retrieved_at_utc','bytes','sha256','content_type','etag','last_modified') "official document $($_.name)"
    if ($_.url -cne $_.final_url -or $_.content_type -cne 'text/markdown; charset=utf-8') {
        throw "Official document $($_.name) final URL or content type changed."
    }
    "$($_.name)|$($_.bytes)|$($_.sha256)"
})
if (($actualDocuments -join "`n") -cne ($expectedDocuments -join "`n")) {
    throw 'WP9 official-document identity set differs from the fresh reviewed snapshots.'
}

$prepared = [DateTimeOffset]::ParseExact($m.prepared_at_utc, 'yyyy-MM-ddTHH:mm:ss.fffffffZ',
    [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal)
$expires = [DateTimeOffset]::ParseExact($m.expires_at_utc, 'yyyy-MM-ddTHH:mm:ss.fffffffZ',
    [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal)
if ($expires -le $prepared -or ($expires - $prepared) -gt [TimeSpan]::FromHours(48)) {
    throw 'WP9 production-profile authority window is invalid or exceeds 48 hours.'
}
$pending = $m.status -ceq 'draft-close-ready-binding-pending'
if ($pending -ne ($m.candidate_binding.close_ready_implementation_commit -ceq ('0' * 40))) {
    throw 'WP9 manifest status and close-ready candidate binding disagree.'
}
if ($RequireReady -and $pending) {
    throw 'WP9 production-profile manifest is still draft binding-pending.'
}

[ordered]@{
    schema = 'infinium.m1-s6.wp9.profile-authorization-validation/v1'
    status = if ($pending) { 'validated-draft-binding-pending' } else { 'validated-ready-for-owner-acceptance' }
    manifest_id = [string]$m.manifest_id
    manifest_sha256 = Get-Sha256 $manifestPath
    close_ready_implementation_commit = [string]$m.candidate_binding.close_ready_implementation_commit
    target_fingerprint_sha256 = [string]$m.profile.target_fingerprint_sha256
    native_call_maximum = 4
    native_effect_executed = $false
    credential_manager_operation_count = 0
    network_operation_count = 0
    provider_operation_count = 0
} | ConvertTo-Json -Depth 10
