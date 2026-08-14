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
$expectedManifestId = 'infinium.m1-s6.wp4.credential-native-authorization/c6e9226e-3d95-496c-bda6-c9142bb6b980'
$expectedWp3 = 'b32939e8b7491a5c47453f912d25dd98c090f103'
$expectedWp7Product = '59367a7479a7395b173b974bf720543aab2404d4'
$expectedWp7Evidence = '51251c0e0eb98d67dbc9b295b9ff084ebca33890'
$expectedHandoff = '44fbcc0542bef77f93c83f1422406a2b6012f0d5'
$expectedCorrection = '2f95692687b60d97db2710835e9d0966f131c164'
$expectedAmbiguityCorrection = '2dce8acc27eece01b0232dd531a2deb27ef752af'
$expectedFramingCorrection = '3456fe02594fd365b1d2627dd08fad44fe0aee92'
$expectedFinalizationCorrection = '03ae6929bad069c7c9e351b2ed5bd361e31b89e7'
$expectedOldManifest = '36890ec28cf706484730fc9dfbd6dec5bcf3be76ed5c509a373fa61b8c910ee2'
$expectedOldLock = '80a014c72636221a2cf52008bb9ee0d27cd0c6badbfa5659d324a6ad9be350a7'
$historicalManifestBlob = (& git -C $repoRoot rev-parse `
    '31643235c014a93f71096d5c80d2a911758e328f:docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json').Trim()
if ($LASTEXITCODE -ne 0 -or $historicalManifestBlob -ne '2de2215bb1dc531baa41778381b1cf89ab56618b') {
    throw 'The consumed 076b981a manifest history differs from its terminal exact-byte authority.'
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
$priorArtifactPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-076b981a/coordinator-stderr.txt' `
    '1c624078f51c8d4eab9563384dd5f67cecde81b16995f0819d29bf2457165f6e'
[void](Assert-ExactArtifactHash 'artifacts/m1-slice6/wp4-native-076b981a/credential-native-summary.txt' `
    'e05a4db0c0f7f2422ce88565b81ea8bf342e96bcf1a06feaa09a8c7a94e03299')
[void](Assert-ExactArtifactHash 'artifacts/m1-slice6/wp4-native-076b981a/native-backup-metadata.v2.json' `
    '04f44827955b7a6d72ba9808b317edb85de70be0759a654a3b15433ac0fefa6c')
$priorLockPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-authority-locks/25c657c7241731d5f91d9df3f49dd2cc0c3241eb5c6a470a3817400552d9d3c8.json' $expectedOldLock
$recoveryEvidencePath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-040817c8/credential-native-recovery-evidence.v1.json' `
    'd65cefe9c2a71231c8fd9a6c4105f26acd742f49af248f38be989b059a93a515'
$recoveryLockPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-locks/e6a97b4f667a5487b314e4de2ae029601348455127c5d33732dd9e3ec63a1724.json' `
    '178711a914651b180d667285c6d4e22c8a820aa6f8450e398626a121afc2c5d0'
$recoveryReceiptPath = Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-040817c8/credentialnativerecovery.json' `
    '413789b410eb3718f7185d01d614d90444b2edb6196338dd21b246802cdb00cf'
[void](Assert-ExactArtifactHash `
    'artifacts/m1-slice6/wp4-native-recovery-040817c8/CredentialNativeRecovery.reconstructed.json' `
    'd105f42e7dfcec30590f40fa9b9ce0c65fe0c4a6aca9d1bd09b47ac048e3d853')
& (Join-Path $repoRoot 'eng/validate-m1-slice6-wp4-recovery-076b981a.ps1') -PostEffect
if ($LASTEXITCODE -ne 0) {
    throw 'The consumed 076b981a recovery lineage failed its exact post-effect validator.'
}
$priorLock = Get-Content -LiteralPath $priorLockPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryEvidence = Get-Content -LiteralPath $recoveryEvidencePath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryLock = Get-Content -LiteralPath $recoveryLockPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$recoveryReceipt = Get-Content -LiteralPath $recoveryReceiptPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
if ($priorLock.disposition -ne 'consumed-before-native-launch-never-delete-or-reuse' -or
    $priorLock.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-authorization/076b981a-9d32-4e6a-af35-1e7017e0f833' -or
    $priorLock.manifest_sha256 -ne $expectedOldManifest) {
    throw 'The consumed 076b981a terminal artifact or authority lock is not exact.'
}
if ($recoveryEvidence.status -ne 'passed' -or
    $recoveryEvidence.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-recovery/040817c8-0a87-480a-915c-71dc2fe54da3' -or
    [bool]$recoveryEvidence.cleanup_ambiguity -or
    -not [bool]$recoveryEvidence.namespace_reuse_blocked -or
    @($recoveryEvidence.target_absence).Count -ne 12 -or
    [int64]$recoveryEvidence.prior_exact_absence_count -ne 0 -or
    [int64]$recoveryEvidence.combined_namespace_target_absence_count -ne 12 -or
    [int64]$recoveryEvidence.native_call_counts.cred_write_w -ne 0 -or
    [int64]$recoveryEvidence.native_call_counts.cred_read_w -ne 12 -or
    [int64]$recoveryEvidence.native_call_counts.cred_delete_w -ne 0 -or
    [int64]$recoveryEvidence.native_call_counts.cred_free -ne 0 -or
    [int64]$recoveryEvidence.native_call_counts.total -ne 12 -or
    [int64]$recoveryEvidence.network_operations -ne 0 -or
    [int64]$recoveryEvidence.dns_operations -ne 0 -or
    [int64]$recoveryEvidence.provider_operations -ne 0 -or
    [int64]$recoveryEvidence.billable_operations -ne 0 -or
    $recoveryLock.disposition -ne 'consumed-never-reuse' -or
    $recoveryLock.manifest_id -ne $recoveryEvidence.manifest_id -or
    $recoveryReceipt.status -ne 'passed' -or
    [int64]$recoveryReceipt.evidence.combined_namespace_target_absence_count -ne 12) {
    throw 'The 076b981a cleanup recovery evidence, lock, or receipt is not terminal and exact.'
}
if ($manifest.schema_identity -ne 'infinium.repository.wp4-credential-native-authorization/1.6.0' -or
    $manifest.manifest_id -ne $expectedManifestId -or
    $manifest.effect_authority -ne 'none-until-owner-accepts-exact-manifest-bytes' -or
    $manifest.candidate_binding.accepted_wp3_candidate_commit -ne $expectedWp3 -or
    $manifest.candidate_binding.accepted_wp7_product_candidate_commit -ne $expectedWp7Product -or
    $manifest.candidate_binding.accepted_wp7_evidence_commit -ne $expectedWp7Evidence -or
    $manifest.candidate_binding.authorization_handoff_commit -ne $expectedHandoff -or
    $manifest.candidate_binding.sqlite_correction_candidate_commit -ne $expectedCorrection -or
    $manifest.candidate_binding.ambiguity_evidence_correction_candidate_commit -ne $expectedAmbiguityCorrection -or
    $manifest.candidate_binding.native_failure_evidence_and_containment_correction_candidate_commit -ne $expectedFramingCorrection -or
    $manifest.candidate_binding.evidence_finalization_correction_candidate_commit -ne $expectedFinalizationCorrection) {
    throw 'WP4 v2 manifest is not bound to the exact accepted WP3/WP7/handoff identities.'
}
if ($manifest.supersedes.manifest_sha256 -ne $expectedOldManifest -or
    $manifest.supersedes.terminal_artifact_kind -ne 'typed-coordinator-stderr-post-success-evidence-finalization' -or
    $manifest.supersedes.terminal_artifact_sha256 -ne '1c624078f51c8d4eab9563384dd5f67cecde81b16995f0819d29bf2457165f6e' -or
    $manifest.supersedes.success_summary_sha256 -ne 'e05a4db0c0f7f2422ce88565b81ea8bf342e96bcf1a06feaa09a8c7a94e03299' -or
    $manifest.supersedes.backup_metadata_sha256 -ne '04f44827955b7a6d72ba9808b317edb85de70be0759a654a3b15433ac0fefa6c' -or
    $manifest.supersedes.output_inventory_sha256 -ne '9e3f55968721c55ce1637dfc00673acd757c6ea04b3f640bb2acb19354b4427f' -or
    $manifest.supersedes.authority_lock_sha256 -ne $expectedOldLock -or
    $manifest.supersedes.namespace_disposition -ne 'terminal-cleanup-confirmed-absent-never-reuse' -or
    $manifest.supersedes.cleanup_recovery.manifest_id -ne 'infinium.m1-s6.wp4.credential-native-recovery/040817c8-0a87-480a-915c-71dc2fe54da3' -or
    $manifest.supersedes.cleanup_recovery.manifest_sha256 -ne '94cb5c77b906100c6c436ddbb889f7511b2f4c1cea0c60556651c97b7020414d' -or
    $manifest.supersedes.cleanup_recovery.evidence_sha256 -ne 'd65cefe9c2a71231c8fd9a6c4105f26acd742f49af248f38be989b059a93a515' -or
    $manifest.supersedes.cleanup_recovery.authority_lock_sha256 -ne '178711a914651b180d667285c6d4e22c8a820aa6f8450e398626a121afc2c5d0' -or
    $manifest.supersedes.cleanup_recovery.receipt_sha256 -ne '413789b410eb3718f7185d01d614d90444b2edb6196338dd21b246802cdb00cf' -or
    $manifest.supersedes.cleanup_recovery.reconstructed_receipt_sha256 -ne 'd105f42e7dfcec30590f40fa9b9ce0c65fe0c4a6aca9d1bd09b47ac048e3d853' -or
    [int64]$manifest.supersedes.cleanup_recovery.combined_namespace_target_absence_count -ne 12) {
    throw 'WP4 v2 manifest does not preserve the exact consumed predecessor terminal evidence.'
}

$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$branch = (& git -C $repoRoot branch --show-current).Trim()
if ($branch -ne 'codex/m1-s6') { throw 'WP4 v2 manifest requires branch codex/m1-s6.' }
foreach ($ancestor in @($expectedWp3, $expectedWp7Product, $expectedWp7Evidence, $expectedHandoff, $expectedCorrection, $expectedAmbiguityCorrection, $expectedFramingCorrection, $expectedFinalizationCorrection)) {
    & git -C $repoRoot merge-base --is-ancestor $ancestor $head
    if ($LASTEXITCODE -ne 0) { throw "Required ancestor $ancestor is not retained." }
}
$currentState = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/current-state.md') -Raw
if (-not $currentState.Contains('c6e9226e-3d95-496c-bda6-c9142bb6b980', [StringComparison]::Ordinal) -or
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
    -not ([string]$manifest.entry_boundary.operator_action).Contains(
        'manually types disposable dummy value #1 and selects Submit in dialog #1; leaves dialog #2 blank and selects Cancel; then manually types a different disposable dummy value #2 and selects Submit in restored-g002 dialog #3',
        [StringComparison]::Ordinal) -or
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
if ($manifest.disposable_namespace.namespace_id -ne 'm1-s6-wp4-native-c6e9226e-3d95-496c-bda6-c9142bb6b980' -or
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
    @('interactive-primary', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-interactive-primary', 'g001', '735a2bb140500c961b6dd1a043328e10ea403fd718a37e8fc1d20278429e2902'),
    @('interactive-cancel', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-interactive-cancel', 'g001', '70ac9332bcde2d808cce41410f75ffc65db1cc19ea00a94d088949f6d359d05b'),
    @('size-valid', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-size-valid', 'g001', 'ee50987dfacfe66e26648307d5163919c7a44289eef69764ac610442d9e1141a'),
    @('size-oversize', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-size-oversize', 'g001', '55ff9c3afb4f6e3766fd58adf26e8ae2e70589dc915bb857ba74547d36d6b54f'),
    @('unavailable-store', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-unavailable', 'g001', 'dd488672949a8bd26896171648b6dcf0a500e133c5c894555cfa04967712f5cd'),
    @('replacement-old', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-replacement', 'g001', 'adc83ec9f53a0c15e04f4fb61adb0d265a3ba9bee4cf40755e1d0bf19e86122f'),
    @('replacement-new', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-replacement', 'g002', '335870b602b5b897dcf199f6ce7b619db5057df98863bcf1fae6022866b45393'),
    @('revoke-delete', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-revoke-delete', 'g001', 'e5de9a8f2d96dbb73111607c42ee2c3d38f9089d9df72c7ab5997e7cba5e7112'),
    @('crash-restart', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-crash-restart', 'g001', '4def6a88eb6e61b7fbbac4965a90f963aeef96b1144c000cda53f58951275670'),
    @('backup-old', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-backup-restore', 'g001', '94c87d9b953118112df5e0fc319fa6c8079e8c62be2ca50abaa176fe972dacd5'),
    @('backup-new', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-backup-restore', 'g002', '82975530d1612c1984c1a9befb8f89f20d1e413858e1a48e6ef405ab225deda7'),
    @('fake-dispatch', 'm1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-fake-dispatch', 'g001', '57097d7dcfc1702fc8d7c39195605cfc137f888229c78372ec7cccdc4ddc9750')
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

$expectedCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNative -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json -OutputRoot artifacts/m1-slice6/wp4-native-c6e9226e'
if ($manifest.execution_command -ne $expectedCommand -or
    -not ([string]$manifest.acceptance_binding.recording).Contains(
        'WP4_V2_OWNER_ACCEPTANCE manifest_id=<manifest_id> sha256=<manifest_sha256> close_ready_commit=<close_ready_implementation_commit> expires_at_utc=<expires_at_utc>',
        [StringComparison]::Ordinal)) {
    throw 'WP4 v2 command or canonical owner-acceptance record shape differs from the finite gate.'
}

$requiredEvidenceText = $manifest.required_evidence -join "`n"
$expectedPredecessorEvidence = 'exact manifest bytes and SHA-256 plus the superseded 076b981a terminal manifest, typed post-success evidence-finalization artifact, success summary, backup metadata, output inventory, authority lock, cleanup-recovery manifest/evidence/lock/gate receipt/reconstructed receipt, combined 12-target absence disposition, and accepted evidence-finalization correction candidate'
if ([string]$manifest.required_evidence[0] -cne $expectedPredecessorEvidence) {
    throw 'WP4 v2 required evidence does not bind the exact consumed 076b981a terminal and recovery lineage.'
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
