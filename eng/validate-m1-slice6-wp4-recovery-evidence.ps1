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

if ($evidence.schema -cne 'infinium.m1-s6.wp4.credential-native-recovery-evidence/v1' -or
    $evidence.manifest_id -cne $ManifestId -or
    $evidence.manifest_sha256 -cne $ManifestSha256 -or
    $evidence.status -cne 'passed' -or
    [bool]$evidence.namespace_blocked -or
    [int]$evidence.network_operations -ne 0 -or
    [int]$evidence.dns_operations -ne 0 -or
    [int]$evidence.provider_operations -ne 0 -or
    [int]$evidence.billable_operations -ne 0) {
    throw 'Recovery evidence identity/effect oracle failed.'
}

$expectedTargets = @{}
foreach ($target in $manifest.disposable_namespace.targets) {
    $expectedTargets[[string]$target.alias] = [string]$target.target_fingerprint_sha256
}
if ($expectedTargets.Count -ne 12) {
    throw 'Recovery manifest target inventory is not exact.'
}

$absence = @($evidence.target_absence)
if ($absence.Count -ne 12 -or @($absence.alias | Sort-Object -Unique).Count -ne 12) {
    throw 'Recovery absence inventory is not exact.'
}
foreach ($item in $absence) {
    if (-not $expectedTargets.ContainsKey([string]$item.alias) -or
        $expectedTargets[[string]$item.alias] -cne [string]$item.target_fingerprint_sha256 -or
        $item.result -cne 'ERROR_NOT_FOUND') {
        throw 'Recovery target absence binding failed.'
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

for ($index = 0; $index -lt $trace.Count; $index++) {
    $item = $trace[$index]
    $fingerprint = [string]$item.target_fingerprint_sha256
    if ([int64]$item.sequence -ne $index + 1 -or
        $allowedOperations -cnotcontains [string]$item.operation -or
        $knownFingerprints -cnotcontains $fingerprint -or
        [string]$item.scenario -cne 'cleanup-only-recovery') {
        throw 'Recovery trace order/operation/target failed.'
    }
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
    $readCount -gt 36 -or $deleteCount -gt 12 -or $freeCount -gt 12 -or $trace.Count -gt 60) {
    throw 'Recovery trace-derived count oracle failed.'
}

foreach ($fingerprint in $knownFingerprints) {
    if (-not $lastByTarget.ContainsKey([string]$fingerprint) -or
        $lastByTarget[[string]$fingerprint].operation -cne 'CredReadW' -or
        $lastByTarget[[string]$fingerprint].result -cne 'ERROR_NOT_FOUND') {
        throw 'Recovery terminal per-target absence trace failed.'
    }
}

[pscustomobject]@{
    status = 'passed'
    manifest_id = $ManifestId
    manifest_sha256 = $ManifestSha256
    target_absence_count = $absence.Count
    native_call_count = $trace.Count
} | ConvertTo-Json -Compress
