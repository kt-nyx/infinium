namespace Infinium.Domain.Contracts;

public enum CandidateDeliveredLinkState
{
    Unspecified,
    Absent,
    Null,
    Resolved,
    Unresolved,
}

public enum CandidateDeliveredFaceGenApplicability
{
    Unspecified,
    Applicable,
    NotApplicable,
    Unknown,
}

public enum CandidateDeliveredAssetAvailability
{
    Unspecified,
    Present,
    Absent,
    Unknown,
}

public sealed record CandidateDeliveredLinkFactContract(
    OpaqueId FactId,
    OpaqueId RecordParticipantId,
    OpaqueId PriorContributionId,
    OpaqueId WinningContributionId,
    string PriorSourceIdentity,
    string WinningSourceIdentity,
    string Field,
    string? Component,
    int Ordinal,
    CandidateDeliveredLinkState PriorState,
    OpaqueId? PriorTargetParticipantId,
    CandidateDeliveredLinkState WinningState,
    OpaqueId? WinningTargetParticipantId,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record CandidateDeliveredFaceGenFactContract(
    OpaqueId FactId,
    OpaqueId NpcParticipantId,
    CandidateDeliveredFaceGenApplicability Applicability,
    OpaqueId MeshAssetId,
    CandidateDeliveredAssetAvailability MeshAvailability,
    OpaqueId? MeshProviderParticipantId,
    OpaqueId TintAssetId,
    CandidateDeliveredAssetAvailability TintAvailability,
    OpaqueId? TintProviderParticipantId,
    int Locality,
    int Specificity,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record CandidateDeliveredCoverageGapFactContract(
    OpaqueId FactId,
    OpaqueId PopulationId,
    long Denominator,
    string MissingCapability,
    string Reason,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record CandidateDeliveredDocumentationFactContract(
    OpaqueId FactId,
    OpaqueId ApplicationId,
    OpaqueId ClaimId,
    OpaqueId PassageId,
    OpaqueId RevisionId,
    OpaqueId SubjectId,
    OpaqueId ConsumingRunId,
    OpaqueId? SupplyingSnapshotId,
    OpaqueId AnalysisContextId,
    ClaimApplicabilityState Applicability,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds);

public sealed record CandidateDeliveredInputContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    OpaqueId SourceSnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId ConfigurationId,
    IReadOnlyList<CandidateDeliveredLinkFactContract> LinkFacts,
    IReadOnlyList<CandidateDeliveredFaceGenFactContract> FaceGenFacts,
    IReadOnlyList<CandidateDeliveredCoverageGapFactContract> CoverageGapFacts,
    IReadOnlyList<CandidateDeliveredDocumentationFactContract> DocumentationFacts);

public sealed record CandidateDeliveredLinkPatternContract(
    CandidateDeliveredLinkState PriorState,
    CandidateDeliveredLinkState WinningState,
    int? PriorTargetOffset,
    int? WinningTargetOffset);

public sealed record CandidateDeliveredLinkSeriesContract(
    int Every,
    string Field,
    string? Component,
    IReadOnlyList<CandidateDeliveredLinkPatternContract> Patterns);

public sealed record CandidateDeliveredFaceGenPatternContract(
    CandidateDeliveredFaceGenApplicability Applicability,
    CandidateDeliveredAssetAvailability MeshAvailability,
    bool MeshProviderPresent,
    CandidateDeliveredAssetAvailability TintAvailability,
    bool TintProviderPresent,
    int Locality,
    int Specificity);

public sealed record CandidateDeliveredFaceGenSeriesContract(
    int Every,
    IReadOnlyList<CandidateDeliveredFaceGenPatternContract> Patterns);

public sealed record CandidateDeliveredDocumentationPatternContract(
    ClaimApplicabilityState Applicability,
    bool SupplyingSnapshotMatches,
    bool AnalysisContextMatches,
    bool HasContradictingEvidence);

public sealed record CandidateDeliveredDocumentationSeriesContract(
    int Every,
    IReadOnlyList<CandidateDeliveredDocumentationPatternContract> Patterns);

public sealed record CandidateDeliveredGapSeriesContract(
    int Every,
    string MissingCapability,
    string Reason);

public sealed record CandidateDeliveredExpansionContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId ExpansionId,
    OpaqueId OriginatingRunId,
    OpaqueId SourceSnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId ConfigurationId,
    int SubjectCount,
    IReadOnlyList<CandidateDeliveredLinkSeriesContract> LinkSeries,
    IReadOnlyList<CandidateDeliveredFaceGenSeriesContract> FaceGenSeries,
    IReadOnlyList<CandidateDeliveredDocumentationSeriesContract> DocumentationSeries,
    IReadOnlyList<CandidateDeliveredGapSeriesContract> CoverageGapSeries);

public static class CandidateDeliveredInputIdentity
{
    public static readonly ContractVersion Version = new(1, 0, 0);

    public static OpaqueId ComputePayloadId(CandidateDeliveredInputContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        List<string> descriptors =
        [
            $"schema={value.SchemaId}",
            $"version={value.SchemaVersion}",
            $"run={value.OriginatingRunId.Value}",
            $"snapshot={value.SourceSnapshotId.Value}",
            $"context={value.AnalysisContextId.Value}",
            $"configuration={value.ConfigurationId.Value}",
        ];
        descriptors.AddRange(value.LinkFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal).Select(item =>
            CandidateAnalysisIdentity.FramedSequence("link", [item.FactId.Value, item.RecordParticipantId.Value,
                item.PriorContributionId.Value, item.WinningContributionId.Value, item.PriorSourceIdentity,
                item.WinningSourceIdentity, item.Field, item.Component ?? "none", item.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                item.PriorState.ToString(), item.PriorTargetParticipantId?.Value ?? "none", item.WinningState.ToString(),
                item.WinningTargetParticipantId?.Value ?? "none",
                CandidateAnalysisIdentity.FramedSequence("dependencies", item.DependencyIds.Select(id => id.Value)),
                CandidateAnalysisIdentity.FramedSequence("evidence", item.EvidenceIds.Select(id => id.Value))])));
        descriptors.AddRange(value.FaceGenFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal).Select(item =>
            CandidateAnalysisIdentity.FramedSequence("facegen", [item.FactId.Value, item.NpcParticipantId.Value,
                item.Applicability.ToString(), item.MeshAssetId.Value, item.MeshAvailability.ToString(),
                item.MeshProviderParticipantId?.Value ?? "none", item.TintAssetId.Value, item.TintAvailability.ToString(),
                item.TintProviderParticipantId?.Value ?? "none", item.Locality.ToString(System.Globalization.CultureInfo.InvariantCulture),
                item.Specificity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CandidateAnalysisIdentity.FramedSequence("dependencies", item.DependencyIds.Select(id => id.Value)),
                CandidateAnalysisIdentity.FramedSequence("evidence", item.EvidenceIds.Select(id => id.Value))])));
        descriptors.AddRange(value.CoverageGapFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal).Select(item =>
            CandidateAnalysisIdentity.FramedSequence("gap", [item.FactId.Value, item.PopulationId.Value,
                item.Denominator.ToString(System.Globalization.CultureInfo.InvariantCulture), item.MissingCapability, item.Reason,
                CandidateAnalysisIdentity.FramedSequence("dependencies", item.DependencyIds.Select(id => id.Value)),
                CandidateAnalysisIdentity.FramedSequence("evidence", item.EvidenceIds.Select(id => id.Value))])));
        descriptors.AddRange(value.DocumentationFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal).Select(item =>
            CandidateAnalysisIdentity.FramedSequence("documentation", [item.FactId.Value, item.ApplicationId.Value,
                item.ClaimId.Value, item.PassageId.Value, item.RevisionId.Value, item.SubjectId.Value,
                item.ConsumingRunId.Value, item.SupplyingSnapshotId?.Value ?? "none", item.AnalysisContextId.Value,
                item.Applicability.ToString(),
                CandidateAnalysisIdentity.FramedSequence("dependencies", item.DependencyIds.Select(id => id.Value)),
                CandidateAnalysisIdentity.FramedSequence("supporting", item.SupportingEvidenceIds.Select(id => id.Value)),
                CandidateAnalysisIdentity.FramedSequence("contradicting", item.ContradictingEvidenceIds.Select(id => id.Value))])));
        return CandidateAnalysisIdentity.StableId("candidate-delivered-input", CandidateAnalysisIdentity.StructuralHash(descriptors).Value);
    }
}

public static class CandidateDeliveredContractInvariants
{
    public const int MaximumFacts = 1_000_000;
    public const int MaximumSeries = 128;

    public static void Validate(CandidateDeliveredInputContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaId != ContractConstants.CandidateDeliveredInputSchemaId
            || value.SchemaVersion != CandidateDeliveredInputIdentity.Version)
        {
            throw new InvalidOperationException("Candidate delivered input requires its exact active header.");
        }
        int total = checked(value.LinkFacts.Count + value.FaceGenFacts.Count + value.CoverageGapFacts.Count + value.DocumentationFacts.Count);
        if (total > MaximumFacts)
        {
            throw new InvalidOperationException("Candidate delivered input exceeds the bounded fact population.");
        }
        RequireUnique(value.LinkFacts.Select(item => item.FactId)
            .Concat(value.FaceGenFacts.Select(item => item.FactId))
            .Concat(value.CoverageGapFacts.Select(item => item.FactId))
            .Concat(value.DocumentationFacts.Select(item => item.FactId)), "delivered fact IDs");
        foreach (CandidateDeliveredLinkFactContract item in value.LinkFacts)
        {
            RequireText(item.PriorSourceIdentity, 512, "prior source identity");
            RequireText(item.WinningSourceIdentity, 512, "winning source identity");
            RequireToken(item.Field, 128, "link field");
            if (item.Component is not null)
            {
                RequireToken(item.Component, 128, "link component");
            }
            if (item.Ordinal < 0 || item.PriorState == CandidateDeliveredLinkState.Unspecified || item.WinningState == CandidateDeliveredLinkState.Unspecified)
            {
                throw new InvalidOperationException("Delivered link facts require a nonnegative ordinal and closed states.");
            }
            RequireTargetCorrelation(item.PriorState, item.PriorTargetParticipantId);
            RequireTargetCorrelation(item.WinningState, item.WinningTargetParticipantId);
            RequireIds(item.DependencyIds, 64, "link dependencies", true);
            RequireIds(item.EvidenceIds, 16, "link evidence", true);
        }
        foreach (CandidateDeliveredFaceGenFactContract item in value.FaceGenFacts)
        {
            if (item.Applicability == CandidateDeliveredFaceGenApplicability.Unspecified
                || item.MeshAvailability == CandidateDeliveredAssetAvailability.Unspecified
                || item.TintAvailability == CandidateDeliveredAssetAvailability.Unspecified
                || item.Locality is < 0 or > 100 || item.Specificity is < 0 or > 100)
            {
                throw new InvalidOperationException("Delivered FaceGen facts require closed factual states and bounded rank inputs.");
            }
            if ((item.MeshProviderParticipantId is not null) != (item.MeshAvailability == CandidateDeliveredAssetAvailability.Present)
                || (item.TintProviderParticipantId is not null) != (item.TintAvailability == CandidateDeliveredAssetAvailability.Present))
            {
                throw new InvalidOperationException("Delivered FaceGen providers must exactly match present asset state.");
            }
            RequireIds(item.DependencyIds, 64, "FaceGen dependencies", true);
            RequireIds(item.EvidenceIds, 16, "FaceGen evidence", true);
        }
        foreach (CandidateDeliveredCoverageGapFactContract item in value.CoverageGapFacts)
        {
            if (item.Denominator < 0)
            {
                throw new InvalidOperationException("Coverage-gap denominators cannot be negative.");
            }
            RequireText(item.MissingCapability, 512, "missing capability");
            RequireText(item.Reason, 2048, "coverage-gap reason");
            RequireIds(item.DependencyIds, 64, "gap dependencies", true);
            RequireIds(item.EvidenceIds, 16, "gap evidence", true);
        }
        foreach (CandidateDeliveredDocumentationFactContract item in value.DocumentationFacts)
        {
            if (item.Applicability == ClaimApplicabilityState.Unspecified)
            {
                throw new InvalidOperationException("Documentation facts require a closed applicability state.");
            }
            RequireIds(item.DependencyIds, 64, "documentation dependencies", true);
            RequireIds(item.SupportingEvidenceIds, 16, "documentation supporting evidence", true);
            RequireIds(item.ContradictingEvidenceIds, 16, "documentation contradicting evidence", false);
        }
    }

    public static void Validate(CandidateDeliveredExpansionContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaId != ContractConstants.CandidateDeliveredExpansionSchemaId
            || value.SchemaVersion != CandidateDeliveredInputIdentity.Version
            || value.SubjectCount is < 1 or > MaximumFacts)
        {
            throw new InvalidOperationException("Candidate delivered expansion requires its exact header and bounded subject count.");
        }
        int series = checked(value.LinkSeries.Count + value.FaceGenSeries.Count + value.DocumentationSeries.Count + value.CoverageGapSeries.Count);
        if (series is < 1 or > MaximumSeries)
        {
            throw new InvalidOperationException("Candidate delivered expansion requires a bounded nonempty series set.");
        }
        foreach (CandidateDeliveredLinkSeriesContract item in value.LinkSeries)
        {
            RequireEvery(item.Every); RequireToken(item.Field, 128, "link field");
            if (item.Component is not null)
            {
                RequireToken(item.Component, 128, "link component");
            }
            if (item.Patterns.Count is < 1 or > 64)
            {
                throw new InvalidOperationException("Link series require bounded patterns.");
            }
            foreach (CandidateDeliveredLinkPatternContract pattern in item.Patterns)
            {
                if (pattern.PriorState == CandidateDeliveredLinkState.Unspecified || pattern.WinningState == CandidateDeliveredLinkState.Unspecified)
                {
                    throw new InvalidOperationException("Link expansion patterns require closed states.");
                }
                RequireOffset(pattern.PriorState, pattern.PriorTargetOffset, value.SubjectCount);
                RequireOffset(pattern.WinningState, pattern.WinningTargetOffset, value.SubjectCount);
            }
        }
        foreach (CandidateDeliveredFaceGenSeriesContract item in value.FaceGenSeries)
        {
            RequireEvery(item.Every);
            if (item.Patterns.Count is < 1 or > 64)
            {
                throw new InvalidOperationException("FaceGen series require bounded patterns.");
            }
            foreach (CandidateDeliveredFaceGenPatternContract pattern in item.Patterns)
            {
                if (pattern.Applicability == CandidateDeliveredFaceGenApplicability.Unspecified
                    || pattern.MeshAvailability == CandidateDeliveredAssetAvailability.Unspecified
                    || pattern.TintAvailability == CandidateDeliveredAssetAvailability.Unspecified
                    || pattern.Locality is < 0 or > 100 || pattern.Specificity is < 0 or > 100
                    || pattern.MeshProviderPresent != (pattern.MeshAvailability == CandidateDeliveredAssetAvailability.Present)
                    || pattern.TintProviderPresent != (pattern.TintAvailability == CandidateDeliveredAssetAvailability.Present))
                {
                    throw new InvalidOperationException("FaceGen expansion patterns require closed factual states.");
                }
            }
        }
        foreach (CandidateDeliveredDocumentationSeriesContract item in value.DocumentationSeries)
        {
            RequireEvery(item.Every);
            if (item.Patterns.Count is < 1 or > 64 || item.Patterns.Any(pattern => pattern.Applicability == ClaimApplicabilityState.Unspecified))
            {
                throw new InvalidOperationException("Documentation series require bounded closed factual patterns.");
            }
        }
        foreach (CandidateDeliveredGapSeriesContract item in value.CoverageGapSeries)
        {
            RequireEvery(item.Every); RequireText(item.MissingCapability, 512, "missing capability"); RequireText(item.Reason, 2048, "gap reason");
        }
        long rows = 0;
        foreach (int every in value.LinkSeries.Select(item => item.Every)
            .Concat(value.FaceGenSeries.Select(item => item.Every))
            .Concat(value.DocumentationSeries.Select(item => item.Every))
            .Concat(value.CoverageGapSeries.Select(item => item.Every)))
        {
            rows = checked(rows + ((long)value.SubjectCount + every - 1) / every);
        }
        if (rows > MaximumFacts)
        {
            throw new InvalidOperationException("Candidate delivered expansion exceeds the bounded fact population.");
        }
    }

    private static void RequireTargetCorrelation(CandidateDeliveredLinkState state, OpaqueId? target)
    {
        if ((state == CandidateDeliveredLinkState.Resolved) != (target is not null))
        {
            throw new InvalidOperationException("Only resolved delivered links may retain a target participant.");
        }
    }

    private static void RequireOffset(CandidateDeliveredLinkState state, int? offset, int count)
    {
        if ((state == CandidateDeliveredLinkState.Resolved) != offset.HasValue || offset is < 0 || offset >= count)
        {
            throw new InvalidOperationException("Only resolved link patterns require a bounded target offset.");
        }
    }

    private static void RequireEvery(int every)
    {
        if (every is < 1 or > MaximumFacts)
        {
            throw new InvalidOperationException("Expansion cadence must be bounded and positive.");
        }
    }

    private static void RequireIds(IReadOnlyList<OpaqueId> values, int maximum, string label, bool nonempty)
    {
        if ((nonempty && values.Count == 0) || values.Count > maximum || values.Distinct().Count() != values.Count)
        {
            throw new InvalidOperationException($"{label} must be {(nonempty ? "nonempty, " : string.Empty)}unique, and bounded.");
        }
    }

    private static void RequireUnique(IEnumerable<OpaqueId> values, string label)
    {
        OpaqueId[] retained = values.ToArray();
        if (retained.Distinct().Count() != retained.Length)
        {
            throw new InvalidOperationException($"{label} must be unique.");
        }
    }

    private static void RequireToken(string value, int maximum, string label)
    {
        RequireText(value, maximum, label);
        if (value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new InvalidOperationException($"{label} must use the closed token alphabet.");
        }
    }

    private static void RequireText(string value, int maximum, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsSurrogate))
        {
            throw new InvalidOperationException($"{label} must be bounded nonempty scalar text.");
        }
    }
}
