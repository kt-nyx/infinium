[CmdletBinding()]
param(
    [string] $MatrixPath = 'docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json',
    [string] $ProfileTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json',
    [string] $QualificationTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json',
    [string] $SourceClaimTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json',
    [string] $CandidateTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json',
    [string] $OutputPath,
    [switch] $RequireFrozenCandidate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Resolve-InputPath([string] $Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $repoRoot $Value))
}

function Read-StrictJson([string] $Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    $text = [Text.Encoding]::UTF8.GetString($bytes)
    $options = [Text.Json.JsonDocumentOptions]::new()
    $options.AllowTrailingCommas = $false
    $options.CommentHandling = [Text.Json.JsonCommentHandling]::Disallow
    $options.MaxDepth = 128
    $document = [Text.Json.JsonDocument]::Parse($text, $options)
    try {
        function Assert-NoDuplicate([Text.Json.JsonElement] $Element, [string] $Location) {
            if ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
                $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                foreach ($property in $Element.EnumerateObject()) {
                    if (-not $names.Add($property.Name)) { throw "Duplicate property '$($property.Name)' in $Location." }
                    Assert-NoDuplicate $property.Value $Location
                }
            } elseif ($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
                foreach ($item in $Element.EnumerateArray()) { Assert-NoDuplicate $item $Location }
            }
        }
        Assert-NoDuplicate $document.RootElement $Path
    } finally {
        $document.Dispose()
    }
    return [ordered]@{
        bytes = $bytes
        text = $text
        value = ($text | ConvertFrom-Json -Depth 100 -DateKind String)
        sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    }
}

function Assert-ExactSequence([object[]] $Actual, [object[]] $Expected, [string] $Name) {
    if (($Actual -join '|') -cne ($Expected -join '|')) { throw "$Name is missing, reordered, or mutated." }
}

function Assert-ExactPropertySet([object] $Value, [string[]] $Expected, [string] $Name) {
    $actual = @($Value.PSObject.Properties.Name | Sort-Object)
    $expectedSorted = @($Expected | Sort-Object)
    if (($actual -join '|') -cne ($expectedSorted -join '|')) { throw "$Name has missing or unknown properties." }
}

$matrixInput = Read-StrictJson (Resolve-InputPath $MatrixPath)
$profileInput = Read-StrictJson (Resolve-InputPath $ProfileTemplatePath)
$requestInputs = @($QualificationTemplatePath, $SourceClaimTemplatePath, $CandidateTemplatePath |
    ForEach-Object { Read-StrictJson (Resolve-InputPath $_) })
$matrix = $matrixInput.value
$profile = $profileInput.value
$requests = @($requestInputs.value)

Assert-ExactPropertySet $matrix @('schema_identity','matrix_id','status','claim_boundary','candidate_binding',
    'registry_binding','evidence_groups','cases','external_effects','review') 'WP8 matrix root'
Assert-ExactPropertySet $profile @('schema_identity','packet_id','packet_kind','status','effect_authority',
    'candidate_binding','materialization','owner_authorization','provider_intent','profile_binding',
    'native_boundary','entry_cancel','persistence_delete','deadline','canaries','execution') 'WP8 profile root'
foreach ($request in $requests) {
    Assert-ExactPropertySet $request @('schema_identity','packet_id','packet_kind','status','effect_authority',
        'candidate_binding','materialization','prerequisites','owner_authorization','billing_disclosure',
        'profile_binding','provider_profile','request_binding','fixture_oracle_binding','capability_price_binding',
        'limits','transport_boundary','canaries','execution') "WP8 request root '$($request.packet_kind)'"
}

if ($matrix.schema_identity -ne 'infinium.repository.wp8-case-requirement-matrix/1.0.0' -or
    $matrix.matrix_id -ne 'infinium.m1-s6.wp8.case-requirement-matrix/v1' -or
    $matrix.status -ne 'candidate-pre-live-review') {
    throw 'WP8 case matrix identity or candidate status is invalid.'
}
$expectedCommits = [ordered]@{
    slice5_base_commit = '5514919b8f742d00e59752fa7125da487a390926'
    wp8_baseline_commit = '63e4584f8926227c2a1e12ef31c71a3a88798c7f'
    accepted_wp4_execution_commit = '1fe62bbad155b4e9b8fc2d1056fee14a15dbc11b'
    accepted_wp4_evidence_sha256 = '3f148b76fef94c077293d863a06447bb22b395997db2b09dea291193c1598390'
    accepted_wp4_audit_commit = 'be55eda59752f884fe6e113f40927295da45f2cd'
    accepted_wp5_commit = 'fd3c80d91dd247e65b5130309a9b5bb19dd1381f'
    accepted_wp6_product_commit = 'ee0b6d31f1c1826c2af7634766155397e916c3e1'
    accepted_wp6_evidence_commit = '2b277338390f7dac37b5a5436bbe2cd81dedc871'
    accepted_wp7_product_commit = '59367a7479a7395b173b974bf720543aab2404d4'
    accepted_wp7_evidence_commit = '51251c0e0eb98d67dbc9b295b9ff084ebca33890'
}
foreach ($entry in $expectedCommits.GetEnumerator()) {
    if ([string]$matrix.candidate_binding.($entry.Key) -cne [string]$entry.Value) {
        throw "WP8 case matrix candidate binding '$($entry.Key)' is stale."
    }
}
$wp8Candidate = [string]$matrix.candidate_binding.wp8_candidate_commit
if ($RequireFrozenCandidate -and $wp8Candidate -eq 'pending-until-candidate-freeze') {
    throw 'WP8 final pre-live validation requires an exact frozen candidate commit.'
}
if ($wp8Candidate -ne 'pending-until-candidate-freeze') {
    if ($wp8Candidate -notmatch '^[0-9a-f]{40}$') { throw 'WP8 candidate binding is malformed.' }
    & git -C $repoRoot merge-base --is-ancestor $wp8Candidate HEAD
    if ($LASTEXITCODE -ne 0) { throw 'WP8 frozen candidate binding is not an ancestor of HEAD.' }
}
foreach ($commitName in @('slice5_base_commit','wp8_baseline_commit','accepted_wp4_execution_commit','accepted_wp4_audit_commit',
        'accepted_wp5_commit','accepted_wp6_product_commit','accepted_wp6_evidence_commit',
        'accepted_wp7_product_commit','accepted_wp7_evidence_commit')) {
    & git -C $repoRoot merge-base --is-ancestor ([string]$matrix.candidate_binding.$commitName) HEAD
    if ($LASTEXITCODE -ne 0) { throw "Required accepted ancestor '$commitName' is absent from HEAD." }
}

$registryPath = Resolve-InputPath ([string]$matrix.registry_binding.path)
$registryInput = Read-StrictJson $registryPath
$registry = $registryInput.value
if ($registryInput.sha256 -ne $matrix.registry_binding.sha256 -or
    $registry.schema_identity -ne $matrix.registry_binding.schema_identity -or
    $registry.registry_version -ne $matrix.registry_binding.registry_version -or
    [int64]$registry.package_count -ne [int64]$matrix.registry_binding.package_count -or
    @($registry.packages).Count -ne [int64]$registry.package_count) {
    throw 'WP8 fixture registry binding is stale or inconsistent.'
}
$registryIdentities = @($registry.packages.package_identity)
foreach ($identity in @($matrix.registry_binding.required_package_identities)) {
    if ($registryIdentities -cnotcontains $identity) { throw "Required WP8 package '$identity' is absent from the registry." }
}
foreach ($package in @($registry.packages)) {
    $authority = Resolve-InputPath ([string]$package.authority_file)
    if (-not (Test-Path -LiteralPath $authority -PathType Leaf) -or
        (Get-Item -LiteralPath $authority).Length -ne [int64]$package.authority_bytes -or
        (Get-FileHash -LiteralPath $authority -Algorithm SHA256).Hash.ToLowerInvariant() -ne [string]$package.authority_sha256) {
        throw "Registry authority file for '$($package.package_identity)' is missing or stale."
    }
}

$expectedCases = @('EVAL-0033','EVAL-0034','EVAL-0035','EVAL-0064','EVAL-0067','EVAL-0076','EVAL-0077',
    'EVAL-0081','EVAL-0083','EVAL-0089','EVAL-0026','EVAL-0037','EVAL-0038','EVAL-0039','EVAL-0040',
    'EVAL-0045','EVAL-0046','EVAL-0080','EVAL-0082','EVAL-0087','EVAL-0088','EVAL-0084','EVAL-0085')
Assert-ExactSequence @($matrix.cases.case_id) $expectedCases 'WP8 23-case inventory'
if (@($matrix.cases.case_id | Sort-Object -Unique).Count -ne 23) { throw 'WP8 case IDs are not unique.' }
foreach ($case in @($matrix.cases)) {
    if (@($case.covered_assertions).Count -eq 0 -or @($case.requirements).Count -eq 0 -or @($case.evidence_gates).Count -eq 0) {
        throw "WP8 case '$($case.case_id)' lacks requirements, gates, or covered assertions."
    }
    if ($case.classification -eq 'primary' -and (@($case.n_a_assertions).Count -ne 0 -or $case.disposition -ne 'covered-non-live')) {
        throw "Primary case '$($case.case_id)' cannot be N/A or partially dispositioned."
    }
    if ($case.classification -eq 'review-only-regression' -and (@($case.n_a_assertions).Count -ne 0 -or
            $case.disposition -ne 'mandatory-review-regression')) {
        throw "Review-only case '$($case.case_id)' must remain mandatory and cannot be N/A."
    }
    foreach ($na in @($case.n_a_assertions)) {
        foreach ($field in @('assertion_id','rationale','authority','unreachable_proof','later_authority')) {
            if ([string]::IsNullOrWhiteSpace([string]$na.$field)) { throw "Case '$($case.case_id)' has an incomplete assertion-level N/A." }
        }
        if (-not [bool]$na.no_activation) { throw "Case '$($case.case_id)' N/A would activate excluded work." }
    }
}
$requiredRequirements = @('EVID-003','EVID-006','ANALYSIS-003','ANALYSIS-004','ANALYSIS-005','ANALYSIS-016','ANALYSIS-019',
    'SNAP-001','SNAP-003','SNAP-005','COVER-001','COVER-002','COVER-003','SCAN-006','SCAN-009',
    'OPS-001','OPS-002','OPS-003','SEC-001','SEC-002','SEC-003','SEC-004','AUTH-001','AUTH-002','AUTH-003',
    'AI-003','AI-004','AI-006','AI-007','PROD-002','PROD-004')
$matrixRequirements = @($matrix.cases.requirements | Sort-Object -Unique)
foreach ($requirement in $requiredRequirements) {
    if ($matrixRequirements -cnotcontains $requirement) { throw "WP8 finite matrix omits required mapping '$requirement'." }
}
$expectedGroups = @('contract-persistence','budget','credential-helper','provider-adapter','semantic-provenance','overall')
Assert-ExactSequence @($matrix.evidence_groups.group_id) $expectedGroups 'WP8 evidence groups'

if ($profile.schema_identity -ne 'infinium.repository.wp8-production-profile-authorization-template/1.0.0' -or
    $profile.packet_id -ne 'infinium.m1-s6.wp8.pre-live-profile-authorization-template/v1' -or
    $profile.packet_kind -ne 'EnrollOrVerifyProfile' -or $profile.status -ne 'non-executable-template' -or
    $profile.effect_authority -ne 'none' -or [bool]$profile.execution.permitted -or $null -ne $profile.execution.command) {
    throw 'WP8 production profile packet is not the exact non-executable template.'
}
Assert-ExactSequence @($profile.native_boundary.new_profile_calls) @('CredWriteW','CredReadW','CredFree') 'New-profile native calls'
Assert-ExactSequence @($profile.native_boundary.existing_profile_calls) @('CredReadW','CredFree') 'Existing-profile native calls'
if ($profile.native_boundary.enumeration -ne 'prohibited' -or $profile.native_boundary.fallback -ne 'none' -or
    -not [bool]$profile.entry_cancel.masked -or -not [bool]$profile.entry_cancel.paste_permitted -or
    [bool]$profile.entry_cancel.renderer_receives_value -or -not [bool]$profile.materialization.no_inheritance) {
    throw 'WP8 production profile native, UI, or no-inheritance boundary is invalid.'
}

if ($requests.Count -ne 3) { throw 'WP8 requires exactly three distinct provider request templates.' }
$expectedRequestTuples = @(
    @('Qualification','infinium.m1-s6.wp8.pre-live-qualification-authorization-template/v1','transport-qualification',16384,20480,256,262144,140000000,60000),
    @('SourceClaimExtraction','infinium.m1-s6.wp8.pre-live-source-claim-authorization-template/v1','source-claim-extraction',65536,73728,4096,1048576,600000000,120000),
    @('CandidateInvestigation','infinium.m1-s6.wp8.pre-live-candidate-investigation-authorization-template/v1','candidate-investigation',65536,73728,4096,1048576,600000000,120000)
)
for ($index = 0; $index -lt 3; $index++) {
    $request = $requests[$index]
    $expected = $expectedRequestTuples[$index]
    if ($request.schema_identity -ne 'infinium.repository.wp8-provider-request-authorization-template/1.0.0' -or
        $request.packet_kind -ne $expected[0] -or $request.packet_id -ne $expected[1] -or
        $request.request_binding.operation -ne $expected[2] -or $request.status -ne 'non-executable-template' -or
        $request.effect_authority -ne 'none' -or [bool]$request.execution.permitted -or $null -ne $request.execution.command -or
        -not [bool]$request.materialization.no_inheritance -or [bool]$request.transport_boundary.automatic_retry -or
        $request.transport_boundary.ambiguous_start -ne 'unresolved-hold-and-no-retry' -or
        [int64]$request.transport_boundary.provider_request_maximum -ne 1 -or
        [int64]$request.limits.maximum_dispatch_count -ne 1 -or
        [int64]$request.limits.maximum_request_bytes -ne $expected[3] -or
        [int64]$request.limits.maximum_input_tokens -ne $expected[4] -or
        [int64]$request.limits.maximum_output_tokens -ne $expected[5] -or
        [int64]$request.limits.maximum_raw_response_bytes -ne $expected[6] -or
        [int64]$request.limits.maximum_calculated_nano_usd -ne $expected[7] -or
        [int64]$request.billing_disclosure.maximum_local_nano_usd -ne $expected[7] -or
        [int64]$request.limits.deadline_milliseconds -ne $expected[8]) {
        throw "WP8 request template at index $index is swapped, executable, retrying, or outside exact limits."
    }
    $p = $request.provider_profile
    if ($p.provider -ne 'openai' -or $p.endpoint -ne 'https://api.openai.com/v1/responses' -or
        $p.model -ne 'gpt-5.6-sol' -or $p.reasoning_effort -ne 'medium' -or
        $p.reasoning_context -ne 'current_turn' -or $p.reasoning_mode -ne 'standard' -or
        -not [bool]$p.structured_output_strict -or [bool]$p.store -or [bool]$p.background -or
        [bool]$p.stream -or $p.service_tier -ne 'default' -or $p.tool_choice -ne 'none' -or
        @($p.tools).Count -ne 0 -or $p.truncation -ne 'disabled' -or $p.prompt_cache_mode -ne 'explicit' -or
        $null -ne $p.prompt_cache_key -or $null -ne $p.prompt_cache_breakpoint) {
        throw "WP8 request '$($request.packet_kind)' differs from the exact M1 provider profile."
    }
}
if ($requests[1].request_binding.prompt_id -ne 'infinium.m1-s6.source-claim-prompt/v1' -or
    $requests[1].request_binding.prompt_fingerprint_sha256 -ne 'd2915f449e72d43cf697d522f2c6a1b44653dd519daba02968c1bfe3cf66ab84' -or
    $requests[1].request_binding.output_schema_sha256 -ne (Get-FileHash -LiteralPath (Resolve-InputPath $requests[1].request_binding.output_schema_path) -Algorithm SHA256).Hash.ToLowerInvariant() -or
    $requests[2].request_binding.prompt_id -ne 'infinium.m1-s6.candidate-investigation-prompt/v1' -or
    $requests[2].request_binding.prompt_fingerprint_sha256 -ne '026d7002102b74df9ef50ed2421714afa9f7b5dc717c69cadf7fb586d9c5b92e' -or
    $requests[2].request_binding.output_schema_sha256 -ne (Get-FileHash -LiteralPath (Resolve-InputPath $requests[2].request_binding.output_schema_path) -Algorithm SHA256).Hash.ToLowerInvariant()) {
    throw 'WP8 semantic request prompt or output-schema binding is stale.'
}
$allTemplateText = $profileInput.text + "`n" + ($requestInputs.text -join "`n")
if ($allTemplateText -match '(?i)bearer\s+[A-Za-z0-9._-]+' -or
    $allTemplateText -match '(?i)sk-(?:proj-)?[A-Za-z0-9_-]{8,}' -or
    $allTemplateText -match 'Infinium:[^"\s]+' -or
    $allTemplateText -match '(?i)"(api_key|secret_value|credential_target|authorization_header)"\s*:') {
    throw 'WP8 pre-live templates contain a secret, raw target, bearer value, or forbidden secret-bearing property.'
}
if (@($requests.packet_id | Sort-Object -Unique).Count -ne 3 -or
    @($requests.packet_kind | Sort-Object -Unique).Count -ne 3 -or
    @($requests.request_binding.operation | Sort-Object -Unique).Count -ne 3) {
    throw 'WP8 provider packets are not three distinct non-inheriting effects.'
}

$currentStateText = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/current-state.md') -Raw
if ($currentStateText.Contains('`M1/S6/WP8` accumulated non-live verification and pre-live review only', [StringComparison]::Ordinal) -and
    ((Test-Path -LiteralPath (Join-Path $repoRoot 'eng/run-m1-slice6-live.ps1')) -or
     (Test-Path -LiteralPath (Join-Path $repoRoot 'eng/run-m1-slice6-credential.ps1')))) {
    throw 'WP8 cannot introduce a live or production-credential execution script.'
}
foreach ($zero in @('credential_manager_operations','dns_operations','public_network_operations','provider_requests','billable_operations')) {
    if ([int64]$matrix.external_effects.$zero -ne 0) { throw "WP8 matrix records a non-zero external effect: $zero." }
}
foreach ($falseField in @('api_key_used','live_manifest_execution','private_fixture_access','archive_access')) {
    if ([bool]$matrix.external_effects.$falseField) { throw "WP8 matrix records a prohibited effect: $falseField." }
}
if ($matrix.review.self_acceptance -ne 'prohibited' -or $matrix.review.judgment -ne 'pending-fresh-independent-review' -or
    @($matrix.review.required_roles).Count -ne 6) {
    throw 'WP8 matrix does not require all six fresh independent review roles.'
}

$receipt = [ordered]@{
    schema = 'infinium.m1-s6.wp8.pre-live-validation-receipt/v1'
    status = 'passed-non-executable-templates-only'
    matrix_sha256 = $matrixInput.sha256
    case_count = @($matrix.cases).Count
    requirement_count = $matrixRequirements.Count
    evidence_group_count = @($matrix.evidence_groups).Count
    registry_sha256 = $registryInput.sha256
    registry_package_count = [int64]$registry.package_count
    profile_template_sha256 = $profileInput.sha256
    request_templates = @($requestInputs | ForEach-Object { [ordered]@{ sha256 = $_.sha256 } })
    packet_count = 4
    wp8_candidate_commit = $wp8Candidate
    execution_authorized = $false
    credential_manager_operations = 0
    dns_operations = 0
    public_network_operations = 0
    provider_requests = 0
    billable_operations = 0
    api_key_used = $false
    live_manifest_execution = $false
}
$json = $receipt | ConvertTo-Json -Depth 10
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutput = Resolve-InputPath $OutputPath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
    [IO.File]::WriteAllText($resolvedOutput, $json + "`n", [Text.UTF8Encoding]::new($false))
}
$json
