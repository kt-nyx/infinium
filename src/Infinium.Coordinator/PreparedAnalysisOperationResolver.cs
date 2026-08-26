using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal sealed record RetainedAnalysisInputDocument(
    string SourceRunId,
    string SourceOperationSha256,
    string Capability,
    string InstallationSnapshotId,
    string InstallationSnapshotSha256,
    string AnalysisContextId,
    string AnalysisContextFingerprint,
    string ResolvedInputManifestId,
    string ResolvedInputManifestFingerprint,
    string AnalyzerId,
    string OperationKind);

internal sealed record ResolvedPreparedAnalysisInput(
    string PackageId,
    string PackageFingerprint,
    RetainedAnalysisInputDocument Document,
    ManagedAnalysisOrchestrationRequest SourceRequest);

internal sealed record ResolvedPreparedAnalysisOperation(
    string OperationKind,
    string RequestJson,
    string RequestSha256);

internal static class PreparedAnalysisOperationResolver
{
    public const string SetupObjectKind = "retained-analysis-input";
    public const string Capability = "delivered-index-local";
    private const int MaximumSnapshotBytes = 64 * 1024 * 1024;

    public static string PackageId(
        string installationSnapshotId,
        string analysisContextId,
        string resolvedInputManifestId) =>
        "analysis-input-" + Hash(string.Join('\n',
            Capability,
            installationSnapshotId,
            analysisContextId,
            resolvedInputManifestId))[..32];

    public static SetupMutationReceipt RetainCompletedSource(
        AuthoritativeStore store,
        string sourceRunId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRunId);
        RunRecord sourceRun = store.GetRun(sourceRunId);
        if (sourceRun.State != LifecycleState.Completed)
        {
            throw new InvalidOperationException(
                "A reusable prepared-analysis input must come from a completed durable analysis run.");
        }

        RunOperationRecord operation = store.GetRunOperation(sourceRunId)
            ?? throw new InvalidOperationException(
                "The reusable source run has no durable analysis operation.");
        if (!StringComparer.Ordinal.Equals(operation.OperationKind, ManagedRunExecutor.ManagedAnalysisOperation))
        {
            throw new InvalidOperationException(
                "The reusable source run does not contain the supported managed analysis operation.");
        }

        ManagedAnalysisOrchestrationRequest request = DeserializeOperation(operation);
        ValidateReusableSource(store, sourceRun, operation, request);
        byte[] snapshotBytes = ReadAndValidateSnapshot(store, request.ExecutionInput.InstallationSnapshot);
        RetainedAnalysisInputDocument document = new(
            sourceRunId,
            operation.RequestSha256,
            Capability,
            sourceRun.Binding.InstallationSnapshotId,
            Hash(snapshotBytes),
            sourceRun.Binding.AnalysisContextId,
            request.AnalysisContext.CanonicalFingerprint.Value,
            sourceRun.Binding.ResolvedInputManifestId,
            request.ExecutionInput.ResolvedInputManifest.Fingerprint.Value,
            DeliveredIndexCandidatePopulationSource.Id.Value,
            operation.OperationKind);
        string packageId = PackageId(
            document.InstallationSnapshotId,
            document.AnalysisContextId,
            document.ResolvedInputManifestId);
        string payload = JsonSerializer.Serialize(document);
        return store.ApplySetupMutation(new(
            "retain-" + Hash(payload)[..32],
            "retain-analysis-input",
            SetupObjectKind,
            packageId,
            ExpectedRevision: 0,
            LifecycleState: "active",
            PayloadJson: payload,
            RequestedAt: now));
    }

    public static ResolvedPreparedAnalysisInput Resolve(
        AuthoritativeStore store,
        string installationSnapshotId,
        string analysisContextId,
        string resolvedInputManifestId)
    {
        string packageId = PackageId(
            installationSnapshotId,
            analysisContextId,
            resolvedInputManifestId);
        SetupObjectRecord record = store.FindSetupObject(SetupObjectKind, packageId)
            ?? throw new KeyNotFoundException(
                "The exact retained analysis input package is unavailable.");
        if (record.LifecycleState != "active")
        {
            throw new InvalidOperationException(
                "The exact retained analysis input package is not active.");
        }

        RetainedAnalysisInputDocument document = JsonSerializer.Deserialize<RetainedAnalysisInputDocument>(
            record.PayloadJson)
            ?? throw new InvalidDataException("The retained analysis input package is malformed.");
        if (document.Capability != Capability
            || document.OperationKind != ManagedRunExecutor.ManagedAnalysisOperation
            || document.AnalyzerId != DeliveredIndexCandidatePopulationSource.Id.Value
            || document.InstallationSnapshotId != installationSnapshotId
            || document.AnalysisContextId != analysisContextId
            || document.ResolvedInputManifestId != resolvedInputManifestId)
        {
            throw new AnalysisIdentityDriftException(
                "The retained analysis input package differs from the requested identities.");
        }

        RunRecord sourceRun = store.GetRun(document.SourceRunId);
        RunOperationRecord operation = store.GetRunOperation(document.SourceRunId)
            ?? throw new AnalysisIdentityDriftException(
                "The retained analysis input source operation is unavailable.");
        if (sourceRun.State != LifecycleState.Completed
            || sourceRun.Binding.InstallationSnapshotId != installationSnapshotId
            || sourceRun.Binding.AnalysisContextId != analysisContextId
            || sourceRun.Binding.ResolvedInputManifestId != resolvedInputManifestId
            || operation.OperationKind != document.OperationKind
            || operation.RequestSha256 != document.SourceOperationSha256)
        {
            throw new AnalysisIdentityDriftException(
                "The retained analysis input source run was substituted or is no longer authoritative.");
        }

        ManagedAnalysisOrchestrationRequest request = DeserializeOperation(operation);
        ValidateReusableSource(store, sourceRun, operation, request);
        byte[] snapshotBytes = ReadAndValidateSnapshot(store, request.ExecutionInput.InstallationSnapshot);
        if (Hash(snapshotBytes) != document.InstallationSnapshotSha256
            || request.AnalysisContext.CanonicalFingerprint.Value != document.AnalysisContextFingerprint
            || request.ExecutionInput.ResolvedInputManifest.Fingerprint.Value
                != document.ResolvedInputManifestFingerprint)
        {
            throw new AnalysisIdentityDriftException(
                "A retained analysis input component no longer has its admitted fingerprint.");
        }

        return new(
            packageId,
            Hash(record.PayloadJson),
            document,
            request);
    }

    public static void ValidateSnapshotProfile(
        AuthoritativeStore store,
        string installationSnapshotId,
        string installationRoot,
        string profileName)
    {
        byte[] bytes = store.ReadPublishedSnapshotPayload(installationSnapshotId, MaximumSnapshotBytes);
        Mo2SnapshotCaptureResult capture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(bytes)
            ?? throw new InvalidDataException("The retained installation snapshot is malformed.");
        Mo2InstallationSnapshot snapshot = capture.Snapshot
            ?? throw new InvalidOperationException("The retained installation snapshot has no completed payload.");
        string expectedRoot = Path.GetFullPath(installationRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string expectedProfile = Path.GetFullPath(Path.Combine(expectedRoot, "profiles", profileName))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string actualRoot = Path.GetFullPath(snapshot.InstanceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string actualProfile = Path.GetFullPath(snapshot.ProfileRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (capture.State is not (SnapshotCaptureState.Completed or SnapshotCaptureState.CompletedWithGaps)
            || snapshot.Contract.SnapshotId.Value != installationSnapshotId
            || !StringComparer.OrdinalIgnoreCase.Equals(actualRoot, expectedRoot)
            || !StringComparer.OrdinalIgnoreCase.Equals(actualProfile, expectedProfile))
        {
            throw new AnalysisIdentityDriftException(
                "The retained installation snapshot does not belong to the confirmed MO2 installation and profile.");
        }
    }

    public static ResolvedPreparedAnalysisOperation Bind(
        ResolvedPreparedAnalysisInput input,
        string runId,
        RunBinding binding,
        string effectiveConfigurationJson,
        ulong maximumElapsedMilliseconds,
        DateTimeOffset preparedAt)
    {
        ArgumentNullException.ThrowIfNull(input);
        string effectiveFingerprint = Hash(effectiveConfigurationJson);
        ManagedAnalysisOrchestrationRequest source = input.SourceRequest;
        AnalysisExecutionInputContract execution = source.ExecutionInput with
        {
            ExecutionInputId = new OpaqueId("execution-" + Hash(runId + "\n" + input.PackageFingerprint)[..32]),
            RunId = new OpaqueId(runId),
            EffectiveConfiguration = source.ExecutionInput.EffectiveConfiguration with
            {
                ArtifactId = new OpaqueId(binding.EffectiveScanConfigurationId),
                Fingerprint = new Sha256Fingerprint(effectiveFingerprint),
            },
            Limits = source.ExecutionInput.Limits with
            {
                MaximumWallTimeMilliseconds = checked((long)maximumElapsedMilliseconds),
            },
        };
        DocumentationImportRequestContract documentation = source.DocumentationImport with
        {
            OriginatingRunId = new OpaqueId(runId),
            ImportRunId = new OpaqueId(runId),
        };
        ManagedAnalysisOrchestrationRequest request = source with
        {
            RequestId = "prepared-analysis-" + Hash(runId + "\n" + input.PackageFingerprint)[..40],
            ExecutionInput = execution,
            DocumentationImport = documentation,
            StartedAt = preparedAt,
        };
        ManagedAnalysisOrchestrator.Validate(request, runId, binding);
        string json = JsonSerializer.Serialize(request, ContractJsonSerializer.Options);
        return new(
            ManagedRunExecutor.ManagedAnalysisOperation,
            json,
            Hash(json));
    }

    public static string SubmissionFingerprint(
        string commandId,
        string requestedRunIdentity,
        string preparationId,
        long preparationRevision,
        string initiationKind,
        string gestureId,
        DateTimeOffset dispatchDeadline,
        RunBinding binding,
        ResolvedPreparedAnalysisOperation operation) => Hash(string.Join('\n',
            "prepared-analysis-submission/v1",
            commandId,
            requestedRunIdentity,
            preparationId,
            preparationRevision.ToString(CultureInfo.InvariantCulture),
            initiationKind,
            gestureId,
            dispatchDeadline.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            binding.InstallationSnapshotId,
            binding.AnalysisContextId,
            binding.EffectiveScanConfigurationId,
            binding.ResolvedInputManifestId,
            operation.OperationKind,
            operation.RequestSha256));

    private static void ValidateReusableSource(
        AuthoritativeStore store,
        RunRecord sourceRun,
        RunOperationRecord operation,
        ManagedAnalysisOrchestrationRequest request)
    {
        ManagedAnalysisOrchestrator.Validate(request, sourceRun.RunId, sourceRun.Binding);
        if (Hash(operation.RequestJson) != operation.RequestSha256
            || request.ExecutionInput.Mode != ReplayMode.Clean
            || request.ExecutionInput.PriorRunId is not null
            || request.Candidate.DeliveredInput is not null
            || request.Candidate.DeliveredInputByteFingerprint is not null
            || request.DocumentationImport.Mode != DocumentationImportMode.CleanImport
            || request.DocumentationImport.RetainedEvidence is not null
            || request.DocumentationImport.AcceptedApplicationTargets.Count != 0
            || request.DocumentationImport.Manifest.Applications.Count != 0
            || request.AnalysisComposition is not null
            || request.ExecutionInput.Boundaries.Any(item => item.State != BoundaryUseState.NotUsed)
            || request.FindingCase.Boundaries.Any(item => item.State != BoundaryUseState.NotUsed))
        {
            throw new InvalidOperationException(
                "The completed analysis operation is not a reusable local retained-input source.");
        }

        AnalyzerDeclarationContract declaration = new DeliveredIndexCandidatePopulationSource().Declaration;
        Sha256Fingerprint expectedAnalyzerFingerprint = CandidateAnalysisIdentity.StructuralHash(
            [JsonSerializer.Serialize(declaration)]);
        if (request.ExecutionInput.AnalyzerDeclarations.Count != 1
            || request.ExecutionInput.AnalyzerDeclarations[0].ArtifactId.Value != declaration.AnalyzerId
            || request.ExecutionInput.AnalyzerDeclarations[0].ArtifactVersion != declaration.AnalyzerVersion
            || request.ExecutionInput.AnalyzerDeclarations[0].Fingerprint != expectedAnalyzerFingerprint
            || request.ExecutionInput.AnalyzerDeclarations[0].Availability != "retained")
        {
            throw new InvalidOperationException(
                "The completed analysis operation does not declare the supported local analysis capability.");
        }

        byte[] snapshotBytes = ReadAndValidateSnapshot(store, request.ExecutionInput.InstallationSnapshot);
        if (Hash(snapshotBytes) != request.ExecutionInput.InstallationSnapshot.Fingerprint.Value)
        {
            throw new AnalysisIdentityDriftException(
                "The completed analysis operation substituted its installation snapshot fingerprint.");
        }
        byte[] bethesdaBytes = store.ReadCandidateAnalysisPayload(
            request.ExecutionInput.BethesdaSemanticInput.ArtifactId.Value);
        if (Hash(bethesdaBytes) != request.ExecutionInput.BethesdaSemanticInput.Fingerprint.Value)
        {
            throw new AnalysisIdentityDriftException(
                "The completed analysis operation substituted its retained semantic input.");
        }
        SemanticAnalysisContextIdentity.Validate(request.AnalysisContext);
        _ = RunOutputJsonCodec.Deserialize(store.ReadAnalysisRunOutput(sourceRun.RunId));
    }

    private static byte[] ReadAndValidateSnapshot(
        AuthoritativeStore store,
        ArtifactReferenceContract reference)
    {
        if (reference.Availability != "retained")
        {
            throw new InvalidOperationException("The installation snapshot is not retained.");
        }
        byte[] bytes = store.ReadPublishedSnapshotPayload(reference.ArtifactId.Value, MaximumSnapshotBytes);
        Mo2SnapshotCaptureResult result = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(bytes)
            ?? throw new InvalidDataException("The retained installation snapshot is malformed.");
        if (result.State is not (SnapshotCaptureState.Completed or SnapshotCaptureState.CompletedWithGaps)
            || result.Snapshot?.Contract.SnapshotId != reference.ArtifactId)
        {
            throw new AnalysisIdentityDriftException(
                "The retained installation snapshot identity or completion state is incompatible.");
        }
        return bytes;
    }

    private static ManagedAnalysisOrchestrationRequest DeserializeOperation(RunOperationRecord operation)
    {
        if (Hash(operation.RequestJson) != operation.RequestSha256)
        {
            throw new AnalysisIdentityDriftException(
                "The retained managed analysis operation failed identity validation.");
        }
        return JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
            operation.RequestJson,
            ContractJsonSerializer.Options)
            ?? throw new InvalidDataException("The retained managed analysis operation is malformed.");
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

    private static string Hash(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
