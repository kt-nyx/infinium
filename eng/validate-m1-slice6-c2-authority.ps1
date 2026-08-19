[CmdletBinding()]
param(
    [string] $Package = 'docs/plans/milestones/m1/slices/s6/m1-slice6-c2-authority-package.v1.json',
    [string] $CoordinatorBinary = 'src/Infinium.Coordinator/bin/Release/net10.0/Infinium.Coordinator.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Full([string] $Relative) { [IO.Path]::GetFullPath((Join-Path $repo $Relative)) }
function Sha([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }

$packagePath = Full $Package
$coordinator = Full $CoordinatorBinary
$m = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
$schemaPairs = @(
    @($Package, 'contracts/repository/m1-slice6-c2-authority-package.v1.schema.json'),
    @('docs/plans/milestones/m1/slices/s6/c2-openai-official-document-snapshot.v1.json', 'contracts/repository/m1-slice6-c2-official-document-snapshot.v1.schema.json'),
    @('docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v3.json', 'contracts/repository/wp9-production-profile-authorization.v3.schema.json'),
    @('docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v3.json', 'contracts/repository/m1-slice6-finite-campaign-authorization.v3.schema.json')
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
Require ($profile.manifest_id -ceq $m.fresh_identities.credential_authorization) 'Credential identity drift.'
Require ($campaign.campaign_id -ceq $m.fresh_identities.campaign) 'Campaign identity drift.'
Require ($campaign.credential_envelope.source_manifest_sha256 -ceq $m.artifacts[0].sha256) 'Campaign/profile digest binding drift.'
Require ($profile.candidate_binding.close_ready_implementation_commit -ceq $m.implementation.c1_1_commit) 'Profile/C1.1 binding drift.'
Require ($campaign.candidate_binding.close_ready_implementation_commit -ceq $m.implementation.c1_1_commit) 'Campaign/C1.1 binding drift.'
Require ($profile.release_build.coordinator_sha256 -ceq $m.implementation.coordinator_sha256) 'Coordinator binding drift.'
Require ($profile.release_build.helper_sha256 -ceq $m.implementation.helper_sha256) 'Helper binding drift.'
Require ($profile.release_build.binary_inventory_sha256 -ceq $m.implementation.binary_inventory_sha256) 'Inventory binding drift.'

& git -C $repo merge-base --is-ancestor $m.implementation.c1_1_commit HEAD
Require ($LASTEXITCODE -eq 0) 'HEAD does not descend from the bound C1.1 implementation.'
Require ((Sha (Full $m.implementation.coordinator_path)) -ceq $m.implementation.coordinator_sha256) 'Coordinator bytes do not match.'
Require ((Sha (Full $m.implementation.helper_path)) -ceq $m.implementation.helper_sha256) 'Helper bytes do not match.'
$binaryRoot = Split-Path -Parent (Full $m.implementation.coordinator_path)
$files = Get-ChildItem -LiteralPath $binaryRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.exe', '.dll') -or $_.Name.EndsWith('.deps.json', [StringComparison]::Ordinal) -or
    $_.Name.EndsWith('.runtimeconfig.json', [StringComparison]::Ordinal)
} | Sort-Object { [IO.Path]::GetRelativePath($binaryRoot, $_.FullName).Replace('\', '/') }
$lines = @($files | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($binaryRoot, $_.FullName).Replace('\', '/')
    "$relative|$(Sha $_.FullName)"
})
$canonical = ($lines -join "`n") + "`n"
$inventory = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
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
Require ($serialized.Contains('reserved-history-only-never-runtime-authority', [StringComparison]::Ordinal)) 'Retired identity reservation is missing.'
foreach ($required in @('fallback','parallel-dispatch','automatic-retry','counter-reset','ceiling-transfer','inherited-authority','fourth-provider-call','git-head-or-marker-runtime-authority')) {
    Require ($m.prohibitions -ccontains $required) "Required prohibition missing: $required."
}
foreach ($property in $m.materialization_state.PSObject.Properties) {
    if ($property.Value -is [bool]) { Require (-not $property.Value) "Materialized state is not inert: $($property.Name)." }
    if ($property.Value -is [long] -or $property.Value -is [int]) { Require ([int64]$property.Value -eq 0) "Effect count is nonzero: $($property.Name)." }
}
foreach ($path in @(
    'docs/plans/milestones/m1/slices/s6/live',
    'artifacts/m1-slice6/c2-authority',
    'artifacts/m1-slice6/wp9-profile',
    'artifacts/m1-slice6/wp9-production-profile-state',
    'artifacts/m1-slice6/wp9-live',
    'artifacts/m1-slice6/wp10-live',
    'artifacts/m1-slice6/wp11-live'
)) { Require (-not (Test-Path -LiteralPath (Full $path))) "Forbidden effect material exists: $path." }

[ordered]@{
    schema = 'infinium.repository.m1-slice6-c2-authority-validation/v1'
    disposition = 'valid-inert-owner-review-candidate'
    package_sha256 = Sha $packagePath
    c1_1_commit = $m.implementation.c1_1_commit
    profile_sha256 = $m.artifacts[0].sha256
    campaign_sha256 = $m.artifacts[1].sha256
    official_snapshot_sha256 = $m.artifacts[2].sha256
    provider_requests = 0
    billable_operations = 0
} | ConvertTo-Json -Compress
