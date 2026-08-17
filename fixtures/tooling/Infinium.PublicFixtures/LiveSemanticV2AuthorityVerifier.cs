using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

#pragma warning disable IDE0011 // Dense verifier guard clauses remain easier to audit without braces.
#pragma warning disable CA1859 // Interface type keeps the authority map immutable to consumers.

namespace Infinium.PublicFixtures;

public sealed record LiveSemanticV2AuthorityReceipt(
    int PackageCount,
    int PreservedRegistryEntryCount,
    int NewPackageCount,
    int SchemaCount,
    string RegistrySha256);

/// <summary>
/// Read-only verifier for the R1 public v2 authority. It validates frozen bytes and
/// semantic joins only; it never executes product code, reseals files, or performs effects.
/// </summary>
public static class LiveSemanticV2AuthorityVerifier
{
    private sealed record SchemaAuthority(string Id, string IdentityProperty, string Identity);
    private const string SourceRoot = "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2";
    private const string CandidateRoot = "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2";
    private const string LiveRoot = "fixtures/public/provider/live-campaign";
    private static readonly string[] PackageIds =
    [
        "S6-CLAIM-LIVE-VAL-v2", "LLM-CLAIM-LIVE-VAL-v2", "S6-CANDIDATE-LIVE-VAL-v2",
        "LLM-INVESTIGATE-LIVE-VAL-v2", "PROV-LIVE-COMPOSED-VAL-v2",
    ];
    private static readonly string[] InputPackageFiles =
    [
        "context-manifest.v2.json", "execution-input.v2.json", "oracle-provenance.v2.json",
        "oracle.v2.json", "partition-history.v2.json", "public-manifest.json",
    ];
    private static readonly string[] LivePackageFiles = ["oracle.v2.json", "public-manifest.json"];
    private static readonly string[] ForbiddenAnswerTokens =
        ["expected", "oracle", "correct", "admit", "reject", "positive", "negative", "ground_truth", "groundtruth"];
    private static readonly IReadOnlyDictionary<string, SchemaAuthority> Schemas = new Dictionary<string, SchemaAuthority>(StringComparer.Ordinal)
    {
        ["public-fixture-source-claim.v2.schema.json"] = new("https://schemas.infinium.dev/repository/public-fixture-source-claim.v2.schema.json", "schema_identity", "infinium.public-fixture.source-claim/2.0.0"),
        ["public-fixture-source-claim-oracle.v2.schema.json"] = new("https://schemas.infinium.dev/repository/public-fixture-source-claim-oracle.v2.schema.json", "schema_id", "infinium.evaluation.source-claim-oracle/2.0.0"),
        ["candidate-investigation-public-manifest.v2.schema.json"] = new("https://schemas.infinium.dev/repository/candidate-investigation-public-manifest.v2.schema.json", "schema_identity", "infinium.evaluation.candidate-investigation-public-manifest/2.0.0"),
        ["candidate-investigation-oracle.v2.schema.json"] = new("https://schemas.infinium.dev/repository/candidate-investigation-oracle.v2.schema.json", "schema_id", "infinium.evaluation.candidate-investigation-oracle/2.0.0"),
        ["live-source-claim-public-manifest.v2.schema.json"] = new("https://schemas.infinium.dev/repository/live-source-claim-public-manifest.v2.schema.json", "schema_identity", "infinium.public-fixture.live-source-claim/2.0.0"),
        ["live-source-claim-oracle.v2.schema.json"] = new("https://schemas.infinium.dev/repository/live-source-claim-oracle.v2.schema.json", "schema_id", "infinium.evaluation.live-source-claim-oracle/2.0.0"),
        ["live-candidate-investigation-public-manifest.v2.schema.json"] = new("https://schemas.infinium.dev/repository/live-candidate-investigation-public-manifest.v2.schema.json", "schema_identity", "infinium.public-fixture.live-candidate-investigation/2.0.0"),
        ["live-candidate-investigation-oracle.v2.schema.json"] = new("https://schemas.infinium.dev/repository/live-candidate-investigation-oracle.v2.schema.json", "schema_id", "infinium.evaluation.live-candidate-investigation-oracle/2.0.0"),
        ["live-composed-provenance-public-manifest.v2.schema.json"] = new("https://schemas.infinium.dev/repository/live-composed-provenance-public-manifest.v2.schema.json", "schema_identity", "infinium.public-fixture.live-composed-provenance/2.0.0"),
        ["live-composed-provenance-oracle.v2.schema.json"] = new("https://schemas.infinium.dev/repository/live-composed-provenance-oracle.v2.schema.json", "schema_id", "infinium.evaluation.live-composed-provenance-oracle/2.0.0"),
        ["source-claim-execution-input.v2.schema.json"] = new("https://schemas.infinium.dev/repository/source-claim-execution-input.v2.schema.json", "schema_id", "infinium.llm.source-claim-execution-input/v2"),
        ["source-claim-context.v2.schema.json"] = new("https://schemas.infinium.dev/repository/source-claim-context.v2.schema.json", "schema_id", "infinium.llm.source-claim-context/v2"),
        ["source-claim-oracle-provenance.v2.schema.json"] = new("https://schemas.infinium.dev/repository/source-claim-oracle-provenance.v2.schema.json", "schema_id", "infinium.evaluation.source-claim-oracle-provenance/v2"),
        ["candidate-investigation-execution-input.v2.schema.json"] = new("https://schemas.infinium.dev/repository/candidate-investigation-execution-input.v2.schema.json", "schema_id", "infinium.llm.candidate-investigation-execution-input/v2"),
        ["candidate-investigation-context.v2.schema.json"] = new("https://schemas.infinium.dev/repository/candidate-investigation-context.v2.schema.json", "schema_id", "infinium.llm.candidate-investigation-context/v2"),
        ["candidate-investigation-oracle-provenance.v2.schema.json"] = new("https://schemas.infinium.dev/repository/candidate-investigation-oracle-provenance.v2.schema.json", "schema_id", "infinium.evaluation.candidate-investigation-oracle-provenance/v2"),
        ["public-fixture-partition-history.v2.schema.json"] = new("https://schemas.infinium.dev/repository/public-fixture-partition-history.v2.schema.json", "schema_id", "infinium.evaluation.fixture-partition-history/2.0.0"),
        ["m1-slice6-campaign-stage-request.v2.schema.json"] = new("https://infinium.invalid/contracts/repository/m1-slice6-campaign-stage-request.v2.schema.json", "schema_identity", "infinium.repository.m1-slice6-campaign-stage-request/2.0.0"),
        ["m1-slice6-campaign-stage-evidence.v2.schema.json"] = new("https://schemas.infinium.dev/repository/m1-slice6-campaign-stage-evidence.v2.schema.json", "schema", "infinium.m1-s6.campaign-stage-evidence/v2"),
        ["m1-slice6-campaign-composed-evidence.v2.schema.json"] = new("https://schemas.infinium.dev/repository/m1-slice6-campaign-composed-evidence.v2.schema.json", "schema", "infinium.m1-s6.campaign-composed-evidence/v2"),
        ["m1-slice6-finite-campaign-authorization.v2.schema.json"] = new("https://schemas.infinium.dev/repository/m1-slice6-finite-campaign-authorization.v2.schema.json", "schema_identity", "infinium.repository.m1-slice6-finite-campaign-authorization/2.0.0"),
        ["wp9-production-profile-authorization.v2.schema.json"] = new("https://schemas.infinium.dev/repository/wp9-production-profile-authorization.v2.schema.json", "schema_identity", "infinium.repository.wp9-production-profile-authorization/2.0.0"),
        ["public-fixture-registry.v2.schema.json"] = new("https://schemas.infinium.dev/repository/public-fixture-registry.v2.schema.json", "schema_identity", "infinium.repository.public-fixture-registry/1.7.0"),
    };
    private static readonly IReadOnlyDictionary<string, string> FrozenV1Files = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/context-manifest.v1.json"] = "b422d956a47bc5f75e0cdc28b17a9e85d7ef6e31bdfa9aad18a7a112c9178ec8",
        ["fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/execution-input.v1.json"] = "77cffbebbc940357e1f8b39a9fd054c50e6c1a25c9e24c7b564f37867b95469c",
        ["fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/oracle-provenance.v1.json"] = "ac80c90d0259fb6c7ade2abce9a91378a019d48dfbd87d602663d803bda6978a",
        ["fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/oracle.v1.json"] = "2b23986da7308d312b4df33ed3440d14dcd2aaab85e5367e97c7ade1ed5cc28c",
        ["fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/public-manifest.json"] = "0f95265340873dc4abb083c6f857db9e8786c6e1ba36da385f07c876afe1c13f",
        ["fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/retained-transcripts.v1.json"] = "e3ad88078c80cc86f8e2919f543ae54eda577b20c7354bbf857a6260ac368d10",
        ["fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL/oracle.v1.json"] = "d917aed55912b0d6c82f8d19c772c6c504b9edcd3b1d3dcf9082da0f7a52e9eb",
        ["fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL/public-manifest.json"] = "83a63ba290966f27f7f6ddc581f63f7e2cb7b4d745f6958177f17043f220b3e6",
        ["fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/context-manifest.v1.json"] = "cc83747261efce58206f4ee71d5fae147392d4cb644e90eacaa7e2c4e414aed6",
        ["fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/execution-input.v1.json"] = "99029f0834e03e72bbba69ad4991a7ca22c441ce4888cfcfac31e7ca7e74fbe7",
        ["fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/oracle-provenance.v1.json"] = "addbf13b4848660ec0abd21e1f28233a5cef80c5cab075b9ad096aa93bc5badd",
        ["fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/oracle.v1.json"] = "0914b4c83eb215418cb28c34ff71018fb2ca2453da8ccc712164c06657b1ecb9",
        ["fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/public-manifest.json"] = "b42dff12144f192c1e7a913a3a99433398f0f2d41148a3353f7aa9cf89154323",
        ["fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/retained-transcripts.v1.json"] = "c150c07d2d9456261bc6458a64a3f2a6fb20851e4b257c858573817962772bd8",
        ["fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL/oracle.v1.json"] = "3f6db5e3618d8d0b5d35f2e79c203ef5bcd1bac8166e5cae417e7b5ac2e3348a",
        ["fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL/public-manifest.json"] = "486181221a67311ca14e5454451f40012465535e0f09e999561a58ed5110a135",
        ["fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL/oracle.v1.json"] = "2b8174e58cfdda414883aff245b8f9087647d05a9d4ca6c11e7b0ac076634f8e",
        ["fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL/public-manifest.json"] = "da6b6b05456a2ed956393c3a0f7c7d470a947adba8e991cae72e9dd7f28250c8",
    };
    private static readonly IReadOnlyDictionary<string, string> FrozenV2Evidence = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SourceRoot + "/oracle.v2.json"] = "e245320022d3e2c21424ca954b7ba8b4368fcb39ddcb9d4f6f053d7c60bab217",
        [SourceRoot + "/oracle-provenance.v2.json"] = "712a32ef7deee27815937e0b08eeb4ca82502fc28fa26fc677e43d68c73385f5",
        [SourceRoot + "/partition-history.v2.json"] = "3ee378eb45b40eb2578991ecf62576ba5f69d017c51321f15850de508efa14a6",
        [CandidateRoot + "/oracle.v2.json"] = "b72a319e70437968989ac73866f9c57326bed3f3b555ee21ff59de0861d6c0f6",
        [CandidateRoot + "/oracle-provenance.v2.json"] = "e821ceb5780e4ad526b48ca04d349fe69b175e21ac29824e865667cae963b82c",
        [CandidateRoot + "/partition-history.v2.json"] = "cb3f2064271806a7a5db61e9b004139efceddf760726f5f3f55173f132ef0ca4",
        [LiveRoot + "/LLM-CLAIM-LIVE-VAL-v2/oracle.v2.json"] = "76a631ffa02eeff301c240588d1507e3fe3cc2fe13f19aa597aecb8d2ddb3e14",
        [LiveRoot + "/LLM-INVESTIGATE-LIVE-VAL-v2/oracle.v2.json"] = "52f13b89f0c0cab2dc91c72e3986b8bc358e41a5ab5253ea1e8fab3b19230e3a",
        [LiveRoot + "/PROV-LIVE-COMPOSED-VAL-v2/oracle.v2.json"] = "e85cb6a9ead7c6ecb1a09b677fb8d6b12b3c29f022f6ea03cd8b6812c073e1d2",
    };

    public static LiveSemanticV2AuthorityReceipt Verify(string repositoryRoot)
    {
        string root = Path.GetFullPath(repositoryRoot);
        ValidateFrozenV1Trees(root);
        ValidateSchemas(root);
        ValidateFrozenV2Evidence(root);
        JsonDocument sourceManifest = ValidateInputPackage(root, SourceRoot, "S6-CLAIM-LIVE-VAL-v2",
            "infinium.public-fixture.source-claim/2.0.0");
        JsonDocument candidateManifest = ValidateInputPackage(root, CandidateRoot, "S6-CANDIDATE-LIVE-VAL-v2",
            "infinium.evaluation.candidate-investigation-public-manifest/2.0.0");
        using (sourceManifest)
        using (candidateManifest)
        {
            ValidateAnswerFree(Read(root, SourceRoot + "/execution-input.v2.json").RootElement, false);
            ValidateAnswerFree(Read(root, SourceRoot + "/context-manifest.v2.json").RootElement, false);
            ValidateAnswerFree(Read(root, CandidateRoot + "/execution-input.v2.json").RootElement, true);
            ValidateAnswerFree(Read(root, CandidateRoot + "/context-manifest.v2.json").RootElement, true);
            ValidateProvenanceAndPartitionHistory(root, SourceRoot, "S6-CLAIM-LIVE-VAL-v2",
                "infinium.evaluation.source-claim-oracle-provenance/v2");
            ValidateProvenanceAndPartitionHistory(root, CandidateRoot, "S6-CANDIDATE-LIVE-VAL-v2",
                "infinium.evaluation.candidate-investigation-oracle-provenance/v2");
            ValidateSourceSemantics(root);
            ValidateCandidateSemantics(root);
            ValidateLiveWrappers(root);
            ValidateLiveOracleSemantics(root);
            ValidateRegistry(root);
        }
        string registry = At(root, "fixtures/public/public-fixture-registry.v2.json");
        return new(43, 38, 5, Schemas.Count, Sha(registry));
    }

    private static JsonDocument ValidateInputPackage(string root, string relativeRoot, string packageId, string schema)
    {
        string directory = At(root, relativeRoot);
        string[] actual = Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(InputPackageFiles, StringComparer.Ordinal))
            throw new InvalidDataException(packageId + " physical file closure is not exact.");
        JsonDocument manifest = Read(root, relativeRoot + "/public-manifest.json");
        JsonElement value = manifest.RootElement;
        if (value.GetProperty("schema_identity").GetString() != schema
            || value.GetProperty("package_identity").GetString() != packageId
            || value.GetProperty("package_version").GetString() != "2.0.0"
            || value.GetProperty("partition").GetString() != "validation"
            || !value.GetProperty("answer_free_product_inputs").GetBoolean()
            || !value.GetProperty("oracle_frozen_before_product_comparison").GetBoolean()
            || value.GetProperty("network_required").GetBoolean()
            || value.GetProperty("provider_request_count").GetInt32() != 0
            || value.GetProperty("credential_operation_count").GetInt32() != 0)
            throw new InvalidDataException(packageId + " manifest identity/effect boundary is invalid.");
        string[] expectedPaths =
            ["execution-input.v2.json", "context-manifest.v2.json", "oracle.v2.json", "oracle-provenance.v2.json", "partition-history.v2.json"];
        JsonElement[] identities = value.GetProperty("file_identities").EnumerateArray().ToArray();
        if (!identities.Select(x => x.GetProperty("path").GetString()).SequenceEqual(expectedPaths, StringComparer.Ordinal)
            || identities.Select(x => x.GetProperty("path").GetString()).Distinct(StringComparer.Ordinal).Count() != 5)
            throw new InvalidDataException(packageId + " manifest inventory is incomplete or duplicated.");
        string[] expectedRoles = ["product-input", "product-input", "oracle", "oracle-provenance", "partition-history"];
        if (!identities.Select(x => x.GetProperty("role").GetString()).SequenceEqual(expectedRoles, StringComparer.Ordinal))
            throw new InvalidDataException(packageId + " manifest evidence roles are not exact.");
        foreach (JsonElement item in identities)
            ValidateBinding(root, relativeRoot + "/" + item.GetProperty("path").GetString(), item, "bytes", "sha256");
        return manifest;
    }

    private static void ValidateFrozenV1Trees(string root)
    {
        string[] expected = FrozenV1Files.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] treeRoots = expected.Select(x => x[..x.LastIndexOf('/')])
            .Distinct(StringComparer.Ordinal).ToArray();
        string[] actual = treeRoots.SelectMany(relative => Directory.EnumerateFiles(At(root, relative), "*", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidDataException("Frozen v1 package-tree inventory drifted or contains an untracked addition.");
        foreach ((string relative, string hash) in FrozenV1Files)
            if (Sha(At(root, relative)) != hash)
                throw new InvalidDataException(relative + " drifted from the fixed planning-base byte hash.");
    }

    private static void ValidateFrozenV2Evidence(string root)
    {
        foreach ((string relative, string hash) in FrozenV2Evidence)
            if (Sha(At(root, relative)) != hash)
                throw new InvalidDataException(relative + " drifted from its independently frozen evidence bytes.");
    }

    private static void ValidateProvenanceAndPartitionHistory(string root, string packageRoot, string packageId, string provenanceSchema)
    {
        using JsonDocument provenance = Read(root, packageRoot + "/oracle-provenance.v2.json");
        JsonElement value = provenance.RootElement;
        if (value.GetProperty("schema_id").GetString() != provenanceSchema
            || value.GetProperty("package_id").GetString() != packageId
            || value.GetProperty("package_version").GetString() != "2.0.0"
            || value.GetProperty("partition").GetString() != "validation"
            || value.GetProperty("oracle_path").GetString() != packageRoot + "/oracle.v2.json")
            throw new InvalidDataException(packageId + " oracle provenance identity is invalid.");
        JsonElement[] bindings = value.GetProperty("frozen_input_bindings").EnumerateArray().ToArray();
        string[] roles = ["context-manifest", "execution-input"];
        string[] paths = [packageRoot + "/context-manifest.v2.json", packageRoot + "/execution-input.v2.json"];
        if (bindings.Length != 2 || !bindings.Select(x => x.GetProperty("role").GetString()).SequenceEqual(roles, StringComparer.Ordinal)
            || !bindings.Select(x => x.GetProperty("path").GetString()).SequenceEqual(paths, StringComparer.Ordinal))
            throw new InvalidDataException(packageId + " provenance frozen-input bindings are not exact.");
        foreach (JsonElement binding in bindings)
            ValidateBinding(root, binding.GetProperty("path").GetString()!, binding, "byte_length", "sha256");
        JsonElement authorship = value.GetProperty("authorship");
        foreach (string field in new[] { "product_output_used", "product_implementation_used", "private_material_used",
            "archive_material_used", "prior_live_response_used", "prior_oracle_used", "retained_transcript_used" })
            if (authorship.GetProperty(field).GetBoolean())
                throw new InvalidDataException(packageId + " provenance violates independent authorship: " + field + ".");
        JsonElement independence = value.GetProperty("independence");
        if (!independence.GetProperty("oracle_is_not_product_visible").GetBoolean()
            || !independence.GetProperty("expected_truth_precedes_product_comparison").GetBoolean()
            || !independence.GetProperty("expected_truth_is_not_derived_from_product_identifiers_or_diagnostics").GetBoolean()
            || independence.GetProperty("fixture_partition").GetString() != "validation")
            throw new InvalidDataException(packageId + " provenance independence contract is invalid.");

        using JsonDocument history = Read(root, packageRoot + "/partition-history.v2.json");
        JsonElement historyRoot = history.RootElement;
        JsonElement[] states = historyRoot.GetProperty("history").EnumerateArray().ToArray();
        if (historyRoot.GetProperty("schema_id").GetString() != "infinium.evaluation.fixture-partition-history/2.0.0"
            || historyRoot.GetProperty("package_id").GetString() != packageId
            || historyRoot.GetProperty("partition").GetString() != "validation"
            || states.Length != 2
            || states[0].GetProperty("state").GetString() != "input-frozen-before-oracle-authoring"
            || states[1].GetProperty("state").GetString() != "oracle-frozen-before-product-comparison"
            || string.IsNullOrWhiteSpace(historyRoot.GetProperty("replacement_rule").GetString()))
            throw new InvalidDataException(packageId + " partition history is incomplete or reordered.");
    }

    private static void ValidateSourceSemantics(string root)
    {
        using JsonDocument input = Read(root, SourceRoot + "/execution-input.v2.json");
        using JsonDocument context = Read(root, SourceRoot + "/context-manifest.v2.json");
        using JsonDocument oracle = Read(root, SourceRoot + "/oracle.v2.json");
        JsonElement sourceBindings = oracle.RootElement.GetProperty("input_bindings");
        ValidateBinding(root, SourceRoot + "/context-manifest.v2.json", sourceBindings.GetProperty("context_manifest"), "byte_length", "sha256");
        ValidateBinding(root, SourceRoot + "/execution-input.v2.json", sourceBindings.GetProperty("execution_input"), "byte_length", "sha256");
        JsonElement[] passages = input.RootElement.GetProperty("passages").EnumerateArray().ToArray();
        if (passages.Length != 9 || passages.Select(x => x.GetProperty("passage_id").GetString()).Distinct().Count() != 9)
            throw new InvalidDataException("Source passage identities are not total and unique.");
        int next = 0;
        foreach (JsonElement passage in passages)
        {
            if (passage.GetProperty("start_byte").GetInt32() != next)
                throw new InvalidDataException("Source passage offsets are not contiguous.");
            next = passage.GetProperty("end_byte").GetInt32() + 1;
            bool deleted = passage.GetProperty("deleted").GetBoolean();
            if (deleted && passage.TryGetProperty("text", out _))
                throw new InvalidDataException("Deleted source body became product-visible.");
            if (!deleted && ShaUtf8(passage.GetProperty("text").GetString()!) != passage.GetProperty("text_sha256").GetString())
                throw new InvalidDataException("Source passage fingerprint drifted.");
        }
        JsonElement semantics = oracle.RootElement.GetProperty("expected_semantics");
        JsonElement[] states = semantics.GetProperty("state_expectations").EnumerateArray().ToArray();
        string[] requiredStates = ["supported-applicable", "unsupported-negative", "conditional-unestablished",
            "version-mismatched", "contradictory", "hostile-untrusted", "deleted-audit-only", "insufficient-abstention"];
        if (!states.Select(x => x.GetProperty("state").GetString()).SequenceEqual(requiredStates, StringComparer.Ordinal)
            || states.Count(x => x.GetProperty("host_admission").GetString() == "required") != 1)
            throw new InvalidDataException("Source state/admission matrix is not exact.");
        string[] statePassages = states.SelectMany(x => x.GetProperty("passage_ids").EnumerateArray())
            .Select(x => x.GetString()!).ToArray();
        if (statePassages.Length != 9 || statePassages.Distinct(StringComparer.Ordinal).Count() != 9
            || !statePassages.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(
                passages.Select(x => x.GetProperty("passage_id").GetString()!).OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Source state coverage is not total over the frozen passage identities.");
        JsonElement totality = semantics.GetProperty("required_totality");
        if (totality.GetProperty("expected_admitted_proposal_count").GetInt32() != 1
            || totality.GetProperty("expected_host_admission_count").GetInt32() != 1
            || totality.GetProperty("expected_admitted_artifact_count").GetInt32() != 1)
            throw new InvalidDataException("Source semantic totality counts are not exact.");
        JsonElement retainedValidation = semantics.GetProperty("retained_live_response_validation");
        if (retainedValidation.GetProperty("required_completed_response_envelopes").GetInt32() != 1
            || !retainedValidation.GetProperty("validates_retained_live_response").GetBoolean()
            || retainedValidation.GetProperty("executes_canned_transcript").GetBoolean()
            || !retainedValidation.GetProperty("requires_replay_equality_without_provider_request").GetBoolean())
            throw new InvalidDataException("Source retained-response/replay contract is invalid.");
        string[] retained = context.RootElement.GetProperty("retained_passage_ids").EnumerateArray().Select(x => x.GetString()!).ToArray();
        string[] audit = context.RootElement.GetProperty("audit_only_passage_ids").EnumerateArray().Select(x => x.GetString()!).ToArray();
        if (retained.Length != 8 || audit.Length != 1 || retained.Intersect(audit, StringComparer.Ordinal).Any())
            throw new InvalidDataException("Source retained/audit projection is invalid.");
    }

    private static void ValidateCandidateSemantics(string root)
    {
        using JsonDocument input = Read(root, CandidateRoot + "/execution-input.v2.json");
        using JsonDocument oracle = Read(root, CandidateRoot + "/oracle.v2.json");
        using JsonDocument sourceOracle = Read(root, SourceRoot + "/oracle.v2.json");
        JsonElement candidateBindings = oracle.RootElement.GetProperty("input_bindings");
        ValidateBinding(root, CandidateRoot + "/context-manifest.v2.json", candidateBindings.GetProperty("context_manifest"), "byte_length", "sha256");
        ValidateBinding(root, CandidateRoot + "/execution-input.v2.json", candidateBindings.GetProperty("execution_input"), "byte_length", "sha256");
        JsonElement[] contexts = input.RootElement.GetProperty("contexts").EnumerateArray().ToArray();
        if (contexts.Length != 2 || contexts.Select(x => x.GetProperty("context_id").GetString()).Distinct().Count() != 2)
            throw new InvalidDataException("Candidate contexts are not exactly two unique cases.");
        string positiveObservation = contexts[0].GetProperty("local_observations")[0].GetProperty("text_sha256").GetString()!;
        if (contexts[1].GetProperty("local_observations")[0].GetProperty("text_sha256").GetString() != positiveObservation)
            throw new InvalidDataException("Candidate matched contexts do not share the exact local observation.");
        JsonElement positiveEvidence = contexts[0].GetProperty("evidence")[0];
        JsonElement negativeEvidence = contexts[1].GetProperty("evidence")[0];
        if (positiveEvidence.GetProperty("relationship").GetString() != "supporting"
            || !positiveEvidence.TryGetProperty("host_bindings", out JsonElement hostBindings)
            || negativeEvidence.GetProperty("relationship").GetString() != "neutral"
            || negativeEvidence.TryGetProperty("host_bindings", out _))
            throw new InvalidDataException("Candidate positive/negative evidence roles are invalid.");
        JsonElement sourceSemantics = sourceOracle.RootElement.GetProperty("expected_semantics");
        JsonElement admission = sourceSemantics.GetProperty("host_admission");
        foreach ((string bindingName, string oracleName) in new[]
        {
            ("acquisition_run_id", "acquisition_run_id"), ("proposal_id", "proposal_id"),
            ("source_admission_id", "admission_id"), ("admitted_artifact_id", "admitted_artifact_id"),
            ("application_link_id", "application_link_id"), ("source_revision_id", "source_revision_id"),
            ("passage_id", "passage_id"), ("persisted_payload_sha256", "persisted_payload_sha256"),
        })
            if (hostBindings.GetProperty(bindingName).GetString()
                != (bindingName == "acquisition_run_id" ? sourceSemantics : admission).GetProperty(oracleName).GetString())
                throw new InvalidDataException("WP11 positive evidence does not bind exact WP10 oracle identity " + bindingName + ".");
        JsonElement[] expected = oracle.RootElement.GetProperty("expected_semantics").GetProperty("contexts").EnumerateArray().ToArray();
        if (expected.Length != 2 || expected[0].GetProperty("expected_host_result").GetString() != "accepted"
            || expected[1].GetProperty("expected_host_result").GetString() != "abstained")
            throw new InvalidDataException("Candidate expected dispositions are not exact.");
        if (!expected.Select(x => x.GetProperty("context_id").GetString()).SequenceEqual(
                contexts.Select(x => x.GetProperty("context_id").GetString()), StringComparer.Ordinal)
            || expected.Any(x => x.GetProperty("required_local_observation").GetProperty("observation_id").GetString()
                != contexts[0].GetProperty("local_observations")[0].GetProperty("observation_id").GetString()))
            throw new InvalidDataException("Candidate oracle context/local-observation identities do not join the frozen input.");
        JsonElement totality = oracle.RootElement.GetProperty("expected_semantics").GetProperty("required_totality");
        if (totality.GetProperty("expected_accepted_hypothesis_count").GetInt32() != 1
            || totality.GetProperty("expected_rejected_or_abstained_hypothesis_count").GetInt32() != 1
            || !totality.GetProperty("requires_host_admission").GetBoolean()
            || !totality.GetProperty("requires_durable_replay").GetBoolean()
            || !totality.GetProperty("requires_exact_wp10_to_wp11_application_edge").GetBoolean())
            throw new InvalidDataException("Candidate semantic totality/replay contract is invalid.");
    }

    private static void ValidateLiveWrappers(string root)
    {
        ValidateLive(root, "LLM-CLAIM-LIVE-VAL-v2", "SourceClaimExtraction", SourceRoot,
            "infinium.public-fixture.live-source-claim/2.0.0", "infinium.evaluation.live-source-claim-oracle/2.0.0");
        ValidateLive(root, "LLM-INVESTIGATE-LIVE-VAL-v2", "CandidateInvestigation", CandidateRoot,
            "infinium.public-fixture.live-candidate-investigation/2.0.0", "infinium.evaluation.live-candidate-investigation-oracle/2.0.0");
        string relative = LiveRoot + "/PROV-LIVE-COMPOSED-VAL-v2";
        AssertFiles(At(root, relative), LivePackageFiles);
        using JsonDocument manifest = Read(root, relative + "/public-manifest.json");
        JsonElement value = manifest.RootElement;
        if (value.GetProperty("schema_identity").GetString() != "infinium.public-fixture.live-composed-provenance/2.0.0"
            || value.GetProperty("provider_call_count").GetInt32() != 0 || !value.GetProperty("no_fourth_call").GetBoolean())
            throw new InvalidDataException("Composed wrapper effect boundary is invalid.");
        ValidateBinding(root, relative + "/oracle.v2.json", value.GetProperty("oracle"), "bytes", "sha256");
        JsonElement qualification = value.GetProperty("qualification");
        if (qualification.GetProperty("semantic_use").GetBoolean())
            throw new InvalidDataException("Qualification must remain non-semantic.");
        ValidateBinding(root, qualification.GetProperty("manifest_path").GetString()!, qualification, "manifest_bytes", "manifest_sha256");
        JsonElement[] stages = value.GetProperty("stage_wrappers").EnumerateArray().ToArray();
        if (stages.Length != 2 || stages[0].GetProperty("stage").GetString() != "WP10"
            || stages[1].GetProperty("stage").GetString() != "WP11" || stages.Any(x => !x.GetProperty("semantic_use").GetBoolean()))
            throw new InvalidDataException("Composed wrapper stage order/semantic use is invalid.");
        foreach (JsonElement stage in stages)
            ValidateBinding(root, stage.GetProperty("manifest_path").GetString()!, stage, "manifest_bytes", "manifest_sha256");
    }

    private static void ValidateLiveOracleSemantics(string root)
    {
        using JsonDocument source = Read(root, LiveRoot + "/LLM-CLAIM-LIVE-VAL-v2/oracle.v2.json");
        using JsonDocument sourceBase = Read(root, SourceRoot + "/oracle.v2.json");
        JsonElement sourceValue = source.RootElement;
        ValidateRetainedLiveFlags(sourceValue, "LLM-CLAIM-LIVE-VAL-v2");
        JsonElement sourcePackage = sourceValue.GetProperty("source_package");
        ValidateBinding(root, SourceRoot + "/execution-input.v2.json", sourcePackage.GetProperty("execution_input"), "byte_length", "sha256");
        ValidateBinding(root, SourceRoot + "/context-manifest.v2.json", sourcePackage.GetProperty("context_manifest"), "byte_length", "sha256");
        JsonElement sourceRequired = sourceValue.GetProperty("required_live_validation");
        JsonElement sourceSemantics = sourceBase.RootElement.GetProperty("expected_semantics");
        JsonElement sourceAdmission = sourceSemantics.GetProperty("host_admission");
        if (sourceRequired.GetProperty("completed_response_envelope_count").GetInt32() != 1
            || sourceRequired.GetProperty("expected_host_admission_count").GetInt32() != 1
            || !sourceRequired.GetProperty("requires_typed_semantic_oracle_validation").GetBoolean()
            || !sourceRequired.GetProperty("requires_retained_response_replay_equality").GetBoolean())
            throw new InvalidDataException("Live source retained-response requirements are invalid.");
        AssertStringJoin(sourceRequired, "acquisition_run_id", sourceSemantics, "acquisition_run_id", "live source acquisition");
        foreach ((string left, string right) in new[] { ("admission_id", "admission_id"), ("proposal_id", "proposal_id"),
            ("admitted_artifact_id", "admitted_artifact_id"), ("application_link_id", "application_link_id"),
            ("source_revision_id", "source_revision_id"), ("passage_id", "passage_id"),
            ("persisted_payload_sha256", "persisted_payload_sha256"),
            ("required_applicability_fact_id", "required_applicability_fact_id"),
            ("required_applicability_fact_sha256", "required_applicability_fact_sha256") })
            AssertStringJoin(sourceRequired, left, sourceAdmission, right, "live source " + left);
        string[] noAdmission = sourceValue.GetProperty("no_admission_states").EnumerateArray().Select(x => x.GetString()!).ToArray();
        string[] forbiddenStates = sourceSemantics.GetProperty("state_expectations").EnumerateArray()
            .Where(x => x.GetProperty("host_admission").GetString() == "forbidden")
            .Select(x => x.GetProperty("state").GetString()!).ToArray();
        if (!noAdmission.SequenceEqual(forbiddenStates, StringComparer.Ordinal))
            throw new InvalidDataException("Live source no-admission states do not exactly project the source oracle.");

        using JsonDocument candidate = Read(root, LiveRoot + "/LLM-INVESTIGATE-LIVE-VAL-v2/oracle.v2.json");
        using JsonDocument candidateBase = Read(root, CandidateRoot + "/oracle.v2.json");
        JsonElement candidateValue = candidate.RootElement;
        ValidateRetainedLiveFlags(candidateValue, "LLM-INVESTIGATE-LIVE-VAL-v2");
        JsonElement candidatePackage = candidateValue.GetProperty("candidate_package");
        ValidateBinding(root, CandidateRoot + "/execution-input.v2.json", candidatePackage.GetProperty("execution_input"), "byte_length", "sha256");
        ValidateBinding(root, CandidateRoot + "/context-manifest.v2.json", candidatePackage.GetProperty("context_manifest"), "byte_length", "sha256");
        JsonElement candidateRequired = candidateValue.GetProperty("required_live_validation");
        JsonElement[] expectedContexts = candidateBase.RootElement.GetProperty("expected_semantics").GetProperty("contexts").EnumerateArray().ToArray();
        if (candidateRequired.GetProperty("completed_response_envelope_count").GetInt32() != 1
            || !candidateRequired.GetProperty("requires_same_conditional_local_observation_in_both_contexts").GetBoolean()
            || !candidateRequired.GetProperty("requires_activation_unestablished_for_abstained_context").GetBoolean()
            || !candidateRequired.GetProperty("requires_visible_uncertainty_and_gaps").GetBoolean()
            || !candidateRequired.GetProperty("requires_typed_semantic_oracle_validation").GetBoolean()
            || !candidateRequired.GetProperty("requires_retained_response_replay_equality").GetBoolean())
            throw new InvalidDataException("Live candidate retained-response requirements are invalid.");
        AssertStringJoin(candidateRequired, "accepted_context_id", expectedContexts[0], "context_id", "accepted context");
        AssertStringJoin(candidateRequired, "accepted_candidate_id", expectedContexts[0], "candidate_id", "accepted candidate");
        AssertStringJoin(candidateRequired, "accepted_hypothesis_id", expectedContexts[0], "hypothesis_id", "accepted hypothesis");
        AssertStringJoin(candidateRequired, "abstained_context_id", expectedContexts[1], "context_id", "abstained context");
        AssertStringJoin(candidateRequired, "abstained_candidate_id", expectedContexts[1], "candidate_id", "abstained candidate");
        AssertStringJoin(candidateRequired, "abstained_hypothesis_id", expectedContexts[1], "hypothesis_id", "abstained hypothesis");
        JsonElement predecessor = candidateRequired.GetProperty("required_wp10_predecessor");
        foreach ((string left, string right) in new[] { ("acquisition_run_id", "acquisition_run_id"), ("proposal_id", "proposal_id"),
            ("source_admission_id", "admission_id"), ("admitted_artifact_id", "admitted_artifact_id"),
            ("application_link_id", "application_link_id"), ("source_revision_id", "source_revision_id"),
            ("passage_id", "passage_id"), ("persisted_payload_sha256", "persisted_payload_sha256"),
            ("required_applicability_fact_id", "required_applicability_fact_id"),
            ("required_applicability_fact_sha256", "required_applicability_fact_sha256") })
            AssertStringJoin(predecessor, left, left == "acquisition_run_id" ? sourceSemantics : sourceAdmission, right, "WP10 predecessor " + left);

        ValidateComposedOracle(root, sourceRequired, candidateRequired);
    }

    private static void ValidateRetainedLiveFlags(JsonElement value, string package)
    {
        JsonElement validates = value.GetProperty("validates");
        if (value.GetProperty("package_id").GetString() != package || !value.GetProperty("semantic_use").GetBoolean()
            || !validates.GetProperty("retained_live_response").GetBoolean()
            || !validates.GetProperty("host_admission").GetBoolean()
            || validates.GetProperty("canned_transcript_execution").GetBoolean()
            || validates.GetProperty("provider_request_during_replay").GetBoolean())
            throw new InvalidDataException(package + " retained-live/canned/provider flags are invalid.");
    }

    private static void ValidateComposedOracle(string root, JsonElement sourceRequired, JsonElement candidateRequired)
    {
        using JsonDocument oracle = Read(root, LiveRoot + "/PROV-LIVE-COMPOSED-VAL-v2/oracle.v2.json");
        using JsonDocument manifest = Read(root, LiveRoot + "/PROV-LIVE-COMPOSED-VAL-v2/public-manifest.json");
        JsonElement value = oracle.RootElement;
        JsonElement flags = value.GetProperty("validates");
        if (value.GetProperty("provider_call_count").GetInt32() != 0 || !value.GetProperty("no_fourth_call").GetBoolean()
            || !flags.GetProperty("retained_live_responses").GetBoolean() || !flags.GetProperty("host_admissions").GetBoolean()
            || flags.GetProperty("canned_transcript_execution").GetBoolean()
            || flags.GetProperty("provider_request_during_composed_validation").GetBoolean())
            throw new InvalidDataException("Composed oracle effect/replay boundary is invalid.");
        JsonElement[] frozen = value.GetProperty("frozen_input_bindings").EnumerateArray().ToArray();
        string[] frozenPaths = [SourceRoot + "/execution-input.v2.json", SourceRoot + "/context-manifest.v2.json",
            CandidateRoot + "/execution-input.v2.json", CandidateRoot + "/context-manifest.v2.json"];
        if (frozen.Length != 4 || !frozen.Select(x => x.GetProperty("path").GetString()).SequenceEqual(frozenPaths, StringComparer.Ordinal))
            throw new InvalidDataException("Composed oracle frozen-input order is invalid.");
        foreach (JsonElement binding in frozen)
            ValidateBinding(root, binding.GetProperty("path").GetString()!, binding, "byte_length", "sha256");
        JsonElement[] stages = value.GetProperty("required_stage_order").EnumerateArray().ToArray();
        string[] packages = ["M1-PLAT-PROVIDER-CAPABILITY-VAL-v1", "LLM-CLAIM-LIVE-VAL-v2", "LLM-INVESTIGATE-LIVE-VAL-v2"];
        string[] roles = ["qualification", "wp10-source-claim", "wp11-candidate-investigation"];
        if (stages.Length != 3 || !stages.Select(x => x.GetProperty("package_id").GetString()).SequenceEqual(packages, StringComparer.Ordinal)
            || !stages.Select(x => x.GetProperty("role").GetString()).SequenceEqual(roles, StringComparer.Ordinal)
            || stages[0].GetProperty("semantic_use").GetBoolean() || stages.Skip(1).Any(x => !x.GetProperty("semantic_use").GetBoolean()))
            throw new InvalidDataException("Composed oracle qualification/WP10/WP11 order is invalid.");
        JsonElement chain = value.GetProperty("required_semantic_chain");
        AssertStringJoin(chain.GetProperty("wp10"), "admitted_artifact_id", sourceRequired, "admitted_artifact_id", "composed WP10 artifact");
        AssertStringJoin(chain.GetProperty("wp11"), "accepted_context_id", candidateRequired, "accepted_context_id", "composed WP11 accepted context");
        string[] required = value.GetProperty("required_bindings").EnumerateArray().Select(x => x.GetString()!).ToArray();
        string[] manifestRequired = manifest.RootElement.GetProperty("required_bindings").EnumerateArray().Select(x => x.GetString()!).ToArray();
        string[] omissions = value.GetProperty("explicit_omissions").EnumerateArray().Select(x => x.GetString()!).ToArray();
        string[] manifestOmissions = manifest.RootElement.GetProperty("explicit_omissions").EnumerateArray().Select(x => x.GetString()!).ToArray();
        if (!required.SequenceEqual(manifestRequired, StringComparer.Ordinal)
            || !omissions.SequenceEqual(manifestOmissions, StringComparer.Ordinal))
            throw new InvalidDataException("Composed oracle binding/omission lists do not join its manifest.");
    }

    private static void AssertStringJoin(JsonElement left, string leftName, JsonElement right, string rightName, string label)
    {
        if (left.GetProperty(leftName).GetString() != right.GetProperty(rightName).GetString())
            throw new InvalidDataException(label + " semantic join drifted.");
    }

    private static void ValidateLive(string root, string package, string operation, string predecessorRoot,
        string schema, string oracleSchema)
    {
        string relative = LiveRoot + "/" + package;
        AssertFiles(At(root, relative), LivePackageFiles);
        using JsonDocument manifest = Read(root, relative + "/public-manifest.json");
        JsonElement value = manifest.RootElement;
        if (value.GetProperty("schema_identity").GetString() != schema
            || value.GetProperty("package_identity").GetString() != package
            || value.GetProperty("operation").GetString() != operation
            || !value.GetProperty("semantic_use").GetBoolean()
            || !value.GetProperty("answer_free_product_input").GetBoolean()
            || value.GetProperty("network_required_for_oracle").GetBoolean()
            || value.GetProperty("oracle").GetProperty("schema_identity").GetString() != oracleSchema
            || value.GetProperty("oracle").GetProperty("product_visible").GetBoolean())
            throw new InvalidDataException(package + " wrapper identity/isolation is invalid.");
        ValidateBinding(root, value.GetProperty("product_input").GetProperty("path").GetString()!, value.GetProperty("product_input"), "bytes", "sha256");
        string predecessorPath = predecessorRoot + "/public-manifest.json";
        if (value.GetProperty("predecessor_manifest").GetProperty("path").GetString() != predecessorPath)
            throw new InvalidDataException(package + " has a v1/v2 predecessor cross-binding.");
        ValidateBinding(root, predecessorPath, value.GetProperty("predecessor_manifest"), "bytes", "sha256");
        ValidateBinding(root, relative + "/oracle.v2.json", value.GetProperty("oracle"), "bytes", "sha256");
    }

    private static void ValidateRegistry(string root)
    {
        using JsonDocument v1 = Read(root, "fixtures/public/public-fixture-registry.v1.json");
        using JsonDocument v2 = Read(root, "fixtures/public/public-fixture-registry.v2.json");
        JsonElement value = v2.RootElement;
        JsonElement[] oldRows = v1.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        JsonElement[] rows = value.GetProperty("packages").EnumerateArray().ToArray();
        if (value.GetProperty("schema_identity").GetString() != "infinium.repository.public-fixture-registry/1.7.0"
            || value.GetProperty("registry_version").GetString() != "1.7.0"
            || value.GetProperty("package_count").GetInt32() != 43 || oldRows.Length != 38 || rows.Length != 43
            || rows.Select(x => x.GetProperty("package_identity").GetString()).Distinct(StringComparer.Ordinal).Count() != 43)
            throw new InvalidDataException("Registry v2 identity, count, or uniqueness is invalid.");
        for (int index = 0; index < oldRows.Length; index++)
            if (!JsonNode.DeepEquals(JsonNode.Parse(oldRows[index].GetRawText()), JsonNode.Parse(rows[index].GetRawText())))
                throw new InvalidDataException("Registry v2 changed retained v1 entry " + index + ".");
        if (!rows.Skip(38).Select(x => x.GetProperty("package_identity").GetString()).SequenceEqual(PackageIds, StringComparer.Ordinal))
            throw new InvalidDataException("Registry v2 appended package order is invalid.");
        foreach (JsonElement row in rows.Skip(38))
            ValidateBinding(root, row.GetProperty("authority_file").GetString()!, row, "authority_bytes", "authority_sha256");
    }

    private static void ValidateSchemas(string root)
    {
        foreach ((string file, SchemaAuthority authority) in Schemas)
        {
            using JsonDocument document = Read(root, "contracts/repository/" + file);
            AssertRecursivelyClosed(document.RootElement, file);
            JsonElement value = document.RootElement;
            if (value.GetProperty("$id").GetString() != authority.Id
                || !value.GetProperty("properties").TryGetProperty(authority.IdentityProperty, out JsonElement identityProperty)
                || !identityProperty.TryGetProperty("const", out JsonElement identity)
                || identity.GetString() != authority.Identity)
                throw new InvalidDataException(file + " does not declare its exact $id and identity-property const.");
        }
    }

    private static void AssertRecursivelyClosed(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String
                && type.GetString() == "object"
                && (!value.TryGetProperty("additionalProperties", out JsonElement additional) || additional.ValueKind != JsonValueKind.False))
                throw new InvalidDataException(path + " contains an open object schema.");
            foreach (JsonProperty property in value.EnumerateObject()) AssertRecursivelyClosed(property.Value, path + "." + property.Name);
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) AssertRecursivelyClosed(item, path);
    }

    private static void ValidateAnswerFree(JsonElement value, bool allowDurableBindingNames)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                bool permittedBinding = allowDurableBindingNames && property.Name is "admitted_artifact_id";
                if (!permittedBinding && ForbiddenAnswerTokens.Any(token => property.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Product input contains answer-bearing field " + property.Name + ".");
                ValidateAnswerFree(property.Value, allowDurableBindingNames);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) ValidateAnswerFree(item, allowDurableBindingNames);
        else if (value.ValueKind == JsonValueKind.String)
        {
            string normalized = value.GetString()!.Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
            if (ForbiddenAnswerTokens.Any(token => normalized.Contains(token.Replace("_", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Product input contains answer-bearing value.");
        }
    }

    private static void ValidateBinding(string root, string relative, JsonElement binding, string bytesName, string shaName)
    {
        string path = At(root, relative);
        if (new FileInfo(path).Length != binding.GetProperty(bytesName).GetInt64()
            || Sha(path) != binding.GetProperty(shaName).GetString())
            throw new InvalidDataException(relative + " hash/length binding drifted.");
    }

    private static void AssertFiles(string directory, string[] expected)
    {
        string[] actual = Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidDataException(directory + " file closure is not exact.");
    }

    private static JsonDocument Read(string root, string relative) => JsonDocument.Parse(File.ReadAllBytes(At(root, relative)));
    private static string At(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path escaped repository root.");
        return path;
    }
    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static string ShaUtf8(string value) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
