using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Infinium.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infinium.Tests;

#pragma warning disable CA1416 // The owning integration scenario is explicitly Windows-only.

internal sealed record PreparedAnalysisFixtureIdentity(
    string InstallationSnapshotId,
    string AnalysisContextId,
    string ResolvedInputManifestId,
    string SourceRunId,
    string SnapshotSha256);

internal static class PreparedAnalysisTestFixture
{
    public static async Task<PreparedAnalysisFixtureIdentity> SeedAsync(
        string productRoot,
        string mo2Root,
        string profileName)
    {
        using AuthoritativeStore store = new(new StoragePaths(productRoot));
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "prepared-analysis-fixture",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        const string snapshotId = "snapshot-local";
        const string contextId = "context-local";
        const string manifestId = "manifest-local";
        Mo2SnapshotCaptureResult snapshot = Snapshot(snapshotId, mo2Root, profileName);
        byte[] snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        string snapshotSha = Hash(snapshotBytes);
        PublishSnapshot(store, authority, snapshot, snapshotBytes, snapshotSha);

        RunBinding semanticBinding = new(snapshotId, contextId, "semantic-seed", manifestId);
        RunRecord semanticRun = store.CreateRun(
            "command-semantic-prepared-source",
            "run-semantic-prepared-source",
            semanticBinding,
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        _ = store.Transition(
            "transition-semantic-prepared-source",
            semanticRun.RunId,
            semanticRun.Generation,
            LifecycleState.Running,
            authority.FencingEpoch,
            "retain semantic input",
            DateTimeOffset.UtcNow);
        AttemptRecord semanticAttempt = store.CreateAttempt(
            semanticRun.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);
        using AttemptStagingAuthority semanticStaging =
            store.Paths.CreateAttemptStagingDirectory(semanticAttempt.AttemptId);
        BethesdaSemanticSnapshot semantic = new(
            new OpaqueId(snapshotId),
            BethesdaSemanticContract.SchemaVersion,
            BethesdaSemanticExtractor.ProducerId,
            BethesdaSemanticExtractor.ProducerVersion,
            new Sha256Fingerprint(snapshotSha),
            [],
            new Dictionary<string, BethesdaOverrideChain>(),
            new Dictionary<string, BethesdaRecordContribution>(),
            [], [], [], [],
            new Dictionary<string, BethesdaResolvedParticipant>(),
            new Dictionary<string, BethesdaNpcFact>(),
            new Dictionary<string, BethesdaRaceFact>(),
            new Dictionary<string, BethesdaPlacedReferenceFact>(),
            [],
            new Dictionary<string, IReadOnlyList<BethesdaLinkFact>>(),
            [], [], [], []);
        byte[] semanticBytes = JsonSerializer.SerializeToUtf8Bytes(
            new BethesdaSemanticExtractionResult(BethesdaExtractionState.Completed, semantic, [], []));
        string semanticSha = Hash(semanticBytes);
        const string semanticName = "bethesda.json";
        File.WriteAllBytes(
            Path.Combine(store.Paths.Staging, semanticAttempt.AttemptId, semanticName),
            semanticBytes);
        PayloadAdmission semanticAdmission = store.AdmitStagedPayload(
            semanticAttempt,
            semanticName,
            semanticSha,
            semanticBytes.LongLength,
            new string('2', 64),
            semanticBytes.LongLength,
            DateTimeOffset.UtcNow);
        semanticStaging.Dispose();
        store.SettleLiveAttempts(
            semanticRun.RunId,
            "semantic fixture retained",
            authority.FencingEpoch);
        RunRecord semanticClosed = store.GetRun(semanticRun.RunId);
        _ = store.Transition(
            "terminal-semantic-prepared-source",
            semanticRun.RunId,
            semanticClosed.Generation,
            LifecycleState.Failed,
            authority.FencingEpoch,
            "fixture producer retained its exact semantic payload",
            DateTimeOffset.UtcNow);

        const string sourceRunId = "run-retained-analysis-source";
        RunBinding sourceBinding = new(snapshotId, contextId, "source-effective", manifestId);
        ManagedAnalysisOrchestrationRequest sourceRequest = ManagedRequest(
            sourceRunId,
            sourceBinding,
            snapshotSha,
            semanticAdmission.PayloadId,
            semanticSha);
        RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
            authority.InstanceId,
            authority.FencingEpoch,
            Environment.ProcessId,
            elevated: false,
            DateTimeOffset.UtcNow);
        WorkerBootstrapRegistry registry = new();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new CoordinatorRuntime(store, authority, descriptor));
        builder.Services.AddSingleton(registry);
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
            options.MaxSendMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
        });
        builder.WebHost.UseNamedPipes(options => options.CurrentUserOnly = true);
        builder.WebHost.ConfigureKestrel(options => options.ListenNamedPipe(descriptor.WorkerPipe, listen =>
        {
            listen.Protocols = HttpProtocols.Http2;
            listen.Use(next => connection =>
            {
                connection.Features.Set(new InfiniumPipeRoleFeature("worker", descriptor.WorkerPipe));
                return next(connection);
            });
        }));
        await using WebApplication app = builder.Build();
        app.MapGrpcService<WorkerGrpcService>();
        await app.StartAsync();
        ManagedRunExecutor executor = new(
            app.Services.GetRequiredService<CoordinatorRuntime>(),
            registry,
            app.Services.GetRequiredService<ILogger<ManagedRunExecutor>>());
        _ = executor.CreateManagedAnalysisRun(
            "command-retained-analysis-source",
            sourceRunId,
            sourceBinding,
            sourceRequest,
            "EvaluationHarness",
            DateTimeOffset.UtcNow.AddMinutes(2));
        executor.Schedule(sourceRunId);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        while (!LifecyclePolicy.IsTerminal(store.GetRun(sourceRunId).State))
        {
            await Task.Delay(25, timeout.Token);
        }
        if (store.GetRun(sourceRunId).State != LifecycleState.Completed)
        {
            throw new InvalidOperationException("The real retained-input fixture analysis did not complete.");
        }
        _ = executor.RetainCompletedPreparedAnalysisInput(sourceRunId, DateTimeOffset.UtcNow);
        await app.StopAsync();
        return new(snapshotId, contextId, manifestId, sourceRunId, snapshotSha);
    }

    private static ManagedAnalysisOrchestrationRequest ManagedRequest(
        string runId,
        RunBinding binding,
        string snapshotSha,
        string bethesdaPayloadId,
        string bethesdaSha)
    {
        SemanticAnalysisContextContract context = new(
            new OpaqueId(binding.AnalysisContextId),
            new ContractVersion(2, 1, 0),
            new Sha256Fingerprint(new string('0', 64)),
            [new OpaqueId("semantic-input-revision-prepared")],
            new Dictionary<string, string> { ["evidence-policy"] = "retained-local-only" });
        context = context with
        {
            CanonicalFingerprint = SemanticAnalysisContextIdentity.ComputeFingerprint(context),
        };
        ArtifactReferenceContract Reference(
            string id,
            string fingerprint,
            ContractVersion? version = null) => new(
                new OpaqueId(id),
                version ?? new ContractVersion(1, 0, 0),
                new Sha256Fingerprint(fingerprint),
                "retained");
        AnalyzerDeclarationContract declaration = new DeliveredIndexCandidatePopulationSource().Declaration;
        byte[] documentationSource = [];
        string documentationSha = Hash(documentationSource);
        AnalysisExecutionInputContract execution = new(
            ContractConstants.AnalysisExecutionInputSchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId("execution-" + runId),
            new OpaqueId(runId),
            Reference(binding.InstallationSnapshotId, snapshotSha, new ContractVersion(3, 0, 0)),
            Reference(bethesdaPayloadId, bethesdaSha, BethesdaSemanticContract.SchemaVersion),
            [Reference("docs-source-prepared", documentationSha)],
            [Reference(
                declaration.AnalyzerId,
                CandidateAnalysisIdentity.StructuralHash([JsonSerializer.Serialize(declaration)]).Value,
                declaration.AnalyzerVersion)],
            Reference(binding.EffectiveScanConfigurationId, new string('b', 64)),
            Reference(binding.ResolvedInputManifestId, new string('c', 64)),
            ReplayMode.Clean,
            null,
            17,
            new(1000, 2000, 1000, 1000, 120_000),
            NotUsedBoundaries())
        {
            AnalysisContext = new(
                context.ContextId,
                context.SchemaVersion,
                context.CanonicalFingerprint,
                "retained"),
        };
        DocumentationClaimImportManifestContract manifest = new(
            ContractConstants.DocumentationClaimImportSchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId("docs-source-prepared"),
            DocumentationSourceKind.Fixture,
            "prepared-fixture-v1",
            DocumentationSourceAvailability.Present,
            new Sha256Fingerprint(documentationSha),
            0,
            null,
            [],
            []);
        DocumentationImportRequestContract documentation = new(
            new OpaqueId(runId),
            new OpaqueId(runId),
            DocumentationImportMode.CleanImport,
            new OpaqueId("docs-closure-prepared"),
            new OpaqueId("docs-extractor-prepared"),
            new UtcTimestamp(DateTimeOffset.UnixEpoch),
            manifest,
            documentationSource,
            null,
            []);
        FindingCasePhaseParameters finding = new(
            PromotionPolicyId: new OpaqueId("promotion-prepared"),
            PromotionPolicyVersion: new ContractVersion(1, 0, 0),
            ReconciliationPolicyId: new OpaqueId("reconciliation-prepared"),
            ReconciliationPolicyVersion: new ContractVersion(1, 0, 0),
            ReconciliationActorId: new OpaqueId("actor-prepared"),
            AssessmentTime: new UtcTimestamp(DateTimeOffset.UnixEpoch),
            FindingEvidenceFacts: [],
            FindingRecommendationFacts: [],
            SharedCauseProofs: [],
            TaxonomySubjects: [],
            RetainedTaxonomyFacts: [],
            TaxonomyProjectionInputs: [],
            CoveragePopulationFacts: [new CoveragePopulationFactContract(
                new OpaqueId("coverage-population-prepared"),
                DeliveredIndexCandidatePopulationSource.Id,
                "population-prepared",
                "declared prepared candidates")],
            CoverageMemberFacts: [],
            CoverageFailureFacts: [],
            PriorFindings: [],
            PriorCases: [],
            ProducerCompatibilities: [],
            RelatedFindingFacts: [],
            Boundaries: NotUsedBoundaries());
        return new(
            ManagedAnalysisOrchestrationRequest.CurrentSchemaVersion,
            "managed-request-" + runId,
            execution,
            context,
            documentation,
            new CandidatePhaseParameters(
                new OpaqueId("population-prepared"),
                new OpaqueId("policy-prepared"),
                new OpaqueId("threshold-prepared"),
                CandidateExecutionLimits.Default),
            finding,
            new string('a', 40),
            DateTimeOffset.UtcNow,
            AnalysisTerminalOutcome.Completed,
            "offline retained-input analysis completed",
            192L * 1024 * 1024,
            AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes,
            100);
    }

    internal static Mo2SnapshotCaptureResult Snapshot(
        string snapshotId,
        string mo2Root,
        string profileName)
    {
        ExecutableIdentity executable = new(
            "fixture.exe", 1, new string('a', 64), "2.5.2-fixture", null, null, null);
        Mo2SnapshotDependencyManifest dependencies = new(
            new ContractVersion(1, 0, 0),
            new Sha256Fingerprint(new string('d', 64)),
            "infinium.mo2-static-reconstruction/v3",
            "mod-organizer-2",
            profileName,
            new RuntimeTargetContext("windows", "fixture", "skyrimse"),
            executable,
            executable,
            executable,
            [], [], [],
            [new("mods", Path.Combine(mo2Root, "mods"), "fixture-mods-root"),
                new("overwrite", Path.Combine(mo2Root, "overwrite"), "fixture-overwrite-root"),
                new("game-data", Path.Combine(mo2Root, "game", "Data"), "fixture-game-data-root")],
            [], []);
        InstallationSnapshotContract contract = new(
            new OpaqueId(snapshotId),
            new ContractVersion(3, 0, 0),
            new OpaqueId("mo2-instance-prepared"),
            new OpaqueId("mo2-profile-prepared"),
            new Sha256Fingerprint(new string('d', 64)),
            [],
            [],
            new UtcTimestamp(DateTimeOffset.UnixEpoch));
        ExecutableAdmission admission = new(
            AdmissionState.Accepted,
            "fixture-executable-manifest",
            executable,
            []);
        Mo2InstallationSnapshot snapshot = new(
            contract,
            "infinium.mo2-static-reconstruction/v3",
            mo2Root,
            Path.Combine(mo2Root, "profiles", profileName),
            profileName,
            admission,
            admission,
            admission,
            dependencies,
            [], [], [], [], [], [], [],
            false,
            false);
        return new(SnapshotCaptureState.Completed, snapshot, []);
    }

    internal static void PublishSnapshot(
        AuthoritativeStore store,
        CoordinatorAuthority authority,
        Mo2SnapshotCaptureResult capture,
        byte[] bytes,
        string sha256,
        string operationId = "snapshot-operation-prepared")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        const string request = "{}";
        SnapshotCaptureOperationRecord operation = store.CreateSnapshotCaptureOperation(
            operationId,
            operationId + "-command",
            request,
            Hash(Encoding.UTF8.GetBytes(request)),
            "EvaluationHarness",
            now.AddMinutes(1),
            authority.FencingEpoch,
            now);
        SnapshotCaptureAttemptRecord attempt = store.DispatchSnapshotCaptureAttempt(
            operation.OperationId,
            operation.Generation,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            now);
        using AttemptStagingAuthority staging =
            store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
        const string output = "mo2-snapshot.v3.json";
        File.WriteAllBytes(Path.Combine(store.Paths.Staging, attempt.AttemptId, output), bytes);
        _ = store.AdmitSnapshotCapturePayload(
            attempt,
            output,
            sha256,
            bytes.LongLength,
            new string('a', 64),
            64L * 1024 * 1024,
            capture.Snapshot!.Contract.SnapshotId.Value,
            "snapshot-capture-result",
            now);
    }

    private static ExecutionBoundaryContract[] NotUsedBoundaries() =>
    [
        new("provider", BoundaryUseState.NotUsed, "local-only"),
        new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
        new("nexus", BoundaryUseState.NotUsed, "local-only"),
        new("loot", BoundaryUseState.NotUsed, "local-only"),
    ];

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
