using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

public static class TargetedVerificationPlanner
{
    public static readonly OpaqueId ClosurePolicyId = new("targeted-dependency-closure");
    public static readonly ContractVersion ClosurePolicyVersion = new(1, 0, 0);
    public static readonly OpaqueId CorrelationPolicyId = new("targeted-cross-snapshot-correlation");
    public static readonly ContractVersion CorrelationPolicyVersion = new(1, 0, 0);
    public static readonly Sha256Fingerprint CorrelationPolicyFingerprint = Fingerprint(
        "targeted-cross-snapshot-correlation/v1\nexact-typed-identity\ntyped-equivalence-proof\nqualified-complete-enumeration");

    public static TargetedAnalysisScopeContract CloseScope(
        OpaqueId preparationId,
        OpaqueId sourceOccurrenceId,
        IReadOnlyList<TargetedScopeMemberContract> directRoots,
        IReadOnlyList<TargetedScopeMemberContract> admittedMembers,
        IReadOnlyList<TargetedScopeDependencyContract> admittedDependencies,
        long maximumMembers = TargetedVerificationContractInvariants.MaximumScopeMembers,
        long maximumEdges = TargetedVerificationContractInvariants.MaximumScopeEdges)
    {
        ArgumentNullException.ThrowIfNull(directRoots);
        ArgumentNullException.ThrowIfNull(admittedMembers);
        ArgumentNullException.ThrowIfNull(admittedDependencies);
        if (directRoots.Count == 0)
        {
            throw new InvalidDataException("Targeted scope requires at least one canonical direct root.");
        }

        Dictionary<OpaqueId, TargetedScopeMemberContract> members;
        try
        {
            members = admittedMembers.ToDictionary(item => item.MemberId);
            foreach (TargetedScopeMemberContract root in directRoots)
            {
                if (members.TryGetValue(root.MemberId, out TargetedScopeMemberContract? admitted))
                {
                    if (admitted.Kind != root.Kind
                        || admitted.StableIdentity != root.StableIdentity
                        || admitted.Reason != root.Reason
                        || admitted.Mandatory != root.Mandatory
                        || !admitted.SourceProofIds.SequenceEqual(root.SourceProofIds))
                    {
                        throw new ArgumentException("A direct root differs from its admitted member definition.");
                    }
                }
                else
                {
                    members.Add(root.MemberId, root);
                }
            }
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Targeted scope contains duplicate member identities.", exception);
        }

        if (admittedDependencies.Count > maximumEdges)
        {
            throw new InvalidDataException("Targeted scope exceeds its admitted dependency-edge bound.");
        }
        if (admittedDependencies.Any(edge => !members.ContainsKey(edge.FromMemberId)
                || !members.ContainsKey(edge.ToMemberId)))
        {
            throw new InvalidDataException("A targeted dependency edge names an unknown admitted member.");
        }

        Dictionary<OpaqueId, TargetedScopeDependencyContract[]> outgoing = admittedDependencies
            .GroupBy(item => item.FromMemberId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.EdgeId.Value, StringComparer.Ordinal).ToArray());
        HashSet<OpaqueId> included = [];
        Queue<OpaqueId> pending = new(directRoots.OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
            .Select(item => item.MemberId));
        while (pending.TryDequeue(out OpaqueId? memberId))
        {
            if (!members.ContainsKey(memberId))
            {
                throw new InvalidDataException("A targeted dependency root names an unknown admitted member.");
            }
            if (!included.Add(memberId))
            {
                continue;
            }
            if (included.Count > maximumMembers)
            {
                throw new InvalidDataException("Targeted scope exceeds its admitted member bound.");
            }
            if (!outgoing.TryGetValue(memberId, out TargetedScopeDependencyContract[]? edges))
            {
                continue;
            }
            foreach (TargetedScopeDependencyContract edge in edges)
            {
                if (!members.ContainsKey(edge.ToMemberId))
                {
                    throw new InvalidDataException("A targeted dependency edge names an unknown admitted member.");
                }
                pending.Enqueue(edge.ToMemberId);
            }
        }

        TargetedScopeMemberContract[] closedMembers = included.Select(id => members[id])
            .OrderBy(item => item.MemberId.Value, StringComparer.Ordinal).ToArray();
        TargetedScopeDependencyContract[] closedEdges = admittedDependencies
            .Where(edge => included.Contains(edge.FromMemberId) && included.Contains(edge.ToMemberId))
            .OrderBy(item => item.EdgeId.Value, StringComparer.Ordinal).ToArray();
        TargetedScopeMemberContract[] roots = directRoots
            .OrderBy(item => item.MemberId.Value, StringComparer.Ordinal).ToArray();
        string canonical = CanonicalScope(preparationId, sourceOccurrenceId, roots, closedMembers, closedEdges,
            maximumMembers, maximumEdges);
        Sha256Fingerprint fingerprint = Fingerprint(canonical);
        TargetedAnalysisScopeContract scope = new(
            "infinium/targeted-analysis-scope", new(1, 0, 0),
            new OpaqueId("targeted-scope-" + fingerprint.Value[..32]), preparationId, sourceOccurrenceId,
            ClosurePolicyId, ClosurePolicyVersion, roots, closedMembers, closedEdges,
            maximumMembers, maximumEdges, fingerprint);
        TargetedVerificationContractInvariants.Validate(scope);
        return scope;
    }

    public static TargetedCorrelationCoverageContract Correlate(
        OpaqueId preparationId,
        TargetedAnalysisScopeContract scope,
        OpaqueId targetSnapshotId,
        OpaqueId evidenceAcquisitionId,
        OpaqueId semanticOutputId,
        IReadOnlyList<TargetedCurrentObservationContract> observations)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(observations);
        HashSet<OpaqueId> scopeIdentities = scope.Members.Select(item => item.StableIdentity).ToHashSet();
        if (observations.Any(item => !scopeIdentities.Contains(item.SourceStableIdentity)))
        {
            throw new InvalidDataException(
                "A targeted correlation observation cannot expand or bypass the closed prepared scope.");
        }
        Dictionary<OpaqueId, TargetedCurrentObservationContract[]> byIdentity = observations
            .GroupBy(item => item.SourceStableIdentity)
            .ToDictionary(group => group.Key, group => group.ToArray());
        List<TargetedCorrelationCoverageRowContract> rows = [];
        foreach (TargetedScopeMemberContract member in scope.Members)
        {
            TargetedCurrentObservationContract observation;
            if (!byIdentity.TryGetValue(member.StableIdentity, out TargetedCurrentObservationContract[]? matches))
            {
                observation = new(member.StableIdentity, new OpaqueId("target-population-unknown"), null, null,
                    TargetedCorrelationStatus.MissingRequiredProof, false, false,
                    "No qualified target-population observation accounts for this required scope member.",
                    member.SourceProofIds, null);
            }
            else if (matches.Length != 1)
            {
                observation = new(member.StableIdentity, matches[0].TargetPopulationId, null, null,
                    TargetedCorrelationStatus.Ambiguous, false, false,
                    "Multiple target observations claim the same source stable identity.",
                    matches.SelectMany(item => item.EvidenceIds).Distinct().OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(), null);
            }
            else
            {
                observation = matches[0];
            }

            string denominatorEffect = observation.Status switch
            {
                TargetedCorrelationStatus.ProvenAbsent or TargetedCorrelationStatus.ProvenNotApplicable => "completed-observation",
                TargetedCorrelationStatus.MatchedExecutable or TargetedCorrelationStatus.ChangedCorrelated => "requires-analysis-coverage",
                _ => "retained-gap",
            };
            string readinessEffect = observation.Status switch
            {
                _ when !observation.CorrelationQualified => "non-startable",
                TargetedCorrelationStatus.Ambiguous or TargetedCorrelationStatus.MissingRequiredProof => "non-startable",
                TargetedCorrelationStatus.Unsupported or TargetedCorrelationStatus.Inaccessible or TargetedCorrelationStatus.Malformed => "limited-plan-gap",
                _ => "scope-limited-no-readiness",
            };
            string rowMaterial = string.Join('\n', member.MemberId.Value, member.StableIdentity.Value,
                observation.TargetPopulationId.Value, observation.TargetStableIdentity?.Value ?? "none",
                observation.CurrentExecutionMemberId?.Value ?? "none", observation.Status.ToString(),
                observation.CorrelationQualified, observation.ProcessingQualified,
                observation.EnumerationOrApplicabilityProofId?.Value ?? "none");
            rows.Add(new(new OpaqueId("target-correlation-" + Fingerprint(rowMaterial).Value[..32]),
                scope.SourceOccurrenceId, member.MemberId, member.Kind, member.StableIdentity,
                observation.TargetPopulationId, CorrelationPolicyId, CorrelationPolicyVersion,
                CorrelationPolicyFingerprint, observation.TargetStableIdentity, observation.CurrentExecutionMemberId,
                observation.Status, observation.CorrelationQualified, observation.ProcessingQualified,
                denominatorEffect, readinessEffect, observation.Reason,
                observation.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
                observation.EnumerationOrApplicabilityProofId));
        }

        TargetedCorrelationCoverageRowContract[] ordered = rows.OrderBy(item => item.ScopeMemberId.Value, StringComparer.Ordinal).ToArray();
        bool startable = !ordered.Any(row => !row.CorrelationQualified || row.Status is TargetedCorrelationStatus.Ambiguous
                or TargetedCorrelationStatus.MissingRequiredProof);
        bool limited = ordered.Any(row => row.CorrelationQualified && row.Status is
            TargetedCorrelationStatus.Unsupported or TargetedCorrelationStatus.Inaccessible or TargetedCorrelationStatus.Malformed);
        string[] nonStartable = ordered.Where(row => row.ReadinessEffect == "non-startable")
            .Select(row => $"{row.ScopeMemberId.Value}:{row.Status}").ToArray();
        string[] gaps = ordered.Where(row => row.DenominatorEffect == "retained-gap")
            .Select(row => $"{row.ScopeMemberId.Value}:{row.Status}:{row.Reason}").ToArray();
        string canonical = CanonicalCoverage(preparationId, scope, targetSnapshotId, evidenceAcquisitionId,
            semanticOutputId, ordered, startable, limited);
        Sha256Fingerprint fingerprint = Fingerprint(canonical);
        TargetedCorrelationCoverageContract coverage = new(
            "infinium/targeted-correlation-coverage", new(1, 0, 0),
            new OpaqueId("targeted-coverage-" + fingerprint.Value[..32]), preparationId, scope.ScopeId,
            targetSnapshotId, evidenceAcquisitionId, semanticOutputId, ordered, ordered.LongLength,
            startable, limited, nonStartable, gaps, fingerprint);
        TargetedVerificationContractInvariants.Validate(coverage, scope);
        return coverage;
    }

    private static string CanonicalScope(OpaqueId preparationId, OpaqueId occurrenceId,
        IReadOnlyList<TargetedScopeMemberContract> roots, IReadOnlyList<TargetedScopeMemberContract> members,
        IReadOnlyList<TargetedScopeDependencyContract> edges, long maximumMembers, long maximumEdges) => string.Join('\n',
        "targeted-analysis-scope/v1", preparationId.Value, occurrenceId.Value, ClosurePolicyId.Value,
        ClosurePolicyVersion.ToString(), maximumMembers, maximumEdges,
        string.Join('|', roots.Select(Member)), string.Join('|', members.Select(Member)), string.Join('|', edges.Select(Edge)));

    private static string CanonicalCoverage(OpaqueId preparationId, TargetedAnalysisScopeContract scope,
        OpaqueId targetSnapshotId, OpaqueId acquisitionId, OpaqueId outputId,
        IReadOnlyList<TargetedCorrelationCoverageRowContract> rows, bool startable, bool limited) => string.Join('\n',
        "targeted-correlation-coverage/v1", preparationId.Value, scope.ScopeId.Value, scope.CanonicalFingerprint.Value,
        targetSnapshotId.Value, acquisitionId.Value, outputId.Value, CorrelationPolicyId.Value,
        CorrelationPolicyVersion.ToString(), CorrelationPolicyFingerprint.Value, startable, limited,
        string.Join('|', rows.Select(Row)));

    private static string Member(TargetedScopeMemberContract item) => string.Join(':', item.MemberId.Value,
        item.Kind, item.StableIdentity.Value, item.Mandatory,
        string.Join(',', item.SourceProofIds.Select(id => id.Value).Order(StringComparer.Ordinal)), Escape(item.Reason));

    private static string Edge(TargetedScopeDependencyContract item) => string.Join(':', item.EdgeId.Value,
        item.FromMemberId.Value, item.ToMemberId.Value, item.Relation,
        string.Join(',', item.ProofIds.Select(id => id.Value).Order(StringComparer.Ordinal)));

    private static string Row(TargetedCorrelationCoverageRowContract item) => string.Join(':', item.RowId.Value,
        item.ScopeMemberId.Value, item.SourceStableIdentity.Value, item.TargetPopulationId.Value,
        item.TargetStableIdentity?.Value ?? "none", item.CurrentExecutionMemberId?.Value ?? "none", item.Status,
        item.CorrelationQualified, item.ProcessingQualified, item.DenominatorEffect, item.ReadinessEffect,
        item.EnumerationOrApplicabilityProofId?.Value ?? "none",
        string.Join(',', item.EvidenceIds.Select(id => id.Value).Order(StringComparer.Ordinal)), Escape(item.Reason));

    private static string Escape(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static Sha256Fingerprint Fingerprint(string value) => new(
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value))));
}
