[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$ManifestSha256,
    [Parameter(Mandatory = $true)][string]$ManifestId,
    [Parameter(Mandatory = $true)][string]$EvidencePath)

if ($PSVersionTable.PSEdition -ne 'Core') {
    & (Get-Command pwsh.exe).Source -NoProfile -File $PSCommandPath @PSBoundParameters
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifest = Get-Content -LiteralPath $ManifestPath -Raw |
    ConvertFrom-Json -Depth 100 -DateKind String
$evidence = Get-Content -LiteralPath $EvidencePath -Raw |
    ConvertFrom-Json -Depth 100 -DateKind String

$isAd876Recovery =
    $manifest.schema_identity -ceq 'infinium.repository.wp4-credential-native-recovery/1.1.0' -and
    $manifest.manifest_id -ceq 'infinium.m1-s6.wp4.credential-native-recovery/df29a608-cc46-4151-bb0b-1a03acb1cdff'
$isE3Recovery =
    $manifest.schema_identity -ceq 'infinium.repository.wp4-credential-native-recovery/1.2.0' -and
    $manifest.manifest_id -ceq 'infinium.m1-s6.wp4.credential-native-recovery/8b7fc811-7cd2-4c2a-abe1-506bd7b06bf5'
$isE6Recovery =
    $manifest.schema_identity -ceq 'infinium.repository.wp4-credential-native-recovery/1.3.0' -and
    $manifest.manifest_id -ceq 'infinium.m1-s6.wp4.credential-native-recovery/6232bae5-f735-4db7-a74f-7ede9f67b752'
$is4936Recovery =
    $manifest.schema_identity -ceq 'infinium.repository.wp4-credential-native-recovery/1.4.0' -and
    $manifest.manifest_id -ceq 'infinium.m1-s6.wp4.credential-native-recovery/dd412ecc-3b2c-4628-8865-bc8574a357c7'
$is076bRecovery =
    $manifest.schema_identity -ceq 'infinium.repository.wp4-credential-native-recovery/1.5.0' -and
    $manifest.manifest_id -ceq 'infinium.m1-s6.wp4.credential-native-recovery/040817c8-0a87-480a-915c-71dc2fe54da3'
$isCurrentRecovery = $isAd876Recovery -or $isE3Recovery -or $isE6Recovery -or $is4936Recovery -or $is076bRecovery
$isLegacyRecovery =
    $manifest.schema_identity -ceq 'infinium.repository.wp4-credential-native-recovery/1.0.0' -and
    $manifest.manifest_id -ceq 'infinium.m1-s6.wp4.credential-native-recovery/89baee92-14d6-4f2b-a970-0fe6be15c54c'
$expectedEvidenceSchema = if ($is076bRecovery) {
    'infinium.m1-s6.wp4.credential-native-recovery-evidence/v2'
} else { 'infinium.m1-s6.wp4.credential-native-recovery-evidence/v1' }
if ($evidence.schema -cne $expectedEvidenceSchema -or
    $evidence.manifest_id -cne $ManifestId -or
    $evidence.manifest_sha256 -cne $ManifestSha256 -or
    $evidence.status -cne 'passed' -or
    (-not $isCurrentRecovery -and -not $isLegacyRecovery) -or
    [int]$evidence.network_operations -ne 0 -or
    [int]$evidence.dns_operations -ne 0 -or
    [int]$evidence.provider_operations -ne 0 -or
    [int]$evidence.billable_operations -ne 0) {
    throw 'Recovery evidence identity/effect oracle failed.'
}
if ($isCurrentRecovery) {
    if ($evidence.PSObject.Properties.Name -notcontains 'cleanup_ambiguity' -or
        $evidence.PSObject.Properties.Name -notcontains 'namespace_reuse_blocked' -or
        $evidence.PSObject.Properties.Name -notcontains 'namespace_disposition' -or
        [bool]$evidence.cleanup_ambiguity -or -not [bool]$evidence.namespace_reuse_blocked -or
        $evidence.namespace_disposition -cne 'cleanup-confirmed-absent-never-reuse' -or
        $evidence.prior_authority_lock_sha256 -cne [string]$manifest.binding.consumed_lock_sha256 -or
        [int]$evidence.prior_exact_absence_count -ne [int]$manifest.binding.prior_exact_absence_count -or
        [int]$evidence.combined_namespace_target_absence_count -ne
            ([int]$manifest.binding.prior_exact_absence_count + [int]$manifest.limits.targets)) {
        throw 'Current recovery terminal namespace evidence differs.'
    }
    if ($is076bRecovery) {
        $expectedV2Properties = @(
            'schema','status','manifest_id','manifest_sha256','target_absence','native_call_counts',
            'native_call_trace','cleanup_ambiguity','namespace_reuse_blocked','namespace_disposition',
            'prior_terminal_evidence_sha256','prior_terminal_artifact_kind','prior_terminal_artifact_sha256',
            'prior_success_summary_sha256','prior_backup_metadata_sha256',
            'prior_helper_receipt_inventory_sha256','prior_output_inventory_sha256',
            'prior_authority_lock_sha256','prior_exact_absence_count',
            'combined_namespace_target_absence_count','network_operations','dns_operations',
            'provider_operations','billable_operations')
        $actualV2Properties = @($evidence.PSObject.Properties.Name)
        if ($actualV2Properties.Count -ne $expectedV2Properties.Count -or
            @(Compare-Object $expectedV2Properties $actualV2Properties -CaseSensitive).Count -ne 0 -or
            $null -ne $evidence.prior_terminal_evidence_sha256 -or
            $evidence.prior_terminal_artifact_kind -cne [string]$manifest.binding.terminal_artifact_kind -or
            $evidence.prior_terminal_artifact_sha256 -cne [string]$manifest.binding.terminal_artifact_sha256 -or
            $evidence.prior_success_summary_sha256 -cne [string]$manifest.binding.success_summary_sha256 -or
            $evidence.prior_backup_metadata_sha256 -cne [string]$manifest.binding.backup_metadata_sha256 -or
            $evidence.prior_helper_receipt_inventory_sha256 -cne [string]$manifest.binding.helper_receipt_inventory_sha256 -or
            $evidence.prior_output_inventory_sha256 -cne [string]$manifest.binding.output_inventory_sha256) {
            throw '076b981a recovery multi-artifact lineage differs.'
        }
    } elseif ($evidence.prior_terminal_evidence_sha256 -cne
        [string]$manifest.binding.terminal_evidence_sha256) {
        throw 'Current recovery prior terminal evidence differs.'
    }
} elseif ($evidence.PSObject.Properties.Name -notcontains 'namespace_blocked' -or
    [bool]$evidence.namespace_blocked) {
    throw 'Legacy recovery namespace evidence differs.'
}

$expectedTargets = @{}
$expectedAliases = @()
$expectedFingerprints = @()
foreach ($target in $manifest.disposable_namespace.targets) {
    $alias = [string]$target.alias
    $fingerprint = [string]$target.target_fingerprint_sha256
    $expectedTargets[$alias] = $fingerprint
    $expectedAliases += $alias
    $expectedFingerprints += $fingerprint
}
if ($expectedTargets.Count -ne [int]$manifest.limits.targets) {
    throw 'Recovery manifest target inventory is not exact.'
}

$absence = @($evidence.target_absence)
if ($absence.Count -ne $expectedTargets.Count -or
    @($absence.alias | Sort-Object -Unique).Count -ne $expectedTargets.Count) {
    throw 'Recovery absence inventory is not exact.'
}
foreach ($item in $absence) {
    if (-not $expectedTargets.ContainsKey([string]$item.alias) -or
        $expectedTargets[[string]$item.alias] -cne [string]$item.target_fingerprint_sha256 -or
        $item.result -cne 'ERROR_NOT_FOUND') {
        throw 'Recovery target absence binding failed.'
    }
}
for ($index = 0; $index -lt $absence.Count; $index++) {
    if ([string]$absence[$index].alias -cne $expectedAliases[$index] -or
        [string]$absence[$index].target_fingerprint_sha256 -cne $expectedFingerprints[$index]) {
        throw 'Recovery absence inventory order differs from exact authority.'
    }
}

$allowedOperations = @('CredReadW', 'CredDeleteW', 'CredFree')
$knownFingerprints = @($expectedTargets.Values)
$trace = @($evidence.native_call_trace)
$readCount = 0
$deleteCount = 0
$freeCount = 0
$allocations = @{}
$lastByTarget = @{}
$targetOrder = @{}
for ($index = 0; $index -lt $expectedFingerprints.Count; $index++) {
    $targetOrder[$expectedFingerprints[$index]] = $index
}
$lastTargetOrdinal = -1

for ($index = 0; $index -lt $trace.Count; $index++) {
    $item = $trace[$index]
    $fingerprint = [string]$item.target_fingerprint_sha256
    if ([int64]$item.sequence -ne $index + 1 -or
        $allowedOperations -cnotcontains [string]$item.operation -or
        $knownFingerprints -cnotcontains $fingerprint -or
        [string]$item.scenario -cne 'cleanup-only-recovery') {
        throw 'Recovery trace order/operation/target failed.'
    }
    $targetOrdinal = [int]$targetOrder[$fingerprint]
    if ($targetOrdinal -lt $lastTargetOrdinal -or $targetOrdinal -gt $lastTargetOrdinal + 1) {
        throw 'Recovery trace target order differs from exact authority.'
    }
    $lastTargetOrdinal = $targetOrdinal
    $lastByTarget[$fingerprint] = $item

    switch -CaseSensitive ([string]$item.operation) {
        'CredReadW' {
            $readCount++
            if ($item.result -ceq 'success') {
                if ($null -eq $item.allocation_id -or
                    [int64]$item.allocation_id -le 0 -or
                    $null -ne $item.paired_allocation_id -or
                    $allocations.ContainsKey([string]$item.allocation_id)) {
                    throw 'Recovery read allocation invalid.'
                }
                $allocations[[string]$item.allocation_id] = @{
                    sequence = [int64]$item.sequence
                    target = $fingerprint
                    scenario = [string]$item.scenario
                    free_count = 0
                }
            } elseif ($item.result -ceq 'ERROR_NOT_FOUND') {
                if ($null -ne $item.allocation_id -or $null -ne $item.paired_allocation_id) {
                    throw 'Failed recovery read allocated or paired memory.'
                }
            } else {
                throw 'Recovery read result is not canonical.'
            }
        }
        'CredDeleteW' {
            $deleteCount++
            if (($item.result -cne 'success' -and $item.result -cne 'ERROR_NOT_FOUND') -or
                $null -ne $item.allocation_id -or $null -ne $item.paired_allocation_id) {
                throw 'Recovery delete fields are not canonical.'
            }
        }
        'CredFree' {
            $freeCount++
            $pair = [string]$item.paired_allocation_id
            if ($item.result -cne 'released' -or
                $null -ne $item.allocation_id -or
                $null -eq $item.paired_allocation_id -or
                -not $allocations.ContainsKey($pair) -or
                $allocations[$pair].free_count -ne 0 -or
                [int64]$item.sequence -le $allocations[$pair].sequence -or
                $fingerprint -cne $allocations[$pair].target -or
                [string]$item.scenario -cne $allocations[$pair].scenario) {
                throw 'Recovery free pairing invalid.'
            }
            $allocations[$pair].free_count = 1
        }
    }
}

if (@($allocations.Values | Where-Object { $_.free_count -ne 1 }).Count -ne 0) {
    throw 'Recovery successful read lacks exactly one later free.'
}

$counts = $evidence.native_call_counts
if ([int]$counts.cred_write_w -ne 0 -or
    [int]$counts.cred_read_w -ne $readCount -or
    [int]$counts.cred_delete_w -ne $deleteCount -or
    [int]$counts.cred_free -ne $freeCount -or
    [int]$counts.total -ne $trace.Count -or
    $readCount -gt [int]$manifest.limits.CredReadW -or
    $deleteCount -gt [int]$manifest.limits.CredDeleteW -or
    $freeCount -gt [int]$manifest.limits.CredFree -or
    $trace.Count -gt [int]$manifest.limits.total_native_calls) {
    throw 'Recovery trace-derived count oracle failed.'
}

foreach ($fingerprint in $knownFingerprints) {
    if (-not $lastByTarget.ContainsKey([string]$fingerprint) -or
        $lastByTarget[[string]$fingerprint].operation -cne 'CredReadW' -or
        $lastByTarget[[string]$fingerprint].result -cne 'ERROR_NOT_FOUND') {
        throw 'Recovery terminal per-target absence trace failed.'
    }
}

if ($is076bRecovery) {
    foreach ($fingerprint in $expectedFingerprints) {
        $targetTrace = @($trace | Where-Object { $_.target_fingerprint_sha256 -ceq $fingerprint })
        $operations = @($targetTrace.operation)
        $results = @($targetTrace.result)
        $absentGrammar = $targetTrace.Count -eq 1 -and
            $operations[0] -ceq 'CredReadW' -and $results[0] -ceq 'ERROR_NOT_FOUND'
        $presentGrammar = $targetTrace.Count -eq 4 -and
            (($operations -join '|') -ceq 'CredReadW|CredFree|CredDeleteW|CredReadW') -and
            $results[0] -ceq 'success' -and $results[1] -ceq 'released' -and
            $results[2] -in @('success', 'ERROR_NOT_FOUND') -and $results[3] -ceq 'ERROR_NOT_FOUND'
        if (-not $absentGrammar -and -not $presentGrammar) {
            throw '076b981a recovery per-target call grammar differs from exact authority.'
        }
    }
}

[pscustomobject]@{
    status = 'passed'
    manifest_id = $ManifestId
    manifest_sha256 = $ManifestSha256
    target_absence_count = $absence.Count
    native_call_count = $trace.Count
} | ConvertTo-Json -Compress
