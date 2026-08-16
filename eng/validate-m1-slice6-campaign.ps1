[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AuthorizationManifest,
    [ValidateSet('Verification', 'Ready', 'Reviewed', 'Admitted', 'RolloverAdmitted')][string]$RequireState = 'Verification',
    [string]$RecordPath = 'docs/plans/milestones/m1/slices/s6/record.md',
    [string]$PriorCredentialManifest,
    [string]$ReplacementCredentialManifest,
    [string]$ZeroEffectEvidence,
    [string]$AuthorityArtifact = 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-owner-authority.v1.json',
    [datetime]$NowUtc = [datetime]::UtcNow
)

if ($PSVersionTable.PSEdition -ne 'Core') {
    $relay = @('-NoProfile','-File',$PSCommandPath,'-AuthorizationManifest',$AuthorizationManifest,
        '-RequireState',$RequireState,'-RecordPath',$RecordPath,'-AuthorityArtifact',$AuthorityArtifact,
        '-NowUtc',$NowUtc.ToUniversalTime().ToString('O',[Globalization.CultureInfo]::InvariantCulture))
    if ($PriorCredentialManifest) { $relay += @('-PriorCredentialManifest',$PriorCredentialManifest) }
    if ($ReplacementCredentialManifest) { $relay += @('-ReplacementCredentialManifest',$ReplacementCredentialManifest) }
    if ($ZeroEffectEvidence) { $relay += @('-ZeroEffectEvidence',$ZeroEffectEvidence) }
    & pwsh @relay
    exit $LASTEXITCODE
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FullPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return [IO.Path]::GetFullPath($PathValue) }
    return [IO.Path]::GetFullPath((Join-Path (Get-Location) $PathValue))
}

function Require-CanonicalRepositoryPath([string]$ActualPath, [string]$ExpectedRelativePath, [string]$Name) {
    $root = (& git rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Campaign validation requires a Git worktree.' }
    $expected = [IO.Path]::GetFullPath((Join-Path $root $ExpectedRelativePath))
    if ((Get-FullPath $ActualPath) -cne $expected) { throw "$Name is not the canonical campaign authority path." }
}

function Get-GitBlobSha256([string]$Commit,[string]$RelativePath) {
    $root = (& git rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Campaign validation requires a Git worktree.' }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'git'
    $start.WorkingDirectory = $root
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('show')
    $start.ArgumentList.Add("$Commit`:$RelativePath")
    $process = [Diagnostics.Process]::Start($start)
    $bytes = [IO.MemoryStream]::new()
    try {
        $process.StandardOutput.BaseStream.CopyTo($bytes)
        $diagnostic = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0 -or $bytes.Length -eq 0) {
            throw "The reviewed candidate does not retain the exact campaign manifest path: $diagnostic"
        }
        return ConvertTo-LowerHex ([Security.Cryptography.SHA256]::HashData($bytes.ToArray()))
    }
    finally {
        $bytes.Dispose()
        $process.Dispose()
    }
}

function Require-ExactMarkerTransition([string]$Marker,[string]$ExpectedParent,[string]$StatePhrase) {
    $recordRelative = 'docs/plans/milestones/m1/slices/s6/record.md'
    $commits = @(& git log --format=%H --fixed-strings -S $Marker -- $recordRelative)
    if ($LASTEXITCODE -ne 0 -or $commits.Count -ne 1) { throw 'Campaign marker must be introduced by one unique committed transition.' }
    $commit = [string]$commits[0]
    $parent = (& git rev-parse "$commit^").Trim()
    if ($LASTEXITCODE -ne 0 -or $parent -cne $ExpectedParent) { throw 'Campaign marker transition has the wrong exact predecessor.' }
    [string[]]$actual = @(& git -c core.quotePath=false diff --name-only $parent $commit --)
    [Array]::Sort($actual,[StringComparer]::Ordinal)
    [string[]]$expected = @('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md',$recordRelative)
    [Array]::Sort($expected,[StringComparer]::Ordinal)
    if ([string]::Join("`n",$actual) -cne [string]::Join("`n",$expected)) { throw 'Campaign marker transition changed a fourth or missing path.' }
    $parentRecord = (@(& git show "$parent`:$recordRelative") -join "`n").TrimEnd("`n")
    $commitRecord = (@(& git show "$commit`:$recordRelative") -join "`n").TrimEnd("`n")
    if ($LASTEXITCODE -ne 0 -or -not $commitRecord.StartsWith($parentRecord + "`n",[StringComparison]::Ordinal)) { throw 'Campaign marker transition is not append-only.' }
    foreach($path in @('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md')) {
        $text = @(& git show "$commit`:$path") -join "`n"
        if ($LASTEXITCODE -ne 0 -or -not $text.Contains($StatePhrase,[StringComparison]::Ordinal)) { throw 'Campaign marker transition has stale authority documentation.' }
    }
    & git merge-base --is-ancestor $commit HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Campaign marker transition is not an ancestor of current HEAD.' }
    return $commit
}

function Resolve-LocalSchemaReference([Text.Json.JsonElement]$Root, [string]$Reference) {
    if (-not $Reference.StartsWith('#/', [StringComparison]::Ordinal)) { throw "Non-local schema reference is prohibited: $Reference" }
    $resolved = $Root
    foreach ($segment in $Reference.Substring(2).Split('/')) {
        $next = [Text.Json.JsonElement]::new()
        $name = $segment.Replace('~1','/').Replace('~0','~')
        if (-not $resolved.TryGetProperty($name, [ref]$next)) { throw "Unresolved schema reference: $Reference" }
        $resolved = $next
    }
    return $resolved
}

function Assert-CampaignSchemaNode([Text.Json.JsonElement]$Instance, [Text.Json.JsonElement]$Schema,
    [Text.Json.JsonElement]$Root, [string]$Location) {
    $probe = [Text.Json.JsonElement]::new()
    if ($Schema.TryGetProperty('oneOf', [ref]$probe)) {
        $matchCount = 0
        foreach ($member in $probe.EnumerateArray()) {
            try { Assert-CampaignSchemaNode $Instance $member $Root $Location; $matchCount++ } catch { }
        }
        if ($matchCount -ne 1) { throw "$Location does not match exactly one schema branch." }
        return
    }
    if ($Schema.TryGetProperty('$ref', [ref]$probe)) {
        Assert-CampaignSchemaNode $Instance (Resolve-LocalSchemaReference $Root $probe.GetString()) $Root $Location
        return
    }
    if ($Schema.TryGetProperty('allOf', [ref]$probe)) {
        foreach ($member in $probe.EnumerateArray()) { Assert-CampaignSchemaNode $Instance $member $Root $Location }
    }
    if ($Schema.TryGetProperty('if', [ref]$probe)) {
        $condition = $probe
        $conditionMatches = $true
        $conditionRequired = [Text.Json.JsonElement]::new()
        if ($condition.TryGetProperty('required', [ref]$conditionRequired)) {
            foreach ($name in $conditionRequired.EnumerateArray()) {
                $found = [Text.Json.JsonElement]::new()
                if ($Instance.ValueKind -ne [Text.Json.JsonValueKind]::Object -or
                    -not $Instance.TryGetProperty($name.GetString(), [ref]$found)) { $conditionMatches = $false }
            }
        }
        $conditionProperties = [Text.Json.JsonElement]::new()
        if ($conditionMatches -and $condition.TryGetProperty('properties', [ref]$conditionProperties)) {
            foreach ($property in $conditionProperties.EnumerateObject()) {
                $actual = [Text.Json.JsonElement]::new()
                if (-not $Instance.TryGetProperty($property.Name, [ref]$actual)) { continue }
                $constant = [Text.Json.JsonElement]::new()
                if ($property.Value.TryGetProperty('const', [ref]$constant) -and
                    -not [Text.Json.JsonElement]::DeepEquals($actual, $constant)) { $conditionMatches = $false }
            }
        }
        $branch = [Text.Json.JsonElement]::new()
        if ($conditionMatches -and $Schema.TryGetProperty('then', [ref]$branch)) {
            Assert-CampaignSchemaNode $Instance $branch $Root $Location
        } elseif (-not $conditionMatches -and $Schema.TryGetProperty('else', [ref]$branch)) {
            Assert-CampaignSchemaNode $Instance $branch $Root $Location
        }
    }
    if ($Schema.TryGetProperty('type', [ref]$probe)) {
        $ok = switch ($probe.GetString()) {
            'object' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Object }
            'array' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Array }
            'string' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::String }
            'integer' { $value = [int64]0; $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Number -and $Instance.TryGetInt64([ref]$value) }
            'boolean' { $Instance.ValueKind -in @([Text.Json.JsonValueKind]::True,[Text.Json.JsonValueKind]::False) }
            'null' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Null }
            default { throw "Unsupported campaign schema type at $Location." }
        }
        if (-not $ok) { throw "$Location has the wrong schema type." }
    }
    if ($Schema.TryGetProperty('const', [ref]$probe) -and -not [Text.Json.JsonElement]::DeepEquals($Instance,$probe)) {
        throw "$Location differs from its schema constant."
    }
    if ($Schema.TryGetProperty('enum', [ref]$probe)) {
        $ok = $false
        foreach ($member in $probe.EnumerateArray()) { if ([Text.Json.JsonElement]::DeepEquals($Instance,$member)) { $ok=$true; break } }
        if (-not $ok) { throw "$Location is outside its schema enumeration." }
    }
    if ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::Number) {
        $minimum = [Text.Json.JsonElement]::new()
        $maximum = [Text.Json.JsonElement]::new()
        $hasMinimum = $Schema.TryGetProperty('minimum',[ref]$minimum)
        $hasMaximum = $Schema.TryGetProperty('maximum',[ref]$maximum)
        if ($hasMinimum -or $hasMaximum) {
            $number = [decimal]0
            if (-not $Instance.TryGetDecimal([ref]$number)) { throw "$Location is not an exact bounded number." }
            if ($hasMinimum -and $number -lt $minimum.GetDecimal()) { throw "$Location is below its schema minimum." }
            if ($hasMaximum -and $number -gt $maximum.GetDecimal()) { throw "$Location exceeds its schema maximum." }
        }
    }
    if ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        $required = [Text.Json.JsonElement]::new()
        if ($Schema.TryGetProperty('required',[ref]$required)) {
            foreach ($name in $required.EnumerateArray()) { $found=[Text.Json.JsonElement]::new(); if (-not $Instance.TryGetProperty($name.GetString(),[ref]$found)) { throw "$Location is missing $($name.GetString())." } }
        }
        $properties=[Text.Json.JsonElement]::new(); $hasProperties=$Schema.TryGetProperty('properties',[ref]$properties)
        $additional=[Text.Json.JsonElement]::new(); $closed=$Schema.TryGetProperty('additionalProperties',[ref]$additional) -and $additional.ValueKind -eq [Text.Json.JsonValueKind]::False
        foreach ($property in $Instance.EnumerateObject()) {
            $child=[Text.Json.JsonElement]::new()
            if ($hasProperties -and $properties.TryGetProperty($property.Name,[ref]$child)) { Assert-CampaignSchemaNode $property.Value $child $Root "$Location.$($property.Name)" }
            elseif ($closed) { throw "$Location has unknown property $($property.Name)." }
        }
    } elseif ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        $items=@($Instance.EnumerateArray()); $min=[Text.Json.JsonElement]::new(); $max=[Text.Json.JsonElement]::new()
        if ($Schema.TryGetProperty('minItems',[ref]$min) -and $items.Count -lt $min.GetInt32()) { throw "$Location has too few items." }
        if ($Schema.TryGetProperty('maxItems',[ref]$max) -and $items.Count -gt $max.GetInt32()) { throw "$Location has too many items." }
        $prefix=[Text.Json.JsonElement]::new()
        if ($Schema.TryGetProperty('prefixItems',[ref]$prefix)) { $schemas=@($prefix.EnumerateArray()); for($i=0;$i -lt $items.Count -and $i -lt $schemas.Count;$i++){ Assert-CampaignSchemaNode $items[$i] $schemas[$i] $Root "$Location[$i]" } }
        $itemSchema=[Text.Json.JsonElement]::new()
        if ($Schema.TryGetProperty('items',[ref]$itemSchema)) {
            if ($itemSchema.ValueKind -eq [Text.Json.JsonValueKind]::False -and (-not $Schema.TryGetProperty('prefixItems',[ref]$prefix) -or $items.Count -gt @($prefix.EnumerateArray()).Count)) { throw "$Location has an unmodelled item." }
            if ($itemSchema.ValueKind -eq [Text.Json.JsonValueKind]::Object) { for($i=0;$i -lt $items.Count;$i++){ Assert-CampaignSchemaNode $items[$i] $itemSchema $Root "$Location[$i]" } }
        }
        $unique = [Text.Json.JsonElement]::new()
        if ($Schema.TryGetProperty('uniqueItems', [ref]$unique) -and $unique.GetBoolean()) {
            for ($left = 0; $left -lt $items.Count; $left++) {
                for ($right = $left + 1; $right -lt $items.Count; $right++) {
                    if ([Text.Json.JsonElement]::DeepEquals($items[$left], $items[$right])) {
                        throw "$Location contains duplicate items."
                    }
                }
            }
        }
    } elseif ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::String) {
        $text = $Instance.GetString()
        $minLength=[Text.Json.JsonElement]::new()
        $maxLength=[Text.Json.JsonElement]::new()
        if ($Schema.TryGetProperty('minLength',[ref]$minLength) -and $text.Length -lt $minLength.GetInt32()) { throw "$Location is shorter than its schema minimum." }
        if ($Schema.TryGetProperty('maxLength',[ref]$maxLength) -and $text.Length -gt $maxLength.GetInt32()) { throw "$Location exceeds its schema maximum length." }
        $pattern=[Text.Json.JsonElement]::new(); if ($Schema.TryGetProperty('pattern',[ref]$pattern) -and $text -cnotmatch $pattern.GetString()) { throw "$Location does not match its schema pattern." }
    }
}

function Assert-CampaignSchema([string]$DocumentPath,[string]$SchemaPath) {
    $document=[Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($DocumentPath)); $schema=[Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($SchemaPath))
    try {
        Assert-NoDuplicateCampaignProperties $document.RootElement '$campaign'
        Assert-CampaignSchemaNode $document.RootElement $schema.RootElement $schema.RootElement '$campaign'
    } finally { $schema.Dispose(); $document.Dispose() }
}

function Assert-NoDuplicateCampaignProperties([Text.Json.JsonElement]$Value,[string]$Location) {
    if ($Value.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($property in $Value.EnumerateObject()) {
            if (-not $names.Add($property.Name)) { throw "$Location has duplicate property $($property.Name)." }
            Assert-NoDuplicateCampaignProperties $property.Value "$Location.$($property.Name)"
        }
    } elseif ($Value.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        $index=0; foreach($item in $Value.EnumerateArray()){ Assert-NoDuplicateCampaignProperties $item "$Location[$index]"; $index++ }
    }
}

function Get-Sha256([string]$PathValue) {
    $stream = [IO.File]::OpenRead($PathValue)
    try {
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
        finally { $algorithm.Dispose() }
    }
    finally { $stream.Dispose() }
}

function ConvertTo-LowerHex([byte[]]$Bytes) { return ([BitConverter]::ToString($Bytes)).Replace('-','').ToLowerInvariant() }

function Get-ExactUtcText($Value) {
    if ($Value -is [datetime]) { return $Value.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ', [Globalization.CultureInfo]::InvariantCulture) }
    return [string]$Value
}

function Require-ExactProperties($Value, [string[]]$Names, [string]$PathValue) {
    if ($null -eq $Value) { throw "$PathValue is absent." }
    $actual = @($Value.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count) { throw "$PathValue has an unknown, missing, or duplicate property." }
    for ($index = 0; $index -lt $Names.Count; $index++) {
        if ($actual[$index] -cne $Names[$index]) { throw "$PathValue property order/identity is not exact." }
    }
}

function Require-ExactArray($Value, [object[]]$Expected, [string]$PathValue) {
    $actual = @($Value)
    if ($actual.Count -ne $Expected.Count) { throw "$PathValue count is not exact." }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ([string]$actual[$index] -cne [string]$Expected[$index]) { throw "$PathValue[$index] is not exact." }
    }
}

function Require-LowerHex([string]$Value, [int]$Length, [string]$PathValue) {
    if ($Value -cnotmatch ('^[0-9a-f]{' + $Length + '}$')) { throw "$PathValue is not exact lowercase hex." }
}

function Require-ZeroEffects($Value) {
    Require-ExactProperties $Value @('credential_helper_launch_count','credential_helper_readiness_count','credential_authority_lock_count','credential_manager_call_count','provider_dispatch_count','dns_or_public_network_count','profile_materialization_count','api_key_observed','production_output_roots_absent') 'semantic_rollover.zero_effect_proof'
    foreach ($name in @('credential_helper_launch_count','credential_helper_readiness_count','credential_authority_lock_count','credential_manager_call_count','provider_dispatch_count','dns_or_public_network_count','profile_materialization_count')) {
        if ([long]$Value.$name -ne 0) { throw "$name closes semantic rollover." }
    }
    if ([bool]$Value.api_key_observed -or -not [bool]$Value.production_output_roots_absent) { throw 'The zero-effect proof is false.' }
}

function Convert-CanonicalJson($Value) { return ($Value | ConvertTo-Json -Depth 100 -Compress) }

function Require-CredentialNonBroadening($Prior, $Replacement, $Effects) {
    Require-ZeroEffects $Effects
    $mutableTop = @('candidate_binding','release_build')
    $priorNames = @($Prior.PSObject.Properties.Name)
    $replacementNames = @($Replacement.PSObject.Properties.Name)
    Require-ExactArray $replacementNames $priorNames 'replacement credential top-level properties'
    foreach ($name in $priorNames) {
        if ($mutableTop -notcontains $name -and (Convert-CanonicalJson $Prior.$name) -cne (Convert-CanonicalJson $Replacement.$name)) {
            throw "Credential semantic rollover changed $name."
        }
    }
    Require-ExactProperties $Replacement.candidate_binding @('close_ready_implementation_commit','accepted_wp8_verification_commit','accepted_wp8_evidence_commit','accepted_wp8_non_live_all_sha256','accepted_wp8_pre_live_sha256','accepted_wp8_direct_layer6_sha256') 'replacement.candidate_binding'
    foreach ($name in @('accepted_wp8_verification_commit','accepted_wp8_evidence_commit','accepted_wp8_non_live_all_sha256','accepted_wp8_pre_live_sha256','accepted_wp8_direct_layer6_sha256')) {
        if ([string]$Prior.candidate_binding.$name -cne [string]$Replacement.candidate_binding.$name) { throw "Credential semantic rollover changed candidate_binding.$name." }
    }
    Require-LowerHex ([string]$Replacement.candidate_binding.close_ready_implementation_commit) 40 'replacement.candidate_binding.close_ready_implementation_commit'
    Require-ExactProperties $Replacement.release_build @('configuration','target_framework','source_commit','build_command','coordinator_relative_path','coordinator_sha256','helper_relative_path','helper_sha256','binary_inventory_pattern','binary_inventory_file_count','binary_inventory_sha256') 'replacement.release_build'
    foreach ($name in @('configuration','target_framework','coordinator_relative_path','helper_relative_path','binary_inventory_pattern')) {
        if ([string]$Prior.release_build.$name -cne [string]$Replacement.release_build.$name) { throw "Credential semantic rollover changed release_build.$name." }
    }
    Require-LowerHex ([string]$Replacement.release_build.source_commit) 40 'replacement.release_build.source_commit'
    foreach ($name in @('coordinator_sha256','helper_sha256','binary_inventory_sha256')) { Require-LowerHex ([string]$Replacement.release_build.$name) 64 "replacement.release_build.$name" }
    if ([int]$Replacement.release_build.binary_inventory_file_count -le 0) { throw 'Replacement Release inventory is incomplete.' }
    $expectedBuild = 'dotnet build Infinium.sln -c Release --no-restore --nologo --no-incremental -p:SourceRevisionId=' + [string]$Replacement.release_build.source_commit
    if ([string]$Replacement.release_build.build_command -cne $expectedBuild) { throw 'Replacement Release build command is not source-pinned.' }
}

$manifestPath = Get-FullPath $AuthorizationManifest
$schemaPath = Get-FullPath 'contracts/repository/m1-slice6-finite-campaign-authorization.v1.schema.json'
$authoritySchemaPath = Get-FullPath 'contracts/repository/m1-slice6-finite-campaign-owner-authority.v1.schema.json'
$authorityPath = Get-FullPath $AuthorityArtifact
foreach ($path in @($manifestPath, $schemaPath, $authorityPath, $authoritySchemaPath)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required campaign authority file is absent: $path" } }
Require-CanonicalRepositoryPath $AuthorizationManifest 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v1.json' 'AuthorizationManifest'
Require-CanonicalRepositoryPath $AuthorityArtifact 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-owner-authority.v1.json' 'AuthorityArtifact'
Require-CanonicalRepositoryPath $RecordPath 'docs/plans/milestones/m1/slices/s6/record.md' 'RecordPath'
Assert-CampaignSchema $manifestPath $schemaPath
Assert-CampaignSchema $authorityPath $authoritySchemaPath
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$null = Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json
$authority = Get-Content -LiteralPath $authorityPath -Raw | ConvertFrom-Json
Require-ExactProperties $authority @('schema_identity','source_attachment_sha256','recorded_at_utc','authority','derivation','effect_boundary') 'authority artifact'
Require-ExactProperties $authority.authority @('accepted_plan_amendment','semantic_rollover','credential_envelope','campaign_expires_at_utc','credential_expires_at_utc','maximum_sequential_provider_calls','retry','fourth_call') 'authority artifact.authority'
Require-ExactProperties $authority.derivation @('future_campaign_bytes_preaccepted','fresh_independent_review_required','campaign_admission_requires_exact_schema_valid_bytes','campaign_admission_marker_role','credential_rollover_marker_role','historical_owner_marker_inheritance') 'authority artifact.derivation'
Require-ExactProperties $authority.effect_boundary @('before_exact_campaign_admission','provider_stage_materialization_before_credential_success_acceptance','private_or_archive_access','push') 'authority artifact.effect_boundary'
if ($authority.schema_identity -cne 'infinium.repository.m1-slice6-finite-campaign-owner-authority/1.0.0' -or
    $authority.source_attachment_sha256 -cne 'c9541bb5563304335e8f7af4d176eba3e507c719c4e135c542b8ac1bc4bc12be' -or
    $authority.authority.accepted_plan_amendment -cne 'exact-finite-three-stage-m1-slice6-live-campaign' -or
    $authority.authority.semantic_rollover -cne 'pre-effect-only-field-by-field-non-broadening' -or
    $authority.authority.credential_envelope -cne 'unchanged' -or
    (Get-ExactUtcText $authority.authority.campaign_expires_at_utc) -cne '2026-08-22T23:59:00.0000000Z' -or
    (Get-ExactUtcText $authority.authority.credential_expires_at_utc) -cne '2026-08-17T15:25:00.0000000Z' -or
    [int]$authority.authority.maximum_sequential_provider_calls -ne 3 -or
    $authority.authority.retry -cne 'prohibited' -or $authority.authority.fourth_call -cne 'prohibited' -or
    [bool]$authority.derivation.future_campaign_bytes_preaccepted -or -not [bool]$authority.derivation.fresh_independent_review_required -or
    -not [bool]$authority.derivation.campaign_admission_requires_exact_schema_valid_bytes -or
    $authority.derivation.historical_owner_marker_inheritance -cne 'prohibited' -or
    $authority.effect_boundary.provider_stage_materialization_before_credential_success_acceptance -cne 'prohibited' -or
    $authority.effect_boundary.private_or_archive_access -cne 'prohibited' -or $authority.effect_boundary.push -cne 'prohibited') {
    throw 'The immutable campaign authority artifact is stale or broadened.'
}

Require-ExactProperties $manifest @('schema_identity','campaign_id','status','effect_authority','prepared_at_utc','expires_at_utc','candidate_binding','authority_source','semantic_rollover','credential_envelope','safety_identifier','official_document_snapshot','ordered_stages','aggregate_limits','campaign_ledger','stage_authority_contract','admission','rehearsal','execution') 'campaign'
if ($manifest.schema_identity -cne 'infinium.repository.m1-slice6-finite-campaign-authorization/1.0.0' -or $manifest.campaign_id -cne 'infinium.m1-s6.finite-live-campaign/da6ba996-29b9-4aa7-a938-b6675047ebee') { throw 'Campaign identity is not exact.' }
if ($manifest.effect_authority -cne 'none-until-exact-reviewed-campaign-admission') { throw 'Campaign effect authority was broadened.' }
if ((Get-ExactUtcText $manifest.expires_at_utc) -cne '2026-08-22T23:59:00.0000000Z') { throw 'Campaign expiry is not exact.' }
if ($manifest.authority_source.attachment_sha256 -cne 'c9541bb5563304335e8f7af4d176eba3e507c719c4e135c542b8ac1bc4bc12be' -or $authority.source_attachment_sha256 -cne $manifest.authority_source.attachment_sha256) { throw 'Immutable attachment authority is stale.' }
Require-ZeroEffects $manifest.semantic_rollover.zero_effect_proof
Require-ExactProperties $manifest.credential_envelope @('source_manifest_id','source_manifest_sha256','source_candidate_commit','comparison','exact_immutable_fields','mutable_fields','ceilings','credential_expires_at_utc','profile_id','generation_id','target_fingerprint_sha256') 'credential_envelope'
if ($manifest.credential_envelope.source_manifest_id -cne 'infinium.m1-s6.wp9.production-profile-authorization/ded946a6-e1b8-4c8e-95eb-5ef59619804f' -or
    $manifest.credential_envelope.source_manifest_sha256 -cne 'fb301a17843496b0452561facdbaa29412c2ba0d44ce4cc7c8bc102a391e88a9' -or
    $manifest.credential_envelope.source_candidate_commit -cne 'cf2b31f3cf109f09c47293aeb1cf6afde1ffff0f' -or
    (Get-ExactUtcText $manifest.credential_envelope.credential_expires_at_utc) -cne '2026-08-17T15:25:00.0000000Z' -or
    $manifest.credential_envelope.profile_id -cne 'openai-platform-492800995cf046c7815f974e865f9e1d' -or
    $manifest.credential_envelope.generation_id -cne 'g-9c663cb01fb649cba7eff4e26e14274c' -or
    $manifest.credential_envelope.target_fingerprint_sha256 -cne '55ade50556f396dd0ba579632a21581887eeb1e4e44411a0ee8e37f460f09fca') {
    throw 'Credential envelope binding is stale or broadened.'
}
Require-ExactArray $manifest.credential_envelope.exact_immutable_fields @('schema_identity','manifest_id','packet_kind','status','effect_authority','expires_at_utc','predecessor_binding','owner_authorization','provider_intent','official_document_refresh','profile','native_boundary','m1_entry_surface','future_product_ux','durable_state','output','stop_conditions','execution') 'credential_envelope.exact_immutable_fields'
Require-ExactArray $manifest.credential_envelope.mutable_fields @('candidate_binding.close_ready_implementation_commit','release_build.source_commit','release_build.build_command','release_build.coordinator_sha256','release_build.helper_sha256','release_build.binary_inventory_file_count','release_build.binary_inventory_sha256') 'credential_envelope.mutable_fields'

$priorCredentialText = (& git show ($manifest.credential_envelope.source_candidate_commit + ':docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json') 2>$null) -join "`n"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($priorCredentialText)) { throw 'The bound prior credential manifest cannot be read from Git.' }
$priorCredential = $priorCredentialText | ConvertFrom-Json
$currentCredentialPath = Get-FullPath 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
$currentCredential = Get-Content -LiteralPath $currentCredentialPath -Raw | ConvertFrom-Json
Require-CredentialNonBroadening $priorCredential $currentCredential $manifest.semantic_rollover.zero_effect_proof

$manifestSha = Get-Sha256 $manifestPath
$recordText = if (Test-Path -LiteralPath (Get-FullPath $RecordPath)) { Get-Content -LiteralPath (Get-FullPath $RecordPath) -Raw } else { '' }
$recordLines = @($recordText -split "`r?`n")
$reviewPattern = '^M1_S6_CAMPAIGN_REVIEW_ACCEPTANCE candidate_commit=([0-9a-f]{40}) campaign_id=' + [regex]::Escape([string]$manifest.campaign_id) + ' sha256=' + $manifestSha + ' verdicts=security,semantics,diff$'
$reviewMatches = @($recordLines | ForEach-Object { if ($_ -cmatch $reviewPattern) { $Matches[1] } })
$reviewCount = $reviewMatches.Count
$reviewedCandidate = if ($reviewCount -eq 1) { [string]$reviewMatches[0] } else { $null }

Require-ExactProperties $manifest.candidate_binding @('close_ready_implementation_commit','review_candidate_resolution') 'candidate_binding'
$ready = $manifest.status -ceq 'ready-for-campaign-review'
$reviewCandidateCommit = $null
if ($ready) {
    Require-LowerHex $manifest.candidate_binding.close_ready_implementation_commit 40 'candidate_binding.close_ready_implementation_commit'
    if ($manifest.candidate_binding.review_candidate_resolution -cne 'exact-clean-head-after-four-document-binding') {
        throw 'Ready campaign review candidate resolution is not exact.'
    }
    if (-not [string]::IsNullOrWhiteSpace((& git status --porcelain))) {
        throw 'Ready campaign validation requires a clean committed review candidate.'
    }
    $reviewCandidateCommit = if ($reviewCount -eq 1) { $reviewedCandidate } else { (& git rev-parse HEAD).Trim() }
    & git merge-base --is-ancestor $reviewCandidateCommit HEAD
    if ($LASTEXITCODE -ne 0) { throw 'The bound campaign review candidate is not an ancestor of current HEAD.' }
    $closeReady = [string]$manifest.candidate_binding.close_ready_implementation_commit
    & git merge-base --is-ancestor $closeReady $reviewCandidateCommit
    if ($LASTEXITCODE -ne 0 -or $closeReady -ceq $reviewCandidateCommit) {
        throw 'Ready campaign requires a distinct ancestor close-ready source A and bound review candidate B.'
    }
    [string[]]$actualBindingPaths = @(& git -c core.quotePath=false diff --name-only $closeReady $reviewCandidateCommit --)
    [string[]]$exactBindingPaths = @(
        'docs/current-state.md',
        'docs/plans/milestones/m1/slices/s6/README.md',
        'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json')
    [Array]::Sort($actualBindingPaths, [StringComparer]::Ordinal)
    [Array]::Sort($exactBindingPaths, [StringComparer]::Ordinal)
    if ([string]::Join("`n", $actualBindingPaths) -cne [string]::Join("`n", $exactBindingPaths)) {
        throw ('Ready campaign review candidate B must differ from close-ready source A by exactly four binding documents. actual=' + [string]::Join('|',$actualBindingPaths))
    }
    $pendingManifest = @(& git show "$closeReady`:docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v1.json") -join "`n"
    if ($LASTEXITCODE -ne 0 -or -not $pendingManifest.Contains('"status": "verification-pending"', [StringComparison]::Ordinal) -or
        -not $pendingManifest.Contains('"review_candidate_resolution": "pending"', [StringComparison]::Ordinal)) {
        throw 'Close-ready source A is not the exact pending non-executable campaign state.'
    }
} elseif ($manifest.status -ceq 'verification-pending') {
    foreach ($name in $manifest.candidate_binding.PSObject.Properties.Name) { if ($manifest.candidate_binding.$name -cne 'pending') { throw 'Pending campaign binding mixed exact and pending values.' } }
} else { throw 'Campaign status is unknown.' }

$expectedStages = @(
    @('1','WP9','Qualification','not-materialized-until-credential-success-independent-acceptance','credential-profile-active-verified-and-independently-accepted','16384','20480','256','262144','60000','1','140000000'),
    @('2','WP10','SourceClaimExtraction','not-materialized-until-wp9-live-evidence-independent-acceptance','wp9-live-evidence-independently-accepted','65536','73728','4096','1048576','120000','1','600000000'),
    @('3','WP11','CandidateInvestigation','not-materialized-until-wp10-live-evidence-independent-acceptance','wp10-live-evidence-independently-accepted','65536','73728','4096','1048576','120000','1','600000000')
)
$stages = @($manifest.ordered_stages)
if ($stages.Count -ne 3) { throw 'Campaign must contain exactly three stages.' }
for ($index = 0; $index -lt 3; $index++) {
    $stage = $stages[$index]
    Require-ExactProperties $stage @('ordinal','work_package','operation','request_manifest','predecessor','maximum_request_bytes','maximum_input_tokens','maximum_output_tokens','maximum_raw_response_bytes','deadline_milliseconds','maximum_provider_calls','maximum_nano_usd') "ordered_stages[$index]"
    $actual = @([string]$stage.ordinal,[string]$stage.work_package,[string]$stage.operation,[string]$stage.request_manifest,[string]$stage.predecessor,[string]$stage.maximum_request_bytes,[string]$stage.maximum_input_tokens,[string]$stage.maximum_output_tokens,[string]$stage.maximum_raw_response_bytes,[string]$stage.deadline_milliseconds,[string]$stage.maximum_provider_calls,[string]$stage.maximum_nano_usd)
    Require-ExactArray $actual $expectedStages[$index] "ordered_stages[$index]"
}

Require-ExactProperties $manifest.aggregate_limits @('maximum_provider_calls','maximum_request_bytes','maximum_input_tokens','maximum_output_tokens','maximum_raw_response_bytes','maximum_nano_usd','maximum_dns_resolutions','maximum_credential_calls','automatic_retry','parallel_calls','fourth_call','ambiguous_start') 'aggregate_limits'
$aggregate = @([string]$manifest.aggregate_limits.maximum_provider_calls,[string]$manifest.aggregate_limits.maximum_request_bytes,[string]$manifest.aggregate_limits.maximum_input_tokens,[string]$manifest.aggregate_limits.maximum_output_tokens,[string]$manifest.aggregate_limits.maximum_raw_response_bytes,[string]$manifest.aggregate_limits.maximum_nano_usd,[string]$manifest.aggregate_limits.maximum_dns_resolutions)
Require-ExactArray $aggregate @('3','147456','167936','8448','2359296','1340000000','3') 'aggregate limits'
if ($manifest.aggregate_limits.automatic_retry -or $manifest.aggregate_limits.parallel_calls -or $manifest.aggregate_limits.fourth_call -cne 'prohibited') { throw 'Retry, parallelism, or a fourth call was enabled.' }
Require-ExactProperties $manifest.aggregate_limits.maximum_credential_calls @('CredWriteW','CredReadW','CredDeleteW','CredFree','total') 'aggregate credential calls'
Require-ExactArray @([string]$manifest.aggregate_limits.maximum_credential_calls.CredWriteW,[string]$manifest.aggregate_limits.maximum_credential_calls.CredReadW,[string]$manifest.aggregate_limits.maximum_credential_calls.CredDeleteW,[string]$manifest.aggregate_limits.maximum_credential_calls.CredFree,[string]$manifest.aggregate_limits.maximum_credential_calls.total) @('1','5','0','4','10') 'aggregate credential calls'

if ($manifest.safety_identifier.domain -cne 'infinium.openai.safety-identifier/v1' -or $manifest.safety_identifier.seed_generation -cne 'cryptographic-random-32-bytes-create-new-once' -or $manifest.safety_identifier.raw_seed_transmitted -or -not $manifest.safety_identifier.stable_for_product_user) { throw 'Safety identifier contract was weakened.' }
if ($manifest.execution.campaign_permitted -or $manifest.execution.credential_helper_launch_permitted -or $manifest.execution.credential_manager_operation_permitted -or $manifest.execution.provider_request_permitted -or $null -ne $manifest.execution.command) { throw 'The pre-effect campaign manifest became executable.' }

$rank = @{ Verification = 0; Ready = 1; Reviewed = 2; Admitted = 3; RolloverAdmitted = 4 }
if ($rank[$RequireState] -ge 1 -and -not $ready) { throw 'Exact campaign bindings are not ready.' }
$admissionPattern = '^M1_S6_CAMPAIGN_ADMISSION candidate_commit=([0-9a-f]{40}) authority_sha256=' + $manifest.authority_source.attachment_sha256 + ' campaign_id=' + [regex]::Escape([string]$manifest.campaign_id) + ' sha256=' + $manifestSha + ' close_ready_commit=' + $manifest.candidate_binding.close_ready_implementation_commit + ' expires_at_utc=' + [regex]::Escape((Get-ExactUtcText $manifest.expires_at_utc)) + '$'
$admissionMatches = @($recordLines | ForEach-Object { if ($_ -cmatch $admissionPattern) { $Matches[1] } })
$admissionCount = $admissionMatches.Count
if ($rank[$RequireState] -ge 2 -and $reviewCount -ne 1) { throw 'The exact campaign review marker is absent or duplicated.' }
if ($rank[$RequireState] -ge 3 -and $admissionCount -ne 1) { throw 'The exact campaign admission marker is absent or duplicated.' }
if ($admissionCount -gt 0 -and $reviewCount -ne 1) { throw 'Campaign admission has no unique predecessor review.' }
if ($reviewCount -eq 1) {
    & git cat-file -e "$reviewedCandidate^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'The reviewed campaign candidate does not exist.' }
    & git merge-base --is-ancestor $reviewedCandidate HEAD
    if ($LASTEXITCODE -ne 0) { throw 'The reviewed campaign candidate is not an ancestor of current HEAD.' }
    $candidateHash = Get-GitBlobSha256 $reviewedCandidate 'docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v1.json'
    if ($candidateHash -cne $manifestSha) {
        throw "The review marker candidate does not bind the exact campaign manifest bytes (candidate=$candidateHash current=$manifestSha)."
    }
}
if ($admissionCount -eq 1 -and [string]$admissionMatches[0] -cne $reviewedCandidate) { throw 'Campaign admission does not bind the exact reviewed candidate.' }
if ($rank[$RequireState] -ge 3 -and $NowUtc.ToUniversalTime() -ge ([datetime]$manifest.expires_at_utc).ToUniversalTime()) { throw 'Campaign admission is expired.' }
$reviewCommit = $null
if ($rank[$RequireState] -ge 2) {
    $exactReviewMarker = 'M1_S6_CAMPAIGN_REVIEW_ACCEPTANCE candidate_commit=' + $reviewedCandidate + ' campaign_id=' + $manifest.campaign_id + ' sha256=' + $manifestSha + ' verdicts=security,semantics,diff'
    $reviewCommit = Require-ExactMarkerTransition $exactReviewMarker $reviewedCandidate 'Campaign review accepted; exact owner admission remains pending and no effect is authorized.'
}
$admissionCommit = $null
if ($rank[$RequireState] -ge 3) {
    $exactAdmissionMarker = 'M1_S6_CAMPAIGN_ADMISSION candidate_commit=' + $reviewedCandidate + ' authority_sha256=' + $manifest.authority_source.attachment_sha256 + ' campaign_id=' + $manifest.campaign_id + ' sha256=' + $manifestSha + ' close_ready_commit=' + $manifest.candidate_binding.close_ready_implementation_commit + ' expires_at_utc=' + (Get-ExactUtcText $manifest.expires_at_utc)
    $admissionCommit = Require-ExactMarkerTransition $exactAdmissionMarker $reviewCommit 'Campaign admitted; exact credential rollover admission remains pending and no effect is authorized.'
}
$rolloverCommit = $null
if ($rank[$RequireState] -ge 4) {
    $credentialSha = Get-Sha256 $currentCredentialPath
    $rolloverMarker = 'WP9_PROFILE_CAMPAIGN_ROLLOVER_ADMISSION campaign_candidate_commit=' + $reviewedCandidate + ' authority_sha256=' + $manifest.authority_source.attachment_sha256 + ' campaign_id=' + $manifest.campaign_id + ' campaign_sha256=' + $manifestSha + ' manifest_id=' + $currentCredential.manifest_id + ' sha256=' + $credentialSha + ' close_ready_commit=' + $currentCredential.candidate_binding.close_ready_implementation_commit + ' credential_expires_at_utc=' + (Get-ExactUtcText $currentCredential.expires_at_utc)
    if (@($recordLines | Where-Object { $_ -ceq $rolloverMarker }).Count -ne 1) { throw 'The exact campaign credential rollover marker is absent or duplicated.' }
    $rolloverCommit = Require-ExactMarkerTransition $rolloverMarker $admissionCommit 'Campaign credential rollover admitted; only the exact one-shot credential enrollment-or-cancel handoff is eligible.'
}

if ($PriorCredentialManifest -or $ReplacementCredentialManifest -or $ZeroEffectEvidence) {
    if (-not ($PriorCredentialManifest -and $ReplacementCredentialManifest -and $ZeroEffectEvidence)) { throw 'Credential rollover comparison requires all three inputs.' }
    $prior = Get-Content -LiteralPath (Get-FullPath $PriorCredentialManifest) -Raw | ConvertFrom-Json
    $replacement = Get-Content -LiteralPath (Get-FullPath $ReplacementCredentialManifest) -Raw | ConvertFrom-Json
    $effects = Get-Content -LiteralPath (Get-FullPath $ZeroEffectEvidence) -Raw | ConvertFrom-Json
    Require-CredentialNonBroadening $prior $replacement $effects
}

[pscustomobject]@{
    schema = 'infinium.m1-s6.campaign-validation-receipt/v1'
    disposition = if ($RequireState -ceq 'RolloverAdmitted') { 'rollover-admitted' } else { $RequireState.ToLowerInvariant() }
    campaign_id = $manifest.campaign_id
    manifest_sha256 = $manifestSha
    review_marker_count = $reviewCount
    admission_marker_count = $admissionCount
    reviewed_candidate_commit = $reviewedCandidate
    bound_review_candidate_commit = $reviewCandidateCommit
    review_closeout_commit = $reviewCommit
    admission_closeout_commit = $admissionCommit
    rollover_closeout_commit = $rolloverCommit
    provider_call_maximum = 3
    dns_maximum = 3
    maximum_nano_usd = 1340000000
    effect_count = 0
} | ConvertTo-Json -Depth 8
