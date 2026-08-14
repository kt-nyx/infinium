[CmdletBinding()]
param(
    [string]$ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.076b981a.v1.json',
    [switch]$TestOnlyManifestPath,
    [switch]$PostEffect)
if ($PSVersionTable.PSEdition -ne 'Core') {
    $arguments = @('-NoProfile', '-File', $PSCommandPath, '-ManifestPath', $ManifestPath)
    if ($TestOnlyManifestPath) { $arguments += '-TestOnlyManifestPath' }
    if ($PostEffect) { $arguments += '-PostEffect' }
    & (Get-Command pwsh.exe).Source @arguments
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$relativeManifest = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.076b981a.v1.json'
$expectedPath = [IO.Path]::GetFullPath((Join-Path $root $relativeManifest))
$path = if ([IO.Path]::IsPathFullyQualified($ManifestPath)) {
    [IO.Path]::GetFullPath($ManifestPath)
} else { [IO.Path]::GetFullPath((Join-Path $root $ManifestPath)) }
if (-not [string]::Equals($path, $expectedPath, [StringComparison]::OrdinalIgnoreCase) -and
    -not ($TestOnlyManifestPath -and $path.StartsWith(
        [IO.Path]::GetFullPath((Join-Path $root 'artifacts/test-temp')),
        [StringComparison]::OrdinalIgnoreCase))) {
    throw '076b981a recovery validator accepts only the exact tracked path.'
}
$schema = Join-Path $root 'contracts/repository/wp4-credential-native-recovery.076b981a.v1.schema.json'
if (-not (Test-Json -LiteralPath $path -SchemaFile $schema -ErrorAction Stop)) {
    throw '076b981a recovery manifest schema failed.'
}
$bytes = [IO.File]::ReadAllBytes($path)
$sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$m = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100 -DateKind String
$binding = $m.binding
$expectedAliases = @(
    'interactive-primary','interactive-cancel','size-valid','size-oversize','unavailable-store',
    'replacement-old','replacement-new','revoke-delete','crash-restart','backup-old','backup-new','fake-dispatch')
$targets = @($m.disposable_namespace.targets)
if ($targets.Count -ne 12 -or (($targets.alias -join '|') -cne ($expectedAliases -join '|'))) {
    throw '076b981a recovery exact ordered target inventory differs.'
}
for ($index = 0; $index -lt $targets.Count; $index++) {
    $target = $targets[$index]
    $raw = "Infinium:$($target.access_profile_id):$($target.generation_id)"
    $derived = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes($raw))).ToLowerInvariant()
    if ($target.target_fingerprint_sha256 -cne $derived) {
        throw "076b981a recovery target fingerprint differs at ordered index $index."
    }
}
if (@($targets.target_fingerprint_sha256 | Sort-Object -Unique).Count -ne 12 -or
    @($targets.access_profile_id | Sort-Object -Unique).Count -ne 10) {
    throw '076b981a recovery target slots/fingerprints are not the exact unique inventory.'
}
$zero = '0000000000000000000000000000000000000000'
if (($m.status -ceq 'draft-binding-pending') -ne
    ($binding.close_ready_recovery_commit -ceq $zero)) {
    throw '076b981a recovery status/close-ready binding differs.'
}
if ($m.status -ceq 'ready-for-owner-acceptance') {
    $head = (& git -C $root rev-parse HEAD).Trim()
    & git -C $root cat-file -e "$($binding.close_ready_recovery_commit)^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { throw '076b981a recovery close-ready commit does not exist.' }
    & git -C $root merge-base --is-ancestor $binding.close_ready_recovery_commit $head
    if ($LASTEXITCODE -ne 0) { throw '076b981a recovery close-ready commit is not an ancestor.' }
}
$prepared = [DateTimeOffset]::Parse([string]$m.prepared_at_utc)
$expires = [DateTimeOffset]::Parse([string]$m.expires_at_utc)
if ($expires -le $prepared -or ($expires - $prepared) -gt [TimeSpan]::FromHours(24) -or
    (-not $PostEffect -and $expires -le [DateTimeOffset]::UtcNow)) {
    throw '076b981a recovery expiry is invalid.'
}

$priorRoot = Join-Path $root 'artifacts/m1-slice6/wp4-native-076b981a'
$failedManifestPath = Join-Path $root 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-authorization.v2.json'
$priorLockPath = Join-Path $root 'artifacts/m1-slice6/wp4-native-authority-locks/25c657c7241731d5f91d9df3f49dd2cc0c3241eb5c6a470a3817400552d9d3c8.json'
$stderrPath = Join-Path $priorRoot 'coordinator-stderr.txt'
$summaryPath = Join-Path $priorRoot 'credential-native-summary.txt'
$backupPath = Join-Path $priorRoot 'native-backup-metadata.v2.json'
foreach ($required in @($failedManifestPath, $priorLockPath, $stderrPath, $summaryPath, $backupPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw '076b981a recovery requires all exact prior immutable artifacts.'
    }
}
if ((Get-FileHash $failedManifestPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$binding.failed_manifest_sha256 -or
    (Get-FileHash $priorLockPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$binding.consumed_lock_sha256 -or
    (Get-FileHash $stderrPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$binding.terminal_artifact_sha256 -or
    (Get-FileHash $summaryPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$binding.success_summary_sha256 -or
    (Get-FileHash $backupPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$binding.backup_metadata_sha256) {
    throw '076b981a recovery prior artifact hash differs.'
}
foreach ($ancestor in @([string]$binding.failed_execution_head_commit, [string]$binding.failure_record_commit)) {
    & git -C $root merge-base --is-ancestor $ancestor HEAD
    if ($LASTEXITCODE -ne 0) { throw '076b981a recovery required terminal ancestry differs.' }
}
$lock = Get-Content -LiteralPath $priorLockPath -Raw | ConvertFrom-Json -Depth 20 -DateKind String
if ($lock.manifest_id -cne [string]$binding.failed_manifest_id -or
    $lock.manifest_sha256 -cne [string]$binding.failed_manifest_sha256 -or
    $lock.execution_head_commit -cne [string]$binding.failed_execution_head_commit -or
    $lock.disposition -cne 'consumed-before-native-launch-never-delete-or-reuse') {
    throw '076b981a recovery consumed qualification lock differs.'
}
$stderr = Get-Content -LiteralPath $stderrPath -Raw
$summary = Get-Content -LiteralPath $summaryPath -Raw
$backup = Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json -Depth 50 -DateKind String
if ($stderr -cne "WP4 v2 coordinator supervisor failed with typed non-secret error: IOException`r`n" -and
    $stderr -cne "WP4 v2 coordinator supervisor failed with typed non-secret error: IOException`n") {
    throw '076b981a recovery terminal typed stderr differs.'
}
if (-not $summary.Contains('WP4 v2 passed', [StringComparison]::Ordinal) -or
    -not $summary.Contains('scenarios=9 targets=12 cleanup=confirmed-absent', [StringComparison]::Ordinal) -or
    $backup.status -cne 'passed' -or -not [bool]$backup.same_generation_rejected -or
    $backup.new_generation_id -cne 'g002' -or -not [bool]$backup.secret_absent -or
    -not [bool]$backup.raw_target_absent) {
    throw '076b981a recovery retained success/backup artifact semantics differ.'
}

function Get-Inventory([IO.FileInfo[]]$Files) {
    $lines = foreach ($file in $Files) {
        $relative = [IO.Path]::GetRelativePath($priorRoot, $file.FullName).Replace('\', '/')
        $fileSha = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$relative|$($file.Length)|$fileSha"
    }
    $canonical = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n") + "`n")
    [pscustomobject]@{
        Count = $Files.Count
        TotalBytes = [int64](($Files | Measure-Object -Property Length -Sum).Sum)
        Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($canonical)).ToLowerInvariant()
    }
}
$files = @(Get-ChildItem -LiteralPath $priorRoot -File -Recurse |
    Sort-Object { [IO.Path]::GetRelativePath($priorRoot, $_.FullName).Replace('\', '/') })
$receipts = @($files | Where-Object Name -CEQ 'helper-receipt.v2.pb')
$inventory = Get-Inventory $files
$receiptInventory = Get-Inventory $receipts
if ($inventory.Count -ne [int]$binding.output_file_count -or
    $inventory.TotalBytes -ne [int64]$binding.output_total_bytes -or
    $inventory.Sha256 -cne [string]$binding.output_inventory_sha256 -or
    $receiptInventory.Count -ne [int]$binding.helper_receipt_count -or
    $receiptInventory.Sha256 -cne [string]$binding.helper_receipt_inventory_sha256) {
    throw '076b981a recovery retained output/receipt inventory differs.'
}
$manual = $binding.manual_receipt_sha256
$manualPaths = [ordered]@{
    interactive_submit = 'state-interactive-entry-submit/staging/interactive-entry-submit-submit-attempt/helper-receipt.v2.pb'
    interactive_cancel = 'state-interactive-entry-cancel/staging/interactive-entry-cancel-cancel-attempt/helper-receipt.v2.pb'
    restored_g002_submit = 'backup-restored/staging/backup-restore-reauthentication-restored-new-generation-attempt/helper-receipt.v2.pb'
}
foreach ($entry in $manualPaths.GetEnumerator()) {
    $manualPath = Join-Path $priorRoot $entry.Value
    if ((Get-FileHash -LiteralPath $manualPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        [string]$manual.($entry.Key)) {
        throw '076b981a recovery retained manual receipt differs.'
    }
}
foreach ($absent in $binding.required_absent_artifacts) {
    if (Test-Path -LiteralPath (Join-Path $priorRoot ([string]$absent))) {
        throw '076b981a recovery requires the exact final-evidence absence inventory.'
    }
}
if ([int]$binding.prior_exact_absence_count -ne 0 -or
    @($binding.prior_exact_absence_fingerprints).Count -ne 0) {
    throw '076b981a recovery may not credit unretained prior per-target absence.'
}
$record = Get-Content -LiteralPath (Join-Path $root 'docs/plans/milestones/m1/slices/s6/record.md') -Raw
$executionMarker = "WP4_V2_NATIVE_EXECUTED manifest_id=$($binding.failed_manifest_id) sha256=$($binding.failed_manifest_sha256) execution_head_commit=$($binding.failed_execution_head_commit)"
if (@($record -split "`r?`n" | Where-Object { $_.StartsWith($executionMarker, [StringComparison]::Ordinal) }).Count -ne 1 -or
    -not $record.Contains('post-effect audit correction', [StringComparison]::Ordinal) -or
    -not $record.Contains('least conservative contract recovery scope is all 12 targets', [StringComparison]::Ordinal)) {
    throw '076b981a recovery terminal documentation lineage differs.'
}
$outputRoot = Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-040817c8'
$lockName = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes([string]$m.manifest_id))).ToLowerInvariant() + '.json'
$recoveryLock = Join-Path (Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-locks') $lockName
if (-not $PostEffect -and ((Test-Path -LiteralPath $outputRoot) -or
    (Test-Path -LiteralPath $recoveryLock))) {
    throw '076b981a recovery requires fresh absent output and one-shot lock.'
}
[pscustomobject]@{
    status = 'ready-for-review'
    manifest_sha256 = $sha
    target_count = 12
    prior_exact_absence_count = 0
    retained_output_file_count = $inventory.Count
    retained_helper_receipt_count = $receiptInventory.Count
    execution_authorized = $false
} | ConvertTo-Json -Compress
