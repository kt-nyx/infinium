[CmdletBinding()]
param([string]$ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.v1.json')
if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -File $PSCommandPath -ManifestPath $ManifestPath
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$path = [IO.Path]::GetFullPath((Join-Path $root $ManifestPath))
$schema = Join-Path $root 'contracts/repository/wp4-credential-native-recovery.v1.schema.json'
if (-not (Test-Json -LiteralPath $path -SchemaFile $schema)) { throw 'Recovery manifest schema failed.' }
$bytes = [IO.File]::ReadAllBytes($path)
$m = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100 -DateKind String

$expectedAliases = @(
    'interactive-primary', 'interactive-cancel', 'size-valid', 'size-oversize',
    'unavailable-store', 'replacement-old', 'replacement-new', 'revoke-delete',
    'crash-restart', 'backup-old', 'backup-new', 'fake-dispatch')
$expectedProfiles = @(
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-interactive-primary',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-interactive-cancel',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-size-valid',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-size-oversize',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-unavailable-store',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-replacement',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-replacement',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-revoke-delete',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-crash-restart',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-backup-restore',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-backup-restore',
    'm1s6-wp4-cedc4c470c58490e8d145159362aadf3-fake-dispatch')
$expectedGenerations = @('g001', 'g001', 'g001', 'g001', 'g001', 'g001', 'g002', 'g001', 'g001', 'g001', 'g002', 'g001')
$targets = @($m.disposable_namespace.targets)
if ($targets.Count -ne 12) { throw 'Recovery requires 12 targets.' }
$slots = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
for ($index = 0; $index -lt $targets.Count; $index++) {
    $target = $targets[$index]
    if ($target.alias -cne $expectedAliases[$index] -or
        $target.access_profile_id -cne $expectedProfiles[$index] -or
        $target.generation_id -cne $expectedGenerations[$index] -or
        -not $slots.Add("$($target.access_profile_id)`0$($target.generation_id)")) {
        throw 'Recovery target alias/slot order differs.'
    }
    $raw = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
    if ($sha -cne $target.target_fingerprint_sha256) { throw "Target fingerprint mismatch $($target.alias)" }
}

$binding = $m.binding
if ($binding.failed_manifest_id -cne 'infinium.m1-s6.wp4.credential-native-authorization/cedc4c47-0c58-490e-8d14-5159362aadf3' -or
    $binding.failed_manifest_sha256 -cne '6a2c1f39137de8e40d9e9574ba963d39c8bbdb7c880663a363cc69e65145c952' -or
    $binding.failure_record_commit -cne 'fd6bd645f041502333d92b5e95c69bf8c69f2c83' -or
    $binding.consumed_lock_sha256 -cne '05bf7fc259bf90d367c20f9ba23af3d1525aa2514ee6e1888304cbaf44b364c4' -or
    $binding.required_branch -cne 'codex/m1-s6') { throw 'Recovery failure binding differs.' }
$zeroCommit = '0000000000000000000000000000000000000000'
if (($m.status -ceq 'draft-binding-pending') -ne
    ($binding.close_ready_recovery_commit -ceq $zeroCommit)) {
    throw 'Recovery status/close-ready binding differs.'
}
if ($m.status -ceq 'ready-for-owner-acceptance') {
    $head = (& git -C $root rev-parse HEAD).Trim()
    & git -C $root cat-file -e "$($binding.close_ready_recovery_commit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Recovery close-ready commit does not exist.' }
    & git -C $root merge-base --is-ancestor $binding.close_ready_recovery_commit $head
    if ($LASTEXITCODE -ne 0) { throw 'Recovery close-ready commit is not an ancestor of HEAD.' }
}
if ($m.disposable_namespace.namespace_id -cne 'm1-s6-wp4-native-cedc4c47-0c58-490e-8d14-5159362aadf3' -or
    $m.disposable_namespace.reuse -cne 'cleanup-only; never requalification; terminal after this recovery attempt') {
    throw 'Recovery namespace/reuse differs.'
}
if (($m.native_boundary.allowed_calls -join '|') -cne 'CredReadW|CredDeleteW|CredFree' -or
    ($m.native_boundary.forbidden -join '|') -cne 'CredWriteW|CredEnumerateW|any prefix/arbitrary target|any alternate store' -or
    $m.native_boundary.fallback -cne 'none' -or $m.native_boundary.ui -cne 'none' -or
    $m.native_boundary.provider -cne 'none') { throw 'Recovery native boundary differs.' }
if ([int]$m.limits.wall_clock_seconds -ne 120 -or [int]$m.limits.targets -ne 12 -or
    [int]$m.limits.CredReadW -ne 36 -or [int]$m.limits.CredDeleteW -ne 12 -or
    [int]$m.limits.CredFree -ne 12 -or [int]$m.limits.total_native_calls -ne 60 -or
    [int]$m.limits.attempts -ne 1) { throw 'Recovery finite limits differ.' }
if ($m.cleanup_contract.success -cne 'each fingerprint ends with exact ERROR_NOT_FOUND and canonical trace/free pairing' -or
    $m.cleanup_contract.ambiguity -cne 'stop immediately; no later native call; namespace remains blocked forever' -or
    [int]$m.cleanup_contract.network_operations -ne 0 -or [int]$m.cleanup_contract.dns_operations -ne 0 -or
    [int]$m.cleanup_contract.provider_operations -ne 0 -or [int]$m.cleanup_contract.billable_operations -ne 0) {
    throw 'Recovery cleanup contract differs.'
}
$prepared = [DateTimeOffset]::Parse([string]$m.prepared_at_utc)
$expires = [DateTimeOffset]::Parse([string]$m.expires_at_utc)
if ($expires -le $prepared -or ($expires - $prepared) -gt [TimeSpan]::FromHours(24)) { throw 'Recovery expiry is not finite.' }
if ($expires -le [DateTimeOffset]::UtcNow) { throw 'Recovery authority is expired.' }
$shortId = ([string]$m.manifest_id).Split('/')[-1].Split('-')[0]
$expectedCommand = "powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNativeRecovery -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.v1.json -OutputRoot artifacts/m1-slice6/wp4-native-recovery-$shortId"
if ($m.execution_command -cne $expectedCommand) { throw 'Recovery command/output root differs.' }

[pscustomobject]@{
    status = if ($m.status -eq 'draft-binding-pending') { 'draft' } else { 'ready' }
    manifest_id = $m.manifest_id
    manifest_sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    execution_authorized = $false
    native_operations = 0
    network_operations = 0
    provider_operations = 0
} | ConvertTo-Json
