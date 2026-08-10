using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Grpc.Net.Client;
using Infinium.Analysis.Candidates;
using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.Evaluation;
using Infinium.Application.FindingCases;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Tests;

#pragma warning disable CA1416 // Analysis-v1 worker containment is exercised through the Windows named-pipe transport.

[TestClass]
public sealed partial class AnalysisReplayIntegrationTests
{
    private static readonly JsonSerializerOptions PrettyContractJson =
        new(ContractJsonSerializer.Options) { WriteIndented = true };

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    public async Task ManagedAnalysisProductPathExecutesDocumentationCandidateFindingCaseRecoversPhaseBoundariesAndPublishes()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-managed-analysis-{Guid.NewGuid():N}");
        StoragePaths paths = new(root);
        AuthoritativeStore? ownedStore = null;
        try
        {
            using AuthoritativeStore store = ownedStore = new(paths);
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "managed-analysis-integration", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
            RunBinding binding = new("snapshot-managed", "context-managed", "config-managed", "manifest-managed");

            RunRecord producerQueued = store.CreateRun("command-bethesda-managed", "run-bethesda-managed",
                binding, authority.FencingEpoch, DateTimeOffset.UtcNow);
            _ = store.Transition("transition-bethesda-managed", producerQueued.RunId, producerQueued.Generation,
                LifecycleState.Running, authority.FencingEpoch, "retain semantic input", DateTimeOffset.UtcNow);
            AttemptRecord producerAttempt = store.CreateAttempt(producerQueued.RunId, authority.FencingEpoch,
                TimeSpan.FromMinutes(2), DateTimeOffset.UtcNow);
            using AttemptStagingAuthority producerStaging = paths.CreateAttemptStagingDirectory(producerAttempt.AttemptId);
            BethesdaSemanticSnapshot semantic = new(
                new OpaqueId(binding.InstallationSnapshotId), BethesdaSemanticContract.SchemaVersion,
                BethesdaSemanticExtractor.ProducerId, BethesdaSemanticExtractor.ProducerVersion,
                new Sha256Fingerprint(new string('1', 64)), [],
                new Dictionary<string, BethesdaOverrideChain>(), new Dictionary<string, BethesdaRecordContribution>(),
                [], [], [], [], new Dictionary<string, BethesdaResolvedParticipant>(),
                new Dictionary<string, BethesdaNpcFact>(), new Dictionary<string, BethesdaRaceFact>(),
                new Dictionary<string, BethesdaPlacedReferenceFact>(), [],
                new Dictionary<string, IReadOnlyList<BethesdaLinkFact>>(), [], [], [], []);
            BethesdaSemanticExtractionResult semanticResult = new(BethesdaExtractionState.Completed, semantic, [], []);
            byte[] semanticBytes = JsonSerializer.SerializeToUtf8Bytes(semanticResult);
            string semanticSha = Convert.ToHexStringLower(SHA256.HashData(semanticBytes));
            File.WriteAllBytes(Path.Combine(paths.Staging, producerAttempt.AttemptId, "bethesda.json"), semanticBytes);
            PayloadAdmission semanticAdmission = store.AdmitStagedPayload(producerAttempt, "bethesda.json",
                semanticSha, semanticBytes.LongLength, new string('2', 64), semanticBytes.LongLength,
                DateTimeOffset.UtcNow);
            producerStaging.Dispose();
            store.SettleLiveAttempts(producerQueued.RunId, "semantic-seed-complete", authority.FencingEpoch);
            RunRecord closedProducer = store.GetRun(producerQueued.RunId);
            _ = store.Transition("terminal-bethesda-managed", producerQueued.RunId, closedProducer.Generation,
                LifecycleState.Failed, authority.FencingEpoch, "seed-only semantic producer closed", DateTimeOffset.UtcNow);

            const string runId = "run-managed-analysis";
            ManagedAnalysisOrchestrationRequest request = ManagedRequest(
                runId, binding, semanticAdmission.PayloadId, semanticSha);

            RuntimeDescriptor descriptor = RuntimeDescriptor.Create(authority.InstanceId, authority.FencingEpoch,
                Environment.ProcessId, elevated: false, DateTimeOffset.UtcNow);
            CoordinatorRuntime runtime = new(store, authority, descriptor);
            WorkerBootstrapRegistry registry = new();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(runtime);
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
            ManagedRunExecutor executor = new(runtime, registry,
                app.Services.GetRequiredService<ILogger<ManagedRunExecutor>>());
            RunRecord queued = executor.CreateManagedAnalysisRun("command-managed-analysis", runId,
                binding, request, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
            _ = store.Transition("transition-managed-analysis", runId, queued.Generation,
                LifecycleState.Running, authority.FencingEpoch, "execute managed phase graph", DateTimeOffset.UtcNow);
            AttemptRecord firstAttempt = store.CreateAttempt(runId, authority.FencingEpoch,
                TimeSpan.FromMinutes(2), DateTimeOffset.UtcNow);

            AnalysisV1WorkAssignment first = ManagedAnalysisOrchestrator.Execute(store, request, firstAttempt,
                binding, DateTimeOffset.UtcNow, () => false);
            Assert.AreEqual(3, first.PhaseExecutions.Count);
            Assert.IsTrue(first.PhaseExecutions.All(item => item.Disposition == "recomputed-invalidated"));
            string deliveredInputId = first.ExecutionInput.SourceInputs.Single(item =>
                request.ExecutionInput.SourceInputs.All(original => original.ArtifactId != item.ArtifactId)).ArtifactId.Value;
            Assert.IsNotNull(store.ReadAnalysisPhaseCheckpoint(runId, DocumentationEvidencePhase.PhaseId,
                first.PhaseExecutions[0].InputFingerprint));
            store.SettleLiveAttempts(runId, "simulated-coordinator-loss-after-finding_case", authority.FencingEpoch);
            Assert.ThrowsExactly<InvalidOperationException>(() => ManagedAnalysisOrchestrator.Execute(
                store, request, firstAttempt, binding, DateTimeOffset.UtcNow, () => false));

            executor.RecoverAtStartup();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
            while (!LifecyclePolicy.IsTerminal(store.GetRun(runId).State))
            {
                await Task.Delay(25, timeout.Token);
            }

            Assert.AreEqual(LifecycleState.Completed, store.GetRun(runId).State);
            RunOutputContract output = RunOutputJsonCodec.Deserialize(store.ReadAnalysisRunOutput(runId));
            Assert.AreEqual("context-managed", output.AnalysisContext.ArtifactId);
            Assert.AreEqual(request.AnalysisContext.SchemaVersion.ToString(), output.AnalysisContext.ArtifactVersion);
            Assert.AreEqual(request.AnalysisContext.CanonicalFingerprint.Value, output.AnalysisContext.Fingerprint);
            Assert.AreEqual(3, store.GetAnalysisReplay(runId).DependencyCount >= 3 ? 3 : 0);
            string documentationArtifactId = store.ListAnalysisArtifacts(runId, new HashSet<string>(),
                new HashSet<string>(), 100, AnalysisArtifactSortOrder.IdentityAscending, null)
                .Items.Single(item => item.Kind == "documentation-evidence").ArtifactId;
            IReadOnlyList<string> managedDocumentationDependencies = store.ListAnalysisDependencyIds(
                runId, documentationArtifactId, 256);
            Assert.Contains(request.AnalysisContext.ContextId.Value, managedDocumentationDependencies);
            Assert.DoesNotContain(deliveredInputId, managedDocumentationDependencies);

            const string retainedImportRunId = "run-managed-retained-import";
            DocumentationEvidenceContract retainedDocumentation = DocumentationEvidenceJsonCodec.Deserialize(
                store.ReadCandidateAnalysisPayload(first.DocumentationEvidence.PayloadId));
            const string substitutedRetainedRunId = "run-managed-retained-import-substituted";
            ManagedAnalysisOrchestrationRequest substitutedRetained = ManagedRequest(
                substitutedRetainedRunId, binding, semanticAdmission.PayloadId, semanticSha);
            substitutedRetained = substitutedRetained with
            {
                ExecutionInput = substitutedRetained.ExecutionInput with
                {
                    Mode = ReplayMode.Incremental,
                    PriorRunId = new OpaqueId(runId),
                    SourceInputs = substitutedRetained.ExecutionInput.SourceInputs.Append(
                        new ArtifactReferenceContract(retainedDocumentation.PayloadId,
                            retainedDocumentation.SchemaVersion, new Sha256Fingerprint(new string('f', 64)),
                            "retained")).ToArray(),
                },
                DocumentationImport = substitutedRetained.DocumentationImport with
                {
                    Mode = DocumentationImportMode.RetainedReuse,
                    SourceBytes = null,
                    RetainedEvidence = retainedDocumentation,
                },
            };
            Assert.ThrowsExactly<AnalysisIdentityDriftException>(() => executor.CreateManagedAnalysisRun(
                "command-managed-retained-import-substituted", substitutedRetainedRunId, binding,
                substitutedRetained, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5)));
            Assert.ThrowsExactly<KeyNotFoundException>(() => store.GetRun(substitutedRetainedRunId));
            ManagedAnalysisOrchestrationRequest retainedImport = ManagedRequest(
                retainedImportRunId, binding, semanticAdmission.PayloadId, semanticSha);
            retainedImport = retainedImport with
            {
                ExecutionInput = retainedImport.ExecutionInput with
                {
                    Mode = ReplayMode.Incremental,
                    PriorRunId = new OpaqueId(runId),
                },
                DocumentationImport = retainedImport.DocumentationImport with
                {
                    Mode = DocumentationImportMode.RetainedReuse,
                    SourceBytes = null,
                    RetainedEvidence = retainedDocumentation,
                },
            };
            _ = executor.CreateManagedAnalysisRun("command-managed-retained-import", retainedImportRunId,
                binding, retainedImport, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
            executor.Schedule(retainedImportRunId);
            while (!LifecyclePolicy.IsTerminal(store.GetRun(retainedImportRunId).State))
            {
                await Task.Delay(25, timeout.Token);
            }
            Assert.AreEqual(LifecycleState.Completed, store.GetRun(retainedImportRunId).State);
            string retainedDocumentationArtifactId = store.ListAnalysisArtifacts(retainedImportRunId,
                new HashSet<string>(), new HashSet<string>(), 100, AnalysisArtifactSortOrder.IdentityAscending, null)
                .Items.Single(item => item.Kind == "documentation-evidence").ArtifactId;
            Assert.Contains(retainedDocumentation.PayloadId.Value, store.ListAnalysisDependencyIds(
                retainedImportRunId, retainedDocumentationArtifactId, 256));

            foreach ((string equivalentRunId, ReplayMode equivalentMode) in new[]
                     {
                         ("run-managed-equivalent-incremental", ReplayMode.Incremental),
                         ("run-managed-equivalent-replay", ReplayMode.RetainedDownstreamReplay),
                     })
            {
                ManagedAnalysisOrchestrationRequest equivalent = ManagedRequest(
                    equivalentRunId, binding, semanticAdmission.PayloadId, semanticSha);
                equivalent = equivalent with
                {
                    ExecutionInput = equivalent.ExecutionInput with
                    {
                        Mode = equivalentMode,
                        PriorRunId = new OpaqueId(runId),
                    },
                    FindingCase = request.FindingCase,
                };
                _ = executor.CreateManagedAnalysisRun("command-" + equivalentRunId, equivalentRunId,
                    binding, equivalent, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
                executor.Schedule(equivalentRunId);
                while (!LifecyclePolicy.IsTerminal(store.GetRun(equivalentRunId).State))
                {
                    await Task.Delay(25, timeout.Token);
                }
                Assert.AreEqual(LifecycleState.Completed, store.GetRun(equivalentRunId).State);
                AnalysisReplayContract equivalentReplay = AnalysisReplayJsonCodec.Deserialize(
                    store.ReadAnalysisReplay(equivalentRunId));
                Assert.IsTrue(equivalentReplay.SemanticallyEquivalent);
                Assert.AreEqual(store.GetAnalysisSemanticFingerprint(runId),
                    store.GetAnalysisSemanticFingerprint(equivalentRunId));
            }

            foreach (string interruptedPhase in new[]
                     {
                         DocumentationEvidencePhase.PhaseId,
                         CandidateAnalysisPhase.PhaseId,
                     })
            {
                string interruptedRunId = "run-managed-boundary-" + interruptedPhase[^4..];
                ManagedAnalysisOrchestrationRequest interrupted = ManagedRequest(
                    interruptedRunId, binding, semanticAdmission.PayloadId, semanticSha);
                RunRecord interruptedQueued = executor.CreateManagedAnalysisRun(
                    "command-" + interruptedRunId, interruptedRunId, binding, interrupted,
                    "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
                _ = store.Transition("transition-" + interruptedRunId, interruptedRunId,
                    interruptedQueued.Generation, LifecycleState.Running, authority.FencingEpoch,
                    "simulate phase-boundary loss", DateTimeOffset.UtcNow);
                AttemptRecord interruptedAttempt = store.CreateAttempt(interruptedRunId,
                    authority.FencingEpoch, TimeSpan.FromMinutes(2), DateTimeOffset.UtcNow);
                Assert.ThrowsExactly<InvalidOperationException>(() => ManagedAnalysisOrchestrator.Execute(
                    store, interrupted, interruptedAttempt, binding, DateTimeOffset.UtcNow, () => false,
                    completedPhase =>
                    {
                        if (completedPhase == interruptedPhase)
                        {
                            throw new InvalidOperationException("simulated coordinator loss at phase boundary");
                        }
                    }));
                store.SettleLiveAttempts(interruptedRunId, "simulated-phase-boundary-loss", authority.FencingEpoch);
                executor.RecoverAtStartup();
                while (!LifecyclePolicy.IsTerminal(store.GetRun(interruptedRunId).State))
                {
                    await Task.Delay(25, timeout.Token);
                }
                Assert.AreEqual(LifecycleState.Completed, store.GetRun(interruptedRunId).State);
            }

            const string expiredRestartRunId = "run-managed-restart-near-expiry";
            ManagedAnalysisOrchestrationRequest expiredRestart = ManagedRequest(
                expiredRestartRunId, binding, semanticAdmission.PayloadId, semanticSha);
            expiredRestart = expiredRestart with
            {
                ExecutionInput = expiredRestart.ExecutionInput with
                {
                    Limits = expiredRestart.ExecutionInput.Limits with { MaximumWallTimeMilliseconds = 750 },
                },
            };
            RunRecord expiredRestartQueued = executor.CreateManagedAnalysisRun(
                "command-managed-restart-near-expiry", expiredRestartRunId, binding, expiredRestart,
                "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
            _ = store.Transition("start-managed-restart-near-expiry", expiredRestartRunId,
                expiredRestartQueued.Generation, LifecycleState.Running, authority.FencingEpoch,
                "simulate restart after retained documentation evidence near deadline", DateTimeOffset.UtcNow);
            AttemptRecord expiredRestartAttempt = store.CreateAttempt(expiredRestartRunId,
                authority.FencingEpoch, TimeSpan.FromMinutes(2), DateTimeOffset.UtcNow);
            Assert.ThrowsExactly<InvalidOperationException>(() => ManagedAnalysisOrchestrator.Execute(
                store, expiredRestart, expiredRestartAttempt, binding, DateTimeOffset.UtcNow, () => false,
                completedPhase =>
                {
                    if (completedPhase == DocumentationEvidencePhase.PhaseId)
                    {
                        throw new InvalidOperationException("simulated restart near immutable deadline");
                    }
                }));
            store.SettleLiveAttempts(expiredRestartRunId, "simulated-near-expiry-restart", authority.FencingEpoch);
            await Task.Delay(800, timeout.Token);
            executor.RecoverAtStartup();
            while (!LifecyclePolicy.IsTerminal(store.GetRun(expiredRestartRunId).State))
            {
                await Task.Delay(25, timeout.Token);
            }
            Assert.AreEqual(LifecycleState.LimitReached, store.GetRun(expiredRestartRunId).State);
            Assert.IsNotNull(store.ReadLatestAnalysisPhaseCheckpoint(
                expiredRestartRunId, DocumentationEvidencePhase.PhaseId));
            Assert.IsNull(store.ReadLatestAnalysisPhaseCheckpoint(
                expiredRestartRunId, CandidateAnalysisPhase.PhaseId));

            const string incrementalRunId = "run-managed-analysis-incremental";
            ManagedAnalysisOrchestrationRequest incremental = ManagedRequest(
                incrementalRunId, binding, semanticAdmission.PayloadId, semanticSha);
            incremental = incremental with
            {
                ExecutionInput = incremental.ExecutionInput with
                {
                    Mode = ReplayMode.Incremental,
                    PriorRunId = new OpaqueId(runId),
                },
                FindingCase = incremental.FindingCase with
                {
                    ReconciliationPolicyId = new OpaqueId("reconciliation-managed-v2"),
                },
            };
            _ = executor.CreateManagedAnalysisRun("command-managed-analysis-incremental", incrementalRunId,
                binding, incremental, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
            executor.Schedule(incrementalRunId);
            while (!LifecyclePolicy.IsTerminal(store.GetRun(incrementalRunId).State))
            {
                await Task.Delay(25, timeout.Token);
            }
            Assert.AreEqual(LifecycleState.Completed, store.GetRun(incrementalRunId).State);
            AnalysisPhaseCheckpointRecord incrementalDocs = store.ReadLatestAnalysisPhaseCheckpoint(
                incrementalRunId, DocumentationEvidencePhase.PhaseId)!;
            AnalysisPhaseCheckpointRecord incrementalCandidates = store.ReadLatestAnalysisPhaseCheckpoint(
                incrementalRunId, CandidateAnalysisPhase.PhaseId)!;
            AnalysisPhaseCheckpointRecord incrementalFindings = store.ReadLatestAnalysisPhaseCheckpoint(
                incrementalRunId, FindingCaseAnalysisPhase.PhaseId)!;
            Assert.AreEqual("reused-retained-phase", incrementalDocs.Disposition);
            Assert.AreEqual(runId, incrementalDocs.SourceRunId);
            Assert.AreEqual(first.DocumentationEvidence.PayloadId, incrementalDocs.PayloadId);
            Assert.AreEqual("recomputed-run-binding", incrementalCandidates.Disposition);
            Assert.AreEqual("recomputed-invalidated", incrementalFindings.Disposition);

            ManagedAnalysisOrchestrationRequest unavailable = ManagedRequest(
                "run-managed-analysis-unavailable", binding, semanticAdmission.PayloadId, semanticSha);
            unavailable = unavailable with
            {
                ExecutionInput = unavailable.ExecutionInput with
                {
                    BethesdaSemanticInput = unavailable.ExecutionInput.BethesdaSemanticInput with
                    {
                        Availability = "unavailable",
                    },
                },
            };
            Assert.ThrowsExactly<AnalysisIdentityDriftException>(() => executor.CreateManagedAnalysisRun(
                "command-run-managed-analysis-unavailable", "run-managed-analysis-unavailable",
                binding, unavailable, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5)));
            Assert.ThrowsExactly<KeyNotFoundException>(() => store.GetRun("run-managed-analysis-unavailable"));

            foreach ((string driftRunId, string defect) in new[]
                     {
                         ("run-managed-analysis-drift", "fingerprint"),
                         ("run-managed-analysis-missing", "physical-missing"),
                     })
            {
                ManagedAnalysisOrchestrationRequest drift = ManagedRequest(
                    driftRunId, binding, semanticAdmission.PayloadId, semanticSha);
                drift = drift with
                {
                    ExecutionInput = drift.ExecutionInput with
                    {
                        BethesdaSemanticInput = drift.ExecutionInput.BethesdaSemanticInput with
                        {
                            ArtifactId = defect == "physical-missing"
                                ? new OpaqueId("missing-bethesda-payload")
                                : drift.ExecutionInput.BethesdaSemanticInput.ArtifactId,
                            Fingerprint = defect == "fingerprint"
                                ? new Sha256Fingerprint(new string('f', 64))
                                : drift.ExecutionInput.BethesdaSemanticInput.Fingerprint,
                            Availability = "retained",
                        },
                    },
                };
                _ = executor.CreateManagedAnalysisRun("command-" + driftRunId, driftRunId,
                    binding, drift, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
                executor.Schedule(driftRunId);
                while (!LifecyclePolicy.IsTerminal(store.GetRun(driftRunId).State))
                {
                    await Task.Delay(25, timeout.Token);
                }
                Assert.AreEqual(LifecycleState.InvalidatedByChangedInput, store.GetRun(driftRunId).State);
            }

            const string limitRunId = "run-managed-analysis-limit";
            ManagedAnalysisOrchestrationRequest limited = ManagedRequest(
                limitRunId, binding, semanticAdmission.PayloadId, semanticSha);
            limited = limited with
            {
                ExecutionInput = limited.ExecutionInput with
                {
                    Limits = limited.ExecutionInput.Limits with { MaximumWallTimeMilliseconds = 1 },
                },
            };
            _ = executor.CreateManagedAnalysisRun("command-managed-analysis-limit", limitRunId,
                binding, limited, "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
            executor.Schedule(limitRunId);
            while (!LifecyclePolicy.IsTerminal(store.GetRun(limitRunId).State))
            {
                await Task.Delay(25, timeout.Token);
            }
            Assert.AreEqual(LifecycleState.LimitReached, store.GetRun(limitRunId).State);
            Assert.AreEqual("context-managed",
                RunOutputJsonCodec.Deserialize(store.ReadAnalysisRunOutput(limitRunId)).AnalysisContext.ArtifactId);

            const string liveCancelledRunId = "run-managed-analysis-live-cancelled";
            ManagedAnalysisOrchestrationRequest liveCancelled = ManagedRequest(
                liveCancelledRunId, binding, semanticAdmission.PayloadId, semanticSha);
            RunRecord liveCancelledQueued = executor.CreateManagedAnalysisRun(
                "command-managed-analysis-live-cancelled", liveCancelledRunId, binding, liveCancelled,
                "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
            _ = store.Transition("start-managed-analysis-live-cancelled", liveCancelledRunId,
                liveCancelledQueued.Generation, LifecycleState.Running, authority.FencingEpoch,
                "exercise live cancellation boundary", DateTimeOffset.UtcNow);
            AttemptRecord liveCancelledAttempt = store.CreateAttempt(liveCancelledRunId,
                authority.FencingEpoch, TimeSpan.FromMinutes(2), DateTimeOffset.UtcNow);
            Assert.ThrowsExactly<WorkerStoppedAtSafeBoundaryException>(() => ManagedAnalysisOrchestrator.Execute(
                store, liveCancelled, liveCancelledAttempt, binding, DateTimeOffset.UtcNow,
                () => store.GetRun(liveCancelledRunId).State == LifecycleState.Cancelling,
                completedPhase =>
                {
                    if (completedPhase == DocumentationEvidencePhase.PhaseId)
                    {
                        RunRecord running = store.GetRun(liveCancelledRunId);
                        _ = store.Transition("cancel-managed-analysis-live", liveCancelledRunId,
                            running.Generation, LifecycleState.Cancelling, authority.FencingEpoch,
                            "cancel after retained documentation evidence boundary", DateTimeOffset.UtcNow);
                    }
                }));
            Assert.IsNotNull(store.ReadLatestAnalysisPhaseCheckpoint(
                liveCancelledRunId, DocumentationEvidencePhase.PhaseId));
            Assert.IsNull(store.ReadLatestAnalysisPhaseCheckpoint(
                liveCancelledRunId, CandidateAnalysisPhase.PhaseId));
            Assert.IsNull(store.ReadLatestAnalysisPhaseCheckpoint(
                liveCancelledRunId, FindingCaseAnalysisPhase.PhaseId));
            executor.RecoverAtStartup();
            Assert.AreEqual(LifecycleState.Cancelled, store.GetRun(liveCancelledRunId).State);
            AnalysisReplayContract liveCancelledReplay = AnalysisReplayJsonCodec.Deserialize(
                store.ReadAnalysisReplay(liveCancelledRunId));
            Assert.AreEqual(AnalysisResultState.Present, liveCancelledReplay.Dependencies.Single(item =>
                item.Kind == "documentation-evidence").State);
            Assert.IsTrue(liveCancelledReplay.Dependencies.Where(item =>
                item.Kind is "candidate-analysis" or "finding-case").All(item =>
                    item.State == AnalysisResultState.Unavailable));
            AnalysisArtifactPersistenceRecord[] cancelledArtifacts = store.ListAnalysisArtifacts(
                liveCancelledRunId, new HashSet<string>(), new HashSet<string>(), 100,
                AnalysisArtifactSortOrder.IdentityAscending, null).Items.ToArray();
            Assert.HasCount(5, cancelledArtifacts);
            foreach (AnalysisArtifactPersistenceRecord artifact in cancelledArtifacts)
            {
                Assert.Contains(liveCancelled.AnalysisContext.ContextId.Value,
                    store.ListAnalysisDependencyIds(liveCancelledRunId, artifact.ArtifactId, 256));
            }

            const string cancelledRunId = "run-managed-analysis-cancelled";
            ManagedAnalysisOrchestrationRequest cancelled = ManagedRequest(
                cancelledRunId, binding, semanticAdmission.PayloadId, semanticSha);
            RunRecord cancelledQueued = executor.CreateManagedAnalysisRun(
                "command-managed-analysis-cancelled", cancelledRunId, binding, cancelled,
                "EvaluationHarness", DateTimeOffset.UtcNow.AddMinutes(5));
            _ = store.Transition("cancel-managed-analysis", cancelledRunId, cancelledQueued.Generation,
                LifecycleState.Cancelling, authority.FencingEpoch, "cancel before dispatch", DateTimeOffset.UtcNow);
            executor.RecoverAtStartup();
            Assert.AreEqual(LifecycleState.Cancelled, store.GetRun(cancelledRunId).State);
            Assert.AreEqual("cancelled",
                RunOutputJsonCodec.Deserialize(store.ReadAnalysisRunOutput(cancelledRunId)).RunState);
            await executor.WaitForIdleAsync(timeout.Token);
            await app.StopAsync();
        }
        finally
        {
            ownedStore?.Dispose();
            paths.Dispose();
            if (Directory.Exists(root))
            {
                for (int attempt = 0; attempt < 80 && Directory.Exists(root); attempt++)
                {
                    try
                    {
                        Directory.Delete(root, recursive: true);
                    }
                    catch (IOException)
                    {
                        if (attempt < 79)
                        {
                            Thread.Sleep(25);
                        }
                    }
                }
            }
        }
    }

    private static ManagedAnalysisOrchestrationRequest ManagedRequest(
        string runId, RunBinding binding, string bethesdaPayloadId, string bethesdaSha)
    {
        SemanticAnalysisContextContract context = new(new OpaqueId(binding.AnalysisContextId),
            new ContractVersion(2, 1, 0), new Sha256Fingerprint(new string('0', 64)),
            [new OpaqueId("semantic-input-revision-managed")],
            new Dictionary<string, string> { ["evidence-policy"] = "retained-local-only" });
        context = context with { CanonicalFingerprint = SemanticAnalysisContextIdentity.ComputeFingerprint(context) };
        ArtifactReferenceContract Reference(string id, char fingerprint = 'a') => new(new OpaqueId(id),
            new ContractVersion(1, 0, 0), new Sha256Fingerprint(new string(fingerprint, 64)), "retained");
        AnalyzerDeclarationContract declaration = new DeliveredIndexCandidatePopulationSource().Declaration;
        byte[] source = [];
        Sha256Fingerprint sourceFingerprint = new(Convert.ToHexStringLower(SHA256.HashData(source)));
        AnalysisExecutionInputContract execution = new(
            ContractConstants.AnalysisExecutionInputSchemaId, new ContractVersion(1, 0, 0),
            new OpaqueId("execution-" + runId), new OpaqueId(runId), Reference(binding.InstallationSnapshotId),
            new ArtifactReferenceContract(new OpaqueId(bethesdaPayloadId), BethesdaSemanticContract.SchemaVersion,
                new Sha256Fingerprint(bethesdaSha), "retained"),
            [new ArtifactReferenceContract(new OpaqueId("docs-source-managed"), new ContractVersion(1, 0, 0),
                sourceFingerprint, "retained")],
            [new ArtifactReferenceContract(new OpaqueId(declaration.AnalyzerId), declaration.AnalyzerVersion,
                CandidateAnalysisIdentity.StructuralHash([JsonSerializer.Serialize(declaration)]), "retained")],
            Reference(binding.EffectiveScanConfigurationId, 'b'), Reference(binding.ResolvedInputManifestId, 'c'),
            ReplayMode.Clean, null, 17, new(1000, 2000, 1000, 1000, 120_000), NotUsedBoundaries())
        {
            AnalysisContext = new(context.ContextId, context.SchemaVersion, context.CanonicalFingerprint, "retained"),
        };
        DocumentationClaimImportManifestContract manifest = new(
            ContractConstants.DocumentationClaimImportSchemaId, new ContractVersion(1, 0, 0),
            new OpaqueId("docs-source-managed"), DocumentationSourceKind.Fixture, "managed-v1",
            DocumentationSourceAvailability.Present, sourceFingerprint,
            0, null, [], []);
        DocumentationImportRequestContract docs = new(new OpaqueId(runId), new OpaqueId(runId),
            DocumentationImportMode.CleanImport, new OpaqueId("docs-closure-managed"),
            new OpaqueId("docs-extractor-managed"), new UtcTimestamp(DateTimeOffset.UnixEpoch), manifest,
            source, null, []);
        FindingCasePhaseParameters finding = new(
            PromotionPolicyId: new OpaqueId("promotion-managed"),
            PromotionPolicyVersion: new ContractVersion(1, 0, 0),
            ReconciliationPolicyId: new OpaqueId("reconciliation-managed"),
            ReconciliationPolicyVersion: new ContractVersion(1, 0, 0),
            ReconciliationActorId: new OpaqueId("actor-managed"),
            AssessmentTime: new UtcTimestamp(DateTimeOffset.UnixEpoch),
            FindingEvidenceFacts: [], FindingRecommendationFacts: [], SharedCauseProofs: [],
            TaxonomySubjects: [], RetainedTaxonomyFacts: [], TaxonomyProjectionInputs: [],
            CoveragePopulationFacts:
            [
                new CoveragePopulationFactContract(new OpaqueId("coverage-population-managed"),
                    DeliveredIndexCandidatePopulationSource.Id, "population-managed", "declared managed candidates"),
            ],
            CoverageMemberFacts: [], CoverageFailureFacts: [], PriorFindings: [], PriorCases: [],
            ProducerCompatibilities: [], RelatedFindingFacts: [], Boundaries: NotUsedBoundaries());
        return new(1, "managed-request-" + runId, execution, context, docs,
            new CandidatePhaseParameters(new OpaqueId("population-managed"), new OpaqueId("policy-managed"),
                new OpaqueId("threshold-managed"), CandidateExecutionLimits.Default),
            finding, new string('a', 40), DateTimeOffset.UtcNow, AnalysisTerminalOutcome.Completed,
            "managed analysis product path completed", 192L * 1024 * 1024,
            AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes, 100);
    }

    private static ExecutionBoundaryContract[] NotUsedBoundaries() =>
    [
        new("provider", BoundaryUseState.NotUsed, "local-only"),
        new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
        new("nexus", BoundaryUseState.NotUsed, "local-only"),
        new("loot", BoundaryUseState.NotUsed, "local-only"),
    ];
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Replay")]
    public void AnalysisReplayPublishesDocumentationThroughFindingCaseAtomicallyAndSurvivesBackupRestore()
    {
        using OperationalContext context = new();
        AnalysisExecutionPhaseResult published = context.Publish();

        Assert.AreEqual(LifecycleState.Completed, context.Store.GetRun(context.RunId).State);
        Assert.AreEqual(published.Bundle.SemanticOutputFingerprint, context.Store.GetAnalysisSemanticFingerprint(context.RunId));
        CollectionAssert.AreEqual(
            RunOutputJsonCodec.Serialize(published.Bundle.RunOutput),
            context.Store.ReadAnalysisRunOutput(context.RunId));
        Assert.IsNotEmpty(published.Bundle.RunOutput.ExternalClaims);
        Assert.IsNotEmpty(published.Bundle.RunOutput.ApplicationLinks);
        Assert.IsNotEmpty(published.Bundle.RunOutput.DeterministicResults);
        Assert.IsTrue(published.Bundle.RunOutput.TaxonomyAssignments.Any(item => item.SubjectType == "installed-entity"));
        Assert.AreEqual(published.Bundle.Artifacts.Count,
            context.Store.ListAnalysisArtifacts(
                context.RunId, new HashSet<string>(), new HashSet<string>(), 100,
                AnalysisArtifactSortOrder.IdentityAscending, null).Items.Count);
        Assert.IsTrue(published.Bundle.Replay.Edges.Any(item => item.From.Value
            == published.Bundle.Artifacts.Single(artifact => artifact.Kind == "documentation-evidence").ArtifactId),
            "Replay edge roots: " + string.Join(",", published.Bundle.Replay.Edges.Select(item => item.From.Value).Distinct()));
        IReadOnlyList<string> documentationDependencies = context.Store.ListAnalysisDependencyIds(
            context.RunId, published.Bundle.Artifacts.Single(item => item.Kind == "documentation-evidence").ArtifactId, 256);
        IReadOnlyList<string> candidateDependencies = context.Store.ListAnalysisDependencyIds(
            context.RunId, published.Bundle.Artifacts.Single(item => item.Kind == "candidate-analysis").ArtifactId, 256);
        IReadOnlyList<string> findingDependencies = context.Store.ListAnalysisDependencyIds(
            context.RunId, published.Bundle.Artifacts.Single(item => item.Kind == "finding-case").ArtifactId, 256);
        IReadOnlyList<string> executionDependencies = context.Store.ListAnalysisDependencyIds(
            context.RunId, context.Assignment.ExecutionInput.ExecutionInputId.Value, 256);
        Assert.IsTrue(documentationDependencies.Contains(context.Assignment.AnalysisContext.ContextId.Value),
            "Documentation provenance closure: " + string.Join(",", documentationDependencies));
        Assert.Contains(published.Bundle.Artifacts.Single(item => item.Kind == "documentation-evidence").ArtifactId,
            candidateDependencies);
        Assert.Contains(published.Bundle.Artifacts.Single(item => item.Kind == "candidate-analysis").ArtifactId,
            findingDependencies);
        Assert.Contains(context.Assignment.AnalysisContext.ContextId.Value, executionDependencies);
        Assert.DoesNotContain(published.Bundle.Artifacts.Single(item => item.Kind == "finding-case").ArtifactId,
            candidateDependencies);
        Assert.DoesNotContain(published.Bundle.Replay.ReplayManifestId.Value, candidateDependencies);
        HashSet<string> retainedReplayNodes = published.Bundle.Replay.Dependencies
            .Select(item => item.DependencyId.Value).ToHashSet(StringComparer.Ordinal);
        foreach (AnalysisPublishedArtifact artifact in published.Bundle.Artifacts)
        {
            string[] absentNodes = context.Store.ListAnalysisDependencyIds(context.RunId, artifact.ArtifactId, 256)
                .Where(id => !retainedReplayNodes.Contains(id)).ToArray();
            Assert.IsEmpty(absentNodes, $"{artifact.Kind} absent replay nodes: {string.Join(',', absentNodes)}");
        }
        Assert.IsTrue(context.Store.ReadAnalysisBoundaryReceipt(context.RunId)
            .AsSpan().IndexOf("not-used"u8) >= 0);

        BackupArtifact backup = context.Store.CreateBackup("operations-replay", DateTimeOffset.UtcNow);
        string restoreRoot = Path.Combine(Path.GetTempPath(), "infinium-operations-restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            using StoragePaths restoredPaths = new(restoreRoot);
            AuthoritativeStore.RestoreBackup(backup, restoredPaths);
            using AuthoritativeStore restored = new(restoredPaths);
            CollectionAssert.AreEqual(context.Store.ReadAnalysisRunOutput(context.RunId), restored.ReadAnalysisRunOutput(context.RunId));
            Assert.AreEqual(0, restored.ReconcilePayloadStore().Count);
            restored.RebuildProjections(DateTimeOffset.UtcNow);
            Assert.AreEqual("1", restored.GetAnalysisSummary(context.RunId).ProjectionVersion);
        }
        finally
        {
            if (Directory.Exists(restoreRoot))
            {
                Directory.Delete(restoreRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void AnalysisFailureRecoveryRollsBackInjectedPublicationAndRejectsStaleAttempt()
    {
        using OperationalContext rollback = new();
        Assert.ThrowsExactly<InvalidOperationException>(() => rollback.Publish(
            point =>
            {
                if (point == "before-commit")
                {
                    throw new InvalidOperationException("injected atomic publication interruption");
                }
            }));
        Assert.AreEqual(LifecycleState.Running, rollback.Store.GetRun(rollback.RunId).State);
        Assert.IsNull(rollback.Store.GetAnalysisSemanticFingerprint(rollback.RunId));
        Assert.ThrowsExactly<KeyNotFoundException>(() => rollback.Store.GetAnalysisReplay(rollback.RunId));
        Assert.IsTrue(rollback.Store.ReconcilePayloadStore().Any(issue => issue.Kind == "orphan-payload"));

        AnalysisExecutionPhaseResult recovered = rollback.Publish();
        Assert.AreEqual(LifecycleState.Completed, recovered.Receipt.TerminalState);
        Assert.IsTrue(rollback.Store.ReconcilePayloadStore().Any(issue => issue.Kind == "orphan-payload"));
        CollectionAssert.AreEqual(
            RunOutputJsonCodec.Serialize(recovered.Bundle.RunOutput),
            rollback.Store.ReadAnalysisRunOutput(rollback.RunId));

        using OperationalContext stale = new();
        AttemptRecord old = stale.Attempt;
        _ = stale.Context.StartRecoveryAttempt(DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.ThrowsExactly<InvalidDataException>(() => stale.Publish(attempt: old));
        Assert.IsNull(stale.Store.GetAnalysisSemanticFingerprint(stale.RunId));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Replay")]
    public void AnalysisReplayFailsClosedOnDriftAndPaginatesDeterministically()
    {
        using OperationalContext context = new();
        AnalysisV1WorkAssignment drifted = context.Assignment with
        {
            CandidateAnalysis = context.Assignment.CandidateAnalysis with { Sha256 = new string('0', 64) },
        };
        Assert.ThrowsExactly<AnalysisIdentityDriftException>(() => context.Publish(assignment: drifted));
        Assert.IsNull(context.Store.GetAnalysisSemanticFingerprint(context.RunId));

        DocumentationEvidenceContract documentation = DocumentationEvidenceJsonCodec.Deserialize(
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.DocumentationEvidence.PayloadId));
        CandidateAnalysisContract candidates = CandidateAnalysisJsonCodec.Deserialize(
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.CandidateAnalysis.PayloadId));
        FindingCaseContract findings = FindingCaseJsonCodec.Deserialize(
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.FindingCase.PayloadId));

        AnalysisV1WorkAssignment WithDocumentationAlias(
            ContractVersion version,
            Sha256Fingerprint fingerprint,
            string availability)
        {
            ArtifactReferenceContract alias = new(
                documentation.PayloadId,
                version,
                fingerprint,
                availability);
            return context.Assignment with
            {
                ExecutionInput = context.Assignment.ExecutionInput with
                {
                    SourceInputs = [.. context.Assignment.ExecutionInput.SourceInputs, alias],
                },
            };
        }

        ContractVersion documentationVersion =
            documentation.SchemaVersion;
        Sha256Fingerprint documentationFingerprint =
            new(context.Assignment.DocumentationEvidence.Sha256);
        void AssertAliasRejected(AnalysisV1WorkAssignment aliasDrift, string driftKind) =>
            Assert.ThrowsExactly<AnalysisIdentityDriftException>(() =>
                AnalysisPublicationBuilder.BuildDependenciesForVerification(
                    aliasDrift, documentation, candidates, findings), driftKind);

        AssertAliasRejected(
            WithDocumentationAlias(documentationVersion, new Sha256Fingerprint(new string('0', 64)), "retained"),
            "documentation fingerprint drift must reach and fail the alias guard");
        AssertAliasRejected(
            WithDocumentationAlias(ContractVersion.Parse("2.0.0"), documentationFingerprint, "retained"),
            "documentation version drift must reach and fail the alias guard");
        AssertAliasRejected(
            WithDocumentationAlias(documentationVersion, documentationFingerprint, "unavailable"),
            "documentation retention drift must reach and fail the alias guard");
        Assert.IsNull(context.Store.GetAnalysisSemanticFingerprint(context.RunId));

        _ = context.Publish();
        AnalysisArtifactPagePersistenceRecord first = context.Store.ListAnalysisArtifacts(
            context.RunId, new HashSet<string>(), new HashSet<string>(), 2,
            AnalysisArtifactSortOrder.IdentityAscending, null);
        AnalysisArtifactPagePersistenceRecord second = context.Store.ListAnalysisArtifacts(
            context.RunId, new HashSet<string>(), new HashSet<string>(), 2,
            AnalysisArtifactSortOrder.IdentityAscending, first.NextKey);
        Assert.IsTrue(first.HasMore);
        Assert.IsTrue(first.Items[^1].ArtifactId.CompareTo(second.Items[0].ArtifactId, StringComparison.Ordinal) < 0);
        Assert.AreEqual(first.Items.Count + second.Items.Count + 1, context.Assignment.MaximumQueryItems > 0 ? 5 : 0);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Cli")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Cli")]
    public void AnalysisCliHumanAndJsonRepresentTheSameTerminalSemantics()
    {
        using OperationalContext context = new(AnalysisTerminalOutcome.LimitReached);
        AnalysisExecutionPhaseResult result = context.Publish();
        string human = AnalysisOutputRenderer.Render(result.Bundle.RunOutput, result.Bundle.CliSummary);

        Assert.AreEqual("limit-reached", result.Bundle.CliSummary.Outcome);
        Assert.AreEqual((int)CliExitCode.LimitReached, result.Bundle.CliSummary.ExitCode);
        Assert.AreEqual(LifecycleState.LimitReached, context.Store.GetRun(context.RunId).State);
        StringAssert.Contains(human, "state=limit-reached outcome=limit-reached");
        StringAssert.Contains(human, $"findings count={result.Bundle.RunOutput.Findings.Count}");
        StringAssert.Contains(human, "no-safety-guarantee=true");
        StringAssert.Contains(human, "provider:not-used");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    public void AnalysisReplayCleanIncrementalAndReplayPreserveUnchangedSemanticOutput()
    {
        using OperationalContext clean = new();
        AnalysisExecutionPhaseResult cleanResult = clean.Publish();

        AttemptRecord incrementalAttempt = clean.Context.CreateRunAttempt("run-operations-incremental", DateTimeOffset.UtcNow);
        using OperationalContext incremental = new(
            mode: ReplayMode.Incremental,
            priorRunId: new OpaqueId(clean.RunId),
            context: clean.Context,
            attempt: incrementalAttempt,
            priorFindingCase: clean.FindingCases);
        AnalysisExecutionPhaseResult incrementalResult = incremental.Publish();

        AttemptRecord replayAttempt = clean.Context.CreateRunAttempt("run-operations-replay", DateTimeOffset.UtcNow);
        using OperationalContext replay = new(
            mode: ReplayMode.RetainedDownstreamReplay,
            priorRunId: new OpaqueId(incremental.RunId),
            context: clean.Context,
            attempt: replayAttempt,
            priorFindingCase: incremental.FindingCases);
        AnalysisExecutionPhaseResult replayResult = replay.Publish();

        Assert.AreEqual(cleanResult.Bundle.SemanticOutputFingerprint, incrementalResult.Bundle.SemanticOutputFingerprint);
        Assert.AreEqual(cleanResult.Bundle.SemanticOutputFingerprint, replayResult.Bundle.SemanticOutputFingerprint);
        Assert.IsTrue(incrementalResult.Bundle.Replay.SemanticallyEquivalent);
        Assert.IsTrue(replayResult.Bundle.Replay.SemanticallyEquivalent);
        Assert.AreNotEqual(cleanResult.Bundle.DependencyClosureId, incrementalResult.Bundle.DependencyClosureId);
        Assert.AreNotEqual(incrementalResult.Bundle.DependencyClosureId, replayResult.Bundle.DependencyClosureId);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Replay")]
    public void UnavailableDependenciesAreExplicitAndPreventCompleteCleanReplayAndAudit()
    {
        using OperationalContext context = new(unavailableDependency: true);
        AnalysisExecutionPhaseResult result = context.Publish();

        Assert.AreEqual(ReplayState.Partial, result.Bundle.Replay.ReplayState);
        Assert.AreEqual(AuditabilityState.Partial, result.Bundle.Replay.AuditabilityState);
        CollectionAssert.Contains(
            result.Bundle.Replay.MissingDependencyIds.Select(item => item.Value).ToArray(),
            result.Bundle.Replay.Dependencies.Single(item => item.Kind == "bethesda-semantic-input").DependencyId.Value);
        Assert.IsTrue(result.Bundle.RunOutput.CoverageGaps.Any(item => item.State == "unavailable"));
        Assert.AreEqual("complete-with-gaps", result.Bundle.RunOutput.Auditability.State);
        Assert.AreEqual(LifecycleState.CompletedWithGaps, result.Receipt.TerminalState);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [DataRow(AnalysisTerminalOutcome.CompletedWithGaps, LifecycleState.CompletedWithGaps, "completed-with-gaps")]
    [DataRow(AnalysisTerminalOutcome.Cancelled, LifecycleState.Cancelled, "cancelled")]
    [DataRow(AnalysisTerminalOutcome.Failed, LifecycleState.Failed, "failed")]
    public void AnalysisReplayPublishesExplicitPartialCancelledAndFailureOutputs(
        AnalysisTerminalOutcome outcome,
        LifecycleState expectedState,
        string expectedToken)
    {
        using OperationalContext context = new(outcome);
        if (outcome == AnalysisTerminalOutcome.Cancelled)
        {
            RunRecord run = context.Store.GetRun(context.RunId);
            _ = context.Store.Transition(
                Guid.NewGuid().ToString("N"), context.RunId, run.Generation,
                LifecycleState.Cancelling, context.Attempt.CoordinatorFencingEpoch,
                "test cancellation at publication boundary", DateTimeOffset.UtcNow);
        }

        AnalysisExecutionPhaseResult result = context.Publish();

        Assert.AreEqual(expectedState, context.Store.GetRun(context.RunId).State);
        Assert.AreEqual(expectedToken, result.Bundle.RunOutput.RunState);
        Assert.AreEqual(expectedToken, result.Bundle.CliSummary.Outcome);
        CollectionAssert.AreEqual(
            RunOutputJsonCodec.Serialize(result.Bundle.RunOutput),
            context.Store.ReadAnalysisRunOutput(context.RunId));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void CoordinatorFallbackPublishesTerminalFailureAndLimitOutputsWithoutReexecutingSemanticProjection()
    {
        using OperationalContext failure = new();
        AnalysisExecutionPhaseResult failed = AnalysisExecutionPhase.PublishTerminalFallback(
            failure.Store, failure.Assignment, failure.Attempt, failure.Context.Binding,
            failure.ValidationReceiptPayloadId, AnalysisTerminalOutcome.Failed,
            "deterministic publication failure", DateTimeOffset.UtcNow);
        Assert.AreEqual(LifecycleState.Failed, failed.Receipt.TerminalState);
        Assert.HasCount(1, failed.Bundle.RunOutput.Failures);
        Assert.HasCount(1, failed.Bundle.RunOutput.DocumentationRevisions);
        Assert.HasCount(1, failed.Bundle.RunOutput.Candidates);
        Assert.HasCount(1, failed.Bundle.RunOutput.Findings);
        Assert.HasCount(0, failed.Bundle.RunOutput.CoverageGaps);
        Assert.IsTrue(failed.Bundle.RunOutput.AnalyzerCoverage.All(item => item.Status == "failed"));
        Assert.AreEqual("failed", failed.Bundle.CliSummary.Outcome);

        using OperationalContext limited = new();
        AnalysisExecutionPhaseResult limitOutput = AnalysisExecutionPhase.PublishTerminalFallback(
            limited.Store, limited.Assignment, limited.Attempt, limited.Context.Binding,
            limited.ValidationReceiptPayloadId, AnalysisTerminalOutcome.LimitReached,
            "output-item authority reached", DateTimeOffset.UtcNow);
        Assert.AreEqual(LifecycleState.LimitReached, limitOutput.Receipt.TerminalState);
        Assert.AreEqual("limit-reached", limitOutput.Bundle.CliSummary.Outcome);
        Assert.HasCount(1, limitOutput.Bundle.RunOutput.CoverageGaps);
        Assert.HasCount(0, limitOutput.Bundle.RunOutput.Failures);
        Assert.IsTrue(limitOutput.Bundle.RunOutput.AnalyzerCoverage.All(item => item.Status == "skipped-by-limit"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Limit")]
    public void TerminalFallbackReservesAndCountsEveryMandatoryItemForMultipleAnalyzers()
    {
        using OperationalContext context = new();
        ArtifactReferenceContract first = context.Assignment.ExecutionInput.AnalyzerDeclarations[0];
        AnalysisExecutionInputContract twoAnalyzerInput = context.Assignment.ExecutionInput with
        {
            AnalyzerDeclarations =
            [
                first,
                first with { ArtifactId = new OpaqueId(first.ArtifactId.Value + "-independent") },
            ],
        };
        AnalysisV1WorkAssignment insufficient = context.Assignment with
        {
            ExecutionInput = twoAnalyzerInput with
            {
                Limits = twoAnalyzerInput.Limits with { MaximumOutputItems = 5 },
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisPublicationBuilder.ValidateAssignment(insufficient));

        AnalysisV1WorkAssignment exact = insufficient with
        {
            ExecutionInput = insufficient.ExecutionInput with
            {
                Limits = insufficient.ExecutionInput.Limits with { MaximumOutputItems = 6 },
            },
        };
        AnalysisPublicationBundle fallback = AnalysisTerminalFallbackBuilder.Build(
            exact, AnalysisTerminalOutcome.Failed, "bounded multi-analyzer failure", DateTimeOffset.UtcNow);
        Assert.AreEqual(6, AnalysisPublicationBuilder.CountOutputItems(fallback.RunOutput));
        Assert.HasCount(2, fallback.RunOutput.AnalyzerCoverage);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    public void SemanticFingerprintChangesWhenDocumentationGraphMembershipIsSwapped()
    {
        using OperationalContext context = new();
        DocumentationEvidenceContract documentation = DocumentationEvidenceJsonCodec.Deserialize(
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.DocumentationEvidence.PayloadId));
        CandidateAnalysisContract candidates = CandidateAnalysisJsonCodec.Deserialize(
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.CandidateAnalysis.PayloadId));
        FindingCaseContract findings = FindingCaseJsonCodec.Deserialize(
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.FindingCase.PayloadId));

        DocumentationClaimContract[] passageSwappedClaims = documentation.Claims.ToArray();
        Assert.IsGreaterThanOrEqualTo(2, passageSwappedClaims.Length);
        (passageSwappedClaims[0], passageSwappedClaims[1]) =
        (
            passageSwappedClaims[0] with { PassageId = passageSwappedClaims[1].PassageId },
            passageSwappedClaims[1] with { PassageId = passageSwappedClaims[0].PassageId }
        );
        string baseline = AnalysisPublicationBuilder.SemanticFingerprintForVerification(
            documentation, candidates, findings);
        string passageMembershipSwap = AnalysisPublicationBuilder.SemanticFingerprintForVerification(
            documentation with { Claims = passageSwappedClaims }, candidates, findings);
        Assert.AreNotEqual(baseline, passageMembershipSwap);

        DocumentationImportContract originalImport = documentation.Imports[0];
        DocumentationImportContract independentImport = originalImport with
        {
            ImportId = new OpaqueId(originalImport.ImportId.Value + "-independent"),
            ExtractorId = new OpaqueId(originalImport.ExtractorId.Value + "-independent"),
        };
        DocumentationClaimContract[] importBaselineClaims = documentation.Claims.ToArray();
        importBaselineClaims[1] = importBaselineClaims[1] with { ProducingImportId = independentImport.ImportId };
        DocumentationEvidenceContract importBaseline = documentation with
        {
            Imports = [originalImport, independentImport],
            Claims = importBaselineClaims,
        };
        DocumentationClaimContract[] importSwappedClaims = importBaseline.Claims.ToArray();
        (importSwappedClaims[0], importSwappedClaims[1]) =
        (
            importSwappedClaims[0] with { ProducingImportId = independentImport.ImportId },
            importSwappedClaims[1] with { ProducingImportId = originalImport.ImportId }
        );
        Assert.AreNotEqual(
            AnalysisPublicationBuilder.SemanticFingerprintForVerification(importBaseline, candidates, findings),
            AnalysisPublicationBuilder.SemanticFingerprintForVerification(
                importBaseline with { Claims = importSwappedClaims }, candidates, findings));

        CandidateDecisionContract decision = candidates.Decisions.First(item =>
            item.DependencyIds.Any(id => !id.Value.StartsWith("candidate-delivered-input-", StringComparison.Ordinal)));
        OpaqueId ordinaryDependency = decision.DependencyIds.First(id =>
            !id.Value.StartsWith("candidate-delivered-input-", StringComparison.Ordinal));
        OpaqueId substitutedDependency = new("candidate-delivered-input-forged-ordinary-dependency");
        CandidateDecisionContract substitutedDecision = decision with
        {
            DependencyIds = decision.DependencyIds.Select(id =>
                id == ordinaryDependency ? substitutedDependency : id).ToArray(),
        };
        CandidateAnalysisContract substitutedCandidates = candidates with
        {
            Decisions = candidates.Decisions.Select(item => item == decision ? substitutedDecision : item).ToArray(),
            DependencyEdges = candidates.DependencyEdges.Select(item =>
                item.FromId == decision.DependencyClosureId
                    && item.ToKind == "dependency"
                    && item.ToId == ordinaryDependency
                    ? item with { ToId = substitutedDependency }
                    : item).ToArray(),
        };
        Assert.AreNotEqual(
            AnalysisPublicationBuilder.SemanticFingerprintForVerification(importBaseline, candidates, findings),
            AnalysisPublicationBuilder.SemanticFingerprintForVerification(
                importBaseline, substitutedCandidates, findings));

        OpaqueId exactRoot = new("candidate-delivered-input-exact-admitted-root");
        CandidateAnalysisContract deliveredCandidates = candidates with
        {
            Decisions = candidates.Decisions.Select(item => item with
            {
                DependencyIds = item.DependencyIds.Append(exactRoot).ToArray(),
            }).ToArray(),
            DependencyEdges = candidates.DependencyEdges.Concat(candidates.Decisions.Select((item, index) =>
                new CandidateDependencyEdgeContract(
                    new OpaqueId($"test-delivered-edge-{index}"),
                    "dependency-closure", item.DependencyClosureId,
                    "dependency", exactRoot, "depends-on"))).ToArray(),
            DeliveredInputId = exactRoot,
        };
        OpaqueId substitutedRoot = new("candidate-delivered-input-forged-common-root");
        CandidateAnalysisContract rootSubstitutedCandidates = deliveredCandidates with
        {
            Decisions = deliveredCandidates.Decisions.Select(item => item with
            {
                DependencyIds = item.DependencyIds.Select(id =>
                    id == exactRoot ? substitutedRoot : id).ToArray(),
            }).ToArray(),
            DependencyEdges = deliveredCandidates.DependencyEdges.Select(item =>
                item.ToKind == "dependency" && item.ToId == exactRoot
                    ? item with { ToId = substitutedRoot }
                    : item).ToArray(),
        };
        Assert.AreNotEqual(
            AnalysisPublicationBuilder.SemanticFingerprintForVerification(importBaseline, deliveredCandidates, findings),
            AnalysisPublicationBuilder.SemanticFingerprintForVerification(
                importBaseline, rootSubstitutedCandidates, findings));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Limit")]
    public void TinyReplayDeadlineIsClassifiedAsALimitAndNeverAsGenericFailure()
    {
        using OperationalContext baseline = new();
        _ = baseline.Publish();
        AttemptRecord attempt = baseline.Context.CreateRunAttempt(
            "run-operations-tiny-deadline", DateTimeOffset.UtcNow);
        using OperationalContext replay = new(
            mode: ReplayMode.Incremental,
            priorRunId: new OpaqueId(baseline.RunId),
            context: baseline.Context,
            attempt: attempt,
            priorFindingCase: baseline.FindingCases);
        AnalysisV1WorkAssignment bounded = replay.Assignment with
        {
            ExecutionInput = replay.Assignment.ExecutionInput with
            {
                Limits = replay.Assignment.ExecutionInput.Limits with { MaximumWallTimeMilliseconds = 1 },
            },
        };

        Assert.ThrowsExactly<AnalysisOutputLimitException>(() => replay.Publish(assignment: bounded));
        Assert.IsNull(replay.Store.GetAnalysisSemanticFingerprint(replay.RunId));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Recovery")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Recovery")]
    public void CancellationRecoveryCreatesAFencedPublicationAttemptAndRetainsCancelledOutput()
    {
        using OperationalContext context = new();
        RunRecord cancelling = context.Store.Transition(
            Guid.NewGuid().ToString("N"), context.RunId,
            context.Store.GetRun(context.RunId).Generation,
            LifecycleState.Cancelling, context.Attempt.CoordinatorFencingEpoch,
            "cancellation requested", DateTimeOffset.UtcNow);
        context.Store.SettleLiveAttempts(
            context.RunId, "cancelled-at-recovery-boundary", context.Attempt.CoordinatorFencingEpoch);
        byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new AnalysisWorkerValidationReceipt(
            1, context.Assignment.AssignmentId, context.RunId,
            [context.Assignment.DocumentationEvidence, context.Assignment.CandidateAnalysis, context.Assignment.FindingCase],
            context.Assignment.DocumentationEvidence.ByteLength
                + context.Assignment.CandidateAnalysis.ByteLength
                + context.Assignment.FindingCase.ByteLength,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "not-used",
                ["model"] = "not-used",
                ["credential"] = "not-used",
                ["live"] = "not-used",
                ["billable"] = "not-used",
            },
            "coordinator-recovery-cancellation-output-only"));
        AnalysisCancellationPublicationAdmission admission = context.Store.PrepareCancelledAnalysisPublication(
            context.RunId, context.Attempt.CoordinatorFencingEpoch, receipt, DateTimeOffset.UtcNow);
        AnalysisExecutionPhaseResult result = AnalysisExecutionPhase.PublishTerminalFallback(
            context.Store, context.Assignment, admission.Attempt, cancelling.Binding,
            admission.ValidationReceiptPayloadId, AnalysisTerminalOutcome.Cancelled,
            "coordinator recovery published cancellation output", DateTimeOffset.UtcNow);

        Assert.AreEqual(LifecycleState.Cancelled, result.Receipt.TerminalState);
        RunOutputContract retained = RunOutputJsonCodec.Deserialize(context.Store.ReadAnalysisRunOutput(context.RunId));
        Assert.AreEqual("cancelled", retained.RunState);
        Assert.HasCount(1, retained.CoverageGaps);
        Assert.HasCount(0, retained.Failures);
        Assert.HasCount(1, retained.DocumentationRevisions);
        Assert.HasCount(1, retained.Candidates);
        Assert.HasCount(1, retained.Findings);
        Assert.IsTrue(retained.AnalyzerCoverage.All(item => item.Status == "completed-with-gaps"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Safety")]
    public void AnalysisReplayLeavesProtectedRootCanariesAndExternalBoundariesUntouched()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-operations-canaries-" + Guid.NewGuid().ToString("N"));
        string[] protectedRoots = ["setup", "game", "mo2"];
        try
        {
            Directory.CreateDirectory(root);
            Dictionary<string, string> before = new(StringComparer.Ordinal);
            foreach (string protectedRoot in protectedRoots)
            {
                string directory = Directory.CreateDirectory(Path.Combine(root, protectedRoot)).FullName;
                string canary = Path.Combine(directory, "do-not-mutate.canary");
                File.WriteAllText(canary, protectedRoot + "-immutable", Encoding.UTF8);
                before[canary] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(canary)));
            }

            using OperationalContext context = new();
            _ = context.Publish();

            foreach ((string canary, string fingerprint) in before)
            {
                Assert.AreEqual(fingerprint, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(canary))));
            }
            string boundary = Encoding.UTF8.GetString(context.Store.ReadAnalysisBoundaryReceipt(context.RunId));
            foreach (string capability in new[] { "provider", "model", "credential", "live", "billable" })
            {
                StringAssert.Contains(boundary, $"\"{capability}\":\"not-used\"");
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Cli")]
    public async Task AnalysisCliReadsPublishedOutputThroughTheCoordinatorQueryBoundary()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-operations-cli-" + Guid.NewGuid().ToString("N"));
        int coordinatorPid = 0;
        string publicationContextId;
        try
        {
            using (CandidateStoreContext storeContext = new(
                root, TimeSpan.FromSeconds(1), preserveRoot: true))
            using (OperationalContext publication = new(
                context: storeContext,
                attempt: storeContext.Attempt))
            {
                publicationContextId = publication.Assignment.AnalysisContext.ContextId.Value;
                _ = publication.Publish();
            }
            Thread.Sleep(1_100);

            ProcessResult json = RunCli(root, ["results", "run-candidate", "--json"]);
            Assert.AreEqual(0, json.ExitCode, json.Error);
            using JsonDocument output = JsonDocument.Parse(json.Output);
            Assert.AreEqual("run-candidate", output.RootElement.GetProperty("run_id").GetString());
            Assert.AreEqual("infinium.run-output/v1", output.RootElement.GetProperty("schema_id").GetString());

            ProcessResult human = RunCli(root, ["results", "run-candidate"]);
            Assert.AreEqual(0, human.ExitCode, human.Error);
            StringAssert.Contains(human.Output, "run run-candidate state=completed outcome=completed");
            StringAssert.Contains(human.Output, "no-safety-guarantee=true");

            RuntimeDescriptor descriptor = RuntimeDescriptor.Read(root);
            coordinatorPid = descriptor.ProcessId;
            using GrpcChannel channel = NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe);
            ApplicationService.ApplicationServiceClient application = new(channel);
            HandshakeResponse handshake = await application.NegotiateAsync(new ApplicationHandshakeRequest
            {
                SupportedProtocol = new ProtocolVersionRange
                {
                    Major = ProtocolConstants.Major,
                    MinimumMinor = ProtocolConstants.Minor,
                    MaximumMinor = ProtocolConstants.Minor,
                },
                Compatibility = ProtocolConstants.Compatibility,
                ClientKind = ApplicationClientKind.TestHarness,
                CoordinatorInstanceNonce = ByteString.CopyFrom(descriptor.GetNonce()),
                RequestedCapabilities = { Capability.ApplicationQuery },
            }).ResponseAsync;
            Assert.AreEqual(HandshakeDisposition.Accepted, handshake.Disposition);
            ListAnalysisArtifactsResponse first = await application.ListAnalysisArtifactsAsync(
                new ListAnalysisArtifactsRequest
                {
                    RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = "run-candidate" },
                    RequestedPageSize = 2,
                    ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                    Sort = AnalysisArtifactSort.RankDescendingIdentityAscending,
                }).ResponseAsync;
            Assert.AreEqual(ListAnalysisArtifactsResponse.ResultOneofCase.Page, first.ResultCase);
            Assert.IsTrue(first.Page.HasMore);
            ListAnalysisArtifactsResponse allArtifacts = await application.ListAnalysisArtifactsAsync(
                new ListAnalysisArtifactsRequest
                {
                    RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = "run-candidate" },
                    RequestedPageSize = 100,
                    ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                    Sort = AnalysisArtifactSort.IdentityAscending,
                }).ResponseAsync;
            Assert.HasCount(5, allArtifacts.Page.Items);
            foreach (Infinium.Contracts.Protobuf.Domain.V1.AnalysisArtifactReference artifact in allArtifacts.Page.Items)
            {
                GetAnalysisProvenanceResponse provenance = await application.GetAnalysisProvenanceAsync(
                    new GetAnalysisProvenanceRequest
                    {
                        RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = "run-candidate" },
                        ArtifactId = artifact.ArtifactId,
                        RequestedMaximumEdges = 256,
                        ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                    }).ResponseAsync;
                Assert.AreEqual(GetAnalysisProvenanceResponse.ResultOneofCase.Provenance, provenance.ResultCase,
                    provenance.Failure?.Detail);
                Assert.IsTrue(provenance.Provenance.Dependencies.Any(item =>
                    item.ArtifactId.Value == publicationContextId));
            }
            Infinium.Contracts.Protobuf.Domain.V1.AnalysisArtifactReference findingArtifact =
                allArtifacts.Page.Items.Single(item =>
                    item.Kind == Infinium.Contracts.Protobuf.Domain.V1.AnalysisArtifactKind.FindingCase);
            GetAnalysisProvenanceResponse boundedProvenance = await application.GetAnalysisProvenanceAsync(
                new GetAnalysisProvenanceRequest
                {
                    RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = "run-candidate" },
                    ArtifactId = findingArtifact.ArtifactId,
                    RequestedMaximumEdges = 1,
                    ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                }).ResponseAsync;
            Assert.IsTrue(boundedProvenance.Provenance.Truncated);
            byte[] validCursorBytes = first.Page.Next.OpaqueValue.ToByteArray();
            byte[] tamperedBytes = [.. validCursorBytes];
            tamperedBytes[0] ^= 0x01;
            ListAnalysisArtifactsResponse tampered = await application.ListAnalysisArtifactsAsync(
                new ListAnalysisArtifactsRequest
                {
                    RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = "run-candidate" },
                    RequestedPageSize = 2,
                    ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                    Sort = AnalysisArtifactSort.RankDescendingIdentityAscending,
                    After = new PageCursor { OpaqueValue = ByteString.CopyFrom(tamperedBytes) },
                }).ResponseAsync;
            Assert.AreEqual(ListAnalysisArtifactsResponse.ResultOneofCase.CursorRejection, tampered.ResultCase);
            Assert.AreEqual(CursorDisposition.Malformed, tampered.CursorRejection.Disposition);

            ListAnalysisArtifactsResponse queryMismatch = await application.ListAnalysisArtifactsAsync(
                new ListAnalysisArtifactsRequest
                {
                    RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = "run-candidate" },
                    RequestedPageSize = 3,
                    ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                    Sort = AnalysisArtifactSort.RankDescendingIdentityAscending,
                    After = new PageCursor { OpaqueValue = ByteString.CopyFrom(validCursorBytes) },
                }).ResponseAsync;
            if (queryMismatch.CursorRejection.Disposition == CursorDisposition.Malformed)
            {
                Assert.Fail(queryMismatch.CursorRejection.Failure.Detail);
            }
            Assert.AreEqual(CursorDisposition.QueryMismatch, queryMismatch.CursorRejection.Disposition,
                queryMismatch.CursorRejection.Failure.Detail);

            ListAnalysisArtifactsResponse sortMismatch = await application.ListAnalysisArtifactsAsync(
                new ListAnalysisArtifactsRequest
                {
                    RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = "run-candidate" },
                    RequestedPageSize = 2,
                    ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                    Sort = AnalysisArtifactSort.UpdatedTickDescendingIdentityDescending,
                    After = new PageCursor { OpaqueValue = ByteString.CopyFrom(validCursorBytes) },
                }).ResponseAsync;
            Assert.AreEqual(CursorDisposition.SortMismatch, sortMismatch.CursorRejection.Disposition);
        }
        finally
        {
            if (coordinatorPid == 0)
            {
                try
                {
                    coordinatorPid = RuntimeDescriptor.Read(root).ProcessId;
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or FileNotFoundException)
                {
                }
            }
            if (coordinatorPid != 0)
            {
                try
                {
                    using Process coordinator = Process.GetProcessById(coordinatorPid);
                    coordinator.Kill(entireProcessTree: true);
                    coordinator.WaitForExit(5_000);
                }
                catch (ArgumentException)
                {
                }
            }
            if (Directory.Exists(root))
            {
                for (int attempt = 0; attempt < 20 && Directory.Exists(root); attempt++)
                {
                    try
                    {
                        Directory.Delete(root, recursive: true);
                    }
                    catch (IOException)
                    {
                        if (attempt < 19)
                        {
                            Thread.Sleep(25);
                        }
                    }
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Cli")]
    public void AnalysisCliStartsAndReadsManagedDocumentationCandidateFindingCaseProductExecution()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-operations-managed-cli-" + Guid.NewGuid().ToString("N"));
        string requestPath = Path.Combine(root, "managed-analysis-request.json");
        int coordinatorPid = 0;
        try
        {
            StoragePaths paths = new(root);
            RunBinding binding = new("snapshot-managed-cli", "context-managed", "config-managed", "manifest-managed");
            string semanticPayloadId;
            string semanticSha;
            using (AuthoritativeStore store = new(paths))
            {
                CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                    "managed-cli-seed", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
                RunRecord producer = store.CreateRun("command-managed-cli-seed", "run-managed-cli-seed",
                    binding, authority.FencingEpoch, DateTimeOffset.UtcNow);
                _ = store.Transition("transition-managed-cli-seed", producer.RunId, producer.Generation,
                    LifecycleState.Running, authority.FencingEpoch, "seed semantic input", DateTimeOffset.UtcNow);
                AttemptRecord attempt = store.CreateAttempt(producer.RunId, authority.FencingEpoch,
                    TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
                using AttemptStagingAuthority staging = paths.CreateAttemptStagingDirectory(attempt.AttemptId);
                BethesdaSemanticSnapshot snapshot = new(new OpaqueId(binding.InstallationSnapshotId),
                    BethesdaSemanticContract.SchemaVersion, BethesdaSemanticExtractor.ProducerId,
                    BethesdaSemanticExtractor.ProducerVersion, new Sha256Fingerprint(new string('3', 64)), [],
                    new Dictionary<string, BethesdaOverrideChain>(), new Dictionary<string, BethesdaRecordContribution>(),
                    [], [], [], [], new Dictionary<string, BethesdaResolvedParticipant>(),
                    new Dictionary<string, BethesdaNpcFact>(), new Dictionary<string, BethesdaRaceFact>(),
                    new Dictionary<string, BethesdaPlacedReferenceFact>(), [],
                    new Dictionary<string, IReadOnlyList<BethesdaLinkFact>>(), [], [], [], []);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
                    new BethesdaSemanticExtractionResult(BethesdaExtractionState.Completed, snapshot, [], []));
                semanticSha = Convert.ToHexStringLower(SHA256.HashData(bytes));
                File.WriteAllBytes(Path.Combine(paths.Staging, attempt.AttemptId, "bethesda.json"), bytes);
                semanticPayloadId = store.AdmitStagedPayload(attempt, "bethesda.json", semanticSha,
                    bytes.LongLength, new string('4', 64), bytes.LongLength, DateTimeOffset.UtcNow).PayloadId;
                store.SettleLiveAttempts(producer.RunId, "seed-complete", authority.FencingEpoch);
                RunRecord current = store.GetRun(producer.RunId);
                _ = store.Transition("terminal-managed-cli-seed", producer.RunId, current.Generation,
                    LifecycleState.Failed, authority.FencingEpoch, "seed-only producer closed", DateTimeOffset.UtcNow);
                ManagedAnalysisOrchestrationRequest request = ManagedRequest(
                    "run-managed-cli", binding, semanticPayloadId, semanticSha);
                File.WriteAllBytes(requestPath, JsonSerializer.SerializeToUtf8Bytes(request, PrettyContractJson));
            }
            paths.Dispose();
            Thread.Sleep(1_100);

            ProcessResult start = RunCli(root,
            [
                "start", "--snapshot", binding.InstallationSnapshotId,
                "--context", binding.AnalysisContextId,
                "--configuration", binding.EffectiveScanConfigurationId,
                "--manifest", binding.ResolvedInputManifestId,
                "--analysis-request", requestPath, "--json",
            ]);
            Assert.AreEqual(0, start.ExitCode, start.Error);
            using (JsonDocument started = JsonDocument.Parse(start.Output))
            {
                Assert.AreEqual("run-managed-cli", started.RootElement.GetProperty("runId").GetString());
            }
            ProcessResult wait = RunCli(root, ["wait", "run-managed-cli", "--timeout-seconds", "30", "--json"]);
            Assert.AreEqual(0, wait.ExitCode, wait.Error);
            ProcessResult results = RunCli(root, ["results", "run-managed-cli", "--json"]);
            Assert.AreEqual(0, results.ExitCode, results.Error);
            using JsonDocument output = JsonDocument.Parse(results.Output);
            Assert.AreEqual("run-managed-cli", output.RootElement.GetProperty("run_id").GetString());
            Assert.AreEqual("2.1.0", output.RootElement.GetProperty("analysis_context").GetProperty("artifact_version").GetString());
            StringAssert.Contains(results.Output,
                output.RootElement.GetProperty("analysis_context").GetProperty("fingerprint").GetString()!);
            coordinatorPid = RuntimeDescriptor.Read(root).ProcessId;
        }
        finally
        {
            if (coordinatorPid == 0)
            {
                try { coordinatorPid = RuntimeDescriptor.Read(root).ProcessId; }
                catch (Exception exception) when (exception is IOException or InvalidDataException or FileNotFoundException) { }
            }
            if (coordinatorPid != 0)
            {
                try
                {
                    using Process coordinator = Process.GetProcessById(coordinatorPid);
                    coordinator.Kill(entireProcessTree: true);
                    coordinator.WaitForExit(5_000);
                }
                catch (ArgumentException) { }
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    public async Task AnalysisReplayManagedWorkerValidatesAndCoordinatorPublishesTheRetainedStages()
    {
        using OperationalContext context = new();
        RunRecord staged = context.Store.GetRun(context.RunId);
        _ = context.Store.Transition(
            Guid.NewGuid().ToString("N"), context.RunId, staged.Generation,
            LifecycleState.Retrying, context.Context.Authority.FencingEpoch,
            "semantic stages retained before analysis-v1 publication dispatch", DateTimeOffset.UtcNow);
        _ = context.Store.RegisterRunOperation(
            context.RunId,
            "analysis-v1",
            JsonSerializer.Serialize(context.Assignment),
            DateTimeOffset.UtcNow);
        RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
            context.Context.Authority.InstanceId,
            context.Context.Authority.FencingEpoch,
            Environment.ProcessId,
            elevated: false,
            DateTimeOffset.UtcNow);
        CoordinatorRuntime runtime = new(context.Store, context.Context.Authority, descriptor);
        WorkerBootstrapRegistry registry = new();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(runtime);
        builder.Services.AddSingleton(registry);
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
            options.MaxSendMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
        });
        builder.WebHost.UseNamedPipes(options => options.CurrentUserOnly = true);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenNamedPipe(descriptor.WorkerPipe, listen =>
            {
                listen.Protocols = HttpProtocols.Http2;
                listen.Use(next => connection =>
                {
                    connection.Features.Set(new InfiniumPipeRoleFeature("worker", descriptor.WorkerPipe));
                    return next(connection);
                });
            });
        });
        await using WebApplication app = builder.Build();
        app.MapGrpcService<WorkerGrpcService>();
        await app.StartAsync();
        ManagedRunExecutor executor = new(
            runtime, registry, app.Services.GetRequiredService<ILogger<ManagedRunExecutor>>());

        executor.RecoverAtStartup();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        while (!LifecyclePolicy.IsTerminal(context.Store.GetRun(context.RunId).State))
        {
            await Task.Delay(25, timeout.Token);
        }

        Assert.AreEqual(LifecycleState.Completed, context.Store.GetRun(context.RunId).State);
        Assert.IsNotNull(context.Store.GetAnalysisSemanticFingerprint(context.RunId));
        Assert.IsTrue(context.Store.ReadAnalysisBoundaryReceipt(context.RunId).AsSpan().IndexOf("not-used"u8) >= 0);
        await app.StopAsync();
    }

    private static ProcessResult RunCli(string root, IReadOnlyList<string> arguments)
    {
        return TestProcessRunner.RunDotnetProject(
            "src/Infinium.Cli",
            ["--root", root, .. arguments],
            30_000,
            "The analysis CLI query exceeded its process bound.");
    }

}

#pragma warning restore CA1416
