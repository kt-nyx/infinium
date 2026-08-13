using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;

namespace Infinium.PublicFixtures;

public sealed record CandidateInvestigationFixturePackage(
    string Directory,
    string PackageId,
    string Partition,
    CandidateInvestigationExecutionInput ExecutionInput,
    IReadOnlyList<CandidateInvestigationRetainedTranscript> Transcripts,
    JsonDocument Oracle,
    JsonDocument Provenance) : IDisposable
{
    public void Dispose()
    {
        Oracle.Dispose();
        Provenance.Dispose();
    }
}

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
        ["expected_", "oracle", "ground_truth", "matched_negative", "correct_answer"];

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
        if (root.GetProperty("schema_identity").GetString() != "infinium.public-fixture.candidate-investigation/1.0.0"
            || packageId != Path.GetFileName(fullDirectory) || root.GetProperty("fixture_id").GetString() != packageId
            || root.GetProperty("package_version").GetString() != "1.0.0"
            || root.GetProperty("fixture_version").GetString() != "1.0.0"
            || partition is not ("development" or "validation")
            || root.GetProperty("status").GetString() != "oracle-frozen-pre-comparison"
            || !root.GetProperty("answer_free_product_inputs").GetBoolean()
            || root.GetProperty("recursive_answer_isolation").GetString() != "PASS"
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
        string[] expectedIdentityPaths = ExactFiles.Where(x => x != "public-manifest.json").ToArray();
        if (!identities.Select(x => x.GetProperty("path").GetString()).SequenceEqual(expectedIdentityPaths, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Candidate-investigation manifest file identity closure is not exact.");
        }
        foreach (JsonElement identity in identities)
        {
            string name = identity.GetProperty("path").GetString()!;
            string path = Path.Combine(fullDirectory, name);
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
            JsonDocument oracle = ReadJson(Path.Combine(fullDirectory, "oracle.v1.json"));
            JsonDocument provenance = ReadJson(Path.Combine(fullDirectory, "oracle-provenance.v1.json"));
            ValidateOracleAndProvenance(fullDirectory, packageId, partition, input, transcripts, oracle, provenance);
            ValidateRegistry(fullDirectory, packageId, partition);
            return new(fullDirectory, packageId, partition, input, transcripts, oracle, provenance);
        }
        finally
        {
            foreach (JsonDocument document in productDocuments.Values)
            {
                document.Dispose();
            }
        }
    }

    private static void ValidateOracleAndProvenance(
        string directory, string packageId, string partition, CandidateInvestigationExecutionInput input,
        IReadOnlyList<CandidateInvestigationRetainedTranscript> transcripts, JsonDocument oracle, JsonDocument provenance)
    {
        JsonElement expected = oracle.RootElement;
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
                    if (ForbiddenAnswerTokens.Any(token => property.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
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
                if (ForbiddenAnswerTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException("Candidate product input contains answer-authority data.");
                }
                break;
        }
    }

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
        JsonElement expected = package.Oracle.RootElement;
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
            string[] admitted = result.Investigation.HypothesisProposals
                .Where(x => x.State == ProposalAdmissionState.Admitted).Select(x => x.ProposalId.Value).ToArray();
            string[] rejected = result.Investigation.HypothesisProposals
                .Where(x => x.State != ProposalAdmissionState.Admitted).Select(x => x.ProposalId.Value).ToArray();
            if (result.ContextId != oracle.GetProperty("context_id").GetString()
                || result.Investigation.CandidateId.Value != oracle.GetProperty("candidate_id").GetString()
                || result.HypothesisId != oracle.GetProperty("hypothesis_id").GetString()
                || result.TranscriptState != oracle.GetProperty("expected_response_state").GetString()
                || result.Disposition != oracle.GetProperty("expected_result").GetString()
                || result.ReplayState != oracle.GetProperty("replay_state").GetString()
                || !result.Investigation.HypothesisProposals.Select(x => x.ProposalId.Value).SequenceEqual(expectedProposalIds)
                || !admitted.SequenceEqual(Strings(oracle, "admitted_proposal_ids"))
                || !rejected.SequenceEqual(Strings(oracle, "rejected_proposal_ids"))
                || !result.Investigation.Abstentions.SequenceEqual(Strings(oracle, "expected_abstentions"))
                || !result.Investigation.Gaps.SequenceEqual(Strings(oracle, "expected_gaps"))
                || result.Investigation.AdmissionLinks.Count(x => x.State == ProposalAdmissionState.Admitted)
                    != oracle.GetProperty("expected_admission_link_count").GetInt32()
                || !result.SourceAcquisitionLinks.Select(x => x.SourceAcquisitionId)
                    .SequenceEqual(Strings(oracle, "expected_source_acquisition_ids"))
                || !result.SourceAcquisitionLinks.Select(x => x.SourceAdmissionId)
                    .SequenceEqual(Strings(oracle, "expected_source_admission_ids"))
                || !result.SourceAcquisitionLinks.Select(x => x.SourceApplicationLinkId)
                    .SequenceEqual(Strings(oracle, "expected_source_application_link_ids")))
            {
                throw new InvalidDataException("Candidate-investigation result disagrees with frozen scenario " + result.TranscriptId + ".");
            }
        }
        JsonElement aggregate = expected.GetProperty("aggregate_expectations");
        if (actual.Scenarios.Sum(x => x.Investigation.HypothesisProposals.Count) != aggregate.GetProperty("proposal_count").GetInt32()
            || actual.Scenarios.Sum(x => x.Investigation.HypothesisProposals.Count(p => p.State == ProposalAdmissionState.Admitted))
                != aggregate.GetProperty("admitted_proposal_count").GetInt32()
            || actual.Scenarios.Sum(x => x.Investigation.HypothesisProposals.Count(p => p.State != ProposalAdmissionState.Admitted))
                != aggregate.GetProperty("rejected_proposal_count").GetInt32())
        {
            throw new InvalidDataException("Candidate-investigation aggregate disagrees with the frozen oracle.");
        }
    }

    private static string[] Strings(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value)
        ? value.EnumerateArray().Select(x => x.GetString()!).ToArray() : [];
}
