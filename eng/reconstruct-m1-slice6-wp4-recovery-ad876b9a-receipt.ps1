[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$ManifestSha256,
    [Parameter(Mandatory = $true)][string]$ManifestId,
    [Parameter(Mandatory = $true)][string]$EvidencePath,
    [Parameter(Mandatory = $true)][string]$EvidenceSha256,
    [Parameter(Mandatory = $true)][string]$AuthorityLockPath,
    [Parameter(Mandatory = $true)][string]$AuthorityLockSha256,
    [Parameter(Mandatory = $true)][string]$PriorEvidencePath,
    [Parameter(Mandatory = $true)][string]$PriorEvidenceSha256,
    [Parameter(Mandatory = $true)][string]$PriorAuthorityLockPath,
    [Parameter(Mandatory = $true)][string]$PriorAuthorityLockSha256,
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
        $escaped=$Value.Replace('\','\\').Replace('"','\"').Replace("`n",'\n').Replace("`r",'\r').Replace("`t",'\t')
        return '"'+$escaped+'"'
    }
    if ($Value -is [bool]) { return $(if($Value){'true'}else{'false'}) }
    if ($Value -is [Management.Automation.PSCustomObject]) {
        $properties=[ordered]@{};foreach($property in $Value.PSObject.Properties){$properties[$property.Name]=$property.Value}
        return ConvertTo-CanonicalJsonValue $properties
    }
    if ($Value -is [Collections.IDictionary]) {
        [string[]]$keys=@($Value.Keys|%{[string]$_});[Array]::Sort($keys,[StringComparer]::Ordinal)
        return '{'+(($keys|%{(ConvertTo-CanonicalJsonValue $_)+':'+(ConvertTo-CanonicalJsonValue $Value[$_])})-join',')+'}'
    }
    if ($Value -is [Collections.IEnumerable]) { return '['+(($Value|%{ConvertTo-CanonicalJsonValue $_})-join',')+']' }
    if ($Value -is [IFormattable]) { return $Value.ToString($null,[Globalization.CultureInfo]::InvariantCulture) }
    throw 'Unsupported canonical receipt value.'
}
$manifest=Resolve-InputPath $ManifestPath;$evidence=Resolve-InputPath $EvidencePath
$lock=Resolve-InputPath $AuthorityLockPath;$priorEvidencePath=Resolve-InputPath $PriorEvidencePath
$priorLock=Resolve-InputPath $PriorAuthorityLockPath;$receipt=Resolve-InputPath $ReceiptPath
if(-not$TestOnlyPaths){
    $expected=@(
        [IO.Path]::GetFullPath((Join-Path $root 'docs/plans/milestones/m1/slices/s6/wp4-credential-native-recovery.ad876b9a.v1.json')),
        [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-df29a608/credential-native-recovery-evidence.v1.json')),
        [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-locks/d7b38deff0756330dad3fd851a913fd8f61697d7ef86cb7ab254eb39b615e609.json')),
        [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-ad876b9a/credential-native-primary-failure.v2.json')),
        [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-authority-locks/19c5362e4a5bff02b1588b1962b36933be8930256aa2938692681f57ec19ba0c.json')),
        [IO.Path]::GetFullPath((Join-Path $root 'artifacts/m1-slice6/wp4-native-recovery-df29a608/CredentialNativeRecovery.json')))
    $actual=@($manifest,$evidence,$lock,$priorEvidencePath,$priorLock,$receipt)
    for($i=0;$i-lt$expected.Count;$i++){if(-not[string]::Equals($actual[$i],$expected[$i],[StringComparison]::OrdinalIgnoreCase)){throw 'Current recovery reconstruction path differs.'}}
}
foreach($sha in @($ManifestSha256,$EvidenceSha256,$AuthorityLockSha256,$PriorEvidenceSha256,$PriorAuthorityLockSha256)){
    if($sha-cnotmatch'^[0-9a-f]{64}$'){throw 'A reconstruction SHA-256 is not canonical.'}
}
$hashes=@(
    (Get-FileHash $manifest -Algorithm SHA256).Hash.ToLowerInvariant(),
    (Get-FileHash $evidence -Algorithm SHA256).Hash.ToLowerInvariant(),
    (Get-FileHash $lock -Algorithm SHA256).Hash.ToLowerInvariant(),
    (Get-FileHash $priorEvidencePath -Algorithm SHA256).Hash.ToLowerInvariant(),
    (Get-FileHash $priorLock -Algorithm SHA256).Hash.ToLowerInvariant())
$expectedHashes=@($ManifestSha256,$EvidenceSha256,$AuthorityLockSha256,$PriorEvidenceSha256,$PriorAuthorityLockSha256)
for($i=0;$i-lt$hashes.Count;$i++){if($hashes[$i]-cne$expectedHashes[$i]){throw 'Immutable reconstruction input hash differs.'}}
if($ManifestId-cne'infinium.m1-s6.wp4.credential-native-recovery/df29a608-cc46-4151-bb0b-1a03acb1cdff'-or
    (-not$TestOnlyPaths-and($PriorEvidenceSha256-cne'cfaee3940cd780a5bcfbcbcf387124d7f7385b01a07f8f0f6fbe4439593a21e6'-or
        $PriorAuthorityLockSha256-cne'b47e0262937f86174ae1b790f4951fbf6fe6621d1f3a25c938990143514950b8'))){
    throw 'Current recovery reconstruction identity differs.'
}
& (Join-Path $root 'eng/validate-m1-slice6-wp4-recovery-evidence.ps1') -ManifestPath $manifest `
    -ManifestSha256 $ManifestSha256 -ManifestId $ManifestId -EvidencePath $evidence
$lockValue=Get-Content $lock -Raw|ConvertFrom-Json -Depth 20
if($lockValue.manifest_id-cne$ManifestId-or$lockValue.manifest_sha256-cne$ManifestSha256-or$lockValue.disposition-cne'consumed-never-reuse'){
    throw 'Current recovery authority lock differs.'
}
$prior=Get-Content $priorEvidencePath -Raw|ConvertFrom-Json -Depth 100
$expectedPriorFingerprints=@(
    'cf749639000f855451374b935af7cc66b3895856d77868a87d12a52bbcaa8fe7','ade2fbfd10c41f22382c11f58e5f23e89c92e43b6eabd686e24e5f3d3aa32096',
    '76fc18abd12bf7dfcc496602f82fe96e579912cac7875c0ddbeab18828401ac6','befecf6ffdf669836062df69f078f54f94b4bf81ca4dcdcd1d28a4463694a422',
    '2025f07cf9eff90bd87cb680be019eb11529a05f98a29459bcc7e72f8fc4b44f','43e9a481fec663f10eef5753b41074b201bcc06c3edb07174153525201521078',
    'd1999c5fca496d9cd417c5f686278564e8028ab8c5db9e91d81790c8aee7ce07','6190a95dc664166e75f57fc39d57ec1eba8643e7865ae73789589ac732f8bc5c',
    '598ae3c4a89d3ec7e72dfc11b6763120955a3fc946ea7425248f4e445558dea0','0a6d4ba3eed8c60a4048ca388178d2700ede8e1835b0ebca99ecb6ef50b6c051')
$priorFingerprints=@($prior.absence_target_fingerprints)
if($prior.status-cne'failed-primary-cleanup-confirmed'-or$prior.manifest_id-cne'infinium.m1-s6.wp4.credential-native-authorization/ad876b9a-9f45-4eb4-8d12-5970d76dd4ea'-or
    [int]$prior.later_native_calls-ne0-or-not[bool]$prior.cleanup_confirmed-or-not[bool]$prior.absence_confirmed-or
    [bool]$prior.whole_namespace_absence_confirmed-or$prior.namespace_disposition-cne'consumed-never-reuse'-or
    @($priorFingerprints|Sort-Object -Unique).Count-ne10-or
    (($priorFingerprints|Sort-Object)-join'|')-cne(($expectedPriorFingerprints|Sort-Object)-join'|')){throw 'Prior exact absence evidence differs.'}
$ev=Get-Content $evidence -Raw|ConvertFrom-Json -Depth 100
$value=[ordered]@{gate='CredentialNativeRecovery';status='passed';network_permitted=$false;credential_access_permitted=$true;evidence=[ordered]@{
    authority_lock_sha256=$AuthorityLockSha256;billable_operations=0;combined_namespace_target_absence_count=12;dns_operations=0
    evidence_sha256=$EvidenceSha256;manifest_id=$ManifestId;manifest_sha256=$ManifestSha256;native_call_counts=$ev.native_call_counts
    namespace_disposition='cleanup-confirmed-absent-consumed-never-reuse';network_operations=0;prior_authority_lock_sha256=$PriorAuthorityLockSha256
    prior_exact_absence_count=10;prior_terminal_evidence_sha256=$PriorEvidenceSha256;provider_operations=0
    receipt_origin='post-effect-reconstruction-from-immutable-evidence-no-native-retry';recovery_target_absence_count=2}}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($receipt))|Out-Null
$bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-CanonicalJsonValue $value)+"`n")
$stream=[IO.File]::Open($receipt,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try{$stream.Write($bytes);$stream.Flush($true)}finally{$stream.Dispose()}
[pscustomobject]@{status='reconstructed-without-native-operation';receipt_sha256=(Get-FileHash $receipt -Algorithm SHA256).Hash.ToLowerInvariant();native_operations=0}|ConvertTo-Json -Compress
