using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.Evaluation;
using Infinium.Application.FindingCases;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

public static class AnalysisPublicationBuilder
{
    private static readonly string[] CollectionNames =
    [
        "observations", "deterministic_results", "external_claims", "application_links",
        "discovery_leads", "model_proposals", "proposal_admissions", "candidates",
        "hypotheses", "findings", "recommendations", "supported_cases", "lead_only_cases",
        "abstentions", "invalid_inputs", "coverage_gaps", "failures", "documentation_revisions",
        "passages", "candidate_decisions", "reconciliation_assessments", "lineage_events",
    ];

    public static AnalysisPublicationBundle Build(
        AnalysisV1WorkAssignment assignment,
        ReadOnlySpan<byte> documentationBytes,
        ReadOnlySpan<byte> candidateBytes,
        ReadOnlySpan<byte> findingCaseBytes,
        DateTimeOffset endedAt,
        string? comparedSemanticFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAssignment(assignment);
        DocumentationEvidenceContract documentation;
        CandidateAnalysisContract candidates;
        FindingCaseContract findingCases;
        try
        {
            ValidateSeal(assignment.DocumentationEvidence, documentationBytes);
            ValidateSeal(assignment.CandidateAnalysis, candidateBytes);
            ValidateSeal(assignment.FindingCase, findingCaseBytes);
            documentation = DocumentationEvidenceJsonCodec.Deserialize(documentationBytes);
            candidates = CandidateAnalysisJsonCodec.Deserialize(candidateBytes);
            findingCases = FindingCaseJsonCodec.Deserialize(findingCaseBytes);
            Slice5ContractInvariants.Validate(assignment.ExecutionInput);
            cancellationToken.ThrowIfCancellationRequested();
            string admittedRunId = assignment.ExecutionInput.RunId.Value;
            string SourceRun(string phaseId) => assignment.PhaseExecutions
                .SingleOrDefault(item => item.PhaseId == phaseId)?.SourceRunId ?? admittedRunId;
            if (documentation.OriginatingRunId.Value != SourceRun(DocumentationEvidencePhase.PhaseId)
                || candidates.OriginatingRunId.Value != SourceRun(CandidateAnalysisPhase.PhaseId)
                || findingCases.OriginatingRunId.Value != SourceRun(FindingCaseAnalysisPhase.PhaseId)
                || findingCases.InputId.Value.Length == 0
                || candidates.ExecutionInputId != assignment.ExecutionInput.ExecutionInputId
                || candidates.ExecutionInputFingerprint != CandidateAnalysisIdentity.StructuralHash(
                    CandidatePipelineRequest.DescribeExecutionInput(assignment.ExecutionInput)))
            {
                throw new InvalidDataException("WP2-WP4 aggregate identities do not bind the admitted analysis-v1 execution input.");
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            throw new AnalysisInputAdmissionException(
                "Retained analysis inputs failed exact schema, version, dependency, or identity admission.", exception);
        }

        string runId = assignment.ExecutionInput.RunId.Value;

        long inputBytes = checked(documentationBytes.Length + candidateBytes.Length + findingCaseBytes.Length);
        if (inputBytes > assignment.MaximumInputBytes)
        {
            throw new InvalidDataException("The retained analysis inputs exceed the assignment input limit.");
        }

        string documentationSha = Hash(documentationBytes);
        string candidateSha = Hash(candidateBytes);
        string findingSha = Hash(findingCaseBytes);
        string semanticFingerprint = SemanticFingerprint(documentation, candidates, findingCases, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        string dependencyClosureId = StableId(
            "analysis-dependency-closure", runId, assignment.ExecutionInput.Mode.ToString(),
            assignment.ExecutionInput.PriorRunId?.Value ?? "none",
            assignment.ExecutionInput.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            documentationSha, candidateSha, findingSha,
            candidates.ExecutionInputFingerprint.Value, candidates.AnalyzerSetFingerprint.Value,
            candidates.PolicyFingerprint.Value, candidates.ThresholdFingerprint.Value,
            candidates.LimitFingerprint.Value);

        List<ReplayDependencyNodeContract> dependencies = BuildDependencies(
            assignment, documentation, candidates, findingCases, documentationSha, candidateSha, findingSha);
        cancellationToken.ThrowIfCancellationRequested();
        OpaqueId[] missingDependencyIds = dependencies
            .Where(item => item.State != Slice5ResultState.Present)
            .Select(item => item.DependencyId)
            .Distinct().OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
        List<ReplayDependencyEdgeContract> dependencyEdges = [];
        HashSet<OpaqueId> documentationDependencyIds = assignment.DocumentationDependencyIds.Count == 0
            ? assignment.ExecutionInput.SourceInputs.Select(item => item.ArtifactId)
                .Where(id => documentation.Revisions.Any(item => item.SourceId == id))
                .Append(assignment.AnalysisContext.ContextId).ToHashSet()
            : assignment.DocumentationDependencyIds.ToHashSet();
        if (!documentationDependencyIds.Contains(assignment.AnalysisContext.ContextId)
            || documentation.Revisions.Any(item => !documentationDependencyIds.Contains(item.SourceId))
            || documentationDependencyIds.Any(id => id != assignment.AnalysisContext.ContextId
                && !assignment.ExecutionInput.SourceInputs.Any(item => item.ArtifactId == id)))
        {
            throw new AnalysisIdentityDriftException(
                "Documentation provenance dependencies differ from the exact retained WP2 input closure.");
        }
        Dictionary<string, OpaqueId> phaseNodes = assignment.PhaseExecutions.ToDictionary(
            item => item.PhaseId,
            item => new OpaqueId("phase-" + Hash(Encoding.UTF8.GetBytes(item.PhaseId + "|" + item.InputFingerprint))[..32]),
            StringComparer.Ordinal);
        if (phaseNodes.TryGetValue(DocumentationEvidencePhase.PhaseId, out OpaqueId? documentationPhase)
            && phaseNodes.TryGetValue(CandidateAnalysisPhase.PhaseId, out OpaqueId? candidatePhase)
            && phaseNodes.TryGetValue(FindingCaseAnalysisPhase.PhaseId, out OpaqueId? findingPhase))
        {
            OpaqueId documentationOutput = documentation.PayloadId;
            OpaqueId candidateOutput = candidates.PayloadId;
            OpaqueId findingOutput = findingCases.PayloadId;
            dependencyEdges.Add(new ReplayDependencyEdgeContract(documentationOutput, documentationPhase));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidateOutput, candidatePhase));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(findingOutput, findingPhase));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidatePhase, documentationOutput));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(findingPhase, candidateOutput));
            foreach ((string kind, ArtifactReferenceContract reference) in References(assignment.ExecutionInput))
            {
                dependencyEdges.Add(new ReplayDependencyEdgeContract(
                    assignment.ExecutionInput.ExecutionInputId, reference.ArtifactId));
                if (documentationDependencyIds.Contains(reference.ArtifactId))
                {
                    dependencyEdges.Add(new ReplayDependencyEdgeContract(documentationPhase, reference.ArtifactId));
                }
                if (kind is "analysis-context" or "installation-snapshot" or "bethesda-semantic-input"
                    or "source-input" or "analyzer-declaration" or "effective-configuration" or "resolved-input-manifest")
                {
                    dependencyEdges.Add(new ReplayDependencyEdgeContract(candidatePhase, reference.ArtifactId));
                }
            }
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidatePhase, candidates.PolicyId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidatePhase, candidates.ThresholdId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidatePhase, candidates.LimitId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(findingPhase, findingCases.PromotionPolicyId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(findingPhase, findingCases.ReconciliationPolicyId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidatePhase,
                new OpaqueId("fixture-seed-" + assignment.ExecutionInput.Seed)));
        }
        else
        {
            foreach ((string kind, ArtifactReferenceContract reference) in References(assignment.ExecutionInput))
            {
                dependencyEdges.Add(new ReplayDependencyEdgeContract(
                    assignment.ExecutionInput.ExecutionInputId, reference.ArtifactId));
                if (documentationDependencyIds.Contains(reference.ArtifactId))
                {
                    dependencyEdges.Add(new ReplayDependencyEdgeContract(documentation.PayloadId, reference.ArtifactId));
                }
                if (kind is "analysis-context" or "installation-snapshot" or "bethesda-semantic-input"
                    or "source-input" or "analyzer-declaration" or "effective-configuration" or "resolved-input-manifest")
                {
                    dependencyEdges.Add(new ReplayDependencyEdgeContract(candidates.PayloadId, reference.ArtifactId));
                }
            }
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidates.PayloadId, documentation.PayloadId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(findingCases.PayloadId, candidates.PayloadId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidates.PayloadId, candidates.PolicyId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidates.PayloadId, candidates.ThresholdId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(candidates.PayloadId, candidates.LimitId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(findingCases.PayloadId, findingCases.PromotionPolicyId));
            dependencyEdges.Add(new ReplayDependencyEdgeContract(findingCases.PayloadId, findingCases.ReconciliationPolicyId));
        }

        bool hasExecutionFailures = documentation.Failures.Count != 0
            || candidates.Failures.Count != 0
            || findingCases.CoverageFailures.Count != 0;
        bool completeTerminal = assignment.TerminalOutcome is AnalysisTerminalOutcome.Completed
            or AnalysisTerminalOutcome.CompletedWithGaps;
        bool semanticallyEquivalent = assignment.ExecutionInput.Mode == ReplayMode.Clean
            ? true
            : StringComparer.Ordinal.Equals(comparedSemanticFingerprint, semanticFingerprint);
        // Coverage gaps describe the bounded semantic result, not loss from the
        // retained dependency or audit closure. A completed-with-gaps result can
        // still replay completely from its exact retained inputs.
        ReplayState replayState = completeTerminal && !hasExecutionFailures
            && missingDependencyIds.Length == 0 && semanticallyEquivalent
            ? ReplayState.CompleteClean
            : ReplayState.Partial;
        AuditabilityState auditabilityState = completeTerminal && !hasExecutionFailures
            && missingDependencyIds.Length == 0
            ? AuditabilityState.Complete
            : AuditabilityState.Partial;
        string replayManifestId = StableId(
            "analysis-replay", dependencyClosureId, semanticFingerprint,
            assignment.TerminalOutcome.ToString(), semanticallyEquivalent.ToString());
        OpaqueId replayNode = new(replayManifestId);
        dependencies.Add(new ReplayDependencyNodeContract(
            replayNode, "analysis-replay", new ContractVersion(1, 0, 0),
            new Sha256Fingerprint(Hash(Encoding.UTF8.GetBytes(
                dependencyClosureId + "|" + semanticFingerprint))), Slice5ResultState.Present));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, assignment.ExecutionInput.ExecutionInputId));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, documentation.PayloadId));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, candidates.PayloadId));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, findingCases.PayloadId));
        AnalysisReplayContract replay = new(
            ContractConstants.AnalysisReplaySchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId(replayManifestId),
            assignment.ExecutionInput.RunId,
            assignment.ExecutionInput.Mode,
            replayState,
            auditabilityState,
            dependencies.OrderBy(item => item.DependencyId.Value, StringComparer.Ordinal).ToArray(),
            dependencyEdges.OrderBy(item => item.To.Value, StringComparer.Ordinal).ToArray(),
            [
                ReplayOutput(assignment.DocumentationEvidence, documentation.PayloadId, documentationSha),
                ReplayOutput(assignment.CandidateAnalysis, candidates.PayloadId, candidateSha),
                ReplayOutput(assignment.FindingCase, findingCases.PayloadId, findingSha),
            ],
            missingDependencyIds,
            documentation.Gaps.Select(item => item.GapId)
                .Concat(candidates.Gaps.Select(item => item.GapId))
                .Concat(findingCases.Gaps.Select(item => item.GapId))
                .Concat(missingDependencyIds.Select(item => new OpaqueId(MissingDependencyGapId(item.Value))))
                .Distinct().OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            semanticallyEquivalent,
            assignment.ExecutionInput.PriorRunId);
        Slice5ContractInvariants.Validate(replay);

        ExternalBoundaryReceipt boundaryReceipt = new(
            1,
            runId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "not-used",
                ["model"] = "not-used",
                ["credential"] = "not-used",
                ["live"] = "not-used",
                ["billable"] = "not-used",
            },
            "M1 Slice 5 WP5 is a retained local-only execution path.");

        string provisionalCliFingerprint = new string('0', 64);
        RunOutputContract provisional = BuildRunOutput(
            assignment, documentation, candidates, findingCases, replay,
            dependencyClosureId, semanticFingerprint, provisionalCliFingerprint, endedAt);
        cancellationToken.ThrowIfCancellationRequested();
        CliSummaryDocumentContract cli = BuildCliSummary(assignment, provisional, endedAt);
        byte[] cliBytes = CliSummaryJsonCodec.Serialize(cli);
        string cliFingerprint = Hash(cliBytes);
        RunOutputContract output = BuildRunOutput(
            assignment, documentation, candidates, findingCases, replay,
            dependencyClosureId, semanticFingerprint, cliFingerprint, endedAt);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] outputBytes = RunOutputJsonCodec.Serialize(output);
        if (outputBytes.LongLength > assignment.MaximumOutputBytes)
        {
            throw new AnalysisOutputLimitException("The canonical run output exceeds the assignment output byte limit.");
        }
        long outputItems = CountOutputItems(output);
        if (outputItems > assignment.ExecutionInput.Limits.MaximumOutputItems)
        {
            throw new AnalysisOutputLimitException("The canonical run output exceeds the assignment output-item limit.");
        }
        string human = AnalysisOutputRenderer.Render(output, cli);
        long queryResponseBytes = checked(outputBytes.LongLength + cliBytes.LongLength + Encoding.UTF8.GetByteCount(human));
        if (queryResponseBytes > AnalysisV1WorkAssignment.AbsoluteMaximumQueryResponseBytes)
        {
            throw new AnalysisOutputLimitException("The complete human and JSON result exceeds the application query-response limit.");
        }

        List<AnalysisPublishedArtifact> artifacts =
        [
            Published(assignment.DocumentationEvidence, documentation.PayloadId.Value, "documentation-evidence", dependencyClosureId),
            Published(assignment.CandidateAnalysis, candidates.PayloadId.Value, "candidate-analysis", dependencyClosureId),
            Published(assignment.FindingCase, findingCases.PayloadId.Value, "finding-case", dependencyClosureId),
            new AnalysisPublishedArtifact(
                replayManifestId, "analysis-replay", replay.SchemaId, replay.SchemaVersion.ToString(), 1,
                Kebab(replay.ReplayState), Hash(AnalysisReplayJsonCodec.Serialize(replay)),
                AnalysisReplayJsonCodec.Serialize(replay).LongLength,
                StableId("provenance", replayManifestId), dependencyClosureId),
            new AnalysisPublishedArtifact(
                assignment.ExecutionInput.ExecutionInputId.Value, "analysis-execution-input",
                assignment.ExecutionInput.SchemaId, assignment.ExecutionInput.SchemaVersion.ToString(), 1,
                "present", Hash(AnalysisExecutionInputJsonCodec.Serialize(assignment.ExecutionInput)),
                AnalysisExecutionInputJsonCodec.Serialize(assignment.ExecutionInput).LongLength,
                StableId("provenance", assignment.ExecutionInput.ExecutionInputId.Value), dependencyClosureId),
        ];
        return new AnalysisPublicationBundle(
            replay, output, cli, boundaryReceipt, dependencyClosureId, semanticFingerprint, artifacts);
    }

    public static void ValidateAssignment(AnalysisV1WorkAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        Slice5ContractInvariants.Validate(assignment.ExecutionInput);
        if (assignment.SchemaVersion != AnalysisV1WorkAssignment.CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(assignment.AssignmentId)
            || assignment.AssignmentId.Length > 128
            || string.IsNullOrWhiteSpace(assignment.AnalysisContext.ContextId.Value)
            || assignment.AnalysisContext.ContextId.Value.Length > 128
            || assignment.ExecutionInput.Boundaries.Any(item => item.State != BoundaryUseState.NotUsed)
            || assignment.MaximumInputBytes is < 1 or > AnalysisV1WorkAssignment.AbsoluteMaximumInputBytes
            || assignment.MaximumOutputBytes is < AnalysisV1WorkAssignment.MinimumTerminalOutputBytes
                or > AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes
            || assignment.MaximumQueryItems is < 1 or > AnalysisV1WorkAssignment.AbsoluteMaximumQueryItems
            || assignment.ExecutionInput.AnalyzerDeclarations.Count == 0
            || assignment.ExecutionInput.Limits.MaximumOutputItems
                < checked(4L + assignment.ExecutionInput.AnalyzerDeclarations.Count)
            || assignment.ExecutionInput.Limits.MaximumOutputItems > assignment.MaximumQueryItems * 1000
            || assignment.TerminalReason.Length is < 1 or > 512
            || !System.Text.RegularExpressions.Regex.IsMatch(
                assignment.ImplementationCommit,
                "^[a-f0-9]{40}$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1)))
        {
            throw new InvalidDataException("The analysis-v1 assignment is unbounded, malformed, or enables an external boundary.");
        }
        SemanticAnalysisContextIdentity.Validate(assignment.AnalysisContext);
        if (assignment.ExecutionInput.AnalysisContext.ArtifactId != assignment.AnalysisContext.ContextId
            || assignment.ExecutionInput.AnalysisContext.ArtifactVersion != assignment.AnalysisContext.SchemaVersion
            || assignment.ExecutionInput.AnalysisContext.Fingerprint != assignment.AnalysisContext.CanonicalFingerprint
            || assignment.ExecutionInput.AnalysisContext.Availability != "retained")
        {
            throw new InvalidDataException("The execution input does not retain the exact semantic analysis context identity.");
        }
        if (assignment.DocumentationDependencyIds.Count != 0
            && (assignment.DocumentationDependencyIds.Count > 10_002
                || assignment.DocumentationDependencyIds.Distinct().Count()
                    != assignment.DocumentationDependencyIds.Count
                || !assignment.DocumentationDependencyIds.Contains(assignment.AnalysisContext.ContextId)
                || assignment.DocumentationDependencyIds.Any(id => id != assignment.AnalysisContext.ContextId
                    && !assignment.ExecutionInput.SourceInputs.Any(item => item.ArtifactId == id))))
        {
            throw new InvalidDataException("The exact documentation input dependency closure is malformed.");
        }

        RetainedAnalysisPayloadSeal[] seals =
            [assignment.DocumentationEvidence, assignment.CandidateAnalysis, assignment.FindingCase];
        if (assignment.DocumentationEvidence.SchemaId != ContractConstants.DocumentationEvidenceSchemaId
            || assignment.DocumentationEvidence.SchemaVersion != "1.0.0"
            || assignment.CandidateAnalysis.SchemaId != ContractConstants.CandidateAnalysisSchemaId
            || assignment.CandidateAnalysis.SchemaVersion != "1.0.0"
            || assignment.FindingCase.SchemaId != ContractConstants.FindingCaseSchemaId
            || assignment.FindingCase.SchemaVersion != "1.0.0"
            || seals.Select(item => item.PayloadId).Distinct(StringComparer.Ordinal).Count() != seals.Length
            || seals.Sum(item => item.ByteLength) > assignment.MaximumInputBytes
            || seals.Any(item => string.IsNullOrWhiteSpace(item.PayloadId)
                || item.ByteLength < 1
                || item.ByteLength > assignment.MaximumInputBytes
                || item.Sha256.Length != 64
                || item.Sha256.Any(ch => ch is not (>= '0' and <= '9' or >= 'a' and <= 'f'))))
        {
            throw new InvalidDataException("The analysis-v1 retained payload seals are invalid or duplicated.");
        }
        string[] knownPhases =
            [DocumentationEvidencePhase.PhaseId, CandidateAnalysisPhase.PhaseId, FindingCaseAnalysisPhase.PhaseId];
        if (assignment.PhaseExecutions.Count > knownPhases.Length
            || assignment.PhaseExecutions.Select(item => item.PhaseId).Distinct(StringComparer.Ordinal).Count()
                != assignment.PhaseExecutions.Count
            || assignment.PhaseExecutions.Any(item => !knownPhases.Contains(item.PhaseId, StringComparer.Ordinal)
                || item.InputFingerprint.Length != 64
                || item.InputFingerprint.Any(ch => ch is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
                || item.SourceRunId.Length is < 1 or > 128
                || item.Disposition is not ("recomputed-invalidated" or "recomputed-run-binding"
                    or "reused-completed-phase" or "reused-retained-phase")
                || !seals.Contains(item.Output)))
        {
            throw new InvalidDataException("The analysis-v1 phase execution ledger is malformed.");
        }
    }

    private static RunOutputContract BuildRunOutput(
        AnalysisV1WorkAssignment assignment,
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases,
        AnalysisReplayContract replay,
        string dependencyClosureId,
        string semanticFingerprint,
        string cliFingerprint,
        DateTimeOffset endedAt)
    {
        string runId = assignment.ExecutionInput.RunId.Value;
        ArtifactReferenceDocumentContract documentPayload = Reference(assignment.DocumentationEvidence);
        ArtifactReferenceDocumentContract candidatePayload = Reference(assignment.CandidateAnalysis);
        ArtifactReferenceDocumentContract findingPayload = Reference(assignment.FindingCase);
        ArtifactProvenanceDocumentContract DocumentProvenance(
            string producer,
            IReadOnlyList<string>? supporting = null,
            IReadOnlyList<string>? contradicting = null) => new(
            producer, "1.0.0", runId,
            [Reference(assignment.ExecutionInput.InstallationSnapshot), Reference(assignment.ExecutionInput.EffectiveConfiguration)],
            supporting?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [],
            contradicting?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [],
            new LlmInvolvementDocumentContract("none", "none", null));
        List<TypedArtifactDocumentContract> docs = documentation.Revisions.Select(item =>
            Typed(item.RevisionId.Value, "documentation-revision", Kebab(item.RetentionState), documentPayload, DocumentProvenance("m1-s5-wp2-documentation-evidence"))).ToList();
        List<TypedArtifactDocumentContract> imports = documentation.Imports.Select(item =>
            Typed(item.ImportId.Value, "deterministic-result", "present", documentPayload,
                DocumentProvenance(item.ExtractorId.Value, [item.RevisionId.Value]))).ToList();
        List<TypedArtifactDocumentContract> passages = documentation.Passages.Select(item =>
            Typed(item.PassageId.Value, "passage", Kebab(item.State), documentPayload, DocumentProvenance("m1-s5-wp2-documentation-evidence"))).ToList();
        List<TypedArtifactDocumentContract> claims = documentation.Claims.Select(item =>
            Typed(item.ClaimId.Value, "external-claim", ClaimState(item.Applicability), documentPayload,
                DocumentProvenance("m1-s5-wp2-documentation-evidence",
                    [item.ProducingImportId.Value, item.PassageId.Value],
                    item.ContradictingEvidenceIds.Select(id => id.Value).ToArray()))).ToList();
        List<TypedArtifactDocumentContract> applications = documentation.Applications.Select(item =>
            Typed(item.ApplicationId.Value, "application-link", ClaimState(item.Applicability), documentPayload,
                DocumentProvenance("m1-s5-wp2-documentation-evidence",
                    [item.ClaimId.Value, .. item.EvidenceIds.Select(id => id.Value)]))).ToList();
        List<TypedArtifactDocumentContract> deletionReceipts = documentation.DeletionReceipts.Select(item =>
            Typed(item.ReceiptId.Value, "deterministic-result",
                item.ReplayEffect == ReplayState.CompleteClean ? "present" : "partial", documentPayload,
                DocumentProvenance("m1-s5-wp2-documentation-evidence",
                    [item.RevisionId.Value, .. item.IndependentlyRetainedPayloadIds.Select(id => id.Value)]))).ToList();
        List<TypedArtifactDocumentContract> candidateArtifacts = candidates.Candidates.Select(item =>
            Typed(item.CandidateId.Value, "candidate", Kebab(item.State), candidatePayload, DocumentProvenance(candidates.AnalyzerId.Value))).ToList();
        List<TypedArtifactDocumentContract> hypotheses = candidates.Hypotheses.Select(item =>
            Typed(item.HypothesisId.Value, "hypothesis", Kebab(item.State), candidatePayload, DocumentProvenance(candidates.AnalyzerId.Value))).ToList();
        List<TypedArtifactDocumentContract> candidateDecisions = candidates.Decisions.Select(item =>
            Typed(item.DecisionId.Value, "candidate-decision", DecisionState(item.Disposition), candidatePayload, DocumentProvenance(item.AnalyzerId.Value))).ToList();
        List<TypedArtifactDocumentContract> abstentions = candidates.Abstentions.Select(item =>
            Typed(item.AbstentionId.Value, "abstention", "abstained", candidatePayload, DocumentProvenance(item.AnalyzerId.Value)))
            .Concat(findingCases.Abstentions.Select(item =>
                Typed(item.AbstentionId.Value, "abstention", "abstained", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))))
            .GroupBy(item => item.ArtifactId, StringComparer.Ordinal).Select(item => item.First()).ToList();
        List<TypedArtifactDocumentContract> findings = findingCases.Findings.Select(item =>
            Typed(item.FindingOccurrenceId.Value, "finding", "present", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> recommendations = findingCases.Recommendations.Select(item =>
            Typed(item.RecommendationId.Value, "recommendation", "present", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> supportedCases = findingCases.Cases.Where(item => item.Kind == CaseOccurrenceKind.Supported).Select(item =>
            Typed(item.CaseOccurrenceId.Value, "supported-case", "present", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> leadCases = findingCases.Cases.Where(item => item.Kind == CaseOccurrenceKind.LeadOnly).Select(item =>
            Typed(item.CaseOccurrenceId.Value, "lead-only-case", "present", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> discoveryLeads = findingCases.Cases.Where(item => item.Kind == CaseOccurrenceKind.LeadOnly).Select(item =>
            Typed("lead-" + item.CaseOccurrenceId.Value, "discovery-lead", "present", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> gaps = documentation.Gaps.Select(item =>
            Typed(item.GapId.Value, "coverage-gap", "partial", documentPayload, DocumentProvenance("m1-s5-wp2-documentation-evidence")))
            .Concat(candidates.Gaps.Select(item =>
                Typed(item.GapId.Value, "coverage-gap", Kebab(item.State), candidatePayload, DocumentProvenance(candidates.AnalyzerId.Value))))
            .Concat(findingCases.Gaps.Select(item =>
                Typed(item.GapId.Value, "coverage-gap", "partial", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))))
            .Concat(replay.MissingDependencyIds.Select(item =>
                Typed(MissingDependencyGapId(item.Value), "coverage-gap", "unavailable", candidatePayload,
                    DocumentProvenance("m1-s5-wp5-replay-dependency-audit", [item.Value]))))
            .GroupBy(item => item.ArtifactId, StringComparer.Ordinal).Select(item => item.First()).ToList();
        List<TypedArtifactDocumentContract> replayDependencyGaps = replay.MissingDependencyIds.Select(item =>
                Typed(MissingDependencyGapId(item.Value), "coverage-gap", "unavailable", candidatePayload,
                    DocumentProvenance("m1-s5-wp5-replay-dependency-audit", [item.Value])))
            .ToList();
        List<TypedArtifactDocumentContract> failures = documentation.Failures.Select(item =>
            Typed(item.FailureId.Value, "failure", "failed", documentPayload, DocumentProvenance("m1-s5-wp2-documentation-evidence")))
            .Concat(candidates.Failures.Select(item =>
                Typed(item.FailureId.Value, "failure", "failed", candidatePayload, DocumentProvenance(item.AnalyzerId.Value))))
            .Concat(findingCases.CoverageFailures.Select(item =>
                Typed(item.FailureId.Value, "failure", "failed", findingPayload, DocumentProvenance(item.AnalyzerId.Value))))
            .GroupBy(item => item.ArtifactId, StringComparer.Ordinal).Select(item => item.First()).ToList();
        List<TypedArtifactDocumentContract> reconciliations = findingCases.ReconciliationAssessments.Select(item =>
            Typed(item.AssessmentId.Value, "reconciliation-assessment", "present", findingPayload, DocumentProvenance(item.ActorId.Value))).ToList();
        List<TypedArtifactDocumentContract> lineage = findingCases.LineageEvents.Select(item =>
            Typed(item.EventId.Value, "lineage-event", "present", findingPayload, DocumentProvenance("m1-s5-wp4-finding-case-analysis"))).ToList();

        Dictionary<string, IReadOnlyList<TypedArtifactDocumentContract>> collections = new(StringComparer.Ordinal)
        {
            ["observations"] = [],
            ["deterministic_results"] = [.. imports, .. deletionReceipts],
            ["external_claims"] = claims,
            ["application_links"] = applications,
            ["discovery_leads"] = discoveryLeads,
            ["model_proposals"] = [],
            ["proposal_admissions"] = [],
            ["candidates"] = candidateArtifacts,
            ["hypotheses"] = hypotheses,
            ["findings"] = findings,
            ["recommendations"] = recommendations,
            ["supported_cases"] = supportedCases,
            ["lead_only_cases"] = leadCases,
            ["abstentions"] = abstentions,
            ["invalid_inputs"] = [],
            ["coverage_gaps"] = gaps,
            ["failures"] = failures,
            ["documentation_revisions"] = docs,
            ["passages"] = passages,
            ["candidate_decisions"] = candidateDecisions,
            ["reconciliation_assessments"] = reconciliations,
            ["lineage_events"] = lineage,
        };
        Dictionary<string, RunOutputCollectionStateContract> states = CollectionNames.ToDictionary(
            name => name,
            name => new RunOutputCollectionStateContract(
                collections[name].Count == 0 ? "empty" : "populated",
                collections[name].Count == 0 ? "no retained artifacts were produced" : "retained artifacts are listed"),
            StringComparer.Ordinal);
        IEnumerable<TaxonomyAssignmentDocumentContract> documentationTaxonomy = documentation.PurposeAssignments.Select(item =>
            new TaxonomyAssignmentDocumentContract(
                item.AssignmentId.Value, item.TaxonomyId, item.TaxonomyVersion.ToString(), item.SubjectType,
                item.SubjectId.Value, item.Axis, item.Facet, item.Code, Kebab(item.Applicability), Kebab(item.Role),
                [item.ClaimId.Value, item.ApplicationId.Value],
                item.ApplicabilityConditionIds.Select(id => id.Value).ToArray(), null, item.Reason,
                DocumentProvenance(item.AnalyzerOrAdjudicatorId.Value,
                    [item.ClaimId.Value, item.ApplicationId.Value])));
        List<TaxonomyAssignmentDocumentContract> taxonomy = documentationTaxonomy.Concat(
            findingCases.TaxonomyAssignments.Select(item => new TaxonomyAssignmentDocumentContract(
            item.AssignmentId.Value, item.TaxonomyId, item.TaxonomyVersion.ToString(), item.SubjectType,
            item.SubjectId.Value, item.Axis, item.Facet, item.Code, Kebab(item.Applicability),
            item.Role is null ? "not-applicable" : Kebab(item.Role.Value),
            item.EvidenceIds.Select(id => id.Value).ToArray(), item.ApplicabilityConditionIds.Select(id => id.Value).ToArray(),
            item.ConfidenceAssessmentId?.Value, item.Reason, DocumentProvenance(item.AnalyzerOrAdjudicatorId.Value))))
            .GroupBy(item => item.AssignmentId, StringComparer.Ordinal).Select(group => group.Single()).ToList();
        List<CoverageDocumentContract> coverage = findingCases.Coverage.Select(item => new CoverageDocumentContract(
            item.CoverageId.Value, item.AnalyzerId.Value, item.PopulationId, item.DenominatorLabel,
            item.Denominator, item.CompletedCount, Kebab(item.State), item.TaxonomyId, item.TaxonomyVersion.ToString(),
            taxonomy.Where(value => item.TaxonomyAssignmentIds.Contains(new OpaqueId(value.AssignmentId))).ToArray(),
            gaps.Where(value => item.GapIds.Contains(new OpaqueId(value.ArtifactId))).ToArray(),
            item.Exclusions.Select(value => value.MemberId.Value + ":" + value.Reason).ToArray(),
            failures.Where(value => item.FailureIds.Contains(new OpaqueId(value.ArtifactId))).ToArray())).ToList();

        string runState = TerminalState(assignment.TerminalOutcome);
        RunOutputContract output = new(
            ContractConstants.RunOutputSchemaId, "1", runId, "analysis", runState,
            assignment.ImplementationCommit, Utc(assignment.StartedAt), Utc(endedAt),
            Reference(assignment.ExecutionInput.InstallationSnapshot),
            new ArtifactReferenceDocumentContract(
                assignment.AnalysisContext.ContextId.Value, assignment.AnalysisContext.SchemaVersion.ToString(),
                assignment.AnalysisContext.CanonicalFingerprint.Value, "retained"),
            Reference(assignment.ExecutionInput.EffectiveConfiguration), Reference(assignment.ExecutionInput.ResolvedInputManifest),
            ContractConstants.TaxonomyId, ContractConstants.TaxonomyVersion,
            assignment.ExecutionInput.AnalyzerDeclarations.Select(Reference).ToArray(),
            collections["observations"], collections["deterministic_results"], collections["external_claims"],
            collections["application_links"], collections["discovery_leads"], collections["model_proposals"],
            collections["proposal_admissions"], collections["candidates"], collections["hypotheses"],
            collections["findings"], collections["recommendations"], collections["supported_cases"],
            collections["lead_only_cases"], collections["abstentions"], collections["invalid_inputs"],
            collections["coverage_gaps"], collections["failures"], collections["documentation_revisions"],
            collections["passages"], collections["candidate_decisions"], collections["reconciliation_assessments"],
            collections["lineage_events"], states, taxonomy, coverage,
            [new ExcludedCapabilityDocumentContract("slice-6-and-later", "unsupported", "outside WP5 authority")],
            new ReadinessDocumentContract("no-readiness-evaluation", "m1-slice5-wp5", true),
            new ReplayabilityDocumentContract(
                replay.ReplayState == ReplayState.CompleteClean ? "complete" : "partial",
                replay.ReplayState == ReplayState.CompleteClean ? "complete-clean" : "boundary-replay",
                new ArtifactReferenceDocumentContract(dependencyClosureId, "1.0.0", semanticFingerprint, "retained"), replayDependencyGaps),
            new AuditabilityDocumentContract(
                replay.AuditabilityState == AuditabilityState.Complete ? "complete" : "complete-with-gaps",
                replay.AuditabilityState == AuditabilityState.Complete ? [] : replayDependencyGaps),
            new ArtifactReferenceDocumentContract(replay.ReplayManifestId.Value, replay.SchemaVersion.ToString(),
                Hash(AnalysisReplayJsonCodec.Serialize(replay)), "retained"),
            [
                new ExcludedCapabilityDocumentContract("provider", "not-used", "local-only retained execution"),
                new ExcludedCapabilityDocumentContract("model", "not-used", "no model surface in WP5"),
                new ExcludedCapabilityDocumentContract("credential", "not-used", "no credential surface in WP5"),
                new ExcludedCapabilityDocumentContract("live", "not-used", "network-off execution"),
                new ExcludedCapabilityDocumentContract("billable", "not-used", "no billable dispatch"),
            ],
            cliFingerprint,
            [new ArtifactReferenceDocumentContract("semantic-output-" + semanticFingerprint[..24], "1.0.0", semanticFingerprint, "retained")]);
        RunOutputContractInvariants.Validate(output);
        return output;
    }

    private static CliSummaryDocumentContract BuildCliSummary(
        AnalysisV1WorkAssignment assignment,
        RunOutputContract output,
        DateTimeOffset endedAt)
    {
        long Duration() => Math.Max(0, (long)(endedAt - assignment.StartedAt).TotalMilliseconds);
        string outcome = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Completed => output.CoverageGaps.Count == 0 && output.Failures.Count == 0
                ? "completed" : "completed-with-gaps",
            AnalysisTerminalOutcome.CompletedWithGaps => "completed-with-gaps",
            AnalysisTerminalOutcome.Cancelled => "cancelled",
            AnalysisTerminalOutcome.LimitReached => "limit-reached",
            _ => "failed",
        };
        CliSummaryDocumentContract summary = new(
            ContractConstants.CliSummarySchemaId, "1", output.RunId, outcome,
            outcome switch
            {
                "completed" or "completed-with-gaps" => 0,
                "cancelled" => (int)CliExitCode.Cancelled,
                "limit-reached" => (int)CliExitCode.LimitReached,
                _ => (int)CliExitCode.Failed,
            },
            new TypedOutputCountsContract(
                output.Observations.Count, output.DeterministicResults.Count, output.ExternalClaims.Count,
                output.ApplicationLinks.Count, output.DiscoveryLeads.Count, output.ModelProposals.Count,
                output.ProposalAdmissions.Count, output.Candidates.Count, output.Hypotheses.Count,
                output.Findings.Count, output.Recommendations.Count, output.SupportedCases.Count,
                output.LeadOnlyCases.Count, output.Abstentions.Count, output.InvalidInputs.Count,
                output.CoverageGaps.Count, output.Failures.Count, output.DocumentationRevisions.Count,
                output.Passages.Count, output.CandidateDecisions.Count, output.ReconciliationAssessments.Count,
                output.LineageEvents.Count),
            new CoverageStateCountsContract(
                output.AnalyzerCoverage.Count(item => item.Status == "completed"),
                output.AnalyzerCoverage.Count(item => item.Status == "completed-with-gaps"),
                output.AnalyzerCoverage.Count(item => item.Status == "failed"),
                output.AnalyzerCoverage.Count(item => item.Status == "skipped-by-configuration"),
                output.AnalyzerCoverage.Count(item => item.Status == "skipped-by-limit"),
                output.AnalyzerCoverage.Count(item => item.Status == "unsupported")),
            Duration(), new CliCostContract(0, 0, 0, 0, 0, 0, 0, false),
            "no-readiness-evaluation", true);
        CliSummaryDocumentContractInvariants.Validate(summary);
        return summary;
    }

    private static List<ReplayDependencyNodeContract> BuildDependencies(
        AnalysisV1WorkAssignment assignment,
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases,
        string documentationSha,
        string candidateSha,
        string findingSha)
    {
        List<ReplayDependencyNodeContract> result = [];
        void Add(string id, string kind, string version, string fingerprint, Slice5ResultState state = Slice5ResultState.Present) =>
            result.Add(new ReplayDependencyNodeContract(new OpaqueId(id), kind, ContractVersion.Parse(version), new Sha256Fingerprint(fingerprint), state));
        Add(assignment.ExecutionInput.ExecutionInputId.Value, "execution-input", assignment.ExecutionInput.SchemaVersion.ToString(),
            Hash(AnalysisExecutionInputJsonCodec.Serialize(assignment.ExecutionInput)));
        foreach ((string kind, ArtifactReferenceContract value) in References(assignment.ExecutionInput))
        {
            // Retained documentation is also emitted below as the typed phase output.
            // Keep one replay node for that identity instead of assigning both the
            // generic source-input kind and the authoritative documentation kind.
            if (value.ArtifactId == documentation.PayloadId)
            {
                if (value.ArtifactVersion != documentation.SchemaVersion
                    || value.Fingerprint.Value != documentationSha
                    || value.Availability != "retained")
                {
                    throw new AnalysisIdentityDriftException(
                        $"Replay dependency identity '{value.ArtifactId.Value}' resolves to drifted retained metadata.");
                }
                continue;
            }
            Add(value.ArtifactId.Value, kind, value.ArtifactVersion.ToString(), value.Fingerprint.Value,
                value.Availability == "retained" ? Slice5ResultState.Present : Slice5ResultState.Unavailable);
        }
        Add(documentation.PayloadId.Value, "documentation-evidence", documentation.SchemaVersion.ToString(), documentationSha);
        Add(candidates.PayloadId.Value, "candidate-analysis", candidates.SchemaVersion.ToString(), candidateSha);
        Add(findingCases.PayloadId.Value, "finding-case", findingCases.SchemaVersion.ToString(), findingSha);
        Add(candidates.PolicyId.Value, "candidate-policy", "1.0.0", candidates.PolicyFingerprint.Value);
        Add(candidates.ThresholdId.Value, "candidate-threshold", "1.0.0", candidates.ThresholdFingerprint.Value);
        Add(candidates.LimitId.Value, "candidate-limit", "1.0.0", candidates.LimitFingerprint.Value);
        Add(findingCases.PromotionPolicyId.Value, "promotion-policy", findingCases.PromotionPolicyVersion.ToString(),
            Hash(Encoding.UTF8.GetBytes(findingCases.PromotionPolicyId.Value + "|" + findingCases.PromotionPolicyVersion)));
        Add(findingCases.ReconciliationPolicyId.Value, "reconciliation-policy", findingCases.ReconciliationPolicyVersion.ToString(),
            Hash(Encoding.UTF8.GetBytes(findingCases.ReconciliationPolicyId.Value + "|" + findingCases.ReconciliationPolicyVersion)));
        Add("fixture-seed-" + assignment.ExecutionInput.Seed, "fixture-seed", "1.0.0",
            Hash(Encoding.UTF8.GetBytes(assignment.ExecutionInput.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        foreach (AnalysisPhaseExecution phase in assignment.PhaseExecutions)
        {
            Add("phase-" + Hash(Encoding.UTF8.GetBytes(phase.PhaseId + "|" + phase.InputFingerprint))[..32],
                "analysis-phase", "1.0.0", phase.InputFingerprint);
        }
        return result.GroupBy(item => item.DependencyId).Select(group =>
        {
            ReplayDependencyNodeContract first = group.First();
            if (group.Any(item => item != first))
            {
                throw new AnalysisIdentityDriftException(
                    $"Replay dependency identity '{group.Key.Value}' resolves to drifted retained metadata.");
            }
            return first;
        }).ToList();
    }

    internal static IReadOnlyList<ReplayDependencyNodeContract> BuildDependenciesForVerification(
        AnalysisV1WorkAssignment assignment,
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases) =>
        BuildDependencies(assignment, documentation, candidates, findingCases,
            assignment.DocumentationEvidence.Sha256,
            assignment.CandidateAnalysis.Sha256,
            assignment.FindingCase.Sha256);

    private static IEnumerable<(string Kind, ArtifactReferenceContract Value)> References(AnalysisExecutionInputContract value)
    {
        yield return ("analysis-context", value.AnalysisContext);
        yield return ("installation-snapshot", value.InstallationSnapshot);
        yield return ("bethesda-semantic-input", value.BethesdaSemanticInput);
        foreach (ArtifactReferenceContract item in value.SourceInputs)
        {
            yield return ("source-input", item);
        }
        foreach (ArtifactReferenceContract item in value.AnalyzerDeclarations)
        {
            yield return ("analyzer-declaration", item);
        }
        yield return ("effective-configuration", value.EffectiveConfiguration);
        yield return ("resolved-input-manifest", value.ResolvedInputManifest);
    }

    internal static string SemanticFingerprintForVerification(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases) =>
        SemanticFingerprint(documentation, candidates, findingCases, CancellationToken.None);

    private static string SemanticFingerprint(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases,
        CancellationToken cancellationToken) =>
        Hash(SemanticProjection(documentation, candidates, findingCases, cancellationToken));

    internal static byte[] SemanticProjectionForVerification(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases) =>
        SemanticProjection(documentation, candidates, findingCases, CancellationToken.None);

    private static byte[] SemanticProjection(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases,
        CancellationToken cancellationToken)
    {
        IOrderedEnumerable<T> Sorted<T>(IEnumerable<T> values) =>
            values.Select(item =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return item;
                })
                .OrderBy(item => JsonSerializer.Serialize(item, Slice5ContractJsonCodec.JsonOptions), StringComparer.Ordinal);

        string Anchor(object value) => Hash(JsonSerializer.SerializeToUtf8Bytes(
            value, Slice5ContractJsonCodec.JsonOptions));
        object SemanticEnvelope(IdentityEnvelopeContract value) => new
        {
            value.AnalyzerFamily,
            value.AnalyzerVersion,
            value.SemanticContractVersion,
            value.IdentityContractVersion,
            participants = value.ParticipantsAndRoles.OrderBy(item => item.Key, StringComparer.Ordinal),
            value.CausalCondition,
            value.AffectedLocus,
            applicability = value.ApplicabilityPredicates.OrderBy(item => item, StringComparer.Ordinal),
        };
        HashSet<OpaqueId> deliveredRootCandidates = candidates.Decisions.Count == 0
            ? []
            : candidates.Decisions
                .Select(item => item.DependencyIds
                    .Where(id => id.Value.StartsWith("candidate-delivered-input-", StringComparison.Ordinal))
                    .ToHashSet())
                .Aggregate((left, right) =>
                {
                    left.IntersectWith(right);
                    return left;
                });
        OpaqueId? deliveredRootId = deliveredRootCandidates.Count == 1
            ? deliveredRootCandidates.Single()
            : null;
        string SemanticDependency(OpaqueId id) => id == deliveredRootId
            ? "candidate-delivered-input"
            : id.Value;
        Dictionary<OpaqueId, string> revisionAnchors = documentation.Revisions.ToDictionary(
            item => item.RevisionId,
            item => Anchor(new
            {
                item.SourceId,
                item.SourceKind,
                item.SourceRevision,
                item.ByteFingerprint,
                item.ByteLength,
                item.SupplyingSnapshotId,
                item.RetentionState,
                item.ReplayState,
            }));
        Dictionary<OpaqueId, string> importAnchors = documentation.Imports.ToDictionary(
            item => item.ImportId,
            item => Anchor(new
            {
                revision = revisionAnchors[item.RevisionId],
                item.Mode,
                item.ExtractorId,
                item.LlmInvolvement,
                item.LlmOperation,
                boundaries = item.Boundaries.OrderBy(boundary => boundary.BoundaryId, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> passageAnchors = documentation.Passages.ToDictionary(
            item => item.PassageId,
            item => Anchor(new
            {
                revision = revisionAnchors[item.RevisionId],
                item.Utf8StartOffset,
                item.Utf8EndOffset,
                item.PassageFingerprint,
                item.State,
            }));
        Dictionary<OpaqueId, string> claimAnchors = documentation.Claims.ToDictionary(
            item => item.ClaimId,
            item => Anchor(new
            {
                producingImport = importAnchors[item.ProducingImportId],
                passage = passageAnchors[item.PassageId],
                item.Kind,
                item.ExactText,
                item.Conditions,
                item.Authority,
                item.Applicability,
                item.ClassificationRole,
                contradictions = item.ContradictingEvidenceIds.OrderBy(id => id.Value),
            }));
        Dictionary<OpaqueId, string> applicationAnchors = documentation.Applications.ToDictionary(
            item => item.ApplicationId,
            item => Anchor(new
            {
                claim = claimAnchors[item.ClaimId],
                item.SubjectId,
                item.SubjectType,
                item.Applicability,
                evidence = item.EvidenceIds.Select(id => claimAnchors.GetValueOrDefault(id, "external:" + id.Value))
                    .OrderBy(value => value, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> decisionAnchors = candidates.Decisions.ToDictionary(
            item => item.DecisionId,
            item => Anchor(new
            {
                item.PopulationMemberId,
                item.SourceFactId,
                item.Lane,
                item.Disposition,
                participants = item.Participants.OrderBy(value => value.ParticipantId.Value),
                item.JoinKind,
                path = item.Path,
                item.Rationale,
                evidence = item.EvidenceIds.OrderBy(id => id.Value),
                item.AdmissionIndependentOfScore,
                item.OptionalRank,
                item.AnalyzerId,
                item.PolicyId,
                item.ThresholdId,
                item.LimitId,
                dependencies = item.DependencyIds.Select(SemanticDependency)
                    .OrderBy(id => id, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> candidateAnchors = candidates.Candidates.ToDictionary(
            item => item.CandidateId,
            item => Anchor(new
            {
                decision = decisionAnchors[item.DecisionId],
                item.State,
                item.CausalExplanation,
                supporting = item.SupportingEvidenceIds.OrderBy(id => id.Value),
                contradicting = item.ContradictingEvidenceIds.OrderBy(id => id.Value),
                item.MissingInformation,
                item.Confidence,
                item.ThresholdId,
            }));
        Dictionary<OpaqueId, string> hypothesisAnchors = candidates.Hypotheses.ToDictionary(
            item => item.HypothesisId,
            item => Anchor(new
            {
                candidate = candidateAnchors[item.CandidateId],
                item.State,
                item.ProposedExplanation,
                item.PredictedImpact,
                supporting = item.SupportingEvidenceIds.OrderBy(id => id.Value),
                contradicting = item.ContradictingEvidenceIds.OrderBy(id => id.Value),
                item.MissingInformation,
                item.Confidence,
                item.ThresholdId,
            }));
        Dictionary<OpaqueId, string> occurrenceAnchors = findingCases.Findings
            .ToDictionary(item => item.FindingOccurrenceId, item => "finding:" + Anchor(new
            {
                candidate = candidateAnchors[item.CandidateId],
                hypothesis = hypothesisAnchors[item.HypothesisId],
                item.Conclusion,
                item.Severity,
                item.Confidence,
            }));
        foreach (Slice5CaseContract item in findingCases.Cases)
        {
            occurrenceAnchors.Add(item.CaseOccurrenceId, "case:" + Anchor(new
            {
                item.Kind,
                candidates = item.CandidateIds.Select(id => candidateAnchors[id]).OrderBy(value => value, StringComparer.Ordinal),
                hypotheses = item.HypothesisIds.Select(id => hypothesisAnchors[id]).OrderBy(value => value, StringComparer.Ordinal),
                item.SharedCause,
                item.AffectsReadiness,
            }));
        }
        string Occurrence(OpaqueId? id) => id is null ? "none"
            : occurrenceAnchors.GetValueOrDefault(id, "external:" + id.Value);
        string Subject(OpaqueId id) => occurrenceAnchors.GetValueOrDefault(id,
            candidateAnchors.GetValueOrDefault(id,
                hypothesisAnchors.GetValueOrDefault(id, "external:" + id.Value)));
        string Evidence(OpaqueId id) => claimAnchors.GetValueOrDefault(id, Subject(id));
        Dictionary<OpaqueId, string> abstentionAnchors = candidates.Abstentions.ToDictionary(
            item => item.AbstentionId,
            item => Anchor(new
            {
                decision = decisionAnchors[item.DecisionId],
                candidate = item.CandidateId is null ? "none" : candidateAnchors[item.CandidateId],
                item.AnalyzerId,
                item.Reason,
                item.RequiredInformation,
            }));
        Dictionary<OpaqueId, string> candidateGapAnchors = candidates.Gaps.ToDictionary(
            item => item.GapId,
            item => Anchor(new
            {
                decision = decisionAnchors[item.DecisionId],
                item.PopulationId,
                item.State,
                item.Reason,
                item.MissingCapabilityOrInformation,
            }));
        Dictionary<OpaqueId, string> taxonomyAnchors = findingCases.TaxonomyAssignments.ToDictionary(
            item => item.AssignmentId,
            item => Anchor(new
            {
                item.TaxonomyId,
                item.TaxonomyVersion,
                item.Axis,
                item.Facet,
                item.Code,
                item.Applicability,
                subject = Subject(item.SubjectId),
                item.SubjectType,
                item.Role,
                evidence = item.EvidenceIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
                applicabilityConditions = item.ApplicabilityConditionIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
                item.ConfidenceAssessmentId,
                item.AnalyzerOrAdjudicatorId,
                item.Reason,
            }));
        Dictionary<OpaqueId, string> findingAbstentionAnchors = findingCases.Abstentions.ToDictionary(
            item => item.AbstentionId,
            item => Anchor(new
            {
                hypothesis = hypothesisAnchors[item.HypothesisId],
                item.Reason,
                item.RequiredInformation,
                evidence = item.EvidenceIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> findingGapAnchors = findingCases.Gaps.ToDictionary(
            item => item.GapId,
            item => Anchor(new
            {
                item.PopulationId,
                item.StageId,
                item.State,
                item.ReplayEffect,
                item.ConclusionEffect,
                item.Reason,
                item.MissingCapabilityOrInformation,
                evidence = item.EvidenceIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> coverageFailureAnchors = findingCases.CoverageFailures.ToDictionary(
            item => item.FailureId,
            item => Anchor(new { item.AnalyzerId, item.FailureCode, item.Message, item.Retryable }));
        Dictionary<OpaqueId, OpaqueId> exactContinuationAliases = findingCases.ReconciliationAssessments
            .Where(item => item.PriorOccurrenceId is not null && item.CurrentOccurrenceId is not null
                && item.Outcome == ReconciliationOutcome.ExactContinuation
                && item.Gates.Causal == ReconciliationGateState.ProvenEquivalent
                && item.Gates.Applicability == ReconciliationGateState.ProvenEquivalent
                && item.Gates.Dependency == ReconciliationGateState.ProvenEquivalent
                && item.Gates.Producer == ReconciliationGateState.ProvenEquivalent)
            .ToDictionary(item => item.PriorOccurrenceId!, item => item.CurrentOccurrenceId!);
        string SemanticOccurrence(OpaqueId? id)
        {
            if (id is not null && exactContinuationAliases.TryGetValue(id, out OpaqueId? current))
            {
                id = current;
            }
            return Occurrence(id);
        }
        string CandidateGraphNode(string kind, OpaqueId id) => kind switch
        {
            "candidate" => candidateAnchors[id],
            "candidate-decision" => decisionAnchors[id],
            "hypothesis" => hypothesisAnchors[id],
            "abstention" => abstentionAnchors[id],
            "gap" => candidateGapAnchors[id],
            "candidate-analysis-root" or "execution-input-binding" or "dependency-closure" => kind,
            "analyzer-declaration-binding" or "policy-binding" or "threshold-binding" or "limit-binding" => kind,
            "dependency" => SemanticDependency(id),
            _ => Evidence(id),
        };

        var projection = new
        {
            documentation = new
            {
                revisions = Sorted(documentation.Revisions.Select(item => new
                {
                    anchor = revisionAnchors[item.RevisionId],
                    item.SourceId,
                    item.SourceKind,
                    item.SourceRevision,
                    item.ByteFingerprint,
                    item.ByteLength,
                    item.SupplyingSnapshotId,
                    item.RetentionState,
                    item.ReplayState,
                })),
                imports = Sorted(documentation.Imports.Select(item => new
                {
                    anchor = importAnchors[item.ImportId],
                    revision = revisionAnchors[item.RevisionId],
                    reusedImport = item.ReusedImportId is null ? "none"
                        : importAnchors.GetValueOrDefault(item.ReusedImportId, "retained-prior-import"),
                    item.Mode,
                    item.ExtractorId,
                    item.LlmInvolvement,
                    item.LlmOperation,
                    boundaries = Sorted(item.Boundaries.Select(boundary => new { boundary.BoundaryId, boundary.State, boundary.Reason })),
                })),
                passages = Sorted(documentation.Passages.Select(item => new
                {
                    anchor = passageAnchors[item.PassageId],
                    revision = revisionAnchors[item.RevisionId],
                    item.Utf8StartOffset,
                    item.Utf8EndOffset,
                    item.PassageFingerprint,
                    item.State,
                })),
                claims = Sorted(documentation.Claims.Select(item => new
                {
                    anchor = claimAnchors[item.ClaimId],
                    producingImport = importAnchors[item.ProducingImportId],
                    passage = passageAnchors[item.PassageId],
                    item.Kind,
                    item.ExactText,
                    item.Conditions,
                    item.Authority,
                    item.Applicability,
                    item.ClassificationRole,
                    contradictingEvidence = Sorted(item.ContradictingEvidenceIds.Select(Evidence)),
                })),
                applications = Sorted(documentation.Applications.Select(item => new
                {
                    claim = claimAnchors[item.ClaimId],
                    item.SubjectId,
                    item.SubjectType,
                    item.Applicability,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                purposes = Sorted(documentation.PurposeAssignments.Select(item => new
                {
                    item.TaxonomyId,
                    item.TaxonomyVersion,
                    item.Axis,
                    item.Facet,
                    item.Code,
                    item.Applicability,
                    item.SubjectId,
                    item.SubjectType,
                    item.Role,
                    claim = claimAnchors[item.ClaimId],
                    application = applicationAnchors[item.ApplicationId],
                    applicabilityConditions = Sorted(item.ApplicabilityConditionIds.Select(Evidence)),
                    item.AnalyzerOrAdjudicatorId,
                    item.Reason,
                })),
                deletionReceipts = Sorted(documentation.DeletionReceipts.Select(item => new
                {
                    revision = revisionAnchors[item.RevisionId],
                    item.DeletedBodyFingerprint,
                    deletedPassages = Sorted(item.DeletedPassageIds.Select(id =>
                        passageAnchors.GetValueOrDefault(id, "deleted:" + id.Value))),
                    retainedPayloads = Sorted(item.IndependentlyRetainedPayloadIds.Select(id => id.Value)),
                    item.ReplayEffect,
                    item.Reason,
                })),
                gaps = Sorted(documentation.Gaps.Select(item => new
                {
                    revision = revisionAnchors[item.RevisionId],
                    claim = item.ClaimId is null ? "none" : claimAnchors[item.ClaimId],
                    application = item.ApplicationId is null ? "none" : applicationAnchors[item.ApplicationId],
                    item.Kind,
                    item.ReplayEffect,
                    item.Reason,
                })),
                failures = Sorted(documentation.Failures.Select(item => new
                {
                    item.FailureCode,
                    item.Message,
                    item.Retryable,
                })),
            },
            candidates = new
            {
                candidates.AnalyzerId,
                candidates.PopulationId,
                candidates.PopulationDenominator,
                candidates.PolicyFingerprint,
                candidates.ThresholdFingerprint,
                candidates.LimitFingerprint,
                candidates.AnalyzerSetFingerprint,
                candidates.PolicyDescriptors,
                candidates.ThresholdDescriptors,
                candidates.LimitDescriptors,
                analyzerBindings = Sorted(candidates.AnalyzerBindings.Select(item => new
                {
                    item.AnalyzerId,
                    item.AnalyzerFamily,
                    item.AnalyzerVersion,
                    item.SemanticContractVersion,
                    item.IdentityContractVersion,
                    item.RulesetVersion,
                    item.DeclarationFingerprint,
                    item.CanonicalDeclarationJson,
                })),
                decisions = Sorted(candidates.Decisions.Select(item => new
                {
                    anchor = decisionAnchors[item.DecisionId],
                    item.PopulationMemberId,
                    item.SourceFactId,
                    item.Lane,
                    item.Disposition,
                    participants = Sorted(item.Participants.Select(participant => new { participant.ParticipantId, participant.Role })),
                    item.JoinKind,
                    path = item.Path,
                    item.Rationale,
                    evidence = Sorted(item.EvidenceIds),
                    item.AdmissionIndependentOfScore,
                    item.OptionalRank,
                    item.AnalyzerId,
                    item.PolicyId,
                    item.ThresholdId,
                    item.LimitId,
                    dependencies = Sorted(item.DependencyIds.Select(SemanticDependency)),
                })),
                entries = Sorted(candidates.Candidates.Select(item => new
                {
                    anchor = candidateAnchors[item.CandidateId],
                    decision = decisionAnchors[item.DecisionId],
                    item.State,
                    item.CausalExplanation,
                    supportingEvidence = Sorted(item.SupportingEvidenceIds),
                    contradictingEvidence = Sorted(item.ContradictingEvidenceIds),
                    item.MissingInformation,
                    item.Confidence,
                    item.ThresholdId,
                    hypothesis = item.HypothesisId is null ? null : hypothesisAnchors[item.HypothesisId],
                    abstention = item.AbstentionId is null ? null : abstentionAnchors[item.AbstentionId],
                })),
                hypotheses = Sorted(candidates.Hypotheses.Select(item => new
                {
                    anchor = hypothesisAnchors[item.HypothesisId],
                    candidate = candidateAnchors[item.CandidateId],
                    item.State,
                    item.ProposedExplanation,
                    item.PredictedImpact,
                    supportingEvidence = Sorted(item.SupportingEvidenceIds),
                    contradictingEvidence = Sorted(item.ContradictingEvidenceIds),
                    item.MissingInformation,
                    item.Confidence,
                    item.ThresholdId,
                })),
                abstentions = Sorted(candidates.Abstentions.Select(item => new
                {
                    anchor = abstentionAnchors[item.AbstentionId],
                    decision = decisionAnchors[item.DecisionId],
                    candidate = item.CandidateId is null ? "none" : candidateAnchors[item.CandidateId],
                    item.AnalyzerId,
                    item.Reason,
                    item.RequiredInformation,
                })),
                gaps = Sorted(candidates.Gaps.Select(item => new
                {
                    decision = decisionAnchors[item.DecisionId],
                    item.PopulationId,
                    item.State,
                    item.Reason,
                    item.MissingCapabilityOrInformation,
                })),
                failures = Sorted(candidates.Failures.Select(item => new
                {
                    item.AnalyzerId,
                    populationMembers = Sorted(item.PopulationMemberIds),
                    item.FailureCode,
                    item.Message,
                    item.Retryable,
                })),
                dependencyEdges = Sorted(candidates.DependencyEdges.Select(item => new
                {
                    item.FromKind,
                    from = CandidateGraphNode(item.FromKind, item.FromId),
                    item.ToKind,
                    to = CandidateGraphNode(item.ToKind, item.ToId),
                    item.EdgeKind,
                })),
                candidates.Counts,
            },
            findings = new
            {
                findingCases.PromotionPolicyId,
                findingCases.PromotionPolicyVersion,
                findingCases.ReconciliationPolicyId,
                findingCases.ReconciliationPolicyVersion,
                promotion = Sorted(findingCases.PromotionAssessments.Select(item => new
                {
                    hypothesis = hypothesisAnchors[item.HypothesisId],
                    item.StatePresent,
                    item.ConfidenceAtLeastPlausible,
                    item.HasSupportingEvidence,
                    item.HasNoDefeatingContradictions,
                    item.HasNoMissingInformation,
                    item.SeverityClosed,
                    item.IdentityClosed,
                    item.ConclusionAvailable,
                    item.LeadEligibleState,
                    item.Outcome,
                    item.Reasons,
                })),
                abstentions = Sorted(findingCases.Abstentions.Select(item => new
                {
                    hypothesis = hypothesisAnchors[item.HypothesisId],
                    item.Reason,
                    item.RequiredInformation,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                items = Sorted(findingCases.Findings.Select(item => new
                {
                    occurrence = Occurrence(item.FindingOccurrenceId),
                    candidate = candidateAnchors[item.CandidateId],
                    hypothesis = hypothesisAnchors[item.HypothesisId],
                    item.Conclusion,
                    item.Severity,
                    item.Confidence,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                    identity = SemanticEnvelope(item.IdentityEnvelope),
                    caseIdentity = SemanticEnvelope(item.CaseIdentityEnvelope),
                    taxonomyAssignments = Sorted(item.TaxonomyAssignmentIds.Select(id => taxonomyAnchors[id])),
                    supersedes = item.SupersedesOccurrenceId is not null
                        && SemanticOccurrence(item.SupersedesOccurrenceId) == Occurrence(item.FindingOccurrenceId)
                            ? "none" : SemanticOccurrence(item.SupersedesOccurrenceId),
                })),
                recommendations = Sorted(findingCases.Recommendations.Select(item => new
                {
                    item.Kind,
                    finding = SemanticOccurrence(item.FindingOccurrenceId),
                    abstention = item.AbstentionId is null ? "none" : findingAbstentionAnchors[item.AbstentionId],
                    lead = item.LeadHypothesisId is null ? null : hypothesisAnchors[item.LeadHypothesisId],
                    item.Action,
                    item.Uncertainty,
                    item.Reversibility,
                    item.Risks,
                    item.Verification,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                cases = Sorted(findingCases.Cases.Select(item => new
                {
                    occurrence = Occurrence(item.CaseOccurrenceId),
                    item.Kind,
                    findings = Sorted(item.FindingOccurrenceIds.Select(SemanticOccurrence)),
                    candidates = Sorted(item.CandidateIds.Select(id => candidateAnchors[id])),
                    hypotheses = Sorted(item.HypothesisIds.Select(id => hypothesisAnchors[id])),
                    item.SharedCause,
                    causeProof = Sorted(item.CauseProofEvidenceIds.Select(Evidence)),
                    identity = SemanticEnvelope(item.IdentityEnvelope),
                    supersedes = item.SupersedesOccurrenceId is not null
                        && SemanticOccurrence(item.SupersedesOccurrenceId) == Occurrence(item.CaseOccurrenceId)
                            ? "none" : SemanticOccurrence(item.SupersedesOccurrenceId),
                    item.AffectsReadiness,
                })),
                reconciliation = Sorted(findingCases.ReconciliationAssessments
                    .Where(item => !(item.PriorOccurrenceId is null && item.Outcome == ReconciliationOutcome.NewDistinct)
                        && (item.PriorOccurrenceId is null
                            || !exactContinuationAliases.ContainsKey(item.PriorOccurrenceId)))
                    .Select(item => new
                    {
                        item.SubjectKind,
                        prior = SemanticOccurrence(item.PriorOccurrenceId),
                        current = SemanticOccurrence(item.CurrentOccurrenceId),
                        item.Gates,
                        item.Outcome,
                        item.Gaps,
                        considered = Sorted(item.ConsideredOccurrenceIds.Select(SemanticOccurrence)),
                        proof = Sorted(item.ProofEvidenceIds.Select(Evidence)),
                        item.PolicyVersion,
                        item.Mechanism,
                        item.ActorId,
                        item.VisibleByDefault,
                    })),
                lineage = Sorted(findingCases.LineageEvents
                    .Where(item => !(item.PredecessorIds.Count == 1 && item.SuccessorIds.Count == 1
                        && SemanticOccurrence(item.PredecessorIds[0]) == SemanticOccurrence(item.SuccessorIds[0])))
                    .Select(item => new
                    {
                        item.Kind,
                        predecessors = Sorted(item.PredecessorIds.Select(SemanticOccurrence)),
                        successors = Sorted(item.SuccessorIds.Select(SemanticOccurrence)),
                        reconciliation = item.ReconciliationAssessmentId is null ? "none"
                        : findingCases.ReconciliationAssessments.Single(value => value.AssessmentId == item.ReconciliationAssessmentId).Outcome.ToString(),
                    })),
                taxonomy = Sorted(findingCases.TaxonomyAssignments.Select(item => new
                {
                    item.TaxonomyId,
                    item.TaxonomyVersion,
                    item.Axis,
                    item.Facet,
                    item.Code,
                    item.Applicability,
                    item.SubjectType,
                    item.Role,
                    anchor = taxonomyAnchors[item.AssignmentId],
                    subject = Subject(item.SubjectId),
                    item.ConfidenceAssessmentId,
                    supersedes = Sorted(item.SupersedesAssignmentIds.Select(id => taxonomyAnchors.GetValueOrDefault(id, "external:" + id.Value))),
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                    applicabilityConditions = Sorted(item.ApplicabilityConditionIds.Select(Evidence)),
                    item.AnalyzerOrAdjudicatorId,
                    item.Reason,
                })),
                taxonomyProjections = Sorted(findingCases.TaxonomyProjections.Select(item => new
                {
                    source = taxonomyAnchors[item.SourceAssignmentId],
                    projected = taxonomyAnchors[item.ProjectedAssignmentId],
                    item.MappingAuthorityId,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                    item.Reason,
                })),
                coverage = Sorted(findingCases.Coverage.Select(item => new
                {
                    item.AnalyzerId,
                    item.PopulationId,
                    item.DenominatorLabel,
                    item.Denominator,
                    item.CompletedCount,
                    item.State,
                    item.TaxonomyId,
                    item.TaxonomyVersion,
                    assignments = Sorted(item.TaxonomyAssignmentIds.Select(id => taxonomyAnchors[id])),
                    gaps = Sorted(item.GapIds.Select(id => findingGapAnchors[id])),
                    failures = Sorted(item.FailureIds.Select(id => coverageFailureAnchors[id])),
                    exclusions = Sorted(item.Exclusions.Select(exclusion => new
                    {
                        member = Subject(exclusion.MemberId),
                        exclusion.Reason,
                        exclusion.State,
                    })),
                    members = Sorted(item.MemberResults.Select(member => new
                    {
                        member = Subject(member.MemberId),
                        member.State,
                        member.Reason,
                        member.MissingCapabilityOrInformation,
                        failure = member.FailureId is null ? "none" : coverageFailureAnchors[member.FailureId],
                        gap = member.GapId is null ? "none" : findingGapAnchors[member.GapId],
                        taxonomy = Sorted(member.TaxonomyAssignmentIds.Select(id => taxonomyAnchors[id])),
                    })),
                })),
                coverageFailures = Sorted(findingCases.CoverageFailures.Select(item => new
                {
                    item.AnalyzerId,
                    item.FailureCode,
                    item.Message,
                    item.Retryable,
                })),
                gaps = Sorted(findingCases.Gaps.Select(item => new
                {
                    item.PopulationId,
                    item.StageId,
                    item.State,
                    item.ReplayEffect,
                    item.ConclusionEffect,
                    item.Reason,
                    item.MissingCapabilityOrInformation,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                boundaries = Sorted(findingCases.Boundaries.Select(item => new { item.BoundaryId, item.State, item.Reason })),
                findingCases.PublicationClaimBoundary,
            },
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(projection, Slice5ContractJsonCodec.JsonOptions);
        cancellationToken.ThrowIfCancellationRequested();
        return bytes;
    }

    private static ReplayOutputContract ReplayOutput(RetainedAnalysisPayloadSeal seal, OpaqueId artifactId, string sha) =>
        new(artifactId, seal.SchemaId, ContractVersion.Parse(seal.SchemaVersion), new Sha256Fingerprint(sha), new Sha256Fingerprint(sha));

    private static AnalysisPublishedArtifact Published(RetainedAnalysisPayloadSeal seal, string artifactId, string kind, string closure) =>
        new(artifactId, kind, seal.SchemaId, seal.SchemaVersion, 1, "present", seal.Sha256, seal.ByteLength,
            StableId("provenance", artifactId), closure);

    private static TypedArtifactDocumentContract Typed(
        string id, string type, string state, ArtifactReferenceDocumentContract payload, ArtifactProvenanceDocumentContract provenance) =>
        new(id, 1, type, state, payload, provenance);

    private static ArtifactReferenceDocumentContract Reference(RetainedAnalysisPayloadSeal value) =>
        new(value.PayloadId, value.SchemaVersion, value.Sha256, "retained");

    private static ArtifactReferenceDocumentContract Reference(ArtifactReferenceContract value) =>
        new(value.ArtifactId.Value, value.ArtifactVersion.ToString(), value.Fingerprint.Value, value.Availability);

    private static void ValidateSeal(RetainedAnalysisPayloadSeal seal, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != seal.ByteLength || !StringComparer.Ordinal.Equals(Hash(bytes), seal.Sha256))
        {
            throw new InvalidDataException($"Retained payload '{seal.PayloadId}' failed exact identity admission.");
        }
    }

    private static string DecisionState(CandidateDecisionDisposition state) => state switch
    {
        CandidateDecisionDisposition.CandidateAdmitted => "present",
        CandidateDecisionDisposition.ResolvedNegative => "resolved-negative",
        CandidateDecisionDisposition.InvalidInput => "invalid-input",
        CandidateDecisionDisposition.Limited => "limit-reached",
        CandidateDecisionDisposition.Deferred or CandidateDecisionDisposition.Unprocessed => "partial",
        _ => Kebab(state),
    };

    private static string ClaimState(ClaimApplicabilityState state) => state switch
    {
        ClaimApplicabilityState.Applicable => "present",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "rejected",
        _ => throw new InvalidDataException("Claim applicability is unspecified."),
    };

    internal static long CountOutputItems(RunOutputContract output) => checked(
        output.Observations.Count
        + output.DeterministicResults.Count
        + output.ExternalClaims.Count
        + output.ApplicationLinks.Count
        + output.DiscoveryLeads.Count
        + output.ModelProposals.Count
        + output.ProposalAdmissions.Count
        + output.Candidates.Count
        + output.Hypotheses.Count
        + output.Findings.Count
        + output.Recommendations.Count
        + output.SupportedCases.Count
        + output.LeadOnlyCases.Count
        + output.Abstentions.Count
        + output.InvalidInputs.Count
        + output.CoverageGaps.Count
        + output.Failures.Count
        + output.DocumentationRevisions.Count
        + output.Passages.Count
        + output.CandidateDecisions.Count
        + output.ReconciliationAssessments.Count
        + output.LineageEvents.Count
        + output.TaxonomyAssignments.Count
        + output.AnalyzerCoverage.Count);

    private static string TerminalState(AnalysisTerminalOutcome value) => value switch
    {
        AnalysisTerminalOutcome.Completed => "completed",
        AnalysisTerminalOutcome.CompletedWithGaps => "completed-with-gaps",
        AnalysisTerminalOutcome.Cancelled => "cancelled",
        AnalysisTerminalOutcome.LimitReached => "limit-reached",
        _ => "failed",
    };

    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
    private static string MissingDependencyGapId(string dependencyId) =>
        StableId("missing-dependency-gap", dependencyId);
    private static string Kebab<T>(T value) where T : struct, Enum => JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string StableId(string kind, params string[] parts) =>
        kind + "-" + Hash(Encoding.UTF8.GetBytes(string.Join('\n', parts)))[..32];
}

public sealed class AnalysisInputAdmissionException(string message, Exception innerException)
    : Exception(message, innerException);
