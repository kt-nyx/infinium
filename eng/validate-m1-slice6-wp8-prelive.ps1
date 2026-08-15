[CmdletBinding()]
param(
    [string] $MatrixPath = 'docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json',
    [string] $ProfileTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json',
    [string] $QualificationTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json',
    [string] $SourceClaimTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json',
    [string] $CandidateTemplatePath = 'docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json',
    [string] $OutputPath,
    [switch] $RequireFrozenCandidate,
    [switch] $RequireAcceptedHistoricalEvidence
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $repoRoot 'eng/wp9-owner-documentation-contract.ps1')

function Resolve-InputPath([string] $Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $repoRoot $Value))
}

function Test-Wp8ExactPathSet([string[]] $Actual, [string[]] $Expected) {
    $actualUnique = @($Actual | Sort-Object -Unique)
    $expectedUnique = @($Expected | Sort-Object -Unique)
    if ($actualUnique.Count -ne @($Actual).Count -or $expectedUnique.Count -ne @($Expected).Count -or
        $actualUnique.Count -ne $expectedUnique.Count) {
        return $false
    }
    return (($actualUnique -join "`n") -ceq ($expectedUnique -join "`n"))
}

function Get-Wp8Wp9OwnerStopPaths() {
    return @(
        'Directory.Build.targets',
        'contracts/repository/wp9-production-profile-authorization.v1.schema.json',
        'docs/current-state.md',
        'docs/plans/milestones/m1/slices/s6/README.md',
        'docs/plans/milestones/m1/slices/s6/record.md',
        'docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json',
        'eng/run-m1-slice6-credential.ps1',
        'eng/validate-m1-slice6-wp4-recovery-4936dcef.ps1',
        'eng/validate-m1-slice6-wp4-recovery-e3f76cd6.ps1',
        'eng/validate-m1-slice6-wp4-recovery-e6e04651.ps1',
        'eng/validate-m1-slice6-wp8-prelive.ps1',
        'eng/validate-m1-slice6-wp9-profile-authorization.ps1',
        'eng/verify-m1-slice6.ps1',
        'eng/wp9-owner-documentation-contract.ps1',
        'src/Infinium.Application/Runtime/NativeHelperFailureProtocol.cs',
        'src/Infinium.Coordinator/CredentialHelperCoordinator.cs',
        'src/Infinium.Coordinator/CredentialNativeQualificationSupervisor.cs',
        'src/Infinium.Coordinator/OneShotCredentialHelperLauncher.cs',
        'src/Infinium.Coordinator/Program.cs',
        'src/Infinium.Coordinator/Wp9ProductionProfileEnrollmentRunner.cs',
        'src/Infinium.CredentialHelper/OneShotHelperEngine.cs',
        'src/Infinium.CredentialHelper/Program.cs',
        'src/Infinium.CredentialHelper/WindowsCredentialNativeQualification.cs',
        'src/Infinium.CredentialHelper/Wp9ProductionEnrollmentSurface.cs',
        'tests/Infinium.ContractTests/ProviderLayer6VerifierContractTests.cs',
        'tests/Infinium.ContractTests/Wp8PreLiveReadinessContractTests.cs',
        'tests/Infinium.IntegrationTests/CredentialHelperIntegrationTests.cs',
        'tests/Infinium.IntegrationTests/Wp9ProductionEnrollmentEvidenceTests.cs',
        'tests/Infinium.UnitTests/CredentialNativeAuthorizationTests.cs',
        'tests/Infinium.UnitTests/Wp9ProductionProfileAuthorizationTests.cs')
}

function Test-Wp8CorrectionCurrentState([string] $Text) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    if ($normalized.Contains('WP8 is independently accepted at exact', [StringComparison]::Ordinal) -or
        $normalized.Contains('The only next eligible action is an owner decision and exact packet-materialization planning for WP9', [StringComparison]::Ordinal)) {
        return $false
    }
    foreach ($required in @(
            '| Current authorized work | `M1/S6/WP8` closeout correction and complete non-live reverification only; WP9 is not eligible. |',
            '| Next eligible action | Freeze the corrected WP8 verification candidate, bind its non-executable templates, then run the complete non-live floor and fresh independent review; do not begin WP9 |',
            '| Later work | WP9 remains ineligible until the corrected WP8 evidence is independently accepted and an exact no-effect closeout is committed. No prior WP8 acceptance or template grants inherited authority |',
            'was later invalidated as current handoff authority by current-HEAD review and remains historical evidence only.',
            'Only WP8 closeout correction and complete non-live reverification are eligible; WP9 remains ineligible.',
            'No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',
            'No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp8CorrectionReadme([string] $Text) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    if ($normalized.Contains('WP8 is independently accepted at exact', [StringComparison]::Ordinal) -or
        $normalized.Contains('The only next eligible action is an owner decision and exact packet-materialization planning for WP9', [StringComparison]::Ordinal)) {
        return $false
    }
    foreach ($required in @(
            'WP8 closeout correction and complete non-live reverification are active.',
            'WP9 is not eligible',
            'The earlier WP8 acceptance identities and receipts are retained only as superseded historical evidence and do not certify the corrected candidate.',
            'No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority',
            'No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp8AcceptedHandoffCurrentState([string] $Text, [object] $Binding) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    foreach ($required in @(
            '| Current authorized work | `M1/S6/WP9` owner decision and exact authorization-packet materialization planning only; corrected WP8 is accepted. No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. |',
            'Accepted corrected `M1/S6/WP8` candidate',
            [string]$Binding.verification_candidate_commit,
            [string]$Binding.post_run_evidence_candidate_commit,
            [string]$Binding.non_live_all_receipt_sha256,
            [string]$Binding.pre_live_receipt_sha256,
            [string]$Binding.direct_layer6_receipt_sha256,
            '| Next eligible action | Owner decision whether to begin `M1/S6/WP9` materialization planning under accepted plan section 20; only fresh exact production-profile and WP9 request authorizations may be prepared, and neither may be executed without separate exact owner acceptance |',
            'No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority',
            'No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp8AcceptedHandoffReadme([string] $Text, [object] $Binding) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    foreach ($required in @(
            'Corrected WP8 is independently accepted.',
            [string]$Binding.verification_candidate_commit,
            [string]$Binding.post_run_evidence_candidate_commit,
            [string]$Binding.non_live_all_receipt_sha256,
            [string]$Binding.pre_live_receipt_sha256,
            [string]$Binding.direct_layer6_receipt_sha256,
            'The next eligible action is only the owner''s decision whether to begin WP9 fresh exact authorization-packet materialization planning',
            'No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority',
            'No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp9OwnerStopCurrentState([string] $Text, [object] $Binding) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    foreach ($required in @(
            '| Current authorized work | `M1/S6/WP9` non-effectful production-profile preparation verification and independent review only. Corrected close-ready implementation `',
            'infinium.m1-s6.wp9.production-profile-authorization/ded946a6-e1b8-4c8e-95eb-5ef59619804f',
            'is bound by manifest',
            'but no exact replacement independent-review or owner-acceptance record exists yet.',
            'Accepted corrected `M1/S6/WP8` candidate',
            [string]$Binding.verification_candidate_commit,
            [string]$Binding.post_run_evidence_candidate_commit,
            [string]$Binding.non_live_all_receipt_sha256,
            [string]$Binding.pre_live_receipt_sha256,
            [string]$Binding.direct_layer6_receipt_sha256,
            '| Next eligible action | Run the complete non-live floor and fresh independent security/semantic/diff review against the exact corrected manifest binding. Only an accepted exact reviewed candidate may then reach the owner accept-or-decline stop.',
            'The transport-qualification request manifest remains unmaterialized and blocked pending separate `safety_identifier` authority resolution plus successful profile enrollment.',
            'No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority',
            'No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp9OwnerStopReadme([string] $Text, [object] $Binding) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    foreach ($required in @(
            'Corrected WP8 is independently accepted.',
            [string]$Binding.verification_candidate_commit,
            [string]$Binding.post_run_evidence_candidate_commit,
            [string]$Binding.non_live_all_receipt_sha256,
            [string]$Binding.pre_live_receipt_sha256,
            [string]$Binding.direct_layer6_receipt_sha256,
            'WP9 non-effectful production-profile preparation is frozen at corrected close-ready implementation',
            'The canonical non-incremental Release build pins both informational-version and SourceLink revision identities to that exact commit.',
            'Two consecutive clean builds reproduced the coordinator, helper, and complete 126-file execution closure exactly.',
            'No corrected independent-review or owner-acceptance record exists, and WP9 execution remains ineligible.',
            'The transport-qualification request manifest is not materialized.',
            'No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority.',
            'No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp9ReviewCloseoutCorrectionCurrentState([string] $Text) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    $fixtureCorrection = $true
    foreach ($required in @(
            '| Current authorized work | `M1/S6/WP9` bounded non-effectful review-closeout fixture correction and reverification only.',
            'The B16 reviewed-pending-owner transition exposed a state-dependent positive closeout fixture.',
            'Its exact review marker is retained as superseded historical evidence and grants no owner or execution authority.',
            'Owner acceptance and WP9 execution are ineligible.',
            '| Next eligible action | Make the exact closeout fixture state-aware for both pre-review and reviewed current HEAD, prove duplicate-marker and protected-path rejection, then refreeze, rebind, rerun the complete non-live floor, and obtain fresh independent review. |',
            'No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',
            'No packet, review, or prior owner statement grants inherited authority.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { $fixtureCorrection = $false; break }
    }
    if ($fixtureCorrection) { return $true }
    foreach ($required in @(
            '| Current authorized work | `M1/S6/WP9` bounded non-effectful review-closeout correction and reverification only.',
            'The B14 reviewed-pending-owner transition exposed stale retained-WP8 and Layer 6 closeout predicates.',
            'Its exact review marker is retained as superseded historical evidence and grants no owner or execution authority.',
            'Owner acceptance and WP9 execution are ineligible.',
            '| Next eligible action | Correct and mutation-test both exact WP9 owner-stop states plus a dedicated three-document review-closeout Layer 6 mode; then refreeze, rebind, and rerun the complete non-live floor and fresh independent review. |',
            'No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',
            'No packet, review, or prior owner statement grants inherited authority.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp9ReviewCloseoutCorrectionReadme([string] $Text) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    $fixtureCorrection = $true
    foreach ($required in @(
            'WP9 review-closeout fixture correction and complete non-live reverification are active; owner acceptance and execution are ineligible.',
            'The exact B16 review marker remains append-only superseded historical evidence and grants no authority.',
            'The replacement fixture resolves the unique current matching review marker to its recorded baseline, uses current HEAD when no matching marker exists, and rejects duplicate markers and any protected fourth path.',
            'No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS or public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. No authority is inherited.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { $fixtureCorrection = $false; break }
    }
    if ($fixtureCorrection) { return $true }
    foreach ($required in @(
            'WP9 review-closeout correction and complete non-live reverification are active; owner acceptance and execution are ineligible.',
            'The exact B14 review marker remains append-only superseded historical evidence and grants no authority.',
            'A replacement must support both the exact pre-review owner-stop state and exact reviewed-pending-owner state, plus a dedicated exact three-document Layer 6 closeout.',
            'No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS or public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. No authority is inherited.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp9ReviewedOwnerPendingState(
    [string] $CurrentStateText,
    [string] $ReadmeText,
    [string] $ReviewedRecordText,
    [string] $HeadRecordText,
    [object] $ReviewBinding) {
    if ($null -eq $ReviewBinding -or
        [string]$ReviewBinding.manifest_id -notmatch '^infinium\.m1-s6\.wp9\.production-profile-authorization/' -or
        [string]$ReviewBinding.manifest_sha256 -notmatch '^[0-9a-f]{64}$' -or
        [string]$ReviewBinding.close_ready_commit -notmatch '^[0-9a-f]{40}$' -or
        [string]$ReviewBinding.reviewed_candidate_commit -notmatch '^[0-9a-f]{40}$') {
        return $false
    }
    $requirements = Get-Wp9ReviewedOwnerPendingDocumentationRequirements `
        -ManifestId ([string]$ReviewBinding.manifest_id) `
        -ManifestSha256 ([string]$ReviewBinding.manifest_sha256) `
        -CloseReadyCommit ([string]$ReviewBinding.close_ready_commit) `
        -ReviewedCandidate ([string]$ReviewBinding.reviewed_candidate_commit)
    return (Test-Wp9DocumentationRequirements -CurrentStateText $CurrentStateText `
            -ReadmeText $ReadmeText -Requirements $requirements) -and
        (Test-Wp9ReviewedOwnerPendingRecord -ReviewedRecordText $ReviewedRecordText `
            -CurrentRecordText $HeadRecordText -ManifestId ([string]$ReviewBinding.manifest_id) `
            -ManifestSha256 ([string]$ReviewBinding.manifest_sha256) `
            -ReviewedCandidate ([string]$ReviewBinding.reviewed_candidate_commit))
}

function Test-Wp9OwnerAcceptedState(
    [string] $CurrentStateText,
    [string] $ReadmeText,
    [string] $ReviewedRecordText,
    [string] $HeadRecordText,
    [object] $ReviewBinding) {
    if ($null -eq $ReviewBinding -or
        [string]$ReviewBinding.manifest_id -notmatch '^infinium\.m1-s6\.wp9\.production-profile-authorization/' -or
        [string]$ReviewBinding.manifest_sha256 -notmatch '^[0-9a-f]{64}$' -or
        [string]$ReviewBinding.close_ready_commit -notmatch '^[0-9a-f]{40}$' -or
        [string]$ReviewBinding.expires_at_utc -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$' -or
        [string]$ReviewBinding.reviewed_candidate_commit -notmatch '^[0-9a-f]{40}$') {
        return $false
    }
    $requirements = Get-Wp9OwnerAcceptedDocumentationRequirements `
        -ManifestId ([string]$ReviewBinding.manifest_id) `
        -ManifestSha256 ([string]$ReviewBinding.manifest_sha256) `
        -CloseReadyCommit ([string]$ReviewBinding.close_ready_commit) `
        -ReviewedCandidate ([string]$ReviewBinding.reviewed_candidate_commit)
    return (Test-Wp9DocumentationRequirements -CurrentStateText $CurrentStateText `
            -ReadmeText $ReadmeText -Requirements $requirements) -and
        (Test-Wp9OwnerAcceptedRecord -ReviewedRecordText $ReviewedRecordText `
            -CurrentRecordText $HeadRecordText -ManifestId ([string]$ReviewBinding.manifest_id) `
            -ManifestSha256 ([string]$ReviewBinding.manifest_sha256) `
            -CloseReadyCommit ([string]$ReviewBinding.close_ready_commit) `
            -ExpiresAtUtc ([string]$ReviewBinding.expires_at_utc) `
            -ReviewedCandidate ([string]$ReviewBinding.reviewed_candidate_commit))
}

function Test-Wp9OwnerAcceptanceCloseoutCorrectionCurrentState([string] $Text) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    foreach ($required in @(
            '| Current authorized work | `M1/S6/WP9` bounded non-effectful owner-acceptance closeout correction and reverification only.',
            'The exact owner marker at `b64353f0f5a843fce7c1c395a606c47e62d274ee` is retained as superseded historical evidence and grants no current owner or execution authority.',
            'Owner acceptance and WP9 execution are ineligible.',
            '| Next eligible action | Add an exact owner-accepted state predicate and dedicated three-document owner-acceptance Layer 6 closeout, mutation-test both, then refreeze, rebind, rerun the complete non-live floor, and obtain fresh independent review. |',
            'No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',
            'No packet, review, or prior owner statement grants inherited authority.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp9OwnerAcceptanceCloseoutCorrectionReadme([string] $Text) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    foreach ($required in @(
            'WP9 owner-acceptance closeout correction and complete non-live reverification are active; owner acceptance and execution are ineligible.',
            'The exact owner marker at `b64353f0f5a843fce7c1c395a606c47e62d274ee` remains append-only superseded historical evidence and grants no authority.',
            'The replacement must recognize only the exact owner-accepted documents and canonical review-plus-owner record, plus a dedicated exact three-document Layer 6 closeout.',
            'No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS or public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. No authority is inherited.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Test-Wp8RetainedAcceptanceRecord([string] $Text, [object] $Binding) {
    $normalized = [regex]::Replace($Text, '\s+', ' ')
    foreach ($required in @(
            'Corrected WP8 independent acceptance and handoff',
            '| contract-persistence | `ACCEPT` |',
            '| budget-settlement-faults | `ACCEPT` |',
            '| credential-helper-security | `ACCEPT` |',
            '| provider-adapter-offline-safety | `ACCEPT` |',
            '| source-candidate-semantics-provenance | `ACCEPT` |',
            '| overall-matrix-claims-diff | `ACCEPT` |',
            [string]$Binding.verification_candidate_commit,
            [string]$Binding.post_run_evidence_candidate_commit,
            [string]$Binding.non_live_all_receipt_sha256,
            [string]$Binding.pre_live_receipt_sha256,
            [string]$Binding.direct_layer6_receipt_sha256,
            'No separate reviewer-judgment artifact or hash was created or required.')) {
        if (-not $normalized.Contains($required, [StringComparison]::Ordinal)) { return $false }
    }
    return $true
}

function Get-Wp8PostVerificationDisposition(
    [string[]] $Paths,
    [string] $CurrentStateText,
    [string] $ReadmeText,
    [string] $VerificationRecordText,
    [string] $HeadRecordText,
    [object] $AcceptanceBinding,
    [object] $Wp9ReviewBinding) {
    if ($null -eq $Wp9ReviewBinding) {
        $Wp9ReviewBinding = [pscustomobject]@{
            manifest_id = ''; manifest_sha256 = ''; close_ready_commit = ''
            expires_at_utc = ''; reviewed_candidate_commit = ''; reviewed_record_text = ''; closeout_paths = @()
        }
    }
    $bindingPaths = @(
        'docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json',
        'docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json')
    $closeoutPaths = @($bindingPaths) + @(
        'docs/current-state.md',
        'docs/plans/milestones/m1/slices/s6/README.md',
        'docs/plans/milestones/m1/slices/s6/record.md')
    $verificationState = $AcceptanceBinding.state -eq 'correction-verification-pending' -and
        (Test-Wp8CorrectionCurrentState $CurrentStateText) -and
        (Test-Wp8CorrectionReadme $ReadmeText)
    $acceptedState = $AcceptanceBinding.state -eq 'accepted-closeout' -and
        (Test-Wp8AcceptedHandoffCurrentState $CurrentStateText $AcceptanceBinding) -and
        (Test-Wp8AcceptedHandoffReadme $ReadmeText $AcceptanceBinding) -and
        (Test-Wp8RetainedAcceptanceRecord $HeadRecordText $AcceptanceBinding)
    $wp9OwnerStopPaths = @(Get-Wp8Wp9OwnerStopPaths)
    $wp9OwnerStopState = $AcceptanceBinding.state -eq 'accepted-closeout' -and
        (Test-Wp9OwnerStopCurrentState $CurrentStateText $AcceptanceBinding) -and
        (Test-Wp9OwnerStopReadme $ReadmeText $AcceptanceBinding) -and
        (Test-Wp8RetainedAcceptanceRecord $HeadRecordText $AcceptanceBinding)
    $wp9ReviewCorrectionState = $AcceptanceBinding.state -eq 'accepted-closeout' -and
        (Test-Wp9ReviewCloseoutCorrectionCurrentState $CurrentStateText) -and
        (Test-Wp9ReviewCloseoutCorrectionReadme $ReadmeText) -and
        (Test-Wp8RetainedAcceptanceRecord $HeadRecordText $AcceptanceBinding)
    $wp9ReviewedOwnerPendingState = $AcceptanceBinding.state -eq 'accepted-closeout' -and
        (Test-Wp8RetainedAcceptanceRecord $HeadRecordText $AcceptanceBinding) -and
        (Test-Wp9ReviewedOwnerPendingState $CurrentStateText $ReadmeText `
            ([string]$Wp9ReviewBinding.reviewed_record_text) $HeadRecordText $Wp9ReviewBinding)
    $wp9OwnerAcceptedState = $AcceptanceBinding.state -eq 'accepted-closeout' -and
        (Test-Wp8RetainedAcceptanceRecord $HeadRecordText $AcceptanceBinding) -and
        (Test-Wp9OwnerAcceptedState $CurrentStateText $ReadmeText `
            ([string]$Wp9ReviewBinding.reviewed_record_text) $HeadRecordText $Wp9ReviewBinding)
    $wp9OwnerAcceptanceCorrectionState = $AcceptanceBinding.state -eq 'accepted-closeout' -and
        (Test-Wp9OwnerAcceptanceCloseoutCorrectionCurrentState $CurrentStateText) -and
        (Test-Wp9OwnerAcceptanceCloseoutCorrectionReadme $ReadmeText) -and
        (Test-Wp8RetainedAcceptanceRecord $HeadRecordText $AcceptanceBinding)

    if (@($Paths).Count -eq 0 -or (Test-Wp8ExactPathSet $Paths $bindingPaths)) {
        if ($verificationState) { return 'exact-correction-verification-state' }
        return 'invalid'
    }
    if (Test-Wp8ExactPathSet $Paths $closeoutPaths) {
        if (-not $acceptedState -or
            -not $HeadRecordText.StartsWith($VerificationRecordText, [StringComparison]::Ordinal) -or
            $HeadRecordText.Length -le $VerificationRecordText.Length) {
            return 'invalid'
        }
        return 'exact-accepted-append-only-handoff'
    }
    if (Test-Wp8ExactPathSet $Paths $wp9OwnerStopPaths) {
        if (-not $HeadRecordText.StartsWith($VerificationRecordText, [StringComparison]::Ordinal) -or
            $HeadRecordText.Length -le $VerificationRecordText.Length) {
            return 'invalid'
        }
        if ($wp9OwnerStopState) { return 'exact-wp9-owner-stop-no-effect-state' }
        if ($wp9ReviewCorrectionState) { return 'exact-wp9-review-closeout-correction-no-effect-state' }
        $reviewCloseoutPaths = @(
            'docs/current-state.md',
            'docs/plans/milestones/m1/slices/s6/README.md',
            'docs/plans/milestones/m1/slices/s6/record.md')
        if ($wp9ReviewedOwnerPendingState -and
            (Test-Wp8ExactPathSet @($Wp9ReviewBinding.closeout_paths) $reviewCloseoutPaths)) {
            return 'exact-wp9-reviewed-owner-pending-no-effect-state'
        }
        if ($wp9OwnerAcceptedState -and
            (Test-Wp8ExactPathSet @($Wp9ReviewBinding.closeout_paths) $reviewCloseoutPaths)) {
            return 'exact-wp9-owner-accepted-bounded-effect-state'
        }
        if ($wp9OwnerAcceptanceCorrectionState) {
            return 'exact-wp9-owner-acceptance-closeout-correction-no-effect-state'
        }
        return 'invalid'
    }
    return 'invalid'
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

function Resolve-LocalSchemaReference(
    [Text.Json.JsonElement] $SchemaRoot,
    [string] $Reference) {
    if (-not $Reference.StartsWith('#/', [StringComparison]::Ordinal)) {
        throw "Only local repository-schema references are supported: $Reference."
    }
    $resolved = $SchemaRoot
    foreach ($segment in $Reference.Substring(2).Split('/')) {
        $name = $segment.Replace('~1', '/').Replace('~0', '~')
        $next = [Text.Json.JsonElement]::new()
        if (-not $resolved.TryGetProperty($name, [ref]$next)) {
            throw "Repository schema reference '$Reference' is unresolved at '$name'."
        }
        $resolved = $next
    }
    return $resolved
}

function Assert-JsonSchemaNode(
    [Text.Json.JsonElement] $Instance,
    [Text.Json.JsonElement] $Schema,
    [Text.Json.JsonElement] $SchemaRoot,
    [string] $Location) {
    $referenceElement = [Text.Json.JsonElement]::new()
    $typeElement = [Text.Json.JsonElement]::new()
    $constElement = [Text.Json.JsonElement]::new()
    $enumElement = [Text.Json.JsonElement]::new()
    $requiredElement = [Text.Json.JsonElement]::new()
    $propertiesElement = [Text.Json.JsonElement]::new()
    $additionalElement = [Text.Json.JsonElement]::new()
    $propertySchema = [Text.Json.JsonElement]::new()
    $minItems = [Text.Json.JsonElement]::new()
    $maxItems = [Text.Json.JsonElement]::new()
    $uniqueItems = [Text.Json.JsonElement]::new()
    $itemSchema = [Text.Json.JsonElement]::new()
    $minLength = [Text.Json.JsonElement]::new()
    $pattern = [Text.Json.JsonElement]::new()
    $minimum = [Text.Json.JsonElement]::new()
    $maximum = [Text.Json.JsonElement]::new()
    if ($Schema.TryGetProperty('$ref', [ref]$referenceElement)) {
        $resolved = Resolve-LocalSchemaReference $SchemaRoot $referenceElement.GetString()
        Assert-JsonSchemaNode $Instance $resolved $SchemaRoot $Location
        return
    }
    if ($Schema.TryGetProperty('type', [ref]$typeElement)) {
        $expectedType = $typeElement.GetString()
        $matchesType = switch ($expectedType) {
            'object' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Object }
            'array' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Array }
            'string' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::String }
            'integer' {
                $integerValue = [int64]0
                $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Number -and $Instance.TryGetInt64([ref]$integerValue)
            }
            'boolean' { $Instance.ValueKind -in @([Text.Json.JsonValueKind]::True, [Text.Json.JsonValueKind]::False) }
            'null' { $Instance.ValueKind -eq [Text.Json.JsonValueKind]::Null }
            default { throw "Unsupported repository schema type '$expectedType' at $Location." }
        }
        if (-not $matchesType) { throw "$Location does not match repository schema type '$expectedType'." }
    }
    if ($Schema.TryGetProperty('const', [ref]$constElement) -and
        -not [Text.Json.JsonElement]::DeepEquals($Instance, $constElement)) {
        throw "$Location differs from its exact repository-schema constant."
    }
    if ($Schema.TryGetProperty('enum', [ref]$enumElement)) {
        $enumMatch = $false
        foreach ($allowed in $enumElement.EnumerateArray()) {
            if ([Text.Json.JsonElement]::DeepEquals($Instance, $allowed)) { $enumMatch = $true; break }
        }
        if (-not $enumMatch) { throw "$Location is outside its repository-schema enumeration." }
    }
    if ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        if ($Schema.TryGetProperty('required', [ref]$requiredElement)) {
            foreach ($required in $requiredElement.EnumerateArray()) {
                $requiredValue = [Text.Json.JsonElement]::new()
                if (-not $Instance.TryGetProperty($required.GetString(), [ref]$requiredValue)) {
                    throw "$Location is missing required property '$($required.GetString())'."
                }
            }
        }
        $hasProperties = $Schema.TryGetProperty('properties', [ref]$propertiesElement)
        $additionalForbidden = $Schema.TryGetProperty('additionalProperties', [ref]$additionalElement) -and
            $additionalElement.ValueKind -eq [Text.Json.JsonValueKind]::False
        foreach ($property in $Instance.EnumerateObject()) {
            if ($hasProperties -and $propertiesElement.TryGetProperty($property.Name, [ref]$propertySchema)) {
                Assert-JsonSchemaNode $property.Value $propertySchema $SchemaRoot "$Location.$($property.Name)"
            } elseif ($additionalForbidden) {
                throw "$Location has unknown nested property '$($property.Name)'."
            }
        }
    } elseif ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        $items = @($Instance.EnumerateArray())
        if ($Schema.TryGetProperty('minItems', [ref]$minItems) -and $items.Count -lt $minItems.GetInt32()) {
            throw "$Location has fewer items than the repository schema permits."
        }
        if ($Schema.TryGetProperty('maxItems', [ref]$maxItems) -and $items.Count -gt $maxItems.GetInt32()) {
            throw "$Location has more items than the repository schema permits."
        }
        if ($Schema.TryGetProperty('uniqueItems', [ref]$uniqueItems) -and $uniqueItems.GetBoolean()) {
            for ($left = 0; $left -lt $items.Count; $left++) {
                for ($right = $left + 1; $right -lt $items.Count; $right++) {
                    if ([Text.Json.JsonElement]::DeepEquals($items[$left], $items[$right])) {
                        throw "$Location contains duplicate array items."
                    }
                }
            }
        }
        if ($Schema.TryGetProperty('items', [ref]$itemSchema)) {
            for ($index = 0; $index -lt $items.Count; $index++) {
                Assert-JsonSchemaNode $items[$index] $itemSchema $SchemaRoot "$Location[$index]"
            }
        }
    } elseif ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::String) {
        $value = $Instance.GetString()
        if ($Schema.TryGetProperty('minLength', [ref]$minLength) -and $value.Length -lt $minLength.GetInt32()) {
            throw "$Location is shorter than its repository-schema minimum."
        }
        if ($Schema.TryGetProperty('pattern', [ref]$pattern) -and $value -cnotmatch $pattern.GetString()) {
            throw "$Location does not match its repository-schema pattern."
        }
    } elseif ($Instance.ValueKind -eq [Text.Json.JsonValueKind]::Number) {
        $value = $Instance.GetInt64()
        if ($Schema.TryGetProperty('minimum', [ref]$minimum) -and $value -lt $minimum.GetInt64()) {
            throw "$Location is below its repository-schema minimum."
        }
        if ($Schema.TryGetProperty('maximum', [ref]$maximum) -and $value -gt $maximum.GetInt64()) {
            throw "$Location exceeds its repository-schema maximum."
        }
    }
}

function Assert-AgainstRepositorySchema([string] $DocumentPath, [string] $SchemaPath) {
    $document = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($DocumentPath))
    $schema = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($SchemaPath))
    try {
        Assert-JsonSchemaNode $document.RootElement $schema.RootElement $schema.RootElement $DocumentPath
    } finally {
        $schema.Dispose()
        $document.Dispose()
    }
}

$matrixInput = Read-StrictJson (Resolve-InputPath $MatrixPath)
$profileInput = Read-StrictJson (Resolve-InputPath $ProfileTemplatePath)
$requestInputs = @($QualificationTemplatePath, $SourceClaimTemplatePath, $CandidateTemplatePath |
    ForEach-Object { Read-StrictJson (Resolve-InputPath $_) })
$matrix = $matrixInput.value
$profile = $profileInput.value
$requests = @($requestInputs.value)

if ($RequireFrozenCandidate -and $RequireAcceptedHistoricalEvidence) {
    throw 'WP8 validation modes are mutually exclusive.'
}
if (($RequireFrozenCandidate -or $RequireAcceptedHistoricalEvidence) -and
    -not [string]::IsNullOrWhiteSpace((& git -C $repoRoot status --porcelain))) {
    throw 'WP8 frozen-candidate validation requires a clean committed worktree.'
}

$matrixSchemaPath = Resolve-InputPath 'contracts/repository/wp8-case-requirement-matrix.v1.schema.json'
$profileSchemaPath = Resolve-InputPath 'contracts/repository/wp8-production-profile-authorization-template.v1.schema.json'
$requestSchemaPath = Resolve-InputPath 'contracts/repository/wp8-provider-request-authorization-template.v1.schema.json'
Assert-AgainstRepositorySchema (Resolve-InputPath $MatrixPath) $matrixSchemaPath
Assert-AgainstRepositorySchema (Resolve-InputPath $ProfileTemplatePath) $profileSchemaPath
Assert-AgainstRepositorySchema (Resolve-InputPath $QualificationTemplatePath) $requestSchemaPath
Assert-AgainstRepositorySchema (Resolve-InputPath $SourceClaimTemplatePath) $requestSchemaPath
Assert-AgainstRepositorySchema (Resolve-InputPath $CandidateTemplatePath) $requestSchemaPath

Assert-ExactPropertySet $matrix @('schema_identity','matrix_id','status','claim_boundary','candidate_binding',
    'acceptance_binding','registry_binding','evidence_groups','cases','supplemental_requirement_mappings','external_effects','review') 'WP8 matrix root'
Assert-ExactPropertySet $profile @('schema_identity','packet_id','packet_kind','status','effect_authority',
    'candidate_binding','acceptance_binding','materialization','owner_authorization','provider_intent','profile_binding',
    'native_boundary','entry_cancel','persistence_delete','deadline','canaries','execution') 'WP8 profile root'
foreach ($request in $requests) {
    Assert-ExactPropertySet $request @('schema_identity','packet_id','packet_kind','status','effect_authority',
        'candidate_binding','acceptance_binding','materialization','prerequisites','owner_authorization','billing_disclosure',
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
$productTemplateCommit = [string]$matrix.candidate_binding.wp8_product_template_commit
$verificationCandidateCommit = [string]$matrix.candidate_binding.wp8_verification_candidate_commit
$acceptanceBinding = $matrix.acceptance_binding
Assert-ExactPropertySet $acceptanceBinding @('state','verification_candidate_commit','post_run_evidence_candidate_commit',
    'non_live_all_receipt_sha256','pre_live_receipt_sha256','direct_layer6_receipt_sha256') 'WP8 acceptance binding'
if ([string]$acceptanceBinding.verification_candidate_commit -cne $verificationCandidateCommit) {
    throw 'WP8 acceptance binding does not name the exact verification candidate.'
}
$pendingAcceptanceValue = 'pending-until-post-run-evidence-freeze'
if ($acceptanceBinding.state -eq 'correction-verification-pending') {
    foreach ($field in @('post_run_evidence_candidate_commit','non_live_all_receipt_sha256',
            'pre_live_receipt_sha256','direct_layer6_receipt_sha256')) {
        if ([string]$acceptanceBinding.$field -cne $pendingAcceptanceValue) {
            throw "WP8 correction-verification acceptance field '$field' is not typed pending."
        }
    }
} elseif ($acceptanceBinding.state -eq 'accepted-closeout') {
    if ([string]$acceptanceBinding.post_run_evidence_candidate_commit -cnotmatch '^[0-9a-f]{40}$') {
        throw 'WP8 accepted closeout lacks an exact post-run evidence candidate.'
    }
    foreach ($field in @('non_live_all_receipt_sha256','pre_live_receipt_sha256','direct_layer6_receipt_sha256')) {
        if ([string]$acceptanceBinding.$field -cnotmatch '^[0-9a-f]{64}$') {
            throw "WP8 accepted closeout receipt '$field' is not an exact SHA-256."
        }
    }
} else {
    throw 'WP8 acceptance binding has an unknown state.'
}
if ($RequireFrozenCandidate -and
    ($productTemplateCommit -eq 'pending-until-product-template-freeze' -or
     $verificationCandidateCommit -eq 'pending-until-verification-freeze')) {
    throw 'WP8 final pre-live validation requires exact product/template and verification candidate commits.'
}
foreach ($binding in @(
        @('product/template', $productTemplateCommit, 'pending-until-product-template-freeze'),
        @('verification', $verificationCandidateCommit, 'pending-until-verification-freeze'))) {
    if ($binding[1] -ne $binding[2]) {
        if ($binding[1] -notmatch '^[0-9a-f]{40}$') { throw "WP8 $($binding[0]) binding is malformed." }
        & git -C $repoRoot merge-base --is-ancestor $binding[1] HEAD
        if ($LASTEXITCODE -ne 0) { throw "WP8 $($binding[0]) binding is not an ancestor of HEAD." }
    }
}
$postVerificationDisposition = 'unfrozen-candidate'
if ($RequireAcceptedHistoricalEvidence) {
    if ($acceptanceBinding.state -ne 'accepted-closeout') {
        throw 'WP8 historical evidence mode requires the exact accepted closeout binding.'
    }
    $postVerificationDisposition = 'accepted-historical-evidence-retained-for-later-no-effect-package'
}
elseif ($verificationCandidateCommit -match '^[0-9a-f]{40}$') {
    $postVerificationPaths = @(& git -C $repoRoot -c core.quotePath=false diff --name-only $verificationCandidateCommit HEAD --)
    if ($LASTEXITCODE -ne 0) { throw 'WP8 post-verification path enumeration failed.' }
    $currentStateAtHead = [string]::Join("`n", @(& git -C $repoRoot show 'HEAD:docs/current-state.md'))
    if ($LASTEXITCODE -ne 0) { throw 'WP8 cannot read committed current-state authority.' }
    $readmeAtHead = [string]::Join("`n", @(& git -C $repoRoot show 'HEAD:docs/plans/milestones/m1/slices/s6/README.md'))
    if ($LASTEXITCODE -ne 0) { throw 'WP8 cannot read the committed Slice 6 entry document.' }
    $verificationRecord = [string]::Join("`n", @(& git -C $repoRoot show "$verificationCandidateCommit`:docs/plans/milestones/m1/slices/s6/record.md"))
    if ($LASTEXITCODE -ne 0) { throw 'WP8 cannot read the verification-candidate record.' }
    $headRecord = [string]::Join("`n", @(& git -C $repoRoot show 'HEAD:docs/plans/milestones/m1/slices/s6/record.md'))
    if ($LASTEXITCODE -ne 0) { throw 'WP8 cannot read the committed head record.' }
    $wp9ManifestInput = Read-StrictJson (Resolve-InputPath 'docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v1.json')
    $wp9Manifest = $wp9ManifestInput.value
    $reviewPattern = '^WP9_PROFILE_REVIEW_ACCEPTANCE candidate_commit=([0-9a-f]{40}) manifest_id=' +
        [Regex]::Escape([string]$wp9Manifest.manifest_id) + ' sha256=' +
        [Regex]::Escape([string]$wp9ManifestInput.sha256) + ' verdicts=security,semantics,diff$'
    $matchingReviewLines = @($headRecord -split "`n" | Where-Object { [Regex]::IsMatch($_, $reviewPattern) })
    $reviewedCandidate = if ($matchingReviewLines.Count -eq 1) {
        [Regex]::Match($matchingReviewLines[0], $reviewPattern).Groups[1].Value
    } else { '' }
    $reviewedRecord = ''
    $reviewCloseoutPaths = @()
    if ($reviewedCandidate -match '^[0-9a-f]{40}$') {
        & git -C $repoRoot merge-base --is-ancestor $reviewedCandidate HEAD
        if ($LASTEXITCODE -eq 0) {
            $reviewedRecord = [string]::Join("`n", @(& git -C $repoRoot show "$reviewedCandidate`:docs/plans/milestones/m1/slices/s6/record.md"))
            if ($LASTEXITCODE -ne 0) { $reviewedRecord = '' }
            $reviewCloseoutPaths = @(& git -C $repoRoot -c core.quotePath=false diff --name-only $reviewedCandidate HEAD --)
            if ($LASTEXITCODE -ne 0) { $reviewCloseoutPaths = @() }
        }
    }
    $wp9ReviewBinding = [pscustomobject]@{
        manifest_id = [string]$wp9Manifest.manifest_id
        manifest_sha256 = [string]$wp9ManifestInput.sha256
        close_ready_commit = [string]$wp9Manifest.candidate_binding.close_ready_implementation_commit
        expires_at_utc = [string]$wp9Manifest.expires_at_utc
        reviewed_candidate_commit = $reviewedCandidate
        reviewed_record_text = $reviewedRecord
        closeout_paths = @($reviewCloseoutPaths)
    }
    $postVerificationDisposition = Get-Wp8PostVerificationDisposition `
        $postVerificationPaths $currentStateAtHead $readmeAtHead $verificationRecord $headRecord `
        $acceptanceBinding $wp9ReviewBinding
    if ($postVerificationDisposition -eq 'invalid') {
        $debugCorrection = Test-Wp8CorrectionCurrentState $currentStateAtHead
        $debugReadme = Test-Wp8CorrectionReadme $readmeAtHead
        throw "WP8 HEAD is neither the exact correction-verification state nor the exact structured accepted closeout (verification=$verificationCandidateCommit; state=$($acceptanceBinding.state); correction=$debugCorrection; readme=$debugReadme; paths=$($postVerificationPaths -join ','))."
    }
    if ($acceptanceBinding.state -eq 'accepted-closeout') {
        $evidenceCommit = [string]$acceptanceBinding.post_run_evidence_candidate_commit
        & git -C $repoRoot merge-base --is-ancestor $verificationCandidateCommit $evidenceCommit
        if ($LASTEXITCODE -ne 0) { throw 'WP8 post-run evidence candidate does not descend from the verification candidate.' }
        & git -C $repoRoot merge-base --is-ancestor $evidenceCommit HEAD
        if ($LASTEXITCODE -ne 0) { throw 'WP8 post-run evidence candidate is not an ancestor of the accepted closeout.' }
        $verificationToEvidencePaths = @(& git -C $repoRoot -c core.quotePath=false diff --name-only $verificationCandidateCommit $evidenceCommit --)
        $bindingPaths = @(
            'docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json',
            'docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json',
            'docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json',
            'docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json',
            'docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json')
        if (-not (Test-Wp8ExactPathSet $verificationToEvidencePaths $bindingPaths)) {
            throw 'WP8 post-run evidence candidate is not the exact structured binding candidate.'
        }
    }
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

$expectedCases = @('EVAL-0026','EVAL-0033','EVAL-0034','EVAL-0035','EVAL-0037','EVAL-0038','EVAL-0039',
    'EVAL-0040','EVAL-0045','EVAL-0046','EVAL-0064','EVAL-0067','EVAL-0076','EVAL-0077','EVAL-0080',
    'EVAL-0081','EVAL-0082','EVAL-0083','EVAL-0084','EVAL-0085','EVAL-0087','EVAL-0088','EVAL-0089')
Assert-ExactSequence @($matrix.cases.case_id) $expectedCases 'WP8 23-case inventory'
if (@($matrix.cases.case_id | Sort-Object -Unique).Count -ne 23) { throw 'WP8 case IDs are not unique.' }
foreach ($case in @($matrix.cases)) {
    if (@($case.covered_assertions).Count -eq 0 -or @($case.requirements).Count -eq 0 -or @($case.evidence_gates).Count -eq 0) {
        throw "WP8 case '$($case.case_id)' lacks requirements, gates, or covered assertions."
    }
    if ($case.classification -eq 'primary' -and $case.disposition -notin @('covered-non-live','covered-with-assertion-level-na')) {
        throw "Primary case '$($case.case_id)' cannot receive a whole-case N/A disposition."
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
$expectedCatalogRequirements = [ordered]@{
    'EVAL-0026' = @('SNAP-002','SCAN-009','INTENT-004'); 'EVAL-0033' = @('SEC-001')
    'EVAL-0034' = @('SEC-002','SEC-004','AI-003','AI-004'); 'EVAL-0035' = @('AUTH-002','SEC-003')
    'EVAL-0037' = @('SCAN-007','DOC-011'); 'EVAL-0038' = @('SCAN-005','SCAN-006','AI-004')
    'EVAL-0039' = @('DOC-002','DOC-011'); 'EVAL-0040' = @('SEC-004','OPS-003')
    'EVAL-0045' = @('SCOPE-004'); 'EVAL-0046' = @('AUTH-001','AUTH-003')
    'EVAL-0064' = @('AI-001','AI-002','OPS-001'); 'EVAL-0067' = @('EVID-001','EVID-004','EVID-007','OPS-002')
    'EVAL-0076' = @('SCAN-003','AI-005'); 'EVAL-0077' = @('AI-004','AI-007')
    'EVAL-0080' = @('AUTH-001','AUTH-002','SEC-003'); 'EVAL-0081' = @('SCAN-004','SCAN-005','AI-004')
    'EVAL-0082' = @('SCAN-002','SCAN-009','EVID-007'); 'EVAL-0083' = @('EVID-002','SNAP-006','AI-006')
    'EVAL-0084' = @('FIND-002'); 'EVAL-0085' = @('PROD-004','COVER-001','COVER-002','COVER-003')
    'EVAL-0087' = @('SNAP-004','SNAP-006','OPS-002','OPS-004'); 'EVAL-0088' = @('SCAN-004','SCAN-005','AUTH-002','SEC-003')
    'EVAL-0089' = @('SEC-002','SEC-004','AI-004','AI-007')
}
foreach ($case in @($matrix.cases)) {
    Assert-ExactSequence @($case.requirements) @($expectedCatalogRequirements[[string]$case.case_id]) "Catalog requirements for $($case.case_id)"
}
$caseSemanticJson = $matrix.cases | ConvertTo-Json -Depth 100 -Compress
$caseSemanticSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($caseSemanticJson))).ToLowerInvariant()
$expectedCaseSemanticSha256 = '46685998f2947a35dae4944d293e2836c2e056b7a4db74025d68dc6e1e776ab4'
if ($caseSemanticSha256 -ne $expectedCaseSemanticSha256) {
    throw 'WP8 per-case classification disposition gates assertions or assertion-level N/A tuples drifted.'
}
$expectedSupplementalRequirements = @('EVID-003','EVID-006','ANALYSIS-003','ANALYSIS-004','ANALYSIS-005',
    'ANALYSIS-016','ANALYSIS-019','SNAP-001','SNAP-003','SNAP-005','PROD-002')
Assert-ExactSequence @($matrix.supplemental_requirement_mappings.requirement_id) $expectedSupplementalRequirements 'WP8 supplemental requirement mappings'
foreach ($mapping in @($matrix.supplemental_requirement_mappings)) {
    if (@($mapping.covered_assertions).Count -eq 0 -or @($mapping.evidence_gates).Count -eq 0) {
        throw "WP8 supplemental requirement '$($mapping.requirement_id)' lacks exact evidence."
    }
}
$supplementalSemanticJson = $matrix.supplemental_requirement_mappings | ConvertTo-Json -Depth 100 -Compress
$supplementalSemanticSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($supplementalSemanticJson))).ToLowerInvariant()
$expectedSupplementalSemanticSha256 = 'a1e255ecf3b497ebecf73637aed51dd7ab5a87b5c4f310c79045d6362d02fcd7'
if ($supplementalSemanticSha256 -ne $expectedSupplementalSemanticSha256) {
    throw 'WP8 supplemental requirement evidence mappings drifted.'
}
$requiredRequirements = @('EVID-003','EVID-006','ANALYSIS-003','ANALYSIS-004','ANALYSIS-005','ANALYSIS-016','ANALYSIS-019',
    'SNAP-001','SNAP-003','SNAP-005','COVER-001','COVER-002','COVER-003','SCAN-006','SCAN-009',
    'OPS-001','OPS-002','OPS-003','SEC-001','SEC-002','SEC-003','SEC-004','AUTH-001','AUTH-002','AUTH-003',
    'AI-003','AI-004','AI-006','AI-007','PROD-002','PROD-004')
$matrixRequirements = @(@($matrix.cases.requirements) + @($matrix.supplemental_requirement_mappings.requirement_id) | Sort-Object -Unique)
foreach ($requirement in $requiredRequirements) {
    if ($matrixRequirements -cnotcontains $requirement) { throw "WP8 finite matrix omits required mapping '$requirement'." }
}
$expectedGroups = @('contract-persistence','budget','credential-helper','provider-adapter','semantic-provenance','overall')
Assert-ExactSequence @($matrix.evidence_groups.group_id) $expectedGroups 'WP8 evidence groups'

$commonCandidateBindings = [ordered]@{
    accepted_wp4_execution_commit = '1fe62bbad155b4e9b8fc2d1056fee14a15dbc11b'
    accepted_wp4_evidence_sha256 = '3f148b76fef94c077293d863a06447bb22b395997db2b09dea291193c1598390'
    accepted_wp4_audit_commit = 'be55eda59752f884fe6e113f40927295da45f2cd'
    accepted_wp7_product_commit = '59367a7479a7395b173b974bf720543aab2404d4'
    accepted_wp7_evidence_commit = '51251c0e0eb98d67dbc9b295b9ff084ebca33890'
}
foreach ($document in @($profile) + $requests) {
    foreach ($entry in $commonCandidateBindings.GetEnumerator()) {
        if ([string]$document.candidate_binding.($entry.Key) -cne [string]$entry.Value) {
            throw "WP8 packet '$($document.packet_kind)' has stale common candidate binding '$($entry.Key)'."
        }
    }
    if ([string]$document.candidate_binding.wp8_product_template_commit -cne $productTemplateCommit -or
        [string]$document.candidate_binding.wp8_verification_candidate_commit -cne $verificationCandidateCommit) {
        throw "WP8 packet '$($document.packet_kind)' does not share the exact product/template and verification identities."
    }
    $documentAcceptanceJson = $document.acceptance_binding | ConvertTo-Json -Depth 10 -Compress
    $matrixAcceptanceJson = $acceptanceBinding | ConvertTo-Json -Depth 10 -Compress
    if ($documentAcceptanceJson -cne $matrixAcceptanceJson) {
        throw "WP8 packet '$($document.packet_kind)' does not share the exact structured acceptance binding."
    }
}

if ($profile.schema_identity -ne 'infinium.repository.wp8-production-profile-authorization-template/1.0.0' -or
    $profile.packet_id -ne 'infinium.m1-s6.wp8.pre-live-profile-authorization-template/v1' -or
    $profile.packet_kind -ne 'EnrollOrVerifyProfile' -or $profile.status -ne 'non-executable-template' -or
    $profile.effect_authority -ne 'none' -or [bool]$profile.execution.permitted -or $null -ne $profile.execution.command) {
    throw 'WP8 production profile packet is not the exact non-executable template.'
}
Assert-ExactSequence @($profile.native_boundary.new_profile_calls) @('CredWriteW','CredReadW','CredFree') 'New-profile native calls'
Assert-ExactSequence @($profile.native_boundary.existing_profile_calls) @('CredReadW','CredFree') 'Existing-profile native calls'
Assert-ExactSequence @($profile.native_boundary.forbidden_calls) @('CredDeleteW','CredEnumerateW','arbitrary-target access','alternate credential or secret-storage mechanism') 'Profile forbidden native calls'
if ($profile.native_boundary.enumeration -ne 'prohibited' -or $profile.native_boundary.fallback -ne 'none' -or
    -not [bool]$profile.entry_cancel.masked -or -not [bool]$profile.entry_cancel.paste_permitted -or
    [bool]$profile.entry_cancel.renderer_receives_value -or -not [bool]$profile.materialization.no_inheritance -or
    [bool]$profile.persistence_delete.deletion_permitted -or
    $profile.persistence_delete.credential_persistence -ne 'retain-exact-generation-for-wp9-through-wp11' -or
    $profile.persistence_delete.post_qualification_intent -ne 'retain-no-delete-authorized-by-this-packet' -or
    $profile.persistence_delete.deletion_authority -ne 'separate-fresh-exact-owner-authorization-required') {
    throw 'WP8 production profile native, UI, or no-inheritance boundary is invalid.'
}

if ($requests.Count -ne 3) { throw 'WP8 requires exactly three distinct provider request templates.' }
$expectedRequestTuples = @(
    @('Qualification','infinium.m1-s6.wp8.pre-live-qualification-authorization-template/v1','transport-qualification',16384,20480,256,262144,140000000,60000),
    @('SourceClaimExtraction','infinium.m1-s6.wp8.pre-live-source-claim-authorization-template/v1','source-claim-extraction',65536,73728,4096,1048576,600000000,120000),
    @('CandidateInvestigation','infinium.m1-s6.wp8.pre-live-candidate-investigation-authorization-template/v1','candidate-investigation',65536,73728,4096,1048576,600000000,120000)
)
$expectedRequestPredecessors = @(
    'accepted-production-profile-sub-gate-receipt-pending',
    'accepted-WP9-qualification-operation-receipt-pending',
    'accepted-WP10-source-claim-operation-receipt-pending')
$expectedRequestPrerequisites = @(
    @('WP8 independently accepted at an exact clean commit','production profile sub-gate independently accepted with an exact verified generation','fresh official-document, capability, and price drift check'),
    @('WP9 independently accepted','LLM-CLAIM-LIVE-VAL inputs and harness-only oracle frozen and independently reviewed','fresh exact candidate, document, profile, capability, and price check'),
    @('WP10 independently accepted','LLM-INVESTIGATE-LIVE-VAL and PROV-LIVE-COMPOSED-VAL frozen and independently reviewed','fresh exact candidate, document, profile, capability, and price check'))
$expectedFixtureOracleTuples = @(
    @('pending-WP9-qualification-live-extension','pending-WP9-qualification-live-oracle'),
    @('LLM-CLAIM-LIVE-VAL-pending-freeze','LLM-CLAIM-LIVE-VAL-oracle-pending-freeze'),
    @('LLM-INVESTIGATE-LIVE-VAL-pending-freeze','LLM-INVESTIGATE-LIVE-VAL-and-PROV-LIVE-COMPOSED-VAL-oracles-pending-freeze'))
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
    if ($request.candidate_binding.required_predecessor_live_acceptance -cne $expectedRequestPredecessors[$index]) {
        throw "WP8 request '$($request.packet_kind)' has a stale or swapped predecessor."
    }
    Assert-ExactSequence @($request.prerequisites) @($expectedRequestPrerequisites[$index]) "Prerequisites for $($request.packet_kind)"
    if ($request.fixture_oracle_binding.fixture_identity -cne $expectedFixtureOracleTuples[$index][0] -or
        $request.fixture_oracle_binding.oracle_identity -cne $expectedFixtureOracleTuples[$index][1]) {
        throw "WP8 request '$($request.packet_kind)' has a stale or swapped fixture/oracle pending identity."
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
if ($requests[0].request_binding.prompt_id -ne 'pending-qualification-prompt-identity' -or
    $requests[0].request_binding.prompt_fingerprint_sha256 -ne 'pending' -or
    $requests[0].request_binding.output_schema_path -ne 'pending-qualification-output-schema' -or
    $requests[0].request_binding.output_schema_sha256 -ne 'pending' -or
    $requests[1].request_binding.prompt_id -ne 'infinium.m1-s6.source-claim-prompt/v1' -or
    $requests[1].request_binding.prompt_fingerprint_sha256 -ne 'd2915f449e72d43cf697d522f2c6a1b44653dd519daba02968c1bfe3cf66ab84' -or
    $requests[1].request_binding.output_schema_sha256 -ne (Get-FileHash -LiteralPath (Resolve-InputPath $requests[1].request_binding.output_schema_path) -Algorithm SHA256).Hash.ToLowerInvariant() -or
    $requests[2].request_binding.prompt_id -ne 'infinium.m1-s6.candidate-investigation-prompt/v1' -or
    $requests[2].request_binding.prompt_fingerprint_sha256 -ne '026d7002102b74df9ef50ed2421714afa9f7b5dc717c69cadf7fb586d9c5b92e' -or
    $requests[2].request_binding.output_schema_sha256 -ne (Get-FileHash -LiteralPath (Resolve-InputPath $requests[2].request_binding.output_schema_path) -Algorithm SHA256).Hash.ToLowerInvariant()) {
    throw 'WP8 qualification or semantic request prompt/output-schema binding is stale.'
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
    wp8_product_template_commit = $productTemplateCommit
    wp8_verification_candidate_commit = $verificationCandidateCommit
    wp8_review_evidence_head = (& git -C $repoRoot rev-parse HEAD).Trim()
    acceptance_state = [string]$acceptanceBinding.state
    post_run_evidence_candidate_commit = [string]$acceptanceBinding.post_run_evidence_candidate_commit
    non_live_all_receipt_sha256 = [string]$acceptanceBinding.non_live_all_receipt_sha256
    accepted_pre_live_receipt_sha256 = [string]$acceptanceBinding.pre_live_receipt_sha256
    direct_layer6_receipt_sha256 = [string]$acceptanceBinding.direct_layer6_receipt_sha256
    post_verification_disposition = $postVerificationDisposition
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
