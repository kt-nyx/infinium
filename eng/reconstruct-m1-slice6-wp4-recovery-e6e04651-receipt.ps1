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
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$arguments = @{}
foreach ($entry in $PSBoundParameters.GetEnumerator()) { $arguments[$entry.Key] = $entry.Value }
& (Join-Path $PSScriptRoot 'reconstruct-m1-slice6-wp4-recovery-ad876b9a-receipt.ps1') @arguments
exit 0
