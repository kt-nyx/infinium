[CmdletBinding()]
param(
    [string]$ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.e3f76cd6.v1.json',
    [switch]$HistoricalEvidence
)
if ($PSVersionTable.PSEdition -ne 'Core') {
    $forward = @('-NoProfile', '-File', $PSCommandPath, '-ManifestPath', $ManifestPath)
    if ($HistoricalEvidence) { $forward += '-HistoricalEvidence' }
    & (Get-Command pwsh.exe).Source @forward
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$path = if ([IO.Path]::IsPathFullyQualified($ManifestPath)) {
    [IO.Path]::GetFullPath($ManifestPath)
} else { [IO.Path]::GetFullPath((Join-Path $root $ManifestPath)) }
$expectedPath = [IO.Path]::GetFullPath((Join-Path $root `
    'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.e3f76cd6.v1.json'))
if ($HistoricalEvidence -and
    -not [string]::Equals($path, $expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'E3 historical recovery validation accepts only the exact tracked path.'
}
$schema = Join-Path $root 'contracts/repository/wp4-credential-native-recovery.e3f76cd6.v1.schema.json'
if (-not (Test-Json -LiteralPath $path -SchemaFile $schema)) { throw 'E3 recovery manifest schema failed.' }
$bytes = [IO.File]::ReadAllBytes($path)
$m = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100 -DateKind String

$expected = @(
    [pscustomobject]@{ alias='backup-new'; profile='m1s6-wp4-e3f76cd645c14e3aa84bfa3251b3cb60-backup-restore'; generation='g002'; fingerprint='b78f660da620c5feee10adff48401ac1b4bc3ec0daec2e35bc39b399d55b41b3' },
    [pscustomobject]@{ alias='fake-dispatch'; profile='m1s6-wp4-e3f76cd645c14e3aa84bfa3251b3cb60-fake-dispatch'; generation='g001'; fingerprint='08e0f7330185d89fa471d83434e768a3d9d54961d325e5b44b5d84f664cc6b02' }
)
$targets = @($m.disposable_namespace.targets)
if ($targets.Count -ne 2) { throw 'E3 recovery requires exactly two unresolved targets.' }
for ($index = 0; $index -lt 2; $index++) {
    $target = $targets[$index]; $item = $expected[$index]
    $raw = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $derived = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
    if ($target.alias -cne $item.alias -or $target.access_profile_id -cne $item.profile -or
        $target.generation_id -cne $item.generation -or $target.target_fingerprint_sha256 -cne $item.fingerprint -or
        $derived -cne $item.fingerprint) { throw 'E3 recovery exact target binding differs.' }
}
$binding = $m.binding
if ($binding.failed_manifest_id -cne 'infinium.m1-s6.wp4.credential-native-authorization/e3f76cd6-45c1-4e3a-a84b-fa3251b3cb60' -or
    $binding.failed_manifest_sha256 -cne '9f43e5d9d7fb8b0cdba9195ba835631fa6073dff1c6ae86eb68a914b04c57db0' -or
    $binding.failure_record_commit -cne 'e2de2ce63a13222784abbdc27d91abcdc0ed4d91' -or
    $binding.terminal_evidence_sha256 -cne '18b4bd64d5ae32596330271e415b10a0a6d8516fded9dfc35bf1fee26dc7cd9f' -or
    [int]$binding.prior_exact_absence_count -ne 10 -or
    $binding.consumed_lock_sha256 -cne '945d2bbf440af7d5a305ae4cbb4dee73636175ff679ac8582a28e84cd73e0e5d' -or
    $binding.required_branch -cne 'codex/m1-s6') { throw 'E3 recovery failure binding differs.' }
$zero = '0000000000000000000000000000000000000000'
if (($m.status -ceq 'draft-binding-pending') -ne ($binding.close_ready_recovery_commit -ceq $zero)) {
    throw 'E3 recovery status/close-ready binding differs.'
}
if ($m.status -ceq 'ready-for-owner-acceptance') {
    $head = (& git -C $root rev-parse HEAD).Trim()
    & git -C $root merge-base --is-ancestor $binding.close_ready_recovery_commit $head
    if ($LASTEXITCODE -ne 0) { throw 'E3 recovery close-ready commit is not an ancestor of HEAD.' }
}
if (($m.native_boundary.allowed_calls -join '|') -cne 'CredReadW|CredDeleteW|CredFree' -or
    ($m.native_boundary.forbidden -join '|') -cne 'CredWriteW|CredEnumerateW|any prefix/arbitrary target|any alternate store' -or
    $m.native_boundary.fallback -cne 'none' -or $m.native_boundary.ui -cne 'none' -or $m.native_boundary.provider -cne 'none') {
    throw 'E3 recovery native boundary differs.'
}
if ([int]$m.limits.wall_clock_seconds -ne 120 -or [int]$m.limits.targets -ne 2 -or
    [int]$m.limits.CredReadW -ne 6 -or [int]$m.limits.CredDeleteW -ne 2 -or
    [int]$m.limits.CredFree -ne 2 -or [int]$m.limits.total_native_calls -ne 10 -or
    [int]$m.limits.attempts -ne 1) { throw 'E3 recovery finite limits differ.' }
$prepared = [DateTimeOffset]::Parse([string]$m.prepared_at_utc)
$expires = [DateTimeOffset]::Parse([string]$m.expires_at_utc)
if ($expires -le $prepared -or ($expires - $prepared) -gt [TimeSpan]::FromHours(24) -or
    (-not $HistoricalEvidence -and $expires -le [DateTimeOffset]::UtcNow)) {
    throw 'E3 recovery expiry is invalid.'
}
$expectedCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNativeRecovery -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.e3f76cd6.v1.json -OutputRoot artifacts/m1-slice6/wp4-native-recovery-8b7fc811'
if ($m.execution_command -cne $expectedCommand) { throw 'E3 recovery command differs.' }
[pscustomobject]@{
    status = if ($HistoricalEvidence) { 'historical-evidence' } elseif ($m.status -eq 'draft-binding-pending') { 'draft' } else { 'ready' }
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
