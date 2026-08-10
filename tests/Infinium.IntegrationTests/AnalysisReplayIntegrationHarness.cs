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
    private sealed record ProjectionValidationReceipt(
        string CaseId,
        string SchemaSha256,
        string ProjectionSha256,
        long ProjectionByteLength,
        string Disposition);

    internal sealed class OperationalContext : IDisposable
    {
        private readonly AttemptStagingAuthority staging;
        private readonly bool ownsContext;

        public OperationalContext(
            AnalysisTerminalOutcome terminal = AnalysisTerminalOutcome.Completed,
            ReplayMode mode = ReplayMode.Clean,
            OpaqueId? priorRunId = null,
            CandidateStoreContext? context = null,
            AttemptRecord? attempt = null,
            FindingCaseContract? priorFindingCase = null,
            bool unavailableDependency = false)
        {
            ownsContext = context is null;
            Context = context ?? new CandidateStoreContext();
            Store = Context.Store;
            Attempt = attempt ?? Context.Attempt;
            RunId = Attempt.RunId;
            staging = Store.Paths.CreateAttemptStagingDirectory(Attempt.AttemptId);
            DocumentationEvidencePhaseResult docs = DocumentationEvidencePhase.Execute(Store, DocumentationRequest(RunId));
            CausalJoinPopulationMember lead = CandidatePipelineIntegrationTests.Member(
                "lead", inputState: CausalJoinInputState.Ambiguous) with
            {
                ContradictingEvidenceIds = [CandidatePipelineIntegrationTests.Id("contradiction-lead")],
            };
            CandidatePipelineRequest candidateRequest = CandidatePipelineIntegrationTests.Request(
                [CandidatePipelineIntegrationTests.Member("alpha"), CandidatePipelineIntegrationTests.Member("beta"), lead],
                RunId, "population-operations");
            SemanticAnalysisContextContract semanticContext = CandidatePipelineIntegrationTests.SemanticContext(
                Context.Binding.AnalysisContextId);
            candidateRequest = candidateRequest with
            {
                ExecutionInput = candidateRequest.ExecutionInput! with
                {
                    AnalysisContext = new(semanticContext.ContextId, semanticContext.SchemaVersion,
                        semanticContext.CanonicalFingerprint, "retained"),
                    Mode = mode,
                    PriorRunId = priorRunId,
                    BethesdaSemanticInput = unavailableDependency
                        ? candidateRequest.ExecutionInput.BethesdaSemanticInput with { Availability = "unavailable" }
                        : candidateRequest.ExecutionInput.BethesdaSemanticInput,
                    SourceInputs = candidateRequest.ExecutionInput.SourceInputs
                        .Concat(docs.Evidence.Revisions.Select(item => new ArtifactReferenceContract(
                            item.SourceId, new ContractVersion(1, 0, 0), item.ByteFingerprint, "retained")))
                        .DistinctBy(item => item.ArtifactId)
                        .OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal).ToArray(),
                },
            };
            CandidateRequest = candidateRequest;
            CandidateAnalysisPhaseResult candidates = CandidateAnalysisPhase.Execute(
                Store, candidateRequest, Attempt, Context.Binding, DateTimeOffset.UtcNow);
            FindingCaseAnalysisPhaseResult findingCases = FindingCaseAnalysisPhase.Execute(
                Store, FindingCaseIntegrationTests.Input(
                    candidates.Pipeline.Analysis,
                    priorFindingCase?.Findings.Select(FindingCaseIntegrationTests.PriorFinding).ToArray(),
                    priorFindingCase?.Cases.Select(FindingCaseIntegrationTests.PriorCase).ToArray()),
                Attempt, Context.Binding, DateTimeOffset.UtcNow);
            FindingCases = findingCases.Analysis;
            Assignment = new AnalysisV1WorkAssignment(
                1, "assignment-" + RunId, candidateRequest.ExecutionInput!, semanticContext,
                Seal(Store, docs.Receipt.PayloadId, docs.Evidence.SchemaId, docs.Evidence.SchemaVersion.ToString()),
                Seal(Store, candidates.Receipt.PayloadId, candidates.Pipeline.Analysis.SchemaId, candidates.Pipeline.Analysis.SchemaVersion.ToString()),
                Seal(Store, findingCases.Receipt.StoredPayloadId, findingCases.Analysis.SchemaId, findingCases.Analysis.SchemaVersion.ToString()),
                new string('a', 40), DateTimeOffset.UtcNow.AddSeconds(-1), terminal,
                "analysis integration terminal outcome", 192L * 1024 * 1024,
                AnalysisV1WorkAssignment.AbsoluteMaximumOutputBytes, 100);
            ValidationReceiptPayloadId = StageValidationReceipt();
        }

        public CandidateStoreContext Context { get; }
        public AuthoritativeStore Store { get; }
        public string RunId { get; }
        public AttemptRecord Attempt { get; }
        public AnalysisV1WorkAssignment Assignment { get; }
        public FindingCaseContract FindingCases { get; }
        public CandidatePipelineRequest CandidateRequest { get; }
        public string ValidationReceiptPayloadId { get; }

        public AnalysisExecutionPhaseResult Publish(
            Action<string>? failureInjection = null,
            AttemptRecord? attempt = null,
            AnalysisV1WorkAssignment? assignment = null) =>
            AnalysisExecutionPhase.Execute(
                Store, assignment ?? Assignment, attempt ?? Attempt, Context.Binding,
                ValidationReceiptPayloadId, DateTimeOffset.UtcNow, failureInjection);

        public void Dispose()
        {
            staging.Dispose();
            if (ownsContext)
            {
                Context.Dispose();
            }
        }

        public string CreateValidationReceiptFor(AttemptRecord attempt)
        {
            byte[] bytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"disposition\":\"validated-for-coordinator-publication-only\"}");
            const string name = "analysis-v1-validation-receipt.json";
            using AttemptStagingAuthority currentStaging = Store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
            using (FileStream file = WindowsHandleRelativeFile.CreateNew(currentStaging.Handle.DangerousGetHandle(), name))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            string artifact = "validation-" + attempt.AttemptId;
            string manifest = Convert.ToHexStringLower(ManagedWorkerManifest.ComputeDigest(artifact, name, sha, bytes.Length));
            return Store.AdmitStagedPayload(
                attempt, name, sha, bytes.Length, manifest, 1024 * 1024,
                DateTimeOffset.UtcNow, stagedArtifactId: artifact).PayloadId;
        }

        private string StageValidationReceipt()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"disposition\":\"validated-for-coordinator-publication-only\"}");
            const string name = "analysis-v1-validation-receipt.json";
            using (FileStream file = WindowsHandleRelativeFile.CreateNew(staging.Handle.DangerousGetHandle(), name))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            string artifact = "validation-" + RunId;
            string manifest = Convert.ToHexStringLower(ManagedWorkerManifest.ComputeDigest(artifact, name, sha, bytes.Length));
            return Store.AdmitStagedPayload(
                Attempt, name, sha, bytes.Length, manifest, 1024 * 1024,
                DateTimeOffset.UtcNow, stagedArtifactId: artifact).PayloadId;
        }

        private DocumentationImportRequestContract DocumentationRequest(string runId)
        {
            const string text = "Purpose: Adds an inert capability.\nRequirement: Component remains local.\n";
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            DocumentationClaimInputContract purpose = new(
                new OpaqueId("operations-purpose"), 0, 34, "Purpose: Adds an inert capability.",
                ClaimKind.DeclaredPurpose, [], EvidenceAuthority.AuthoritativeExternal,
                ClaimApplicabilityState.Applicable, ClassificationRole.Declared, []);
            long requirementStart = Encoding.UTF8.GetByteCount("Purpose: Adds an inert capability.\n");
            DocumentationClaimInputContract requirement = new(
                new OpaqueId("operations-requirement"), requirementStart, bytes.Length - 1,
                "Requirement: Component remains local.", ClaimKind.Requirement, [],
                EvidenceAuthority.AuthoritativeExternal, ClaimApplicabilityState.Applicable,
                ClassificationRole.Declared, []);
            DocumentationApplicationInputContract application = new(
                purpose.ClaimKey, new OpaqueId(runId), new OpaqueId(Context.Binding.AnalysisContextId),
                new OpaqueId("entity-operations"), "installed-entity", new OpaqueId("closure-operations"),
                ClaimApplicabilityState.Applicable, [requirement.ClaimKey],
                new("purpose.add-expand", [requirement.ClaimKey], new OpaqueId("documentation-importer"), "exact declared purpose"));
            DocumentationClaimImportManifestContract manifest = new(
                ContractConstants.DocumentationClaimImportSchemaId, new ContractVersion(1, 0, 0),
                new OpaqueId("source-operations"), DocumentationSourceKind.Fixture, "fixture-operations-r1",
                DocumentationSourceAvailability.Present,
                new Sha256Fingerprint(Convert.ToHexStringLower(SHA256.HashData(bytes))), bytes.Length,
                new OpaqueId(Context.Binding.InstallationSnapshotId), [purpose, requirement], [application]);
            return new DocumentationImportRequestContract(
                new OpaqueId(runId), new OpaqueId(runId), DocumentationImportMode.CleanImport,
                new OpaqueId("closure-operations"), new OpaqueId("extractor-operations"),
                new UtcTimestamp(DateTimeOffset.UtcNow), manifest, bytes, null,
                [new DocumentationApplicationTargetContract(
                    application.ConsumingRunId, new OpaqueId(Context.Binding.InstallationSnapshotId),
                    application.AnalysisContextId, new OpaqueId(Context.Binding.ResolvedInputManifestId),
                    application.SubjectId, application.SubjectType, application.DependencyClosureId)]);
        }

        private static RetainedAnalysisPayloadSeal Seal(
            AuthoritativeStore store, string payloadId, string schemaId, string schemaVersion)
        {
            RetainedPayloadRecord retained = store.GetRetainedPayload(payloadId);
            return new RetainedAnalysisPayloadSeal(
                retained.PayloadId, schemaId, schemaVersion, retained.Sha256, retained.ByteLength);
        }
    }
}

#pragma warning restore CA1416
