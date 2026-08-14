[CmdletBinding()]
param(
    [string] $ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json',
    [string] $OutputPath
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh.exe -ErrorAction Stop
    $forward = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-ManifestPath', $ManifestPath)
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) { $forward += @('-OutputPath', $OutputPath) }
    & $pwsh.Source @forward
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedManifest = if ([IO.Path]::IsPathRooted($ManifestPath)) {
    [IO.Path]::GetFullPath($ManifestPath)
} else { [IO.Path]::GetFullPath((Join-Path $repoRoot $ManifestPath)) }
$expectedManifest = [IO.Path]::GetFullPath((Join-Path $repoRoot `
    'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json'))
if (-not [string]::Equals($resolvedManifest, $expectedManifest, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'WP4 v2 validator accepts only the exact tracked close-ready manifest path.'
}
$schema = Join-Path $repoRoot 'contracts/repository/wp4-credential-native-authorization.v2.schema.json'
if (-not (Test-Json -LiteralPath $resolvedManifest -SchemaFile $schema -ErrorAction Stop)) {
    throw 'WP4 v2 manifest failed structural JSON Schema validation.'
}

$bytes = [IO.File]::ReadAllBytes($resolvedManifest)
$text = [Text.Encoding]::UTF8.GetString($bytes)
$manifest = $text | ConvertFrom-Json -Depth 100 -DateKind String
$expectedManifestId = 'infinium.m1-s6.wp4.credential-native-authorization/076b981a-9d32-4e6a-af35-1e7017e0f833'
$expectedWp3 = 'b32939e8b7491a5c47453f912d25dd98c090f103'
$expectedWp7Product = '59367a7479a7395b173b974bf720543aab2404d4'
$expectedWp7Evidence = '51251c0e0eb98d67dbc9b295b9ff084ebca33890'
$expectedHandoff = '44fbcc0542bef77f93c83f1422406a2b6012f0d5'
$expectedCorrection = '2f95692687b60d97db2710835e9d0966f131c164'
$expectedAmbiguityCorrection = '2dce8acc27eece01b0232dd531a2deb27ef752af'
$expectedFramingCorrection = '3456fe02594fd365b1d2627dd08fad44fe0aee92'
$expectedOldManifest = '910ff1552d178bcfe5ff36fd9b618d187203c38c6b023d9610af5c702bdb3393'
$expectedOldEvidence = '0a10a873b7356612cd8ac25934c8fbf85ab0cae76f7aea42b2317421dd251674'
$expectedOldLock = '18ffe3e24687543c7c0d538ec98874245ef3fe0c3d2c26945d375b5e23604d02'
$historicalManifestBlob = (& git -C $repoRoot rev-parse `
    '8f49943d0af53c495b8f288048cbd8d8bd1fe775:docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json').Trim()
if ($LASTEXITCODE -ne 0 -or $historicalManifestBlob -ne '9e2126c2d5e97f12a174dad154f5aa2a1a806e62') {
    throw 'The consumed 4936dcef manifest history differs from its terminal exact-byte authority.'
}
function Assert-ExactArtifactHash([string] $RelativePath, [string] $ExpectedSha256) {
    $path = [IO.Path]::GetFullPath((Join-Path $repoRoot $RelativePath))
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required immutable predecessor artifact is absent: $RelativePath"
    }
    $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [IO.File]::ReadAllBytes($path))).ToLowerInvariant()
    if ($actual -ne $ExpectedSha256) {
        throw "Required immutable predecessor artifact hash differs: $RelativePath"
    }
    return $path
}
$priorEvidencePath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-4936dcef/credential-native-cleanup-ambiguity.v3.json' $expectedOldEvidence
$priorLockPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-authority-locks/16d19410cd200caee29da362c474805929cc4c65651685173d39838849e27421.json' $expectedOldLock
$recoveryEvidencePath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-dd412ecc/credential-native-recovery-evidence.v1.json' `
    '427d78e467fa0f26517d35abcb2c4405bbaf4db5a5845f278d9b584effdc271a'
$recoveryLockPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-locks/4dac12f3ba2f0c264d08b1a9f004374a8c12d255949d6b31566eda1e266429ad.json' `
    '5f9420335ce08c482bf747cf43ac409bb3e13204a6910370c480f9caae00720e'
$recoveryReceiptPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-dd412ecc/credentialnativerecovery.json' `
    'eb4ec7b518329081830bceb3e3b4f3894dee74ed7d334eacb532ce72009dc429'
$priorEvidence = Get-Content -LiteralPath $priorEvidencePath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$priorLock = Get-Content -LiteralPath $priorLockPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryEvidence = Get-Content -LiteralPath $recoveryEvidencePath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryLock = Get-Content -LiteralPath $recoveryLockPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryReceipt = Get-Content -LiteralPath $recoveryReceiptPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
if ($priorEvidence.status -ne 'failed-cleanup-ambiguous' -or
    $priorEvidence.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-authorization/4936dcef-a0f4-4302-9899-0afd99b19799' -or
    $priorEvidence.manifest_sha256 -ne $expectedOldManifest -or
    -not [bool]$priorEvidence.namespace_blocked -or
    [int64]$priorEvidence.later_native_calls -ne 0 -or
    [bool]$priorEvidence.cleanup_confirmed -or
    [bool]$priorEvidence.whole_namespace_absence_confirmed -or
    $priorLock.disposition -ne 'consumed-before-native-launch-never-delete-or-reuse' -or
    $priorLock.manifest_id -ne $priorEvidence.manifest_id -or
    $priorLock.manifest_sha256 -ne $expectedOldManifest) {
    throw 'The consumed 4936dcef ambiguity evidence or authority lock is not terminal and exact.'
}
if ($recoveryEvidence.status -ne 'passed' -or
    $recoveryEvidence.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-recovery/dd412ecc-3b2c-4628-8865-bc8574a357c7' -or
    [bool]$recoveryEvidence.cleanup_ambiguity -or
    -not [bool]$recoveryEvidence.namespace_reuse_blocked -or
    @($recoveryEvidence.target_absence).Count -ne 1 -or
    [int64]$recoveryEvidence.prior_exact_absence_count -ne 11 -or
    [int64]$recoveryEvidence.combined_namespace_target_absence_count -ne 12 -or
    $recoveryLock.disposition -ne 'consumed-never-reuse' -or
    $recoveryLock.manifest_id -ne $recoveryEvidence.manifest_id -or
    $recoveryReceipt.status -ne 'passed' -or
    [int64]$recoveryReceipt.evidence.combined_namespace_target_absence_count -ne 12) {
    throw 'The 4936dcef cleanup recovery evidence, lock, or receipt is not terminal and exact.'
}
if ($manifest.schema_identity -ne 'infinium.repository.wp4-credential-native-authorization/1.5.0' -or
    $manifest.manifest_id -ne $expectedManifestId -or
    $manifest.effect_authority -ne 'none-until-owner-accepts-exact-manifest-bytes' -or
    $manifest.candidate_binding.accepted_wp3_candidate_commit -ne $expectedWp3 -or
    $manifest.candidate_binding.accepted_wp7_product_candidate_commit -ne $expectedWp7Product -or
    $manifest.candidate_binding.accepted_wp7_evidence_commit -ne $expectedWp7Evidence -or
    $manifest.candidate_binding.authorization_handoff_commit -ne $expectedHandoff -or
    $manifest.candidate_binding.sqlite_correction_candidate_commit -ne $expectedCorrection -or
    $manifest.candidate_binding.ambiguity_evidence_correction_candidate_commit -ne $expectedAmbiguityCorrection -or
    $manifest.candidate_binding.native_failure_evidence_and_containment_correction_candidate_commit -ne $expectedFramingCorrection) {
    throw 'WP4 v2 manifest is not bound to the exact accepted WP3/WP7/handoff identities.'
}
if ($manifest.supersedes.manifest_sha256 -ne $expectedOldManifest -or
    $manifest.supersedes.native_evidence_sha256 -ne $expectedOldEvidence -or
    $manifest.supersedes.authority_lock_sha256 -ne $expectedOldLock -or
    $manifest.supersedes.namespace_disposition -ne 'terminal-cleanup-confirmed-absent-never-reuse' -or
    $manifest.supersedes.cleanup_recovery.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-recovery/dd412ecc-3b2c-4628-8865-bc8574a357c7' -or
    $manifest.supersedes.cleanup_recovery.manifest_sha256 -ne '09b6858eaf472038499f18654d2a2fc4ca0a32b2ed34cd1a192146f90755e183' -or
    $manifest.supersedes.cleanup_recovery.evidence_sha256 -ne '427d78e467fa0f26517d35abcb2c4405bbaf4db5a5845f278d9b584effdc271a' -or
    $manifest.supersedes.cleanup_recovery.authority_lock_sha256 -ne '5f9420335ce08c482bf747cf43ac409bb3e13204a6910370c480f9caae00720e' -or
    $manifest.supersedes.cleanup_recovery.receipt_sha256 -ne 'eb4ec7b518329081830bceb3e3b4f3894dee74ed7d334eacb532ce72009dc429' -or
    [int64]$manifest.supersedes.cleanup_recovery.combined_namespace_target_absence_count -ne 12) {
    throw 'WP4 v2 manifest does not preserve the exact consumed predecessor terminal evidence.'
}

$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$branch = (& git -C $repoRoot branch --show-current).Trim()
if ($branch -ne 'codex/m1-s6') { throw 'WP4 v2 manifest requires branch codex/m1-s6.' }
foreach ($ancestor in @($expectedWp3, $expectedWp7Product, $expectedWp7Evidence, $expectedHandoff, $expectedCorrection, $expectedAmbiguityCorrection, $expectedFramingCorrection)) {
    & git -C $repoRoot merge-base --is-ancestor $ancestor $head
    if ($LASTEXITCODE -ne 0) { throw "Required ancestor $ancestor is not retained." }
}
$currentState = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/current-state.md') -Raw
if (-not $currentState.Contains('fresh WP4 qualification-manifest consumer binding and owner-review preparation only', [StringComparison]::Ordinal) -or
    -not $currentState.Contains($expectedWp3, [StringComparison]::Ordinal) -or
    -not $currentState.Contains($expectedWp7Product, [StringComparison]::Ordinal) -or
    -not $currentState.Contains($expectedWp7Evidence, [StringComparison]::Ordinal)) {
    throw 'WP4 v2 proposal requires the current closed native gate and exact accepted WP3/WP7 identities.'
}
$closeReady = [string]$manifest.candidate_binding.close_ready_implementation_commit
$bindingPending = $closeReady -eq ('0' * 40)
if ($bindingPending -ne ($manifest.status -eq 'draft-close-ready-binding-pending')) {
    throw 'WP4 v2 status and close-ready implementation binding disagree.'
}
if (-not $bindingPending) {
    if ($manifest.status -ne 'ready-for-owner-acceptance') {
        throw 'WP4 v2 bound candidate is not marked ready for owner acceptance.'
    }
    & git -C $repoRoot merge-base --is-ancestor $closeReady $head
    if ($LASTEXITCODE -ne 0) { throw 'Close-ready implementation commit is not an ancestor of HEAD.' }
}

$prepared = [DateTimeOffset]::ParseExact($manifest.prepared_at_utc,
    'yyyy-MM-ddTHH:mm:ss.fffffffZ', [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
$expires = [DateTimeOffset]::ParseExact($manifest.expires_at_utc,
    'yyyy-MM-ddTHH:mm:ss.fffffffZ', [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal)
if (($expires - $prepared).TotalSeconds -ne [int64]$manifest.operation_limits.authorization_window_seconds -or
    [int64]$manifest.operation_limits.gate_wall_clock_seconds -ne
        [int64]$manifest.operation_limits.primary_phase_seconds +
        [int64]$manifest.operation_limits.cleanup_reserve_seconds +
        [int64]$manifest.operation_limits.evidence_reserve_seconds) {
    throw 'WP4 v2 finite authorization/deadline partition is inconsistent.'
}
if ($expires -le [DateTimeOffset]::UtcNow) { throw 'WP4 v2 manifest has expired.' }

$allowed = @('CredWriteW', 'CredReadW', 'CredDeleteW', 'CredFree')
$forbidden = @('CredEnumerateW', 'CredRenameW', 'CredWriteDomainCredentialsW',
    'any alternate credential or secret-storage mechanism')
if (($manifest.native_boundary.allowed_calls -join '|') -ne ($allowed -join '|') -or
    ($manifest.native_boundary.forbidden_calls -join '|') -ne ($forbidden -join '|') -or
    $manifest.native_boundary.fallback -ne 'none' -or
    @($manifest.native_boundary.forbidden_calls | Where-Object { $_ -match 'Enumerate' }).Count -eq 0) {
    throw 'WP4 v2 native boundary differs from the accepted exact-call/no-enumeration/no-fallback rule.'
}
$max = $manifest.operation_limits.native_call_maxima
if ([int64]$max.CredWriteW + [int64]$max.CredReadW + [int64]$max.CredDeleteW + [int64]$max.CredFree -ne
    [int64]$max.total) { throw 'WP4 v2 native-call maxima do not sum exactly.' }
if ([int64]$manifest.provider_boundary.dns_operations -ne 0 -or
    [int64]$manifest.provider_boundary.network_operations -ne 0 -or
    [int64]$manifest.provider_boundary.provider_operations -ne 0 -or
    [int64]$manifest.provider_boundary.billable_operations -ne 0 -or
    [int64]$manifest.operation_limits.fake_provider_dispatches -ne 1) {
    throw 'WP4 v2 permits a non-fake or external provider effect.'
}
if ([int64]$manifest.operation_limits.entry_dialogs -ne 3) {
    throw 'WP4 v2 requires exactly three genuine manual entry dialogs: submit, cancel, and restored next generation.'
}
if ([bool]$manifest.entry_boundary.prepopulate -or [bool]$manifest.entry_boundary.echo -or
    [bool]$manifest.entry_boundary.clipboard_return -or
    -not ([string]$manifest.entry_boundary.control).Contains('begins empty', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('manually types', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains(
        'no secret is supplied in arguments, environment, file, stdin, IPC, or programmatic window message',
        [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('current input-desktop object', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('short finite 10-second automatic pre-entry readiness window', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('separate finite five-minute human response interval', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.control).Contains('Settings -> Add/Replace -> WPF-parented helper modal', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('clipboard paste is deliberately blocked only in the qualification harness', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('paste-capable WPF-parented helper-owned masked modal', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.operator_action).Contains('React/WebView provides only the gesture and non-secret status', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.readiness_oracle).Contains('first terminal action', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.action_routing).Contains('first terminal action only', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.entry_boundary.action_routing).Contains('never poll global key state', [StringComparison]::Ordinal)) {
    throw 'WP4 v2 does not require genuine blank non-echoing operator entry.'
}
if ([bool]$manifest.qualification_components.native_success_run.inject_cleanup_ambiguity -or
    -not ([string]$manifest.qualification_components.non_native_prerequisites.cleanup_ambiguity_probe).Contains(
        'zero later native calls', [StringComparison]::Ordinal) -or
    -not ([string]$manifest.qualification_components.actual_cleanup_ambiguity_rule).Contains(
        'no subsequent credential API call', [StringComparison]::Ordinal)) {
    throw 'WP4 v2 ambiguity placement is not terminal, non-native, and no-later-call.'
}
$processModel = [string]$manifest.qualification_components.native_success_run.process_model
foreach ($requiredProcessPhrase in @('exactly two inherited anonymous-pipe handles', 'no other inherited handle',
    'Job Object', 'no nested uncontained child')) {
    if (-not $processModel.Contains($requiredProcessPhrase, [StringComparison]::Ordinal)) {
        throw "WP4 v2 process containment is missing '$requiredProcessPhrase'."
    }
}

$targets = @($manifest.disposable_namespace.targets)
if ($manifest.disposable_namespace.namespace_id -ne 'm1-s6-wp4-native-076b981a-9d32-4e6a-af35-1e7017e0f833' -or
    $targets.Count -ne 12 -or
    @($targets.alias | Sort-Object -Unique).Count -ne 12 -or
    @($targets.target_fingerprint_sha256 | Sort-Object -Unique).Count -ne 12) {
    throw 'WP4 v2 disposable namespace or target inventory is not the exact fresh 12-target authority.'
}
foreach ($target in $targets) {
    $raw = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
    if ($actual -ne $target.target_fingerprint_sha256 -or $text.Contains($raw, [StringComparison]::Ordinal)) {
        throw "WP4 v2 target '$($target.alias)' is not exact fingerprint-only authority."
    }
}
$expectedTargets = @(
    @('interactive-primary', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-interactive-primary', 'g001', '04b35e2718e202cb0a6bfef233dbe033c791aa02b2261e1779813d310bd3baad'),
    @('interactive-cancel', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-interactive-cancel', 'g001', '24f709437c97a67819b06270d0d211aaae426bbfbd56f83774106bd2f7da5277'),
    @('size-valid', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-size-valid', 'g001', '6aa891fe3db76c45c994b7b7a461f5242621226a788c489d0bcecde87b78e2dd'),
    @('size-oversize', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-size-oversize', 'g001', '01338cb4af7abf7d50b49313cce237db80a389ba1ff2c01d23a8a96ff02d66f2'),
    @('unavailable-store', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-unavailable', 'g001', 'a7af4cc90f3f3021cf2a7220f92d247165fcaaf1f6b41410ca2d34fd55582895'),
    @('replacement-old', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-replacement', 'g001', 'e82ee891429ff57587ea0f7f35f6f5ef98ae96a9d5d75da5ad7ee716a645ae77'),
    @('replacement-new', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-replacement', 'g002', 'fea103ab44d0057a2a9cc10de5792ffec891cc6fe17086fe960a979d87eb852a'),
    @('revoke-delete', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-revoke-delete', 'g001', '92cf677dd3dfc6509d75c9d502c12ae3d4b9295b2c25b4327b965f252b10649d'),
    @('crash-restart', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-crash-restart', 'g001', 'c36ea4643f97ff6a68d1880445669f213e1ef1e2b71487b3179d6102a1ce0f95'),
    @('backup-old', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-backup-restore', 'g001', '1ce6dceb1deea0485f5c56b9dce06eb3d44cda389ff0805291a9719eb1de865f'),
    @('backup-new', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-backup-restore', 'g002', '11d51fa6e870709f346f61e931a91ab8cf5336b689f8ddcfc427283d71fb1d0a'),
    @('fake-dispatch', 'm1s6-wp4-076b981a9d324e6aaf351e7017e0f833-fake-dispatch', 'g001', 'bcb55be3c8d4f1b89103d28cd5fa40d97fcdbc528ffd2f4513f4f3b12770c0b1')
)
for ($index = 0; $index -lt $expectedTargets.Count; $index++) {
    $expected = $expectedTargets[$index]
    $target = $targets[$index]
    if ($target.alias -ne $expected[0] -or
        $target.access_profile_id -ne $expected[1] -or
        $target.generation_id -ne $expected[2] -or
        $target.target_fingerprint_sha256 -ne $expected[3]) {
        throw "WP4 v2 exact target tuple at index $index is incomplete, mutated, consumed, or reordered."
    }
}
$scenarios = @('interactive-entry-submit', 'interactive-entry-cancel', 'credential-size-boundaries',
    'secure-store-unavailable', 'replacement', 'revoke-delete',
    'helper-and-coordinator-crash-restart', 'backup-restore-reauthentication', 'fake-provider-dispatch')
if (($manifest.required_scenarios.id -join '|') -ne ($scenarios -join '|')) {
    throw 'WP4 v2 native success scenario set is incomplete, reordered, or includes ambiguity injection.'
}
foreach ($scenario in $manifest.required_scenarios) {
    foreach ($alias in $scenario.targets) {
        if ($targets.alias -notcontains $alias) { throw "Scenario '$($scenario.id)' references unknown target '$alias'." }
    }
}
$expectedScenarioTargets = @{
    'interactive-entry-submit' = 'interactive-primary'
    'interactive-entry-cancel' = 'interactive-cancel'
    'credential-size-boundaries' = 'size-valid|size-oversize'
    'secure-store-unavailable' = 'unavailable-store'
    'replacement' = 'replacement-old|replacement-new'
    'revoke-delete' = 'revoke-delete'
    'helper-and-coordinator-crash-restart' = 'crash-restart'
    'backup-restore-reauthentication' = 'backup-old|backup-new'
    'fake-provider-dispatch' = 'fake-dispatch'
}
foreach ($scenario in $manifest.required_scenarios) {
    if (($scenario.targets -join '|') -ne $expectedScenarioTargets[[string]$scenario.id]) {
        throw "WP4 v2 scenario '$($scenario.id)' has the wrong exact target set."
    }
}

$expectedCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNative -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json -OutputRoot artifacts/m1-slice6/wp4-native-076b981a'
if ($manifest.execution_command -ne $expectedCommand -or
    -not ([string]$manifest.acceptance_binding.recording).Contains(
        'WP4_V2_OWNER_ACCEPTANCE manifest_id=<manifest_id> sha256=<manifest_sha256> close_ready_commit=<close_ready_implementation_commit> expires_at_utc=<expires_at_utc>',
        [StringComparison]::Ordinal)) {
    throw 'WP4 v2 command or canonical owner-acceptance record shape differs from the finite gate.'
}

$requiredEvidenceText = $manifest.required_evidence -join "`n"
$expectedPredecessorEvidence = 'exact manifest bytes and SHA-256 plus the superseded 4936dcef terminal manifest, ambiguity evidence, authority lock, cleanup-recovery manifest/evidence/lock/receipt, and combined 12-target absence disposition'
if ([string]$manifest.required_evidence[0] -cne $expectedPredecessorEvidence) {
    throw 'WP4 v2 required evidence does not bind the exact consumed 4936dcef terminal and recovery lineage.'
}
foreach ($phrase in @('ordered allowed-call trace', 'real coordinator lifecycle', 'final-gate receipt',
    'initially blank', 'canary', 'Job Object', 'fresh independent Windows credential/security ACCEPT')) {
    if (-not $requiredEvidenceText.Contains($phrase, [StringComparison]::OrdinalIgnoreCase)) {
        throw "WP4 v2 required evidence is missing '$phrase'."
    }
}
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$receipt = [ordered]@{
    validation = 'M1/S6/WP4 v2 close-ready authorization proposal'
    status = if ($bindingPending) { 'draft-close-ready-binding-pending' } else { 'validated-ready-for-owner-acceptance' }
    manifest_id = $manifest.manifest_id
    manifest_bytes = $bytes.Length
    manifest_sha256 = $hash
    close_ready_implementation_commit = $closeReady
    accepted_wp3_candidate_commit = $expectedWp3
    accepted_wp7_product_candidate_commit = $expectedWp7Product
    accepted_wp7_evidence_commit = $expectedWp7Evidence
    authorization_handoff_commit = $expectedHandoff
    branch = $branch
    repository_head = $head
    expires_at_utc = $manifest.expires_at_utc
    target_count = $targets.Count
    native_scenario_count = $scenarios.Count
    non_native_ambiguity_prerequisite = $true
    execution_authorized = $false
    credential_manager_operations = 0
    network_operations = 0
    provider_operations = 0
}
$json = $receipt | ConvertTo-Json -Depth 8
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolved = if ([IO.Path]::IsPathRooted($OutputPath)) { [IO.Path]::GetFullPath($OutputPath) }
        else { [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath)) }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolved)) | Out-Null
    [IO.File]::WriteAllText($resolved, $json + "`n", [Text.UTF8Encoding]::new($false))
}
$json
