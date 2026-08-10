[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $FixtureRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$root = if ([System.IO.Path]::IsPathRooted($FixtureRoot)) {
    [System.IO.Path]::GetFullPath($FixtureRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $FixtureRoot))
}

function Read-Json([string] $Path) {
    if (-not [System.IO.File]::Exists($Path)) {
        throw "Required cross-stage corpus file is missing: $Path"
    }
    return [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json -ErrorAction Stop
}

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-GitBlobBytes([string] $Revision, [string] $Path) {
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'git'
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('-C')
    $start.ArgumentList.Add($repoRoot)
    $start.ArgumentList.Add('cat-file')
    $start.ArgumentList.Add('blob')
    $start.ArgumentList.Add("${Revision}:$Path")
    $process = [System.Diagnostics.Process]::Start($start)
    $memory = [System.IO.MemoryStream]::new()
    try {
        $process.StandardOutput.BaseStream.CopyTo($memory)
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "Unable to resolve frozen source authority '${Revision}:$Path': $errorText"
        }
        return ,$memory.ToArray()
    } finally {
        $memory.Dispose()
        $process.Dispose()
    }
}

function Assert-NoForbiddenProperty([object] $Value, [string[]] $Forbidden) {
    if ($null -eq $Value) {
        return
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            if ($Forbidden -icontains $property.Name) {
                throw "Answer-bearing property leaked into ordinary input: $($property.Name)"
            }
            Assert-NoForbiddenProperty $property.Value $Forbidden
        }
        return
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        foreach ($item in $Value) {
            Assert-NoForbiddenProperty $item $Forbidden
        }
    }
}

$manifestPath = Join-Path $root 'fixture-manifest.v1.json'
$manifest = Read-Json $manifestPath
$ordinaryPath = Join-Path $root 'ordinary-product-inputs.v1.json'
$schemaPath = Join-Path $root 'ordinary-product-input.schema.json'
$ordinary = Read-Json $ordinaryPath
$expected = Read-Json (Join-Path $root 'expected-results.v1.json')
$harness = Read-Json (Join-Path $root 'harness-envelope.v1.json')
$reviewPath = Join-Path $root 'independent-review.md'

$declaredPaths = @($manifest.package_file_paths)
if ($declaredPaths.Count -ne 10 -or $declaredPaths.Count -ne (@($declaredPaths | Select-Object -Unique)).Count) {
    throw 'cross-stage corpus package_file_paths must be an exact ten-file unique closure.'
}
$actualPackagePaths = @(Get-ChildItem -LiteralPath $root -File |
    Where-Object Name -ne 'independent-review.md' |
    Sort-Object Name | ForEach-Object Name)
$expectedPackagePaths = @($declaredPaths | Sort-Object)
if (($actualPackagePaths -join "`n") -cne ($expectedPackagePaths -join "`n")) {
    throw 'cross-stage corpus on-disk package closure differs from package_file_paths.'
}

$aggregateLines = [System.Text.StringBuilder]::new()
foreach ($file in @($manifest.files)) {
    $path = Join-Path $root ([string] $file.path)
    $item = Get-Item -LiteralPath $path
    $sha = Get-Sha256 $path
    if ($item.Length -ne [long] $file.bytes -or $sha -cne [string] $file.sha256) {
        throw "cross-stage corpus frozen file identity mismatch: $($file.path)"
    }
    $null = $aggregateLines.Append([string] $file.path).Append(':').Append($item.Length).Append(':').Append($sha).Append("`n")
}
if (@($manifest.files).Count -ne 9 -or @($manifest.files.path) -contains 'fixture-manifest.v1.json') {
    throw 'cross-stage corpus hash-bound file list must contain exactly the nine non-self files.'
}
$aggregateBytes = [System.Text.Encoding]::UTF8.GetBytes($aggregateLines.ToString())
$aggregateSha = [Convert]::ToHexStringLower([System.Security.Cryptography.SHA256]::HashData($aggregateBytes))
if ($aggregateSha -cne [string] $manifest.content_aggregate.sha256) {
    throw 'cross-stage corpus ordered content aggregate does not match the manifest.'
}

if (-not (Test-Json -LiteralPath $ordinaryPath -SchemaFile $schemaPath -ErrorAction Stop)) {
    throw 'cross-stage corpus ordinary input failed its recursively closed pre-dispatch schema.'
}
Assert-NoForbiddenProperty $ordinary @(
    'case_id', 'eval_ids', 'oracle_pointer', 'expected', 'expected_results', 'partition',
    'review_metadata', 'supported_cause', 'answer', 'verdict')
$ordinaryClassDrifted = [string] $ordinary.content_class -cne 'ordinary-product-input-only'
$oracleUsedProductOutput = [bool] $expected.product_output_used
$oracleAuthorshipDrifted = [string] $expected.authorship -cne 'independent-before-product-comparison'
if ($ordinaryClassDrifted -or $oracleUsedProductOutput -or $oracleAuthorshipDrifted) {
    throw 'cross-stage corpus answer-isolation metadata is not closed.'
}

$caseIds = @($harness.cases.case_id)
$oracleCaseIds = @($expected.cases.case_id)
if ($caseIds.Count -ne 4 -or ($caseIds -join "`n") -cne ($oracleCaseIds -join "`n")) {
    throw 'cross-stage corpus harness and oracle must bind the same four ordered cases.'
}
$registrations = @($manifest.accumulated_package_registrations)
if ($registrations.Count -ne 11) {
    throw 'cross-stage corpus must register the exact two documentation stage, three candidate stage, four finding/case stage, and two operational stage package identities.'
}
foreach ($registration in $registrations) {
    $authorityPath = Join-Path $repoRoot ([string] $registration.authority_path)
    $item = Get-Item -LiteralPath $authorityPath
    if ($item.Length -ne [long] $registration.bytes -or (Get-Sha256 $authorityPath) -cne [string] $registration.sha256) {
        throw "Accumulated package authority identity drifted: $($registration.package_identity)"
    }
}

$authorityRevision = [string] $manifest.source_authority_resolution.accepted_starting_revision
if ($authorityRevision -cne 'e7de0305515657223c513195f8323b2649b6c7c8') {
    throw 'cross-stage corpus source authority is not bound to the accepted starting revision.'
}
foreach ($authority in @($manifest.source_authority)) {
    $authorityPath = [string] $authority.path
    if ([System.IO.Path]::IsPathRooted($authorityPath) -or
        $authorityPath -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "cross-stage corpus source authority path is unsafe: $authorityPath"
    }
    [byte[]] $authorityBytes = Get-GitBlobBytes $authorityRevision $authorityPath
    $authoritySha = [Convert]::ToHexStringLower(
        [System.Security.Cryptography.SHA256]::HashData($authorityBytes))
    if (($authorityBytes.LongLength -ne [long] $authority.bytes) -or
        ($authoritySha -cne [string] $authority.sha256)) {
        throw "cross-stage corpus frozen source authority identity does not resolve at the accepted revision: $authorityPath"
    }
}

$directIds = @($harness.ownership_audit.exercised_by_this_four_case_corpus.requirement_groups.ids) +
    @($harness.ownership_audit.exercised_by_this_four_case_corpus.adr_groups.ids)
$overstated = @('OPS-004', 'ANALYSIS-019', 'ADR-0017', 'ADR-0023')
if (@($directIds | Where-Object { $overstated -contains $_ }).Count -ne 0) {
    throw 'cross-stage corpus direct four-case ownership retains an unexercised scale/Bethesda/desktop/budget claim.'
}
$inheritedIds = @($harness.ownership_audit.assembled_existing_contract_foundation_operations_evidence.requirement_groups.ids) +
    @($harness.ownership_audit.assembled_existing_contract_foundation_operations_evidence.adr_groups.ids)
foreach ($id in $overstated) {
    if ($inheritedIds -notcontains $id) {
        throw "cross-stage corpus inherited evidence scope is missing $id."
    }
}

if (-not [System.IO.File]::Exists($reviewPath)) {
    throw 'cross-stage corpus final independent review is missing.'
}
$review = [System.IO.File]::ReadAllText($reviewPath)
$manifestLength = (Get-Item -LiteralPath $manifestPath).Length
$manifestSha = Get-Sha256 $manifestPath
$normalizationReviewMarker = '## v1.0.8 repository-cleanup normalization revalidation'
$normalizationReviewIndex = $review.IndexOf($normalizationReviewMarker, [StringComparison]::Ordinal)
$normalizationReview = if ($normalizationReviewIndex -ge 0) {
    $review.Substring($normalizationReviewIndex)
} else {
    ''
}
$manifestStatusDrifted = [string] $manifest.status -cne 'independently-reviewed-accepted-normalization-revalidation'
$reviewVerdictMissing = $normalizationReview -notmatch '(?im)^Verdict:\s*\*\*ACCEPT\*\*'
$reviewManifestMissing = $normalizationReview.IndexOf($manifestSha, [StringComparison]::Ordinal) -lt 0
$reviewAggregateMissing = $normalizationReview.IndexOf($aggregateSha, [StringComparison]::Ordinal) -lt 0
$reviewLengthMissing = $normalizationReview.IndexOf(
    $manifestLength.ToString('N0', [Globalization.CultureInfo]::InvariantCulture), [StringComparison]::Ordinal) -lt 0
if ($manifestStatusDrifted -or $reviewVerdictMissing -or $reviewManifestMissing -or $reviewAggregateMissing -or $reviewLengthMissing) {
    throw 'cross-stage corpus final independent review does not externally freeze and accept the exact manifest/aggregate.'
}

[ordered]@{
    registry_identity = [string] $manifest.registry_identity
    registry_version = [string] $manifest.registry_version
    package_identity = [string] $manifest.package_identity
    package_version = [string] $manifest.package_version
    partition = [string] $manifest.partition
    case_count = $caseIds.Count
    package_file_count = $declaredPaths.Count
    accumulated_registration_count = $registrations.Count
    manifest = [ordered]@{ byte_length = $manifestLength; sha256 = $manifestSha }
    content_aggregate_sha256 = $aggregateSha
    independent_review = [ordered]@{
        byte_length = (Get-Item -LiteralPath $reviewPath).Length
        sha256 = Get-Sha256 $reviewPath
        verdict = 'ACCEPT'
    }
    answer_isolation = 'validated-before-product-dispatch'
    claim_boundary = [string] $manifest.claim_boundary
}
