using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Candidates;

public sealed record CandidateDeliveredExpansionMeasurement(
    long LinkFacts,
    long FaceGenFacts,
    long DocumentationFacts,
    long CoverageGapFacts,
    long TotalFacts,
    Sha256Fingerprint FactStreamFingerprint);

public static class CandidateDeliveredInputExpander
{
    public const int MaximumMaterializedFacts = 100_000;

    public static CandidateDeliveredInputContract Expand(CandidateDeliveredExpansionContract expansion)
    {
        CandidateDeliveredContractInvariants.Validate(expansion);
        CandidateDeliveredExpansionMeasurement measurement = Measure(expansion);
        if (measurement.TotalFacts > MaximumMaterializedFacts)
        {
            throw new InvalidOperationException("The delivered expansion exceeds the bounded materialization limit; use streaming measurement for stress evidence.");
        }

        CandidateDeliveredInputContract result = new(
            ContractConstants.CandidateDeliveredInputSchemaId,
            CandidateDeliveredInputIdentity.Version,
            new OpaqueId("candidate-delivered-input-pending"),
            expansion.OriginatingRunId,
            expansion.SourceSnapshotId,
            expansion.AnalysisContextId,
            expansion.ConfigurationId,
            EnumerateLinkFacts(expansion).ToArray(),
            EnumerateFaceGenFacts(expansion).ToArray(),
            EnumerateGapFacts(expansion).ToArray(),
            EnumerateDocumentationFacts(expansion).ToArray());
        result = result with { PayloadId = CandidateDeliveredInputIdentity.ComputePayloadId(result) };
        CandidateDeliveredContractInvariants.Validate(result);
        return result;
    }

    public static CandidateDeliveredExpansionMeasurement Measure(CandidateDeliveredExpansionContract expansion)
    {
        CandidateDeliveredContractInvariants.Validate(expansion);
        long links = Count(expansion.SubjectCount, expansion.LinkSeries.Select(item => item.Every));
        long faceGen = Count(expansion.SubjectCount, expansion.FaceGenSeries.Select(item => item.Every));
        long documentation = Count(expansion.SubjectCount, expansion.DocumentationSeries.Select(item => item.Every));
        long gaps = Count(expansion.SubjectCount, expansion.CoverageGapSeries.Select(item => item.Every));
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        UTF8Encoding utf8 = new(false, true);
        foreach (string descriptor in EnumerateFactDescriptors(expansion))
        {
            byte[] bytes = utf8.GetBytes(descriptor);
            hash.AppendData(utf8.GetBytes(FormattableString.Invariant($"{bytes.Length:x8}:")));
            hash.AppendData(bytes);
        }
        return new(links, faceGen, documentation, gaps, checked(links + faceGen + documentation + gaps),
            new Sha256Fingerprint(Convert.ToHexStringLower(hash.GetHashAndReset())));
    }

    private static IEnumerable<string> EnumerateFactDescriptors(CandidateDeliveredExpansionContract expansion)
    {
        yield return SemanticRecord("header", expansion.OriginatingRunId.Value, expansion.SourceSnapshotId.Value,
            expansion.AnalysisContextId.Value, expansion.ConfigurationId.Value,
            expansion.SubjectCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

        for (int seriesIndex = 0; seriesIndex < expansion.LinkSeries.Count; seriesIndex++)
        {
            CandidateDeliveredLinkSeriesContract series = expansion.LinkSeries[seriesIndex];
            int emitted = 0;
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every, emitted++)
            {
                int patternIndex = emitted % series.Patterns.Count;
                CandidateDeliveredLinkPatternContract pattern = series.Patterns[patternIndex];
                yield return SemanticRecord("link", Number(seriesIndex), Number(subjectIndex), Number(patternIndex),
                    series.Field, series.Component ?? "none", Number(seriesIndex), LinkState(pattern.PriorState),
                    TargetOrdinal(pattern.PriorTargetOffset, subjectIndex, expansion.SubjectCount),
                    LinkState(pattern.WinningState),
                    TargetOrdinal(pattern.WinningTargetOffset, subjectIndex, expansion.SubjectCount));
            }
        }

        for (int seriesIndex = 0; seriesIndex < expansion.FaceGenSeries.Count; seriesIndex++)
        {
            CandidateDeliveredFaceGenSeriesContract series = expansion.FaceGenSeries[seriesIndex];
            int emitted = 0;
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every, emitted++)
            {
                int patternIndex = emitted % series.Patterns.Count;
                CandidateDeliveredFaceGenPatternContract pattern = series.Patterns[patternIndex];
                yield return SemanticRecord("facegen", Number(seriesIndex), Number(subjectIndex), Number(patternIndex),
                    FaceGenApplicability(pattern.Applicability), AssetAvailability(pattern.MeshAvailability),
                    Boolean(pattern.MeshProviderPresent), AssetAvailability(pattern.TintAvailability),
                    Boolean(pattern.TintProviderPresent), Number(pattern.Locality), Number(pattern.Specificity));
            }
        }

        for (int seriesIndex = 0; seriesIndex < expansion.DocumentationSeries.Count; seriesIndex++)
        {
            CandidateDeliveredDocumentationSeriesContract series = expansion.DocumentationSeries[seriesIndex];
            int emitted = 0;
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every, emitted++)
            {
                int patternIndex = emitted % series.Patterns.Count;
                CandidateDeliveredDocumentationPatternContract pattern = series.Patterns[patternIndex];
                yield return SemanticRecord("documentation", Number(seriesIndex), Number(subjectIndex), Number(patternIndex),
                    DocumentationApplicability(pattern.Applicability), Boolean(pattern.SupplyingSnapshotMatches),
                    Boolean(pattern.AnalysisContextMatches), Boolean(pattern.HasContradictingEvidence));
            }
        }

        for (int seriesIndex = 0; seriesIndex < expansion.CoverageGapSeries.Count; seriesIndex++)
        {
            CandidateDeliveredGapSeriesContract series = expansion.CoverageGapSeries[seriesIndex];
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every)
            {
                yield return SemanticRecord("coverage-gap", Number(seriesIndex), Number(subjectIndex), "0",
                    series.MissingCapability, series.Reason, "1");
            }
        }
    }

    private static string SemanticRecord(params string[] fields)
    {
        UTF8Encoding utf8 = new(false, true);
        StringBuilder result = new();
        foreach (string field in fields)
        {
            result.Append(FormattableString.Invariant($"{utf8.GetByteCount(field):x8}:"));
            result.Append(field);
        }
        return result.ToString();
    }

    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Boolean(bool value) => value ? "true" : "false";
    private static string TargetOrdinal(int? offset, int subjectIndex, int subjectCount) => offset is null
        ? "none"
        : Number((int)(((long)subjectIndex + offset.Value) % subjectCount));
    private static string LinkState(CandidateDeliveredLinkState value) => value switch
    {
        CandidateDeliveredLinkState.Absent => "absent",
        CandidateDeliveredLinkState.Null => "null",
        CandidateDeliveredLinkState.Resolved => "resolved",
        CandidateDeliveredLinkState.Unresolved => "unresolved",
        _ => throw new InvalidDataException("The link state is not closed.")
    };
    private static string FaceGenApplicability(CandidateDeliveredFaceGenApplicability value) => value switch
    {
        CandidateDeliveredFaceGenApplicability.Applicable => "applicable",
        CandidateDeliveredFaceGenApplicability.NotApplicable => "not-applicable",
        CandidateDeliveredFaceGenApplicability.Unknown => "unknown",
        _ => throw new InvalidDataException("The FaceGen applicability is not closed.")
    };
    private static string AssetAvailability(CandidateDeliveredAssetAvailability value) => value switch
    {
        CandidateDeliveredAssetAvailability.Present => "present",
        CandidateDeliveredAssetAvailability.Absent => "absent",
        CandidateDeliveredAssetAvailability.Unknown => "unknown",
        _ => throw new InvalidDataException("The asset availability is not closed.")
    };
    private static string DocumentationApplicability(ClaimApplicabilityState value) => value switch
    {
        ClaimApplicabilityState.Applicable => "applicable",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "contradicted",
        _ => throw new InvalidDataException("The documentation applicability is not closed.")
    };

    private static IEnumerable<CandidateDeliveredLinkFactContract> EnumerateLinkFacts(CandidateDeliveredExpansionContract expansion)
    {
        for (int seriesIndex = 0; seriesIndex < expansion.LinkSeries.Count; seriesIndex++)
        {
            CandidateDeliveredLinkSeriesContract series = expansion.LinkSeries[seriesIndex];
            int emitted = 0;
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every, emitted++)
            {
                CandidateDeliveredLinkPatternContract pattern = series.Patterns[emitted % series.Patterns.Count];
                OpaqueId factId = ExpansionFactId("link", seriesIndex, subjectIndex);
                yield return new(
                    factId,
                    Subject(expansion, "link-subject", seriesIndex, subjectIndex),
                    Id(expansion, "prior-contribution", seriesIndex, subjectIndex),
                    Id(expansion, "winning-contribution", seriesIndex, subjectIndex),
                    $"prior-source/{seriesIndex:D3}/{subjectIndex:D8}",
                    $"winning-source/{seriesIndex:D3}/{subjectIndex:D8}",
                    series.Field,
                    series.Component,
                    seriesIndex,
                    pattern.PriorState,
                    Target(expansion, "link-subject", seriesIndex, pattern.PriorTargetOffset, subjectIndex, expansion.SubjectCount),
                    pattern.WinningState,
                    Target(expansion, "link-subject", seriesIndex, pattern.WinningTargetOffset, subjectIndex, expansion.SubjectCount),
                    [expansion.SourceSnapshotId, Id(expansion, "link-dependency", seriesIndex, subjectIndex)],
                    [Id(expansion, "link-evidence-prior", seriesIndex, subjectIndex), Id(expansion, "link-evidence-winner", seriesIndex, subjectIndex)]);
            }
        }
    }

    private static IEnumerable<CandidateDeliveredFaceGenFactContract> EnumerateFaceGenFacts(CandidateDeliveredExpansionContract expansion)
    {
        for (int seriesIndex = 0; seriesIndex < expansion.FaceGenSeries.Count; seriesIndex++)
        {
            CandidateDeliveredFaceGenSeriesContract series = expansion.FaceGenSeries[seriesIndex];
            int emitted = 0;
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every, emitted++)
            {
                CandidateDeliveredFaceGenPatternContract pattern = series.Patterns[emitted % series.Patterns.Count];
                yield return new(
                    ExpansionFactId("facegen", seriesIndex, subjectIndex),
                    Subject(expansion, "facegen-subject", seriesIndex, subjectIndex),
                    pattern.Applicability,
                    Id(expansion, "mesh-asset", seriesIndex, subjectIndex),
                    pattern.MeshAvailability,
                    pattern.MeshProviderPresent ? Id(expansion, "mesh-provider", seriesIndex, subjectIndex) : null,
                    Id(expansion, "tint-asset", seriesIndex, subjectIndex),
                    pattern.TintAvailability,
                    pattern.TintProviderPresent ? Id(expansion, "tint-provider", seriesIndex, subjectIndex) : null,
                    pattern.Locality,
                    pattern.Specificity,
                    [expansion.SourceSnapshotId, Id(expansion, "facegen-dependency", seriesIndex, subjectIndex)],
                    [Id(expansion, "facegen-evidence", seriesIndex, subjectIndex)]);
            }
        }
    }

    private static IEnumerable<CandidateDeliveredDocumentationFactContract> EnumerateDocumentationFacts(CandidateDeliveredExpansionContract expansion)
    {
        for (int seriesIndex = 0; seriesIndex < expansion.DocumentationSeries.Count; seriesIndex++)
        {
            CandidateDeliveredDocumentationSeriesContract series = expansion.DocumentationSeries[seriesIndex];
            int emitted = 0;
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every, emitted++)
            {
                CandidateDeliveredDocumentationPatternContract pattern = series.Patterns[emitted % series.Patterns.Count];
                OpaqueId claim = Id(expansion, "claim", seriesIndex, subjectIndex);
                OpaqueId passage = Id(expansion, "passage", seriesIndex, subjectIndex);
                OpaqueId revision = Id(expansion, "revision", seriesIndex, subjectIndex);
                yield return new(
                    ExpansionFactId("documentation", seriesIndex, subjectIndex),
                    Id(expansion, "application", seriesIndex, subjectIndex), claim, passage, revision,
                    Subject(expansion, "documentation-subject", seriesIndex, subjectIndex),
                    expansion.OriginatingRunId,
                    pattern.SupplyingSnapshotMatches ? expansion.SourceSnapshotId : Id(expansion, "other-snapshot", seriesIndex, subjectIndex),
                    pattern.AnalysisContextMatches ? expansion.AnalysisContextId : Id(expansion, "other-context", seriesIndex, subjectIndex),
                    pattern.Applicability,
                    [expansion.SourceSnapshotId, Id(expansion, "documentation-dependency", seriesIndex, subjectIndex), passage, revision],
                    [Id(expansion, "documentation-evidence", seriesIndex, subjectIndex)],
                    pattern.HasContradictingEvidence ? [Id(expansion, "documentation-contradiction", seriesIndex, subjectIndex)] : []);
            }
        }
    }

    private static IEnumerable<CandidateDeliveredCoverageGapFactContract> EnumerateGapFacts(CandidateDeliveredExpansionContract expansion)
    {
        for (int seriesIndex = 0; seriesIndex < expansion.CoverageGapSeries.Count; seriesIndex++)
        {
            CandidateDeliveredGapSeriesContract series = expansion.CoverageGapSeries[seriesIndex];
            for (int subjectIndex = 0; subjectIndex < expansion.SubjectCount; subjectIndex += series.Every)
            {
                yield return new(
                    ExpansionFactId("coverage-gap", seriesIndex, subjectIndex),
                    Subject(expansion, "coverage-gap-subject", seriesIndex, subjectIndex), 1,
                    series.MissingCapability, series.Reason,
                    [expansion.SourceSnapshotId, Id(expansion, "coverage-gap-dependency", seriesIndex, subjectIndex)],
                    [Id(expansion, "coverage-gap-evidence", seriesIndex, subjectIndex)]);
            }
        }
    }

    private static OpaqueId Subject(
        CandidateDeliveredExpansionContract expansion,
        string kind,
        int series,
        int index) => Id(expansion, kind, series, index);

    private static OpaqueId ExpansionFactId(string kind, int series, int subject) =>
        new($"candidate-{kind}-fact-s{series:D3}-n{subject:D8}");

    private static OpaqueId? Target(
        CandidateDeliveredExpansionContract expansion,
        string kind,
        int series,
        int? offset,
        int subjectIndex,
        int subjectCount) => offset is null
        ? null
        : Subject(expansion, kind, series, (int)(((long)subjectIndex + offset.Value) % subjectCount));

    private static OpaqueId Id(CandidateDeliveredExpansionContract expansion, string kind, int series, int subject) =>
        CandidateAnalysisIdentity.StableId("candidate-delivered-expansion", expansion.SourceSnapshotId.Value,
            expansion.ConfigurationId.Value, kind, series.ToString(System.Globalization.CultureInfo.InvariantCulture),
            subject.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static long Count(int subjects, IEnumerable<int> everyValues)
    {
        long total = 0;
        foreach (int every in everyValues)
        {
            total = checked(total + ((long)subjects + every - 1) / every);
        }
        return total;
    }
}
