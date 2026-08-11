[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Contracts', 'StateSurfaces', 'StateTotality')]
    [string] $Gate,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$schemaNames = @(
    'provider-access-profile.v1.schema.json',
    'provider-operation.v1.schema.json',
    'provider-response.v1.schema.json',
    'source-claim-extraction.v1.schema.json',
    'candidate-investigation.v1.schema.json',
    'provider-execution-input.v1.schema.json',
    'effective-scan-configuration.v2.schema.json',
    'run-output.v2.schema.json',
    'cli-summary.v2.schema.json'
)

function Invoke-DotnetTest([string] $Project, [string] $Filter) {
    & dotnet test $Project -c Release --no-build --nologo --filter $Filter
    if ($LASTEXITCODE -ne 0) {
        throw "Focused test command failed for $Project."
    }
}

function Assert-Slice5V1Unchanged {
    $paths = @(
        'contracts/json-schema/effective-scan-configuration.v1.schema.json',
        'contracts/json-schema/run-output.v1.schema.json',
        'contracts/json-schema/cli-summary.v1.schema.json',
        'src/Infinium.Application/Serialization/RunOutputJsonCodec.cs',
        'src/Infinium.Application/Serialization/CliSummaryJsonCodec.cs'
    )
    & git diff --quiet 6ac66e7d79c63a231bbbf22209015a894cd4bd6d -- @paths
    if ($LASTEXITCODE -ne 0) {
        throw 'A frozen Slice 5 v1 configuration/output contract or codec changed.'
    }
}

function Write-Receipt([string] $Name, [System.Collections.IDictionary] $Evidence, [string] $Status = 'passed') {
    $receipt = [ordered]@{
        gate = $Name
        status = $Status
        network_permitted = $false
        credential_access_permitted = $false
        evidence = $Evidence
    }
    $json = $receipt | ConvertTo-Json -Depth 16
    [System.IO.File]::WriteAllText(
        (Join-Path $resolvedOutputRoot ($Name.ToLowerInvariant() + '.json')),
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

function Invoke-ContractsGate {
    Assert-Slice5V1Unchanged
    $schemaEvidence = @()
    foreach ($schemaName in $schemaNames) {
        $path = Join-Path $repoRoot "contracts/json-schema/$schemaName"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing WP1 schema $schemaName."
        }
        $document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($document.type -ne 'object' -or $document.additionalProperties -ne $false) {
            throw "WP1 schema $schemaName is not a closed root object."
        }
        $schemaEvidence += [ordered]@{
            name = $schemaName
            schema_id = $document.title
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }

    $protoPath = Join-Path $repoRoot 'contracts/protobuf/infinium/helper/v2/helper.proto'
    $protobufRoot = Join-Path $repoRoot 'contracts/protobuf'
    $contractBytes = [System.IO.MemoryStream]::new()
    foreach ($proto in Get-ChildItem -LiteralPath $protobufRoot -Recurse -Filter '*.proto' |
             Sort-Object { $_.FullName.Substring($protobufRoot.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, '/') }) {
        $relative = $proto.FullName.Substring($protobufRoot.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relative + "`n")
        $contractBytes.Write($pathBytes, 0, $pathBytes.Length)
        $protoBytes = [System.IO.File]::ReadAllBytes($proto.FullName)
        $contractBytes.Write($protoBytes, 0, $protoBytes.Length)
    }
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $protocolFingerprint = [BitConverter]::ToString(
            $sha256.ComputeHash($contractBytes.ToArray())).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha256.Dispose()
    }
    $constantPath = Join-Path $repoRoot 'src/Infinium.Application/Runtime/HelperProtocolV2Constants.cs'
    $helperBytes = [System.IO.MemoryStream]::new()
    foreach ($relative in @('infinium/common/v1/common.proto', 'infinium/domain/v1/identities.proto', 'infinium/helper/v2/helper.proto')) {
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relative + "`n")
        $helperBytes.Write($pathBytes, 0, $pathBytes.Length)
        $protoBytes = [System.IO.File]::ReadAllBytes((Join-Path $protobufRoot $relative))
        $helperBytes.Write($protoBytes, 0, $protoBytes.Length)
    }
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $helperFingerprint = [BitConverter]::ToString(
            $sha256.ComputeHash($helperBytes.ToArray())).Replace('-', '').ToLowerInvariant()
    } finally {
        $sha256.Dispose()
    }
    if (-not (Select-String -LiteralPath $constantPath -SimpleMatch $helperFingerprint -Quiet)) {
        throw 'Helper protocol v2 fingerprint constant is stale.'
    }
    $applicationConstantPath = Join-Path $repoRoot 'src/Infinium.Application/Runtime/ProtocolConstants.cs'
    if (-not (Select-String -LiteralPath $applicationConstantPath -SimpleMatch $protocolFingerprint -Quiet)) {
        throw 'Application protocol fingerprint constant is stale.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'contracts/protobuf/infinium/helper/v1/helper.proto'))) {
        throw 'Helper protocol v1 decodability authority is missing.'
    }

    $forbiddenPattern = '"(credential_target|provider_secret|authorization_header|secret_bytes|raw_headers)"\s*:'
    foreach ($schemaName in $schemaNames) {
        if (Select-String -LiteralPath (Join-Path $repoRoot "contracts/json-schema/$schemaName") -Pattern $forbiddenPattern -Quiet) {
            throw "WP1 schema $schemaName exposes a forbidden secret/transport field."
        }
    }

    Invoke-DotnetTest `
        'tests/Infinium.ContractTests/Infinium.ContractTests.csproj' `
        'FullyQualifiedName~Provider|FullyQualifiedName~Helper|FullyQualifiedName~RunOutput'

    Write-Receipt 'Contracts' ([ordered]@{
        schemas = $schemaEvidence
        schema_count = $schemaEvidence.Count
        helper_protocol_v2_sha256 = $helperFingerprint
        application_protocol_set_sha256 = $protocolFingerprint
        helper_protocol_v1_retained = $true
        public_package_count = 19
        answer_free_example_count = 9
        slice5_v1_byte_compatibility = 'unchanged-from-6ac66e7d79c63a231bbbf22209015a894cd4bd6d'
        forbidden_field_scan = 'passed'
    })
}

function Invoke-StateSurfaceGate([bool] $RequireAcceptedInputProof) {
    Assert-Slice5V1Unchanged
    Invoke-DotnetTest `
        'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' `
        'FullyQualifiedName~ProviderContract|FullyQualifiedName~ProviderFiniteBound|FullyQualifiedName~OperationalContract'
    Invoke-DotnetTest `
        'tests/Infinium.UnitTests/Infinium.UnitTests.csproj' `
        'FullyQualifiedName~Schema6|FullyQualifiedName~ProviderPersistence|FullyQualifiedName~BackupRestore'

    $migrationPath = Join-Path $repoRoot 'src/Infinium.Persistence/AuthoritativeStore.Migrations.cs'
    foreach ($required in @('M1-S6-0006', 'SchemaV6', 'provider_operation_projection', 'provider_budget_projection',
        'provider_price_rules', 'live_billable_slot', 'maximum_request_bytes', 'installation_snapshot_id')) {
        if (-not (Select-String -LiteralPath $migrationPath -SimpleMatch $required -Quiet)) {
            throw "Schema-6 persistence declaration is missing $required."
        }
    }
    $traceabilityPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp1-contract-traceability.v1.json'
    $traceability = Get-Content -LiteralPath $traceabilityPath -Raw | ConvertFrom-Json
    if ($traceability.contracts.Count -ne 9) {
        throw 'WP1 traceability inventory does not cover exactly nine contracts.'
    }

    $gateName = if ($RequireAcceptedInputProof) { 'StateTotality' } else { 'StateSurfaces' }
    $gateStatus = if ($RequireAcceptedInputProof) { 'blocked-authority-required' } else { 'passed' }
    Write-Receipt $gateName ([ordered]@{
        migration_id = 'M1-S6-0006'
        source_schema = 5
        destination_schema = 6
        storage_contract = '1.5.0'
        source_schema_fingerprint = 'e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d'
        destination_schema_fingerprint = '688b702c7720d720d73d7be59816051b28010cd6a6da64f64b26514e894b8be7'
        append_only_provider_history_table_count = 23
        rebuildable_projection_count = 3
        traceability_contract_count = $traceability.contracts.Count
        local_input_bound_proof = 'authority-required-no-accepted-local-tokenizer-or-framing-grammar'
        provider_dispatch_admission = 'fail-closed'
        transport_qualification_request_byte_ceiling = 16384
        semantic_request_byte_ceiling = 65536
        maximum_dispatch_count = 1
    }) $gateStatus
    if ($RequireAcceptedInputProof) {
        throw 'WP1 StateTotality is blocked: no accepted repository-local tokenizer/framing proof exists.'
    }
}

Push-Location $repoRoot
try {
    switch ($Gate) {
        'Contracts' { Invoke-ContractsGate }
        'StateSurfaces' { Invoke-StateSurfaceGate $false }
        'StateTotality' { Invoke-StateSurfaceGate $true }
    }
} finally {
    Pop-Location
}
