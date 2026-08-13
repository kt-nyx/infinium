[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$ManifestSha256,
    [Parameter(Mandatory = $true)][string]$ManifestId,
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [Parameter(Mandatory = $true)][string]$EvidenceSha256,
    [Parameter(Mandatory = $true)][string]$AuthorityLockPath,
    [Parameter(Mandatory = $true)][string]$AuthorityLockSha256,
    [Parameter(Mandatory = $true)][string]$ReceiptPath,
    [switch]$TestOnlyPaths)

if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -File $PSCommandPath @PSBoundParameters
    exit $LASTEXITCODE
}
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

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
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
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
    if ($Value -is [IFormattable]) { return $Value.ToString($null, [Globalization.CultureInfo]::InvariantCulture) }
    throw "Canonical receipt serialization does not support type $($Value.GetType().FullName)."
}

$manifest = Resolve-InputPath $ManifestPath
$evidence = Resolve-InputPath $EvidencePath
$lock = Resolve-InputPath $AuthorityLockPath
$receipt = Resolve-InputPath $ReceiptPath
if (-not $TestOnlyPaths) {
    $expectedManifest = [IO.Path]::GetFullPath((Join-Path $root 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.v1.json'))
    $expectedEvidence = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-89baee92/credential-native-recovery-evidence.v1.json'))
    $expectedLock = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-locks/2ff2faef5257f32d5f311e370148d92b5b58c36a84611d08b2c0489fb7a899ce.json'))
    $expectedReceipt = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-89baee92/CredentialNativeRecovery.json'))
    if (-not [string]::Equals($manifest, $expectedManifest, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($evidence, $expectedEvidence, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($lock, $expectedLock, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($receipt, $expectedReceipt, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Production recovery receipt reconstruction paths are not exact.'
    }
}
foreach ($item in @($ManifestSha256, $EvidenceSha256, $AuthorityLockSha256)) {
    if ($item -cnotmatch '^[0-9a-f]{64}$') { throw 'A reconstruction SHA-256 is not canonical lowercase hex.' }
}
if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ManifestSha256 -or
    (Get-FileHash -LiteralPath $evidence -Algorithm SHA256).Hash.ToLowerInvariant() -cne $EvidenceSha256 -or
    (Get-FileHash -LiteralPath $lock -Algorithm SHA256).Hash.ToLowerInvariant() -cne $AuthorityLockSha256) {
    throw 'Immutable recovery reconstruction input hash differs.'
}
& (Join-Path $root 'eng/validate-m1-slice6-wp4-recovery-evidence.ps1') `
    -ManifestPath $manifest -ManifestSha256 $ManifestSha256 -ManifestId $ManifestId -EvidencePath $evidence
$lockValue = Get-Content -LiteralPath $lock -Raw | ConvertFrom-Json -Depth 20
if ($lockValue.manifest_id -cne $ManifestId -or
    $lockValue.manifest_sha256 -cne $ManifestSha256 -or
    $lockValue.disposition -cne 'consumed-never-reuse') {
    throw 'Recovery authority lock differs before receipt reconstruction.'
}
$ev = Get-Content -LiteralPath $evidence -Raw | ConvertFrom-Json -Depth 100
$receiptValue = [ordered]@{
    gate = 'CredentialNativeRecovery'
    status = 'passed'
    network_permitted = $false
    credential_access_permitted = $true
    evidence = [ordered]@{
        authority_lock_sha256 = $AuthorityLockSha256
        billable_operations = 0
        dns_operations = 0
        evidence_sha256 = $EvidenceSha256
        manifest_id = $ManifestId
        manifest_sha256 = $ManifestSha256
        native_call_counts = $ev.native_call_counts
        namespace_disposition = 'cleanup-confirmed-absent-consumed-never-reuse'
        network_operations = 0
        provider_operations = 0
        receipt_origin = 'post-effect-reconstruction-from-immutable-evidence-no-native-retry'
        target_absence_count = @($ev.target_absence).Count
    }
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($receipt)) | Out-Null
$receiptBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-CanonicalJsonValue $receiptValue) + "`n")
$stream = [IO.File]::Open($receipt, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $stream.Write($receiptBytes)
    $stream.Flush($true)
} finally { $stream.Dispose() }
[pscustomobject]@{
    status = 'reconstructed-without-native-operation'
    receipt_sha256 = (Get-FileHash -LiteralPath $receipt -Algorithm SHA256).Hash.ToLowerInvariant()
    evidence_sha256 = $EvidenceSha256
    authority_lock_sha256 = $AuthorityLockSha256
    native_operations = 0
} | ConvertTo-Json -Compress
