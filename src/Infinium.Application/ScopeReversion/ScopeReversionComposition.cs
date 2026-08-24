using System.Security.Cryptography;
using System.Text;
using Infinium.Analysis.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.ScopeReversion;

public sealed record ScopeReversionCompositionRequest(
    OpaqueId OriginatingRunId,
    OpaqueId ConfigurationId,
    IReadOnlyList<string> EnabledAdapterIds,
    IReadOnlyList<ScopeReversionSourceBindingContract> Sources,
    IReadOnlyList<ActorScopeReversionInput> ActorInputs,
    IReadOnlyList<PlacedReferenceScopeReversionInput> ReferenceInputs)
{
    public AnalysisExecutionInputContract? ExecutionInput { get; init; }
}

public sealed record ScopeReversionPipelineResult(
    ScopeReversionWorkAssignmentContract Assignment,
    ScopeReversionAnalysisContract Analysis,
    byte[] CanonicalJson,
    string HumanSummary);

public static class ScopeReversionComposition
{
    private static readonly string[] RegisteredAdapters =
    [
        ActorScopeReversionAdapter.StableAdapterId,
        PlacedReferenceScopeReversionAdapter.StableAdapterId,
    ];

    private static readonly ExecutionBoundaryContract[] NotUsedBoundaries =
    [
        new("provider", BoundaryUseState.NotUsed, "deterministic local analyzer"),
        new("hosted-search", BoundaryUseState.NotUsed, "deterministic local analyzer"),
        new("nexus", BoundaryUseState.NotUsed, "deterministic local analyzer"),
        new("loot", BoundaryUseState.NotUsed, "deterministic local analyzer"),
    ];

    public static ScopeReversionWorkAssignmentContract Compose(ScopeReversionCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AnalysisExecutionInputContract executionInput = request.ExecutionInput
            ?? throw new InvalidDataException("Scope-reversion composition requires an admitted analysis execution input.");
        AnalysisExecutionContractInvariants.Validate(executionInput);
        if (executionInput.RunId != request.OriginatingRunId
            || executionInput.EffectiveConfiguration.ArtifactId != request.ConfigurationId
            || executionInput.Boundaries.Any(item => item.State != BoundaryUseState.NotUsed)
            || !executionInput.Boundaries.Select(item => item.BoundaryId).Order(StringComparer.Ordinal)
                .SequenceEqual(NotUsedBoundaries.Select(item => item.BoundaryId).Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidDataException("Scope-reversion run, configuration, or local-only boundary differs from the admitted execution input.");
        }
        string[] enabled = request.EnabledAdapterIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (enabled.Any(item => !RegisteredAdapters.Contains(item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Scope-reversion configuration enables an unregistered adapter.");
        }

        ActorScopeReversionAdapter actorAdapter = new();
        PlacedReferenceScopeReversionAdapter referenceAdapter = new();
        ScopeReversionMemberContract[] members = request.ActorInputs.Select(actorAdapter.Adapt)
            .Concat(request.ReferenceInputs.Select(referenceAdapter.Adapt))
            .OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
            .ToArray();
        AnalyzerDeclarationContract declaration = ScopeReversionAnalyzerDeclaration.Create();
        string canonicalDeclaration = AnalyzerDeclarationJsonCodec.Serialize(declaration);
        Sha256Fingerprint declarationFingerprint = Hash(Encoding.UTF8.GetBytes(canonicalDeclaration));
        ArtifactReferenceContract declarationReference = executionInput.AnalyzerDeclarations.SingleOrDefault(item =>
            item.ArtifactId.Value == declaration.AnalyzerId)
            ?? throw new InvalidDataException("The admitted execution input does not register the scope-reversion analyzer declaration.");
        if (declarationReference.ArtifactVersion != declaration.AnalyzerVersion
            || declarationReference.Fingerprint != declarationFingerprint
            || declarationReference.Availability != "retained")
        {
            throw new InvalidDataException("The admitted scope-reversion analyzer declaration identity or retained fingerprint drifted.");
        }
        ScopeReversionAnalyzerBindingContract binding = new(
            declaration.AnalyzerFamily,
            declaration.AnalyzerId,
            declaration.AnalyzerVersion,
            declaration.SemanticContractVersion,
            declaration.IdentityContractVersion,
            declaration.RulesetVersion,
            declarationFingerprint,
            canonicalDeclaration,
            declaration.Maturity);
        ScopeReversionSourceBindingContract[] sources = request.Sources
            .OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal)
            .ToArray();
        ArtifactReferenceContract[] admittedSources = executionInput.SourceInputs
            .OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal)
            .ToArray();
        if (sources.Length != admittedSources.Length
            || sources.Where((item, index) => item.ArtifactId != admittedSources[index].ArtifactId
                || item.SchemaVersion != admittedSources[index].ArtifactVersion
                || item.Fingerprint != admittedSources[index].Fingerprint
                || item.Availability != admittedSources[index].Availability).Any())
        {
            throw new InvalidDataException("Scope-reversion sources differ from the exact admitted execution-input source list.");
        }
        ScopeReversionConfigurationContract configurationWithoutFingerprint = new(
            request.ConfigurationId,
            new Sha256Fingerprint(new string('0', 64)),
            RegisteredAdapters,
            enabled,
            executionInput.Limits.MaximumEntities,
            executionInput.Limits.MaximumOutputItems,
            executionInput.Limits.MaximumWallTimeMilliseconds);
        Sha256Fingerprint configurationFingerprint =
            ScopeReversionContractInvariants.ComputeConfigurationFingerprint(configurationWithoutFingerprint);
        if (executionInput.EffectiveConfiguration.Fingerprint != configurationFingerprint)
        {
            throw new InvalidDataException(
                "Scope-reversion effective configuration fingerprint differs from the admitted execution input.");
        }
        ScopeReversionConfigurationContract configuration = configurationWithoutFingerprint with
        {
            Fingerprint = configurationFingerprint,
        };
        Sha256Fingerprint inputFingerprint = ScopeReversionContractInvariants.ComputeInputFingerprint(
            request.OriginatingRunId,
            configuration,
            sources,
            members,
            binding.DeclarationFingerprint);
        OpaqueId assignmentId = ScopeReversionContractInvariants.ComputeAssignmentId(
            request.OriginatingRunId, inputFingerprint);
        ScopeReversionWorkAssignmentContract assignment = new(
            ContractConstants.ScopeReversionSchemaId,
            new ContractVersion(1, 0, 0),
            assignmentId,
            request.OriginatingRunId,
            binding,
            configuration,
            sources,
            members,
            inputFingerprint,
            NotUsedBoundaries);
        ScopeReversionContractInvariants.Validate(assignment);
        return assignment;
    }

    public static ScopeReversionPipelineResult Execute(ScopeReversionCompositionRequest request)
    {
        ScopeReversionWorkAssignmentContract assignment = Compose(request);
        ScopeReversionAnalysisContract analysis = ScopeReversionAnalyzer.Execute(assignment);
        byte[] bytes = ScopeReversionJsonCodec.Serialize(analysis);
        ScopeReversionAnalysisContract roundTrip = ScopeReversionJsonCodec.Deserialize(bytes);
        byte[] canonicalRoundTrip = ScopeReversionJsonCodec.Serialize(roundTrip);
        if (!bytes.AsSpan().SequenceEqual(canonicalRoundTrip)
            || roundTrip.PayloadId != analysis.PayloadId)
        {
            throw new InvalidDataException("Scope-reversion publication did not preserve canonical bytes and identity.");
        }
        return new ScopeReversionPipelineResult(
            assignment,
            analysis,
            bytes,
            ScopeReversionOutputRenderer.RenderHuman(analysis));
    }

    private static Sha256Fingerprint Hash(ReadOnlySpan<byte> bytes) => new(
        Convert.ToHexStringLower(SHA256.HashData(bytes)));
}
