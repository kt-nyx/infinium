[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManifestSha256,
    [Parameter(Mandatory = $true)][string]$EvidenceSha256,
    [Parameter(Mandatory = $true)][string]$AuthorityLockSha256,
    [string]$ManifestPath = 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.076b981a.v1.json',
    [string]$EvidencePath = 'artifacts/m1-slice6/wp4-native-recovery-040817c8/credential-native-recovery-evidence.v1.json',
    [string]$AuthorityLockPath = 'artifacts/m1-slice6/wp4-native-recovery-locks/e6a97b4f667a5487b314e4de2ae029601348455127c5d33732dd9e3ec63a1724.json',
    [string]$ReceiptPath = 'artifacts/m1-slice6/wp4-native-recovery-040817c8/CredentialNativeRecovery.reconstructed.json',
    [switch]$TestOnlyPaths)
if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -File $PSCommandPath @PSBoundParameters
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestId = 'infinium.m1-s6.wp4.credential-native-recovery/040817c8-0a87-480a-915c-71dc2fe54da3'

function Resolve-InputPath([string]$Value) {
    if ([IO.Path]::IsPathFullyQualified($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $root $Value))
}
function ConvertTo-CanonicalJsonValue([object]$Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string]) {
        $escaped = $Value.Replace('\', '\\').Replace('"', '\"')
        $escaped = $escaped.Replace("`b", '\b').Replace("`f", '\f').Replace("`n", '\n')
        $escaped = $escaped.Replace("`r", '\r').Replace("`t", '\t')
        return '"' + $escaped + '"'
    }
    if ($Value -is [bool]) { return $(if ($Value) { 'true' } else { 'false' }) }
    if ($Value -is [Management.Automation.PSCustomObject]) {
        $properties = [ordered]@{}
        foreach ($property in $Value.PSObject.Properties) { $properties[$property.Name] = $property.Value }
        return ConvertTo-CanonicalJsonValue $properties
    }
    if ($Value -is [Collections.IDictionary]) {
        [string[]]$keys = @($Value.Keys | ForEach-Object { [string]$_ })
        [Array]::Sort($keys, [StringComparer]::Ordinal)
        $members = foreach ($key in $keys) {
            (ConvertTo-CanonicalJsonValue $key) + ':' + (ConvertTo-CanonicalJsonValue $Value[$key])
        }
        return '{' + ($members -join ',') + '}'
    }
    if ($Value -is [Collections.IEnumerable]) {
        $items = foreach ($item in $Value) { ConvertTo-CanonicalJsonValue $item }
        return '[' + ($items -join ',') + ']'
    }
    if ($Value -is [IFormattable]) {
        return $Value.ToString($null, [Globalization.CultureInfo]::InvariantCulture)
    }
    throw "Canonical receipt serialization does not support type $($Value.GetType().FullName)."
}

$manifest = Resolve-InputPath $ManifestPath
$evidence = Resolve-InputPath $EvidencePath
$lock = Resolve-InputPath $AuthorityLockPath
$receipt = Resolve-InputPath $ReceiptPath
if (-not $TestOnlyPaths) {
    $expected = @(
        (Resolve-InputPath 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.076b981a.v1.json')
        (Resolve-InputPath 'artifacts/m1-slice6/wp4-native-recovery-040817c8/credential-native-recovery-evidence.v1.json')
        (Resolve-InputPath 'artifacts/m1-slice6/wp4-native-recovery-locks/e6a97b4f667a5487b314e4de2ae029601348455127c5d33732dd9e3ec63a1724.json')
        (Resolve-InputPath 'artifacts/m1-slice6/wp4-native-recovery-040817c8/CredentialNativeRecovery.reconstructed.json'))
    $actual = @($manifest, $evidence, $lock, $receipt)
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if (-not [string]::Equals($actual[$index], $expected[$index], [StringComparison]::OrdinalIgnoreCase)) {
            throw '076b981a production recovery receipt paths are not exact.'
        }
    }
}
foreach ($item in @($ManifestSha256, $EvidenceSha256, $AuthorityLockSha256)) {
    if ($item -cnotmatch '^[0-9a-f]{64}$') { throw 'A reconstruction SHA-256 is not canonical lowercase hex.' }
}
if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ManifestSha256 -or
    (Get-FileHash -LiteralPath $evidence -Algorithm SHA256).Hash.ToLowerInvariant() -cne $EvidenceSha256 -or
    (Get-FileHash -LiteralPath $lock -Algorithm SHA256).Hash.ToLowerInvariant() -cne $AuthorityLockSha256) {
    throw 'Immutable 076b981a recovery reconstruction input hash differs.'
}
& (Join-Path $root 'eng/validate-m1-slice6-wp4-recovery-076b981a.ps1') `
    -ManifestPath $manifest -PostEffect
& (Join-Path $root 'eng/validate-m1-slice6-wp4-recovery-evidence.ps1') `
    -ManifestPath $manifest -ManifestSha256 $ManifestSha256 -ManifestId $manifestId -EvidencePath $evidence
$manifestValue = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json -Depth 100 -DateKind String
$lockValue = Get-Content -LiteralPath $lock -Raw | ConvertFrom-Json -Depth 20 -DateKind String
$ev = Get-Content -LiteralPath $evidence -Raw | ConvertFrom-Json -Depth 100 -DateKind String
if ($lockValue.manifest_id -cne $manifestId -or
    $lockValue.manifest_sha256 -cne $ManifestSha256 -or
    $lockValue.disposition -cne 'consumed-never-reuse') {
    throw '076b981a recovery authority lock differs before reconstruction.'
}

$rawTargets = @($manifestValue.disposable_namespace.targets | ForEach-Object {
    "Infinium:$($_.access_profile_id):$($_.generation_id)"
})
foreach ($surface in @($evidence, $lock)) {
    $surfaceBytes = [IO.File]::ReadAllBytes($surface)
    foreach ($rawTarget in $rawTargets) {
        foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode)) {
            if ($encoding.GetString($surfaceBytes).Contains($rawTarget, [StringComparison]::Ordinal)) {
                throw '076b981a recovery reconstruction input retained a raw target.'
            }
        }
    }
}

$receiptValue = [ordered]@{
    gate = 'CredentialNativeRecovery'
    status = 'passed'
    network_permitted = $false
    credential_access_permitted = $true
    evidence = [ordered]@{
        authority_lock_sha256 = $AuthorityLockSha256
        billable_operations = 0
        combined_namespace_target_absence_count = 12
        dns_operations = 0
        evidence_sha256 = $EvidenceSha256
        manifest_id = $manifestId
        manifest_sha256 = $ManifestSha256
        native_call_counts = $ev.native_call_counts
        namespace_disposition = 'cleanup-confirmed-absent-never-reuse'
        network_operations = 0
        prior_authority_lock_sha256 = $manifestValue.binding.consumed_lock_sha256
        prior_backup_metadata_sha256 = $manifestValue.binding.backup_metadata_sha256
        prior_helper_receipt_inventory_sha256 = $manifestValue.binding.helper_receipt_inventory_sha256
        prior_output_inventory_sha256 = $manifestValue.binding.output_inventory_sha256
        prior_success_summary_sha256 = $manifestValue.binding.success_summary_sha256
        prior_terminal_artifact_sha256 = $manifestValue.binding.terminal_artifact_sha256
        provider_operations = 0
        receipt_origin = 'post-effect-reconstruction-from-immutable-v2-evidence-no-native-retry'
        target_absence_count = @($ev.target_absence).Count
    }
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($receipt)) | Out-Null
$receiptBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-CanonicalJsonValue $receiptValue) + "`n")
$stream = [IO.File]::Open($receipt, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $stream.Write($receiptBytes); $stream.Flush($true) } finally { $stream.Dispose() }

foreach ($surface in @($receipt)) {
    $surfaceBytes = [IO.File]::ReadAllBytes($surface)
    foreach ($rawTarget in $rawTargets) {
        foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode)) {
            if ($encoding.GetString($surfaceBytes).Contains($rawTarget, [StringComparison]::Ordinal)) {
                throw '076b981a recovery reconstruction retained a raw target.'
            }
        }
    }
}
[pscustomobject]@{
    status = 'reconstructed-without-native-operation'
    receipt_sha256 = (Get-FileHash -LiteralPath $receipt -Algorithm SHA256).Hash.ToLowerInvariant()
    evidence_sha256 = $EvidenceSha256
    authority_lock_sha256 = $AuthorityLockSha256
    native_operations = 0
} | ConvertTo-Json -Compress
