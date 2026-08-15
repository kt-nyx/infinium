[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [ValidateSet('EnrollOrVerifyProfile')] [string] $Operation,
    [Parameter(Mandatory = $true)] [string] $AuthorizationManifest,
    [Parameter(Mandatory = $true)] [string] $OutputRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = (Resolve-Path -LiteralPath $AuthorizationManifest).Path
$expectedManifestPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
if ($manifestPath -cne $expectedManifestPath) { throw 'Only the exact WP9 production-profile manifest path is executable.' }
$validation = & (Join-Path $PSScriptRoot 'validate-m1-slice6-wp9-profile-authorization.ps1') `
    -AuthorizationManifest $manifestPath -RequireReady | ConvertFrom-Json
$m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 100 -DateKind String
if ([DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse($m.expires_at_utc, [Globalization.CultureInfo]::InvariantCulture)) {
    throw 'The exact WP9 production-profile manifest has expired.'
}

$branch = (& git -C $repoRoot branch --show-current).Trim()
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
$status = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0 -or $branch -cne 'codex/m1-s6' -or $status.Count -ne 0) {
    throw 'WP9 profile execution requires the exact clean codex/m1-s6 candidate.'
}
$closeReady = [string]$m.candidate_binding.close_ready_implementation_commit
& git -C $repoRoot merge-base --is-ancestor $closeReady $head
if ($LASTEXITCODE -ne 0) { throw 'The WP9 close-ready implementation is not an ancestor of the exact execution candidate.' }
$allowedPostClose = @(
    'docs/current-state.md',
    'docs/plans/milestones/m1/slices/s6/README.md',
    'docs/plans/milestones/m1/slices/s6/record.md',
    'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json'
)
$postClose = @(& git -C $repoRoot diff --name-only "$closeReady..$head")
if (@($postClose | Where-Object { $_ -notin $allowedPostClose }).Count -ne 0) {
    throw 'Code or unapproved documentation drift exists after the exact WP9 close-ready implementation.'
}

$manifestSha = [string]$validation.manifest_sha256
$canonical = "WP9_PROFILE_OWNER_ACCEPTANCE manifest_id=$($m.manifest_id) sha256=$manifestSha close_ready_commit=$closeReady expires_at_utc=$($m.expires_at_utc)"
$recordPath = Join-Path $repoRoot 'docs/plans/milestones/m1/slices/s6/record.md'
$acceptanceLines = @([IO.File]::ReadAllLines($recordPath) | Where-Object {
    $_.StartsWith('WP9_PROFILE_OWNER_ACCEPTANCE ', [StringComparison]::Ordinal)
})
if ($acceptanceLines.Count -ne 1 -or $acceptanceLines[0] -cne $canonical) {
    throw 'WP9 profile execution requires exactly one canonical owner-acceptance line for these exact manifest bytes.'
}

$expectedOutput = Join-Path $repoRoot ([string]$m.output.output_root_relative -replace '/', [IO.Path]::DirectorySeparatorChar)
$resolvedOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $OutputRoot))
if ($resolvedOutput -cne $expectedOutput -or [IO.Directory]::Exists($resolvedOutput) -or [IO.File]::Exists($resolvedOutput)) {
    throw 'WP9 profile output must be the exact fresh absent manifest-bound root.'
}
$stateRoot = Join-Path $repoRoot ([string]$m.durable_state.product_state_root_relative -replace '/', [IO.Path]::DirectorySeparatorChar)
if ([IO.Directory]::Exists($stateRoot) -or [IO.File]::Exists($stateRoot)) {
    throw 'The new-only WP9 production profile state root already exists; verification/replacement is not authorized.'
}

$coordinator = Join-Path $repoRoot 'src/Infinium.Coordinator/bin/Debug/net10.0/Infinium.Coordinator.exe'
if (-not (Test-Path -LiteralPath $coordinator -PathType Leaf)) {
    throw 'The exact repository-built Coordinator executable is absent. Build and revalidate before owner-authorized execution.'
}
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$lockPath = Join-Path $resolvedOutput 'authority-lock.json'
$lock = [ordered]@{
    schema = 'infinium.m1-s6.wp9.profile-authority-lock/v1'
    manifest_id = [string]$m.manifest_id
    manifest_sha256 = $manifestSha
    close_ready_implementation_commit = $closeReady
    execution_candidate_commit = $head
    consumed_at_utc = [DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
    retry_permitted = $false
}
[IO.File]::WriteAllText($lockPath, ($lock | ConvertTo-Json -Depth 10) + "`n", [Text.UTF8Encoding]::new($false))

& $coordinator --wp9-production-profile-enrollment --manifest $manifestPath `
    --manifest-sha256 $manifestSha --output-root $resolvedOutput --product-root $stateRoot
if ($LASTEXITCODE -ne 0) { throw "WP9 profile enrollment stopped with typed exit code $LASTEXITCODE; no retry is authorized." }
