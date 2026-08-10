using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DeliveredCandidateSourceTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void AnswerFreeExpansionFlowsThroughTheRealDeliveredSource()
    {
        CandidateDeliveredExpansionContract expansion = new(
            ContractConstants.CandidateDeliveredExpansionSchemaId,
            CandidateDeliveredInputIdentity.Version,
            Id("candidate-delivered-expansion-v1"), Id("run-delivered-source"), Id("snapshot-delivered-source"),
            Id("context-delivered-source"), Id("configuration-delivered-source"), 6,
            [new(1, "Race", null,
            [
                new(CandidateDeliveredLinkState.Resolved, CandidateDeliveredLinkState.Resolved, 1, 1),
                new(CandidateDeliveredLinkState.Resolved, CandidateDeliveredLinkState.Resolved, 1, 2),
                new(CandidateDeliveredLinkState.Unresolved, CandidateDeliveredLinkState.Unresolved, null, null),
            ])],
            [new(1,
            [
                new(CandidateDeliveredFaceGenApplicability.Applicable, CandidateDeliveredAssetAvailability.Present, true,
                    CandidateDeliveredAssetAvailability.Present, true, 9, 8),
                new(CandidateDeliveredFaceGenApplicability.NotApplicable, CandidateDeliveredAssetAvailability.Unknown, false,
                    CandidateDeliveredAssetAvailability.Unknown, false, 0, 0),
                new(CandidateDeliveredFaceGenApplicability.Unknown, CandidateDeliveredAssetAvailability.Unknown, false,
                    CandidateDeliveredAssetAvailability.Unknown, false, 0, 0),
            ])],
            [new(2,
            [
                new(ClaimApplicabilityState.Applicable, true, true, false),
                new(ClaimApplicabilityState.NotApplicable, true, true, false),
                new(ClaimApplicabilityState.Contradicted, true, true, true),
            ])],
            [new(3, "decoder capability", "The source explicitly reports an unsupported population.")]);

        IReadOnlyList<CausalJoinPopulationMember> members = new DeliveredIndexCandidatePopulationSource()
            .ConstructPopulation(new CandidatePopulationContext(
                null, expansion.OriginatingRunId, expansion.SourceSnapshotId,
                expansion.AnalysisContextId, expansion.ConfigurationId,
                DeliveredExpansion: expansion));

        Assert.AreEqual(17, members.Count);
        Assert.AreEqual(6, members.Count(item => item.JoinKind == "record-link-winner-comparison"));
        Assert.AreEqual(6, members.Count(item => item.JoinKind == "record-facegen-provider"));
        Assert.AreEqual(3, members.Count(item => item.JoinKind == "documentation-application"));
        Assert.AreEqual(2, members.Count(item => item.JoinKind == "delivered-coverage-gap"));
        Assert.AreEqual(5, members.Count(item => item.InputState == CausalJoinInputState.ResolvedNegative));
        Assert.AreEqual(5, members.Count(item => item.InputState == CausalJoinInputState.Ambiguous));
        Assert.AreEqual(2, members.Count(item => item.InputState == CausalJoinInputState.Unsupported));
        Assert.AreEqual(5, members.Count(item => item.InputState == CausalJoinInputState.Complete));
        Assert.IsTrue(members.Where(item => item.Lane == CandidateLane.OptionalRanked)
            .All(item => item.OptionalRank is > 0));
        Assert.IsTrue(members.All(item => item.Participants.All(participant => item.Path.Contains(participant.ParticipantId))));

        CandidateDeliveredExpansionContract otherSnapshot = expansion with
        {
            SourceSnapshotId = Id("snapshot-delivered-source-other"),
        };
        IReadOnlyList<CausalJoinPopulationMember> otherMembers = new DeliveredIndexCandidatePopulationSource()
            .ConstructPopulation(new CandidatePopulationContext(
                null, otherSnapshot.OriginatingRunId, otherSnapshot.SourceSnapshotId,
                otherSnapshot.AnalysisContextId, otherSnapshot.ConfigurationId,
                DeliveredExpansion: otherSnapshot));
        CollectionAssert.AreNotEquivalent(
            members.SelectMany(item => item.Participants).Select(item => item.ParticipantId).Distinct().ToArray(),
            otherMembers.SelectMany(item => item.Participants).Select(item => item.ParticipantId).Distinct().ToArray());
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Mutation")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Mutation")]
    public void PipelineBindsExactDeliveredBytesAndAnalyzerDeclaration()
    {
        CandidateDeliveredInputContract input = new(
            ContractConstants.CandidateDeliveredInputSchemaId, CandidateDeliveredInputIdentity.Version,
            Id("candidate-delivered-input-unit"), Id("run-delivered-bound"), Id("snapshot-delivered-bound"),
            Id("context-delivered-bound"), Id("configuration-delivered-bound"), [], [],
            [new(Id("gap-fact"), Id("population-fact"), 1, "decoder capability",
                "The source explicitly reports an unsupported population.",
                [Id("snapshot-delivered-bound"), Id("gap-dependency")], [Id("gap-evidence")])], []);
        byte[] bytes = CandidateDeliveredInputJsonCodec.Serialize(input);
        Sha256Fingerprint byteFingerprint = new(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        Assert.IsTrue(ContractJsonSerializer.Options.IsReadOnly);
        Assert.AreEqual(byteFingerprint, ContractJsonSerializer.Fingerprint(input));
        Assert.ThrowsExactly<InvalidOperationException>(() => ContractJsonSerializer.Options.WriteIndented = false);
        CollectionAssert.AreEqual(bytes, CandidateDeliveredInputJsonCodec.Serialize(input));
        DeliveredIndexCandidatePopulationSource source = new();
        Sha256Fingerprint analyzerFingerprint = CandidateAnalysisIdentity.StructuralHash(
            [System.Text.Json.JsonSerializer.Serialize(source.Declaration)]);
        AnalysisExecutionInputContract executionInput = new(
            ContractConstants.AnalysisExecutionInputSchemaId, new ContractVersion(1, 0, 0), Id("execution-delivered-bound"),
            input.OriginatingRunId,
            Reference(input.SourceSnapshotId, new string('1', 64)), Reference(Id("bethesda-delivered-bound"), new string('2', 64)),
            [new(input.PayloadId, CandidateDeliveredInputIdentity.Version, byteFingerprint, "retained")],
            [new(source.AnalyzerId, source.Declaration.AnalyzerVersion, analyzerFingerprint, "retained")],
            Reference(input.ConfigurationId, new string('3', 64)), Reference(Id("manifest-delivered-bound"), new string('4', 64)),
            ReplayMode.Clean, null, 0, new(10, 100, 10, 20, 120_000),
            [new("provider", BoundaryUseState.NotUsed, "local-only"),
             new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
             new("nexus", BoundaryUseState.NotUsed, "local-only"),
             new("loot", BoundaryUseState.NotUsed, "local-only")]);
        CandidatePopulationContext context = new(
            null, input.OriginatingRunId, input.SourceSnapshotId, input.AnalysisContextId,
            input.ConfigurationId, input, byteFingerprint,
            AdmittedDeliveredInputId: input.PayloadId);
        CandidatePipelineRequest request = new(
            input.OriginatingRunId, Id("population-delivered-bound"), Id("policy-delivered-bound"),
            Id("threshold-delivered-bound"), new(Id("limit-delivered-bound"), 10, 10), context, [source], executionInput);

        CandidatePipelineResult result = CandidatePipeline.Execute(request);
        Assert.AreEqual(1L, result.Analysis.Counts.Unsupported);
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidatePipeline.Execute(request with
        {
            Context = context with { DeliveredInputByteFingerprint = new Sha256Fingerprint(new string('f', 64)) },
        }));
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidatePipeline.Execute(request with
        {
            Context = context with { DeliveredInput = null, DeliveredInputByteFingerprint = null },
        }));
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidatePipeline.Execute(request with
        {
            Context = context with
            {
                DeliveredInput = input with
                {
                    CoverageGapFacts = [input.CoverageGapFacts[0] with
                        { Reason = "Different factual content under the same logical artifact ID." }],
                },
            },
        }));
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidatePipeline.Execute(request with
        {
            ExecutionInput = executionInput with
            {
                AnalyzerDeclarations = [executionInput.AnalyzerDeclarations[0] with
                    { Fingerprint = new Sha256Fingerprint(new string('e', 64)) }],
            },
        }));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Mutation")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Mutation")]
    public void PipelineBindsExactExpansionBytesBeforeTheRealSourceExpandsThem()
    {
        CandidateDeliveredExpansionContract expansion = new(
            ContractConstants.CandidateDeliveredExpansionSchemaId, CandidateDeliveredInputIdentity.Version,
            Id("candidate-expansion-bound"), Id("run-expansion-bound"), Id("snapshot-expansion-bound"),
            Id("context-expansion-bound"), Id("configuration-expansion-bound"), 1,
            [], [], [], [new(1, "decoder capability", "The decoder explicitly lacks this population.")]);
        byte[] bytes = CandidateDeliveredExpansionJsonCodec.Serialize(expansion);
        Sha256Fingerprint byteFingerprint = new(Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(bytes)));
        DeliveredIndexCandidatePopulationSource source = new();
        Sha256Fingerprint analyzerFingerprint = CandidateAnalysisIdentity.StructuralHash(
            [System.Text.Json.JsonSerializer.Serialize(source.Declaration)]);
        AnalysisExecutionInputContract executionInput = new(
            ContractConstants.AnalysisExecutionInputSchemaId, new ContractVersion(1, 0, 0), Id("execution-expansion-bound"),
            expansion.OriginatingRunId,
            Reference(expansion.SourceSnapshotId, new string('1', 64)), Reference(Id("bethesda-expansion-bound"), new string('2', 64)),
            [new(expansion.ExpansionId, CandidateDeliveredInputIdentity.Version, byteFingerprint, "retained")],
            [new(source.AnalyzerId, source.Declaration.AnalyzerVersion, analyzerFingerprint, "retained")],
            Reference(expansion.ConfigurationId, new string('3', 64)), Reference(Id("manifest-expansion-bound"), new string('4', 64)),
            ReplayMode.Clean, null, 0, new(100, 10_000, 100, 1_000, 120_000),
            [new("provider", BoundaryUseState.NotUsed, "local-only"),
             new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
             new("nexus", BoundaryUseState.NotUsed, "local-only"),
             new("loot", BoundaryUseState.NotUsed, "local-only")]);
        CandidatePopulationContext context = new(
            null, expansion.OriginatingRunId, expansion.SourceSnapshotId, expansion.AnalysisContextId,
            expansion.ConfigurationId, null, null, expansion, byteFingerprint,
            CandidateDeliveredInputExpander.Expand(expansion).PayloadId);
        CandidatePipelineRequest request = new(
            expansion.OriginatingRunId, Id("population-expansion-bound"), Id("policy-expansion-bound"),
            Id("threshold-expansion-bound"), new(Id("limit-expansion-bound"), 100, 100), context, [source], executionInput);

        CandidatePipelineResult result = CandidatePipeline.Execute(request);
        Assert.AreEqual(1L, result.Analysis.Counts.Population);
        Assert.AreEqual(context.AdmittedDeliveredInputId, result.Analysis.DeliveredInputId);
        Assert.IsTrue(result.Analysis.Decisions.All(item =>
            item.DependencyIds.Contains(result.Analysis.DeliveredInputId)));
        Slice5ContractInvariants.Validate(result.Analysis);
        OpaqueId substitutedRoot = Id("candidate-delivered-input-expansion-substitution");
        CandidateAnalysisContract substituted = result.Analysis with
        {
            Decisions = result.Analysis.Decisions.Select(item => item with
            {
                DependencyIds = item.DependencyIds.Select(id =>
                    id == result.Analysis.DeliveredInputId ? substitutedRoot : id).ToArray(),
            }).ToArray(),
            DependencyEdges = result.Analysis.DependencyEdges.Select(item =>
                item.ToKind == "dependency" && item.ToId == result.Analysis.DeliveredInputId
                    ? item with { ToId = substitutedRoot }
                    : item).ToArray(),
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Slice5ContractInvariants.Validate(substituted));
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidatePipeline.Execute(request with
        {
            Context = context with { DeliveredExpansionByteFingerprint = new Sha256Fingerprint(new string('f', 64)) },
        }));
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidatePipeline.Execute(request with
        {
            Context = context with { DeliveredExpansion = expansion with { SubjectCount = 2 } },
        }));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Fault")]
    public void CooperativeSourceCannotRunPastTheAdmittedDeadline()
    {
        OpaqueId run = Id("run-deadline");
        OpaqueId snapshot = Id("snapshot-deadline");
        OpaqueId configuration = Id("configuration-deadline");
        DeadlineCandidatePopulationSource source = new(Id("analyzer-deadline"));
        Sha256Fingerprint analyzerFingerprint = CandidateAnalysisIdentity.StructuralHash(
            [System.Text.Json.JsonSerializer.Serialize(source.Declaration)]);
        AnalysisExecutionInputContract executionInput = new(
            ContractConstants.AnalysisExecutionInputSchemaId, new ContractVersion(1, 0, 0), Id("execution-deadline"), run,
            Reference(snapshot, new string('1', 64)), Reference(Id("bethesda-deadline"), new string('2', 64)), [],
            [new(source.AnalyzerId, source.Declaration.AnalyzerVersion, analyzerFingerprint, "retained")],
            Reference(configuration, new string('3', 64)), Reference(Id("manifest-deadline"), new string('4', 64)),
            ReplayMode.Clean, null, 0, new(10, 100, 10, 20, 10),
            [new("provider", BoundaryUseState.NotUsed, "local-only"),
             new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
             new("nexus", BoundaryUseState.NotUsed, "local-only"),
             new("loot", BoundaryUseState.NotUsed, "local-only")]);
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Assert.ThrowsExactly<InvalidDataException>(() => CandidatePipeline.Execute(new(
            run, Id("population-deadline"), Id("policy-deadline"), Id("threshold-deadline"),
            new(Id("limit-deadline"), 10, 10),
            new CandidatePopulationContext(null, run, snapshot, Id("context-deadline"), configuration),
            [source], executionInput)));
        stopwatch.Stop();
        Assert.IsLessThan(1_000L, stopwatch.ElapsedMilliseconds);
    }

    private static ArtifactReferenceContract Reference(OpaqueId id, string fingerprint) =>
        new(id, new ContractVersion(1, 0, 0), new Sha256Fingerprint(fingerprint), "retained");

    private static OpaqueId Id(string value) => new(value);

    private sealed class DeadlineCandidatePopulationSource(OpaqueId analyzerId) : ICandidatePopulationSource
    {
        public OpaqueId AnalyzerId => analyzerId;
        public AnalyzerDeclarationContract Declaration { get; } = CandidateAnalyzerDeclarations.Create(analyzerId);

        public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
            CandidatePopulationContext context,
            CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.SpinWait(1_000);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
            CandidatePopulationContext context,
            CancellationToken cancellationToken = default) => [];
    }
}
