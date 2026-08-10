using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Net.Client;
using Infinium.Analysis.Candidates;
using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.Evaluation;
using Infinium.Application.FindingCases;
using Infinium.Application.Runtime;
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

namespace Infinium.Tests;

#pragma warning disable CA1416 // The managed worker and Application query use the Windows named-pipe transport.

[TestClass]
public sealed class Slice5Wp6ManagedCorpusIntegrationTests
{
    private const string FixtureDirectory = "m1-slice5-wp6-cross-stage-corpus-v1";
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Replay")]
    public async Task FrozenWp6FourCaseCorpusExecutesManagedCoordinatorAndTypedQueryBeforeOracleComparison()
    {
        string repositoryRoot = FindRepositoryRoot();
        string fixtureRoot = Path.Combine(repositoryRoot, "docs", "evaluation", "fixtures", FixtureDirectory);
        using JsonDocument ordinary = Parse(Path.Combine(fixtureRoot, "ordinary-product-inputs.v1.json"));
        using JsonDocument harness = Parse(Path.Combine(fixtureRoot, "harness-envelope.v1.json"));
        AssertAnswerFree(ordinary.RootElement);
        JsonElement shared = ordinary.RootElement.GetProperty("shared_facts");

        Dictionary<string, JsonElement> requests = ordinary.RootElement.GetProperty("requests").EnumerateArray()
            .ToDictionary(item => item.GetProperty("input_id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        JsonElement[] cases = harness.RootElement.GetProperty("cases").EnumerateArray().Select(item => item.Clone()).ToArray();
        Assert.AreEqual(4, cases.Length);
        foreach (JsonElement item in cases)
        {
            JsonElement binding = item.GetProperty("request_binding");
            JsonElement request = requests[binding.GetProperty("input_id").GetString()!];
            Assert.AreEqual(binding.GetProperty("mode").GetString(), request.GetProperty("mode").GetString());
            Assert.AreEqual(binding.GetProperty("revision_key").GetString(), request.GetProperty("revision_key").GetString());
            Assert.AreEqual(binding.GetProperty("prior_result").GetString(), request.GetProperty("prior_result").GetString());
        }

        string root = Path.Combine(Path.GetTempPath(), $"infinium-wp6-managed-corpus-{Guid.NewGuid():N}");
        StoragePaths? paths = null;
        AuthoritativeStore? ownedStore = null;
        try
        {
            paths = new StoragePaths(root);
            using AuthoritativeStore store = ownedStore = new(paths);
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "wp6-managed-corpus", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
            RunBinding runBinding = new("snapshot.001", "context.001", "configuration.001", "manifest.001");
            ArtifactReferenceContract bethesda = SeedBethesda(store, paths, authority, runBinding);
            RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
                authority.InstanceId, authority.FencingEpoch, Environment.ProcessId, false, DateTimeOffset.UtcNow);
            CoordinatorRuntime runtime = new(store, authority, descriptor);
            WorkerBootstrapRegistry registry = new();

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(runtime);
            builder.Services.AddSingleton(registry);
            builder.Services.AddSingleton<ManagedRunExecutor>();
            builder.Services.AddSingleton<SnapshotCaptureExecutor>();
            builder.Services.AddGrpc(options =>
            {
                options.MaxReceiveMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
                options.MaxSendMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
            });
            builder.WebHost.UseNamedPipes(options => options.CurrentUserOnly = true);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenNamedPipe(descriptor.ApplicationPipe, listen =>
                {
                    listen.Protocols = HttpProtocols.Http2;
                    listen.Use(next => connection =>
                    {
                        connection.Features.Set(new InfiniumPipeRoleFeature("application", descriptor.ApplicationPipe));
                        return next(connection);
                    });
                });
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
            app.MapGrpcService<ApplicationGrpcService>();
            app.MapGrpcService<WorkerGrpcService>();
            await app.StartAsync();

            ManagedRunExecutor executor = app.Services.GetRequiredService<ManagedRunExecutor>();
            using GrpcChannel channel = NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe);
            ApplicationService.ApplicationServiceClient client = new(channel);
            HandshakeResponse handshake = await client.NegotiateAsync(new ApplicationHandshakeRequest
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

            Dictionary<string, ManagedCaseObservation> observations = new(StringComparer.Ordinal);
            List<string> executionOrder = [];
            DocumentationEvidenceContract? cleanDocumentation = null;
            string? cleanRunId = null;
            byte[]? cleanOutputBefore = null;
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(90));
            foreach (JsonElement caseEnvelope in cases)
            {
                string caseId = caseEnvelope.GetProperty("case_id").GetString()!;
                JsonElement requestInput = requests[caseEnvelope.GetProperty("request_binding")
                    .GetProperty("input_id").GetString()!];
                string runId = "run-" + requestInput.GetProperty("input_id").GetString()!.Replace('.', '-');
                string mode = requestInput.GetProperty("mode").GetString()!;
                bool usesPrior = mode != "clean";
                ManagedAnalysisOrchestrationRequest request = ManagedRequest(
                    runId, runBinding, bethesda, shared, requestInput,
                    usesPrior ? cleanRunId : null,
                    mode is "incremental" or "retained-replay" &&
                    requestInput.GetProperty("revision_key").GetString() == "revision.001"
                        ? cleanDocumentation : null);
                if (request.DocumentationImport.RetainedEvidence is { } retained)
                {
                    AnalysisPhaseCheckpointRecord previous = store.ReadLatestAnalysisPhaseCheckpoint(
                        cleanRunId!, DocumentationEvidencePhase.PhaseId)!;
                    byte[] retainedBytes = DocumentationEvidenceJsonCodec.Serialize(retained);
                    Assert.AreEqual(previous.PayloadSha256,
                        Convert.ToHexStringLower(SHA256.HashData(retainedBytes)));
                    Assert.AreEqual(previous.PayloadByteLength, retainedBytes.LongLength);
                }

                RunRecord admitted = executor.CreateManagedAnalysisRun(
                    "command-" + runId, runId, runBinding, request, "EvaluationHarness",
                    DateTimeOffset.UtcNow.AddMinutes(2));
                Assert.AreEqual(LifecycleState.Queued, admitted.State);
                executor.Schedule(runId);
                while (!LifecyclePolicy.IsTerminal(store.GetRun(runId).State))
                {
                    await Task.Delay(25, timeout.Token);
                }
                LifecycleState terminalState = store.GetRun(runId).State;
                Assert.AreEqual(LifecycleState.CompletedWithGaps, terminalState, runId);

                GetAnalysisOutputResponse queried = await client.GetAnalysisOutputAsync(new GetAnalysisOutputRequest
                {
                    RunId = new Infinium.Contracts.Protobuf.Domain.V1.RunId { Value = runId },
                    ExpectedProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = "1" },
                }).ResponseAsync;
                Assert.AreEqual(GetAnalysisOutputResponse.ResultOneofCase.Output, queried.ResultCase, queried.Failure?.Detail);
                ManagedCaseObservation observation = Observe(caseId, runId, store, queried.Output);
                if (cleanOutputBefore is not null)
                {
                    observation = observation with
                    {
                        RetainedHistoryUnchanged = cleanOutputBefore.AsSpan()
                            .SequenceEqual(store.ReadAnalysisRunOutput(cleanRunId!)),
                    };
                }
                AssertHarnessReceipt(caseEnvelope.GetProperty("required_receipts"), observation, cleanRunId);
                observations.Add(caseId, observation);
                executionOrder.Add(caseId);
                if (mode == "clean")
                {
                    cleanRunId = runId;
                    cleanDocumentation = observation.Documentation;
                    cleanOutputBefore = [.. queried.Output.RunOutputJson.Span];
                }
            }

            Assert.IsNotNull(cleanRunId);
            Assert.IsNotNull(cleanOutputBefore);
            CollectionAssert.AreEqual(cleanOutputBefore, store.ReadAnalysisRunOutput(cleanRunId));
            Assert.HasCount(4, observations);
            CollectionAssert.AreEqual(
                harness.RootElement.GetProperty("observation_protocol").GetProperty("case_execution_order")
                    .EnumerateArray().Select(item => item.GetString()!).ToArray(),
                executionOrder);

            // Frozen expected truth becomes available only after every coordinator/query observation is sealed.
            using JsonDocument expected = Parse(Path.Combine(fixtureRoot, "expected-results.v1.json"));
            foreach (JsonElement expectedCase in expected.RootElement.GetProperty("cases").EnumerateArray())
            {
                string caseId = expectedCase.GetProperty("case_id").GetString()!;
                AssertCase(expectedCase.GetProperty("expected"), observations[caseId], observations["WP6-CROSS-CLEAN-D01"]);
            }
            WriteComparisonReceipt(repositoryRoot, executionOrder.Select(caseId => observations[caseId]));
        }
        finally
        {
            ownedStore?.Dispose();
            paths?.Dispose();
            if (Directory.Exists(root))
            {
                for (int attempt = 0; attempt < 20 && Directory.Exists(root); attempt++)
                {
                    try { Directory.Delete(root, recursive: true); }
                    catch (IOException) when (attempt < 19) { await Task.Delay(25); }
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    public void ManagedRequestRejectsDeliveredInputFingerprintOrSourceReferenceDriftBeforeAdmission()
    {
        string fixtureRoot = Path.Combine(
            FindRepositoryRoot(), "docs", "evaluation", "fixtures", FixtureDirectory);
        using JsonDocument ordinary = Parse(Path.Combine(fixtureRoot, "ordinary-product-inputs.v1.json"));
        JsonElement shared = ordinary.RootElement.GetProperty("shared_facts");
        JsonElement requestInput = ordinary.RootElement.GetProperty("requests").EnumerateArray()
            .Single(item => item.GetProperty("mode").GetString() == "clean");
        RunBinding binding = new("snapshot.001", "context.001", "configuration.001", "manifest.001");
        ArtifactReferenceContract bethesda = new(
            Id("bethesda.test"), BethesdaSemanticContract.SchemaVersion,
            new Sha256Fingerprint(new string('d', 64)), "retained");
        ManagedAnalysisOrchestrationRequest request = ManagedRequest(
            "run-wp6-drift", binding, bethesda, shared, requestInput, null, null);

        Assert.ThrowsExactly<AnalysisIdentityDriftException>(() => ManagedAnalysisOrchestrator.Validate(
            request with
            {
                Candidate = request.Candidate with
                {
                    DeliveredInputByteFingerprint = new Sha256Fingerprint(new string('e', 64)),
                },
            }, "run-wp6-drift", binding));
        Assert.ThrowsExactly<AnalysisIdentityDriftException>(() => ManagedAnalysisOrchestrator.Validate(
            request with
            {
                ExecutionInput = request.ExecutionInput with
                {
                    SourceInputs = request.ExecutionInput.SourceInputs.Where(item =>
                        item.ArtifactId != request.Candidate.DeliveredInput!.PayloadId).ToArray(),
                },
            }, "run-wp6-drift", binding));
    }

    private static ManagedAnalysisOrchestrationRequest ManagedRequest(
        string runId,
        RunBinding binding,
        ArtifactReferenceContract bethesda,
        JsonElement shared,
        JsonElement requestInput,
        string? priorRunId,
        DocumentationEvidenceContract? retainedDocumentation)
    {
        JsonElement revision = shared.GetProperty("documentation_revisions").EnumerateArray().Single(item =>
            item.GetProperty("revision_key").GetString() == requestInput.GetProperty("revision_key").GetString());
        DocumentationImportRequestContract documentation = DocumentationRequest(runId, binding, revision, shared);
        if (retainedDocumentation is not null)
        {
            documentation = documentation with
            {
                Mode = DocumentationImportMode.RetainedReuse,
                SourceBytes = null,
                RetainedEvidence = retainedDocumentation,
                AcceptedApplicationTargets = [],
            };
        }

        SemanticAnalysisContextContract context = AnalysisContext(binding.AnalysisContextId);
        CandidateDeliveredInputContract delivered = DeliveredInput(runId, binding, shared);
        byte[] deliveredBytes = CandidateDeliveredInputJsonCodec.Serialize(delivered);
        Sha256Fingerprint deliveredFingerprint = new(Convert.ToHexStringLower(SHA256.HashData(deliveredBytes)));
        DeliveredIndexCandidatePopulationSource source = new();
        ArtifactReferenceContract Reference(string id, char fingerprint) => new(
            Id(id), Version(), new Sha256Fingerprint(new string(fingerprint, 64)), "retained");
        ArtifactReferenceContract documentationSource = new(
            documentation.Manifest.SourceId, Version(), documentation.Manifest.ByteFingerprint, "retained");
        ArtifactReferenceContract deliveredReference = new(
            delivered.PayloadId, delivered.SchemaVersion, deliveredFingerprint, "retained");
        ArtifactReferenceContract analyzerReference = new(
            source.AnalyzerId, source.Declaration.AnalyzerVersion,
            CandidateAnalysisIdentity.StructuralHash([JsonSerializer.Serialize(source.Declaration)]), "retained");
        ReplayMode mode = requestInput.GetProperty("mode").GetString() switch
        {
            "clean" => ReplayMode.Clean,
            "incremental" => ReplayMode.Incremental,
            "retained-replay" => ReplayMode.RetainedDownstreamReplay,
            _ => throw new InvalidDataException("WP6 request mode is outside the closed mapping."),
        };
        AnalysisExecutionInputContract execution = new(
            ContractConstants.AnalysisExecutionInputSchemaId, Version(), Id("execution." + runId), Id(runId),
            Reference(binding.InstallationSnapshotId, 'a'), bethesda,
            [documentationSource, deliveredReference], [analyzerReference],
            Reference(binding.EffectiveScanConfigurationId, 'b'),
            Reference(binding.ResolvedInputManifestId, 'c'),
            mode, priorRunId is null ? null : Id(priorRunId), 17,
            new(1_000_000, 2_000_000, 100_000, 100_000, 120_000), Boundaries())
        {
            AnalysisContext = new(context.ContextId, context.SchemaVersion, context.CanonicalFingerprint, "retained"),
        };
        CandidatePhaseParameters candidate = new(
            Id("population.001"), Id("ruleset.001"), Id("threshold.001"), CandidateExecutionLimits.Default)
        {
            DeliveredInput = delivered,
            DeliveredInputByteFingerprint = deliveredFingerprint,
        };
        FindingCasePhaseParameters finding = FindingParameters(runId, binding, shared, delivered, execution, candidate, source);
        return new(
            ManagedAnalysisOrchestrationRequest.CurrentSchemaVersion,
            "assignment." + runId,
            execution,
            context,
            documentation,
            candidate,
            finding,
            new string('a', 40),
            DateTimeOffset.UtcNow,
            AnalysisTerminalOutcome.CompletedWithGaps,
            "WP6 managed corpus completed with the declared visible gap",
            AnalysisV1WorkAssignment.AbsoluteMaximumInputBytes,
            AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes,
            AnalysisV1WorkAssignment.AbsoluteMaximumQueryItems);
    }

    private static DocumentationImportRequestContract DocumentationRequest(
        string runId, RunBinding binding, JsonElement revision, JsonElement shared)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(revision.GetProperty("text").GetString()!);
        DocumentationClaimInputContract[] claims = revision.GetProperty("claims").EnumerateArray().Select(claim =>
            new DocumentationClaimInputContract(
                Id(claim.GetProperty("claim_key").GetString()!),
                claim.GetProperty("start").GetInt64(), claim.GetProperty("end").GetInt64(),
                Encoding.UTF8.GetString(bytes.AsSpan(claim.GetProperty("start").GetInt32(),
                    claim.GetProperty("end").GetInt32() - claim.GetProperty("start").GetInt32())),
                claim.GetProperty("kind").GetString() switch
                {
                    "declared-purpose" => ClaimKind.DeclaredPurpose,
                    "known-issue" => ClaimKind.KnownIssue,
                    "patch-instruction" => ClaimKind.PatchInstruction,
                    _ => throw new InvalidDataException("WP6 claim kind is outside the closed mapping."),
                },
                [], EvidenceAuthority.AuthoritativeExternal,
                Applicability(claim.GetProperty("applicability").GetString()!), ClassificationRole.Declared,
                claim.GetProperty("contradicts").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray()))
            .ToArray();
        DocumentationApplicationInputContract[] applications = shared.GetProperty("applications").EnumerateArray()
            .Select(application => new DocumentationApplicationInputContract(
                Id(application.GetProperty("claim_key").GetString()!), Id(runId), Id(binding.AnalysisContextId),
                Id(application.GetProperty("subject_id").GetString()!), "installed-entity", Id("dependency.source.001"),
                Applicability(application.GetProperty("applicability").GetString()!),
                application.TryGetProperty("supporting_claim_keys", out JsonElement supporting)
                    ? supporting.EnumerateArray().Select(item => Id(item.GetString()!)).ToArray() : [],
                application.TryGetProperty("declared_purpose_code", out JsonElement purpose)
                    ? new DocumentationPurposeInputContract(purpose.GetString()!, [], Id("analyzer.001"),
                        "exact independently authored declared purpose") : null)).ToArray();
        DocumentationClaimImportManifestContract manifest = new(
            ContractConstants.DocumentationClaimImportSchemaId, Version(),
            Id(revision.GetProperty("source_id").GetString()!), DocumentationSourceKind.Fixture,
            revision.GetProperty("source_revision").GetString()!, DocumentationSourceAvailability.Present,
            new Sha256Fingerprint(revision.GetProperty("sha256").GetString()!), bytes.LongLength,
            Id(binding.InstallationSnapshotId), claims, applications);
        DocumentationApplicationTargetContract target = new(
            Id(runId), Id(binding.InstallationSnapshotId), Id(binding.AnalysisContextId),
            Id(binding.ResolvedInputManifestId), Id("entity.001"), "installed-entity", Id("dependency.source.001"));
        return new(
            Id(runId), Id(runId), DocumentationImportMode.CleanImport, Id("dependency.source.001"),
            Id("extractor.wp6"), new UtcTimestamp(DateTimeOffset.UnixEpoch), manifest, bytes, null, [target]);
    }

    private static CandidateDeliveredInputContract DeliveredInput(string runId, RunBinding binding, JsonElement shared)
    {
        CandidateDeliveredLinkFactContract[] links = shared.GetProperty("candidate_source_facts").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == "link")
            .Select(item => new CandidateDeliveredLinkFactContract(
                Id(item.GetProperty("fact_id").GetString()!), Id("record." + item.GetProperty("fact_id").GetString()),
                Id("contribution.prior." + item.GetProperty("fact_id").GetString()),
                Id("contribution.winner." + item.GetProperty("fact_id").GetString()),
                "prior-source", "winning-source", "linked_reference", null, 0,
                CandidateDeliveredLinkState.Resolved, Id(item.GetProperty("prior_target").GetString()!),
                CandidateDeliveredLinkState.Resolved, Id(item.GetProperty("winning_target").GetString()!),
                item.GetProperty("dependency_ids").EnumerateArray().Select(value => Id(value.GetString()!)).ToArray(),
                item.GetProperty("evidence_ids").EnumerateArray().Select(value => Id(value.GetString()!)).ToArray()))
            .ToArray();
        JsonElement documentationFact = shared.GetProperty("candidate_source_facts").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == "documentation-application");
        CandidateDeliveredDocumentationFactContract documentation = new(
            Id(documentationFact.GetProperty("fact_id").GetString()!), Id("application.002"), Id("claim.002"),
            Id("passage.claim.002"), Id("revision.001"), Id("entity.001"), Id(runId),
            Id(binding.InstallationSnapshotId), Id(binding.AnalysisContextId), ClaimApplicabilityState.Contradicted,
            documentationFact.GetProperty("dependency_ids").EnumerateArray().Select(value => Id(value.GetString()!)).ToArray(),
            [Id("claim.002")], [Id("claim.003")]);
        CandidateDeliveredInputContract input = new(
            ContractConstants.CandidateDeliveredInputSchemaId, CandidateDeliveredInputIdentity.Version,
            Id("candidate-delivered-input-pending"), Id(runId), Id(binding.InstallationSnapshotId),
            Id(binding.AnalysisContextId), Id(binding.EffectiveScanConfigurationId), links, [], [], [documentation]);
        return input with { PayloadId = CandidateDeliveredInputIdentity.ComputePayloadId(input) };
    }

    private static FindingCasePhaseParameters FindingParameters(
        string runId,
        RunBinding binding,
        JsonElement shared,
        CandidateDeliveredInputContract delivered,
        AnalysisExecutionInputContract execution,
        CandidatePhaseParameters parameters,
        DeliveredIndexCandidatePopulationSource source)
    {
        Sha256Fingerprint deliveredFingerprint = new(Convert.ToHexStringLower(
            SHA256.HashData(CandidateDeliveredInputJsonCodec.Serialize(delivered))));
        CandidatePopulationContext populationContext = new(
            null, Id(runId), Id(binding.InstallationSnapshotId), Id(binding.AnalysisContextId),
            Id(binding.EffectiveScanConfigurationId), delivered, deliveredFingerprint);
        CandidatePipelineRequest candidateRequest = new(
            Id(runId), parameters.PopulationId, parameters.PolicyId, parameters.ThresholdId,
            parameters.Limits, populationContext, [source], execution);
        ProjectedCandidate[] projected = source.DeclarePopulation(populationContext)
            .Select(member => Project(candidateRequest, member)).ToArray();
        Dictionary<string, JsonElement> factual = shared.GetProperty("conclusion_factual_inputs").EnumerateArray()
            .ToDictionary(item => item.GetProperty("fact_id").GetString()!, item => item, StringComparer.Ordinal);
        ProjectedCandidate[] withHypothesis = projected.Where(item => item.HypothesisId is not null).ToArray();
        FindingEvidenceFactContract[] evidence = withHypothesis.Select(item =>
        {
            JsonElement fact = factual[item.Member.SourceFactId.Value];
            bool assigned = fact.GetProperty("consequence").GetProperty("state").GetString() == "assigned";
            return new FindingEvidenceFactContract(
                Id("finding." + runId + "." + item.Member.SourceFactId.Value), item.HypothesisId!,
                assigned ? WorstCredibleConsequence.MeaningfulBoundedLoss : WorstCredibleConsequence.MaintenanceOnly,
                fact.GetProperty("causal_locus").GetProperty("field").GetString()!,
                fact.GetProperty("causal_conditions")[0].GetString()!,
                fact.GetProperty("applicability_condition_ids").EnumerateArray().Select(value => value.GetString()!).ToArray(),
                fact.GetProperty("contradicting_evidence_ids").EnumerateArray().Select(value => Id(value.GetString()!)).ToArray(),
                [], fact.GetProperty("supporting_evidence_ids").EnumerateArray()
                    .Select(value => Id(value.GetString()!)).Where(item.Member.SupportingEvidenceIds.Contains).ToArray());
        }).ToArray();
        FindingRecommendationFactContract[] recommendations = evidence.Select(item =>
            new FindingRecommendationFactContract(
                Id("recommendation." + runId + "." + item.FactId.Value), item.HypothesisId, RecommendationKind.Validation,
                "Validate the typed causal condition.", "Bounded to supplied typed evidence.",
                "Analysis is non-mutating.", ["State may differ after new input."],
                "Reobserve the affected locus.", item.EvidenceIds)).ToArray();
        ProjectedCandidate supported = projected.Single(item => item.Member.SourceFactId == Id("fact.001"));
        SharedCauseProofContract proof = new(
            Id("proof." + runId), [supported.HypothesisId!], source.Declaration.AnalyzerFamily,
            source.Declaration.SemanticContractVersion, source.Declaration.IdentityContractVersion,
            supported.Member.Participants.ToDictionary(item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
            "prior-target-differs-from-winning-target", "linked_reference", ["condition.001"],
            FindingCaseIdentity.SharedCauseDependencyClosureId(supported.Member.DependencyIds),
            supported.Member.SupportingEvidenceIds)
        {
            AnalyzerVersion = source.Declaration.AnalyzerVersion,
        };
        TaxonomyClassificationFactContract[] taxonomy = withHypothesis.Select(item =>
            new TaxonomyClassificationFactContract(
                Id("taxonomy." + item.HypothesisId!.Value), item.HypothesisId!, "infinium.mod-impact", Version(),
                "impact", "effect", "bounded-effect", TaxonomyApplicability.Assigned,
                ClassificationRole.Established, item.Member.SupportingEvidenceIds,
                [Id("taxonomy-condition.wp6")], null, source.AnalyzerId,
                new UtcTimestamp(DateTimeOffset.UnixEpoch), "Generic synthetic WP6 classification fact.")).ToArray();
        OpaqueId gapId = Id("coverage-gap." + runId + ".fact.003");
        CoverageMemberFactContract[] coverage = projected.Select(item =>
        {
            bool gap = item.Member.SourceFactId == Id("fact.003");
            return new CoverageMemberFactContract(
                Id("coverage-member." + runId + "." + item.Member.SourceFactId.Value), source.AnalyzerId, "population.001",
                "candidate source facts", item.Member.SourceFactId,
                gap ? CoverageMemberState.CompletedWithGaps : CoverageMemberState.Completed,
                gap ? "contradicted application needs information" : "typed fact completed",
                gap ? "uncontradicted-applicable-evidence" : "none", null,
                item.HypothesisId is null ? [] : [taxonomy.Single(value => value.HypothesisId == item.HypothesisId).FactId],
                gap ? gapId : null);
        }).ToArray();
        return new(
            Id("promotion.wp6"), Version(), Id("reconciliation.wp6"), Version(), Id("actor.wp6"),
            new UtcTimestamp(DateTimeOffset.UnixEpoch), evidence, recommendations, [proof], [], taxonomy, [],
            [new CoveragePopulationFactContract(Id("coverage.population.001"), source.AnalyzerId,
                "population.001", "candidate source facts")],
            coverage, [], [], [], [], [], Boundaries());
    }

    private static ProjectedCandidate Project(CandidatePipelineRequest request, CausalJoinPopulationMember member)
    {
        CandidateDecisionDisposition disposition = member.InputState switch
        {
            CausalJoinInputState.Complete => CandidateDecisionDisposition.CandidateAdmitted,
            CausalJoinInputState.Ambiguous => CandidateDecisionDisposition.Ambiguous,
            CausalJoinInputState.ResolvedNegative => CandidateDecisionDisposition.ResolvedNegative,
            _ => throw new InvalidDataException("WP6 delivered fact has an unexpected decision state."),
        };
        OpaqueId closure = CandidateAnalysisIdentity.StableId("candidate-closure",
            member.DependencyIds.Select(item => item.Value).Prepend(member.PopulationMemberId.Value).ToArray());
        OpaqueId decision = CandidateAnalysisIdentity.StableId(
            "candidate-decision", request.OriginatingRunId.Value, request.PopulationId.Value,
            member.PopulationMemberId.Value, request.PolicyId.Value, request.PolicyFingerprint.Value,
            request.ThresholdId.Value, request.ThresholdFingerprint.Value,
            request.Limits.SemanticsFingerprint.Value, member.InputFingerprint.Value, disposition.ToString());
        if (disposition == CandidateDecisionDisposition.ResolvedNegative)
        {
            return new(member, decision, null);
        }
        OpaqueId candidate = CandidateAnalysisIdentity.StableId("candidate", decision.Value, closure.Value);
        return new(member, decision,
            CandidateAnalysisIdentity.StableId("hypothesis", candidate.Value, request.ThresholdId.Value));
    }

    private static ManagedCaseObservation Observe(
        string caseId, string runId, AuthoritativeStore store, AnalysisOutputPayload queried)
    {
        AnalysisPhaseCheckpointRecord docsCheckpoint = store.ReadLatestAnalysisPhaseCheckpoint(
            runId, DocumentationEvidencePhase.PhaseId)!;
        AnalysisPhaseCheckpointRecord candidateCheckpoint = store.ReadLatestAnalysisPhaseCheckpoint(
            runId, CandidateAnalysisPhase.PhaseId)!;
        AnalysisPhaseCheckpointRecord findingCheckpoint = store.ReadLatestAnalysisPhaseCheckpoint(
            runId, FindingCaseAnalysisPhase.PhaseId)!;
        DocumentationEvidenceContract documentation = DocumentationEvidenceJsonCodec.Deserialize(
            store.ReadDocumentationEvidencePayload(docsCheckpoint.PayloadId));
        CandidateAnalysisContract candidates = CandidateAnalysisJsonCodec.Deserialize(
            store.ReadCandidateAnalysisPayload(candidateCheckpoint.PayloadId));
        FindingCaseContract findings = FindingCaseJsonCodec.Deserialize(
            store.ReadFindingCasePayload(findingCheckpoint.PayloadId));
        RunOutputContract output = RunOutputJsonCodec.Deserialize(queried.RunOutputJson.Span);
        AnalysisReplayContract replay = AnalysisReplayJsonCodec.Deserialize(store.ReadAnalysisReplay(runId));
        byte[] stored = store.ReadAnalysisRunOutput(runId);
        bool queryMatchesStore = queried.RunOutputJson.Span.SequenceEqual(stored);
        bool humanEmbedsJson = queried.HumanOutput.Contains(
            "canonical-run-output-json=" + Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        ExternalBoundaryReceipt boundary = JsonSerializer.Deserialize<ExternalBoundaryReceipt>(
            store.ReadAnalysisBoundaryReceipt(runId))
            ?? throw new AssertFailedException("The managed boundary receipt was unavailable.");
        return new(
            caseId, runId, documentation, candidates, findings, output, replay,
            docsCheckpoint.Disposition, candidateCheckpoint.Disposition, findingCheckpoint.Disposition,
            store.GetAnalysisSemanticFingerprint(runId)!,
            TypedSemanticFingerprint(documentation, candidates, findings),
            queryMatchesStore, humanEmbedsJson, boundary.Effects);
    }

    private static void AssertCase(JsonElement expected, ManagedCaseObservation actual, ManagedCaseObservation clean)
    {
        Assert.IsTrue(actual.QueryMatchesStore);
        Assert.IsTrue(actual.HumanEmbedsJson);
        Assert.AreEqual(0, actual.ExternalEffects);
        Assert.AreEqual("completed-with-gaps", actual.Output.RunState);
        switch (actual.CaseId)
        {
            case "WP6-CROSS-CLEAN-D01":
                AssertClean(expected, actual);
                Assert.AreEqual("recomputed-invalidated", actual.DocumentationDisposition);
                Assert.AreEqual("recomputed-invalidated", actual.CandidateDisposition);
                Assert.AreEqual("recomputed-invalidated", actual.FindingDisposition);
                break;
            case "WP6-CROSS-UNCHANGED-D02":
                Assert.AreEqual(clean.RunId, actual.Replay.ComparedRunId?.Value);
                Assert.AreEqual(expected.GetProperty("resolved_dependencies_equal_clean").GetBoolean(),
                    actual.DocumentationCheckpointPayloadId == clean.DocumentationCheckpointPayloadId);
                Assert.AreEqual("reused-exact", expected.GetProperty("wp2_checkpoint").GetString());
                Assert.AreEqual("reused-retained-phase", actual.DocumentationDisposition);
                Assert.AreEqual("recomputed-run-binding", expected.GetProperty("wp3_execution").GetString());
                Assert.AreEqual("recomputed-run-binding", actual.CandidateDisposition);
                Assert.AreEqual("recomputed-from-current-wp3-closure", expected.GetProperty("wp4_execution").GetString());
                Assert.IsTrue(actual.FindingDisposition.StartsWith("recomputed-", StringComparison.Ordinal));
                Assert.AreEqual(expected.GetProperty("typed_semantic_equivalence_to_clean").GetBoolean(),
                    actual.TypedSemanticFingerprint == clean.TypedSemanticFingerprint);
                Assert.AreEqual("wp2-checkpoint-only", expected.GetProperty("byte_reuse_claim").GetString());
                Assert.AreNotEqual(clean.Candidates.PayloadId, actual.Candidates.PayloadId);
                Assert.AreNotEqual(clean.Findings.PayloadId, actual.Findings.PayloadId);
                Assert.AreEqual("not-predeclared", expected.GetProperty("new_semantic_object_count").GetString());
                Assert.AreEqual(expected.GetProperty("publication_commits").GetInt32(), actual.QueryMatchesStore ? 1 : 0);
                Assert.AreEqual(expected.GetProperty("history_mutations").GetInt32(), actual.RetainedHistoryUnchanged ? 0 : 1);
                Assert.AreEqual(expected.GetProperty("network_calls").GetInt32(), actual.ExternalEffects);
                break;
            case "WP6-CROSS-CHANGED-D03":
                Assert.AreEqual(clean.RunId, actual.Replay.ComparedRunId?.Value);
                Assert.AreNotEqual(clean.DocumentationCheckpointPayloadId, actual.DocumentationCheckpointPayloadId);
                Assert.AreEqual("new-revision-and-dependent-checkpoint", expected.GetProperty("wp2_phase").GetString());
                Assert.IsFalse(expected.GetProperty("wp2_checkpoint_reuse").GetBoolean());
                Assert.AreNotEqual("reused-retained-phase", actual.DocumentationDisposition);
                Assert.AreEqual("recomputed-transitively-with-new-run-binding", expected.GetProperty("wp3_execution").GetString());
                Assert.AreEqual("recomputed-invalidated", actual.CandidateDisposition);
                Assert.AreEqual("recomputed-from-current-wp3-closure", expected.GetProperty("wp4_execution").GetString());
                Assert.IsTrue(actual.FindingDisposition.StartsWith("recomputed-", StringComparison.Ordinal));
                Assert.AreEqual("new-aggregate-publication", expected.GetProperty("wp5_execution").GetString());
                Assert.AreEqual(expected.GetProperty("wp2_shape_counts_equal_clean").GetBoolean(),
                    DocumentationShape(actual.Documentation) == DocumentationShape(clean.Documentation));
                Assert.AreEqual(expected.GetProperty("new_revision_identity_required").GetBoolean(),
                    actual.Documentation.Revisions.Single().RevisionId != clean.Documentation.Revisions.Single().RevisionId);
                string changedDependency = expected.GetProperty("changed_dependencies")[0].GetString()!;
                Assert.AreEqual("source.001:r1->r2", changedDependency);
                Assert.AreEqual(clean.Documentation.Revisions.Single().SourceId,
                    actual.Documentation.Revisions.Single().SourceId);
                Assert.AreEqual("r1", clean.Documentation.Revisions.Single().SourceRevision);
                Assert.AreEqual("r2", actual.Documentation.Revisions.Single().SourceRevision);
                Assert.AreNotEqual(clean.Documentation.Revisions.Single().ByteFingerprint,
                    actual.Documentation.Revisions.Single().ByteFingerprint);
                foreach (string stable in expected.GetProperty("stable_unrelated_typed_facts").EnumerateArray()
                    .Select(item => item.GetString()!))
                {
                    CandidateDecisionContract cleanFact = clean.Candidates.Decisions.Single(item => item.SourceFactId == Id(stable));
                    CandidateDecisionContract actualFact = actual.Candidates.Decisions.Single(item => item.SourceFactId == Id(stable));
                    Assert.AreEqual(StableDecisionShape(cleanFact), StableDecisionShape(actualFact));
                }
                Assert.AreEqual("not-asserted", expected.GetProperty("stable_fact_checkpoint_reuse").GetString());
                Assert.AreEqual("not-asserted", expected.GetProperty("semantic_fingerprint_equivalence").GetString());
                Assert.AreEqual(expected.GetProperty("retained_history_mutations").GetInt32(), actual.RetainedHistoryUnchanged ? 0 : 1);
                Assert.AreEqual(expected.GetProperty("network_calls").GetInt32(), actual.ExternalEffects);
                break;
            case "WP6-CROSS-REPLAY-D04":
                Assert.IsTrue(actual.Replay.SemanticallyEquivalent,
                    $"Replay semantic fingerprint {actual.SemanticFingerprint} did not equal clean {clean.SemanticFingerprint}. "
                    + SemanticProjectionDifference(clean, actual));
                Assert.AreEqual(ReplayState.CompleteClean, actual.Replay.ReplayState);
                Assert.AreEqual("complete-clean", expected.GetProperty("replayability").GetString());
                Assert.AreEqual(AuditabilityState.Complete, actual.Replay.AuditabilityState);
                Assert.AreEqual(clean.RunId, actual.Replay.ComparedRunId?.Value);
                Assert.AreEqual(expected.GetProperty("retained_dependencies_verified").GetBoolean(),
                    actual.Replay.MissingDependencyIds.Count == 0);
                Assert.AreEqual("reused-exact", expected.GetProperty("wp2_checkpoint").GetString());
                Assert.AreEqual("reused-retained-phase", actual.DocumentationDisposition);
                Assert.AreEqual("recomputed-run-binding", expected.GetProperty("wp3_execution").GetString());
                Assert.AreEqual("recomputed-run-binding", actual.CandidateDisposition);
                Assert.AreEqual("recomputed-from-current-wp3-closure", expected.GetProperty("wp4_execution").GetString());
                Assert.IsTrue(actual.FindingDisposition.StartsWith("recomputed-", StringComparison.Ordinal));
                Assert.AreEqual("new-replay-publication", expected.GetProperty("wp5_execution").GetString());
                Assert.AreEqual(expected.GetProperty("typed_semantic_equivalence_to_clean").GetBoolean(),
                    actual.TypedSemanticFingerprint == clean.TypedSemanticFingerprint);
                Assert.AreEqual(expected.GetProperty("human_json_semantically_equivalent").GetBoolean(), actual.HumanEmbedsJson);
                Assert.AreEqual(expected.GetProperty("hidden_dependency_substitutions").GetInt32(),
                    actual.Replay.MissingDependencyIds.Count);
                Assert.AreEqual(expected.GetProperty("network_calls").GetInt32(), actual.ExternalEffects);
                Assert.AreEqual(expected.GetProperty("provider_dispatches").GetInt32(), actual.ProviderDispatches);
                Assert.AreEqual(expected.GetProperty("history_mutations").GetInt32(), actual.RetainedHistoryUnchanged ? 0 : 1);
                break;
            default:
                Assert.Fail("Unexpected WP6 case identity: " + actual.CaseId);
                break;
        }
    }

    private static void AssertClean(JsonElement expected, ManagedCaseObservation actual)
    {
        JsonElement wp2 = expected.GetProperty("wp2");
        Assert.AreEqual(wp2.GetProperty("revisions").GetInt32(), actual.Documentation.Revisions.Count);
        Assert.AreEqual(wp2.GetProperty("imports").GetInt32(), actual.Documentation.Imports.Count);
        Assert.AreEqual(wp2.GetProperty("passages").GetInt32(), actual.Documentation.Passages.Count);
        Assert.AreEqual(wp2.GetProperty("claims").GetInt32(), actual.Documentation.Claims.Count);
        Assert.AreEqual(wp2.GetProperty("applications").GetInt32(), actual.Documentation.Applications.Count);
        Assert.AreEqual(wp2.GetProperty("purpose_assignments").GetInt32(), actual.Documentation.PurposeAssignments.Count);
        Assert.AreEqual(wp2.GetProperty("contradiction_gaps").GetInt32(), actual.Documentation.Gaps.Count);
        Assert.AreEqual(wp2.GetProperty("failures").GetInt32(), actual.Documentation.Failures.Count);
        JsonElement decisions = expected.GetProperty("wp3").GetProperty("decisions");
        Assert.AreEqual(decisions.GetProperty("admitted").GetInt32(), actual.Candidates.Decisions.Count(item =>
            item.Disposition == CandidateDecisionDisposition.CandidateAdmitted));
        Assert.AreEqual(decisions.GetProperty("resolved-negative").GetInt32(), actual.Candidates.Decisions.Count(item =>
            item.Disposition == CandidateDecisionDisposition.ResolvedNegative));
        Assert.AreEqual(decisions.GetProperty("ambiguous").GetInt32(), actual.Candidates.Decisions.Count(item =>
            item.Disposition == CandidateDecisionDisposition.Ambiguous));
        Assert.AreEqual(decisions.GetProperty("unsupported").GetInt32(), actual.Candidates.Decisions.Count(item =>
            item.Disposition == CandidateDecisionDisposition.Unsupported));
        Assert.AreEqual(expected.GetProperty("wp3").GetProperty("candidates").GetInt32(), actual.Candidates.Candidates.Count);
        Assert.AreEqual(expected.GetProperty("wp3").GetProperty("hypotheses").GetInt32(), actual.Candidates.Hypotheses.Count);
        Assert.AreEqual(expected.GetProperty("wp3").GetProperty("abstentions").GetInt32(), actual.Candidates.Abstentions.Count);
        JsonElement wp4 = expected.GetProperty("wp4");
        Assert.AreEqual(wp4.GetProperty("findings").GetInt32(), actual.Findings.Findings.Count);
        Assert.AreEqual(wp4.GetProperty("recommendations").GetInt32(), actual.Findings.Recommendations.Count);
        Assert.AreEqual(wp4.GetProperty("supported_cases").GetInt32(),
            actual.Findings.Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported));
        Assert.AreEqual(wp4.GetProperty("lead_only_cases").GetInt32(),
            actual.Findings.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly));
        Assert.AreEqual(wp4.GetProperty("readiness_effect_from_leads").GetInt32(),
            actual.Findings.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly && item.AffectsReadiness));
        JsonElement coverage = wp4.GetProperty("coverage");
        Assert.AreEqual(coverage.GetProperty("denominator").GetInt64(), actual.Findings.Coverage.Single().Denominator);
        Assert.AreEqual(coverage.GetProperty("completed").GetInt64(), actual.Findings.Coverage.Single().CompletedCount);
        Assert.AreEqual(coverage.GetProperty("visible_gaps").GetInt32(), actual.Findings.Gaps.Count);
        Assert.AreEqual(coverage.GetProperty("state").GetString(),
            JsonNamingPolicy.KebabCaseLower.ConvertName(actual.Findings.Coverage.Single().State.ToString()));
        JsonElement wp5 = expected.GetProperty("wp5");
        Assert.AreEqual(wp5.GetProperty("publication_commits").GetInt32(), actual.QueryMatchesStore ? 1 : 0);
        Assert.AreEqual(wp5.GetProperty("partial_publications").GetInt32(), actual.QueryMatchesStore ? 0 : 1);
        Assert.AreEqual(wp5.GetProperty("terminal_state").GetString(), actual.Output.RunState);
        Assert.AreEqual(wp5.GetProperty("human_json_semantically_equivalent").GetBoolean(), actual.HumanEmbedsJson);
        Assert.AreEqual(wp5.GetProperty("network_calls").GetInt32(), actual.ExternalEffects);
        Assert.AreEqual(wp5.GetProperty("provider_dispatches").GetInt32(), actual.ProviderDispatches);
    }

    private static void AssertHarnessReceipt(
        JsonElement required, ManagedCaseObservation actual, string? cleanRunId)
    {
        Assert.AreEqual("admitted", required.GetProperty("coordinator").GetProperty("admission").GetString());
        Assert.AreEqual(required.GetProperty("coordinator").GetProperty("execution").GetString(), actual.Output.RunState);
        JsonElement prior = required.GetProperty("prior_result_flow");
        if (prior.TryGetProperty("input", out JsonElement input))
        {
            Assert.AreEqual("absent", input.GetString());
            Assert.IsNull(actual.Replay.ComparedRunId);
            Assert.AreEqual("result.001", prior.GetProperty("produces").GetString());
        }
        else
        {
            Assert.AreEqual("result.001", prior.GetProperty("consumes").GetString());
            Assert.AreEqual(cleanRunId, actual.Replay.ComparedRunId?.Value);
            Assert.AreEqual("exact-retained-identity", prior.GetProperty("binding").GetString());
        }
        Assert.AreEqual(0, prior.GetProperty("hidden_substitutions").GetInt32());
        Assert.IsTrue(required.GetProperty("run_binding").GetProperty("captured").GetBoolean());
        Assert.IsFalse(required.GetProperty("run_binding").GetProperty("opaque_value_predeclared").GetBoolean());
        JsonElement publication = required.GetProperty("publication");
        Assert.AreEqual(publication.GetProperty("commit_count").GetInt32(), actual.QueryMatchesStore ? 1 : 0);
        Assert.IsTrue(publication.GetProperty("atomic").GetBoolean());
        Assert.AreEqual(publication.GetProperty("partial_publications").GetInt32(), actual.QueryMatchesStore ? 0 : 1);
        JsonElement query = required.GetProperty("application_result_query");
        Assert.AreEqual("Application", query.GetProperty("request").GetProperty("surface").GetString());
        Assert.AreEqual("result-query-request", query.GetProperty("request").GetProperty("type").GetString());
        Assert.AreEqual(0, query.GetProperty("request").GetProperty("field_level_predicates").GetArrayLength());
        Assert.AreEqual("query-results", query.GetProperty("response").GetProperty("type").GetString());
        Assert.IsTrue(query.GetProperty("response").GetProperty("bounded").GetBoolean());
        Assert.AreEqual(query.GetProperty("response").GetProperty("published_analysis_result_count").GetInt32(),
            actual.QueryMatchesStore ? 1 : 0);
        Assert.AreEqual(query.GetProperty("response").GetProperty("typed_result_present").GetBoolean(), actual.QueryMatchesStore);
        Assert.AreEqual(query.GetProperty("human_json_projections").GetProperty("semantically_equivalent").GetBoolean(),
            actual.HumanEmbedsJson);
        JsonElement effects = required.GetProperty("external_effects");
        Assert.AreEqual(effects.GetProperty("network_calls").GetInt32(), actual.ExternalEffects);
        Assert.AreEqual(effects.GetProperty("provider_dispatches").GetInt32(), actual.ProviderDispatches);
        Assert.AreEqual(effects.GetProperty("external_mutations").GetInt32(), actual.ExternalEffects);
        Assert.IsTrue(required.GetProperty("oracle_load").GetProperty("observation_sealed").GetBoolean());
        Assert.AreEqual("after-observation", required.GetProperty("oracle_load").GetProperty("load_sequence").GetString());
    }

    private static string DocumentationShape(DocumentationEvidenceContract value) => string.Join('|',
        value.Revisions.Count, value.Imports.Count, value.Passages.Count, value.Claims.Count,
        value.Applications.Count, value.PurposeAssignments.Count, value.Gaps.Count, value.Failures.Count);

    private static string SemanticProjectionDifference(ManagedCaseObservation expected, ManagedCaseObservation actual)
    {
        string left = Encoding.UTF8.GetString(AnalysisPublicationBuilder.SemanticProjectionForVerification(
            expected.Documentation, expected.Candidates, expected.Findings));
        string right = Encoding.UTF8.GetString(AnalysisPublicationBuilder.SemanticProjectionForVerification(
            actual.Documentation, actual.Candidates, actual.Findings));
        int length = Math.Min(left.Length, right.Length);
        int index = Enumerable.Range(0, length).FirstOrDefault(i => left[i] != right[i], -1);
        if (index < 0)
        {
            index = length;
        }
        int start = Math.Max(0, index - 240);
        int leftCount = Math.Min(480, left.Length - start);
        int rightCount = Math.Min(480, right.Length - start);
        return $"First projection difference at {index}; clean={left.Substring(start, leftCount)}; replay={right.Substring(start, rightCount)}";
    }

    private static string StableDecisionShape(CandidateDecisionContract value) => JsonSerializer.Serialize(new
    {
        value.SourceFactId,
        value.Lane,
        value.Disposition,
        value.JoinKind,
        path = value.Path,
        value.Rationale,
        participants = value.Participants.OrderBy(item => item.ParticipantId.Value),
        evidence = value.EvidenceIds.OrderBy(item => item.Value),
        dependencies = value.DependencyIds
            .Where(item => !item.Value.StartsWith("candidate-delivered-input-", StringComparison.Ordinal))
            .OrderBy(item => item.Value),
    });

    private static string TypedSemanticFingerprint(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findings)
    {
        byte[] projection = JsonSerializer.SerializeToUtf8Bytes(new
        {
            documentation = new
            {
                documentation.PayloadId,
                shape = DocumentationShape(documentation),
            },
            candidate_counts = new
            {
                admitted = candidates.Decisions.Count(item =>
                    item.Disposition == CandidateDecisionDisposition.CandidateAdmitted),
                resolved_negative = candidates.Decisions.Count(item =>
                    item.Disposition == CandidateDecisionDisposition.ResolvedNegative),
                ambiguous = candidates.Decisions.Count(item =>
                    item.Disposition == CandidateDecisionDisposition.Ambiguous),
                unsupported = candidates.Decisions.Count(item =>
                    item.Disposition == CandidateDecisionDisposition.Unsupported),
                candidates = candidates.Candidates.Count,
                hypotheses = candidates.Hypotheses.Count,
                abstentions = candidates.Abstentions.Count,
            },
            finding_counts = new
            {
                findings = findings.Findings.Count,
                recommendations = findings.Recommendations.Count,
                supported_cases = findings.Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported),
                lead_only_cases = findings.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly),
                visible_gaps = findings.Gaps.Count,
            },
            coverage = findings.Coverage.Select(item => new
            {
                item.Denominator,
                item.CompletedCount,
                item.State,
                members = item.MemberResults.Select(member => new
                {
                    member.State,
                    member.Reason,
                    member.MissingCapabilityOrInformation,
                }).OrderBy(member => member.Reason, StringComparer.Ordinal),
            }).OrderBy(item => item.State),
            gap_count = findings.Gaps.Count,
        });
        return Convert.ToHexStringLower(SHA256.HashData(projection));
    }

    private static void WriteComparisonReceipt(
        string repositoryRoot,
        IEnumerable<ManagedCaseObservation> observations)
    {
        string directory = Environment.GetEnvironmentVariable("INFINIUM_WP6_RECEIPT_ROOT")
            ?? Path.Combine(repositoryRoot, "artifacts", "m1-slice5", "wp6");
        Directory.CreateDirectory(directory);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_id = "infinium.verification.m1-slice5.wp6-product-comparison/v1",
            result = "passed",
            product_path = "managed-coordinator-wp2-wp3-wp4-atomic-publication-application-result-query-output",
            oracle_load_order = "after-all-four-observations-sealed",
            cases = observations.Select(item => new
            {
                case_id = item.CaseId,
                run_id = item.RunId,
                coordinator_terminal_state = item.Output.RunState,
                phase_dispositions = new
                {
                    wp2 = item.DocumentationDisposition,
                    wp3 = item.CandidateDisposition,
                    wp4 = item.FindingDisposition,
                },
                publication_commits = item.QueryMatchesStore ? 1 : 0,
                replay_state = JsonNamingPolicy.KebabCaseLower.ConvertName(item.Replay.ReplayState.ToString()),
                auditability_state = JsonNamingPolicy.KebabCaseLower.ConvertName(item.Replay.AuditabilityState.ToString()),
                semantically_equivalent = item.Replay.SemanticallyEquivalent,
                missing_dependency_count = item.Replay.MissingDependencyIds.Count,
                prior_run_id = item.Replay.ComparedRunId?.Value,
                history_mutations = item.RetainedHistoryUnchanged ? 0 : 1,
                application_result_query = new
                {
                    request = new
                    {
                        surface = "Application",
                        type = "result-query-request",
                        run_identity_binding = item.RunId,
                        selection = "published-analysis-result-for-run",
                        field_level_predicates = Array.Empty<string>(),
                    },
                    response = new
                    {
                        type = "query-results",
                        bounded = true,
                        run_identity_binding = item.RunId,
                        published_analysis_result_count = item.QueryMatchesStore ? 1 : 0,
                        typed_result_present = item.QueryMatchesStore,
                    },
                    human_json_projections = new
                    {
                        human_present = item.HumanEmbedsJson,
                        json_present = item.QueryMatchesStore,
                        semantically_equivalent = item.HumanEmbedsJson,
                    },
                    field_level_query_claim = "none",
                },
                external_effects = item.ExternalEffects,
                oracle_comparison = "passed",
            }).ToArray(),
        }, PrettyJson);
        File.WriteAllBytes(Path.Combine(directory, "product-comparison-receipt.json"), bytes);
    }

    private static ArtifactReferenceContract SeedBethesda(
        AuthoritativeStore store, StoragePaths paths, CoordinatorAuthority authority, RunBinding binding)
    {
        RunRecord producer = store.CreateRun("command-wp6-bethesda", "run-wp6-bethesda", binding,
            authority.FencingEpoch, DateTimeOffset.UtcNow);
        _ = store.Transition("transition-wp6-bethesda", producer.RunId, producer.Generation,
            LifecycleState.Running, authority.FencingEpoch, "retain bounded semantic dependency", DateTimeOffset.UtcNow);
        AttemptRecord attempt = store.CreateAttempt(producer.RunId, authority.FencingEpoch,
            TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow);
        using AttemptStagingAuthority staging = paths.CreateAttemptStagingDirectory(attempt.AttemptId);
        BethesdaSemanticSnapshot snapshot = new(
            Id(binding.InstallationSnapshotId), BethesdaSemanticContract.SchemaVersion,
            BethesdaSemanticExtractor.ProducerId, BethesdaSemanticExtractor.ProducerVersion,
            new Sha256Fingerprint(new string('1', 64)), [], new Dictionary<string, BethesdaOverrideChain>(),
            new Dictionary<string, BethesdaRecordContribution>(), [], [], [], [],
            new Dictionary<string, BethesdaResolvedParticipant>(), new Dictionary<string, BethesdaNpcFact>(),
            new Dictionary<string, BethesdaRaceFact>(), new Dictionary<string, BethesdaPlacedReferenceFact>(), [],
            new Dictionary<string, IReadOnlyList<BethesdaLinkFact>>(), [], [], [], []);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            new BethesdaSemanticExtractionResult(BethesdaExtractionState.Completed, snapshot, [], []));
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        const string name = "bethesda.json";
        File.WriteAllBytes(Path.Combine(paths.Staging, attempt.AttemptId, name), bytes);
        PayloadAdmission admission = store.AdmitStagedPayload(attempt, name, sha, bytes.LongLength,
            new string('2', 64), bytes.LongLength, DateTimeOffset.UtcNow);
        staging.Dispose();
        store.SettleLiveAttempts(producer.RunId, "seed-complete", authority.FencingEpoch);
        RunRecord current = store.GetRun(producer.RunId);
        _ = store.Transition("terminal-wp6-bethesda", producer.RunId, current.Generation,
            LifecycleState.Failed, authority.FencingEpoch, "seed-only producer closed", DateTimeOffset.UtcNow);
        return new(Id(admission.PayloadId), BethesdaSemanticContract.SchemaVersion, new Sha256Fingerprint(sha), "retained");
    }

    private static SemanticAnalysisContextContract AnalysisContext(string id)
    {
        SemanticAnalysisContextContract value = new(
            Id(id), new ContractVersion(2, 1, 0), new Sha256Fingerprint(new string('0', 64)),
            [Id("source.001:r1")], new Dictionary<string, string> { ["evidence-policy"] = "public-synthetic-local-only" });
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
        _ => throw new InvalidDataException("WP6 applicability is outside the closed mapping."),
    };

    private static void AssertAnswerFree(JsonElement value)
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
                Assert.IsFalse(forbidden.Contains(property.Name), "Answer-bearing property leaked: " + property.Name);
                AssertAnswerFree(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                AssertAnswerFree(item);
            }
        }
    }

    private static JsonDocument Parse(string path) => JsonDocument.Parse(
        File.ReadAllBytes(path), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Infinium repository root was not found.");
    }

    private static OpaqueId Id(string value) => new(value);
    private static ContractVersion Version() => new(1, 0, 0);

    private sealed record ProjectedCandidate(
        CausalJoinPopulationMember Member,
        OpaqueId DecisionId,
        OpaqueId? HypothesisId);

    private sealed record ManagedCaseObservation(
        string CaseId,
        string RunId,
        DocumentationEvidenceContract Documentation,
        CandidateAnalysisContract Candidates,
        FindingCaseContract Findings,
        RunOutputContract Output,
        AnalysisReplayContract Replay,
        string DocumentationDisposition,
        string CandidateDisposition,
        string FindingDisposition,
        string SemanticFingerprint,
        string TypedSemanticFingerprint,
        bool QueryMatchesStore,
        bool HumanEmbedsJson,
        IReadOnlyDictionary<string, string> BoundaryEffects)
    {
        public string DocumentationCheckpointPayloadId => Documentation.PayloadId.Value;
        public int ExternalEffects => BoundaryEffects.Count(item => item.Value != "not-used");
        public int ProviderDispatches => BoundaryEffects.TryGetValue("provider", out string? value) && value != "not-used" ? 1 : 0;
        public bool RetainedHistoryUnchanged { get; init; } = true;
    }
}
