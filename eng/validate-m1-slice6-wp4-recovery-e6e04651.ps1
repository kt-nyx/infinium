[CmdletBinding()]
param([string]$ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.e6e04651.v1.json')
if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -File $PSCommandPath -ManifestPath $ManifestPath
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$path = if ([IO.Path]::IsPathFullyQualified($ManifestPath)) {
    [IO.Path]::GetFullPath($ManifestPath)
} else { [IO.Path]::GetFullPath((Join-Path $root $ManifestPath)) }
$schema = Join-Path $root 'contracts/repository/wp4-credential-native-recovery.e6e04651.v1.schema.json'
if (-not (Test-Json -LiteralPath $path -SchemaFile $schema -ErrorAction Stop)) {
    throw 'E6 recovery manifest schema failed.'
}
$bytes = [IO.File]::ReadAllBytes($path)
$m = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100 -DateKind String
$expected = @(
    [pscustomobject]@{alias='interactive-primary';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-interactive-primary';generation='g001';fingerprint='821904462accf62dc1d6317199cd76091f7c271a599ac27ca970d5575002f3a4'},
    [pscustomobject]@{alias='interactive-cancel';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-interactive-cancel';generation='g001';fingerprint='e432bd2911ef3b00088e03bd05c40fc094489e89f4905ae8305e2494d708e9c7'},
    [pscustomobject]@{alias='size-valid';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-size-valid';generation='g001';fingerprint='53cbe11d187d98e681719a42b2e39373ac14ea8a86a1c8bc5c2f7df819bef7cc'},
    [pscustomobject]@{alias='size-oversize';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-size-oversize';generation='g001';fingerprint='250b7287feac38456b9e9f4a8dd9ecf846e03e210c588cd52fd654cf6b25c6f8'},
    [pscustomobject]@{alias='unavailable-store';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-unavailable';generation='g001';fingerprint='8b1da75ab27fb15d10d5703989430dc28386a3238b4f4cb62de7c701350f4bf7'},
    [pscustomobject]@{alias='replacement-old';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-replacement';generation='g001';fingerprint='545f3a638456276cf35967e02449f4cbb9b8c05196b39005de8d16b5e44d9ad3'},
    [pscustomobject]@{alias='replacement-new';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-replacement';generation='g002';fingerprint='f166ca075fb6c66e5d2c4782b42f0c5c35f32bd00f9a867b3a63b9dcfac55b0c'},
    [pscustomobject]@{alias='revoke-delete';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-revoke-delete';generation='g001';fingerprint='2e7ff725c4f2c404b59cebe08e646d71ae9f3112e9369142002a35de0c875619'},
    [pscustomobject]@{alias='crash-restart';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-crash-restart';generation='g001';fingerprint='c3b90f33f1dfb98b44f1db7b3bdc550175a55333a21832c58a330696d840740a'},
    [pscustomobject]@{alias='backup-old';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-backup-restore';generation='g001';fingerprint='b1a4d68aaaefd0f62ae7979c994ca6193a34f273e463f5395e87d474cbb9f40a'},
    [pscustomobject]@{alias='backup-new';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-backup-restore';generation='g002';fingerprint='c3a2805323e54bdc4aba66b5d3e33686ce12a525dfed82c4b2e463deea2c28b1'},
    [pscustomobject]@{alias='fake-dispatch';profile='m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-fake-dispatch';generation='g001';fingerprint='d189000ff046ad5062614e915882796017b97778178875a58a77ebde902ab1c8'}
)
$targets = @($m.disposable_namespace.targets)
if ($targets.Count -ne 12) { throw 'E6 recovery requires exactly 12 unresolved targets.' }
$slots = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
for ($index = 0; $index -lt 12; $index++) {
    $target = $targets[$index]; $item = $expected[$index]
    $raw = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $derived = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
    if ($target.alias -cne $item.alias -or $target.access_profile_id -cne $item.profile -or
        $target.generation_id -cne $item.generation -or
        $target.target_fingerprint_sha256 -cne $item.fingerprint -or $derived -cne $item.fingerprint -or
        -not $slots.Add("$($target.access_profile_id)`0$($target.generation_id)")) {
        throw 'E6 recovery exact ordered target binding differs.'
    }
}
$binding = $m.binding
if ($binding.failed_manifest_id -cne 'infinium.m1-s6.wp4.credential-native-authorization/e6e04651-4cd5-4f5d-8b46-5ec84a81cbbe' -or
    $binding.failed_manifest_sha256 -cne 'c0e6aed84ca8d01a2722ff9970d52f816f47626f3e309cf9081b3c71b1245497' -or
    $binding.failure_record_commit -cne '0d0b230c73ddd5bb11f4b0f8cf3b85a5cee2a82d' -or
    $binding.terminal_evidence_sha256 -cne '5b565888a412188f7c814c0d923e696e27d4135d7ebb23f5884ef7b2e3f228c7' -or
    [int]$binding.prior_exact_absence_count -ne 0 -or
    $binding.consumed_lock_sha256 -cne '4fc808d221d340eb6b145ceffa35a2472cd621102b0e0dc280a8dbb71f77ddd4' -or
    $binding.required_branch -cne 'codex/m1-s6') { throw 'E6 recovery failure binding differs.' }
$zero = '0000000000000000000000000000000000000000'
if (($m.status -ceq 'draft-binding-pending') -ne ($binding.close_ready_recovery_commit -ceq $zero)) {
    throw 'E6 recovery status/close-ready binding differs.'
}
if ($m.status -ceq 'ready-for-owner-acceptance') {
    $head = (& git -C $root rev-parse HEAD).Trim()
    & git -C $root cat-file -e "$($binding.close_ready_recovery_commit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'E6 recovery close-ready commit does not exist.' }
    & git -C $root merge-base --is-ancestor $binding.close_ready_recovery_commit $head
    if ($LASTEXITCODE -ne 0) { throw 'E6 recovery close-ready commit is not an ancestor of HEAD.' }
}
if ($m.disposable_namespace.namespace_id -cne 'm1-s6-wp4-native-e6e04651-4cd5-4f5d-8b46-5ec84a81cbbe' -or
    $m.disposable_namespace.reuse -cne 'cleanup-only; never requalification; terminal after this recovery attempt') {
    throw 'E6 recovery namespace/reuse differs.'
}
if (($m.native_boundary.allowed_calls -join '|') -cne 'CredReadW|CredDeleteW|CredFree' -or
    ($m.native_boundary.forbidden -join '|') -cne 'CredWriteW|CredEnumerateW|any prefix/arbitrary target|any alternate store' -or
    $m.native_boundary.fallback -cne 'none' -or $m.native_boundary.ui -cne 'none' -or
    $m.native_boundary.provider -cne 'none') { throw 'E6 recovery native boundary differs.' }
if ([int]$m.limits.wall_clock_seconds -ne 120 -or [int]$m.limits.targets -ne 12 -or
    [int]$m.limits.CredReadW -ne 36 -or [int]$m.limits.CredDeleteW -ne 12 -or
    [int]$m.limits.CredFree -ne 12 -or [int]$m.limits.total_native_calls -ne 60 -or
    [int]$m.limits.attempts -ne 1) { throw 'E6 recovery finite limits differ.' }
if ($m.cleanup_contract.success -cne 'each fingerprint ends with exact ERROR_NOT_FOUND and canonical trace/free pairing' -or
    $m.cleanup_contract.combined_absence -cne 'this recovery must independently prove all 12 namespace targets absent; no prior per-target absence proof is admissible' -or
    $m.cleanup_contract.ambiguity -cne 'stop immediately; no later native call; namespace remains blocked forever' -or
    [int]$m.cleanup_contract.network_operations -ne 0 -or [int]$m.cleanup_contract.dns_operations -ne 0 -or
    [int]$m.cleanup_contract.provider_operations -ne 0 -or [int]$m.cleanup_contract.billable_operations -ne 0) {
    throw 'E6 recovery cleanup contract differs.'
}
$prepared = [DateTimeOffset]::Parse([string]$m.prepared_at_utc)
$expires = [DateTimeOffset]::Parse([string]$m.expires_at_utc)
if ($expires -le $prepared -or ($expires - $prepared) -gt [TimeSpan]::FromHours(24) -or
    $expires -le [DateTimeOffset]::UtcNow) { throw 'E6 recovery expiry is invalid.' }
$expectedCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNativeRecovery -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.e6e04651.v1.json -OutputRoot artifacts/m1-slice6/wp4-native-recovery-6232bae5'
if ($m.execution_command -cne $expectedCommand) { throw 'E6 recovery command differs.' }
[pscustomobject]@{
    status = if ($m.status -eq 'draft-binding-pending') { 'draft' } else { 'ready' }
    manifest_id = $m.manifest_id
    manifest_sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    recovery_target_count = 12
    prior_exact_absence_count = 0
    combined_namespace_target_count = 12
    execution_authorized = $false
    native_operations = 0
    network_operations = 0
    provider_operations = 0
} | ConvertTo-Json
