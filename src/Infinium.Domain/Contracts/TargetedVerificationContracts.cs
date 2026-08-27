using System.Security.Cryptography;
using System.Text.Json;

namespace Infinium.Domain.Contracts;

public enum TargetedVerificationPreparationState
{
    Unspecified,
    Queued,
    CapturingSnapshot,
    AcquiringEvidence,
    PreparingPlan,
    Ready,
    ReadyWithGaps,
    Cancelling,
    Cancelled,
    Invalidated,
    Failed,
    Started,
}

public enum TargetedVerificationRootKind
{
    Unspecified,
    Finding,
    Case,
}

public enum TargetedScopeMemberKind
{
    Unspecified,
    Finding,
    Case,
    Candidate,
    Hypothesis,
    Participant,
    Record,
    Contribution,
    Asset,
    Provider,
    ApplicabilityPopulation,
    Analyzer,
    Evidence,
}

public enum TargetedCorrelationStatus
{
    Unspecified,
    MatchedExecutable,
    ChangedCorrelated,
    ProvenAbsent,
    ProvenNotApplicable,
    Ambiguous,
    Unsupported,
    Inaccessible,
    Malformed,
    MissingRequiredProof,
}

public enum TargetedReconciliationRelationship
{
    Unspecified,
    Exact,
    Revision,
    Related,
    Ambiguous,
    Distinct,
    NotObserved,
    NotEvaluated,
}

public sealed record TargetedVerificationSourceContract(
    OpaqueId SourceRunId,
    TargetedVerificationRootKind RootKind,
    OpaqueId RootOccurrenceId,
    OpaqueId LogicalId,
    OpaqueId SourcePayloadId,
    Sha256Fingerprint SourcePayloadFingerprint,
    Sha256Fingerprint CanonicalSignature,
    OpaqueId SourceSnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId EffectiveConfigurationId,
    OpaqueId ResolvedInputManifestId);

public sealed record TargetedScopeMemberContract(
    OpaqueId MemberId,
    TargetedScopeMemberKind Kind,
    OpaqueId StableIdentity,
    string Reason,
    bool Mandatory,
    IReadOnlyList<OpaqueId> SourceProofIds);

public sealed record TargetedScopeDependencyContract(
    OpaqueId EdgeId,
    OpaqueId FromMemberId,
    OpaqueId ToMemberId,
    string Relation,
    IReadOnlyList<OpaqueId> ProofIds);

public sealed record TargetedAnalysisScopeContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId ScopeId,
    OpaqueId PreparationId,
    OpaqueId SourceOccurrenceId,
    OpaqueId ClosurePolicyId,
    ContractVersion ClosurePolicyVersion,
    IReadOnlyList<TargetedScopeMemberContract> DirectRoots,
    IReadOnlyList<TargetedScopeMemberContract> Members,
    IReadOnlyList<TargetedScopeDependencyContract> Dependencies,
    long MaximumMembers,
    long MaximumEdges,
    Sha256Fingerprint CanonicalFingerprint);

public sealed record TargetedCorrelationCoverageRowContract(
    OpaqueId RowId,
    OpaqueId SourceOccurrenceId,
    OpaqueId ScopeMemberId,
    TargetedScopeMemberKind MemberKind,
    OpaqueId SourceStableIdentity,
    OpaqueId TargetPopulationId,
    OpaqueId CorrelationPolicyId,
    ContractVersion CorrelationPolicyVersion,
    Sha256Fingerprint CorrelationPolicyFingerprint,
    OpaqueId? TargetStableIdentity,
    OpaqueId? CurrentExecutionMemberId,
    TargetedCorrelationStatus Status,
    bool CorrelationQualified,
    bool ProcessingQualified,
    string DenominatorEffect,
    string ReadinessEffect,
    string Reason,
    IReadOnlyList<OpaqueId> EvidenceIds,
    OpaqueId? EnumerationOrApplicabilityProofId);

public sealed record TargetedCurrentObservationContract(
    OpaqueId SourceStableIdentity,
    OpaqueId TargetPopulationId,
    OpaqueId? TargetStableIdentity,
    OpaqueId? CurrentExecutionMemberId,
    TargetedCorrelationStatus Status,
    bool CorrelationQualified,
    bool ProcessingQualified,
    string Reason,
    IReadOnlyList<OpaqueId> EvidenceIds,
    OpaqueId? EnumerationOrApplicabilityProofId);

public sealed record TargetedCorrelationCoverageContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId CoverageId,
    OpaqueId PreparationId,
    OpaqueId ScopeId,
    OpaqueId TargetSnapshotId,
    OpaqueId EvidenceAcquisitionId,
    OpaqueId SemanticOutputId,
    IReadOnlyList<TargetedCorrelationCoverageRowContract> Rows,
    long PopulationDenominator,
    bool Startable,
    bool Limited,
    IReadOnlyList<string> NonStartableReasons,
    IReadOnlyList<string> Gaps,
    Sha256Fingerprint CanonicalFingerprint);

public sealed record TargetedReuseDecisionContract(
    string ArtifactKind,
    OpaqueId ArtifactId,
    string Disposition,
    OpaqueId ProofId,
    Sha256Fingerprint ProofFingerprint,
    string Reason);

public sealed record TargetedVerificationPlanContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PlanId,
    OpaqueId PreparationId,
    long PreparationRevision,
    TargetedVerificationSourceContract Source,
    OpaqueId CaptureOperationId,
    OpaqueId TargetSnapshotId,
    Sha256Fingerprint TargetSnapshotFingerprint,
    OpaqueId EvidenceAcquisitionId,
    OpaqueId SemanticOutputId,
    Sha256Fingerprint SemanticOutputFingerprint,
    TargetedAnalysisScopeContract Scope,
    TargetedCorrelationCoverageContract CorrelationCoverage,
    IReadOnlyList<TargetedReuseDecisionContract> ReuseDecisions,
    string ReadinessBoundary,
    bool Startable,
    bool Limited,
    IReadOnlyList<string> NonStartableReasons,
    IReadOnlyList<string> Gaps,
    Sha256Fingerprint PlanFingerprint);

public sealed record TargetedVerificationInitiationLineageContract(
    OpaqueId LineageId,
    OpaqueId PreparationId,
    OpaqueId SourceRunId,
    OpaqueId SourceOccurrenceId,
    OpaqueId SuccessorRunId,
    OpaqueId TargetSnapshotId,
    OpaqueId EvidenceAcquisitionId,
    OpaqueId ManagedOperationId,
    Sha256Fingerprint ManagedOperationFingerprint,
    UtcTimestamp CreatedAt);

public static class TargetedVerificationContractInvariants
{
    public const int MaximumScopeMembers = 4096;
    public const int MaximumScopeEdges = 16384;

    public static Sha256Fingerprint ComputePlanFingerprint(TargetedVerificationPlanContract plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        TargetedVerificationPlanContract canonical = plan with
        {
            PlanId = new("targeted-plan-pending"),
            PlanFingerprint = new(new string('0', 64)),
        };
        return new(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical))).ToLowerInvariant());
    }

    public static void ValidatePlanIdentity(TargetedVerificationPlanContract plan)
    {
        Sha256Fingerprint expected = ComputePlanFingerprint(plan);
        if (plan.PlanFingerprint != expected
            || plan.PlanId.Value != "targeted-plan-" + expected.Value[..32])
        {
            throw new InvalidDataException("The targeted plan identity differs from its canonical content.");
        }
    }

    private static readonly HashSet<string> AllowedRelations = new(StringComparer.Ordinal)
    {
        "root-member",
        "candidate-hypothesis",
        "candidate-participant",
        "candidate-dependency",
        "case-member",
        "shared-cause-member",
        "provider-contribution",
        "record-contribution",
        "asset-provider",
        "population-member",
        "analyzer-population",
        "evidence-support",
    };

    public static void Validate(TargetedAnalysisScopeContract scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        HashSet<OpaqueId> memberIds = scope.Members.Select(item => item.MemberId).ToHashSet();
        HashSet<OpaqueId> reachable = scope.DirectRoots.Select(item => item.MemberId).ToHashSet();
        bool expanded;
        do
        {
            expanded = false;
            foreach (TargetedScopeDependencyContract edge in scope.Dependencies.Where(edge =>
                         reachable.Contains(edge.FromMemberId)))
            {
                expanded |= reachable.Add(edge.ToMemberId);
            }
        }
        while (expanded);
        if (scope.SchemaId != "infinium/targeted-analysis-scope"
            || scope.SchemaVersion != new ContractVersion(1, 0, 0)
            || scope.ClosurePolicyId.Value != "targeted-dependency-closure"
            || scope.ClosurePolicyVersion != new ContractVersion(1, 0, 0)
            || scope.DirectRoots.Count is < 1 or > MaximumScopeMembers
            || scope.Members.Count is < 1 or > MaximumScopeMembers
            || scope.Dependencies.Count > MaximumScopeEdges
            || scope.MaximumMembers is < 1 or > MaximumScopeMembers
            || scope.MaximumEdges is < 0 or > MaximumScopeEdges
            || scope.Members.Count > scope.MaximumMembers
            || scope.Dependencies.Count > scope.MaximumEdges
            || scope.Members.Select(item => item.MemberId).Distinct().Count() != scope.Members.Count
            || scope.Members.Select(item => item.StableIdentity).Distinct().Count() != scope.Members.Count
            || scope.Members.Any(item => item.Kind == TargetedScopeMemberKind.Unspecified
                || string.IsNullOrWhiteSpace(item.Reason)
                || item.SourceProofIds.Count == 0)
            || scope.DirectRoots.Select(item => item.MemberId).Distinct().Count() != scope.DirectRoots.Count
            || scope.DirectRoots.Any(root => !scope.Members.Any(item => item.MemberId == root.MemberId))
            || scope.DirectRoots.Any(root => !SameMember(root,
                scope.Members.Single(item => item.MemberId == root.MemberId)))
            || scope.Dependencies.Select(item => item.EdgeId).Distinct().Count() != scope.Dependencies.Count
            || scope.Dependencies.Any(edge => !AllowedRelations.Contains(edge.Relation)
                || edge.ProofIds.Count == 0
                || !memberIds.Contains(edge.FromMemberId)
                || !memberIds.Contains(edge.ToMemberId))
            || !reachable.SetEquals(memberIds))
        {
            throw new InvalidDataException("The targeted analysis scope is not a closed bounded dependency graph.");
        }
    }

    public static void Validate(TargetedCorrelationCoverageContract coverage, TargetedAnalysisScopeContract scope)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        Validate(scope);
        Dictionary<OpaqueId, TargetedScopeMemberContract> members = scope.Members
            .ToDictionary(item => item.MemberId);
        if (coverage.SchemaId != "infinium/targeted-correlation-coverage"
            || coverage.SchemaVersion != new ContractVersion(1, 0, 0)
            || coverage.ScopeId != scope.ScopeId
            || coverage.Rows.Count != scope.Members.Count
            || coverage.PopulationDenominator != scope.Members.Count
            || coverage.Rows.Select(item => item.ScopeMemberId).Distinct().Count() != coverage.Rows.Count
            || !coverage.Rows.Select(item => item.ScopeMemberId).ToHashSet()
                .SetEquals(scope.Members.Select(item => item.MemberId))
            || coverage.Rows.Any(item => !ValidRow(item, members[item.ScopeMemberId], scope.SourceOccurrenceId))
            || coverage.Startable != !coverage.Rows.Any(item => !item.CorrelationQualified
                || item.Status is TargetedCorrelationStatus.Ambiguous
                    or TargetedCorrelationStatus.MissingRequiredProof)
            || coverage.Limited != coverage.Rows.Any(item => item.CorrelationQualified
                && item.Status is TargetedCorrelationStatus.Unsupported
                    or TargetedCorrelationStatus.Inaccessible
                    or TargetedCorrelationStatus.Malformed)
            || (coverage.Startable ? coverage.NonStartableReasons.Count != 0
                : coverage.NonStartableReasons.Count == 0)
            || (coverage.Limited && coverage.Gaps.Count == 0)
            || coverage.NonStartableReasons.Any(string.IsNullOrWhiteSpace)
            || coverage.Gaps.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("The targeted correlation ledger is incomplete or grants unsupported authority.");
        }
    }

    public static void Validate(TargetedVerificationPlanContract plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Validate(plan.Scope);
        Validate(plan.CorrelationCoverage, plan.Scope);
        ValidatePlanIdentity(plan);
        if (plan.SchemaId != "infinium/targeted-verification-plan"
            || plan.SchemaVersion != new ContractVersion(1, 0, 0)
            || plan.PreparationRevision < 1
            || plan.PreparationId != plan.Scope.PreparationId
            || plan.PreparationId != plan.CorrelationCoverage.PreparationId
            || plan.Source.RootOccurrenceId != plan.Scope.SourceOccurrenceId
            || plan.TargetSnapshotId != plan.CorrelationCoverage.TargetSnapshotId
            || plan.EvidenceAcquisitionId != plan.CorrelationCoverage.EvidenceAcquisitionId
            || plan.SemanticOutputId != plan.CorrelationCoverage.SemanticOutputId
            || plan.Startable != plan.CorrelationCoverage.Startable
            || plan.Limited != plan.CorrelationCoverage.Limited
            || !plan.NonStartableReasons.SequenceEqual(plan.CorrelationCoverage.NonStartableReasons)
            || !plan.Gaps.SequenceEqual(plan.CorrelationCoverage.Gaps)
            || string.IsNullOrWhiteSpace(plan.ReadinessBoundary)
            || plan.ReuseDecisions.Select(item => (item.ArtifactKind, item.ArtifactId)).Distinct().Count()
                != plan.ReuseDecisions.Count
            || plan.ReuseDecisions.Any(item => item.Disposition is not ("recompute" or "reuse-with-proof")
                || string.IsNullOrWhiteSpace(item.ArtifactKind)
                || string.IsNullOrWhiteSpace(item.Reason)))
        {
            throw new InvalidDataException("The targeted verification plan does not bind one coherent preparation.");
        }
    }

    private static bool ValidRow(
        TargetedCorrelationCoverageRowContract row,
        TargetedScopeMemberContract member,
        OpaqueId sourceOccurrenceId)
    {
        bool executable = row.Status is TargetedCorrelationStatus.MatchedExecutable
            or TargetedCorrelationStatus.ChangedCorrelated;
        bool provenMissing = row.Status is TargetedCorrelationStatus.ProvenAbsent
            or TargetedCorrelationStatus.ProvenNotApplicable;
        bool identityFailure = row.Status is TargetedCorrelationStatus.Ambiguous
            or TargetedCorrelationStatus.MissingRequiredProof;
        bool processingGap = row.Status is TargetedCorrelationStatus.Unsupported
            or TargetedCorrelationStatus.Inaccessible
            or TargetedCorrelationStatus.Malformed;
        string expectedDenominator = provenMissing ? "completed-observation"
            : executable ? "requires-analysis-coverage" : "retained-gap";
        string expectedReadiness = !row.CorrelationQualified || identityFailure ? "non-startable"
            : processingGap ? "limited-plan-gap" : "scope-limited-no-readiness";
        return row.Status != TargetedCorrelationStatus.Unspecified
            && row.SourceOccurrenceId == sourceOccurrenceId
            && row.MemberKind == member.Kind
            && row.SourceStableIdentity == member.StableIdentity
            && row.CorrelationPolicyId.Value == "targeted-cross-snapshot-correlation"
            && row.CorrelationPolicyVersion == new ContractVersion(1, 0, 0)
            && row.EvidenceIds.Count > 0
            && row.DenominatorEffect == expectedDenominator
            && row.ReadinessEffect == expectedReadiness
            && !string.IsNullOrWhiteSpace(row.Reason)
            && (!row.ProcessingQualified || row.CorrelationQualified)
            && (!executable || (row.CorrelationQualified && row.ProcessingQualified
                && row.TargetStableIdentity is not null))
            && (!provenMissing || (row.CorrelationQualified && row.ProcessingQualified
                && row.TargetStableIdentity is null && row.CurrentExecutionMemberId is null
                && row.EnumerationOrApplicabilityProofId is not null))
            && (!identityFailure || (!row.CorrelationQualified && !row.ProcessingQualified))
            && (!processingGap || !row.ProcessingQualified)
            && (row.EnumerationOrApplicabilityProofId is null
                || row.EvidenceIds.Contains(row.EnumerationOrApplicabilityProofId))
            && (row.Status != TargetedCorrelationStatus.MissingRequiredProof
                || row.EnumerationOrApplicabilityProofId is null);
    }

    private static bool SameMember(TargetedScopeMemberContract left, TargetedScopeMemberContract right) =>
        left.MemberId == right.MemberId
        && left.Kind == right.Kind
        && left.StableIdentity == right.StableIdentity
        && left.Reason == right.Reason
        && left.Mandatory == right.Mandatory
        && left.SourceProofIds.SequenceEqual(right.SourceProofIds);
}
