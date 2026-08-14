[CmdletBinding()]
param([string]$ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.4936dcef.v1.json')
if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -File $PSCommandPath -ManifestPath $ManifestPath
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$expectedPath = [IO.Path]::GetFullPath((Join-Path $root `
    'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.4936dcef.v1.json'))
$path = if ([IO.Path]::IsPathFullyQualified($ManifestPath)) {
    [IO.Path]::GetFullPath($ManifestPath)
} else { [IO.Path]::GetFullPath((Join-Path $root $ManifestPath)) }
if (-not [string]::Equals($path, $expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw '4936dcef recovery validator accepts only the exact tracked path.'
}
$schema = Join-Path $root 'contracts/repository/wp4-credential-native-recovery.4936dcef.v1.schema.json'
if (-not (Test-Json -LiteralPath $path -SchemaFile $schema -ErrorAction Stop)) {
    throw '4936dcef recovery manifest schema failed.'
}
$bytes = [IO.File]::ReadAllBytes($path)
$sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$m = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100 -DateKind String
$target = @($m.disposable_namespace.targets)
if ($target.Count -ne 1 -or $target[0].alias -cne 'backup-new' -or
    $target[0].access_profile_id -cne 'm1s6-wp4-4936dcefa0f4430298990afd99b19799-backup-restore' -or
    $target[0].generation_id -cne 'g002') {
    throw '4936dcef recovery exact target differs.'
}
$raw = "Infinium:$($target[0].access_profile_id):$($target[0].generation_id)"
$derived = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
if ($derived -cne '01fcbe4a9138bcc10819e04cdadc9f83a592c022b4b436bbd2d29f50b52816c7' -or
    $target[0].target_fingerprint_sha256 -cne $derived) {
    throw '4936dcef recovery target fingerprint differs.'
}
$prior = @(
    '1280f6eb54fc26043b11c19b40f8d5fff0777aeb3ea6b8ad3d225365b1b41677',
    '1b83b44086e5ef8d7173acc772da1a471924a5e7fd53bfefe9e85f882d282275',
    'ce92785c4620f0482503a0cfc282feb87e065827735ad4b5d2645c448b5b9f5f',
    'd16fcd6b08b0d3eeb4931b8d440a5e5a245000692343e9e0ce57a415b1e2915e',
    '5a63263824e2d065203489bbf18615a6324d8493a153e57d98a9021947d19e9c',
    'ebd23333c28423c14099f86ae43b294e9c0911359b8df8051792ed261d86811a',
    '4b57de8f5cf207ca7fd25a99582a7e9a9c65bd0756570312d2211df9c2de65e8',
    'eb5044984f24f3a4a56a016a24f50245659d270f20bb31ce6c2752b3d75aba2a',
    '6d8306b7661f2b3242ad93c2438917fac74cf93b1e52c486e95ff346550d37bb',
    '13df4cf0001cd0571f9b05463d0bf9e61016480b1eb637b7c99378bd584fb09e',
    'e995b83b9d65f4d8d1f07ae59d03d6af31fc90275ed48a90e059106a7d388900')
$binding = $m.binding
if ($binding.failed_manifest_id -cne 'infinium.m1-s6.wp4.credential-native-authorization/4936dcef-a0f4-4302-9899-0afd99b19799' -or
    $binding.failed_manifest_sha256 -cne '910ff1552d178bcfe5ff36fd9b618d187203c38c6b023d9610af5c702bdb3393' -or
    $binding.failed_execution_head_commit -cne '8f49943d0af53c495b8f288048cbd8d8bd1fe775' -or
    $binding.failure_record_commit -cne '2eb7ed8b81331698bc2bffe3786b62c682b88598' -or
    $binding.terminal_evidence_sha256 -cne '0a10a873b7356612cd8ac25934c8fbf85ab0cae76f7aea42b2317421dd251674' -or
    $binding.consumed_lock_sha256 -cne '18ffe3e24687543c7c0d538ec98874245ef3fe0c3d2c26945d375b5e23604d02' -or
    [int]$binding.prior_exact_absence_count -ne 11 -or
    (($binding.prior_exact_absence_fingerprints -join '|') -cne ($prior -join '|')) -or
    $binding.prior_absence_reconstruction -cne 'ten validated post-cleanup terminal ERROR_NOT_FOUND targets plus fake-dispatch preflight ERROR_NOT_FOUND with no later reachable assignment or native call' -or
    $binding.required_branch -cne 'codex/m1-s6') {
    throw '4936dcef recovery failure/absence binding differs.'
}
$zero = '0000000000000000000000000000000000000000'
if (($m.status -ceq 'draft-binding-pending') -ne
    ($binding.close_ready_recovery_commit -ceq $zero)) {
    throw '4936dcef recovery status/close-ready binding differs.'
}
if ($m.status -ceq 'ready-for-owner-acceptance') {
    $head = (& git -C $root rev-parse HEAD).Trim()
    & git -C $root cat-file -e "$($binding.close_ready_recovery_commit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { throw '4936dcef recovery close-ready commit does not exist.' }
    & git -C $root merge-base --is-ancestor $binding.close_ready_recovery_commit $head
    if ($LASTEXITCODE -ne 0) { throw '4936dcef recovery close-ready commit is not an ancestor.' }
}
if ($m.disposable_namespace.namespace_id -cne 'm1-s6-wp4-native-4936dcef-a0f4-4302-9899-0afd99b19799' -or
    $m.disposable_namespace.reuse -cne 'cleanup-only; never requalification; terminal after this recovery attempt' -or
    ($m.native_boundary.allowed_calls -join '|') -cne 'CredReadW|CredDeleteW|CredFree' -or
    ($m.native_boundary.forbidden -join '|') -cne 'CredWriteW|CredEnumerateW|any prefix/arbitrary target|any alternate store' -or
    $m.native_boundary.fallback -cne 'none' -or $m.native_boundary.ui -cne 'none' -or
    $m.native_boundary.provider -cne 'none') {
    throw '4936dcef recovery namespace/native boundary differs.'
}
if ([int]$m.limits.wall_clock_seconds -ne 120 -or [int]$m.limits.targets -ne 1 -or
    [int]$m.limits.CredWriteW -ne 0 -or [int]$m.limits.CredReadW -ne 3 -or
    [int]$m.limits.CredDeleteW -ne 1 -or [int]$m.limits.CredFree -ne 1 -or
    [int]$m.limits.total_native_calls -ne 5 -or [int]$m.limits.attempts -ne 1) {
    throw '4936dcef recovery finite limits differ.'
}
$prepared = [DateTimeOffset]::Parse([string]$m.prepared_at_utc)
$expires = [DateTimeOffset]::Parse([string]$m.expires_at_utc)
if ($expires -le $prepared -or ($expires - $prepared) -gt [TimeSpan]::FromHours(24) -or
    $expires -le [DateTimeOffset]::UtcNow) { throw '4936dcef recovery expiry is invalid.' }
$expectedCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNativeRecovery -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.4936dcef.v1.json -OutputRoot artifacts/m1-slice6/wp4-native-recovery-dd412ecc'
if ($m.execution_command -cne $expectedCommand) { throw '4936dcef recovery command differs.' }

$priorEvidencePath = Join-Path $root 'artifacts/m1-slice6/wp4-native-4936dcef/credential-native-cleanup-ambiguity.v3.json'
$priorLockPath = Join-Path $root 'artifacts/m1-slice6/wp4-native-authority-locks/16d19410cd200caee29da362c474805929cc4c65651685173d39838849e27421.json'
if ((Get-FileHash $priorEvidencePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$binding.terminal_evidence_sha256 -or
    (Get-FileHash $priorLockPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$binding.consumed_lock_sha256) {
    throw '4936dcef recovery actual prior artifact/lock bytes differ.'
}
$e = Get-Content $priorEvidencePath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$lock = Get-Content $priorLockPath -Raw | ConvertFrom-Json -Depth 20 -DateKind String
if ($lock.manifest_id -cne [string]$binding.failed_manifest_id -or
    $lock.manifest_sha256 -cne [string]$binding.failed_manifest_sha256 -or
    $lock.execution_head_commit -cne [string]$binding.failed_execution_head_commit -or
    $lock.disposition -cne 'consumed-before-native-launch-never-delete-or-reuse' -or
    $e.schema -cne 'infinium.m1-s6.wp4.credential-native-cleanup-ambiguity/v3' -or
    $e.status -cne 'failed-cleanup-ambiguous' -or
    $e.manifest_id -cne [string]$binding.failed_manifest_id -or
    $e.manifest_sha256 -cne [string]$binding.failed_manifest_sha256 -or
    $e.assignment_id -cne 'wp4-v2/backup-restore-reauthentication/cleanup-successor' -or
    $e.reason -cne 'cleanup-phase-failed' -or
    $e.prior_primary_failure.failure_type -cne 'CredentialNativeHelperEvidenceAmbiguityException' -or
    $e.terminal_failure.failure_type -cne 'InvalidDataException' -or
    [bool]$e.cleanup_confirmed -or [bool]$e.whole_namespace_absence_confirmed -or
    -not [bool]$e.namespace_blocked -or [int]$e.later_native_calls -ne 0) {
    throw '4936dcef recovery prior terminal state differs.'
}
$phases = @($e.evidence.scenarios.phases)
$trace = @($phases.process.native_call_trace)
$allFingerprints = @($prior + $derived)
$allowedOps = @('CredWriteW', 'CredReadW', 'CredDeleteW', 'CredFree')
$sequenceFailures = 0
foreach ($phase in $phases) {
    $phaseTrace = @($phase.process.native_call_trace)
    for ($index = 0; $index -lt $phaseTrace.Count; $index++) {
        if ([int64]$phaseTrace[$index].sequence -ne $index + 1) { $sequenceFailures++ }
    }
}
if ($trace.Count -ne 92 -or
    @($trace | Where-Object operation -eq 'CredWriteW').Count -ne 7 -or
    @($trace | Where-Object operation -eq 'CredReadW').Count -ne 60 -or
    @($trace | Where-Object operation -eq 'CredDeleteW').Count -ne 6 -or
    @($trace | Where-Object operation -eq 'CredFree').Count -ne 19 -or
    @($trace | Where-Object operation -notin $allowedOps).Count -ne 0 -or
    @($trace | Where-Object target_fingerprint_sha256 -notin $allFingerprints).Count -ne 0 -or
    $sequenceFailures -ne 0) {
    throw '4936dcef recovery prior trace/count/target reconstruction differs.'
}
$allocations = @($trace | Where-Object { $_.operation -ceq 'CredReadW' -and $_.result -ceq 'success' })
$frees = @($trace | Where-Object operation -eq 'CredFree')
if ($allocations.Count -ne 19 -or $frees.Count -ne 19) {
    throw '4936dcef recovery prior allocation/free count differs.'
}
foreach ($allocation in $allocations) {
    $pairs = @($frees | Where-Object {
        [string]$_.paired_allocation_id -ceq [string]$allocation.allocation_id -and
        $_.target_fingerprint_sha256 -ceq $allocation.target_fingerprint_sha256 -and
        $_.scenario -ceq $allocation.scenario })
    if ($pairs.Count -ne 1) { throw '4936dcef recovery prior allocation/free pairing differs.' }
}
$lastByTarget = @{}
foreach ($item in $trace) { $lastByTarget[[string]$item.target_fingerprint_sha256] = $item }
foreach ($fingerprint in $prior) {
    if (-not $lastByTarget.ContainsKey($fingerprint) -or
        $lastByTarget[$fingerprint].operation -cne 'CredReadW' -or
        $lastByTarget[$fingerprint].result -cne 'ERROR_NOT_FOUND') {
        throw '4936dcef recovery prior exact absence reconstruction differs.'
    }
}
$backupPhases = @($e.evidence.scenarios | Where-Object scenario_id -eq 'backup-restore-reauthentication').phases
$fakePhases = @($e.evidence.scenarios | Where-Object scenario_id -eq 'fake-provider-dispatch').phases
if (($backupPhases.phase_id -join '|') -cne
        'preflight-old|preflight-new|backup-active|cleanup-restored-predecessor' -or
    $fakePhases.Count -ne 1 -or $fakePhases[0].phase_id -cne 'preflight' -or
    @($fakePhases[0].process.native_call_trace).Count -ne 1 -or
    $fakePhases[0].process.native_call_trace[0].operation -cne 'CredReadW' -or
    $fakePhases[0].process.native_call_trace[0].result -cne 'ERROR_NOT_FOUND' -or
    [int]$e.validated_native_call_counts.total -ne 92 -or
    $null -ne $e.rejected_phase_native_call_counts -or
    [int]$e.canary_facts.secret_matches -ne 0 -or [int]$e.canary_facts.raw_target_matches -ne 0 -or
    -not [bool]$e.containment_facts.process_trees_terminated -or
    [int]$e.containment_facts.process_tree_survivor_count -ne 0) {
    throw '4936dcef recovery closed-runner chronology/canary/containment reconstruction differs.'
}
[pscustomobject]@{
    status = if ($m.status -eq 'draft-binding-pending') { 'draft' } else { 'ready' }
    manifest_id = $m.manifest_id
    manifest_sha256 = $sha
    recovery_target_count = 1
    prior_exact_absence_count = 11
    combined_namespace_target_count = 12
    execution_authorized = $false
    native_operations = 0
    network_operations = 0
    provider_operations = 0
} | ConvertTo-Json
