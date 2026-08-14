[CmdletBinding()]
param([string]$ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.ad876b9a.v1.json')
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
$schema = Join-Path $root 'contracts/repository/wp4-credential-native-recovery.ad876b9a.v1.schema.json'
if (-not (Test-Json -LiteralPath $path -SchemaFile $schema)) { throw 'Current recovery manifest schema failed.' }
$bytes = [IO.File]::ReadAllBytes($path)
$m = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100 -DateKind String

$expected = @(
    [pscustomobject]@{ alias='backup-new'; profile='m1s6-wp4-ad876b9a9f454eb48d125970d76dd4ea-backup-restore'; generation='g002'; fingerprint='d9221f7aac7ababf9e3efbf6ef69b03d2e9c8b0f51c1c552862958d5f3eff061' },
    [pscustomobject]@{ alias='fake-dispatch'; profile='m1s6-wp4-ad876b9a9f454eb48d125970d76dd4ea-fake-dispatch'; generation='g001'; fingerprint='c27212cc4f0720e9fd20f7a2aff397402257bd53ad6d568048b217ac3e3df963' }
)
$targets = @($m.disposable_namespace.targets)
if ($targets.Count -ne 2) { throw 'Current recovery requires exactly two unresolved targets.' }
for ($index = 0; $index -lt 2; $index++) {
    $target = $targets[$index]; $item = $expected[$index]
    $raw = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $derived = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
    if ($target.alias -cne $item.alias -or $target.access_profile_id -cne $item.profile -or
        $target.generation_id -cne $item.generation -or $target.target_fingerprint_sha256 -cne $item.fingerprint -or
        $derived -cne $item.fingerprint) { throw 'Current recovery exact target binding differs.' }
}
$binding = $m.binding
if ($binding.failed_manifest_id -cne 'infinium.m1-s6.wp4.credential-native-authorization/ad876b9a-9f45-4eb4-8d12-5970d76dd4ea' -or
    $binding.failed_manifest_sha256 -cne '7d1e8c35072c6676258c9cbcc47fd8833458878bf289728cc453e5e0942d35ce' -or
    $binding.failure_record_commit -cne '2efba7c96ce356c5d4687a27e115ff802ec6b42f' -or
    $binding.terminal_evidence_sha256 -cne 'cfaee3940cd780a5bcfbcbcf387124d7f7385b01a07f8f0f6fbe4439593a21e6' -or
    [int]$binding.prior_exact_absence_count -ne 10 -or
    $binding.consumed_lock_sha256 -cne 'b47e0262937f86174ae1b790f4951fbf6fe6621d1f3a25c938990143514950b8' -or
    $binding.required_branch -cne 'codex/m1-s6') { throw 'Current recovery failure binding differs.' }
$zero = '0000000000000000000000000000000000000000'
if (($m.status -ceq 'draft-binding-pending') -ne ($binding.close_ready_recovery_commit -ceq $zero)) {
    throw 'Current recovery status/close-ready binding differs.'
}
if ($m.status -ceq 'ready-for-owner-acceptance') {
    $head = (& git -C $root rev-parse HEAD).Trim()
    & git -C $root merge-base --is-ancestor $binding.close_ready_recovery_commit $head
    if ($LASTEXITCODE -ne 0) { throw 'Current recovery close-ready commit is not an ancestor of HEAD.' }
}
if (($m.native_boundary.allowed_calls -join '|') -cne 'CredReadW|CredDeleteW|CredFree' -or
    ($m.native_boundary.forbidden -join '|') -cne 'CredWriteW|CredEnumerateW|any prefix/arbitrary target|any alternate store' -or
    $m.native_boundary.fallback -cne 'none' -or $m.native_boundary.ui -cne 'none' -or $m.native_boundary.provider -cne 'none') {
    throw 'Current recovery native boundary differs.'
}
if ([int]$m.limits.wall_clock_seconds -ne 120 -or [int]$m.limits.targets -ne 2 -or
    [int]$m.limits.CredReadW -ne 6 -or [int]$m.limits.CredDeleteW -ne 2 -or
    [int]$m.limits.CredFree -ne 2 -or [int]$m.limits.total_native_calls -ne 10 -or
    [int]$m.limits.attempts -ne 1) { throw 'Current recovery finite limits differ.' }
$prepared = [DateTimeOffset]::Parse([string]$m.prepared_at_utc)
$expires = [DateTimeOffset]::Parse([string]$m.expires_at_utc)
if ($expires -le $prepared -or ($expires - $prepared) -gt [TimeSpan]::FromHours(24) -or $expires -le [DateTimeOffset]::UtcNow) {
    throw 'Current recovery expiry is invalid.'
}
$expectedCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNativeRecovery -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.ad876b9a.v1.json -OutputRoot artifacts/m1-slice6/wp4-native-recovery-df29a608'
if ($m.execution_command -cne $expectedCommand) { throw 'Current recovery command differs.' }
[pscustomobject]@{
    status = if ($m.status -eq 'draft-binding-pending') { 'draft' } else { 'ready' }
    manifest_id = $m.manifest_id
    manifest_sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    recovery_target_count = 2
    prior_exact_absence_count = 10
    combined_namespace_target_count = 12
    execution_authorized = $false
    native_operations = 0
    network_operations = 0
    provider_operations = 0
} | ConvertTo-Json
