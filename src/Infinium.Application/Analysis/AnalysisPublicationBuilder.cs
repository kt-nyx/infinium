using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;


public static partial class AnalysisPublicationBuilder
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
            AnalysisExecutionContractInvariants.Validate(assignment.ExecutionInput);
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
                throw new InvalidDataException("Documentation, candidate, and finding/case aggregate identities do not bind the admitted analysis-v1 execution input.");
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
        string compositionFingerprint = assignment.AnalysisComposition is null
            ? "none"
            : AnalysisComposition.Fingerprint(assignment.AnalysisComposition);
        string baseSemanticFingerprint = SemanticFingerprint(documentation, candidates, findingCases, cancellationToken);
        string semanticFingerprint = assignment.AnalysisComposition is null
            ? baseSemanticFingerprint
            : Hash(Encoding.UTF8.GetBytes(baseSemanticFingerprint + "|" + compositionFingerprint));
        cancellationToken.ThrowIfCancellationRequested();
        string dependencyClosureId = StableId(
            "analysis-dependency-closure", runId, assignment.ExecutionInput.Mode.ToString(),
            assignment.ExecutionInput.PriorRunId?.Value ?? "none",
            assignment.ExecutionInput.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            documentationSha, candidateSha, findingSha, compositionFingerprint,
            candidates.ExecutionInputFingerprint.Value, candidates.AnalyzerSetFingerprint.Value,
            candidates.PolicyFingerprint.Value, candidates.ThresholdFingerprint.Value,
            candidates.LimitFingerprint.Value);

        List<ReplayDependencyNodeContract> dependencies = BuildDependencies(
            assignment, documentation, candidates, findingCases, documentationSha, candidateSha, findingSha);
        cancellationToken.ThrowIfCancellationRequested();
        OpaqueId[] missingDependencyIds = dependencies
            .Where(item => item.State != AnalysisResultState.Present)
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
                "Documentation provenance dependencies differ from the exact retained documentation input closure.");
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
        if (assignment.AnalysisComposition is not null)
        {
            foreach (AnalysisRetainedDependency dependency in assignment.AnalysisComposition.Dependencies)
            {
                dependencyEdges.Add(new ReplayDependencyEdgeContract(
                    assignment.ExecutionInput.ExecutionInputId, new OpaqueId(dependency.DependencyId)));
            }
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
                dependencyClosureId + "|" + semanticFingerprint))), AnalysisResultState.Present));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, assignment.ExecutionInput.ExecutionInputId));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, documentation.PayloadId));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, candidates.PayloadId));
        dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, findingCases.PayloadId));
        if (assignment.AnalysisComposition is not null)
        {
            foreach (AnalysisRetainedDependency dependency in assignment.AnalysisComposition.Dependencies)
            {
                dependencyEdges.Add(new ReplayDependencyEdgeContract(replayNode, new OpaqueId(dependency.DependencyId)));
            }
        }
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
        AnalysisReplayContractInvariants.Validate(replay);

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
            "The bounded analysis pipeline is a retained local-only execution path.");

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
        if (assignment.AnalysisComposition is not null)
        {
            byte[] compositionBytes = JsonSerializer.SerializeToUtf8Bytes(
                assignment.AnalysisComposition, ContractJsonSerializer.Options);
            // Retained serialized identity from the accepted M1 run-output v1 contract.
            artifacts.Add(new AnalysisPublishedArtifact(
                assignment.AnalysisComposition.EnvelopeId, "m1-slice9-composition",
                "infinium.internal.m1-slice9-composition/v1", "1", 1, "present",
                Hash(compositionBytes), compositionBytes.LongLength,
                StableId("provenance", assignment.AnalysisComposition.EnvelopeId), dependencyClosureId));
        }
        return new AnalysisPublicationBundle(
            replay, output, cli, boundaryReceipt, dependencyClosureId, semanticFingerprint, artifacts);
    }

    public static void ValidateAssignment(AnalysisV1WorkAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        AnalysisExecutionContractInvariants.Validate(assignment.ExecutionInput);
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
        if (assignment.AnalysisComposition is not null)
        {
            AnalysisComposition.Validate(assignment.AnalysisComposition);
        }
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
            Typed(item.RevisionId.Value, "documentation-revision", Kebab(item.RetentionState), documentPayload, DocumentProvenance("documentation-evidence"))).ToList();
        List<TypedArtifactDocumentContract> imports = documentation.Imports.Select(item =>
            Typed(item.ImportId.Value, "deterministic-result", "present", documentPayload,
                DocumentProvenance(item.ExtractorId.Value, [item.RevisionId.Value]))).ToList();
        List<TypedArtifactDocumentContract> passages = documentation.Passages.Select(item =>
            Typed(item.PassageId.Value, "passage", Kebab(item.State), documentPayload, DocumentProvenance("documentation-evidence"))).ToList();
        List<TypedArtifactDocumentContract> claims = documentation.Claims.Select(item =>
            Typed(item.ClaimId.Value, "external-claim", ClaimState(item.Applicability), documentPayload,
                DocumentProvenance("documentation-evidence",
                    [item.ProducingImportId.Value, item.PassageId.Value],
                    item.ContradictingEvidenceIds.Select(id => id.Value).ToArray()))).ToList();
        List<TypedArtifactDocumentContract> applications = documentation.Applications.Select(item =>
            Typed(item.ApplicationId.Value, "application-link", ClaimState(item.Applicability), documentPayload,
                DocumentProvenance("documentation-evidence",
                    [item.ClaimId.Value, .. item.EvidenceIds.Select(id => id.Value)]))).ToList();
        List<TypedArtifactDocumentContract> deletionReceipts = documentation.DeletionReceipts.Select(item =>
            Typed(item.ReceiptId.Value, "deterministic-result",
                item.ReplayEffect == ReplayState.CompleteClean ? "present" : "partial", documentPayload,
                DocumentProvenance("documentation-evidence",
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
                Typed(item.AbstentionId.Value, "abstention", "abstained", findingPayload, DocumentProvenance("finding-case-analysis"))))
            .GroupBy(item => item.ArtifactId, StringComparer.Ordinal).Select(item => item.First()).ToList();
        List<TypedArtifactDocumentContract> findings = findingCases.Findings.Select(item =>
            Typed(item.FindingOccurrenceId.Value, "finding", "present", findingPayload, DocumentProvenance("finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> recommendations = findingCases.Recommendations.Select(item =>
            Typed(item.RecommendationId.Value, "recommendation", "present", findingPayload, DocumentProvenance("finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> supportedCases = findingCases.Cases.Where(item => item.Kind == CaseOccurrenceKind.Supported).Select(item =>
            Typed(item.CaseOccurrenceId.Value, "supported-case", "present", findingPayload, DocumentProvenance("finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> leadCases = findingCases.Cases.Where(item => item.Kind == CaseOccurrenceKind.LeadOnly).Select(item =>
            Typed(item.CaseOccurrenceId.Value, "lead-only-case", "present", findingPayload, DocumentProvenance("finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> discoveryLeads = findingCases.Cases.Where(item => item.Kind == CaseOccurrenceKind.LeadOnly).Select(item =>
            Typed("lead-" + item.CaseOccurrenceId.Value, "discovery-lead", "present", findingPayload, DocumentProvenance("finding-case-analysis"))).ToList();
        List<TypedArtifactDocumentContract> gaps = documentation.Gaps.Select(item =>
            Typed(item.GapId.Value, "coverage-gap", "partial", documentPayload, DocumentProvenance("documentation-evidence")))
            .Concat(candidates.Gaps.Select(item =>
                Typed(item.GapId.Value, "coverage-gap", Kebab(item.State), candidatePayload, DocumentProvenance(candidates.AnalyzerId.Value))))
            .Concat(findingCases.Gaps.Select(item =>
                Typed(item.GapId.Value, "coverage-gap", "partial", findingPayload, DocumentProvenance("finding-case-analysis"))))
            .Concat(replay.MissingDependencyIds.Select(item =>
                Typed(MissingDependencyGapId(item.Value), "coverage-gap", "unavailable", candidatePayload,
                    DocumentProvenance("analysis-replay-dependency-audit", [item.Value]))))
            .GroupBy(item => item.ArtifactId, StringComparer.Ordinal).Select(item => item.First()).ToList();
        List<TypedArtifactDocumentContract> replayDependencyGaps = replay.MissingDependencyIds.Select(item =>
                Typed(MissingDependencyGapId(item.Value), "coverage-gap", "unavailable", candidatePayload,
                    DocumentProvenance("analysis-replay-dependency-audit", [item.Value])))
            .ToList();
        List<TypedArtifactDocumentContract> failures = documentation.Failures.Select(item =>
            Typed(item.FailureId.Value, "failure", "failed", documentPayload, DocumentProvenance("documentation-evidence")))
            .Concat(candidates.Failures.Select(item =>
                Typed(item.FailureId.Value, "failure", "failed", candidatePayload, DocumentProvenance(item.AnalyzerId.Value))))
            .Concat(findingCases.CoverageFailures.Select(item =>
                Typed(item.FailureId.Value, "failure", "failed", findingPayload, DocumentProvenance(item.AnalyzerId.Value))))
            .GroupBy(item => item.ArtifactId, StringComparer.Ordinal).Select(item => item.First()).ToList();
        List<TypedArtifactDocumentContract> reconciliations = findingCases.ReconciliationAssessments.Select(item =>
            Typed(item.AssessmentId.Value, "reconciliation-assessment", "present", findingPayload, DocumentProvenance(item.ActorId.Value))).ToList();
        List<TypedArtifactDocumentContract> lineage = findingCases.LineageEvents.Select(item =>
            Typed(item.EventId.Value, "lineage-event", "present", findingPayload, DocumentProvenance("finding-case-analysis"))).ToList();

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
        List<CoverageDocumentContract> composedCoverage = [];
        List<TaxonomyAssignmentDocumentContract> composedTaxonomy = [];
        if (assignment.AnalysisComposition is not null)
        {
            AnalysisComposition.Apply(
                assignment.AnalysisComposition, runId, collections, composedTaxonomy, composedCoverage);
        }
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
        taxonomy.AddRange(composedTaxonomy);
        List<CoverageDocumentContract> coverage = findingCases.Coverage.Select(item => new CoverageDocumentContract(
            item.CoverageId.Value, item.AnalyzerId.Value, item.PopulationId, item.DenominatorLabel,
            item.Denominator, item.CompletedCount, Kebab(item.State), item.TaxonomyId, item.TaxonomyVersion.ToString(),
            taxonomy.Where(value => item.TaxonomyAssignmentIds.Contains(new OpaqueId(value.AssignmentId))).ToArray(),
            gaps.Where(value => item.GapIds.Contains(new OpaqueId(value.ArtifactId))).ToArray(),
            item.Exclusions.Select(value => value.MemberId.Value + ":" + value.Reason).ToArray(),
            failures.Where(value => item.FailureIds.Contains(new OpaqueId(value.ArtifactId))).ToArray())).ToList();
        coverage.AddRange(composedCoverage);

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
            [new ExcludedCapabilityDocumentContract("future-analysis-capabilities", "unsupported", "outside bounded analysis authority")],
            new ReadinessDocumentContract("no-readiness-evaluation", "bounded-local-analysis", true),
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
                new ExcludedCapabilityDocumentContract("model", "not-used", "no model surface in bounded local analysis"),
                new ExcludedCapabilityDocumentContract("credential", "not-used", "no credential surface in bounded local analysis"),
                new ExcludedCapabilityDocumentContract("live", "not-used", "network-off execution"),
                new ExcludedCapabilityDocumentContract("billable", "not-used", "no billable dispatch"),
            ],
            cliFingerprint,
            [new ArtifactReferenceDocumentContract("semantic-output-" + semanticFingerprint[..24], "1.0.0", semanticFingerprint, "retained")]);
        RunOutputContractInvariants.Validate(output);
        return output;
    }

}
