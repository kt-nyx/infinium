using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Bethesda;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class TargetedVerificationProductionCorrelationIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void ProductionCorrelationClassifiesUnchangedRemovedAndIdentityChangedMembers()
    {
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV");
        BethesdaSemanticExtractionResult extracted = new BethesdaSemanticExtractor().Extract(request);
        BethesdaSemanticSnapshot sourceSnapshot = extracted.Snapshot!;
        CandidateDeliveredInputContract source = CandidateDeliveredInputAdapter.Create(
            new("targeted-production-source"), sourceSnapshot.SourceSnapshotId, new("context"),
            new("configuration"), sourceSnapshot, null);
        BethesdaOverrideChain sourceChain = sourceSnapshot.OverrideChains.Values.First();
        OpaqueId recordIdentity = CandidateAnalysisIdentity.StableId(
            "candidate-delivered-source", "record", sourceChain.Identity.ParticipantId);
        TargetedScopeMemberContract recordMember = new(new("production-record-member"),
            TargetedScopeMemberKind.Record, recordIdentity, "typed source record", true,
            [new("record-proof")]);
        OpaqueId proof = new("qualified-production-publication");

        TargetedCurrentObservationContract unchanged = TargetedVerificationExecutor.CorrelateCurrentMember(
            recordMember, source, source, sourceSnapshot, extracted, request.AcceptedSnapshot, proof);
        Assert.AreEqual(TargetedCorrelationStatus.MatchedExecutable, unchanged.Status);

        Dictionary<string, BethesdaOverrideChain> remainingChains = sourceSnapshot.OverrideChains
            .Where(item => !string.Equals(item.Key, sourceChain.Identity.FormKey,
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BethesdaRecordContribution> remainingWinners = sourceSnapshot.Winners
            .Where(item => !string.Equals(item.Key, sourceChain.Identity.FormKey,
                StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BethesdaResolvedParticipant> remainingParticipants = sourceSnapshot.ResolvedParticipants
            .Where(item => item.Value.ParticipantId != sourceChain.Identity.ParticipantId)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        BethesdaSemanticSnapshot removedSnapshot = sourceSnapshot with
        {
            OverrideChains = remainingChains,
            Winners = remainingWinners,
            ResolvedParticipants = remainingParticipants,
            NpcContributions = sourceSnapshot.NpcContributions.Where(item =>
                item.Contribution.Identity.ParticipantId != sourceChain.Identity.ParticipantId).ToArray(),
            RaceContributions = sourceSnapshot.RaceContributions.Where(item =>
                item.Contribution.Identity.ParticipantId != sourceChain.Identity.ParticipantId).ToArray(),
            PlacedReferenceContributions = sourceSnapshot.PlacedReferenceContributions.Where(item =>
                item.Contribution.Identity.ParticipantId != sourceChain.Identity.ParticipantId).ToArray(),
            Npcs = sourceSnapshot.Npcs.Where(item => !string.Equals(item.Key, sourceChain.Identity.FormKey,
                StringComparison.OrdinalIgnoreCase)).ToDictionary(item => item.Key, item => item.Value,
                StringComparer.OrdinalIgnoreCase),
            Races = sourceSnapshot.Races.Where(item => !string.Equals(item.Key, sourceChain.Identity.FormKey,
                StringComparison.OrdinalIgnoreCase)).ToDictionary(item => item.Key, item => item.Value,
                StringComparer.OrdinalIgnoreCase),
            PlacedReferences = sourceSnapshot.PlacedReferences.Where(item => !string.Equals(item.Key,
                sourceChain.Identity.FormKey, StringComparison.OrdinalIgnoreCase)).ToDictionary(
                item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
            FaceGen = sourceSnapshot.FaceGen.Where(item =>
                item.NpcParticipantId != sourceChain.Identity.ParticipantId).ToArray(),
            Links = sourceSnapshot.Links.Where(item =>
                item.SourceParticipantId != sourceChain.Identity.ParticipantId).ToArray(),
        };
        CandidateDeliveredInputContract removed = CandidateDeliveredInputAdapter.Create(
            new("targeted-production-removed"), removedSnapshot.SourceSnapshotId, new("context"),
            new("configuration"), removedSnapshot, null);
        TargetedCurrentObservationContract absent = TargetedVerificationExecutor.CorrelateCurrentMember(
            recordMember, source, removed, removedSnapshot, extracted with { Snapshot = removedSnapshot },
            request.AcceptedSnapshot, proof);
        Assert.AreEqual(TargetedCorrelationStatus.ProvenAbsent, absent.Status);
        Assert.IsNull(absent.CurrentExecutionMemberId);
        StringAssert.Contains(absent.Reason, "proves absence only");

        BethesdaFaceGenFact faceGen = sourceSnapshot.FaceGen[0];
        BethesdaFaceGenFact sourceProviderFact = faceGen with
        {
            Mesh = faceGen.Mesh with
            {
                Availability = BethesdaAssetAvailability.Present,
                Present = true,
                ExactAbsenceKnown = false,
                WinnerParticipantId = "provider-source",
                ProviderParticipantIds = ["provider-source"],
            },
        };
        BethesdaSemanticSnapshot providerSourceSnapshot = sourceSnapshot with { FaceGen = [sourceProviderFact] };
        CandidateDeliveredInputContract providerSource = CandidateDeliveredInputAdapter.Create(
            new("provider-source-run"), providerSourceSnapshot.SourceSnapshotId, new("context"),
            new("configuration"), providerSourceSnapshot, null);
        OpaqueId providerIdentity = CandidateAnalysisIdentity.StableId(
            "candidate-delivered-source", "provider", "provider-source");
        TargetedScopeMemberContract providerMember = new(new("production-provider-member"),
            TargetedScopeMemberKind.Provider, providerIdentity, "typed source provider", true,
            [new("provider-proof")]);
        BethesdaFaceGenFact renamedProviderFact = sourceProviderFact with
        {
            Mesh = sourceProviderFact.Mesh with
            {
                WinnerParticipantId = "provider-renamed",
                ProviderParticipantIds = ["provider-renamed"],
            },
        };
        BethesdaSemanticSnapshot renamedProviderSnapshot = sourceSnapshot with { FaceGen = [renamedProviderFact] };
        CandidateDeliveredInputContract renamedProvider = CandidateDeliveredInputAdapter.Create(
            new("provider-renamed-run"), renamedProviderSnapshot.SourceSnapshotId, new("context"),
            new("configuration"), renamedProviderSnapshot, null);
        TargetedCurrentObservationContract ambiguous = TargetedVerificationExecutor.CorrelateCurrentMember(
            providerMember, providerSource, renamedProvider, renamedProviderSnapshot,
            extracted with { Snapshot = renamedProviderSnapshot }, request.AcceptedSnapshot, proof);
        Assert.AreEqual(TargetedCorrelationStatus.Ambiguous, ambiguous.Status);
        Assert.IsFalse(ambiguous.CorrelationQualified);

        BethesdaFaceGenFact removedProviderFact = sourceProviderFact with
        {
            Mesh = sourceProviderFact.Mesh with
            {
                Availability = BethesdaAssetAvailability.Absent,
                Present = false,
                ExactAbsenceKnown = true,
                WinnerParticipantId = null,
                ProviderParticipantIds = [],
            },
        };
        BethesdaSemanticSnapshot removedProviderSnapshot = sourceSnapshot with { FaceGen = [removedProviderFact] };
        CandidateDeliveredInputContract removedProvider = CandidateDeliveredInputAdapter.Create(
            new("provider-removed-run"), removedProviderSnapshot.SourceSnapshotId, new("context"),
            new("configuration"), removedProviderSnapshot, null);
        TargetedCurrentObservationContract providerAbsent = TargetedVerificationExecutor.CorrelateCurrentMember(
            providerMember, providerSource, removedProvider, removedProviderSnapshot,
            extracted with { Snapshot = removedProviderSnapshot }, request.AcceptedSnapshot, proof);
        Assert.AreEqual(TargetedCorrelationStatus.ProvenAbsent, providerAbsent.Status);
        Assert.IsNull(providerAbsent.CurrentExecutionMemberId);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void QualifiedProcessingEvidenceProducesReachableLimitedPlanStates()
    {
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV");
        BethesdaSemanticExtractionResult extracted = new BethesdaSemanticExtractor().Extract(request);
        Assert.IsNotNull(extracted.Snapshot);
        BethesdaSemanticSnapshot snapshot = extracted.Snapshot;
        CandidateDeliveredInputContract delivered = CandidateDeliveredInputAdapter.Create(
            new("targeted-production-correlation"), snapshot.SourceSnapshotId, new("context"),
            new("configuration"), snapshot, null);
        CandidateDeliveredFaceGenFactContract faceGen = delivered.FaceGenFacts.First(fact =>
            fact.MeshAvailability == CandidateDeliveredAssetAvailability.Unknown
            || fact.TintAvailability == CandidateDeliveredAssetAvailability.Unknown);
        OpaqueId asset = faceGen.MeshAvailability == CandidateDeliveredAssetAvailability.Unknown
            ? faceGen.MeshAssetId : faceGen.TintAssetId;
        TargetedScopeMemberContract member = new(new("targeted-limited-asset"),
            TargetedScopeMemberKind.Asset, asset, "qualified production asset", true,
            [new("source-proof")]);
        OpaqueId publication = new("qualified-acquisition-publication");

        TargetedCurrentObservationContract unsupported = TargetedVerificationExecutor.CorrelateCurrentMember(
            member, delivered, delivered, snapshot, extracted, request.AcceptedSnapshot, publication);
        Assert.AreEqual(TargetedCorrelationStatus.Unsupported, unsupported.Status);
        Assert.IsTrue(unsupported.CorrelationQualified);
        Assert.IsFalse(unsupported.ProcessingQualified);

        SnapshotGap inaccessibleGap = new("reparse-point-unsupported", "filesystem",
            "A qualified capture object could not be traversed.");
        Mo2SnapshotCaptureResult inaccessibleCapture = request.AcceptedSnapshot with
        {
            State = SnapshotCaptureState.CompletedWithGaps,
            Gaps = [.. request.AcceptedSnapshot.Gaps, inaccessibleGap],
            Snapshot = request.AcceptedSnapshot.Snapshot! with
            {
                Gaps = [.. request.AcceptedSnapshot.Snapshot!.Gaps, inaccessibleGap],
            },
        };
        TargetedCurrentObservationContract inaccessible = TargetedVerificationExecutor.CorrelateCurrentMember(
            member, delivered, delivered, snapshot, extracted, inaccessibleCapture, publication);
        Assert.AreEqual(TargetedCorrelationStatus.Inaccessible, inaccessible.Status);
        Assert.IsTrue(inaccessible.CorrelationQualified);
        Assert.IsFalse(inaccessible.ProcessingQualified);

        BethesdaOverrideChain record = snapshot.OverrideChains.Values.First(chain =>
            delivered.FaceGenFacts.Any(fact => fact.NpcParticipantId == CandidateAnalysisIdentity.StableId(
                "candidate-delivered-source", "record", chain.Identity.ParticipantId)));
        OpaqueId recordIdentity = CandidateAnalysisIdentity.StableId(
            "candidate-delivered-source", "record", record.Identity.ParticipantId);
        TargetedScopeMemberContract malformedMember = new(new("targeted-limited-record"),
            TargetedScopeMemberKind.Record, recordIdentity, "qualified production record", true,
            [new("record-source-proof")]);
        BethesdaCoverageGap malformedGap = new("qualified-malformed-shape",
            BethesdaCoverageGapCategory.UnsupportedShape,
            record.Identity.Signature.ToLowerInvariant() + ":shape", "npc-records", 1,
            "The qualified record shape is outside the accepted content adapter.",
            "allowlisted-record-shape-semantics");
        BethesdaSemanticSnapshot malformedSnapshot = snapshot with
        {
            Gaps = [.. snapshot.Gaps, malformedGap],
        };
        BethesdaSemanticExtractionResult malformedExtraction = extracted with
        {
            State = BethesdaExtractionState.CompletedWithGaps,
            Snapshot = malformedSnapshot,
            Gaps = malformedSnapshot.Gaps,
        };
        TargetedCurrentObservationContract malformed = TargetedVerificationExecutor.CorrelateCurrentMember(
            malformedMember, delivered, delivered, malformedSnapshot, malformedExtraction,
            request.AcceptedSnapshot, publication);
        Assert.AreEqual(TargetedCorrelationStatus.Malformed, malformed.Status);
        Assert.IsTrue(malformed.CorrelationQualified);
        Assert.IsFalse(malformed.ProcessingQualified);

        TargetedAnalysisScopeContract scope = TargetedVerificationPlanner.CloseScope(
            new("limited-production-preparation"), new("source-occurrence"),
            [member, malformedMember], [member, malformedMember], []);
        TargetedCorrelationCoverageContract coverage = TargetedVerificationPlanner.Correlate(
            new("limited-production-preparation"), scope, snapshot.SourceSnapshotId,
            new("acquisition"), new("semantic-output"), [unsupported, malformed]);
        Assert.IsTrue(coverage.Startable);
        Assert.IsTrue(coverage.Limited);
        Assert.AreEqual(2L, coverage.PopulationDenominator);
        Assert.IsTrue(coverage.Rows.All(row => row.DenominatorEffect == "retained-gap"));
    }
}
