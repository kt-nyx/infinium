using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Analysis.ScopeReversion;
using Infinium.Application.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.PublicFixtures;

public sealed record ScopeReversionExpectedMember(
    string MemberId,
    string Disposition,
    int Findings,
    int Cases);

public sealed record ScopeReversionExpectedAggregate(
    int Population,
    int SupportedFindings,
    int ResolvedNegative,
    int Abstentions,
    int Findings,
    int Cases,
    int Recommendations,
    int ExternalBoundariesUsed);

public sealed record ScopeReversionFixtureExpectations(
    IReadOnlyList<ScopeReversionExpectedMember> Expected,
    ScopeReversionExpectedAggregate Aggregate);

public sealed record ScopeReversionFixturePackage(
    string PackageDirectory,
    string InputSha256,
    string ExpectationsSha256,
    ScopeReversionCompositionRequest Request,
    ScopeReversionFixtureExpectations Expectations);

public static class ScopeReversionFixtureReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static ScopeReversionFixturePackage Read(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string directory = Path.Combine(
            Path.GetFullPath(repositoryRoot), "fixtures", "public", "analysis", "scope-reversion", "synthetic-bounded-cases-v1");
        string inputPath = Path.Combine(directory, "input.v1.json");
        string expectationsPath = Path.Combine(directory, "expectations.v1.json");
        string manifestPath = Path.Combine(directory, "conformance-manifest.v1.json");
        byte[] inputBytes = File.ReadAllBytes(inputPath);
        byte[] expectationBytes = File.ReadAllBytes(expectationsPath);
        ConformanceManifest manifest = JsonSerializer.Deserialize<ConformanceManifest>(
            File.ReadAllBytes(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("Scope-reversion conformance manifest is empty.");
        Verify(manifest.Input, inputPath, inputBytes);
        Verify(manifest.Expectations, expectationsPath, expectationBytes);
        string[] requiredEvaluations =
        [
            "EVAL-0001", "EVAL-0002", "EVAL-0032", "EVAL-0065", "EVAL-0067",
            "EVAL-0083", "EVAL-0084", "EVAL-0085", "EVAL-0086",
        ];
        if (manifest.SchemaId != "infinium.evaluation.developer-conformance-manifest/v1"
            || manifest.SchemaVersion != "1.0.0"
            || manifest.PackageIdentity != "scope-reversion-synthetic-bounded-cases-v1"
            || manifest.PackageVersion != "1.0.0"
            || manifest.Partition != "development"
            || manifest.SemanticOracle || manifest.VerdictAuthority
            || manifest.EvidenceClass != "developer-owned-product-conformance"
            || manifest.Input.AnswerFree != true
            || manifest.Expectations.Kind != "developer-owned-expected-outcomes-not-semantic-oracle"
            || !manifest.EvaluationIds.SequenceEqual(requiredEvaluations, StringComparer.Ordinal)
            || !manifest.Domains.SequenceEqual(["actor-ai-facegen", "refr-link-placement"], StringComparer.Ordinal)
            || !DateTimeOffset.TryParse(manifest.CreatedAt, out _))
        {
            throw new InvalidDataException("Scope-reversion fixture is not bounded developer-owned conformance evidence.");
        }

        FixtureInput input = JsonSerializer.Deserialize<FixtureInput>(inputBytes, JsonOptions)
            ?? throw new InvalidDataException("Scope-reversion fixture input is empty.");
        FixtureExpectations expected = JsonSerializer.Deserialize<FixtureExpectations>(expectationBytes, JsonOptions)
            ?? throw new InvalidDataException("Scope-reversion fixture expectations are empty.");
        if (input.SemanticOracle || expected.SemanticOracle || expected.VerdictAuthority
            || input.PackageIdentity != manifest.PackageIdentity
            || expected.PackageIdentity != manifest.PackageIdentity
            || input.PackageVersion != manifest.PackageVersion
            || input.EvidenceClass != manifest.EvidenceClass
            || expected.EvidenceClass != manifest.EvidenceClass
            || expected.ClaimKind != "bounded-developer-conformance-only")
        {
            throw new InvalidDataException("Scope-reversion fixture authority or package identity drifted.");
        }

        List<ActorScopeReversionInput> actors = [];
        List<PlacedReferenceScopeReversionInput> references = [];
        foreach (FixtureCase item in input.Cases.OrderBy(value => value.MemberId, StringComparer.Ordinal))
        {
            OpaqueId[] dependencies = item.DependencyIds.Select(value => new OpaqueId(value))
                .OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
            OpaqueId[] evidence = item.EvidenceIds.Select(value => new OpaqueId(value))
                .OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
            if (item.Domain == "actor-ai-facegen")
            {
                actors.Add(new ActorScopeReversionInput(
                    new(item.MemberId), new(item.SubjectId), new(item.MemberId + "-prior"),
                    new(item.MemberId + "-winner"), item.PriorFeature, item.WinningFeature,
                    item.SecondaryChangePrior, item.SecondaryChangeWinner,
                    Support(item.PurposeSupport), Applicability(item.PurposeApplicability),
                    item.PurposeDimensions, item.IntentionalDimensions, Contradiction(item.Contradiction),
                    Closure(item.CausalClosure), new("closure-" + item.MemberId), dependencies, evidence,
                    ScopePublicationEligibility.Eligible, CoverageMemberState.Completed,
                    ScopeGapFailureState.None, null));
            }
            else if (item.Domain == "refr-link-placement")
            {
                references.Add(new PlacedReferenceScopeReversionInput(
                    new(item.MemberId), new(item.SubjectId), new(item.MemberId + "-prior"),
                    new(item.MemberId + "-winner"), item.PriorFeature, item.WinningFeature,
                    item.SecondaryChangePrior, item.SecondaryChangeWinner,
                    Support(item.PurposeSupport), Applicability(item.PurposeApplicability),
                    item.PurposeDimensions, item.IntentionalDimensions, Contradiction(item.Contradiction),
                    Closure(item.CausalClosure), new("closure-" + item.MemberId), dependencies, evidence,
                    ScopePublicationEligibility.Eligible, CoverageMemberState.Completed,
                    ScopeGapFailureState.None, null));
            }
            else
            {
                throw new InvalidDataException($"Unsupported scope-reversion fixture domain '{item.Domain}'.");
            }
        }

        string inputSha = Hash(inputBytes);
        ScopeReversionSourceBindingContract source = new(
            new OpaqueId("scope-reversion-synthetic-input"),
            "infinium.evaluation.scope-reversion-input/v1",
            new ContractVersion(1, 0, 0),
            new Sha256Fingerprint(inputSha),
            "retained");
        AnalyzerDeclarationContract declaration = ScopeReversionAnalyzerDeclaration.Create();
        Sha256Fingerprint declarationFingerprint = new(Hash(
            System.Text.Encoding.UTF8.GetBytes(AnalyzerDeclarationJsonCodec.Serialize(declaration))));
        ArtifactReferenceContract Reference(string id, string fingerprint) => new(
            new OpaqueId(id), new ContractVersion(1, 0, 0), new Sha256Fingerprint(fingerprint), "retained");
        string syntheticHash = new('a', 64);
        AnalysisExecutionLimitsContract limits = new(100_000, 200_000, 100_000, 100_000, 120_000);
        string[] enabledAdapters =
        [
            ActorScopeReversionAdapter.StableAdapterId,
            PlacedReferenceScopeReversionAdapter.StableAdapterId,
        ];
        ScopeReversionConfigurationContract configurationWithoutFingerprint = new(
            new OpaqueId("scope-reversion-synthetic-configuration"),
            new Sha256Fingerprint(new string('0', 64)),
            enabledAdapters,
            enabledAdapters,
            limits.MaximumEntities,
            limits.MaximumOutputItems,
            limits.MaximumWallTimeMilliseconds);
        Sha256Fingerprint configurationFingerprint =
            ScopeReversionContractInvariants.ComputeConfigurationFingerprint(configurationWithoutFingerprint);
        AnalysisExecutionInputContract executionInput = new(
            ContractConstants.AnalysisExecutionInputSchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId("scope-reversion-synthetic-execution"),
            new OpaqueId("scope-reversion-synthetic-run"),
            Reference("scope-reversion-synthetic-snapshot", syntheticHash),
            Reference("scope-reversion-synthetic-bethesda-substrate", syntheticHash),
            [new ArtifactReferenceContract(source.ArtifactId, source.SchemaVersion, source.Fingerprint, source.Availability)],
            [new ArtifactReferenceContract(new OpaqueId(declaration.AnalyzerId), declaration.AnalyzerVersion,
                declarationFingerprint, "retained")],
            Reference("scope-reversion-synthetic-configuration", configurationFingerprint.Value),
            Reference("scope-reversion-synthetic-manifest", syntheticHash),
            ReplayMode.Clean,
            null,
            7,
            limits,
            [
                new("provider", BoundaryUseState.NotUsed, "deterministic local analyzer"),
                new("hosted-search", BoundaryUseState.NotUsed, "deterministic local analyzer"),
                new("nexus", BoundaryUseState.NotUsed, "deterministic local analyzer"),
                new("loot", BoundaryUseState.NotUsed, "deterministic local analyzer"),
            ])
        {
            AnalysisContext = Reference("scope-reversion-synthetic-context", syntheticHash),
        };
        ScopeReversionCompositionRequest request = new(
            new OpaqueId("scope-reversion-synthetic-run"),
            new OpaqueId("scope-reversion-synthetic-configuration"),
            enabledAdapters,
            [source],
            actors,
            references)
        {
            ExecutionInput = executionInput,
        };
        ScopeReversionFixtureExpectations expectations = new(
            expected.Expected.Select(item => new ScopeReversionExpectedMember(
                item.MemberId, item.Disposition, item.Findings, item.Cases)).ToArray(),
            new ScopeReversionExpectedAggregate(
                expected.Aggregate.Population, expected.Aggregate.SupportedFindings,
                expected.Aggregate.ResolvedNegative, expected.Aggregate.Abstentions,
                expected.Aggregate.Findings, expected.Aggregate.Cases,
                expected.Aggregate.Recommendations, expected.Aggregate.ExternalBoundariesUsed));
        return new ScopeReversionFixturePackage(
            directory, inputSha, Hash(expectationBytes), request, expectations);
    }

    private static void Verify(ManifestArtifact artifact, string path, byte[] bytes)
    {
        if (Path.GetFileName(path) != artifact.Path
            || bytes.LongLength != artifact.Bytes
            || Hash(bytes) != artifact.Sha256)
        {
            throw new InvalidDataException($"Scope-reversion fixture artifact '{artifact.Path}' failed exact byte identity.");
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static ScopeSupportState Support(string value) => value switch
    {
        "supported" => ScopeSupportState.Supported,
        "unsupported" => ScopeSupportState.Unsupported,
        _ => throw new InvalidDataException($"Unsupported fixture support state '{value}'."),
    };

    private static ScopeApplicabilityState Applicability(string value) => value switch
    {
        "applicable" => ScopeApplicabilityState.Applicable,
        "unknown" => ScopeApplicabilityState.Unknown,
        "not-applicable" => ScopeApplicabilityState.NotApplicable,
        _ => throw new InvalidDataException($"Unsupported fixture applicability state '{value}'."),
    };

    private static ScopeContradictionState Contradiction(string value) => value switch
    {
        "none" => ScopeContradictionState.None,
        "intentional-change" => ScopeContradictionState.IntentionalChange,
        "unknown" => ScopeContradictionState.Unknown,
        _ => throw new InvalidDataException($"Unsupported fixture contradiction state '{value}'."),
    };

    private static ScopeCausalClosureState Closure(string value) => value switch
    {
        "closed" => ScopeCausalClosureState.Closed,
        "open" => ScopeCausalClosureState.Open,
        _ => throw new InvalidDataException($"Unsupported fixture closure state '{value}'."),
    };

    private sealed record ConformanceManifest(
        string SchemaId,
        string SchemaVersion,
        string PackageIdentity,
        string PackageVersion,
        string Partition,
        string EvidenceClass,
        bool SemanticOracle,
        bool VerdictAuthority,
        IReadOnlyList<string> EvaluationIds,
        ManifestArtifact Input,
        ManifestArtifact Expectations,
        IReadOnlyList<string> Domains,
        string CreatedAt);

    private sealed record ManifestArtifact(
        string Path,
        long Bytes,
        string Sha256,
        bool? AnswerFree = null,
        string? Kind = null);

    private sealed record FixtureInput(
        string PackageIdentity,
        string PackageVersion,
        string EvidenceClass,
        bool SemanticOracle,
        IReadOnlyList<FixtureCase> Cases);

    private sealed record FixtureCase(
        string MemberId,
        string Domain,
        string SubjectId,
        string? PriorFeature,
        string? WinningFeature,
        string SecondaryChangePrior,
        string SecondaryChangeWinner,
        string PurposeSupport,
        string PurposeApplicability,
        IReadOnlyList<string> PurposeDimensions,
        IReadOnlyList<string> IntentionalDimensions,
        string Contradiction,
        string CausalClosure,
        IReadOnlyList<string> DependencyIds,
        IReadOnlyList<string> EvidenceIds);

    private sealed record FixtureExpectations(
        string PackageIdentity,
        string EvidenceClass,
        bool SemanticOracle,
        IReadOnlyList<ExpectedMemberDto> Expected,
        ExpectedAggregateDto Aggregate,
        string ClaimKind,
        bool VerdictAuthority);

    private sealed record ExpectedMemberDto(string MemberId, string Disposition, int Findings, int Cases);

    private sealed record ExpectedAggregateDto(
        int Population,
        int SupportedFindings,
        int ResolvedNegative,
        int Abstentions,
        int Findings,
        int Cases,
        int Recommendations,
        int ExternalBoundariesUsed);
}
