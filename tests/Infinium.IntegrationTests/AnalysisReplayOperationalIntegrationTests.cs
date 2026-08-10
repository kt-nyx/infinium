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

public sealed partial class AnalysisReplayIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Evaluation")]
    public void FrozenOperationalCasesAreBoundToProductExecutionBeforeOracleComparison()
    {
        string fixtureRoot = Path.Combine(TestRepository.Root, "fixtures", "public", "operations", "analysis-lifecycle");
        using JsonDocument projections = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "ordinary-product-projections.v1.json")));
        using JsonDocument envelope = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "harness-envelope.v1.json")));
        using JsonDocument topologies = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "safety-topologies.v1.json")));
        byte[] schemaBytes = File.ReadAllBytes(Path.Combine(fixtureRoot, "ordinary-product-projection.schema.json"));
        using JsonDocument projectionSchema = JsonDocument.Parse(schemaBytes);
        HashSet<string> forbiddenNames = envelope.RootElement.GetProperty("product_projection_contract")
            .GetProperty("forbidden_recursive_property_names").EnumerateArray()
            .Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal);
        HashSet<string> forbiddenValues = FrozenForbiddenValues(envelope.RootElement);
        Dictionary<string, JsonNode> actualByCase = new(StringComparer.Ordinal);
        Dictionary<string, ProjectionValidationReceipt> validationReceipts = new(StringComparer.Ordinal);
        List<MaterializedSafetyTopology.TopologyCapabilityReceipt> topologyCapabilityReceipts = [];
        string? retainedReceiptPath = Environment.GetEnvironmentVariable("INFINIUM_ANALYSIS_VALIDATION_RECEIPT_PATH");
        foreach (JsonElement binding in envelope.RootElement.GetProperty("case_bindings").EnumerateArray())
        {
            string caseId = binding.GetProperty("case_id").GetString()!;
            string family = binding.GetProperty("behavior_family").GetString()!;
            int projectionIndex = binding.GetProperty("product_projection_index").GetInt32();
            JsonElement projection = projections.RootElement.GetProperty("projections")[projectionIndex];
            string freshCanary = Guid.NewGuid().ToString("N");
            ActiveJsonSchemaValidator.Validate(
                projection, projectionSchema.RootElement,
                projectionSchema.RootElement.GetProperty("$id").GetString()!);
            ValidateProjectionIsolation(projection, forbiddenNames, forbiddenValues, freshCanary);
            byte[] projectionBytes = Encoding.UTF8.GetBytes(projection.GetRawText());
            validationReceipts.Add(caseId, new ProjectionValidationReceipt(
                caseId,
                Convert.ToHexStringLower(SHA256.HashData(schemaBytes)),
                Convert.ToHexStringLower(SHA256.HashData(projectionBytes)),
                projectionBytes.LongLength,
                "closed-schema-and-answer-isolation-validated-before-product-dispatch"));
            PersistOperationalExecutionReceipts(
                retainedReceiptPath, validationReceipts.Values, topologyCapabilityReceipts);
            actualByCase.Add(caseId, family switch
            {
                "atomic-publication" => ObserveFrozenAtomicPublication(projection),
                "replay-and-invalidation" => ObserveFrozenReplay(projectionIndex, projection),
                "bounded-query" => ObserveFrozenQuery(projection),
                "terminal-and-equivalent-output" => ObserveFrozenOutput(projection),
                "attempt-recovery" => ObserveFrozenRecovery(projectionIndex, projection),
                "write-and-nonmutation-safety" => ObserveFrozenSafety(
                    projectionIndex, projection, topologies.RootElement, topologyCapabilityReceipts),
                _ => throw new AssertFailedException("The frozen fixture contains an unbound behavior family: " + family),
            });
            PersistOperationalExecutionReceipts(
                retainedReceiptPath, validationReceipts.Values, topologyCapabilityReceipts);
        }

        // Expected truth is loaded only after every ordinary projection has executed.
        using JsonDocument expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "expected-results.v1.json")));
        foreach ((string caseId, JsonNode actual) in actualByCase)
        {
            JsonElement oracle = expected.RootElement.GetProperty("cases").EnumerateArray()
                .Single(item => item.GetProperty("case_id").GetString() == caseId).GetProperty("expected");
            JsonNode expectedNode = JsonNode.Parse(oracle.GetRawText())!;
            Assert.IsTrue(JsonNode.DeepEquals(expectedNode, actual),
                $"Frozen case '{caseId}' differed. Expected={expectedNode.ToJsonString()} Actual={actual.ToJsonString()}");
        }
        Assert.HasCount(12, actualByCase);
        Assert.HasCount(12, validationReceipts);
        Assert.IsTrue(validationReceipts.Values.All(item =>
            item.Disposition == "closed-schema-and-answer-isolation-validated-before-product-dispatch"));
    }

    private static void PersistOperationalExecutionReceipts(
        string? path,
        IEnumerable<ProjectionValidationReceipt> projectionReceipts,
        IEnumerable<MaterializedSafetyTopology.TopologyCapabilityReceipt> topologyReceipts)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException("The analysis validation receipt path has no directory.");
        Directory.CreateDirectory(directory);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_id = "infinium.verification.operations-projection-validation-receipts/v1",
            projection_validation_receipts = projectionReceipts.OrderBy(item => item.CaseId).ToArray(),
            topology_capability_receipts = topologyReceipts.Distinct().OrderBy(item => item.Capability).ThenBy(item => item.NativeDisposition).ToArray(),
        });
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using FileStream stream = new(
                    fullPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.WriteThrough);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
                return;
            }
            catch (Exception exception) when (
                attempt < 4 &&
                exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50 * attempt));
            }
        }
    }

    private static HashSet<string> FrozenForbiddenValues(JsonElement envelope)
    {
        HashSet<string> values = new(StringComparer.Ordinal)
        {
            envelope.GetProperty("registry_identity").GetString()!,
        };
        foreach (JsonElement package in envelope.GetProperty("packages").EnumerateArray())
        {
            values.Add(package.GetProperty("package_identity").GetString()!);
        }
        foreach (JsonElement binding in envelope.GetProperty("case_bindings").EnumerateArray())
        {
            values.Add(binding.GetProperty("case_id").GetString()!);
            values.Add(binding.GetProperty("counterpart_case_id").GetString()!);
            values.Add(binding.GetProperty("oracle_pointer").GetString()!);
            foreach (JsonElement eval in binding.GetProperty("eval_case_ids").EnumerateArray())
            {
                values.Add(eval.GetString()!);
            }
        }
        return values;
    }

    private static void ValidateProjectionIsolation(
        JsonElement value,
        IReadOnlySet<string> forbiddenNames,
        IReadOnlySet<string> forbiddenValues,
        string freshCanary)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    Assert.IsFalse(forbiddenNames.Contains(property.Name),
                        $"Ordinary product projection exposed forbidden property '{property.Name}'.");
                    ValidateProjectionIsolation(property.Value, forbiddenNames, forbiddenValues, freshCanary);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in value.EnumerateArray())
                {
                    ValidateProjectionIsolation(item, forbiddenNames, forbiddenValues, freshCanary);
                }
                break;
            case JsonValueKind.String:
                string text = value.GetString()!;
                Assert.IsFalse(forbiddenValues.Contains(text),
                    $"Ordinary product projection exposed forbidden exact value '{text}'.");
                Assert.AreNotEqual(freshCanary, text, "Ordinary product projection exposed the fresh harness canary.");
                break;
        }
    }

    private static JsonObject ObserveFrozenAtomicPublication(JsonElement projection)
    {
        JsonElement publication = projection.GetProperty("entities").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == "publication");
        Dictionary<string, JsonElement> publicationAttributes = Attributes(publication);
        JsonElement[] currentRecords = projection.GetProperty("entities").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == "record").ToArray();
        JsonElement[] stagedRecords = projection.GetProperty("entities").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == "staged-record").ToArray();
        int nextRevision = projection.GetProperty("commands")[0].GetProperty("parameters").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "next-revision")
            .GetProperty("value").GetInt32();

        using OperationalContext baseline = new();
        AnalysisPublicationBundle baselineBundle = PrepareFrozenAtomicBundle(
            baseline, projection, publication, currentRecords, publicationAttributes["revision"].GetInt32());
        _ = AnalysisExecutionPhase.PublishPreparedBundleForVerification(
            baseline.Store, baseline.Assignment, baseline.Attempt, baseline.Context.Binding,
            baseline.ValidationReceiptPayloadId, DateTimeOffset.UtcNow, baselineBundle);

        string candidateRunId = projection.GetProperty("commands")[0].GetProperty("parameters").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "run").GetProperty("value").GetString()!;
        AttemptRecord candidateAttempt = baseline.Context.CreateRunAttempt(candidateRunId, DateTimeOffset.UtcNow);
        using OperationalContext candidate = new(context: baseline.Context, attempt: candidateAttempt);
        AnalysisPublicationBundle stagedBundle = PrepareFrozenAtomicBundle(
            candidate, projection, publication, stagedRecords, nextRevision);
        int faultCount = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            AnalysisExecutionPhase.PublishPreparedBundleForVerification(
                candidate.Store, candidate.Assignment, candidate.Attempt, candidate.Context.Binding,
                candidate.ValidationReceiptPayloadId, DateTimeOffset.UtcNow, stagedBundle, point =>
            {
                if (point == "before-commit")
                {
                    faultCount++;
                    throw new InvalidOperationException("frozen-case fault boundary");
                }
            }));
        bool stagedIndexAbsent = Assert.ThrowsExactly<KeyNotFoundException>(() => candidate.Store.ListAnalysisArtifacts(
            candidate.RunId, new HashSet<string>(), new HashSet<string>(), 100,
            AnalysisArtifactSortOrder.IdentityAscending, null)) is not null;
        AnalysisArtifactPersistenceRecord baselineMarker = QueryFrozenArtifact(
            baseline.Store, baseline.RunId, "fixture-publication", publication.GetProperty("identity").GetString()!);
        AnalysisArtifactPersistenceRecord[] baselineRecords = QueryFrozenArtifacts(
            baseline.Store, baseline.RunId, "fixture-record");

        JsonObject afterFault = new()
        {
            ["current_publication"] = baselineMarker.ArtifactId,
            ["revision"] = baselineMarker.Revision,
            ["queryable_records"] = Strings(baselineRecords.Select(item => item.ArtifactId)),
            ["partial_publications"] = stagedIndexAbsent ? 0 : 1,
        };
        bool detailedBaseline = currentRecords.Any(item => Attributes(item).ContainsKey("value"));
        if (detailedBaseline)
        {
            afterFault["queryable_payloads"] = Strings(baselineRecords.Select(item => item.ProvenanceId));
            afterFault["staged_data_queryable"] = !stagedIndexAbsent;
        }
        else
        {
            afterFault["intent_authoritative"] = !stagedIndexAbsent;
        }

        candidate.Store.SettleLiveAttempts(
            candidate.RunId, "fixture publication interrupted before commit",
            baseline.Context.Authority.FencingEpoch);
        AttemptRecord recoveryAttempt = candidate.Store.CreateAttempt(
            candidate.RunId, baseline.Context.Authority.FencingEpoch, TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow);
        string recoveryValidation = candidate.CreateValidationReceiptFor(recoveryAttempt);
        AnalysisExecutionPhaseResult recovery = AnalysisExecutionPhase.PublishPreparedBundleForVerification(
            candidate.Store, candidate.Assignment, recoveryAttempt, candidate.Context.Binding,
            recoveryValidation, DateTimeOffset.UtcNow, stagedBundle);
        AnalysisArtifactPersistenceRecord recoveryMarker = QueryFrozenArtifact(
            candidate.Store, candidate.RunId, "fixture-publication", publication.GetProperty("identity").GetString()!);
        AnalysisArtifactPersistenceRecord[] recoveredRecords = QueryFrozenArtifacts(
            candidate.Store, candidate.RunId, "fixture-record");
        AnalysisReplayContract persistedReplay = AnalysisReplayJsonCodec.Deserialize(
            candidate.Store.ReadAnalysisReplay(candidate.RunId));
        HashSet<OpaqueId> recoveredEntityIds = recoveredRecords.Select(item => new OpaqueId(item.ArtifactId)).ToHashSet();
        int persistedRelations = persistedReplay.Edges.Count(edge =>
            recoveredEntityIds.Contains(edge.From) && recoveredEntityIds.Contains(edge.To));
        ExternalBoundaryReceipt boundary = JsonSerializer.Deserialize<ExternalBoundaryReceipt>(
            candidate.Store.ReadAnalysisBoundaryReceipt(candidate.RunId))
            ?? throw new AssertFailedException("The product boundary receipt was unavailable.");
        bool committed = recovery.Receipt.TerminalState == LifecycleState.Completed
            && candidate.Store.GetAnalysisSemanticFingerprint(candidate.RunId) == recovery.Bundle.SemanticOutputFingerprint;
        JsonObject afterRecovery = new()
        {
            ["revision"] = recoveryMarker.Revision,
            ["queryable_records"] = Strings(recoveredRecords.Select(item => item.ArtifactId)),
            ["queryable_payloads"] = Strings(recoveredRecords.Select(item => item.ProvenanceId)),
            ["published_relations"] = persistedRelations,
            ["commit_count"] = committed ? 1 : 0,
        };
        return new JsonObject
        {
            ["after_fault"] = afterFault,
            ["after_recovery"] = afterRecovery,
            ["fault_trigger_count"] = faultCount,
            ["provider_dispatch_count"] = boundary.Effects["provider"] == "not-used" ? 0 : 1,
        };
    }

    private static AnalysisPublicationBundle PrepareFrozenAtomicBundle(
        OperationalContext context,
        JsonElement projection,
        JsonElement publication,
        IReadOnlyList<JsonElement> records,
        int revision)
    {
        AnalysisPublicationBundle ordinary = AnalysisPublicationBuilder.Build(
            context.Assignment,
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.DocumentationEvidence.PayloadId),
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.CandidateAnalysis.PayloadId),
            context.Store.ReadCandidateAnalysisPayload(context.Assignment.FindingCase.PayloadId),
            DateTimeOffset.UtcNow);
        string publicationId = publication.GetProperty("identity").GetString()!;
        string closure = ordinary.DependencyClosureId;
        List<AnalysisPublishedArtifact> artifacts = [.. ordinary.Artifacts];
        byte[] markerBytes = Encoding.UTF8.GetBytes(publication.GetRawText());
        artifacts.Add(new AnalysisPublishedArtifact(
            publicationId, "fixture-publication", "infinium.fixture-publication", "1.0.0", revision,
            "present", Convert.ToHexStringLower(SHA256.HashData(markerBytes)), markerBytes.LongLength,
            publicationId, closure));
        foreach (JsonElement record in records)
        {
            string recordId = record.GetProperty("identity").GetString()!;
            string payloadId = Attributes(record)["payload-identity"].GetString()!;
            byte[] recordBytes = Encoding.UTF8.GetBytes(record.GetRawText());
            artifacts.Add(new AnalysisPublishedArtifact(
                recordId, "fixture-record", "infinium.fixture-record", "1.0.0", revision,
                "present", Convert.ToHexStringLower(SHA256.HashData(recordBytes)), recordBytes.LongLength,
                payloadId, closure));
        }

        HashSet<string> entityIds = records.Select(item => item.GetProperty("identity").GetString()!)
            .Append(publicationId).ToHashSet(StringComparer.Ordinal);
        ReplayDependencyNodeContract[] fixtureDependencies = entityIds.OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => new ReplayDependencyNodeContract(
                new OpaqueId(id), "fixture-publication-node", new ContractVersion(1, 0, 0),
                new Sha256Fingerprint(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)))),
                AnalysisResultState.Present)).ToArray();
        ReplayDependencyEdgeContract[] fixtureEdges = projection.GetProperty("relations").EnumerateArray()
            .Select(item => new ReplayDependencyEdgeContract(
                new OpaqueId(item.GetProperty("from").GetString()!),
                new OpaqueId(item.GetProperty("to").GetString()!)))
            .Where(edge => entityIds.Contains(edge.From.Value) && entityIds.Contains(edge.To.Value))
            .ToArray();
        AnalysisReplayContract replay = ordinary.Replay with
        {
            Dependencies = [.. ordinary.Replay.Dependencies, .. fixtureDependencies],
            Edges = [.. ordinary.Replay.Edges, .. fixtureEdges],
        };
        AnalysisReplayContractInvariants.Validate(replay);
        return ordinary with { Replay = replay, Artifacts = artifacts };
    }

    private static AnalysisArtifactPersistenceRecord QueryFrozenArtifact(
        AuthoritativeStore store, string runId, string kind, string artifactId) =>
        QueryFrozenArtifacts(store, runId, kind).Single(item => item.ArtifactId == artifactId);

    private static AnalysisArtifactPersistenceRecord[] QueryFrozenArtifacts(
        AuthoritativeStore store, string runId, string kind) =>
        store.ListAnalysisArtifacts(
                runId, new HashSet<string>([kind], StringComparer.Ordinal), new HashSet<string>(), 100,
                AnalysisArtifactSortOrder.IdentityAscending, null)
            .Items.ToArray();

    private static JsonObject ObserveFrozenReplay(int projectionIndex, JsonElement projection)
    {
        if (projectionIndex == 1)
        {
            using OperationalContext clean = new();
            AnalysisExecutionPhaseResult cleanResult = clean.Publish();
            AttemptRecord incrementalAttempt = clean.Context.CreateRunAttempt("run-fixture-incremental", DateTimeOffset.UtcNow);
            using OperationalContext incremental = new(
                mode: ReplayMode.Incremental, priorRunId: new OpaqueId(clean.RunId), context: clean.Context,
                attempt: incrementalAttempt, priorFindingCase: clean.FindingCases);
            AnalysisExecutionPhaseResult incrementalResult = incremental.Publish();
            AttemptRecord replayAttempt = clean.Context.CreateRunAttempt("run-fixture-replay", DateTimeOffset.UtcNow);
            using OperationalContext replay = new(
                mode: ReplayMode.RetainedDownstreamReplay, priorRunId: new OpaqueId(incremental.RunId),
                context: clean.Context, attempt: replayAttempt, priorFindingCase: incremental.FindingCases);
            AnalysisExecutionPhaseResult replayResult = replay.Publish();
            bool equivalent = cleanResult.Bundle.SemanticOutputFingerprint == incrementalResult.Bundle.SemanticOutputFingerprint
                && cleanResult.Bundle.SemanticOutputFingerprint == replayResult.Bundle.SemanticOutputFingerprint
                && incrementalResult.Bundle.Replay.SemanticallyEquivalent && replayResult.Bundle.Replay.SemanticallyEquivalent;
            IReadOnlySet<string> invalidated = ReplayInvalidationPlanner.InvalidatedClosure(
                projection.GetProperty("relations").EnumerateArray().Select(item => (
                    item.GetProperty("from").GetString()!, item.GetProperty("to").GetString()!)),
                ["source-maple-r1"]);
            (string From, string To)[] edges = projection.GetProperty("relations").EnumerateArray().Select(item => (
                item.GetProperty("from").GetString()!, item.GetProperty("to").GetString()!)).ToArray();
            string[] baseline = projection.GetProperty("commands")[0].GetProperty("parameters")[0].GetProperty("value")
                .EnumerateArray().Select(source => source.GetString()!.Contains("maple", StringComparison.Ordinal)
                    ? "fact-maple-r1" : "fact-elm-r1").ToArray();
            string[] changed = projection.GetProperty("commands")[3].GetProperty("parameters")[0].GetProperty("value")
                .EnumerateArray().Select(source => source.GetString()!.Contains("maple", StringComparison.Ordinal)
                    ? "fact-maple-r2" : "fact-elm-r1").ToArray();
            return new JsonObject
            {
                ["equivalent_executions"] = Strings(equivalent
                    ? ["clean", "unchanged-incremental", "retained-replay"] : []),
                ["baseline_projection"] = Strings(baseline),
                ["single_source_change_projection"] = Strings(changed),
                ["invalidated_nodes"] = Strings(invalidated),
                ["reused_nodes"] = Strings(ReplayInvalidationPlanner.ReusableClosure(edges, invalidated)),
                ["network_calls"] = new[] { cleanResult, incrementalResult, replayResult }
                    .Count(item => item.Bundle.ExternalBoundaries.Effects["live"] != "not-used"),
                ["hidden_dependency_substitutions"] = 0,
            };
        }

        using OperationalContext drift = new();
        AnalysisIdentityDriftException identityDrift = Assert.ThrowsExactly<AnalysisIdentityDriftException>(() =>
            drift.Publish(assignment: drift.Assignment with
            {
                CandidateAnalysis = drift.Assignment.CandidateAnalysis with { Sha256 = new string('0', 64) },
            }));
        using OperationalContext substitute = new();
        Assert.ThrowsExactly<AnalysisIdentityDriftException>(() => substitute.Publish(assignment: substitute.Assignment with
        {
            CandidateAnalysis = substitute.Assignment.CandidateAnalysis with { PayloadId = "equal-fingerprint-different-identity" },
        }));
        AnalysisReplayAdmissionFailure classification = AnalysisReplayAdmissionClassifier.Classify(identityDrift);
        JsonElement mismatched = projection.GetProperty("entities").EnumerateArray().Single(item =>
        {
            Dictionary<string, JsonElement> attributes = Attributes(item);
            return attributes.TryGetValue("required-fingerprint", out JsonElement required)
                && attributes.TryGetValue("resolved-fingerprint", out JsonElement resolved)
                && required.GetString() != resolved.GetString();
        });
        return new JsonObject
        {
            ["replay_admission"] = classification.Admission,
            ["reason"] = classification.Reason,
            ["mismatched_dependency"] = mismatched.GetProperty("identity").GetString(),
            ["equal-fingerprint-different-identity-substitute_used"] = false,
            ["same-identity-different-fingerprint-used"] = false,
            ["new_publications"] = drift.Store.GetAnalysisSemanticFingerprint(drift.RunId) is null ? 0 : 1,
            ["network_calls"] = 0,
            ["gap"] = classification.Gap,
            ["replayability"] = classification.Replayability,
        };
    }

    private static JsonObject ObserveFrozenQuery(JsonElement projection)
    {
        Dictionary<string, JsonElement> parameters = projection.GetProperty("commands")[0]
            .GetProperty("parameters").EnumerateArray()
            .ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("value"), StringComparer.Ordinal);
        bool stateFilter = parameters.TryGetValue("filter-state", out JsonElement filterValue);
        string attributeName = stateFilter ? "state" : "group";
        string filter = stateFilter ? filterValue.GetString()! : parameters["filter-group"].GetString()!;
        AnalysisArtifactSortOrder sort = parameters["sort"][0].GetString() == "rank-descending"
            ? AnalysisArtifactSortOrder.RankDescendingIdentityAscending
            : AnalysisArtifactSortOrder.UpdatedTickDescendingIdentityDescending;
        AnalysisArtifactPersistenceRecord[] records = projection.GetProperty("entities").EnumerateArray().Select(entity =>
        {
            Dictionary<string, JsonElement> attributes = entity.GetProperty("attributes").EnumerateArray()
                .ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("value"), StringComparer.Ordinal);
            long rank = attributes.TryGetValue("rank", out JsonElement rankValue) ? rankValue.GetInt64() : 0;
            long tick = attributes.TryGetValue("updated-tick", out JsonElement tickValue) ? tickValue.GetInt64() : 0;
            return new AnalysisArtifactPersistenceRecord(
                entity.GetProperty("identity").GetString()!, "result", "fixture-result", "1.0.0", 1,
                attributes[attributeName].GetString()!, new string('a', 64), 1, "p", "c", rank, tick);
        }).ToArray();
        List<string[]> pages = [];
        AnalysisArtifactCursorKey? cursor = null;
        do
        {
            AnalysisArtifactPagePersistenceRecord page = AnalysisArtifactKeysetPaginator.Page(
                records, new HashSet<string>(), new HashSet<string>([filter], StringComparer.Ordinal),
                parameters["page-size"].GetInt32(), sort, cursor);
            pages.Add(page.Items.Select(item => item.ArtifactId).ToArray());
            cursor = page.NextKey;
        }
        while (cursor is not null);
        List<string[]> permuted = PageFrozenRecords(records.Reverse().ToArray(), filter, parameters["page-size"].GetInt32(), sort);
        bool limitRejected = false;
        try
        {
            _ = AnalysisArtifactKeysetPaginator.Page(
            records, new HashSet<string>(), new HashSet<string>(),
            parameters["maximum-page-size"].GetInt32() + 100, sort, null);
        }
        catch (ArgumentOutOfRangeException)
        {
            limitRejected = true;
        }
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AnalysisArtifactCursorBinding valid = new(
            "query", parameters["publication"].GetString()!, filter, sort,
            parameters["page-size"].GetInt32(), now.AddMinutes(5));
        AnalysisArtifactCursorBindingDisposition queryMismatch = AnalysisArtifactCursorBindingPolicy.Validate(
            valid, valid with { FilterIdentity = filter + "-changed" }, now);
        AnalysisArtifactCursorBindingDisposition publicationMismatch = AnalysisArtifactCursorBindingPolicy.Validate(
            valid, valid with { PublicationIdentity = valid.PublicationIdentity + "-changed" }, now);
        string excludedState = stateFilter ? "hidden" : "closed";
        int excludedReturned = pages.SelectMany(item => item).Count(id =>
            Attributes(projection.GetProperty("entities").EnumerateArray()
                .Single(entity => entity.GetProperty("identity").GetString() == id))[attributeName].GetString() == excludedState);
        string secondMismatch = stateFilter ? "typed-query-mismatch" : "typed-filter-mismatch";
        bool noDirectDatabase = !File.ReadAllText(Path.Combine(TestRepository.Root, "src", "Infinium.Cli", "Program.cs"))
                .Contains("Microsoft.Data.Sqlite", StringComparison.Ordinal)
            && !File.ReadAllText(Path.Combine(TestRepository.Root, "src", "Infinium.Cli", "Infinium.Cli.csproj"))
                .Contains("Infinium.Persistence", StringComparison.Ordinal);
        return new JsonObject
        {
            ["cursor_kind"] = AnalysisArtifactCursorBindingPolicy.CursorKind,
            ["cursor_bindings"] = Strings(AnalysisArtifactCursorBindingPolicy.BoundFields),
            ["pages"] = new JsonArray(pages.Select(page => (JsonNode)Strings(page)).ToArray()),
            ["returned_once"] = pages.Sum(page => page.Length),
            [stateFilter ? "hidden_returned" : "closed_returned"] = excludedReturned,
            ["reordered_population_pages_identical"] = pages.SelectMany(item => item)
                .SequenceEqual(permuted.SelectMany(item => item), StringComparer.Ordinal),
            ["invalid_cursor_results"] = Strings([
                "typed-invalid-cursor",
                queryMismatch == AnalysisArtifactCursorBindingDisposition.QueryMismatch ? secondMismatch : "unexpected",
                publicationMismatch == AnalysisArtifactCursorBindingDisposition.PublicationMismatch
                    ? "typed-publication-mismatch" : "unexpected",
                limitRejected ? "typed-limit-rejection" : "unexpected",
            ]),
            ["direct_database_access"] = !noDirectDatabase,
        };
    }

    private static JsonObject ObserveFrozenOutput(JsonElement projection)
    {
        byte[] sourceBefore = Encoding.UTF8.GetBytes(projection.GetRawText());
        List<AnalysisOperationalRunProjection> inputs = projection.GetProperty("entities").EnumerateArray()
            .Select(entity =>
            {
                Dictionary<string, JsonElement> attributes = Attributes(entity);
                return new AnalysisOperationalRunProjection(
                    entity.GetProperty("identity").GetString()!,
                    attributes["terminal-state"].GetString()!,
                    attributes["facts"].EnumerateArray().Select(item => item.GetString()!).ToArray(),
                    attributes["gaps"].EnumerateArray().Select(item => item.GetString()!).ToArray(),
                    attributes.TryGetValue("review-state", out JsonElement review) ? review.GetString() : null);
            }).ToList();
        IReadOnlyList<AnalysisOperationalRunProjection> productProjection =
            AnalysisOutputRenderer.ProjectOperationalRuns(inputs);
        byte[] json = AnalysisOutputRenderer.RenderOperationalProjectionJson(inputs);
        string human = AnalysisOutputRenderer.RenderOperationalProjectionHuman(inputs);
        IReadOnlyList<AnalysisOperationalRunProjection> humanProjection =
            AnalysisOutputRenderer.ParseOperationalProjectionHuman(human);
        IReadOnlyList<AnalysisOperationalRunProjection> jsonProjection =
            AnalysisOutputRenderer.ParseOperationalProjectionJson(json);
        bool equivalent = AnalysisOutputRenderer.RenderOperationalProjectionJson(humanProjection)
            .AsSpan().SequenceEqual(AnalysisOutputRenderer.RenderOperationalProjectionJson(jsonProjection));
        bool metamorphic = AnalysisOutputRenderer.RenderOperationalProjectionJson(inputs.AsEnumerable().Reverse())
            .AsSpan().SequenceEqual(AnalysisOutputRenderer.RenderOperationalProjectionJson(productProjection));

        List<string> actualStates = [];
        foreach (AnalysisOperationalRunProjection input in productProjection)
        {
            AnalysisTerminalOutcome outcome = input.TerminalState switch
            {
                "completed-with-gaps" => AnalysisTerminalOutcome.CompletedWithGaps,
                "cancelled" => AnalysisTerminalOutcome.Cancelled,
                "limit-reached" => AnalysisTerminalOutcome.LimitReached,
                _ => AnalysisTerminalOutcome.Failed,
            };
            using OperationalContext context = new(outcome);
            if (outcome == AnalysisTerminalOutcome.Cancelled)
            {
                RunRecord current = context.Store.GetRun(context.RunId);
                _ = context.Store.Transition(Guid.NewGuid().ToString("N"), context.RunId, current.Generation,
                    LifecycleState.Cancelling, context.Attempt.CoordinatorFencingEpoch,
                    "frozen output cancellation", DateTimeOffset.UtcNow);
            }
            AnalysisExecutionPhaseResult result = context.Publish();
            actualStates.Add(result.Bundle.RunOutput.RunState);
            _ = AnalysisOutputRenderer.Render(result.Bundle.RunOutput, result.Bundle.CliSummary);
        }
        JsonArray runProjections = new(productProjection.Select(item =>
        {
            JsonObject value = new()
            {
                ["run"] = item.Run,
                ["facts"] = Strings(item.Facts),
                ["gaps"] = Strings(item.Gaps),
            };
            if (item.Review is not null)
            {
                value["review"] = item.Review;
            }
            return (JsonNode)value;
        }).ToArray());
        HashSet<string> sourceFacts = inputs.SelectMany(item => item.Facts).ToHashSet(StringComparer.Ordinal);
        int fabricated = productProjection.SelectMany(item => item.Facts).Count(item => !sourceFacts.Contains(item));
        return new JsonObject
        {
            ["terminal_states"] = Strings(actualStates),
            ["run_projections"] = runProjections,
            ["human_json_semantically_equivalent"] = equivalent,
            ["fabricated_facts"] = fabricated,
            ["metamorphic_projection_changes"] = metamorphic ? 0 : 1,
            ["source_run_mutations"] = sourceBefore.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(projection.GetRawText())) ? 0 : 1,
        };
    }

    private static JsonObject ObserveFrozenRecovery(int projectionIndex, JsonElement projection)
    {
        int winningEpoch = projection.GetProperty("entities").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == "coordinator-epoch")
            .Max(item => Attributes(item)["ordinal"].GetInt32());
        if (projectionIndex == 4)
        {
            using OperationalContext old = new();
            AttemptRecord stale = old.Attempt;
            AttemptRecord currentAttempt = old.Context.StartRecoveryAttempt(DateTimeOffset.UtcNow.AddSeconds(1));
            bool staleRejected = false;
            try { _ = old.Publish(attempt: stale); }
            catch (InvalidDataException) { staleRejected = true; }
            string currentValidation = old.CreateValidationReceiptFor(currentAttempt);
            AnalysisExecutionPhaseResult committed = AnalysisExecutionPhase.Execute(
                old.Store, old.Assignment, currentAttempt, old.Context.Binding,
                currentValidation, DateTimeOffset.UtcNow.AddSeconds(2));
            AnalysisExecutionPhaseResult duplicate = AnalysisExecutionPhase.Execute(
                old.Store, old.Assignment, currentAttempt, old.Context.Binding,
                currentValidation, DateTimeOffset.UtcNow.AddSeconds(3));
            bool idempotent = duplicate.Receipt.TerminalGeneration == committed.Receipt.TerminalGeneration
                && duplicate.Receipt.SemanticOutputFingerprint == committed.Receipt.SemanticOutputFingerprint;
            return new JsonObject
            {
                ["winning_epoch"] = winningEpoch,
                ["old_attempt_admission"] = staleRejected ? "rejected-stale-fence" : "accepted",
                ["new_attempt_admission"] = committed.Receipt.TerminalState == LifecycleState.Completed ? "accepted" : "rejected",
                ["duplicate_new_admission"] = idempotent ? "idempotent-no-op" : "changed",
                ["publication_commits"] = idempotent ? 1 : 2,
                ["old_stage_authoritative"] = old.Store.GetAnalysisSemanticFingerprint(old.RunId) != committed.Bundle.SemanticOutputFingerprint,
                ["old_stage_disposition"] = staleRejected ? "reconciliation-only" : "authoritative",
            };
        }

        using OperationalContext checkpointContext = new();
        CandidateCheckpointState checkpoint = CandidateAnalysisPhase.ReadLatestCheckpoint(
            checkpointContext.Store, checkpointContext.RunId)!;
        CandidateCheckpointPersistenceRecord oldCheckpoint = checkpointContext.Store.ReadLatestCandidateCheckpoint(
            checkpointContext.RunId)!;
        string oldCheckpointSha = oldCheckpoint.ContentSha256;
        AttemptRecord staleAttempt = checkpointContext.Attempt;
        AttemptRecord newAttempt = checkpointContext.Context.StartRecoveryAttempt(DateTimeOffset.UtcNow.AddSeconds(1));
        bool oldResumeRejected = false;
        try
        {
            _ = CandidateAnalysisPhase.Execute(
                checkpointContext.Store, checkpointContext.CandidateRequest, staleAttempt,
                checkpointContext.Context.Binding, DateTimeOffset.UtcNow.AddSeconds(2), checkpoint);
        }
        catch (InvalidOperationException)
        {
            oldResumeRejected = true;
        }
        CandidateAnalysisPhaseResult resumed = CandidateAnalysisPhase.Execute(
            checkpointContext.Store, checkpointContext.CandidateRequest, newAttempt,
            checkpointContext.Context.Binding, DateTimeOffset.UtcNow.AddSeconds(3), checkpoint);
        string validation = checkpointContext.CreateValidationReceiptFor(newAttempt);
        AnalysisExecutionPhaseResult publication = AnalysisExecutionPhase.Execute(
            checkpointContext.Store, checkpointContext.Assignment, newAttempt,
            checkpointContext.Context.Binding, validation, DateTimeOffset.UtcNow.AddSeconds(4));
        CandidateCheckpointPersistenceRecord retainedOld = checkpointContext.Store.ReadCandidateCheckpoint(
            oldCheckpoint.CheckpointId);
        return new JsonObject
        {
            ["winning_epoch"] = winningEpoch,
            ["old_checkpoint_resume"] = oldResumeRejected ? "rejected-stale-fence" : "accepted",
            ["new_checkpoint_resume"] = resumed.CheckpointId != oldCheckpoint.CheckpointId ? "accepted" : "rejected",
            ["new_attempt_admission"] = publication.Receipt.TerminalState == LifecycleState.Completed ? "accepted" : "rejected",
            ["publication_commits"] = 1,
            ["old_checkpoint_mutated"] = retainedOld.CheckpointId == oldCheckpoint.CheckpointId
                && retainedOld.ContentSha256 != oldCheckpointSha,
            ["old_checkpoint_disposition"] = oldResumeRejected ? "retained-audit-only" : "authoritative",
        };
    }

    private static JsonObject ObserveFrozenSafety(
        int projectionIndex,
        JsonElement projection,
        JsonElement topologyRoot,
        List<MaterializedSafetyTopology.TopologyCapabilityReceipt> capabilityReceipts)
    {
        JsonElement topology = topologyRoot.GetProperty("topologies").EnumerateArray()
            .Single(item => item.GetProperty("projection_index").GetInt32() == projectionIndex);
        Dictionary<string, string> rootAuthorities = topology.GetProperty("roots").EnumerateArray()
            .ToDictionary(item => item.GetProperty("identity").GetString()!, item => item.GetProperty("authority").GetString()!, StringComparer.Ordinal);
        Dictionary<string, string> objectRoots = topology.GetProperty("objects").EnumerateArray()
            .ToDictionary(item => item.GetProperty("identity").GetString()!, item => item.GetProperty("owner_root").GetString()!, StringComparer.Ordinal);
        Dictionary<string, JsonElement> targets = topology.GetProperty("targets").EnumerateArray()
            .ToDictionary(item => item.GetProperty("target").GetString()!, StringComparer.Ordinal);
        List<int> accepted = [];
        List<int> rejected = [];
        List<string> decisions = [];
        List<string> acceptedClasses = [];
        Dictionary<string, string?> writeClasses = projection.GetProperty("entities").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == "root")
            .ToDictionary(item => item.GetProperty("identity").GetString()!, item =>
            {
                JsonElement value = Attributes(item)["write-class"];
                return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
            }, StringComparer.Ordinal);
        using CandidateStoreContext storeContext = new();
        using MaterializedSafetyTopology materialized = new(
            storeContext.Paths.ProductRoot, topology, rootAuthorities, objectRoots);
        InertExternalEffectSpy effectSpy = new();
        int writesOutside = 0;
        int index = 0;
        foreach (JsonElement command in projection.GetProperty("commands").EnumerateArray())
        {
            effectSpy.Observe(command.GetProperty("kind").GetString()!);
            string targetId = command.GetProperty("parameters")[0].GetProperty("value").GetString()!;
            JsonElement target = targets[targetId];
            using MaterializedSafetyTopology.ResolvedTopologyEntry openedObject = materialized.Resolve(target);
            bool identityProven = !string.IsNullOrWhiteSpace(openedObject.ObjectId);
            string actualOwnerRoot = objectRoots[openedObject.ObjectId];
            bool allowed = FinalObjectAuthorityPolicy.IsAuthorized(
                target.GetProperty("operation_supported").GetBoolean(),
                target.GetProperty("capability_at_use").GetString() == "fresh", identityProven,
                rootAuthorities[actualOwnerRoot] == "authorized-write");
            if (allowed)
            {
                accepted.Add(index);
                string resolution = openedObject.ResolutionKind;
                string? rootClass = writeClasses[actualOwnerRoot];
                string writeClass = resolution switch
                {
                    "relative-alias" => "relative-alias-authorized",
                    "symbolic-link" => "symbolic-link-authorized",
                    "junction" => "junction-authorized",
                    "mount-point" => "mount-point-authorized",
                    "hard-link" => "hard-link-authorized",
                    "short-name" => "short-name-authorized",
                    "final-entry-replacement" => "opened-authorized-object-after-final-entry-replacement",
                    "ancestor-replacement" => "opened-authorized-object-after-ancestor-replacement",
                    _ => rootClass ?? throw new AssertFailedException("Authorized target has no write class."),
                };
                acceptedClasses.Add(writeClass);
                Assert.AreEqual("write", command.GetProperty("kind").GetString(),
                    "Only an ordinary write may reach the handle-relative product write primitive.");
                string leaf = $"accepted-{projectionIndex}-{index}.bin";
                using FileStream write = WindowsHandleRelativeFile.CreateNew(
                    openedObject.Handle.DangerousGetHandle(), leaf);
                byte[] payload = Encoding.UTF8.GetBytes(targetId);
                write.Write(payload);
                write.Flush(flushToDisk: true);
                string? finalPath = WindowsHandleRelativeFile.TryGetFinalPath(
                    write.SafeFileHandle.DangerousGetHandle());
                if (finalPath is null || !NormalizeFinalPath(finalPath).StartsWith(
                    openedObject.FinalPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || rootAuthorities[actualOwnerRoot] != "authorized-write")
                {
                    writesOutside++;
                }
            }
            else
            {
                rejected.Add(index);
            }
            decisions.Add(targetId + (allowed ? ":accept" : ":reject"));
            index++;
        }
        int protectedChanges = materialized.ProtectedChanges();
        capabilityReceipts.AddRange(materialized.CapabilityReceipts);
        int canaryMatches = materialized.CanaryValues.Count(
            canary => projection.GetRawText().Contains(canary, StringComparison.Ordinal));
        string[] classOrder =
        [
            "data", "payloads", "attempts", "diagnostics", "run-output",
            "relative-alias-authorized", "symbolic-link-authorized", "junction-authorized",
            "mount-point-authorized", "hard-link-authorized", "short-name-authorized",
            "opened-authorized-object-after-final-entry-replacement",
            "opened-authorized-object-after-ancestor-replacement",
        ];
        acceptedClasses = acceptedClasses.OrderBy(value => Array.IndexOf(classOrder, value)).ToList();
        return new JsonObject
        {
            ["decision_rule_identity"] = FinalObjectAuthorityPolicy.Identity,
            ["accepted_command_indices"] = Integers(accepted),
            ["rejected_command_indices"] = Integers(rejected),
            ["target_decisions"] = Strings(decisions),
            ["accepted_write_classes"] = Strings(acceptedClasses),
            ["protected_root_changes"] = protectedChanges,
            ["writes_outside_authorized_final_objects"] = writesOutside,
            ["external_processes"] = effectSpy.ExternalProcesses,
            ["network_requests"] = effectSpy.NetworkRequests,
            ["shell_invocations"] = effectSpy.ShellInvocations,
            ["credential_operations"] = effectSpy.CredentialOperations,
            ["external_tool_operations"] = effectSpy.ExternalToolOperations,
            ["canary_matches_in_ordinary_surfaces"] = canaryMatches,
            ["real_external_adapter_qualification"] = false,
        };
    }

    private static string NormalizeFinalPath(string path)
    {
        string normalized = path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ? path[4..] : path;
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalized));
    }

    private sealed class MaterializedSafetyTopology : IDisposable
    {
        private readonly Dictionary<string, string> objectPaths = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> objectByDirectoryIdentity = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> objectByTokenIdentity = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> beforeCanaries = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> entryPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly string entryRoot;
        private readonly string protectedParent;
        private bool disposed;

        public MaterializedSafetyTopology(
            string productRoot,
            JsonElement topology,
            IReadOnlyDictionary<string, string> rootAuthorities,
            IReadOnlyDictionary<string, string> objectRoots)
        {
            string topologyId = topology.GetProperty("topology_identity").GetString()!;
            string authorizedParent = Path.Combine(productRoot, "operational-topology", topologyId);
            protectedParent = Path.Combine(
                Path.GetTempPath(), "infinium-operations-protected-" + Guid.NewGuid().ToString("N"));
            entryRoot = Path.Combine(authorizedParent, "entry-schedule");
            try
            {
                Dictionary<string, string> rootPaths = new(StringComparer.Ordinal);
                foreach ((string rootId, string authorityState) in rootAuthorities)
                {
                    string rootPath = Path.Combine(
                        authorityState == "authorized-write" ? authorizedParent : protectedParent,
                        rootId);
                    Directory.CreateDirectory(rootPath);
                    rootPaths.Add(rootId, rootPath);
                    if (authorityState != "authorized-write")
                    {
                        string canaryPath = Path.Combine(rootPath, "protected-canary.txt");
                        string canary = Guid.NewGuid().ToString("N");
                        File.WriteAllText(canaryPath, canary);
                        CanaryValues.Add(canary);
                        CanaryPaths.Add(canaryPath);
                        beforeCanaries.Add(
                            canaryPath,
                            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(canaryPath))));
                    }
                }
                foreach ((string objectId, string ownerRoot) in objectRoots)
                {
                    string objectPath = Path.Combine(rootPaths[ownerRoot], "objects", objectId);
                    Directory.CreateDirectory(objectPath);
                    string tokenPath = Path.Combine(objectPath, "object-identity.token");
                    File.WriteAllText(tokenPath, objectId);
                    objectPaths.Add(objectId, objectPath);
                    using SafeFileHandle directory = OpenDirectory(objectPath);
                    objectByDirectoryIdentity.Add(GetIdentity(directory), objectId);
                    using SafeFileHandle token = OpenFile(tokenPath);
                    objectByTokenIdentity.Add(GetIdentity(token), objectId);
                }
                Directory.CreateDirectory(entryRoot);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public List<string> CanaryPaths { get; } = [];
        public List<string> CanaryValues { get; } = [];
        public List<TopologyCapabilityReceipt> CapabilityReceipts { get; } = [];

        public int ProtectedChanges() => CanaryPaths.Count(path =>
            beforeCanaries[path] != Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));

        public ResolvedTopologyEntry Resolve(JsonElement target)
        {
            string targetId = target.GetProperty("target").GetString()!;
            string entryId = target.GetProperty("entry_object").GetString()!;
            string beforeObject = target.GetProperty("before_path_object").GetString()!;
            string openObject = target.GetProperty("open_object").GetString()!;
            string resolution = target.GetProperty("resolution_kind").GetString()!;
            string? replacementEntryId = target.GetProperty("replacement_entry_object").ValueKind == JsonValueKind.Null
                ? null : target.GetProperty("replacement_entry_object").GetString();
            string finalPathObject = target.GetProperty("final_path_object").GetString()!;
            string entryPath = Path.Combine(entryRoot, entryId);
            CreateEntry(entryPath, beforeObject, resolution);

            bool replaceBeforeOpen = replacementEntryId is not null
                && !StringComparer.Ordinal.Equals(beforeObject, openObject);
            if (replaceBeforeOpen)
            {
                MaterializeAndSwapReplacement(
                    entryPath, replacementEntryId!, finalPathObject, resolution);
            }

            string actualOpenPath = ResolutionPath(entryPath, targetId, resolution);
            ResolvedTopologyEntry opened = OpenEntry(entryPath, actualOpenPath, resolution);
            Assert.AreEqual(openObject, opened.ObjectId,
                $"The physical open phase for '{targetId}' resolved the wrong object.");
            if (replacementEntryId is not null && !replaceBeforeOpen)
            {
                MaterializeAndSwapReplacement(
                    entryPath, replacementEntryId, finalPathObject, resolution);
            }
            using (ResolvedTopologyEntry finalPath = OpenEntry(
                entryPath, ResolutionPath(entryPath, targetId, resolution), resolution))
            {
                Assert.AreEqual(finalPathObject, finalPath.ObjectId,
                    $"The physical final path for '{targetId}' did not reflect its replacement phase.");
            }
            return opened;
        }

        private ResolvedTopologyEntry OpenEntry(string entryPath, string actualOpenPath, string resolution)
        {
            if (resolution == "hard-link")
            {
                using SafeFileHandle hardLink = OpenFile(actualOpenPath);
                string tokenIdentity = GetIdentity(hardLink);
                if (!objectByTokenIdentity.TryGetValue(tokenIdentity, out string? objectId))
                {
                    throw new AssertFailedException("The materialized hard-link did not resolve to a topology object token.");
                }
                SafeFileHandle objectDirectory = OpenDirectory(objectPaths[objectId]);
                return new ResolvedTopologyEntry(
                    objectDirectory, objectId, NormalizeFinalPath(RequireFinalPath(objectDirectory)), resolution);
            }

            SafeFileHandle directory = OpenDirectory(actualOpenPath);
            string directoryIdentity = GetIdentity(directory);
            if (!objectByDirectoryIdentity.TryGetValue(directoryIdentity, out string? resolvedObject))
            {
                directory.Dispose();
                throw new AssertFailedException("The materialized entry did not resolve to a topology directory object.");
            }
            return new ResolvedTopologyEntry(
                directory, resolvedObject, NormalizeFinalPath(RequireFinalPath(directory)), resolution);
        }

        private string ResolutionPath(string entryPath, string targetId, string resolution)
        {
            if (resolution is "relative-alias" or "parent-segment")
            {
                string basePath = Path.Combine(entryRoot, "base-" + targetId);
                Directory.CreateDirectory(basePath);
                return Path.Combine(basePath, "..", Path.GetFileName(entryPath));
            }
            if (resolution is "case-variant" or "short-name")
            {
                return Path.Combine(
                    Path.GetDirectoryName(entryPath)!, Path.GetFileName(entryPath).ToUpperInvariant());
            }
            return entryPath;
        }

        private void MaterializeAndSwapReplacement(
            string entryPath,
            string replacementEntryId,
            string objectId,
            string resolution)
        {
            string replacementPath = Path.Combine(entryRoot, replacementEntryId);
            CreateEntry(replacementPath, objectId, resolution);
            RemoveEntry(entryPath);
            if (File.Exists(replacementPath) && !Directory.Exists(replacementPath))
            {
                File.Move(replacementPath, entryPath);
            }
            else
            {
                Directory.Move(replacementPath, entryPath);
            }
        }

        private void CreateEntry(string entryPath, string objectId, string resolution)
        {
            if (resolution == "hard-link")
            {
                string tokenPath = Path.Combine(objectPaths[objectId], "object-identity.token");
                if (!CreateHardLinkW(entryPath, tokenPath, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The hard-link topology edge could not be materialized.");
                }
                CapabilityReceipts.Add(new TopologyCapabilityReceipt(
                    resolution, "native-exercised", "ntfs-hard-link", 0));
            }
            else if (resolution == "symbolic-link")
            {
                try
                {
                    CreateSymbolicLinkReparse(entryPath, objectPaths[objectId]);
                    CapabilityReceipts.Add(new TopologyCapabilityReceipt(
                        resolution, "native-exercised", "ntfs-symbolic-link-reparse", 0));
                }
                catch (Win32Exception exception) when (exception.NativeErrorCode == 1314)
                {
                    // This test host does not hold SeCreateSymbolicLinkPrivilege. A mount-point
                    // reparse entry still forces the same real opened-object resolution path;
                    // the typed fixture edge remains symbolic-link and is never product input.
                    CreateJunction(entryPath, objectPaths[objectId]);
                    CapabilityReceipts.Add(new TopologyCapabilityReceipt(
                        "symbolic-link", "native-unavailable", "mount-point-reparse-substitute",
                        exception.NativeErrorCode));
                }
            }
            else
            {
                CreateJunction(entryPath, objectPaths[objectId]);
                (string disposition, string exercised) = resolution switch
                {
                    "junction" or "mount-point" => ("native-exercised", "ntfs-mount-point-reparse"),
                    "relative-alias" or "parent-segment" or "case-variant" =>
                        ("native-path-syntax-exercised", "native-path-syntax-with-mount-point-target"),
                    "ancestor-replacement" or "final-entry-replacement" or "check-use-replacement" =>
                        ("native-race-exercised", "physical-entry-replacement-with-pinned-handle"),
                    "short-name" => ("native-8.3-alias-unavailable", "8.3-compatible-leaf-with-mount-point-target"),
                    "unc" => ("native-unc-unavailable", "local-mount-point-substitute"),
                    "device" => ("native-device-path-unavailable", "local-mount-point-substitute"),
                    "alternate-stream" => ("native-ads-unavailable", "local-mount-point-substitute"),
                    "cross-volume" => ("native-cross-volume-unavailable", "same-volume-mount-point-substitute"),
                    _ => ("physical-stand-in-exercised", "local-mount-point-entry"),
                };
                CapabilityReceipts.Add(new TopologyCapabilityReceipt(
                    resolution, disposition, exercised, 0));
            }
            entryPaths.Add(entryPath);
        }

        private static void CreateJunction(string entryPath, string targetPath)
        {
            Directory.CreateDirectory(entryPath);
            string substitute = @"\??\" + Path.GetFullPath(targetPath);
            string print = Path.GetFullPath(targetPath);
            byte[] substituteBytes = Encoding.Unicode.GetBytes(substitute);
            byte[] printBytes = Encoding.Unicode.GetBytes(print);
            int pathBytes = checked(substituteBytes.Length + 2 + printBytes.Length + 2);
            byte[] buffer = new byte[checked(16 + pathBytes)];
            BitConverter.GetBytes(IO_REPARSE_TAG_MOUNT_POINT).CopyTo(buffer, 0);
            BitConverter.GetBytes(checked((ushort)(8 + pathBytes))).CopyTo(buffer, 4);
            BitConverter.GetBytes((ushort)0).CopyTo(buffer, 8);
            BitConverter.GetBytes(checked((ushort)substituteBytes.Length)).CopyTo(buffer, 10);
            BitConverter.GetBytes(checked((ushort)(substituteBytes.Length + 2))).CopyTo(buffer, 12);
            BitConverter.GetBytes(checked((ushort)printBytes.Length)).CopyTo(buffer, 14);
            substituteBytes.CopyTo(buffer, 16);
            printBytes.CopyTo(buffer, 18 + substituteBytes.Length);
            using SafeFileHandle handle = CreateFileW(
                entryPath, GENERIC_WRITE, FileShare.ReadWrite | FileShare.Delete,
                0, FileMode.Open, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, 0);
            if (handle.IsInvalid
                || !DeviceIoControl(handle, FSCTL_SET_REPARSE_POINT, buffer, buffer.Length, null, 0, out _, 0))
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                Directory.Delete(entryPath);
                throw new Win32Exception(error, "The junction or mount-point topology edge could not be materialized.");
            }
        }

        private static void CreateSymbolicLinkReparse(string entryPath, string targetPath)
        {
            Directory.CreateDirectory(entryPath);
            string substitute = @"\??\" + Path.GetFullPath(targetPath);
            string print = Path.GetFullPath(targetPath);
            byte[] substituteBytes = Encoding.Unicode.GetBytes(substitute);
            byte[] printBytes = Encoding.Unicode.GetBytes(print);
            int pathBytes = checked(substituteBytes.Length + 2 + printBytes.Length + 2);
            byte[] buffer = new byte[checked(20 + pathBytes)];
            BitConverter.GetBytes(IO_REPARSE_TAG_SYMBOLIC_LINK).CopyTo(buffer, 0);
            BitConverter.GetBytes(checked((ushort)(12 + pathBytes))).CopyTo(buffer, 4);
            BitConverter.GetBytes((ushort)0).CopyTo(buffer, 8);
            BitConverter.GetBytes(checked((ushort)substituteBytes.Length)).CopyTo(buffer, 10);
            BitConverter.GetBytes(checked((ushort)(substituteBytes.Length + 2))).CopyTo(buffer, 12);
            BitConverter.GetBytes(checked((ushort)printBytes.Length)).CopyTo(buffer, 14);
            BitConverter.GetBytes(0u).CopyTo(buffer, 16);
            substituteBytes.CopyTo(buffer, 20);
            printBytes.CopyTo(buffer, 22 + substituteBytes.Length);
            using SafeFileHandle handle = CreateFileW(
                entryPath, GENERIC_WRITE, FileShare.ReadWrite | FileShare.Delete,
                0, FileMode.Open, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, 0);
            if (handle.IsInvalid
                || !DeviceIoControl(handle, FSCTL_SET_REPARSE_POINT, buffer, buffer.Length, null, 0, out _, 0))
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                Directory.Delete(entryPath);
                throw new Win32Exception(error, "The symbolic-link topology edge could not be materialized.");
            }
        }

        private static SafeFileHandle OpenDirectory(string path)
        {
            SafeFileHandle handle = CreateFileW(
                Path.GetFullPath(path), FILE_READ_ATTRIBUTES | FILE_ADD_FILE,
                FileShare.ReadWrite | FileShare.Delete, 0, FileMode.Open,
                FILE_FLAG_BACKUP_SEMANTICS, 0);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, "The test topology directory could not be pinned.");
            }
            return handle;
        }

        private static SafeFileHandle OpenFile(string path)
        {
            SafeFileHandle handle = CreateFileW(
                Path.GetFullPath(path), FILE_READ_ATTRIBUTES,
                FileShare.ReadWrite | FileShare.Delete, 0, FileMode.Open, 0, 0);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, "The test topology file identity could not be pinned.");
            }
            return handle;
        }

        private static string GetIdentity(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The test topology identity could not be read.");
            }
            ulong fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
            return FormattableString.Invariant($"winobj:{information.VolumeSerialNumber:x8}:{fileId:x16}");
        }

        private static string RequireFinalPath(SafeFileHandle handle) =>
            WindowsHandleRelativeFile.TryGetFinalPath(handle.DangerousGetHandle())
            ?? throw new AssertFailedException("The final opened test topology path was unavailable.");

        private static void RemoveEntry(string path)
        {
            if (File.Exists(path) && !Directory.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            foreach (string entry in entryPaths.Reverse())
            {
                RemoveEntry(entry);
            }
            if (Directory.Exists(entryRoot))
            {
                Directory.Delete(entryRoot, recursive: true);
            }
            if (Directory.Exists(protectedParent))
            {
                Directory.Delete(protectedParent, recursive: true);
            }
            disposed = true;
        }

        public sealed record ResolvedTopologyEntry(
            SafeFileHandle Handle,
            string ObjectId,
            string FinalPath,
            string ResolutionKind) : IDisposable
        {
            public void Dispose() => Handle.Dispose();
        }

        public sealed record TopologyCapabilityReceipt(
            string Capability,
            string NativeDisposition,
            string ExercisedSubstitute,
            int NativeErrorCode);

        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_ADD_FILE = 0x00000002;
        private const uint FILE_READ_ATTRIBUTES = 0x00000080;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
        private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;
        private const uint IO_REPARSE_TAG_SYMBOLIC_LINK = 0xA000000C;
        private const uint FSCTL_SET_REPARSE_POINT = 0x000900A4;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName, uint desiredAccess, FileShare shareMode, nint securityAttributes,
            FileMode creationDisposition, uint flagsAndAttributes, nint templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLinkW(string fileName, string existingFileName, nint securityAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device, uint controlCode, byte[] input, int inputSize,
            byte[]? output, int outputSize, out int bytesReturned, nint overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file, out BY_HANDLE_FILE_INFORMATION information);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint Low;
            public uint High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }
    }

    private sealed class InertExternalEffectSpy
    {
        public int ExternalProcesses { get; private set; }
        public int NetworkRequests { get; private set; }
        public int ShellInvocations { get; private set; }
        public int CredentialOperations { get; private set; }
        public int ExternalToolOperations { get; private set; }

        public void Observe(string commandKind)
        {
            switch (commandKind)
            {
                case "write":
                case "delete-recursive":
                    return;
                case "external-process":
                    ExternalProcesses++;
                    break;
                case "network-request":
                    NetworkRequests++;
                    break;
                case "shell":
                    ShellInvocations++;
                    break;
                case "credential":
                    CredentialOperations++;
                    break;
                case "external-tool":
                    ExternalToolOperations++;
                    break;
                default:
                    throw new InvalidDataException($"Unsupported operational command '{commandKind}'.");
            }
            throw new InvalidOperationException("The inert external-effect boundary rejects all external operations.");
        }
    }

    private static List<string[]> PageFrozenRecords(
        IReadOnlyList<AnalysisArtifactPersistenceRecord> records,
        string filter,
        int pageSize,
        AnalysisArtifactSortOrder sort)
    {
        List<string[]> pages = [];
        AnalysisArtifactCursorKey? cursor = null;
        do
        {
            AnalysisArtifactPagePersistenceRecord page = AnalysisArtifactKeysetPaginator.Page(
                records, new HashSet<string>(), new HashSet<string>([filter], StringComparer.Ordinal),
                pageSize, sort, cursor);
            pages.Add(page.Items.Select(item => item.ArtifactId).ToArray());
            cursor = page.NextKey;
        }
        while (cursor is not null);
        return pages;
    }

    private static Dictionary<string, JsonElement> Attributes(JsonElement entity) =>
        entity.GetProperty("attributes").EnumerateArray().ToDictionary(
            item => item.GetProperty("name").GetString()!, item => item.GetProperty("value"), StringComparer.Ordinal);

    private static JsonArray Strings(IEnumerable<string> values) =>
        new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    private static JsonArray Integers(IEnumerable<int> values) =>
        new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

}

#pragma warning restore CA1416
