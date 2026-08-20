[CmdletBinding()]
param(
    [string] $Package = 'docs/plans/milestones/m1/slices/s6/m1-slice6-c2a-post-success-recovery-authority-package.v1.json',
    [string] $CoordinatorBinary = 'src/Infinium.Coordinator/bin/Release/net10.0/Infinium.Coordinator.exe',
    [string] $ActivatedExecutionCommit,
    [string] $ExpectedRuntimeAuthoritySha256,
    [switch] $RequireClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Full([string] $Relative) { [IO.Path]::GetFullPath((Join-Path $repo $Relative)) }
function Sha([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Require([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Exact($Value, [string[]] $Names, [string] $Label) {
    $actual = @($Value.PSObject.Properties.Name)
    Require ($actual.Count -eq $Names.Count) "$Label property count drift."
    for ($index = 0; $index -lt $Names.Count; $index++) {
        Require ($actual[$index] -ceq $Names[$index]) "$Label property order/name drift at $index."
    }
}
function Relative([string] $Root, [string] $Path) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath($Path)
    Require ($full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) 'Inventory path escaped its root.'
    $full.Substring($prefix.Length).Replace('\', '/')
}
function ShaBytes([byte[]] $Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { -join @($algorithm.ComputeHash($Bytes) | ForEach-Object { $_.ToString('x2') }) }
    finally { $algorithm.Dispose() }
}

$packagePath = Full $Package
$coordinator = Full $CoordinatorBinary
$m = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
Exact $m @('schema_identity','package_id','status','effect_authority','prepared_at_utc','expires_at_utc','accepted_replacement_authority','terminal_c2a','corrected_implementation','proposed_process_amendment','recovery_runtime_authority_derivation','execution','subsequent_gate','prohibitions','materialization_state','independent_review','acceptance') 'package'
Exact $m.accepted_replacement_authority @('package_commit','package_sha256','activation_commit','profile_path','profile_id','profile_sha256','campaign_path','campaign_id','campaign_sha256','credential_runtime_authority_id','credential_runtime_authority_sha256') 'accepted replacement'
Exact $m.terminal_c2a @('execution_repository','ledger_path','ledger_sha256','terminal_event_hash','terminal_event','failure_path','failure_id','failure_sha256','success_path','success_id','success_sha256','product_state_root','product_state_file_count','product_state_identity_sha256','native_history','helper_launches','ui_submissions','dns_operations','public_network_operations','provider_requests','billable_operations','retry_permitted','credential_retention') 'terminal C2A'
Exact $m.corrected_implementation @('commit','build_command','readiness_receipt_path','readiness_receipt_sha256','coordinator_path','coordinator_sha256','helper_path','helper_sha256','runner_path','runner_sha256','helper_source','binary_inventory_file_count','binary_inventory_sha256','scenario_contract','recovery_transition','external_effects') 'corrected implementation'
Exact $m.proposed_process_amendment @('path','sha256','status','proposal','guardrail') 'process amendment'
Exact $m.recovery_runtime_authority_derivation @('future_path','authority_id','schema_identity','scope','kind','status_after_acceptance','subject_manifest','campaign','predecessor','candidate_binding','review','owner_decision','not_before','expires_at_or_before_utc','execution','limits') 'runtime derivation'
Exact $m.execution @('working_repository','working_directory','tracked_activation','tracked_status_required','binary_staging','pre_invocation_validation','command','allowed_mutation','forbidden_mutations') 'execution'
Exact $m.subsequent_gate @('c2b_status','open_condition','before_c2b_materialization','provider_effect_proceeds_automatically') 'subsequent gate'
Exact $m.materialization_state @('retained_success_evidence','retained_failure_evidence','terminal_ledger','durable_product_profile_projection','recovery_runtime_authority','recovery_transition_applied','c2b_stage_request','provider_requests','billable_operations') 'materialization state'
Exact $m.independent_review @('verdict','scope','external_effects_observed') 'independent review'
Exact $m.acceptance @('required','scope','does_not_execute','does_not_accept_itself','does_not_open_c2b','automatic_handoff') 'acceptance'

& $coordinator --validate-repository-authority-json --document $packagePath `
    --schema (Full 'contracts/repository/m1-slice6-c2a-post-success-recovery-authority-package.v1.schema.json') *> $null
Require ($LASTEXITCODE -eq 0) 'Recovery package schema validation failed.'

Require ($m.status -ceq 'ready-for-owner-review-not-accepted' -and $m.effect_authority -ceq 'none') 'Package is not inert.'
$prepared = [DateTimeOffset]::ParseExact($m.prepared_at_utc, 'O', [Globalization.CultureInfo]::InvariantCulture)
$expires = [DateTimeOffset]::ParseExact($m.expires_at_utc, 'O', [Globalization.CultureInfo]::InvariantCulture)
Require ($prepared.Offset -eq [TimeSpan]::Zero -and $expires.Offset -eq [TimeSpan]::Zero -and $prepared -lt $expires) 'Package window is invalid.'

foreach ($binding in @(
    @($m.accepted_replacement_authority.profile_path, $m.accepted_replacement_authority.profile_sha256),
    @($m.accepted_replacement_authority.campaign_path, $m.accepted_replacement_authority.campaign_sha256),
    @($m.corrected_implementation.readiness_receipt_path, $m.corrected_implementation.readiness_receipt_sha256),
    @($m.proposed_process_amendment.path, $m.proposed_process_amendment.sha256)
)) { Require ((Sha (Full $binding[0])) -ceq $binding[1]) "Tracked binding drift: $($binding[0])." }

$profile = Get-Content -Raw -LiteralPath (Full $m.accepted_replacement_authority.profile_path) | ConvertFrom-Json
$campaign = Get-Content -Raw -LiteralPath (Full $m.accepted_replacement_authority.campaign_path) | ConvertFrom-Json
Require ($profile.manifest_id -ceq $m.accepted_replacement_authority.profile_id) 'Profile identity drift.'
Require ($campaign.campaign_id -ceq $m.accepted_replacement_authority.campaign_id) 'Campaign identity drift.'
Require ($campaign.credential_envelope.source_manifest_sha256 -ceq $m.accepted_replacement_authority.profile_sha256) 'Campaign/profile binding drift.'
Require ($expires -le [DateTimeOffset]::Parse($profile.expires_at_utc) -and $expires -le [DateTimeOffset]::Parse($campaign.expires_at_utc)) 'Recovery package outlives accepted authority.'

& git -C $repo merge-base --is-ancestor $m.corrected_implementation.commit HEAD
Require ($LASTEXITCODE -eq 0) 'HEAD does not descend from the corrected implementation.'
if ($RequireClean) {
    Require ([string]::IsNullOrWhiteSpace((& git -C $repo status --porcelain))) 'Final recovery validation requires a clean worktree.'
}
Require ((Sha (Full $m.corrected_implementation.coordinator_path)) -ceq $m.corrected_implementation.coordinator_sha256) 'Coordinator digest drift.'
Require ((Sha (Full $m.corrected_implementation.helper_path)) -ceq $m.corrected_implementation.helper_sha256) 'Accepted helper digest drift.'
Require ((Sha (Full $m.corrected_implementation.runner_path)) -ceq $m.corrected_implementation.runner_sha256) 'Recovery runner digest drift.'
$binaryRoot = Split-Path -Parent (Full $m.corrected_implementation.coordinator_path)
$files = Get-ChildItem -LiteralPath $binaryRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.exe','.dll') -or $_.Name.EndsWith('.deps.json',[StringComparison]::Ordinal) -or
    $_.Name.EndsWith('.runtimeconfig.json',[StringComparison]::Ordinal)
} | Sort-Object { Relative $binaryRoot $_.FullName }
$lines = @($files | ForEach-Object { "$(Relative $binaryRoot $_.FullName)|$(Sha $_.FullName)" })
$inventory = ShaBytes ([Text.Encoding]::UTF8.GetBytes((($lines -join "`n") + "`n")))
Require ($files.Count -eq [int]$m.corrected_implementation.binary_inventory_file_count) 'Binary count drift.'
Require ($inventory -ceq $m.corrected_implementation.binary_inventory_sha256) 'Binary inventory drift.'

$executionRepo = [IO.Path]::GetFullPath(([string]$m.terminal_c2a.execution_repository).Replace('/', [IO.Path]::DirectorySeparatorChar))
Require (Test-Path -LiteralPath (Join-Path $executionRepo '.git') -PathType Container) 'Execution repository is not a full clone.'
$executionHead = (& git -C $executionRepo rev-parse HEAD).Trim()
Require ([string]::IsNullOrWhiteSpace((& git -C $executionRepo status --porcelain --untracked-files=no))) 'Execution repository has tracked drift.'
if ([string]::IsNullOrWhiteSpace($ActivatedExecutionCommit)) {
    Require ([string]::IsNullOrWhiteSpace($ExpectedRuntimeAuthoritySha256)) 'Owner-review mode cannot accept a runtime-authority digest.'
    Require ($executionHead -ceq $m.accepted_replacement_authority.activation_commit) 'Retained execution repository HEAD drift.'
} else {
    Require ($ExpectedRuntimeAuthoritySha256 -cmatch '^[0-9a-f]{64}$') 'Activated mode requires the exact lowercase runtime-authority SHA-256.'
    Require ($ActivatedExecutionCommit -match '^[0-9a-f]{40}$' -and $executionHead -ceq $ActivatedExecutionCommit) 'Activated execution commit drift.'
    & git -C $executionRepo merge-base --is-ancestor $m.corrected_implementation.commit $ActivatedExecutionCommit
    Require ($LASTEXITCODE -eq 0) 'Activated execution commit does not descend from the corrected implementation.'
    Require ((Sha (Join-Path $executionRepo ([string]$m.corrected_implementation.runner_path).Replace('/', '\'))) -ceq $m.corrected_implementation.runner_sha256) 'Activated recovery runner digest drift.'
    Require ((Sha (Join-Path $executionRepo $Package.Replace('/', '\'))) -ceq (Sha $packagePath)) 'Activated recovery package bytes drift.'
    foreach ($tracked in @(
        @($m.accepted_replacement_authority.profile_path, $m.accepted_replacement_authority.profile_sha256),
        @($m.accepted_replacement_authority.campaign_path, $m.accepted_replacement_authority.campaign_sha256)
    )) { Require ((Sha (Join-Path $executionRepo ([string]$tracked[0]).Replace('/', '\'))) -ceq $tracked[1]) "Activated tracked input drift: $($tracked[0])." }
}
foreach ($binding in @(
    @($m.terminal_c2a.success_path, $m.terminal_c2a.success_sha256),
    @($m.terminal_c2a.failure_path, $m.terminal_c2a.failure_sha256),
    @($m.terminal_c2a.ledger_path, $m.terminal_c2a.ledger_sha256),
    @('artifacts/m1-slice6/c2-replacement-authority/runtime/credential.v1.json', $m.accepted_replacement_authority.credential_runtime_authority_sha256)
)) { Require ((Sha (Join-Path $executionRepo ([string]$binding[0]).Replace('/', '\'))) -ceq $binding[1]) "Retained execution binding drift: $($binding[0])." }

$ledgerLines = @(Get-Content -LiteralPath (Join-Path $executionRepo ([string]$m.terminal_c2a.ledger_path).Replace('/', '\')) | Where-Object { $_.Length -gt 0 })
$terminal = $ledgerLines[-1] | ConvertFrom-Json
Require ($terminal.event_hash -ceq $m.terminal_c2a.terminal_event_hash -and $terminal.event -ceq $m.terminal_c2a.terminal_event) 'Terminal ledger predecessor drift.'
Require ($terminal.evidence_id -ceq $m.terminal_c2a.failure_id -and $terminal.evidence_sha256 -ceq $m.terminal_c2a.failure_sha256) 'Terminal failure binding drift.'
$success = Get-Content -Raw -LiteralPath (Join-Path $executionRepo ([string]$m.terminal_c2a.success_path).Replace('/', '\')) | ConvertFrom-Json
Require ($success.status -ceq 'passed-active-verified' -and $success.native_credential_operation_count -eq 4 -and
    $success.network_operation_count -eq 0 -and $success.listener_count -eq 0 -and
    $success.provider_operation_count -eq 0 -and $success.billable_operation_count -eq 0 -and
    -not $success.retry_attempted -and $success.containment.process_tree_survivor_count -eq 0) 'Retained success semantics drift.'

$stateRoot = Join-Path $executionRepo ([string]$m.terminal_c2a.product_state_root).Replace('/', '\')
$stateFiles = Get-ChildItem -LiteralPath $stateRoot -Recurse -File | Sort-Object { Relative $stateRoot $_.FullName }
$stateHash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
try {
    foreach ($file in $stateFiles) {
        $relative = Relative $stateRoot $file.FullName
        $stateHash.AppendData([Text.Encoding]::UTF8.GetBytes($relative + "`0" + $file.Length + "`0"))
        $stateHash.AppendData([IO.File]::ReadAllBytes($file.FullName))
    }
    $stateIdentity = -join @($stateHash.GetHashAndReset() | ForEach-Object { $_.ToString('x2') })
} finally { $stateHash.Dispose() }
Require ($stateFiles.Count -eq [int]$m.terminal_c2a.product_state_file_count -and $stateIdentity -ceq $m.terminal_c2a.product_state_identity_sha256) 'Durable product-state identity drift.'

$derivation = $m.recovery_runtime_authority_derivation
Require ($derivation.scope -ceq 'effect-free-rehearsal' -and $derivation.kind -ceq 'credential-evidence-recovery') 'Recovery runtime type drift.'
Require ($derivation.predecessor.ledger_event_hash -ceq $m.terminal_c2a.terminal_event_hash -and
    $derivation.predecessor.evidence_sha256 -ceq $m.terminal_c2a.failure_sha256 -and
    $derivation.review.evidence_sha256 -ceq $m.terminal_c2a.success_sha256) 'Recovery derivation evidence drift.'
Require ($derivation.candidate_binding.implementation_commit -ceq $m.corrected_implementation.commit -and
    $derivation.candidate_binding.coordinator_sha256 -ceq $m.corrected_implementation.coordinator_sha256 -and
    $derivation.candidate_binding.helper_sha256 -ceq $m.corrected_implementation.helper_sha256) 'Recovery executable derivation drift.'
foreach ($property in $derivation.limits.PSObject.Properties) {
    if ($property.Value -is [bool]) { Require (-not $property.Value) "Recovery boolean limit broadened: $($property.Name)." }
    else { Require ([int64]$property.Value -eq 0) "Recovery numeric limit broadened: $($property.Name)." }
}
$runtimePath = Join-Path $executionRepo ([string]$derivation.future_path).Replace('/', '\')
if ([string]::IsNullOrWhiteSpace($ActivatedExecutionCommit)) {
    Require (-not (Test-Path -LiteralPath $runtimePath)) 'Recovery runtime authority is prematurely materialized.'
} else {
    Require (Test-Path -LiteralPath $runtimePath -PathType Leaf) 'Activated recovery runtime authority is absent.'
    Require ((Sha $runtimePath) -ceq $ExpectedRuntimeAuthoritySha256) 'Activated recovery runtime-authority digest drift.'
    & $coordinator --validate-repository-authority-json --document $runtimePath `
        --schema (Full 'contracts/json-schema/provider-effect-runtime-authority.v1.schema.json') *> $null
    Require ($LASTEXITCODE -eq 0) 'Activated recovery runtime-authority schema validation failed.'
    $runtime = Get-Content -Raw -LiteralPath $runtimePath | ConvertFrom-Json
    Require ($runtime.schema_identity -ceq $derivation.schema_identity -and
        $runtime.authority_id -ceq $derivation.authority_id -and
        $runtime.scope -ceq $derivation.scope -and
        $runtime.kind -ceq $derivation.kind -and
        $runtime.status -ceq $derivation.status_after_acceptance) 'Activated recovery runtime identity/type/status drift.'
    Require ($runtime.subject_manifest.id -ceq $derivation.subject_manifest.id -and
        $runtime.subject_manifest.sha256 -ceq $derivation.subject_manifest.sha256 -and
        $runtime.campaign.id -ceq $derivation.campaign.id -and
        $runtime.campaign.sha256 -ceq $derivation.campaign.sha256) 'Activated recovery subject/campaign drift.'
    Require ($runtime.predecessor.ledger_event_hash -ceq $derivation.predecessor.ledger_event_hash -and
        $runtime.predecessor.evidence_id -ceq $derivation.predecessor.evidence_id -and
        $runtime.predecessor.evidence_sha256 -ceq $derivation.predecessor.evidence_sha256 -and
        $runtime.review.evidence_id -ceq $derivation.review.evidence_id -and
        $runtime.review.evidence_sha256 -ceq $derivation.review.evidence_sha256) 'Activated recovery predecessor/review drift.'
    Require ($runtime.candidate_binding.implementation_commit -ceq $derivation.candidate_binding.implementation_commit -and
        $runtime.candidate_binding.coordinator_sha256 -ceq $derivation.candidate_binding.coordinator_sha256 -and
        $runtime.candidate_binding.helper_sha256 -ceq $derivation.candidate_binding.helper_sha256) 'Activated recovery executable binding drift.'
    foreach ($name in @('output_root_relative','ledger_path_relative','product_state_root_relative','coordinator_path_relative','helper_path_relative')) {
        Require ($runtime.execution.$name -ceq $derivation.execution.$name) "Activated recovery execution path drift: $name."
    }
    foreach ($property in $derivation.limits.PSObject.Properties) {
        Require ($runtime.limits.($property.Name) -ceq $property.Value) "Activated recovery limit drift: $($property.Name)."
    }
    $runtimeNotBefore = [DateTimeOffset]::ParseExact($runtime.not_before_utc, 'O', [Globalization.CultureInfo]::InvariantCulture)
    $runtimeExpires = [DateTimeOffset]::ParseExact($runtime.expires_at_utc, 'O', [Globalization.CultureInfo]::InvariantCulture)
    Require ($runtimeNotBefore.Offset -eq [TimeSpan]::Zero -and $runtimeExpires.Offset -eq [TimeSpan]::Zero -and
        $runtimeNotBefore -le [DateTimeOffset]::UtcNow -and $runtimeExpires -gt [DateTimeOffset]::UtcNow -and
        $runtimeExpires -le $expires) 'Activated recovery runtime interval is invalid or expired.'
}
foreach ($path in @('docs/plans/milestones/m1/slices/s6/live/wp9-qualification.v4.json','artifacts/m1-slice6/wp9-live')) {
    Require (-not (Test-Path -LiteralPath (Join-Path $executionRepo $path.Replace('/', '\')))) "C2B material is premature: $path."
}
Require ($m.materialization_state.provider_requests -eq 0 -and $m.materialization_state.billable_operations -eq 0 -and
    -not $m.materialization_state.recovery_runtime_authority -and -not $m.materialization_state.recovery_transition_applied -and
    -not $m.materialization_state.c2b_stage_request -and -not $m.subsequent_gate.provider_effect_proceeds_automatically) 'Materialization state is not inert.'
foreach ($required in @('helper-launch','credential-manager-read-write-delete-enumeration','credential-retry','dns','public-network','provider-request','billable-operation','counter-reset','inherited-authority','c2b-before-recovery-acceptance','push')) {
    Require ($m.prohibitions -ccontains $required) "Required prohibition missing: $required."
}

[ordered]@{
    schema = 'infinium.repository.m1-slice6-c2a-post-success-recovery-authority-validation/v1'
    disposition = if ([string]::IsNullOrWhiteSpace($ActivatedExecutionCommit)) { 'valid-inert-owner-review-candidate' } else { 'valid-activated-zero-effect-pre-invocation' }
    package_sha256 = Sha $packagePath
    corrected_implementation_commit = $m.corrected_implementation.commit
    readiness_receipt_sha256 = $m.corrected_implementation.readiness_receipt_sha256
    coordinator_sha256 = $m.corrected_implementation.coordinator_sha256
    helper_sha256 = $m.corrected_implementation.helper_sha256
    terminal_event_hash = $m.terminal_c2a.terminal_event_hash
    success_evidence_sha256 = $m.terminal_c2a.success_sha256
    provider_requests = 0
    billable_operations = 0
} | ConvertTo-Json -Compress
