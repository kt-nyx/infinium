using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.ScopeReversion;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ControlledRealScopeReversionEvaluationTests
{
    private const string ManifestVariable = "INFINIUM_CONTROLLED_SCOPE_INPUT_MANIFEST";
    private const string OutputVariable = "INFINIUM_CONTROLLED_SCOPE_OUTPUT_ROOT";
    private static readonly string[] FixedCoverageIds =
    [
        "analyzer", "persistence", "projection", "purpose", "replay", "taxonomy",
    ];
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("ControlledReal")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void ExactMatchedCasesProduceBoundedPositiveAndResolvedControlResults()
    {
        string manifestPath = Environment.GetEnvironmentVariable(ManifestVariable)
            ?? throw new AssertInconclusiveException($"{ManifestVariable} is required for the controlled-real verification gate.");
        string outputRoot = Environment.GetEnvironmentVariable(OutputVariable)
            ?? throw new AssertInconclusiveException($"{OutputVariable} is required for the controlled-real verification gate.");

        ControlledRealInputAdmissionReceipt admission = ControlledRealInputAdmission.Validate(
            manifestPath,
            ControlledRealScopeReversionSupport.ExpectedInputs);
        ValidatePublicManifests(PublicManifests());
        string root = ReadDeclaredRoot(manifestPath);

        ScopeReversionV2PipelineResult actorPositive = ExecuteActor(root, admission, control: false);
        ScopeReversionV2PipelineResult actorControl = ExecuteActor(root, admission, control: true);
        ScopeReversionV2PipelineResult referencePositive = ExecuteReference(root, admission, control: false);
        ScopeReversionV2PipelineResult referenceControl = ExecuteReference(root, admission, control: true);

        AssertPositive(actorPositive.Analysis, ScopeReversionV2SubjectKind.ActorCohort, expectedMembers: 2);
        AssertResolvedControl(actorControl.Analysis, ScopeReversionV2SubjectKind.ActorCohort, expectedMembers: 2);
        AssertPositive(referencePositive.Analysis, ScopeReversionV2SubjectKind.PlacedReference, expectedMembers: 1);
        AssertResolvedControl(referenceControl.Analysis, ScopeReversionV2SubjectKind.PlacedReference, expectedMembers: 1);
        Assert.IsTrue(actorPositive.Analysis.Gaps.Any(item => item.Reason.Contains("AIDT", StringComparison.Ordinal)));
        Assert.IsTrue(actorControl.Analysis.Gaps.Any(item => item.Reason.Contains("AIDT", StringComparison.Ordinal)));
        Assert.IsTrue(referencePositive.Analysis.Gaps.Any(item => item.Reason.Contains("runtime", StringComparison.Ordinal)));

        Directory.CreateDirectory(outputRoot);
        PersistControlledResults(outputRoot,
            [actorPositive, actorControl, referencePositive, referenceControl]);
        string receiptPath = Path.Combine(outputRoot, "controlled-real-results.json");
        object receipt = new
        {
            schema = "infinium-m1-slice8-controlled-real-results/1",
            handoff_id = admission.HandoffId,
            input_manifest_sha256 = admission.ManifestFingerprint.Value,
            public_manifests = actorPositive.Analysis.PublicManifests,
            controlled_inputs = admission.Inputs.Select(item => new
            {
                item.CaseId,
                item.RelativePath,
                item.ByteLength,
                sha256 = item.Sha256.Value,
                role = item.Role.ToString(),
            }),
            partition = "controlled-real-development",
            cases = new[]
            {
                Result("REAL-NPC-0001-POS", actorPositive),
                Result("REAL-NPC-0001-CTRL", actorControl),
                Result("REAL-REFR-0001-POS", referencePositive),
                Result("REAL-REFR-0001-CTRL", referenceControl),
            },
            prohibited_boundaries = actorPositive.Analysis.Boundaries.Select(item => new
            {
                id = item.BoundaryId,
                state = item.State.ToString(),
            }),
            third_party_payload_bytes_written = false,
        };
        File.WriteAllBytes(receiptPath, JsonSerializer.SerializeToUtf8Bytes(receipt, ReceiptJsonOptions));
    }

    private static object Result(string caseId, ScopeReversionV2PipelineResult result) => new
    {
        case_id = caseId,
        payload_id = result.Analysis.PayloadId.Value,
        output_sha256 = Convert.ToHexStringLower(SHA256.HashData(result.CanonicalJson)),
        decisions = result.Analysis.Decisions.Count,
        hypotheses = result.Analysis.Hypotheses.Count,
        findings = result.Analysis.Findings.Count,
        cases = result.Analysis.Cases.Count,
        recommendations = result.Analysis.Recommendations.Count,
        gaps = result.Analysis.Gaps.Count,
    };

    private static void PersistControlledResults(
        string outputRoot,
        IReadOnlyList<ScopeReversionV2PipelineResult> results)
    {
        string productRoot = Path.Combine(outputRoot, "product-state");
        using AuthoritativeStore store = new(new StoragePaths(productRoot));
        foreach (ScopeReversionV2PipelineResult result in results)
        {
            string payloadId = result.Analysis.PayloadId.Value;
            ScopeReversionV2RetainedArtifact[] artifacts =
            [
                new(payloadId + "-structural", "bethesda-selected-structural-facts",
                    JsonSerializer.SerializeToUtf8Bytes(result.Analysis.Members, ContractJsonSerializer.Options)),
                new(payloadId + "-source-application", "source-application-facts",
                    JsonSerializer.SerializeToUtf8Bytes(result.Analysis.SourceDecisions, ContractJsonSerializer.Options)),
            ];
            ScopeReversionV2PersistenceReceipt receipt = store.PublishScopeReversionV2Analysis(
                new ScopeReversionV2PublicationRequest(
                    result.Analysis, result.CanonicalJson, artifacts, DateTimeOffset.UtcNow));
            Assert.AreEqual(payloadId, receipt.PayloadId);
            CollectionAssert.AreEqual(result.CanonicalJson, store.ReadScopeReversionV2AnalysisBytes(payloadId));
        }
    }

    private static void AssertPositive(
        ScopeReversionV2AnalysisContract analysis,
        ScopeReversionV2SubjectKind kind,
        int expectedMembers)
    {
        Assert.HasCount(1, analysis.Decisions);
        Assert.AreEqual(ScopeReversionDisposition.SupportedFinding, analysis.Decisions[0].Disposition);
        Assert.HasCount(expectedMembers, analysis.Members);
        Assert.HasCount(1, analysis.Findings);
        Assert.HasCount(1, analysis.Hypotheses);
        Assert.AreEqual(ScopeHypothesisState.Present, analysis.Hypotheses[0].State);
        Assert.AreEqual(FindingSeverity.Moderate, analysis.Findings[0].Severity);
        Assert.AreEqual(AnalysisConfidence.StronglySupported, analysis.Findings[0].Confidence);
        Assert.HasCount(1, analysis.Cases);
        Assert.HasCount(1, analysis.Recommendations);
        Assert.AreEqual(kind, analysis.Subjects[0].Kind);
        AssertCoveragePopulations(analysis, kind, "positive");
    }

    private static void AssertResolvedControl(
        ScopeReversionV2AnalysisContract analysis,
        ScopeReversionV2SubjectKind kind,
        int expectedMembers)
    {
        Assert.HasCount(1, analysis.Decisions);
        Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative, analysis.Decisions[0].Disposition);
        Assert.HasCount(expectedMembers, analysis.Members);
        Assert.IsEmpty(analysis.Findings);
        Assert.HasCount(1, analysis.Hypotheses);
        Assert.AreEqual(ScopeHypothesisState.ResolvedRejected, analysis.Hypotheses[0].State);
        Assert.IsEmpty(analysis.Cases);
        Assert.IsEmpty(analysis.Recommendations);
        Assert.AreEqual(kind, analysis.Subjects[0].Kind);
        AssertCoveragePopulations(analysis, kind, "control");
    }

    private static void AssertCoveragePopulations(
        ScopeReversionV2AnalysisContract analysis,
        ScopeReversionV2SubjectKind kind,
        string lane)
    {
        string domain = kind == ScopeReversionV2SubjectKind.ActorCohort ? "actor" : "reference";
        string[] actual = analysis.Coverage.Select(item => item.PopulationId).ToArray();
        Assert.HasCount(7, actual);
        CollectionAssert.Contains(actual, $"{domain}-{lane}");
        foreach (string required in FixedCoverageIds)
        {
            CollectionAssert.Contains(actual, required);
        }
        Assert.IsTrue(analysis.Coverage.All(item => item.Denominator > 0));
    }

    private static ScopeReversionV2PipelineResult ExecuteActor(
        string root,
        ControlledRealInputAdmissionReceipt admission,
        bool control)
    {
        string[] order =
        [
            "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm",
            "ccBGSSSE001-Fish.esm", "ccQDRSSE001-SurvivalMode.esl", "ccBGSSSE037-Curios.esl",
            "ccBGSSSE025-AdvDSGS.esm", "_ResourcePack.esl", "unofficial skyrim special edition patch.esp",
            "AI Overhaul.esp", "Children of the Pariah.esp",
        ];
        if (control)
        {
            order = [.. order, "CotP - AIO Patch.esp"];
        }
        BethesdaSemanticSnapshot snapshot = Extract(root, "REAL-NPC-0001", order);
        OpaqueId runId = new(control ? "slice8-real-npc-control" : "slice8-real-npc-positive");
        OpaqueId subjectId = new("real-npc-subject");
        OpaqueId causeId = new("real-npc-shared-package-cause");
        string winner = control ? "CotP - AIO Patch.esp" : "Children of the Pariah.esp";
        string[] formKeys = ["0001339A:Skyrim.esm", "0001AA63:Skyrim.esm"];
        ScopeReversionV2ProjectionMemberSpec[] members = formKeys.Select(formKey =>
        {
            BethesdaNpcFact prior = FindNpc(snapshot, formKey, "AI Overhaul.esp");
            BethesdaNpcFact winning = FindNpc(snapshot, formKey, winner);
            OpaqueId memberId = new("member-" + formKey[..8].ToLowerInvariant());
            return new ScopeReversionV2ProjectionMemberSpec(
                memberId, subjectId, ScopeReversionV2SubjectKind.ActorCohort, "packages",
                prior.Contribution.ContributionId, winning.Contribution.ContributionId, true,
                [new OpaqueId("source-npc-appearance"), new OpaqueId("source-npc-behavior")], causeId,
                [new OpaqueId("dependency-" + formKey[..8].ToLowerInvariant())],
                [ScopeReversionV2Contract.StableId("evidence", prior.Contribution.ContributionId),
                    ScopeReversionV2Contract.StableId("evidence", winning.Contribution.ContributionId)],
                ["AIDT difference retained outside the selected package relation"],
                ["archive and rendered appearance not validated", "runtime actor behavior not observed"]);
        }).OrderBy(item => item.MemberId.Value, StringComparer.Ordinal).ToArray();
        ScopeReversionV2SubjectContract subject = new(
            subjectId, ScopeReversionV2SubjectKind.ActorCohort,
            members.Select(item => item.MemberId).ToArray(), causeId,
            "two selected actor package relations", "selected actors may lose established scheduling behavior",
            "preserve the appearance winner while restoring the established package relation",
            "rerun the exact local structural analysis and inspect both package chains",
            ["AIDT remains outside the selected relation", "archive completeness is not established", "runtime actor behavior not observed"]);
        return ControlledRealScopeReversionProjector.Execute(new(
            runId, admission.HandoffId, admission.ManifestFingerprint, PublicManifests(),
            ControlledInputs(admission, "REAL-NPC-0001"), ScopeReversionV2PartitionRole.ControlledRealDevelopment,
            snapshot, [subject], members, ActorSources(runId, subjectId),
            Taxonomy(runId, subjectId, "actor"), [Transition(runId, admission.ManifestFingerprint,
                control ? "REAL-NPC-0001-CTRL" : "REAL-NPC-0001-POS") ]));
    }

    private static ScopeReversionV2PipelineResult ExecuteReference(
        string root,
        ControlledRealInputAdmissionReceipt admission,
        bool control)
    {
        string[] order =
        [
            "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm",
            "Candlehearth.esp", "Nightgate Inn Revived.esp",
        ];
        if (control)
        {
            order = [.. order, "Nightgate Inn Revived - Candlehearth.esp"];
        }
        BethesdaSemanticSnapshot snapshot = Extract(root, "REAL-REFR-0001", order);
        OpaqueId runId = new(control ? "slice8-real-refr-control" : "slice8-real-refr-positive");
        OpaqueId subjectId = new("real-refr-subject");
        OpaqueId causeId = new("real-refr-linked-reference-cause");
        BethesdaPlacedReferenceFact prior = FindReference(snapshot, "00017061:Skyrim.esm", "Candlehearth.esp");
        BethesdaPlacedReferenceFact winning = FindReference(snapshot, "00017061:Skyrim.esm",
            control ? "Nightgate Inn Revived - Candlehearth.esp" : "Nightgate Inn Revived.esp");
        ScopeReversionV2ProjectionMemberSpec member = new(
            new OpaqueId("member-00017061"), subjectId, ScopeReversionV2SubjectKind.PlacedReference,
            "linked-references", prior.Contribution.ContributionId, winning.Contribution.ContributionId, true,
            [new OpaqueId("source-refr-structure"), new OpaqueId("source-refr-visual")], causeId,
            [new OpaqueId("dependency-00017061")],
            [ScopeReversionV2Contract.StableId("evidence", prior.Contribution.ContributionId),
                ScopeReversionV2Contract.StableId("evidence", winning.Contribution.ContributionId)], [],
            ["navmesh and other references not inspected", "runtime rental behavior not observed"]);
        ScopeReversionV2SubjectContract subject = new(
            subjectId, ScopeReversionV2SubjectKind.PlacedReference, [member.MemberId], causeId,
            "one selected placed-reference link relation", "the moved bed may lose its established rental association",
            "preserve the placement winner while restoring the established linked-reference relation",
            "rerun exact local structural analysis and inspect the selected link and placement",
            ["quest and global state not inspected", "runtime rental behavior not observed"]);
        return ControlledRealScopeReversionProjector.Execute(new(
            runId, admission.HandoffId, admission.ManifestFingerprint, PublicManifests(),
            ControlledInputs(admission, "REAL-REFR-0001"), ScopeReversionV2PartitionRole.ControlledRealDevelopment,
            snapshot, [subject], [member], ReferenceSources(runId, subjectId),
            Taxonomy(runId, subjectId, "reference"), [Transition(runId, admission.ManifestFingerprint,
                control ? "REAL-REFR-0001-CTRL" : "REAL-REFR-0001-POS") ]));
    }

    private static BethesdaSemanticSnapshot Extract(string root, string caseId, IReadOnlyList<string> order)
    {
        IReadOnlyList<(string Name, int Order, string Path, OpaqueId Entity)> plugins = order.Select((name, index) =>
        {
            string subdirectory = name.EndsWith(".es", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".esm", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".esl", StringComparison.OrdinalIgnoreCase)
                ? "masters" : "plugins";
            string path = Path.Combine(root, "cases", caseId, subdirectory, name);
            if (!File.Exists(path))
            {
                path = Path.Combine(root, "cases", caseId, "plugins", name);
            }
            return (name, index, path, new OpaqueId($"{caseId.ToLowerInvariant()}-provider-{index:D3}"));
        }).ToArray();
        string[] selectedFormKeys = caseId == "REAL-NPC-0001"
            ? ["0001339A:Skyrim.esm", "0001AA63:Skyrim.esm"]
            : ["00017061:Skyrim.esm"];
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor(
            maximumInputBytes: 768L * 1024 * 1024,
            maximumDecompressedBytes: 64L * 1024 * 1024,
            selectedFormKeys).Extract(
            BethesdaSemanticTestSnapshot.Create(plugins));
        if (result.State is not (BethesdaExtractionState.Completed or BethesdaExtractionState.CompletedWithGaps)
            || result.Snapshot is null)
        {
            string detail = string.Join("; ", result.Failures.Select(item => $"{item.Code} ({item.Input}): {item.Message}"));
            throw new InvalidDataException($"Controlled Bethesda extraction failed for {caseId}: {detail}");
        }
        return result.Snapshot;
    }

    private static BethesdaNpcFact FindNpc(BethesdaSemanticSnapshot snapshot, string formKey, string plugin) =>
        snapshot.NpcContributions.Single(item => item.Contribution.Identity.FormKey == formKey
            && StringComparer.OrdinalIgnoreCase.Equals(item.Contribution.SourcePlugin, plugin));

    private static BethesdaPlacedReferenceFact FindReference(BethesdaSemanticSnapshot snapshot, string formKey, string plugin) =>
        snapshot.PlacedReferenceContributions.Single(item => item.Contribution.Identity.FormKey == formKey
            && StringComparer.OrdinalIgnoreCase.Equals(item.Contribution.SourcePlugin, plugin));

    private static ScopeReversionV2SourceDecisionContract Source(
        OpaqueId runId,
        OpaqueId subjectId,
        string decisionId,
        string sourceRegistryId,
        string revision,
        string publicManifestPath,
        string passage) => new(
        new OpaqueId(decisionId), runId, sourceRegistryId, revision, decisionId + "-passage",
        new Sha256Fingerprint(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(passage)))),
        publicManifestPath,
        new UtcTimestamp(new DateTimeOffset(2026, 7, 28, 0, 0, 0, TimeSpan.Zero)),
        SemanticProposalState.Proposed, SemanticSupportState.Supported, SemanticApplicabilityState.Applicable,
        SemanticDecisionState.Admitted, ContractJsonSerializer.Fingerprint(decisionId + "-local-facts"),
        [subjectId], ["declared-purpose", "selected-relation"], [new OpaqueId(decisionId + "-evidence")],
        "Accepted source support and exact local applicability are independently admitted for this bounded relation.");

    private static ScopeReversionV2SourceDecisionContract[] ActorSources(OpaqueId runId, OpaqueId subjectId) =>
    [
        Source(runId, subjectId, "source-npc-appearance", "SRC-NEXUS-97981",
            "1.2.5-2026-03-30", "docs/research/investigations/artifacts/RESEARCH-0035/eval-0016-independent-byte-map.json",
            "Not strictly compatible with mods that edit the same NPC records (appearance, skills, inventories, etc.)"),
        Source(runId, subjectId, "source-npc-behavior", "SRC-NEXUS-21654",
            "1.9.5-2026-06-19", "docs/research/investigations/artifacts/RESEARCH-0035/eval-0016-independent-byte-map.json",
            "Highly noticeable tweaks & rewrites many of the vanilla NPCs AI"),
    ];

    private static ScopeReversionV2SourceDecisionContract[] ReferenceSources(OpaqueId runId, OpaqueId subjectId) =>
    [
        Source(runId, subjectId, "source-refr-structure", "SRC-NEXUS-97542",
            "1.1.1-2023-08-23", "docs/research/investigations/artifacts/RESEARCH-0035/eval-0017-independent-byte-map.json",
            "Candlehearth is an inn overhaul that adds extended inn rentals and safe storage to every inn in Skyrim."),
        Source(runId, subjectId, "source-refr-visual", "SRC-NEXUS-121244",
            "1.6-2025-06-14", "docs/research/investigations/artifacts/RESEARCH-0035/eval-0017-independent-byte-map.json",
            "This mod is purely a visual overhaul, it does not edit NPCs or quests. A patch will be needed to resolve conflicts."),
    ];

    private static ScopeReversionV2TaxonomyReferenceContract[] Taxonomy(OpaqueId runId, OpaqueId subjectId, string domain)
    {
        (string Axis, string Facet, string Code, ClassificationRole Role)[] assignments = domain == "actor"
            ?
            [
                ("declared-purpose", "purpose", "purpose.replace-overhaul", ClassificationRole.Declared),
                ("declared-purpose-target", "actors", "purpose-target.actors.appearance-identity", ClassificationRole.Declared),
                ("declared-prior-purpose", "purpose", "purpose.modify-tune", ClassificationRole.Declared),
                ("declared-prior-target", "actors", "purpose-target.actors.ai-packages", ClassificationRole.Declared),
                ("observed-surface", "plugin", "surface.plugin-data", ClassificationRole.Observed),
                ("observed-surface", "asset", "surface.asset", ClassificationRole.Observed),
                ("observed-delivery", "plugin", "delivery.plugin-container", ClassificationRole.Observed),
                ("observed-delivery", "loose", "delivery.loose-data-file", ClassificationRole.Observed),
                ("established-area", "actors", "area.actors.appearance-identity", ClassificationRole.Observed),
                ("established-area", "actors", "area.actors.ai-packages", ClassificationRole.Observed),
                ("predicted-consequence", "behavior", "consequence.incorrect-functional-behavior", ClassificationRole.Predicted),
                ("established-extent", "subject", "extent.subject.bounded-set", ClassificationRole.Observed),
                ("established-extent", "spatial", "extent.spatial.nonspatial", ClassificationRole.Observed),
                ("established-extent", "persistence", "extent.persistence.installation-persistent", ClassificationRole.Observed),
                ("established-extent", "propagation", "extent.propagation.bounded-dependents", ClassificationRole.Observed),
            ]
            :
            [
                ("declared-purpose", "purpose", "purpose.modify-tune", ClassificationRole.Declared),
                ("declared-purpose-target", "world", "purpose-target.world", ClassificationRole.Declared),
                ("declared-later-purpose", "purpose", "purpose.replace-overhaul", ClassificationRole.Declared),
                ("declared-later-target", "presentation", "purpose-target.presentation.visual", ClassificationRole.Declared),
                ("observed-surface", "plugin", "surface.plugin-data", ClassificationRole.Observed),
                ("observed-delivery", "plugin", "delivery.plugin-container", ClassificationRole.Observed),
                ("established-area", "world", "area.world.cells-worldspaces-locations", ClassificationRole.Observed),
                ("established-area", "activation", "area.world.placed-objects-activation", ClassificationRole.Observed),
                ("predicted-consequence", "behavior", "consequence.incorrect-functional-behavior", ClassificationRole.Predicted),
                ("predicted-area", "economy", "area.gameplay.items-inventory-economy", ClassificationRole.Predicted),
                ("predicted-extent", "subject", "extent.subject.single-instance", ClassificationRole.Predicted),
                ("predicted-extent", "spatial", "extent.spatial.cell-or-location", ClassificationRole.Predicted),
                ("predicted-extent", "persistence", "extent.persistence.installation-persistent", ClassificationRole.Predicted),
                ("predicted-extent", "propagation", "extent.propagation.bounded-dependents", ClassificationRole.Predicted),
            ];
        return assignments.Select(item => new ScopeReversionV2TaxonomyReferenceContract(
                ScopeReversionV2Contract.StableId("taxonomy", subjectId.Value, item.Axis, item.Code), runId,
                ScopeReversionV2Contract.TaxonomyId, ScopeReversionV2Contract.TaxonomyVersion, subjectId,
                item.Axis, item.Facet, item.Code, TaxonomyApplicability.Assigned, item.Role,
                [new OpaqueId("taxonomy-" + domain + "-evidence")], "Accepted upstream typed taxonomy reference."))
            .OrderBy(item => item.AssignmentId.Value, StringComparer.Ordinal).ToArray();
    }

    private static ScopeReversionV2PartitionTransitionContract Transition(
        OpaqueId runId,
        Sha256Fingerprint manifestFingerprint,
        string caseId) => new(
            ScopeReversionV2Contract.StableId("partition-transition", runId.Value, caseId), caseId,
            manifestFingerprint, "working-candidate-aa47deba", ScopeReversionV2PartitionRole.ControlledRealValidation,
            ScopeReversionV2PartitionRole.ControlledRealDevelopment,
            "The first controlled-real execution exposed selected-input capacity and record-population defects; corrected evidence is development data.",
            new UtcTimestamp(new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero)));

    private static ScopeReversionV2PublicManifestReferenceContract[] PublicManifests() =>
    [
        new("docs/research/investigations/artifacts/RESEARCH-0035/eval-0016-independent-byte-map.json", 45038,
            new Sha256Fingerprint("e5a1ff7cbe1ff1db84331769b426df333cd442c1ff5b522c7959e08a09a16130")),
        new("docs/research/investigations/artifacts/RESEARCH-0035/eval-0017-independent-byte-map.json", 10504,
            new Sha256Fingerprint("9dee14a525fa4aac751c946a87ba2a567f03d0e362dd4b68386f79b69b7b5cb9")),
        new("docs/research/investigations/artifacts/RESEARCH-0035/gate-c-case-manifests.json", 10699,
            new Sha256Fingerprint("2ab135d50adb533e533918de2b5c42f3642348c3234432d6750f073ba68e4d15")),
    ];

    private static ScopeReversionV2ControlledInputReferenceContract[] ControlledInputs(
        ControlledRealInputAdmissionReceipt admission,
        string caseId) => admission.Inputs
        .Where(item => item.CaseId == caseId)
        .Select(item => new ScopeReversionV2ControlledInputReferenceContract(
            item.RelativePath,
            item.Role switch
            {
                ControlledRealInputRole.OfficialMaster => "official-master",
                ControlledRealInputRole.PositivePluginOrAsset => "positive-plugin-or-asset",
                ControlledRealInputRole.MatchedPatchControl => "matched-patch-control",
                ControlledRealInputRole.RequiredExtractionDependency => "required-extraction-dependency",
                _ => throw new InvalidDataException("The admitted controlled input role is unsupported."),
            },
            item.ByteLength,
            item.Sha256))
        .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
        .ToArray();

    private static void ValidatePublicManifests(
        IReadOnlyList<ScopeReversionV2PublicManifestReferenceContract> manifests)
    {
        string repositoryRoot = ScopeReversionTestSupport.RepositoryRoot();
        foreach (ScopeReversionV2PublicManifestReferenceContract manifest in manifests)
        {
            string path = Path.Combine(repositoryRoot, manifest.RepositoryPath.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual(manifest.ByteLength, bytes.LongLength, manifest.RepositoryPath);
            Assert.AreEqual(manifest.Sha256.Value,
                Convert.ToHexStringLower(SHA256.HashData(bytes)), manifest.RepositoryPath);
        }
    }

    private static string ReadDeclaredRoot(string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        return Path.GetFullPath(document.RootElement.GetProperty("root").GetString()!);
    }
}

internal static class ControlledRealScopeReversionSupport
{
    internal static readonly ControlledRealExpectedInput[] ExpectedInputs =
    [
        new("REAL-NPC-0001", "0001339a.dds", 5592580, Hash("217a41fe5e2aba2c71e8778c46160908c516fabc51699ebf8ceeb7971388f1b2"), ControlledRealInputRole.PositivePluginOrAsset),
        new("REAL-NPC-0001", "0001339a.nif", 3410436, Hash("06a16385f2702a23eab8eab041aa098be41b0da1db38a34290d54d30d6e3764c"), ControlledRealInputRole.PositivePluginOrAsset),
        new("REAL-NPC-0001", "0001aa63.dds", 5592580, Hash("8207efdec8532b76819d1ed8d1f0c8b17beccd60183c481ed86417a19a0d88fa"), ControlledRealInputRole.PositivePluginOrAsset),
        new("REAL-NPC-0001", "0001aa63.nif", 5266559, Hash("7145bb6562ff7737a15baf92cd9ed8025a7f2ef9e7b6ea26054de87663a5846e"), ControlledRealInputRole.PositivePluginOrAsset),
        new("REAL-NPC-0001", "Dawnguard.esm", 24813534, Hash("1208e5153e35366e0ada1a887720d6d636e2d8592d007fe142b37a57e46b476e"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "Dragonborn.esm", 64259475, Hash("3b8bf5ead27337f829fa4d474f0363324124a9696d33fe1aee7b01262eff5bd1"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "HearthFires.esm", 3681749, Hash("70e0d5d6dc42224349d33e8c7bca73da447463f671cacc9c15fc0273c93e0008"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "Skyrim.esm", 249753412, Hash("2bbc77fdec35a70ef96b710f8c525e50a1db9e63e11a391a0eb9ee8f56d36107"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "Update.esm", 18429562, Hash("5f2985b205ea57428164b47e1a5df57f9b5a1ac0399d4c8b5cf30fc0a60fb008"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "_ResourcePack.esl", 78418, Hash("d231915a4bbfe6e89536dfb0a46c5adc8e4d2d23ce95d248dc24943776eb76fb"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "ccBGSSSE001-Fish.esm", 1203374, Hash("f30a9c18c3e375e002cc26e5dd3cdf72a615d574738581fba2bfd58215024fe7"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "ccBGSSSE025-AdvDSGS.esm", 614669, Hash("68d8ddcbabd6ef491b175838b25d6e2870df3d2faffdb535491740269e9335dc"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "ccBGSSSE037-Curios.esl", 37476, Hash("f5a970ada5cf32f3088f01bccdad0a6be69a5b43e5e325ad4cbd7c6d1a15f4d3"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "ccQDRSSE001-SurvivalMode.esl", 237674, Hash("109246cd704ce7765bea99ed2b9b1800eeb069d46c7a308b8ba5db2def4af77b"), ControlledRealInputRole.OfficialMaster),
        new("REAL-NPC-0001", "AI Overhaul.esp", 2183246, Hash("fed6f25ffa2dac3a7a578add18b0fd763e6c48d33732000480bad9069bee55d2"), ControlledRealInputRole.PositivePluginOrAsset),
        new("REAL-NPC-0001", "Children of the Pariah.esp", 88141, Hash("c60db49682ca14b4651a75c25b7690c8c833ef010412481e7f60c904318434ff"), ControlledRealInputRole.PositivePluginOrAsset),
        new("REAL-NPC-0001", "CotP - AIO Patch.esp", 30319, Hash("16e49fc6337094b88ea60aa2241fe888d194407859bc5405089f785a83485416"), ControlledRealInputRole.MatchedPatchControl),
        new("REAL-NPC-0001", "unofficial skyrim special edition patch.esp", 19589391, Hash("c33f42e503e1c3908bfb0f241778d5d7a5114599a07b1b6e0773f0828c6e1876"), ControlledRealInputRole.RequiredExtractionDependency),
        new("REAL-REFR-0001", "Dawnguard.esm", 24813534, Hash("1208e5153e35366e0ada1a887720d6d636e2d8592d007fe142b37a57e46b476e"), ControlledRealInputRole.OfficialMaster),
        new("REAL-REFR-0001", "Dragonborn.esm", 64259475, Hash("3b8bf5ead27337f829fa4d474f0363324124a9696d33fe1aee7b01262eff5bd1"), ControlledRealInputRole.OfficialMaster),
        new("REAL-REFR-0001", "HearthFires.esm", 3681749, Hash("70e0d5d6dc42224349d33e8c7bca73da447463f671cacc9c15fc0273c93e0008"), ControlledRealInputRole.OfficialMaster),
        new("REAL-REFR-0001", "Skyrim.esm", 249753412, Hash("2bbc77fdec35a70ef96b710f8c525e50a1db9e63e11a391a0eb9ee8f56d36107"), ControlledRealInputRole.OfficialMaster),
        new("REAL-REFR-0001", "Update.esm", 18429562, Hash("5f2985b205ea57428164b47e1a5df57f9b5a1ac0399d4c8b5cf30fc0a60fb008"), ControlledRealInputRole.OfficialMaster),
        new("REAL-REFR-0001", "Candlehearth.esp", 34707, Hash("18a8275ca579701aba8f525f839c27bf19549f7ee78265e3855a2bb602a5bf04"), ControlledRealInputRole.PositivePluginOrAsset),
        new("REAL-REFR-0001", "Nightgate Inn Revived - Candlehearth.esp", 838, Hash("03811e47ba8614707dc191d9c39c73b6a3a1d4a4dfac8baa8037798575397fbd"), ControlledRealInputRole.MatchedPatchControl),
        new("REAL-REFR-0001", "Nightgate Inn Revived.esp", 268904, Hash("9a34c0e33ed14d9cdbf61c3641c03c4e6c048c8495523c4aeaeabfa1acd66318"), ControlledRealInputRole.PositivePluginOrAsset),
    ];

    private static Sha256Fingerprint Hash(string value) => new(value);
}
