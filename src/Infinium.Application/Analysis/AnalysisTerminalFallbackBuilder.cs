using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

/// <summary>
/// Produces the small coordinator-owned terminal document used only when the
/// ordinary WP2-WP4 projection cannot be completed. It never retries or
/// interprets the failed semantic payloads.
/// </summary>
public static class AnalysisTerminalFallbackBuilder
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
        AnalysisTerminalOutcome terminalOutcome,
        string terminalReason,
        DateTimeOffset endedAt)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        AnalysisPublicationBuilder.ValidateAssignment(assignment);
        if (terminalOutcome is not (AnalysisTerminalOutcome.Cancelled
            or AnalysisTerminalOutcome.LimitReached
            or AnalysisTerminalOutcome.Failed))
        {
            throw new ArgumentOutOfRangeException(nameof(terminalOutcome));
        }
        if (string.IsNullOrWhiteSpace(terminalReason) || terminalReason.Length > 512)
        {
            throw new ArgumentException("A bounded terminal reason is required.", nameof(terminalReason));
        }

        AnalysisV1WorkAssignment terminalAssignment = assignment with
        {
            TerminalOutcome = terminalOutcome,
            TerminalReason = terminalReason,
        };
        string runId = assignment.ExecutionInput.RunId.Value;
        string semanticFingerprint = Hash(Encoding.UTF8.GetBytes(
            $"analysis-terminal-fallback/v1\n{terminalOutcome}\n{terminalReason}"));
        string dependencyClosureId = StableId(
            "analysis-terminal-dependency-closure", runId,
            assignment.ExecutionInput.Mode.ToString(), assignment.ExecutionInput.PriorRunId?.Value ?? "none",
            assignment.DocumentationEvidence.Sha256, assignment.CandidateAnalysis.Sha256,
            assignment.FindingCase.Sha256);

        List<ReplayDependencyNodeContract> dependencies = [];
        void Add(string id, string kind, string version, string fingerprint, Slice5ResultState state) =>
            dependencies.Add(new ReplayDependencyNodeContract(
                new OpaqueId(id), kind, ContractVersion.Parse(version), new Sha256Fingerprint(fingerprint), state));
        Add(assignment.ExecutionInput.ExecutionInputId.Value, "execution-input",
            assignment.ExecutionInput.SchemaVersion.ToString(),
            Hash(AnalysisExecutionInputJsonCodec.Serialize(assignment.ExecutionInput)), Slice5ResultState.Present);
        foreach ((string kind, ArtifactReferenceContract value) in References(assignment.ExecutionInput))
        {
            Add(value.ArtifactId.Value, kind, value.ArtifactVersion.ToString(), value.Fingerprint.Value,
                value.Availability == "retained" ? Slice5ResultState.Present : Slice5ResultState.Unavailable);
        }
        Add(assignment.DocumentationEvidence.PayloadId, "documentation-evidence",
            assignment.DocumentationEvidence.SchemaVersion, assignment.DocumentationEvidence.Sha256, Slice5ResultState.Present);
        Add(assignment.CandidateAnalysis.PayloadId, "candidate-analysis",
            assignment.CandidateAnalysis.SchemaVersion, assignment.CandidateAnalysis.Sha256, Slice5ResultState.Present);
        Add(assignment.FindingCase.PayloadId, "finding-case",
            assignment.FindingCase.SchemaVersion, assignment.FindingCase.Sha256, Slice5ResultState.Present);
        ReplayDependencyNodeContract[] uniqueDependencies = dependencies
            .GroupBy(item => item.DependencyId)
            .Select(group => group.Distinct().Count() == 1
                ? group.First()
                : throw new InvalidDataException("Fallback dependency identities contain conflicting metadata."))
            .OrderBy(item => item.DependencyId.Value, StringComparer.Ordinal)
            .ToArray();
        OpaqueId[] missing = uniqueDependencies.Where(item => item.State != Slice5ResultState.Present)
            .Select(item => item.DependencyId).ToArray();
        string replayManifestId = StableId(
            "analysis-terminal-replay", dependencyClosureId, semanticFingerprint, terminalOutcome.ToString());
        AnalysisReplayContract replay = new(
            ContractConstants.AnalysisReplaySchemaId, new ContractVersion(1, 0, 0),
            new OpaqueId(replayManifestId), assignment.ExecutionInput.RunId, assignment.ExecutionInput.Mode,
            ReplayState.Partial, AuditabilityState.Partial, uniqueDependencies,
            uniqueDependencies.Where(item => item.DependencyId != assignment.ExecutionInput.ExecutionInputId)
                .Select(item => new ReplayDependencyEdgeContract(assignment.ExecutionInput.ExecutionInputId, item.DependencyId))
                .ToArray(),
            [
                ReplayOutput(assignment.DocumentationEvidence),
                ReplayOutput(assignment.CandidateAnalysis),
                ReplayOutput(assignment.FindingCase),
            ],
            missing, [], false, assignment.ExecutionInput.PriorRunId);
        Slice5ContractInvariants.Validate(replay);

        ExternalBoundaryReceipt boundaryReceipt = new(
            1, runId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "not-used",
                ["model"] = "not-used",
                ["credential"] = "not-used",
                ["live"] = "not-used",
                ["billable"] = "not-used",
            },
            "The coordinator terminal fallback performs retained local-only publication.");

        RunOutputContract provisional = Output(
            terminalAssignment, replay, dependencyClosureId, semanticFingerprint,
            new string('0', 64), terminalReason, endedAt);
        CliSummaryDocumentContract cli = Summary(terminalAssignment, provisional, endedAt);
        string cliFingerprint = Hash(CliSummaryJsonCodec.Serialize(cli));
        RunOutputContract output = Output(
            terminalAssignment, replay, dependencyClosureId, semanticFingerprint,
            cliFingerprint, terminalReason, endedAt);
        byte[] outputBytes = RunOutputJsonCodec.Serialize(output);
        byte[] cliBytes = CliSummaryJsonCodec.Serialize(cli);
        string human = AnalysisOutputRenderer.Render(output, cli);
        if (outputBytes.LongLength > assignment.MaximumOutputBytes
            || AnalysisPublicationBuilder.CountOutputItems(output)
                > assignment.ExecutionInput.Limits.MaximumOutputItems
            || checked(outputBytes.LongLength + cliBytes.LongLength + Encoding.UTF8.GetByteCount(human))
                > AnalysisV1WorkAssignment.AbsoluteMaximumQueryResponseBytes)
        {
            throw new AnalysisOutputLimitException("The coordinator terminal fallback exceeds its admitted output bound.");
        }

        byte[] replayBytes = AnalysisReplayJsonCodec.Serialize(replay);
        List<AnalysisPublishedArtifact> artifacts =
        [
            Published(assignment.DocumentationEvidence, "documentation-evidence", dependencyClosureId),
            Published(assignment.CandidateAnalysis, "candidate-analysis", dependencyClosureId),
            Published(assignment.FindingCase, "finding-case", dependencyClosureId),
            new(replayManifestId, "analysis-replay", replay.SchemaId, replay.SchemaVersion.ToString(), 1,
                "partial", Hash(replayBytes), replayBytes.LongLength,
                StableId("provenance", replayManifestId), dependencyClosureId),
            new(assignment.ExecutionInput.ExecutionInputId.Value, "analysis-execution-input",
                assignment.ExecutionInput.SchemaId, assignment.ExecutionInput.SchemaVersion.ToString(), 1,
                "present", Hash(AnalysisExecutionInputJsonCodec.Serialize(assignment.ExecutionInput)),
                AnalysisExecutionInputJsonCodec.Serialize(assignment.ExecutionInput).LongLength,
                StableId("provenance", assignment.ExecutionInput.ExecutionInputId.Value), dependencyClosureId),
        ];
        return new AnalysisPublicationBundle(
            replay, output, cli, boundaryReceipt, dependencyClosureId, semanticFingerprint, artifacts);
    }

    private static RunOutputContract Output(
        AnalysisV1WorkAssignment assignment,
        AnalysisReplayContract replay,
        string closure,
        string semanticFingerprint,
        string cliFingerprint,
        string reason,
        DateTimeOffset endedAt)
    {
        ArtifactReferenceDocumentContract Reference(ArtifactReferenceContract value) =>
            new(value.ArtifactId.Value, value.ArtifactVersion.ToString(), value.Fingerprint.Value, value.Availability);
        ArtifactProvenanceDocumentContract provenance = new(
            "m1-s5-wp5-terminal-fallback", "1.0.0", assignment.ExecutionInput.RunId.Value,
            [Reference(assignment.ExecutionInput.InstallationSnapshot), Reference(assignment.ExecutionInput.EffectiveConfiguration)],
            [], [], new LlmInvolvementDocumentContract("none", "none", null));
        TypedArtifactDocumentContract Stage(string kind, string artifactType, RetainedAnalysisPayloadSeal seal) => new(
            StableId("analysis-terminal-retained-stage", assignment.ExecutionInput.RunId.Value, kind, seal.Sha256), 1,
            artifactType, "present",
            new ArtifactReferenceDocumentContract(seal.PayloadId, seal.SchemaVersion, seal.Sha256, "retained"), provenance);
        TypedArtifactDocumentContract documentationStage = Stage(
            "documentation-evidence", "documentation-revision", assignment.DocumentationEvidence);
        TypedArtifactDocumentContract candidateStage = Stage(
            "candidate-analysis", "candidate", assignment.CandidateAnalysis);
        TypedArtifactDocumentContract findingStage = Stage(
            "finding-case", "finding", assignment.FindingCase);
        Dictionary<string, RunOutputCollectionStateContract> states = CollectionNames.ToDictionary(
            name => name,
            name => new RunOutputCollectionStateContract("empty", "no retained evidence exists for this collection"),
            StringComparer.Ordinal);
        states["documentation_revisions"] = new("populated", "retained WP2 stage evidence remains authoritative");
        states["candidates"] = new("populated", "retained WP3 stage evidence remains authoritative");
        states["findings"] = new("populated", "retained WP4 stage evidence remains authoritative");
        string markerKind = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Cancelled => "cancellation-gap",
            AnalysisTerminalOutcome.LimitReached => "output-limit-gap",
            _ => "failure",
        };
        string markerState = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Cancelled => "cancelled",
            AnalysisTerminalOutcome.LimitReached => "limit-reached",
            _ => "failed",
        };
        TypedArtifactDocumentContract terminalMarker = new(
            StableId("analysis-terminal-marker", assignment.ExecutionInput.RunId.Value, markerKind, reason), 1,
            assignment.TerminalOutcome == AnalysisTerminalOutcome.Failed ? "failure" : "coverage-gap", markerState,
            new ArtifactReferenceDocumentContract(
                assignment.FindingCase.PayloadId, assignment.FindingCase.SchemaVersion,
                assignment.FindingCase.Sha256, "retained"), provenance);
        bool isFailure = assignment.TerminalOutcome == AnalysisTerminalOutcome.Failed;
        states[isFailure ? "failures" : "coverage_gaps"] = new("populated", reason);
        TypedArtifactDocumentContract[] terminalGaps = isFailure ? [] : [terminalMarker];
        TypedArtifactDocumentContract[] terminalFailures = isFailure ? [terminalMarker] : [];
        string coverageStatus = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Cancelled => "completed-with-gaps",
            AnalysisTerminalOutcome.LimitReached => "skipped-by-limit",
            _ => "failed",
        };
        CoverageDocumentContract[] coverage = assignment.ExecutionInput.AnalyzerDeclarations
            .Select(analyzer => new CoverageDocumentContract(
                StableId("analysis-terminal-coverage", assignment.ExecutionInput.RunId.Value, analyzer.ArtifactId.Value),
                analyzer.ArtifactId.Value, "terminal-fallback", "admitted analyzer", 1, 0, coverageStatus,
                ContractConstants.TaxonomyId, ContractConstants.TaxonomyVersion,
                [], terminalGaps, [], terminalFailures))
            .ToArray();
        string state = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Cancelled => "cancelled",
            AnalysisTerminalOutcome.LimitReached => "limit-reached",
            _ => "failed",
        };
        RunOutputContract output = new(
            ContractConstants.RunOutputSchemaId, "1", assignment.ExecutionInput.RunId.Value, "analysis", state,
            assignment.ImplementationCommit, Utc(assignment.StartedAt), Utc(endedAt),
            Reference(assignment.ExecutionInput.InstallationSnapshot),
            new ArtifactReferenceDocumentContract(
                assignment.AnalysisContextId, "1.0.0", Hash(Encoding.UTF8.GetBytes(assignment.AnalysisContextId)), "retained"),
            Reference(assignment.ExecutionInput.EffectiveConfiguration),
            Reference(assignment.ExecutionInput.ResolvedInputManifest),
            ContractConstants.TaxonomyId, ContractConstants.TaxonomyVersion,
            assignment.ExecutionInput.AnalyzerDeclarations.Select(Reference).ToArray(),
            [], [], [], [], [], [], [], [candidateStage], [], [findingStage], [], [], [], [], [],
            terminalGaps, terminalFailures, [documentationStage], [], [], [], [],
            states, [], coverage,
            [new ExcludedCapabilityDocumentContract("slice-6-and-later", "unsupported", "outside WP5 authority")],
            new ReadinessDocumentContract("no-readiness-evaluation", "m1-slice5-wp5", true),
            new ReplayabilityDocumentContract(
                "partial", "boundary-replay",
                new ArtifactReferenceDocumentContract(closure, "1.0.0", semanticFingerprint, "retained"), []),
            new AuditabilityDocumentContract("complete-with-gaps", terminalGaps.Concat(terminalFailures).ToArray()),
            new ArtifactReferenceDocumentContract(
                replay.ReplayManifestId.Value, replay.SchemaVersion.ToString(),
                Hash(AnalysisReplayJsonCodec.Serialize(replay)), "retained"),
            [
                new ExcludedCapabilityDocumentContract("provider", "not-used", "local-only terminal publication"),
                new ExcludedCapabilityDocumentContract("model", "not-used", "no model dispatch"),
                new ExcludedCapabilityDocumentContract("credential", "not-used", "no credential access"),
                new ExcludedCapabilityDocumentContract("live", "not-used", "no network access"),
                new ExcludedCapabilityDocumentContract("billable", "not-used", "no billable dispatch"),
            ],
            cliFingerprint,
            [new ArtifactReferenceDocumentContract(
                "semantic-output-" + semanticFingerprint[..24], "1.0.0", semanticFingerprint, "retained")]);
        RunOutputContractInvariants.Validate(output);
        return output;
    }

    private static CliSummaryDocumentContract Summary(
        AnalysisV1WorkAssignment assignment,
        RunOutputContract output,
        DateTimeOffset endedAt)
    {
        string outcome = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Cancelled => "cancelled",
            AnalysisTerminalOutcome.LimitReached => "limit-reached",
            _ => "failed",
        };
        CliSummaryDocumentContract summary = new(
            ContractConstants.CliSummarySchemaId, "1", output.RunId, outcome,
            outcome == "cancelled" ? (int)CliExitCode.Cancelled
                : outcome == "limit-reached" ? (int)CliExitCode.LimitReached : (int)CliExitCode.Failed,
            new TypedOutputCountsContract(
                0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0,
                assignment.TerminalOutcome == AnalysisTerminalOutcome.Failed ? 0 : 1,
                assignment.TerminalOutcome == AnalysisTerminalOutcome.Failed ? 1 : 0,
                1, 0, 0, 0, 0),
            new CoverageStateCountsContract(
                0,
                assignment.TerminalOutcome == AnalysisTerminalOutcome.Cancelled
                    ? assignment.ExecutionInput.AnalyzerDeclarations.Count : 0,
                assignment.TerminalOutcome == AnalysisTerminalOutcome.Failed
                    ? assignment.ExecutionInput.AnalyzerDeclarations.Count : 0,
                0,
                assignment.TerminalOutcome == AnalysisTerminalOutcome.LimitReached
                    ? assignment.ExecutionInput.AnalyzerDeclarations.Count : 0,
                0),
            Math.Max(0, (long)(endedAt - assignment.StartedAt).TotalMilliseconds),
            new CliCostContract(0, 0, 0, 0, 0, 0, 0, false),
            "no-readiness-evaluation", true);
        CliSummaryDocumentContractInvariants.Validate(summary);
        return summary;
    }

    private static IEnumerable<(string Kind, ArtifactReferenceContract Value)> References(AnalysisExecutionInputContract input)
    {
        yield return ("installation-snapshot", input.InstallationSnapshot);
        yield return ("bethesda-semantic-input", input.BethesdaSemanticInput);
        foreach (ArtifactReferenceContract value in input.SourceInputs)
        {
            yield return ("source-input", value);
        }
        foreach (ArtifactReferenceContract value in input.AnalyzerDeclarations)
        {
            yield return ("analyzer-declaration", value);
        }
        yield return ("effective-configuration", input.EffectiveConfiguration);
        yield return ("resolved-input-manifest", input.ResolvedInputManifest);
    }

    private static ReplayOutputContract ReplayOutput(RetainedAnalysisPayloadSeal seal) =>
        new(new OpaqueId(seal.PayloadId), seal.SchemaId, ContractVersion.Parse(seal.SchemaVersion),
            new Sha256Fingerprint(seal.Sha256), new Sha256Fingerprint(seal.Sha256));

    private static AnalysisPublishedArtifact Published(
        RetainedAnalysisPayloadSeal seal, string kind, string closure) =>
        new(seal.PayloadId, kind, seal.SchemaId, seal.SchemaVersion, 1, "present", seal.Sha256,
            seal.ByteLength, StableId("provenance", seal.PayloadId), closure);

    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
    private static string StableId(string kind, params string[] values) =>
        kind + "-" + Hash(Encoding.UTF8.GetBytes(string.Join('\n', values)))[..32];
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
}
