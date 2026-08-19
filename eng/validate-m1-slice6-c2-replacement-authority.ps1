[CmdletBinding()]
param(
    [string] $Package = 'docs/plans/milestones/m1/slices/s6/m1-slice6-c2-replacement-authority-package.v2.json',
    [string] $CoordinatorBinary = 'src/Infinium.Coordinator/bin/Release/net10.0/Infinium.Coordinator.exe',
    [switch] $RequireClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Full([string] $Relative) { [IO.Path]::GetFullPath((Join-Path $repo $Relative)) }
function Sha([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Relative([string] $Root, [string] $Path) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($Path)
    Require ($fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) 'Binary inventory path escaped its root.'
    $fullPath.Substring($prefix.Length).Replace('\', '/')
}
function ShaBytes([byte[]] $Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { -join @($algorithm.ComputeHash($Bytes) | ForEach-Object { $_.ToString('x2') }) }
    finally { $algorithm.Dispose() }
}

$packagePath = Full $Package
$coordinator = Full $CoordinatorBinary
$m = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
$schemaPairs = @(
    @($Package, 'contracts/repository/m1-slice6-c2-replacement-authority-package.v2.schema.json'),
    @('docs/plans/milestones/m1/slices/s6/c2-replacement-openai-official-document-snapshot.v2.json', 'contracts/repository/m1-slice6-c2-official-document-snapshot.v2.schema.json'),
    @('docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v4.json', 'contracts/repository/wp9-production-profile-authorization.v4.schema.json'),
    @('docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v4.json', 'contracts/repository/m1-slice6-finite-campaign-authorization.v4.schema.json')
)
foreach ($pair in $schemaPairs) {
    & $coordinator --validate-repository-authority-json --document (Full $pair[0]) --schema (Full $pair[1]) *> $null
    Require ($LASTEXITCODE -eq 0) "Schema validation failed for $($pair[0])."
}

foreach ($artifact in $m.artifacts) {
    $actual = Sha (Full ([string]$artifact.path))
    Require ($actual -ceq [string]$artifact.sha256) "Artifact digest drift: $($artifact.path)."
}
$profile = Get-Content -LiteralPath (Full $m.artifacts[0].path) -Raw | ConvertFrom-Json
$campaign = Get-Content -LiteralPath (Full $m.artifacts[1].path) -Raw | ConvertFrom-Json
$snapshot = Get-Content -LiteralPath (Full $m.artifacts[2].path) -Raw | ConvertFrom-Json
Require ($profile.manifest_id -ceq $m.fresh_identities.credential_authorization) 'Credential identity drift.'
Require ($campaign.campaign_id -ceq $m.fresh_identities.campaign) 'Campaign identity drift.'
Require ($campaign.credential_envelope.source_manifest_sha256 -ceq $m.artifacts[0].sha256) 'Campaign/profile digest binding drift.'
Require ($profile.candidate_binding.close_ready_implementation_commit -ceq $m.implementation.c1_2_commit) 'Profile/C1.2 binding drift.'
Require ($campaign.candidate_binding.close_ready_implementation_commit -ceq $m.implementation.c1_2_commit) 'Campaign/C1.2 binding drift.'
Require ($profile.release_build.coordinator_sha256 -ceq $m.implementation.coordinator_sha256) 'Coordinator binding drift.'
Require ($profile.release_build.helper_sha256 -ceq $m.implementation.helper_sha256) 'Helper binding drift.'
Require ($profile.release_build.binary_inventory_sha256 -ceq $m.implementation.binary_inventory_sha256) 'Inventory binding drift.'
Require ($profile.release_build.source_commit -ceq $m.implementation.c1_2_commit) 'Profile build source commit drift.'
Require ($profile.release_build.build_command -ceq $m.implementation.build_command) 'Profile build command drift.'
Require ($profile.release_build.coordinator_relative_path -ceq $m.implementation.coordinator_path) 'Profile coordinator path drift.'
Require ($profile.release_build.helper_relative_path -ceq $m.implementation.helper_path) 'Profile helper path drift.'
Require ([int]$profile.release_build.binary_inventory_file_count -eq [int]$m.implementation.binary_inventory_file_count) 'Profile inventory count drift.'
Require ($campaign.credential_envelope.source_candidate_commit -ceq $m.implementation.c1_2_commit) 'Campaign credential source commit drift.'
Require ([DateTimeOffset]::Parse($m.prepared_at_utc) -lt [DateTimeOffset]::Parse($m.expires_at_utc)) 'Package time window is invalid.'
Require ([DateTimeOffset]::Parse($m.expires_at_utc) -le [DateTimeOffset]::Parse($profile.expires_at_utc)) 'Package outlives credential authority.'
Require ([DateTimeOffset]::Parse($m.expires_at_utc) -le [DateTimeOffset]::Parse($campaign.expires_at_utc)) 'Package outlives campaign authority.'

$freshIds = @($m.package_id, $m.fresh_identities.campaign, $m.fresh_identities.credential_authorization,
    $m.fresh_identities.profile, $m.fresh_identities.generation,
    $m.fresh_identities.qualification_stage, $m.fresh_identities.source_claim_stage,
    $m.fresh_identities.candidate_stage, $m.fresh_identities.runtime_credential,
    $m.fresh_identities.runtime_wp9, $m.fresh_identities.runtime_wp10,
    $m.fresh_identities.runtime_wp11)
Require (@($freshIds | Sort-Object -Unique).Count -eq $freshIds.Count) 'Fresh identity set contains a duplicate.'
$retiredIds = @($m.fresh_identities.retired_campaign, $m.fresh_identities.retired_credential_authorization,
    $m.fresh_identities.terminal_c2_campaign, $m.fresh_identities.terminal_c2_credential_authorization,
    $m.fresh_identities.terminal_c2_profile, $m.fresh_identities.terminal_c2_generation,
    $m.fresh_identities.terminal_c2_package)
$retiredIds += @($m.fresh_identities.terminal_c2_stage_ids)
$retiredIds += @($m.fresh_identities.terminal_c2_runtime_authority_ids)
Require (@($freshIds | Where-Object { $retiredIds -ccontains $_ }).Count -eq 0) 'Fresh identity reuses a reserved or terminal identity.'
Require ($m.fresh_identities.terminal_c2_owner_decision -ceq 'infinium.m1-s6.c2.owner-acceptance/bbefdd1a-fbf7-4bc4-a7de-877e41756ef9' -and
    $m.fresh_identities.future_owner_decision_identity -ceq 'derive-fresh-only-after-actual-owner-acceptance-never-preassign-or-reuse-terminal-decision') 'Future owner-decision identity rule is not closed.'
Require ($m.terminal_predecessor.helper_launches -eq 1 -and
    -not $m.terminal_predecessor.manual_ui_attempted -and
    $m.terminal_predecessor.native_credential_operations -eq 0 -and
    $m.terminal_predecessor.dns_operations -eq 0 -and
    $m.terminal_predecessor.public_network_operations -eq 0 -and
    $m.terminal_predecessor.provider_requests -eq 0 -and
    $m.terminal_predecessor.billable_operations -eq 0 -and
    -not $m.terminal_predecessor.api_key_observed -and
    -not $m.terminal_predecessor.retry_permitted -and
    $m.terminal_predecessor.successor_stage_authority -ceq 'none') 'Terminal predecessor facts are not closed.'

$priorSnapshot = Get-Content -LiteralPath (Full 'docs/plans/milestones/m1/slices/s6/c2-openai-official-document-snapshot.v1.json') -Raw | ConvertFrom-Json
Require ($snapshot.sources.Count -eq 8 -and $priorSnapshot.sources.Count -eq 8) 'Official source inventory is incomplete.'
for ($index = 0; $index -lt 8; $index++) {
    Require ($snapshot.sources[$index].name -ceq $priorSnapshot.sources[$index].name) "Official source order drift at index $index."
    Require ($snapshot.sources[$index].url -ceq $priorSnapshot.sources[$index].url) "Official source URL drift at index $index."
    Require ($snapshot.sources[$index].bytes -eq $priorSnapshot.sources[$index].bytes) "Official source byte drift at index $index."
    Require ($snapshot.sources[$index].sha256 -ceq $priorSnapshot.sources[$index].sha256) "Official source content drift at index $index."
}
Require ($m.owner_inputs[1].value -ceq 'owner-confirms-intended-platform-account-at-entry' -and
    $m.owner_inputs[2].value -ceq 'owner-confirms-direct-usage-billing-at-entry') 'Owner account or billing intent was broadened or concretized.'

& git -C $repo merge-base --is-ancestor $m.implementation.c1_2_commit HEAD
Require ($LASTEXITCODE -eq 0) 'HEAD does not descend from the bound C1.2 implementation.'
if ($RequireClean) {
    Require ([string]::IsNullOrWhiteSpace((& git -C $repo status --porcelain))) 'Final replacement-package validation requires a clean tracked and untracked worktree.'
}
Require ((Sha (Full $m.implementation.coordinator_path)) -ceq $m.implementation.coordinator_sha256) 'Coordinator bytes do not match.'
Require ((Sha (Full $m.implementation.helper_path)) -ceq $m.implementation.helper_sha256) 'Helper bytes do not match.'
$binaryRoot = Split-Path -Parent (Full $m.implementation.coordinator_path)
$files = Get-ChildItem -LiteralPath $binaryRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.exe', '.dll') -or $_.Name.EndsWith('.deps.json', [StringComparison]::Ordinal) -or
    $_.Name.EndsWith('.runtimeconfig.json', [StringComparison]::Ordinal)
} | Sort-Object { Relative $binaryRoot $_.FullName }
$lines = @($files | ForEach-Object {
    $relative = Relative $binaryRoot $_.FullName
    "$relative|$(Sha $_.FullName)"
})
$canonical = ($lines -join "`n") + "`n"
$inventory = ShaBytes ([Text.Encoding]::UTF8.GetBytes($canonical))
Require ($files.Count -eq [int]$m.implementation.binary_inventory_file_count) 'Binary inventory count drift.'
Require ($inventory -ceq $m.implementation.binary_inventory_sha256) 'Binary inventory digest drift.'

$expectedPrices = @(135680000, 583680000, 583680000)
for ($index = 0; $index -lt 3; $index++) {
    $stage = $m.stage_authority[$index]
    $campaignStage = $campaign.ordered_stages[$index]
    $priceName = @('wp9','wp10','wp11')[$index]
    Require ([int64]$stage.limits.maximum_nano_usd -eq [int64]$campaignStage.maximum_nano_usd) "Stage $($index + 1) cost ceiling drift."
    Require ([int64]$m.pricing_arithmetic.$priceName.reserved_nano_usd -eq $expectedPrices[$index]) "Stage $($index + 1) arithmetic drift."
    foreach ($mapping in @(
        @('request_bytes','maximum_request_bytes'), @('input_tokens','maximum_input_tokens'),
        @('output_tokens','maximum_output_tokens'), @('raw_response_bytes','maximum_raw_response_bytes'),
        @('deadline_milliseconds','deadline_milliseconds'), @('provider_calls','maximum_provider_calls')
    )) {
        Require ([int64]$stage.limits.($mapping[0]) -eq [int64]$campaignStage.($mapping[1])) "Stage $($index + 1) $($mapping[0]) drift."
    }
}
Require ([int64]$m.pricing_arithmetic.aggregate.reserved_nano_usd -eq 1303040000) 'Aggregate price arithmetic drift.'
Require ([int64]$m.pricing_arithmetic.aggregate.ceiling_nano_usd -eq 1340000000) 'Aggregate cost ceiling broadened.'

$serialized = $m | ConvertTo-Json -Depth 100 -Compress
Require ($serialized.IndexOf('reserved-or-terminal-history-only-never-runtime-authority', [StringComparison]::Ordinal) -ge 0) 'Retired identity reservation is missing.'
foreach ($required in @('fallback','parallel-dispatch','automatic-retry','counter-reset','ceiling-transfer','inherited-authority','fourth-provider-call','git-head-or-marker-runtime-authority')) {
    Require ($m.prohibitions -ccontains $required) "Required prohibition missing: $required."
}
foreach ($property in $m.materialization_state.PSObject.Properties) {
    if ($property.Value -is [bool]) { Require (-not $property.Value) "Materialized state is not inert: $($property.Name)." }
    if ($property.Value -is [long] -or $property.Value -is [int]) { Require ([int64]$property.Value -eq 0) "Effect count is nonzero: $($property.Name)." }
}
foreach ($path in @(
    'docs/plans/milestones/m1/slices/s6/live',
    'artifacts/m1-slice6/c2-replacement-authority',
    'artifacts/m1-slice6/wp9-profile',
    'artifacts/m1-slice6/wp9-production-profile-state',
    'artifacts/m1-slice6/wp9-live',
    'artifacts/m1-slice6/wp10-live',
    'artifacts/m1-slice6/wp11-live'
)) { Require (-not (Test-Path -LiteralPath (Full $path))) "Forbidden effect material exists: $path." }

[ordered]@{
    schema = 'infinium.repository.m1-slice6-c2-replacement-authority-validation/v2'
    disposition = 'valid-inert-owner-review-candidate'
    package_sha256 = Sha $packagePath
    c1_2_commit = $m.implementation.c1_2_commit
    profile_sha256 = $m.artifacts[0].sha256
    campaign_sha256 = $m.artifacts[1].sha256
    official_snapshot_sha256 = $m.artifacts[2].sha256
    provider_requests = 0
    billable_operations = 0
} | ConvertTo-Json -Compress
