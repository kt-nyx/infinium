using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Analysis.Candidates;
using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.Evaluation;
using Infinium.Application.FindingCases;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal static class ManagedAnalysisOrchestrator
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static AnalysisV1WorkAssignment Execute(
        AuthoritativeStore store,
        ManagedAnalysisOrchestrationRequest request,
        AttemptRecord attempt,
        RunBinding binding,
        DateTimeOffset now,
        Func<bool> stopRequested,
        Action<string>? phaseCompleted = null,
        Action<AnalysisV1WorkAssignment>? progress = null)
    {
        Validate(request, attempt.RunId, binding);
        RunRecord admittedRun = store.GetRun(attempt.RunId);
        request = request with
        {
            StartedAt = admittedRun.CreatedAt,
            ExecutionInput = WithDocumentationReferences(request, request.ExecutionInput),
        };
        DateTimeOffset executionDeadline = admittedRun.CreatedAt.AddMilliseconds(
            request.ExecutionInput.Limits.MaximumWallTimeMilliseconds);
        void Boundary()
        {
            if (stopRequested())
            {
                throw new WorkerStoppedAtSafeBoundaryException();
            }
            if (DateTimeOffset.UtcNow >= executionDeadline)
            {
                throw new AnalysisOutputLimitException("The managed analysis phase graph exceeded its wall-time authority.");
            }
            store.EnsureCandidateAttemptIsCurrent(attempt, binding);
        }

        string docsFingerprint = Fingerprint(new
        {
            context = request.AnalysisContext.CanonicalFingerprint.Value,
            request.DocumentationImport.Mode,
            request.DocumentationImport.DependencyClosureId,
            request.DocumentationImport.ExtractorId,
            request.DocumentationImport.ImportedAt,
            request.DocumentationImport.Manifest,
            source_sha256 = request.DocumentationImport.SourceBytes is null
                ? "none" : Hash(request.DocumentationImport.SourceBytes.Value.Span),
            retained_evidence_sha256 = request.DocumentationImport.RetainedEvidence is null
                ? "none" : Hash(DocumentationEvidenceJsonCodec.Serialize(request.DocumentationImport.RetainedEvidence)),
            accepted_targets = request.DocumentationImport.AcceptedApplicationTargets
                .OrderBy(item => JsonSerializer.Serialize(item, ContractJsonSerializer.Options), StringComparer.Ordinal),
        });
        if (request.DocumentationImport.Mode == DocumentationImportMode.RetainedReuse
            && request.DocumentationImport.RetainedEvidence is { } retainedDocumentation
            && request.ExecutionInput.PriorRunId is { } priorRunId)
        {
            AnalysisPhaseCheckpointRecord? previous = store.ReadLatestAnalysisPhaseCheckpoint(
                priorRunId.Value, DocumentationEvidencePhase.PhaseId);
            byte[] retainedBytes = DocumentationEvidenceJsonCodec.Serialize(retainedDocumentation);
            if (previous is not null
                && previous.PayloadSha256 == Hash(retainedBytes)
                && previous.PayloadByteLength == retainedBytes.LongLength)
            {
                docsFingerprint = previous.InputFingerprint;
            }
        }
        string candidateFingerprint = Fingerprint(new
        {
            docsFingerprint,
            installation_snapshot = request.ExecutionInput.InstallationSnapshot,
            bethesda = request.ExecutionInput.BethesdaSemanticInput,
            source_inputs = request.ExecutionInput.SourceInputs.OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal),
            context = request.AnalysisContext.CanonicalFingerprint.Value,
            configuration = request.ExecutionInput.EffectiveConfiguration,
            manifest = request.ExecutionInput.ResolvedInputManifest,
            analyzers = request.ExecutionInput.AnalyzerDeclarations,
            request.Candidate,
            request.ExecutionInput.Seed,
            request.ExecutionInput.Limits,
            boundaries = request.ExecutionInput.Boundaries.OrderBy(item => item.BoundaryId, StringComparer.Ordinal),
        });
        string findingFingerprint = Fingerprint(new
        {
            candidateFingerprint,
            request.FindingCase,
        });
        Dictionary<string, string> inputFingerprints = new(StringComparer.Ordinal)
        {
            [DocumentationEvidencePhase.PhaseId] = docsFingerprint,
            [CandidateAnalysisPhase.PhaseId] = candidateFingerprint,
            [FindingCaseAnalysisPhase.PhaseId] = findingFingerprint,
        };
        IReadOnlySet<string> invalidated = Invalidated(request, store, inputFingerprints);

        Boundary();
        (DocumentationEvidenceContract Documentation, RetainedAnalysisPayloadSeal Seal, AnalysisPhaseExecution Execution) docs =
            LoadOrExecuteDocumentation(store, request, attempt, binding, docsFingerprint, invalidated, now);
        progress?.Invoke(Assignment(request, request.ExecutionInput, [docs.Execution], executionDeadline));
        phaseCompleted?.Invoke(DocumentationEvidencePhase.PhaseId);
        Boundary();
        (CandidateAnalysisContract Candidate, AnalysisExecutionInputContract ExecutionInput, RetainedAnalysisPayloadSeal Seal, AnalysisPhaseExecution Execution) candidates =
            LoadOrExecuteCandidates(store, request, attempt, binding, candidateFingerprint, invalidated,
                docs.Documentation, docs.Execution.Disposition == "reused-retained-phase", now);
        progress?.Invoke(Assignment(request, candidates.ExecutionInput, [docs.Execution, candidates.Execution], executionDeadline));
        phaseCompleted?.Invoke(CandidateAnalysisPhase.PhaseId);
        Boundary();
        (FindingCaseContract Finding, RetainedAnalysisPayloadSeal Seal, AnalysisPhaseExecution Execution) findings =
            LoadOrExecuteFindings(store, request, attempt, binding, findingFingerprint, invalidated, candidates.Candidate, now);
        phaseCompleted?.Invoke(FindingCaseAnalysisPhase.PhaseId);
        Boundary();

        AnalysisV1WorkAssignment assignment = Assignment(
            request, candidates.ExecutionInput, [docs.Execution, candidates.Execution, findings.Execution], executionDeadline);
        progress?.Invoke(assignment);
        AnalysisPublicationBuilder.ValidateAssignment(assignment);
        return assignment;
    }

    public static AnalysisV1WorkAssignment TerminalAssignment(
        AuthoritativeStore store,
        ManagedAnalysisOrchestrationRequest request)
    {
        RunRecord admittedRun = store.GetRun(request.ExecutionInput.RunId.Value);
        request = request with { StartedAt = admittedRun.CreatedAt };
        string[] phaseIds =
            [DocumentationEvidencePhase.PhaseId, CandidateAnalysisPhase.PhaseId, FindingCaseAnalysisPhase.PhaseId];
        AnalysisPhaseExecution[] completed = phaseIds
            .Select(phaseId => store.ReadLatestAnalysisPhaseCheckpoint(request.ExecutionInput.RunId.Value, phaseId))
            .Where(item => item is not null)
            .Select(item => Execution(item!, item!.Disposition))
            .ToArray();
        return Assignment(request, request.ExecutionInput, completed,
            admittedRun.CreatedAt.AddMilliseconds(request.ExecutionInput.Limits.MaximumWallTimeMilliseconds));
    }

    private static AnalysisV1WorkAssignment Assignment(
        ManagedAnalysisOrchestrationRequest request,
        AnalysisExecutionInputContract executionInput,
        IReadOnlyList<AnalysisPhaseExecution> phases,
        DateTimeOffset executionDeadline)
    {
        executionInput = WithDocumentationReferences(request, executionInput);
        RetainedAnalysisPayloadSeal Placeholder(string phase, string schema) => new(
            "unavailable-" + phase + "-" + request.ExecutionInput.RunId.Value,
            schema, "1.0.0", Hash(System.Text.Encoding.UTF8.GetBytes(
                "managed-terminal-placeholder|" + phase + "|" + request.RequestId)), 1);
        RetainedAnalysisPayloadSeal Output(string phase, string schema) => phases
            .SingleOrDefault(item => item.PhaseId == phase)?.Output ?? Placeholder(phase, schema);
        AnalysisV1WorkAssignment assignment = new(
            AnalysisV1WorkAssignment.CurrentSchemaVersion, request.RequestId, executionInput,
            request.AnalysisContext,
            Output(DocumentationEvidencePhase.PhaseId, ContractConstants.DocumentationEvidenceSchemaId),
            Output(CandidateAnalysisPhase.PhaseId, ContractConstants.CandidateAnalysisSchemaId),
            Output(FindingCaseAnalysisPhase.PhaseId, ContractConstants.FindingCaseSchemaId),
            request.ImplementationCommit, request.StartedAt, request.TerminalOutcome, request.TerminalReason,
            request.MaximumInputBytes, request.MaximumOutputBytes, request.MaximumQueryItems)
        {
            PhaseExecutions = phases,
            DocumentationDependencyIds = DocumentationDependencyIds(request),
            ExecutionDeadline = executionDeadline,
        };
        AnalysisPublicationBuilder.ValidateAssignment(assignment);
        return assignment;
    }

    private static AnalysisExecutionInputContract WithDocumentationReferences(
        ManagedAnalysisOrchestrationRequest request,
        AnalysisExecutionInputContract executionInput)
    {
        if (request.DocumentationImport.RetainedEvidence is null)
        {
            return executionInput;
        }
        DocumentationEvidenceContract retained = request.DocumentationImport.RetainedEvidence;
        byte[] bytes = DocumentationEvidenceJsonCodec.Serialize(retained);
        ArtifactReferenceContract reference = new(retained.PayloadId, retained.SchemaVersion,
            new Sha256Fingerprint(Hash(bytes)), "retained");
        ArtifactReferenceContract? existing = executionInput.SourceInputs.SingleOrDefault(item =>
            item.ArtifactId == reference.ArtifactId);
        if (existing is not null && existing != reference)
        {
            throw new AnalysisIdentityDriftException(
                "The retained documentation evidence identity resolves to substituted source metadata.");
        }
        if (existing == reference)
        {
            return executionInput;
        }
        return executionInput with
        {
            SourceInputs = executionInput.SourceInputs.Append(reference)
                .OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal).ToArray(),
        };
    }

    private static OpaqueId[] DocumentationDependencyIds(ManagedAnalysisOrchestrationRequest request) =>
        new[]
        {
            request.AnalysisContext.ContextId,
            request.DocumentationImport.Manifest.SourceId,
            request.DocumentationImport.RetainedEvidence?.PayloadId,
        }
        .Where(item => item is not null)
        .Select(item => item!)
        .Distinct()
        .OrderBy(item => item.Value, StringComparer.Ordinal)
        .ToArray();

    public static void Validate(ManagedAnalysisOrchestrationRequest request, string runId, RunBinding binding)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = WithDocumentationReferences(request, request.ExecutionInput);
        ValidateDeliveredCandidateInput(request, runId, binding);
        SemanticAnalysisContextIdentity.Validate(request.AnalysisContext);
        Slice5ContractInvariants.Validate(request.ExecutionInput);
        if (request.SchemaVersion != ManagedAnalysisOrchestrationRequest.CurrentSchemaVersion
            || request.RequestId.Length is < 1 or > 128
            || request.ExecutionInput.RunId.Value != runId
            || request.ExecutionInput.InstallationSnapshot.ArtifactId.Value != binding.InstallationSnapshotId
            || request.ExecutionInput.AnalysisContext.ArtifactId != request.AnalysisContext.ContextId
            || request.ExecutionInput.AnalysisContext.ArtifactVersion != request.AnalysisContext.SchemaVersion
            || request.ExecutionInput.AnalysisContext.Fingerprint != request.AnalysisContext.CanonicalFingerprint
            || request.AnalysisContext.ContextId.Value != binding.AnalysisContextId
            || request.ExecutionInput.EffectiveConfiguration.ArtifactId.Value != binding.EffectiveScanConfigurationId
            || request.ExecutionInput.ResolvedInputManifest.ArtifactId.Value != binding.ResolvedInputManifestId
            || request.ExecutionInput.AnalysisContext.Availability != "retained"
            || request.ExecutionInput.InstallationSnapshot.Availability != "retained"
            || request.ExecutionInput.BethesdaSemanticInput.Availability != "retained"
            || request.ExecutionInput.EffectiveConfiguration.Availability != "retained"
            || request.ExecutionInput.ResolvedInputManifest.Availability != "retained"
            || request.ExecutionInput.AnalyzerDeclarations.Any(item => item.Availability != "retained")
            || request.DocumentationImport.OriginatingRunId.Value != runId
            || request.DocumentationImport.ImportRunId.Value != runId
            || !request.ExecutionInput.SourceInputs.Any(item =>
                item.ArtifactId == request.DocumentationImport.Manifest.SourceId
                && item.Fingerprint == request.DocumentationImport.Manifest.ByteFingerprint
                && item.Availability == "retained")
            || (request.DocumentationImport.SourceBytes is not null
                && (request.DocumentationImport.SourceBytes.Value.Length != request.DocumentationImport.Manifest.ByteLength
                    || Hash(request.DocumentationImport.SourceBytes.Value.Span)
                        != request.DocumentationImport.Manifest.ByteFingerprint.Value))
            || request.TerminalOutcome is not (AnalysisTerminalOutcome.Completed or AnalysisTerminalOutcome.CompletedWithGaps))
        {
            throw new AnalysisIdentityDriftException("The durable managed analysis request differs from its immutable run binding.");
        }
    }

    private static void ValidateDeliveredCandidateInput(
        ManagedAnalysisOrchestrationRequest request,
        string runId,
        RunBinding binding)
    {
        CandidateDeliveredInputContract? delivered = request.Candidate.DeliveredInput;
        Sha256Fingerprint? declaredFingerprint = request.Candidate.DeliveredInputByteFingerprint;
        if (delivered is null && declaredFingerprint is null)
        {
            return;
        }
        if (delivered is null || declaredFingerprint is null)
        {
            throw new AnalysisIdentityDriftException(
                "The managed candidate delivered input and its byte fingerprint must be supplied together.");
        }

        CandidateDeliveredContractInvariants.Validate(delivered);
        byte[] bytes = CandidateDeliveredInputJsonCodec.Serialize(delivered);
        Sha256Fingerprint actualFingerprint = new(Hash(bytes));
        ArtifactReferenceContract? reference = request.ExecutionInput.SourceInputs.SingleOrDefault(item =>
            item.ArtifactId == delivered.PayloadId);
        if (delivered.OriginatingRunId.Value != runId
            || delivered.SourceSnapshotId.Value != binding.InstallationSnapshotId
            || delivered.AnalysisContextId.Value != binding.AnalysisContextId
            || delivered.ConfigurationId.Value != binding.EffectiveScanConfigurationId
            || actualFingerprint != declaredFingerprint
            || reference is null
            || reference.ArtifactVersion != delivered.SchemaVersion
            || reference.Fingerprint != actualFingerprint
            || reference.Availability != "retained")
        {
            throw new AnalysisIdentityDriftException(
                "The managed candidate delivered input differs from its immutable run binding or source reference.");
        }
    }

    private static (DocumentationEvidenceContract, RetainedAnalysisPayloadSeal, AnalysisPhaseExecution)
        LoadOrExecuteDocumentation(AuthoritativeStore store, ManagedAnalysisOrchestrationRequest request,
            AttemptRecord attempt, RunBinding binding, string fingerprint, IReadOnlySet<string> invalidated, DateTimeOffset now)
    {
        AnalysisPhaseCheckpointRecord? current = store.ReadAnalysisPhaseCheckpoint(attempt.RunId, DocumentationEvidencePhase.PhaseId, fingerprint);
        if (current is not null)
        {
            byte[] retained = ReadExact(store, current);
            DocumentationEvidenceContract value = DocumentationEvidenceJsonCodec.Deserialize(retained);
            return (value, Seal(current), Execution(current, "reused-completed-phase"));
        }
        if (!invalidated.Contains(DocumentationEvidencePhase.PhaseId)
            && request.ExecutionInput.PriorRunId is not null)
        {
            AnalysisPhaseCheckpointRecord? previous = store.ReadLatestAnalysisPhaseCheckpoint(
                request.ExecutionInput.PriorRunId.Value, DocumentationEvidencePhase.PhaseId);
            if (previous is not null && previous.InputFingerprint == fingerprint)
            {
                byte[] retained = ReadExact(store, previous);
                DocumentationEvidenceContract value = DocumentationEvidenceJsonCodec.Deserialize(retained);
                const string reusedDisposition = "reused-retained-phase";
                AnalysisPhaseCheckpointRecord alias = store.RecordAnalysisPhaseCheckpoint(
                    attempt, binding, DocumentationEvidencePhase.PhaseId, fingerprint,
                    previous.PayloadId, previous.SchemaId, previous.SchemaVersion,
                    previous.PayloadSha256, previous.PayloadByteLength, reusedDisposition,
                    previous.SourceRunId, now);
                return (value, Seal(alias), Execution(alias, reusedDisposition));
            }
        }
        DocumentationEvidencePhaseResult result = DocumentationEvidencePhase.Execute(store, request.DocumentationImport);
        string sha = Hash(result.SerializedPayload);
        string disposition = invalidated.Contains(DocumentationEvidencePhase.PhaseId)
            ? "recomputed-invalidated" : "recomputed-run-binding";
        AnalysisPhaseCheckpointRecord recorded = store.RecordAnalysisPhaseCheckpoint(attempt, binding,
            DocumentationEvidencePhase.PhaseId, fingerprint, result.Receipt.PayloadId,
            ContractConstants.DocumentationEvidenceSchemaId, "1.0.0", sha,
            result.SerializedPayload.LongLength, disposition, attempt.RunId, now);
        return (result.Evidence, Seal(recorded), Execution(recorded, disposition));
    }

    private static (CandidateAnalysisContract, AnalysisExecutionInputContract, RetainedAnalysisPayloadSeal, AnalysisPhaseExecution)
        LoadOrExecuteCandidates(AuthoritativeStore store, ManagedAnalysisOrchestrationRequest request,
            AttemptRecord attempt, RunBinding binding, string fingerprint, IReadOnlySet<string> invalidated,
            DocumentationEvidenceContract documentation, bool documentationReused, DateTimeOffset now)
    {
        CandidateDeliveredInputContract delivered;
        if (request.Candidate.DeliveredInput is { } supplied)
        {
            delivered = supplied;
        }
        else
        {
            BethesdaSemanticSnapshot? bethesda = ReadBethesda(store, request.ExecutionInput.BethesdaSemanticInput);
            delivered = CandidateDeliveredInputAdapter.Create(
                request.ExecutionInput.RunId,
                request.ExecutionInput.InstallationSnapshot.ArtifactId,
                request.AnalysisContext.ContextId,
                request.ExecutionInput.EffectiveConfiguration.ArtifactId,
                bethesda,
                documentation,
                retainedDocumentationSourceRunId: documentation.OriginatingRunId == request.ExecutionInput.RunId
                    ? null : documentation.OriginatingRunId);
        }
        byte[] deliveredBytes = CandidateDeliveredInputJsonCodec.Serialize(delivered);
        _ = store.RetainAnalysisPhaseInput(attempt, "candidate-delivered-input",
            delivered.PayloadId.Value, deliveredBytes, now);
        ArtifactReferenceContract deliveredReference = new(delivered.PayloadId, delivered.SchemaVersion,
            new Sha256Fingerprint(Hash(deliveredBytes)), "retained");
        AnalysisExecutionInputContract effectiveExecutionInput = request.ExecutionInput with
        {
            SourceInputs = request.ExecutionInput.SourceInputs.Append(deliveredReference)
                .DistinctBy(item => item.ArtifactId).OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal).ToArray(),
        };
        AnalysisPhaseCheckpointRecord? current = store.ReadAnalysisPhaseCheckpoint(attempt.RunId, CandidateAnalysisPhase.PhaseId, fingerprint);
        if (current is not null)
        {
            byte[] retained = ReadExact(store, current);
            CandidateAnalysisContract value = CandidateAnalysisJsonCodec.Deserialize(retained);
            return (value, effectiveExecutionInput, Seal(current), Execution(current, "reused-completed-phase"));
        }
        CandidatePopulationContext context = new(documentation, request.ExecutionInput.RunId,
            request.ExecutionInput.InstallationSnapshot.ArtifactId, request.AnalysisContext.ContextId,
            request.ExecutionInput.EffectiveConfiguration.ArtifactId, delivered,
            new Sha256Fingerprint(Hash(deliveredBytes)));
        CandidatePipelineRequest pipeline = new(request.ExecutionInput.RunId, request.Candidate.PopulationId,
            request.Candidate.PolicyId, request.Candidate.ThresholdId, request.Candidate.Limits,
            context, [new DeliveredIndexCandidatePopulationSource()], effectiveExecutionInput);
        CandidateAnalysisPhaseResult result = CandidateAnalysisPhase.Execute(store, pipeline, attempt, binding, now);
        string disposition = invalidated.Contains(CandidateAnalysisPhase.PhaseId) && !documentationReused
            ? "recomputed-invalidated" : "recomputed-run-binding";
        AnalysisPhaseCheckpointRecord recorded = store.RecordAnalysisPhaseCheckpoint(attempt, binding,
            CandidateAnalysisPhase.PhaseId, fingerprint, result.Receipt.PayloadId,
            ContractConstants.CandidateAnalysisSchemaId, "1.0.0", Hash(result.SerializedPayload),
            result.SerializedPayload.LongLength, disposition, attempt.RunId, now);
        return (result.Pipeline.Analysis, effectiveExecutionInput, Seal(recorded), Execution(recorded, disposition));
    }

    private static (FindingCaseContract, RetainedAnalysisPayloadSeal, AnalysisPhaseExecution)
        LoadOrExecuteFindings(AuthoritativeStore store, ManagedAnalysisOrchestrationRequest request,
            AttemptRecord attempt, RunBinding binding, string fingerprint, IReadOnlySet<string> invalidated,
            CandidateAnalysisContract candidates, DateTimeOffset now)
    {
        AnalysisPhaseCheckpointRecord? current = store.ReadAnalysisPhaseCheckpoint(attempt.RunId, FindingCaseAnalysisPhase.PhaseId, fingerprint);
        if (current is not null)
        {
            byte[] retained = ReadExact(store, current);
            FindingCaseContract value = FindingCaseJsonCodec.Deserialize(retained);
            return (value, Seal(current), Execution(current, "reused-completed-phase"));
        }
        FindingCaseAnalysisPhaseResult result = FindingCaseAnalysisPhase.Execute(
            store, request.FindingCase.Bind(candidates), attempt, binding, now);
        string disposition = invalidated.Contains(FindingCaseAnalysisPhase.PhaseId)
            ? "recomputed-invalidated" : "recomputed-run-binding";
        AnalysisPhaseCheckpointRecord recorded = store.RecordAnalysisPhaseCheckpoint(attempt, binding,
            FindingCaseAnalysisPhase.PhaseId, fingerprint, result.Receipt.StoredPayloadId,
            ContractConstants.FindingCaseSchemaId, "1.0.0", Hash(result.SerializedPayload),
            result.SerializedPayload.LongLength, disposition, attempt.RunId, now);
        return (result.Analysis, Seal(recorded), Execution(recorded, disposition));
    }

    private static IReadOnlySet<string> Invalidated(ManagedAnalysisOrchestrationRequest request,
        AuthoritativeStore store, Dictionary<string, string> current)
    {
        string[] phases = [DocumentationEvidencePhase.PhaseId, CandidateAnalysisPhase.PhaseId, FindingCaseAnalysisPhase.PhaseId];
        (string From, string To)[] edges =
        [
            (CandidateAnalysisPhase.PhaseId, DocumentationEvidencePhase.PhaseId),
            (FindingCaseAnalysisPhase.PhaseId, CandidateAnalysisPhase.PhaseId),
            (AnalysisExecutionPhase.PhaseId, FindingCaseAnalysisPhase.PhaseId),
        ];
        if (request.ExecutionInput.PriorRunId is null)
        {
            return phases.Append(AnalysisExecutionPhase.PhaseId).ToHashSet(StringComparer.Ordinal);
        }
        string prior = request.ExecutionInput.PriorRunId.Value;
        try
        {
            RunRecord priorRun = store.GetRun(prior);
            if (!LifecyclePolicy.IsTerminal(priorRun.State)
                || store.GetAnalysisSemanticFingerprint(prior) is null)
            {
                throw new AnalysisIdentityDriftException(
                    "The retained prior analysis run is unavailable or has no authoritative publication.");
            }
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException)
        {
            throw new AnalysisIdentityDriftException(
                "The retained prior analysis run is unavailable or drifted.", exception);
        }
        List<string> changed = [];
        foreach (string phase in phases)
        {
            AnalysisPhaseCheckpointRecord? previous = store.ReadLatestAnalysisPhaseCheckpoint(prior, phase);
            if (previous is null)
            {
                throw new AnalysisIdentityDriftException(
                    $"The retained prior analysis phase '{phase}' is missing its durable checkpoint.");
            }
            if (previous.InputFingerprint != current[phase])
            {
                changed.Add(phase);
            }
        }
        return ReplayInvalidationPlanner.InvalidatedClosure(edges, changed);
    }

    private static BethesdaSemanticSnapshot? ReadBethesda(AuthoritativeStore store, ArtifactReferenceContract reference)
    {
        if (reference.Availability != "retained")
        {
            throw new AnalysisIdentityDriftException("The required Bethesda semantic dependency is unavailable.");
        }
        byte[] bytes;
        try
        {
            bytes = store.ReadCandidateAnalysisPayload(reference.ArtifactId.Value);
        }
        catch (KeyNotFoundException exception)
        {
            throw new AnalysisIdentityDriftException(
                "The required Bethesda semantic dependency bytes are absent.", exception);
        }
        if (Hash(bytes) != reference.Fingerprint.Value)
        {
            throw new AnalysisIdentityDriftException("The retained Bethesda semantic input fingerprint drifted.");
        }
        BethesdaSemanticExtractionResult result = JsonSerializer.Deserialize<BethesdaSemanticExtractionResult>(bytes, StrictJson)
            ?? throw new InvalidDataException("The retained Bethesda semantic input is malformed.");
        return result.Snapshot ?? throw new InvalidDataException("The retained Bethesda semantic input has no snapshot.");
    }

    private static byte[] ReadExact(AuthoritativeStore store, AnalysisPhaseCheckpointRecord checkpoint)
    {
        RetainedPayloadRecord retained;
        byte[] bytes;
        try
        {
            retained = store.GetRetainedPayload(checkpoint.PayloadId);
            bytes = store.ReadCandidateAnalysisPayload(checkpoint.PayloadId);
        }
        catch (KeyNotFoundException exception)
        {
            throw new AnalysisIdentityDriftException(
                "A retained analysis phase checkpoint names absent payload bytes.", exception);
        }
        if (retained.Sha256 != checkpoint.PayloadSha256 || retained.ByteLength != checkpoint.PayloadByteLength
            || Hash(bytes) != checkpoint.PayloadSha256)
        {
            throw new AnalysisIdentityDriftException("A completed analysis phase checkpoint drifted from retained bytes.");
        }
        return bytes;
    }

    private static RetainedAnalysisPayloadSeal Seal(AnalysisPhaseCheckpointRecord value) =>
        new(value.PayloadId, value.SchemaId, value.SchemaVersion, value.PayloadSha256, value.PayloadByteLength);
    private static AnalysisPhaseExecution Execution(AnalysisPhaseCheckpointRecord value, string disposition) =>
        new(value.PhaseId, value.InputFingerprint, Seal(value), disposition, value.SourceRunId);
    private static string Fingerprint<T>(T value) => Hash(JsonSerializer.SerializeToUtf8Bytes(value, ContractJsonSerializer.Options));
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
}
