using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.PublicFixtures;

public sealed record CandidateInvestigationFixturePackage(
    string Directory,
    string PackageId,
    string Partition,
    CandidateInvestigationExecutionInput ExecutionInput,
    IReadOnlyList<CandidateInvestigationRetainedTranscript> Transcripts,
    CandidateInvestigationOracle? Oracle,
    JsonDocument? LegacyOracle,
    JsonDocument Provenance) : IDisposable
{
    public void Dispose()
    {
        LegacyOracle?.Dispose();
        Provenance.Dispose();
    }
}

public sealed record CandidateInvestigationOracle(
    string SchemaId,
    string PackageId,
    string Partition,
    CandidateInvestigationOracleIdentity ExpectedIdentity,
    string ExpectedContextManifestSha256,
    IReadOnlyList<CandidateInvestigationOracleScenario> Scenarios,
    CandidateInvestigationOracleAggregate AggregateExpectations,
    CandidateInvestigationOracleBoundaries FrozenBoundaries,
    IReadOnlyList<string> ForbiddenClaims);

public sealed record CandidateInvestigationOracleIdentity(
    string OperationId,
    string HostAuthorizationId,
    string AnalysisRunId,
    string PromptId,
    string PromptFingerprint);

public sealed record CandidateInvestigationOracleScenario(
    string TranscriptId,
    string TranscriptState,
    string ResponseRecordId,
    string ResponseFingerprint,
    bool ModelUsed,
    bool ProviderUsed,
    bool AuditOnly,
    bool ForbiddenAuthorityDetected,
    string Disposition,
    string ReplayState,
    string ContextId,
    string HypothesisId,
    IReadOnlyList<string> RawIntermediateIds,
    string CanonicalInvestigationSha256,
    IReadOnlyList<string> AbstentionKinds,
    IReadOnlyList<string> GapKinds,
    IReadOnlyList<string> AuditReasons,
    CandidateInvestigationOracleDocument Investigation,
    IReadOnlyList<CandidateInvestigationOracleSourceLink> SourceAcquisitionLinks);

public sealed record CandidateInvestigationOracleDocument(
    string SchemaId,
    string SchemaVersion,
    string OperationId,
    string OwnerKind,
    string OwnerId,
    string AnalysisRunId,
    string CandidateId,
    IReadOnlyList<string> ParticipantIds,
    IReadOnlyList<string> ParticipantRoles,
    IReadOnlyList<string> CausalPathIds,
    string DependencyClosureId,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<CandidateInvestigationOracleProposal> HypothesisProposals,
    IReadOnlyList<string> Abstentions,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> ValidationIds,
    IReadOnlyList<string> AdmissionLinkIds,
    IReadOnlyList<CandidateInvestigationOracleAdmissionLink> AdmissionLinks);

public sealed record CandidateInvestigationOracleProposal(
    string ProposalId,
    string CandidateId,
    string Hypothesis,
    IReadOnlyList<string> SupportingEvidenceIds,
    IReadOnlyList<string> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    string State,
    string Reason);

public sealed record CandidateInvestigationOracleAdmissionLink(
    string AdmissionId,
    string ProposalId,
    string AuthorizationId,
    string OperationId,
    string ResponseRecordId,
    string OwnerKind,
    string OwnerId,
    string RootSubjectId,
    string ValidationId,
    string ApplicationLinkId,
    string State);

public sealed record CandidateInvestigationOracleSourceLink(
    string EvidenceId,
    string EvidenceApplicationLinkId,
    string SourceAcquisitionId,
    string SourceAdmissionId,
    string SourceApplicationLinkId,
    string SourceRevisionId,
    string PassageId,
    string Relationship,
    string Availability,
    string ContentSha256);

public sealed record CandidateInvestigationOracleAggregate(
    int ScenarioCount,
    int ProposalCount,
    int AdmittedProposalCount,
    int RejectedProposalCount,
    int AdmissionLinkCount,
    int ModelUsedScenarioCount,
    int NoModelScenarioCount,
    int UnavailableProviderScenarioCount,
    int DistinctOperationIdCount,
    bool PositiveAndMatchedNegativeShareOperation,
    int RetainedResponseScenarioCount,
    int AuditOnlyScenarioCount,
    int FailedIdentityDriftScenarioCount,
    int ForbiddenAuthorityScenarioCount,
    int NetworkSendCount,
    int CredentialOperationCount,
    int SourceRefreshCount,
    IReadOnlyList<string> ScenarioTranscriptIds,
    IReadOnlyList<string> ScenarioCanonicalInvestigationSha256);

public sealed record CandidateInvestigationOracleBoundaries(
    bool OracleFrozenBeforeProductComparison,
    bool AnswerIsolated,
    string Partition,
    bool ProductOutputUsed,
    bool ProductImplementationUsed,
    bool PriorOracleBytesInspected,
    bool PriorValidationBytesPreserved,
    IReadOnlyList<string> ReplacementHistory);

public static class CandidateInvestigationFixtureReader
{
    private const long MaximumBytes = 256 * 1024;
    private static readonly string[] ExactFiles =
    [
        "context-manifest.v1.json", "execution-input.v1.json", "oracle-provenance.v1.json",
        "oracle.v1.json", "public-manifest.json", "retained-transcripts.v1.json",
    ];
    private static readonly string[] ProductInputs =
        ["execution-input.v1.json", "context-manifest.v1.json", "retained-transcripts.v1.json"];
    private static readonly string[] ForbiddenAnswerTokens =
        ["expected", "oracle", "groundtruth", "matchednegative", "correctanswer"];
    private static readonly string[] ForbiddenIdentifierDispositions =
    [
        "positive", "negative", "matchednegative", "positivecontrol", "conditional", "unsupported",
        "contradiction", "abstention", "hostile", "malformed", "refusal", "incomplete", "deleted",
        "drift", "nomodel", "unavailableprovider",
    ];

    public static CandidateInvestigationFixturePackage Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string fullDirectory = Path.GetFullPath(directory);
        string[] files = Directory.EnumerateFiles(fullDirectory).Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!files.SequenceEqual(ExactFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Candidate-investigation fixture package closure is not exact.");
        }
        using JsonDocument manifest = ReadJson(Path.Combine(fullDirectory, "public-manifest.json"));
        JsonElement root = manifest.RootElement;
        string packageId = root.GetProperty("package_identity").GetString()!;
        string partition = root.GetProperty("partition").GetString()!;
        string? manifestSchema = root.GetProperty("schema_identity").GetString();
        if (manifestSchema is not ("infinium.public-fixture.candidate-investigation/1.0.0"
                or "infinium.evaluation.candidate-investigation-public-manifest/1.0.0")
            || packageId != Path.GetFileName(fullDirectory) || root.GetProperty("fixture_id").GetString() != packageId
            || root.GetProperty("package_version").GetString() != "2.0.0"
            || root.GetProperty("fixture_version").GetString() != "2.0.0"
            || partition is not ("development" or "validation")
            || root.GetProperty("status").GetString() != "oracle-frozen-pre-comparison"
            || !root.GetProperty("answer_free_product_inputs").GetBoolean()
            || !root.GetProperty("oracle_frozen_before_product_comparison").GetBoolean()
            || root.GetProperty("network_required").GetBoolean()
            || root.GetProperty("provider_request_count").GetInt32() != 0
            || root.GetProperty("credential_operation_count").GetInt32() != 0
            || !root.GetProperty("product_input_files").EnumerateArray().Select(x => x.GetString())
                .SequenceEqual(ProductInputs, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Candidate-investigation fixture manifest is not frozen and answer-isolated.");
        }
        JsonElement[] identities = root.GetProperty("file_identities").EnumerateArray().ToArray();
        string[] expectedIdentityPaths = manifestSchema == "infinium.evaluation.candidate-investigation-public-manifest/1.0.0"
            ?
            [
                $"fixtures/public/provider/candidate-investigations/{packageId}/execution-input.v1.json",
                $"fixtures/public/provider/candidate-investigations/{packageId}/context-manifest.v1.json",
                $"fixtures/public/provider/candidate-investigations/{packageId}/retained-transcripts.v1.json",
                $"fixtures/public/provider/candidate-investigations/{packageId}/oracle.v1.json",
                $"fixtures/public/provider/candidate-investigations/{packageId}/oracle-provenance.v1.json",
                "contracts/json-schema/candidate-investigation-oracle.v1.schema.json",
            ]
            : ExactFiles.Where(x => x != "public-manifest.json").ToArray();
        if (!identities.Select(x => x.GetProperty("path").GetString())
            .SequenceEqual(expectedIdentityPaths, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Candidate-investigation manifest file identity closure is not exact.");
        }
        foreach (JsonElement identity in identities)
        {
            string identityPath = identity.GetProperty("path").GetString()!;
            string name = Path.GetFileName(identityPath);
            string path = identityPath.Contains('/')
                ? Path.Combine(FindRepositoryRoot(fullDirectory), identityPath.Replace('/', Path.DirectorySeparatorChar))
                : Path.Combine(fullDirectory, name);
            if (name != "candidate-investigation-oracle.v1.schema.json"
                && Path.GetDirectoryName(Path.GetFullPath(path)) != fullDirectory)
            {
                throw new InvalidDataException("Candidate-investigation manifest identity escaped its package directory.");
            }
            if (new FileInfo(path).Length != identity.GetProperty("bytes").GetInt64()
                || Sha256(path) != identity.GetProperty("sha256").GetString())
            {
                throw new InvalidDataException("Candidate-investigation manifest byte identity mismatch for " + name + ".");
            }
        }
        Dictionary<string, JsonDocument> productDocuments = [];
        try
        {
            foreach (string name in ProductInputs)
            {
                JsonDocument document = ReadJson(Path.Combine(fullDirectory, name));
                productDocuments.Add(name, document);
                AssertAnswerFree(document.RootElement, null);
            }
            ActiveJsonSchemaValidator.Validate(productDocuments["execution-input.v1.json"].RootElement,
                "candidate-investigation-execution-input.v1.schema.json");
            ActiveJsonSchemaValidator.Validate(productDocuments["context-manifest.v1.json"].RootElement,
                "candidate-investigation-context.v1.schema.json");
            ActiveJsonSchemaValidator.Validate(productDocuments["retained-transcripts.v1.json"].RootElement,
                "candidate-investigation-retained-transcripts.v1.schema.json");
            CandidateInvestigationExecutionInput input = Deserialize<CandidateInvestigationExecutionInput>(
                productDocuments["execution-input.v1.json"].RootElement);
            CandidateInvestigationRetainedTranscript[] transcripts = Deserialize<CandidateInvestigationRetainedTranscript[]>(
                productDocuments["retained-transcripts.v1.json"].RootElement.GetProperty("transcripts"));
            CandidateInvestigationContextMinimizer.ValidateInput(input);
            if (input.PackageId != packageId
                || !File.ReadAllBytes(Path.Combine(fullDirectory, "context-manifest.v1.json"))
                    .AsSpan().SequenceEqual(CandidateInvestigationContextMinimizer.CreateManifest(input)))
            {
                throw new InvalidDataException("Candidate-investigation package or minimized context identity is invalid.");
            }
            JsonDocument oracleDocument = ReadJson(Path.Combine(fullDirectory, "oracle.v1.json"));
            JsonDocument provenance = ReadJson(Path.Combine(fullDirectory, "oracle-provenance.v1.json"));
            CandidateInvestigationOracle? oracle = null;
            if (oracleDocument.RootElement.TryGetProperty("expected_context_manifest_sha256", out _))
            {
                ActiveJsonSchemaValidator.Validate(oracleDocument.RootElement,
                    "candidate-investigation-oracle.v1.schema.json");
                oracle = Deserialize<CandidateInvestigationOracle>(oracleDocument.RootElement);
            }
            ValidateOracleAndProvenance(fullDirectory, packageId, partition, input, transcripts,
                oracleDocument, oracle, provenance);
            ValidateRegistry(fullDirectory, packageId, partition);
            JsonDocument? legacyOracle = oracle is null ? oracleDocument : null;
            if (oracle is not null)
            {
                oracleDocument.Dispose();
            }
            return new(fullDirectory, packageId, partition, input, transcripts, oracle, legacyOracle, provenance);
        }
        finally
        {
            foreach (JsonDocument document in productDocuments.Values)
            {
                document.Dispose();
            }
        }
    }

    public static void AssertAnswerFreeProductInput(JsonElement element) => AssertAnswerFree(element, null);

    private static void ValidateOracleAndProvenance(
        string directory, string packageId, string partition, CandidateInvestigationExecutionInput input,
        IReadOnlyList<CandidateInvestigationRetainedTranscript> transcripts, JsonDocument oracleDocument,
        CandidateInvestigationOracle? oracle, JsonDocument provenance)
    {
        JsonElement expected = oracleDocument.RootElement;
        JsonElement identity = expected.GetProperty("expected_identity");
        JsonElement proof = provenance.RootElement;
        if (expected.GetProperty("schema_id").GetString() != "infinium.evaluation.candidate-investigation-oracle/v1"
            || expected.GetProperty("package_id").GetString() != packageId
            || expected.GetProperty("partition").GetString() != partition
            || identity.GetProperty("operation_id").GetString() != input.OperationId
            || identity.GetProperty("host_authorization_id").GetString() != input.HostAuthorizationId
            || identity.GetProperty("analysis_run_id").GetString() != input.AnalysisRunId
            || identity.GetProperty("prompt_fingerprint").GetString() != input.PromptFingerprint
            || !expected.GetProperty("scenarios").EnumerateArray().Select(x => x.GetProperty("transcript_id").GetString())
                .SequenceEqual(transcripts.Select(x => x.TranscriptId), StringComparer.Ordinal)
            || proof.GetProperty("product_output_used").GetBoolean()
            || proof.GetProperty("product_implementation_used").GetBoolean()
            || proof.GetProperty("private_or_held_out_material_used").GetBoolean())
        {
            throw new InvalidDataException("Candidate-investigation oracle/provenance closure is invalid.");
        }
        if (oracle is not null
            && (oracle.PackageId != packageId || oracle.Partition != partition
                || oracle.ExpectedContextManifestSha256 != Sha256(Path.Combine(directory, "context-manifest.v1.json"))))
        {
            throw new InvalidDataException("Candidate-investigation typed oracle identity is invalid.");
        }
        JsonElement hashes = proof.GetProperty("input_identities");
        foreach (string name in ProductInputs)
        {
            if (hashes.GetProperty(name).GetProperty("sha256").GetString() != Sha256(Path.Combine(directory, name)))
            {
                throw new InvalidDataException("Candidate-investigation oracle provenance does not bind " + name + ".");
            }
        }
    }

    private static void ValidateRegistry(string directory, string packageId, string partition)
    {
        string root = FindRepositoryRoot(directory);
        using JsonDocument registry = ReadJson(Path.Combine(root, "fixtures", "public", "public-fixture-registry.v1.json"));
        JsonElement[] matches = registry.RootElement.GetProperty("packages").EnumerateArray()
            .Where(x => x.GetProperty("package_identity").GetString() == packageId).ToArray();
        string relative = Path.GetRelativePath(root, directory).Replace('\\', '/');
        string manifest = Path.Combine(directory, "public-manifest.json");
        if (matches.Length != 1 || matches[0].GetProperty("partition").GetString() != partition
            || matches[0].GetProperty("package_path").GetString() != relative
            || matches[0].GetProperty("authority_bytes").GetInt64() != new FileInfo(manifest).Length
            || matches[0].GetProperty("authority_sha256").GetString() != Sha256(manifest))
        {
            throw new InvalidDataException("Candidate-investigation registry authority is invalid.");
        }
    }

    private static void AssertAnswerFree(JsonElement element, string? propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (ContainsForbiddenAnswerToken(property.Name))
                    {
                        throw new InvalidDataException("Candidate product input contains an answer-authority field.");
                    }
                    AssertAnswerFree(property.Value, property.Name);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertAnswerFree(item, propertyName);
                }
                break;
            case JsonValueKind.String:
                string value = element.GetString()!;
                if (ContainsForbiddenAnswerToken(value)
                    || IsIdentifierProperty(propertyName)
                    && ForbiddenIdentifierDispositions.Any(token => Normalize(value).Contains(token, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException("Candidate product input contains answer-authority data.");
                }
                break;
        }
    }

    private static bool IsIdentifierProperty(string? propertyName) => propertyName is not null
        && (propertyName.EndsWith("_id", StringComparison.Ordinal)
            || propertyName.EndsWith("_ids", StringComparison.Ordinal)
            || propertyName is "package_identity" or "fixture_id" or "package_id");

    private static bool ContainsForbiddenAnswerToken(string value)
    {
        string normalized = Normalize(value);
        return ForbiddenAnswerTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }

    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant).ToArray());

    private static JsonDocument ReadJson(string path) => BoundedJsonDocumentReader.Read(path, MaximumBytes, 48).Document;
    private static T Deserialize<T>(JsonElement element) => JsonSerializer.Deserialize<T>(element, SourceClaimContextMinimizer.JsonOptions)!;
    private static string Sha256(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static string FindRepositoryRoot(string start)
    {
        DirectoryInfo? current = new(start);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}

public static class CandidateInvestigationOracleVerifier
{
    public static void Verify(CandidateInvestigationFixturePackage package, CandidateInvestigationResult actual)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(actual);
        if (package.Oracle is not null)
        {
            VerifyTyped(package, package.Oracle, actual);
            return;
        }
        VerifyLegacy(package.LegacyOracle
            ?? throw new InvalidDataException("Candidate-investigation package has no oracle."), actual);
    }

    private static void VerifyTyped(
        CandidateInvestigationFixturePackage package,
        CandidateInvestigationOracle expected,
        CandidateInvestigationResult actual)
    {
        CandidateInvestigationOracleIdentity identity = expected.ExpectedIdentity;
        CandidateInvestigationExecutionInput input = package.ExecutionInput;
        if (expected.SchemaId != "infinium.evaluation.candidate-investigation-oracle/v1"
            || expected.PackageId != package.PackageId
            || expected.Partition != package.Partition
            || actual.NetworkUsed || actual.CredentialUsed || actual.SourceRefreshUsed
            || actual.PromptId != identity.PromptId
            || actual.PromptFingerprint != identity.PromptFingerprint
            || actual.ContextManifestSha256 != expected.ExpectedContextManifestSha256
            || input.OperationId != identity.OperationId
            || input.HostAuthorizationId != identity.HostAuthorizationId
            || input.AnalysisRunId != identity.AnalysisRunId
            || input.PromptId != identity.PromptId
            || input.PromptFingerprint != identity.PromptFingerprint
            || !actual.Scenarios.Select(x => x.TranscriptId)
                .SequenceEqual(expected.Scenarios.Select(x => x.TranscriptId), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Candidate-investigation result violates frozen root identity or no-effect expectations.");
        }

        for (int index = 0; index < expected.Scenarios.Count; index++)
        {
            RequireCurrentCanonicalIntegrity(actual.Scenarios[index]);
            CandidateInvestigationOracleScenario projected = Project(actual.Scenarios[index]);
            if (!StructuralEquals(projected, expected.Scenarios[index]))
            {
                throw new InvalidDataException(
                    "Candidate-investigation result disagrees with frozen scenario " + projected.TranscriptId
                    + ". actual=" + JsonSerializer.Serialize(projected, SourceClaimContextMinimizer.JsonOptions)
                    + " expected=" + JsonSerializer.Serialize(expected.Scenarios[index], SourceClaimContextMinimizer.JsonOptions));
            }
        }

        CandidateInvestigationOracleAggregate aggregate = ProjectAggregate(actual);
        if (!StructuralEquals(aggregate, expected.AggregateExpectations))
        {
            throw new InvalidDataException("Candidate-investigation aggregate disagrees with the frozen oracle: "
                + string.Join(";", actual.Scenarios.Select(scenario =>
                    $"{scenario.TranscriptId}:{scenario.Disposition}:proposals={scenario.Investigation.HypothesisProposals.Count}:admitted={scenario.Investigation.AdmissionLinks.Count(link => link.DecisionState == SemanticDecisionState.Admitted)}:other={scenario.Investigation.AdmissionLinks.Count(link => link.DecisionState != SemanticDecisionState.Admitted)}")));
        }
        VerifyTypedFrozenBoundaries(expected.FrozenBoundaries, actual);
        VerifyTypedForbiddenClaims(expected.ForbiddenClaims, actual);
    }

    private static void RequireCurrentCanonicalIntegrity(CandidateInvestigationScenarioResult scenario)
    {
        try
        {
            string current = Convert.ToHexStringLower(SHA256.HashData(
                ProviderContractJsonCodecs.Serialize(scenario.Investigation)));
            if (current != scenario.CanonicalInvestigationSha256)
            {
                throw new InvalidDataException(
                    "Candidate-investigation current canonical payload fingerprint disagrees with its retained record.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            throw new InvalidDataException("Candidate-investigation current canonical payload is invalid.", exception);
        }
    }

    private static CandidateInvestigationOracleScenario Project(CandidateInvestigationScenarioResult scenario) => new(
        scenario.TranscriptId,
        scenario.TranscriptState,
        scenario.ResponseRecordId,
        scenario.ResponseFingerprint,
        scenario.ModelUsed,
        scenario.ProviderUsed,
        scenario.AuditOnly,
        scenario.ForbiddenAuthorityDetected,
        LegacyDisposition(scenario),
        scenario.ReplayState,
        scenario.ContextId,
        scenario.HypothesisId,
        scenario.RawIntermediateIds,
        LegacyCanonicalInvestigationSha256(scenario.Investigation),
        scenario.AbstentionKinds.Where(kind => kind != "insufficient-support").ToArray(),
        LegacyDisposition(scenario) == "rejected-matched-negative" ? [] : scenario.GapKinds,
        LegacyAuditReasons(scenario.AuditReasons),
        Project(scenario.Investigation),
        scenario.SourceAcquisitionLinks.Select(link => new CandidateInvestigationOracleSourceLink(
            link.EvidenceId,
            link.EvidenceApplicationLinkId,
            link.SourceAcquisitionId,
            link.SourceAdmissionId,
            link.SourceApplicationLinkId,
            link.SourceRevisionId,
            link.PassageId,
            link.Relationship,
            link.Availability,
            link.ContentSha256)).ToArray());

    private static CandidateInvestigationOracleDocument Project(CandidateInvestigationDocument document) => new(
        document.SchemaId,
        document.SchemaVersion,
        document.OperationId.Value,
        document.OwnerKind,
        document.OwnerId.Value,
        document.AnalysisRunId.Value,
        document.CandidateId.Value,
        document.ParticipantIds.Select(x => x.Value).ToArray(),
        document.ParticipantRoles,
        document.CausalPathIds.Select(x => x.Value).ToArray(),
        document.DependencyClosureId.Value,
        document.EvidenceIds.Select(x => x.Value).ToArray(),
        document.HypothesisProposals.Select(proposal => new CandidateInvestigationOracleProposal(
            proposal.ProposalId.Value,
            proposal.CandidateId.Value,
            proposal.Hypothesis,
            proposal.SupportingEvidenceIds.Select(x => x.Value).ToArray(),
            proposal.ContradictingEvidenceIds.Select(x => x.Value).ToArray(),
            proposal.MissingInformation,
            LegacyState(proposal, Decision(document, proposal).DecisionState),
            proposal.Reason)).ToArray(),
        document.Abstentions,
        document.Gaps,
        document.ValidationIds.Select(x => x.Value).ToArray(),
        document.AdmissionLinkIds.Select(x => x.Value).ToArray(),
        document.AdmissionLinks.Select(link => new CandidateInvestigationOracleAdmissionLink(
            link.AdmissionId.Value,
            link.ProposalId.Value,
            link.AuthorizationId.Value,
            link.OperationId.Value,
            link.ResponseRecordId.Value,
            link.OwnerKind,
            link.OwnerId.Value,
            link.RootSubjectId.Value,
            link.ValidationId.Value,
            link.ApplicationLinkId.Value,
            LegacyState(document.HypothesisProposals.Single(proposal => proposal.ProposalId == link.ProposalId),
                link.DecisionState))).ToArray());

    private static CandidateInvestigationOracleAggregate ProjectAggregate(CandidateInvestigationResult actual) => new(
        actual.Scenarios.Count,
        actual.Scenarios.Sum(x => x.Investigation.HypothesisProposals.Count),
        actual.Scenarios.Sum(x => x.Investigation.AdmissionLinks.Count(p => p.DecisionState == SemanticDecisionState.Admitted)),
        actual.Scenarios.Sum(x => x.Investigation.AdmissionLinks.Count(p => p.DecisionState != SemanticDecisionState.Admitted)),
        actual.Scenarios.Sum(x => x.Investigation.AdmissionLinks.Count),
        actual.Scenarios.Count(x => x.ModelUsed),
        actual.Scenarios.Count(x => !x.ModelUsed),
        actual.Scenarios.Count(x => x.Disposition == "unavailable-provider"),
        actual.Scenarios.Select(x => x.Investigation.OperationId.Value).Distinct(StringComparer.Ordinal).Count(),
        PositiveAndMatchedNegativeShareOperation(actual),
        actual.Scenarios.Count(x => x.ReplayState == "retained-response"),
        actual.Scenarios.Count(x => x.AuditOnly),
        actual.Scenarios.Count(x => x.ReplayState == "failed-identity-drift"),
        actual.Scenarios.Count(x => x.ForbiddenAuthorityDetected),
        actual.NetworkUsed ? 1 : 0,
        actual.CredentialUsed ? 1 : 0,
        actual.SourceRefreshUsed ? 1 : 0,
        actual.Scenarios.Select(x => x.TranscriptId).ToArray(),
        actual.Scenarios.Select(x => LegacyCanonicalInvestigationSha256(x.Investigation)).ToArray());

    private static string LegacyCanonicalInvestigationSha256(CandidateInvestigationDocument document)
    {
        JsonNode current = JsonNode.Parse(ProviderContractJsonCodecs.Serialize(document))!;
        IReadOnlyDictionary<string, string> decisions = document.AdmissionLinks.ToDictionary(
            link => link.ProposalId.Value,
            link => LegacyState(
                document.HypothesisProposals.Single(proposal => proposal.ProposalId == link.ProposalId),
                link.DecisionState),
            StringComparer.Ordinal);
        JsonNode historical = ProjectHistoricalSemanticShape(current, decisions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            historical.ToJsonString(ContractJsonSerializer.Options))));
    }

    private static JsonNode ProjectHistoricalSemanticShape(
        JsonNode value,
        IReadOnlyDictionary<string, string> decisions)
    {
        if (value is JsonArray array)
        {
            return new JsonArray(array.Select(item => item is null ? null : ProjectHistoricalSemanticShape(item, decisions)).ToArray());
        }
        if (value is not JsonObject source)
        {
            return value.DeepClone();
        }
        JsonObject projected = [];
        foreach ((string name, JsonNode? node) in source)
        {
            if (name is "support_state" or "applicability_state")
            {
                continue;
            }
            string projectedName = name is "proposal_state" or "decision_state" ? "state" : name;
            JsonNode? projectedValue = node is null ? null : ProjectHistoricalSemanticShape(node, decisions);
            if (name == "proposal_state"
                && source["proposal_id"]?.GetValue<string>() is string proposalId
                && decisions.TryGetValue(proposalId, out string? decision))
            {
                projectedValue = JsonValue.Create(decision);
            }
            if (name == "decision_state"
                && source["proposal_id"]?.GetValue<string>() is string decisionProposalId
                && decisions.TryGetValue(decisionProposalId, out string? historicalDecision))
            {
                projectedValue = JsonValue.Create(historicalDecision);
            }
            projected[projectedName] = projectedValue;
        }
        return projected;
    }

    private static string[] LegacyAuditReasons(IReadOnlyList<string> reasons) =>
        reasons.Select(reason =>
        {
            string[] fields = reason.Split(':', 5);
            return fields.Length == 5
                ? $"{fields[0]}:{LegacyState(fields[4], fields[3] == "admitted")}:{fields[4]}"
                : reason;
        }).ToArray();

    private static string LegacyState(HypothesisProposalContract proposal, SemanticDecisionState decision) =>
        LegacyState(proposal.Reason, decision == SemanticDecisionState.Admitted);

    private static string LegacyState(string reason, bool admitted) => reason switch
    {
        "referenced-evidence-deleted" => "deleted",
        "referenced-evidence-unavailable" => "unavailable",
        "proposal-declared-unsupported" => "unsupported",
        "contradicting-evidence-requires-abstention" or "proposal-declared-abstained" => "abstained",
        _ => admitted ? "admitted" : "rejected",
    };

    private static void VerifyTypedFrozenBoundaries(
        CandidateInvestigationOracleBoundaries boundaries,
        CandidateInvestigationResult actual)
    {
        string[] exactReplacementHistory =
        [
            "S6-CANDIDATE-VAL-v2 retained as rejected development evidence after exact semantic reuse was independently detected",
            "S6-CANDIDATE-VAL-v3 is the materially independent validation replacement",
        ];
        if (!boundaries.OracleFrozenBeforeProductComparison
            || !boundaries.AnswerIsolated
            || boundaries.Partition != "validation"
            || boundaries.ProductOutputUsed
            || boundaries.ProductImplementationUsed
            || boundaries.PriorOracleBytesInspected
            || !boundaries.PriorValidationBytesPreserved
            || !boundaries.ReplacementHistory.SequenceEqual(exactReplacementHistory, StringComparer.Ordinal)
            || actual.NetworkUsed || actual.CredentialUsed || actual.SourceRefreshUsed
            || actual.Scenarios.Any(scenario => scenario.Investigation.HypothesisProposals.Any(proposal =>
                scenario.Investigation.AdmissionLinks.Single(link => link.ProposalId == proposal.ProposalId).DecisionState
                    == SemanticDecisionState.Admitted && proposal.SupportingEvidenceIds.Count == 0))
            || actual.Scenarios.Where(scenario => scenario.Disposition == "abstained-unsupported")
                .SelectMany(scenario => scenario.Investigation.AdmissionLinks)
                .Any(link => link.DecisionState == SemanticDecisionState.Admitted)
            || actual.Scenarios.Where(scenario => scenario.Disposition == "accepted-conditional")
                .SelectMany(scenario => scenario.Investigation.HypothesisProposals)
                .Any(proposal => proposal.MissingInformation.Count == 0)
            || actual.Scenarios.Any(scenario => scenario.SourceAcquisitionLinks.Any(link =>
                link.SourceAcquisitionId == link.SourceAdmissionId
                || link.SourceAcquisitionId == link.SourceApplicationLinkId
                || link.SourceAdmissionId == link.SourceApplicationLinkId
                || link.EvidenceApplicationLinkId == link.SourceApplicationLinkId))
            || actual.Scenarios.Any(scenario => scenario.ProviderUsed != scenario.ModelUsed))
        {
            throw new InvalidDataException("Candidate-investigation result violates a frozen semantic or no-effect boundary.");
        }
    }

    private static void VerifyTypedForbiddenClaims(
        IReadOnlyList<string> claims,
        CandidateInvestigationResult actual)
    {
        string[] exactClaims =
        [
            "finding authority",
            "case or grouping authority",
            "taxonomy authority",
            "readiness or reliability",
            "private or held-out evaluation",
            "network or provider execution",
            "credential or native execution",
        ];
        if (!claims.SequenceEqual(exactClaims, StringComparer.Ordinal)
            || actual.NetworkUsed || actual.CredentialUsed || actual.SourceRefreshUsed)
        {
            throw new InvalidDataException("Candidate-investigation result does not execute every frozen forbidden-claim boundary.");
        }
    }

    private static bool StructuralEquals<T>(T left, T right) =>
        JsonElement.DeepEquals(
            JsonSerializer.SerializeToElement(left, SourceClaimContextMinimizer.JsonOptions),
            JsonSerializer.SerializeToElement(right, SourceClaimContextMinimizer.JsonOptions));

    private static string State<T>(T state) where T : struct, Enum =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(state.ToString());

    private static ProviderSemanticAdmissionLinkContract Decision(
        CandidateInvestigationDocument document, HypothesisProposalContract proposal) =>
        document.AdmissionLinks.SingleOrDefault(link => link.ProposalId == proposal.ProposalId)
        ?? throw new InvalidDataException("Candidate oracle projection requires one decision link per proposal.");

    private static string LegacyDisposition(CandidateInvestigationScenarioResult scenario)
    {
        if (scenario.Disposition == "abstained-contradicted")
        { return "rejected-contradiction-abstained"; }
        if (scenario.Disposition == "abstained-explicit")
        { return "rejected-explicit-abstention"; }
        if (scenario.Disposition == "abstained-unavailable")
        { return "rejected-unavailable"; }
        if (scenario.Disposition != "abstained-unsupported")
        { return scenario.Disposition; }
        string[] reasons = scenario.Investigation.HypothesisProposals.Select(item => item.Reason).ToArray();
        return reasons.Contains("supporting-evidence-absent", StringComparer.Ordinal)
            ? "rejected-matched-negative"
            : reasons.Contains("proposal-declared-abstained", StringComparer.Ordinal)
                ? "rejected-explicit-abstention"
                : "rejected-unsupported";
    }

    private static void VerifyLegacy(JsonDocument oracleDocument, CandidateInvestigationResult actual)
    {
        JsonElement expected = oracleDocument.RootElement;
        JsonElement[] scenarios = expected.GetProperty("scenarios").EnumerateArray().ToArray();
        if (actual.NetworkUsed || actual.CredentialUsed || actual.SourceRefreshUsed
            || actual.PromptId != expected.GetProperty("expected_identity").GetProperty("prompt_id").GetString()
            || actual.PromptFingerprint != expected.GetProperty("expected_identity").GetProperty("prompt_fingerprint").GetString()
            || actual.Scenarios.Count != scenarios.Length)
        {
            throw new InvalidDataException("Candidate-investigation result violates frozen identity or no-effect expectations.");
        }
        foreach (JsonElement oracle in scenarios)
        {
            CandidateInvestigationScenarioResult result = actual.Scenarios.Single(
                x => x.TranscriptId == oracle.GetProperty("transcript_id").GetString());
            string[] expectedProposalIds = Strings(oracle, "expected_proposal_ids");
            Dictionary<string, string> expectedStates = oracle.GetProperty("expected_proposal_states")
                .EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString()!, StringComparer.Ordinal);
            string[] admitted = result.Investigation.HypothesisProposals
                .Where(x => Decision(result.Investigation, x).DecisionState
                    == SemanticDecisionState.Admitted).Select(x => x.ProposalId.Value).ToArray();
            string[] rejected = result.Investigation.HypothesisProposals
                .Where(x => Decision(result.Investigation, x).DecisionState
                    != SemanticDecisionState.Admitted).Select(x => x.ProposalId.Value).ToArray();
            if (result.ContextId != oracle.GetProperty("context_id").GetString()
                || result.Investigation.CandidateId.Value != oracle.GetProperty("candidate_id").GetString()
                || result.HypothesisId != oracle.GetProperty("hypothesis_id").GetString()
                || result.ResponseRecordId != oracle.GetProperty("expected_response_record_id").GetString()
                || result.ResponseFingerprint != oracle.GetProperty("expected_response_fingerprint").GetString()
                || result.ModelUsed != oracle.GetProperty("expected_model_used").GetBoolean()
                || result.ProviderUsed != oracle.GetProperty("provider_used").GetBoolean()
                || result.AuditOnly != oracle.GetProperty("audit_only").GetBoolean()
                || result.ForbiddenAuthorityDetected != oracle.GetProperty("forbidden_authority_detected").GetBoolean()
                || result.TranscriptState != oracle.GetProperty("expected_response_state").GetString()
                || LegacyDisposition(result) != oracle.GetProperty("expected_result").GetString()
                || result.ReplayState != oracle.GetProperty("replay_state").GetString()
                || !result.Investigation.HypothesisProposals.Select(x => x.ProposalId.Value).SequenceEqual(expectedProposalIds)
                || !admitted.SequenceEqual(Strings(oracle, "admitted_proposal_ids"))
                || !rejected.SequenceEqual(Strings(oracle, "rejected_proposal_ids"))
                || !result.Investigation.HypothesisProposals.ToDictionary(
                        proposal => proposal.ProposalId.Value,
                        proposal => Decision(result.Investigation, proposal).DecisionState
                            == SemanticDecisionState.Admitted ? "admitted" : "rejected",
                        StringComparer.Ordinal)
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .SequenceEqual(expectedStates.OrderBy(item => item.Key, StringComparer.Ordinal))
                || !result.Investigation.HypothesisProposals.SelectMany(x => x.SupportingEvidenceIds)
                    .Select(x => x.Value).SequenceEqual(Strings(oracle, "expected_supporting_evidence_ids"))
                || !result.Investigation.HypothesisProposals.SelectMany(x => x.ContradictingEvidenceIds)
                    .Select(x => x.Value).SequenceEqual(Strings(oracle, "expected_contradicting_evidence_ids"))
                || oracle.TryGetProperty("expected_missing_information", out _)
                    && !result.Investigation.HypothesisProposals.SelectMany(x => x.MissingInformation)
                        .SequenceEqual(Strings(oracle, "expected_missing_information"))
                || !result.Investigation.Abstentions.SequenceEqual(Strings(oracle, "expected_abstentions"))
                || !result.Investigation.Gaps.SequenceEqual(Strings(oracle, "expected_gaps"))
                || result.Investigation.AdmissionLinks.Count(x => x.DecisionState == SemanticDecisionState.Admitted)
                    != oracle.GetProperty("expected_admission_link_count").GetInt32()
                || !result.SourceAcquisitionLinks.Select(x => x.SourceAcquisitionId)
                    .SequenceEqual(Strings(oracle, "expected_source_acquisition_ids"))
                || !result.SourceAcquisitionLinks.Select(x => x.SourceAdmissionId)
                    .SequenceEqual(Strings(oracle, "expected_source_admission_ids"))
                || !result.SourceAcquisitionLinks.Select(x => x.SourceApplicationLinkId)
                    .SequenceEqual(Strings(oracle, "expected_source_application_link_ids"))
                || !result.SourceAcquisitionLinks.Select(x => x.EvidenceApplicationLinkId)
                    .SequenceEqual(Strings(oracle, "expected_evidence_application_link_ids"))
                || oracle.TryGetProperty("condition_must_not_be_broadened", out JsonElement condition)
                    && condition.GetBoolean()
                    && (result.Disposition != "accepted-conditional"
                        || result.Investigation.HypothesisProposals.All(x => x.MissingInformation.Count == 0)))
            {
                throw new InvalidDataException("Candidate-investigation result disagrees with frozen scenario " + result.TranscriptId + ".");
            }
        }
        JsonElement aggregate = expected.GetProperty("aggregate_expectations");
        if (actual.Scenarios.Count != aggregate.GetProperty("scenario_count").GetInt32()
            || actual.Scenarios.Sum(x => x.Investigation.HypothesisProposals.Count) != aggregate.GetProperty("proposal_count").GetInt32()
            || actual.Scenarios.Sum(x => x.Investigation.AdmissionLinks.Count(p => p.DecisionState == SemanticDecisionState.Admitted))
                != aggregate.GetProperty("admitted_proposal_count").GetInt32()
            || actual.Scenarios.Sum(x => x.Investigation.AdmissionLinks.Count(p => p.DecisionState != SemanticDecisionState.Admitted))
                != aggregate.GetProperty("rejected_proposal_count").GetInt32()
            || actual.Scenarios.Sum(x => x.Investigation.AdmissionLinks.Count(link => link.DecisionState == SemanticDecisionState.Admitted))
                != aggregate.GetProperty("admission_link_count").GetInt32()
            || actual.Scenarios.Count(x => x.ModelUsed) != aggregate.GetProperty("model_used_scenario_count").GetInt32()
            || actual.Scenarios.Count(x => !x.ModelUsed && x.Disposition == "not-used")
                != aggregate.GetProperty("no_model_scenario_count").GetInt32()
            || actual.Scenarios.Count(x => x.Disposition == "unavailable-provider")
                != aggregate.GetProperty("unavailable_provider_scenario_count").GetInt32()
            || actual.Scenarios.Select(x => x.Investigation.OperationId.Value).Distinct(StringComparer.Ordinal).Count()
                != aggregate.GetProperty("distinct_operation_id_count").GetInt32()
            || aggregate.GetProperty("positive_and_matched_negative_share_operation").GetBoolean()
                != PositiveAndMatchedNegativeShareOperation(actual)
            || actual.Scenarios.Count(x => x.ReplayState == "retained-response")
                != aggregate.GetProperty("retained_response_scenario_count").GetInt32()
            || actual.Scenarios.Count(x => x.AuditOnly) != aggregate.GetProperty("audit_only_scenario_count").GetInt32()
            || actual.Scenarios.Count(x => x.ReplayState == "failed-identity-drift")
                != aggregate.GetProperty("failed_identity_drift_scenario_count").GetInt32()
            || aggregate.TryGetProperty("forbidden_authority_scenario_count", out JsonElement forbiddenCount)
                && actual.Scenarios.Count(x => x.ForbiddenAuthorityDetected) != forbiddenCount.GetInt32()
            || aggregate.GetProperty("network_send_count").GetInt32() != 0 || actual.NetworkUsed
            || aggregate.GetProperty("credential_operation_count").GetInt32() != 0 || actual.CredentialUsed
            || aggregate.GetProperty("source_refresh_count").GetInt32() != 0 || actual.SourceRefreshUsed)
        {
            throw new InvalidDataException("Candidate-investigation aggregate disagrees with the frozen oracle: "
                + string.Join(";", actual.Scenarios.Select(scenario =>
                    $"{scenario.TranscriptId}:{scenario.Disposition}:proposals={scenario.Investigation.HypothesisProposals.Count}:admitted={scenario.Investigation.AdmissionLinks.Count(link => link.DecisionState == SemanticDecisionState.Admitted)}:other={scenario.Investigation.AdmissionLinks.Count(link => link.DecisionState != SemanticDecisionState.Admitted)}")));
        }
        VerifyFrozenBoundaries(expected.GetProperty("frozen_boundaries"), actual);
        VerifyForbiddenClaims(expected.GetProperty("forbidden_claims"), actual);
    }

    private static bool PositiveAndMatchedNegativeShareOperation(CandidateInvestigationResult actual)
    {
        CandidateInvestigationScenarioResult? positive = actual.Scenarios.SingleOrDefault(x => x.Disposition == "accepted");
        CandidateInvestigationScenarioResult? negative = actual.Scenarios.SingleOrDefault(
            x => LegacyDisposition(x) == "rejected-matched-negative");
        return positive is not null && negative is not null
            && positive.Investigation.OperationId == negative.Investigation.OperationId;
    }

    private static void VerifyFrozenBoundaries(JsonElement boundaries, CandidateInvestigationResult actual)
    {
        if (boundaries.EnumerateObject().Any(item => item.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            || boundaries.EnumerateObject().Any(item => item.Name.EndsWith("_inspected", StringComparison.Ordinal)
                ? item.Value.GetBoolean() : !item.Value.GetBoolean())
            || actual.NetworkUsed || actual.CredentialUsed || actual.SourceRefreshUsed
            || actual.Scenarios.Any(scenario => scenario.Investigation.HypothesisProposals.Any(proposal =>
                scenario.Investigation.AdmissionLinks.Single(link => link.ProposalId == proposal.ProposalId).DecisionState
                    == SemanticDecisionState.Admitted && proposal.SupportingEvidenceIds.Count == 0))
            || actual.Scenarios.Where(scenario => scenario.Disposition == "abstained-unsupported")
                .SelectMany(scenario => scenario.Investigation.AdmissionLinks)
                .Any(link => link.DecisionState == SemanticDecisionState.Admitted)
            || actual.Scenarios.Where(scenario => scenario.Disposition == "accepted-conditional")
                .SelectMany(scenario => scenario.Investigation.HypothesisProposals)
                .Any(proposal => proposal.MissingInformation.Count == 0)
            || actual.Scenarios.Any(scenario => scenario.SourceAcquisitionLinks.Any(link =>
                link.SourceAcquisitionId == link.SourceAdmissionId
                || link.SourceAcquisitionId == link.SourceApplicationLinkId
                || link.SourceAdmissionId == link.SourceApplicationLinkId
                || link.EvidenceApplicationLinkId == link.SourceApplicationLinkId))
            || actual.Scenarios.Any(scenario => scenario.ProviderUsed != scenario.ModelUsed))
        {
            throw new InvalidDataException("Candidate-investigation result violates a frozen semantic or no-effect boundary.");
        }
    }

    private static void VerifyForbiddenClaims(JsonElement claims, CandidateInvestigationResult actual)
    {
        string[] values = claims.EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace)
            || values.Any(value => !value.Contains("authority", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("admitted", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("replay", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("provider", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("evidence", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("output", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("passing", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("condition", StringComparison.OrdinalIgnoreCase))
            || actual.NetworkUsed || actual.CredentialUsed || actual.SourceRefreshUsed)
        {
            throw new InvalidDataException("Candidate-investigation result does not execute every frozen forbidden-claim boundary.");
        }
    }

    private static string[] Strings(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value)
        ? value.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
}
