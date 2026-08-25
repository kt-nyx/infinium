using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.FindingCases;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class AnalysisPipelineCorpusIntegrationTests
{
    private static readonly string[] FixturePath = ["fixtures", "public", "analysis-pipeline", "end-to-end-corpus"];

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Evaluation")]
    public void FrozenAnalysisPipelineCorpusExecutesDocumentationThroughOperationalBeforeOracleComparison()
    {
        string fixtureRoot = Path.Combine([TestRepository.Root, .. FixturePath]);
        using JsonDocument ordinary = Parse(Path.Combine(fixtureRoot, "ordinary-product-inputs.v1.json"));
        AssertOrdinaryProductInputIsAnswerFree(ordinary.RootElement);

        JsonElement shared = ordinary.RootElement.GetProperty("shared_facts");
        JsonElement cleanRequest = ordinary.RootElement.GetProperty("requests").EnumerateArray()
            .Single(item => item.GetProperty("mode").GetString() == "clean");
        JsonElement revision = shared.GetProperty("documentation_revisions").EnumerateArray()
            .Single(item => item.GetProperty("revision_key").GetString()
                == cleanRequest.GetProperty("revision_key").GetString());

        string root = Path.Combine(Path.GetTempPath(), $"infinium-analysis_pipeline-corpus-{Guid.NewGuid():N}");
        StoragePaths? paths = null;
        AuthoritativeStore? store = null;
        try
        {
            paths = new StoragePaths(root);
            store = new AuthoritativeStore(paths);
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "analysis_pipeline-comprehensive-corpus", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
            RunBinding binding = new("snapshot.001", "context.001", "configuration.001", "manifest.001");
            const string runId = "run-analysis_pipeline-comprehensive-clean";
            RunRecord queued = store.CreateRun("command-analysis_pipeline-comprehensive-clean", runId, binding,
                authority.FencingEpoch, DateTimeOffset.UtcNow);
            _ = store.Transition("transition-analysis_pipeline-comprehensive-clean", runId, queued.Generation,
                LifecycleState.Running, authority.FencingEpoch, "execute frozen analysis pipeline corpus", DateTimeOffset.UtcNow);
            AttemptRecord attempt = store.CreateAttempt(runId, authority.FencingEpoch,
                TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow);

            DocumentationEvidencePhaseResult documentation = DocumentationEvidencePhase.Execute(
                store, DocumentationRequest(runId, binding, revision, shared));
            CandidatePipelineRequest candidateRequest = CandidateRequest(runId, binding, shared, documentation.Evidence);
            CandidateAnalysisPhaseResult candidates = CandidateAnalysisPhase.Execute(
                store, candidateRequest, attempt, binding, DateTimeOffset.UtcNow);
            FindingCaseInputContract findingInput = FindingInput(candidates.Pipeline.Analysis, shared);
            FindingCaseAnalysisPhaseResult findings = FindingCaseAnalysisPhase.Execute(
                store, findingInput, attempt, binding, DateTimeOffset.UtcNow);

            string validationReceipt = StageValidationReceipt(store, paths, attempt);
            SemanticAnalysisContextContract analysisContext = AnalysisContext(binding.AnalysisContextId);
            AnalysisV1WorkAssignment assignment = new(
                AnalysisV1WorkAssignment.CurrentSchemaVersion,
                "assignment-analysis_pipeline-comprehensive-clean",
                candidateRequest.ExecutionInput!,
                analysisContext,
                Seal(store, documentation.Receipt.PayloadId, documentation.Evidence.SchemaId,
                    documentation.Evidence.SchemaVersion.ToString()),
                Seal(store, candidates.Receipt.PayloadId, candidates.Pipeline.Analysis.SchemaId,
                    candidates.Pipeline.Analysis.SchemaVersion.ToString()),
                Seal(store, findings.Receipt.StoredPayloadId, findings.Analysis.SchemaId,
                    findings.Analysis.SchemaVersion.ToString()),
                new string('a', 40),
                DateTimeOffset.UtcNow.AddSeconds(-1),
                AnalysisTerminalOutcome.Completed,
                "analysis pipeline corpus comprehensive clean corpus completed",
                AnalysisV1WorkAssignment.AbsoluteMaximumInputBytes,
                AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes,
                AnalysisV1WorkAssignment.AbsoluteMaximumQueryItems);
            AnalysisExecutionPhaseResult publication = AnalysisExecutionPhase.Execute(
                store, assignment, attempt, binding, validationReceipt, DateTimeOffset.UtcNow);

            RunOutputContract storedOutput = RunOutputJsonCodec.Deserialize(store.ReadAnalysisRunOutput(runId));
            string human = AnalysisOutputRenderer.Render(publication.Bundle.RunOutput, publication.Bundle.CliSummary);
            ProductObservation observation = Observe(documentation.Evidence, candidates.Pipeline.Analysis,
                findings.Analysis, publication, storedOutput, human, store.GetRun(runId));

            // The independently authored oracle is deliberately unavailable until all product output is observed.
            using JsonDocument expected = Parse(Path.Combine(fixtureRoot, "expected-results.v1.json"));
            JsonElement oracle = expected.RootElement.GetProperty("cases").EnumerateArray()
                .Single(item => item.GetProperty("case_id").GetString() == "ANALYSIS-PIPELINE-CLEAN-D01")
                .GetProperty("expected");
            AssertObservation(oracle, observation);
        }
        finally
        {
            store?.Dispose();
            paths?.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static DocumentationImportRequestContract DocumentationRequest(
        string runId, RunBinding binding, JsonElement revision, JsonElement shared)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(revision.GetProperty("text").GetString()!);
        DocumentationClaimInputContract[] claims = revision.GetProperty("claims").EnumerateArray()
            .Select(claim => new DocumentationClaimInputContract(
                Id(claim.GetProperty("claim_key").GetString()!),
                claim.GetProperty("start").GetInt64(),
                claim.GetProperty("end").GetInt64(),
                Encoding.UTF8.GetString(bytes.AsSpan(
                    claim.GetProperty("start").GetInt32(),
                    claim.GetProperty("end").GetInt32() - claim.GetProperty("start").GetInt32())),
                claim.GetProperty("kind").GetString() switch
                {
                    "declared-purpose" => ClaimKind.DeclaredPurpose,
                    "known-issue" => ClaimKind.KnownIssue,
                    "patch-instruction" => ClaimKind.PatchInstruction,
                    _ => throw new InvalidDataException("The analysis pipeline corpus claim kind is outside the closed product mapping."),
                },
                [], EvidenceAuthority.AuthoritativeExternal,
                Applicability(claim.GetProperty("applicability").GetString()!),
                ClassificationRole.Declared,
                claim.GetProperty("contradicts").EnumerateArray().Select(value => Id(value.GetString()!)).ToArray()))
            .ToArray();
        DocumentationApplicationInputContract[] applications = shared.GetProperty("applications").EnumerateArray()
            .Select(application =>
            {
                string? purpose = application.TryGetProperty("declared_purpose_code", out JsonElement code)
                    ? code.GetString() : null;
                return new DocumentationApplicationInputContract(
                    Id(application.GetProperty("claim_key").GetString()!), Id(runId),
                    Id(binding.AnalysisContextId), Id(application.GetProperty("subject_id").GetString()!),
                    "installed-entity", Id("dependency.source.001"),
                    Applicability(application.GetProperty("applicability").GetString()!),
                    application.TryGetProperty("supporting_claim_keys", out JsonElement supporting)
                        ? supporting.EnumerateArray().Select(value => Id(value.GetString()!)).ToArray() : [],
                    purpose is null ? null : new DocumentationPurposeInputContract(
                        purpose, [], Id("analyzer.001"), "exact independently authored declared purpose"));
            }).ToArray();
        DocumentationClaimImportManifestContract manifest = new(
            ContractConstants.DocumentationClaimImportSchemaId, Version(),
            Id(revision.GetProperty("source_id").GetString()!), DocumentationSourceKind.Fixture,
            revision.GetProperty("source_revision").GetString()!, DocumentationSourceAvailability.Present,
            new Sha256Fingerprint(revision.GetProperty("sha256").GetString()!), bytes.LongLength,
            Id(binding.InstallationSnapshotId), claims, applications);
        DocumentationApplicationTargetContract target = new(
            Id(runId), Id(binding.InstallationSnapshotId), Id(binding.AnalysisContextId),
            Id(binding.ResolvedInputManifestId), Id("entity.001"), "installed-entity", Id("dependency.source.001"));
        return new(Id(runId), Id(runId), DocumentationImportMode.CleanImport,
            Id("dependency.source.001"), Id("extractor.analysis_pipeline"), new UtcTimestamp(DateTimeOffset.UnixEpoch),
            manifest, bytes, null, [target]);
    }

    private static CandidatePipelineRequest CandidateRequest(
        string runId, RunBinding binding, JsonElement shared, DocumentationEvidenceContract documentation)
    {
        OpaqueId analyzer = Id("analyzer.001");
        CausalJoinPopulationMember[] members = shared.GetProperty("candidate_source_facts").EnumerateArray()
            .Select(fact => Member(fact, analyzer)).ToArray();
        TestCandidatePopulationSource source = new(analyzer, members);
        SemanticAnalysisContextContract context = AnalysisContext(binding.AnalysisContextId);
        ArtifactReferenceContract Reference(string id, char fingerprint = 'a') => new(
            Id(id), Version(), new Sha256Fingerprint(new string(fingerprint, 64)), "retained");
        AnalysisExecutionInputContract execution = new(
            ContractConstants.AnalysisExecutionInputSchemaId, Version(), Id("execution.analysis_pipeline.clean"), Id(runId),
            Reference(binding.InstallationSnapshotId), Reference("bethesda.analysis_pipeline"),
            documentation.Revisions.Select(item => new ArtifactReferenceContract(
                item.SourceId, Version(), item.ByteFingerprint, "retained")).ToArray(),
            [new(analyzer, Version(), CandidateAnalysisIdentity.StructuralHash(
                [JsonSerializer.Serialize(source.Declaration)]), "retained")],
            Reference(binding.EffectiveScanConfigurationId, 'b'), Reference(binding.ResolvedInputManifestId, 'c'),
            ReplayMode.Clean, null, 17, new(1_000_000, 2_000_000, 100_000, 100_000, 120_000), Boundaries())
        {
            AnalysisContext = new(context.ContextId, context.SchemaVersion, context.CanonicalFingerprint, "retained"),
        };
        return new(Id(runId), Id("population.001"), Id("ruleset.001"), Id("threshold.001"),
            CandidateExecutionLimits.Default,
            new CandidatePopulationContext(documentation, Id(runId), Id(binding.InstallationSnapshotId),
                Id(binding.AnalysisContextId), Id(binding.EffectiveScanConfigurationId)),
            [source], execution);
    }

    private static CausalJoinPopulationMember Member(JsonElement fact, OpaqueId analyzer)
    {
        string id = fact.GetProperty("fact_id").GetString()!;
        bool documentation = fact.GetProperty("kind").GetString() == "documentation-application";
        CausalJoinInputState state = documentation ? CausalJoinInputState.Ambiguous
            : fact.GetProperty("prior_target").GetString() == fact.GetProperty("winning_target").GetString()
                ? CausalJoinInputState.ResolvedNegative : CausalJoinInputState.Complete;
        OpaqueId source = Id(documentation ? "claim.002" : id + ".source");
        OpaqueId target = Id(documentation ? "entity.001" : id + ".target");
        return new(
            Id("member." + id), analyzer, CandidateLane.DeterministicRequired,
            [new(source, "source"), new(target, "target")],
            documentation ? "documentation-application" : "typed-relation-delta",
            [source, .. fact.GetProperty("evidence_ids").EnumerateArray().Select(value => Id(value.GetString()!)), target],
            fact.GetProperty("dependency_ids").EnumerateArray().Select(value => Id(value.GetString()!)).ToArray(),
            [Id(fact.GetProperty("evidence_ids")[0].GetString()!)],
            documentation ? [Id("claim.003")] : [],
            documentation ? ["uncontradicted-applicable-evidence"] : [], state,
            documentation ? "contradicted documentation application" : "bounded typed relation comparison",
            "The typed relation may affect retained downstream analysis.", null)
        {
            SourceFactId = Id(id),
        };
    }

    private static FindingCaseInputContract FindingInput(CandidateAnalysisContract candidates, JsonElement shared)
    {
        Dictionary<string, JsonElement> facts = shared.GetProperty("conclusion_factual_inputs").EnumerateArray()
            .ToDictionary(item => item.GetProperty("fact_id").GetString()!, StringComparer.Ordinal);
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = candidates.Decisions.ToDictionary(item => item.DecisionId);
        FindingEvidenceFactContract[] evidence = candidates.Hypotheses.Select(hypothesis =>
        {
            CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(item => item.CandidateId == hypothesis.CandidateId);
            CandidateDecisionContract decision = decisions[candidate.DecisionId];
            JsonElement fact = facts[decision.SourceFactId.Value];
            bool assigned = fact.GetProperty("consequence").GetProperty("state").GetString() == "assigned";
            return new FindingEvidenceFactContract(
                Id("finding." + decision.SourceFactId.Value), hypothesis.HypothesisId,
                assigned ? WorstCredibleConsequence.MeaningfulBoundedLoss : WorstCredibleConsequence.MaintenanceOnly,
                fact.GetProperty("causal_locus").GetProperty("field").GetString()!,
                fact.GetProperty("causal_conditions")[0].GetString()!,
                fact.GetProperty("applicability_condition_ids").EnumerateArray().Select(value => value.GetString()!).ToArray(),
                fact.GetProperty("contradicting_evidence_ids").EnumerateArray().Select(value => Id(value.GetString()!)).ToArray(),
                [], fact.GetProperty("supporting_evidence_ids").EnumerateArray()
                    .Where(value => hypothesis.SupportingEvidenceIds.Contains(Id(value.GetString()!)))
                    .Select(value => Id(value.GetString()!)).ToArray());
        }).ToArray();
        FindingRecommendationFactContract[] recommendations = evidence.Select(item => new FindingRecommendationFactContract(
            Id("recommendation." + item.FactId.Value), item.HypothesisId,
            RecommendationKind.Validation, "Validate the typed causal condition.",
            "Bounded to supplied typed evidence.", "Analysis is non-mutating.",
            ["State may differ after new input."], "Reobserve the affected locus.", item.EvidenceIds)).ToArray();

        CandidateHypothesisContract supported = candidates.Hypotheses.Single(hypothesis =>
            decisions[candidates.Candidates.Single(item => item.CandidateId == hypothesis.CandidateId).DecisionId]
                .SourceFactId == Id("fact.001"));
        CandidateDecisionContract supportedDecision = decisions[
            candidates.Candidates.Single(item => item.CandidateId == supported.CandidateId).DecisionId];
        CandidateAnalyzerBindingContract analyzer = candidates.AnalyzerBindings.Single();
        SharedCauseProofContract proof = new(
            Id("proof.001"), [supported.HypothesisId], analyzer.AnalyzerFamily,
            analyzer.SemanticContractVersion, analyzer.IdentityContractVersion,
            supportedDecision.Participants.ToDictionary(item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
            "prior-target-differs-from-winning-target", "linked_reference", ["condition.001"],
            FindingCaseIdentity.SharedCauseDependencyClosureId(supportedDecision.DependencyIds),
            supported.SupportingEvidenceIds)
        {
            AnalyzerVersion = analyzer.AnalyzerVersion,
        };
        TaxonomyClassificationFactContract[] taxonomy = candidates.Hypotheses.Select(hypothesis =>
            new TaxonomyClassificationFactContract(
                Id("taxonomy." + hypothesis.HypothesisId.Value), hypothesis.HypothesisId,
                "infinium.mod-impact", Version(), "impact", "effect", "bounded-effect",
                TaxonomyApplicability.Assigned, ClassificationRole.Established,
                hypothesis.SupportingEvidenceIds, [Id("taxonomy-condition.analysis_pipeline")], null,
                Id("analyzer.001"), new UtcTimestamp(DateTimeOffset.UnixEpoch),
                "Generic synthetic analysis pipeline corpus classification fact.")).ToArray();
        OpaqueId gapId = Id("coverage-gap.fact.003");
        CoverageMemberFactContract[] coverage = candidates.Decisions.Select(decision =>
        {
            CandidateHypothesisContract? hypothesis = candidates.Hypotheses.SingleOrDefault(item =>
                candidates.Candidates.Single(candidate => candidate.CandidateId == item.CandidateId).DecisionId == decision.DecisionId);
            bool gap = decision.SourceFactId == Id("fact.003");
            return new CoverageMemberFactContract(
                Id("coverage-member." + decision.SourceFactId.Value), Id("analyzer.001"), "population.001",
                "candidate source facts", decision.SourceFactId,
                gap ? CoverageMemberState.CompletedWithGaps : CoverageMemberState.Completed,
                gap ? "contradicted application needs information" : "typed fact completed",
                gap ? "uncontradicted-applicable-evidence" : "none", null,
                hypothesis is null ? [] : [taxonomy.Single(item => item.HypothesisId == hypothesis.HypothesisId).FactId],
                gap ? gapId : null);
        }).ToArray();
        FindingCaseInputContract input = new(
            ContractConstants.FindingCaseInputSchemaId, Version(), Id("pending"), candidates.OriginatingRunId,
            Id("promotion.analysis_pipeline"), Version(), Id("reconciliation.analysis_pipeline"), Version(), Id("actor.analysis_pipeline"),
            new UtcTimestamp(DateTimeOffset.UnixEpoch), candidates, evidence, recommendations, [proof], taxonomy, [],
            [new CoveragePopulationFactContract(Id("coverage.population.001"), Id("analyzer.001"),
                "population.001", "candidate source facts")],
            coverage, [], [], [], [], [], Boundaries());
        return input with { InputId = FindingCaseIdentity.ComputeInputId(input) };
    }

    private static ProductObservation Observe(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findings,
        AnalysisExecutionPhaseResult publication,
        RunOutputContract storedOutput,
        string human,
        RunRecord run) => new(
            documentation.Revisions.Count, documentation.Imports.Count, documentation.Passages.Count,
            documentation.Claims.Count, documentation.Applications.Count,
            documentation.PurposeAssignments.Count, documentation.Gaps.Count, documentation.Failures.Count,
            candidates.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.CandidateAdmitted),
            candidates.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.ResolvedNegative),
            candidates.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.Ambiguous),
            candidates.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.Unsupported),
            candidates.Candidates.Count, candidates.Hypotheses.Count, candidates.Abstentions.Count,
            findings.Findings.Count, findings.Recommendations.Count,
            findings.Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported),
            findings.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly),
            findings.Cases.Where(item => item.Kind == CaseOccurrenceKind.LeadOnly).Count(item => item.AffectsReadiness),
            findings.Coverage.Single().Denominator, findings.Coverage.Single().CompletedCount,
            findings.Coverage.Single().State, findings.Gaps.Count,
            run.State, LifecycleStateToken(run.State),
            RunOutputJsonCodec.Serialize(publication.Bundle.RunOutput).AsSpan()
                .SequenceEqual(RunOutputJsonCodec.Serialize(storedOutput)),
            human.Contains("canonical-run-output-json=" + Encoding.UTF8.GetString(RunOutputJsonCodec.Serialize(storedOutput)),
                StringComparison.Ordinal),
            publication.Bundle.ExternalBoundaries.Effects.Values.Count(value => value != "not-used"));

    private static void AssertObservation(JsonElement oracle, ProductObservation actual)
    {
        JsonElement documentation = oracle.GetProperty("documentation");
        Assert.AreEqual(documentation.GetProperty("revisions").GetInt32(), actual.Revisions);
        Assert.AreEqual(documentation.GetProperty("imports").GetInt32(), actual.Imports);
        Assert.AreEqual(documentation.GetProperty("passages").GetInt32(), actual.Passages);
        Assert.AreEqual(documentation.GetProperty("claims").GetInt32(), actual.Claims);
        Assert.AreEqual(documentation.GetProperty("applications").GetInt32(), actual.Applications);
        Assert.AreEqual(documentation.GetProperty("purpose_assignments").GetInt32(), actual.PurposeAssignments);
        Assert.AreEqual(documentation.GetProperty("contradiction_gaps").GetInt32(), actual.DocumentationGaps);
        Assert.AreEqual(documentation.GetProperty("failures").GetInt32(), actual.DocumentationFailures);
        JsonElement decisions = oracle.GetProperty("candidate").GetProperty("decisions");
        Assert.AreEqual(decisions.GetProperty("admitted").GetInt32(), actual.Admitted);
        Assert.AreEqual(decisions.GetProperty("resolved-negative").GetInt32(), actual.ResolvedNegative);
        Assert.AreEqual(decisions.GetProperty("ambiguous").GetInt32(), actual.Ambiguous);
        Assert.AreEqual(decisions.GetProperty("unsupported").GetInt32(), actual.Unsupported);
        Assert.AreEqual(oracle.GetProperty("candidate").GetProperty("candidates").GetInt32(), actual.Candidates);
        Assert.AreEqual(oracle.GetProperty("candidate").GetProperty("hypotheses").GetInt32(), actual.Hypotheses);
        Assert.AreEqual(oracle.GetProperty("candidate").GetProperty("abstentions").GetInt32(), actual.CandidateAbstentions);
        JsonElement finding_case = oracle.GetProperty("finding_case");
        Assert.AreEqual(finding_case.GetProperty("findings").GetInt32(), actual.Findings);
        Assert.AreEqual(finding_case.GetProperty("recommendations").GetInt32(), actual.Recommendations);
        Assert.AreEqual(finding_case.GetProperty("supported_cases").GetInt32(), actual.SupportedCases);
        Assert.AreEqual(finding_case.GetProperty("lead_only_cases").GetInt32(), actual.LeadOnlyCases);
        Assert.AreEqual(finding_case.GetProperty("readiness_effect_from_leads").GetInt32(), actual.LeadReadinessEffects);
        JsonElement coverage = finding_case.GetProperty("coverage");
        Assert.AreEqual(coverage.GetProperty("denominator").GetInt32(), actual.CoverageDenominator);
        Assert.AreEqual(coverage.GetProperty("completed").GetInt32(), actual.CoverageCompleted);
        Assert.AreEqual(coverage.GetProperty("state").GetString(), CoverageStateToken(actual.CoverageState));
        Assert.AreEqual(coverage.GetProperty("visible_gaps").GetInt32(), actual.VisibleGaps);
        JsonElement operations = oracle.GetProperty("operations");
        Assert.AreEqual(operations.GetProperty("publication_commits").GetInt32(), actual.StoredOutputEqual ? 1 : 0);
        Assert.AreEqual(operations.GetProperty("partial_publications").GetInt32(), actual.StoredOutputEqual ? 0 : 1);
        Assert.AreEqual(operations.GetProperty("terminal_state").GetString(), actual.StoredRunState);
        Assert.AreEqual(LifecycleState.CompletedWithGaps, actual.LifecycleState);
        Assert.AreEqual(operations.GetProperty("human_json_semantically_equivalent").GetBoolean(), actual.HumanEmbedsStoredJson);
        Assert.AreEqual(operations.GetProperty("network_calls").GetInt32(), actual.ExternalEffects);
        Assert.AreEqual(operations.GetProperty("provider_dispatches").GetInt32(), actual.ExternalEffects);
    }

    private static string StageValidationReceipt(AuthoritativeStore store, StoragePaths paths, AttemptRecord attempt)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"disposition\":\"validated-for-coordinator-publication-only\"}");
        const string name = "analysis-v1-validation-receipt.json";
        using AttemptStagingAuthority staging = paths.CreateAttemptStagingDirectory(attempt.AttemptId);
        File.WriteAllBytes(Path.Combine(paths.Staging, attempt.AttemptId, name), bytes);
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string artifact = "validation-" + attempt.AttemptId;
        string manifest = Convert.ToHexStringLower(ManagedWorkerManifest.ComputeDigest(artifact, name, sha, bytes.Length));
        return store.AdmitStagedPayload(attempt, name, sha, bytes.Length, manifest, 1024 * 1024,
            DateTimeOffset.UtcNow, stagedArtifactId: artifact).PayloadId;
    }

    private static RetainedAnalysisPayloadSeal Seal(
        AuthoritativeStore store, string payloadId, string schemaId, string schemaVersion)
    {
        RetainedPayloadRecord retained = store.GetRetainedPayload(payloadId);
        return new(retained.PayloadId, schemaId, schemaVersion, retained.Sha256, retained.ByteLength);
    }

    private static SemanticAnalysisContextContract AnalysisContext(string id)
    {
        SemanticAnalysisContextContract value = new(
            Id(id), Version(), new Sha256Fingerprint(new string('0', 64)), [],
            new Dictionary<string, string> { ["evidence-policy"] = "public-synthetic-local-only" });
        return value with { CanonicalFingerprint = SemanticAnalysisContextIdentity.ComputeFingerprint(value) };
    }

    private static ExecutionBoundaryContract[] Boundaries() =>
    [
        new("provider", BoundaryUseState.NotUsed, "local fixture"),
        new("hosted-search", BoundaryUseState.NotUsed, "local fixture"),
        new("nexus", BoundaryUseState.NotUsed, "local fixture"),
        new("loot", BoundaryUseState.NotUsed, "local fixture"),
    ];

    private static ClaimApplicabilityState Applicability(string value) => value switch
    {
        "applicable" => ClaimApplicabilityState.Applicable,
        "contradicted" => ClaimApplicabilityState.Contradicted,
        _ => throw new InvalidDataException("The analysis pipeline corpus applicability value is outside the closed product mapping."),
    };

    private static string CoverageStateToken(CoverageState value) => value switch
    {
        CoverageState.Completed => "completed",
        CoverageState.CompletedWithGaps => "completed-with-gaps",
        CoverageState.Failed => "failed",
        CoverageState.SkippedByLimit => "skipped-by-limit",
        CoverageState.Unsupported => "unsupported",
        _ => "unspecified",
    };

    private static string LifecycleStateToken(LifecycleState value) => value switch
    {
        LifecycleState.Completed => "completed",
        LifecycleState.CompletedWithGaps => "completed-with-gaps",
        LifecycleState.Cancelled => "cancelled",
        LifecycleState.LimitReached => "limit-reached",
        LifecycleState.Failed => "failed",
        _ => throw new InvalidDataException("The analysis pipeline corpus product lifecycle is not terminal."),
    };

    private static void AssertOrdinaryProductInputIsAnswerFree(JsonElement value)
    {
        HashSet<string> forbidden = new(StringComparer.OrdinalIgnoreCase)
        {
            "case_id", "eval_ids", "oracle_pointer", "expected", "expected_results", "partition",
            "review_metadata", "supported_cause", "answer", "verdict",
        };
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                Assert.IsFalse(forbidden.Contains(property.Name), $"Answer-bearing property leaked: {property.Name}");
                AssertOrdinaryProductInputIsAnswerFree(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertOrdinaryProductInputIsAnswerFree(item);
            }
        }
    }

    private static JsonDocument Parse(string path) => JsonDocument.Parse(
        File.ReadAllBytes(path), new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });

    private static OpaqueId Id(string value) => new(value);
    private static ContractVersion Version() => new(1, 0, 0);

    private sealed record ProductObservation(
        int Revisions, int Imports, int Passages, int Claims, int Applications, int PurposeAssignments,
        int DocumentationGaps, int DocumentationFailures, int Admitted, int ResolvedNegative, int Ambiguous,
        int Unsupported, int Candidates, int Hypotheses, int CandidateAbstentions, int Findings,
        int Recommendations, int SupportedCases, int LeadOnlyCases, int LeadReadinessEffects,
        long CoverageDenominator, long CoverageCompleted, CoverageState CoverageState, int VisibleGaps,
        LifecycleState LifecycleState, string StoredRunState, bool StoredOutputEqual,
        bool HumanEmbedsStoredJson, int ExternalEffects);
}
