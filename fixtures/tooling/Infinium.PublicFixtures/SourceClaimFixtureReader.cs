using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.PublicFixtures;

public sealed record SourceClaimFileIdentity(string Path, string Role, long Bytes, string Sha256);
public sealed record SourceClaimFixtureManifest(
    string SchemaIdentity, string PackageIdentity, string PackageVersion, string FixtureId, string FixtureVersion,
    string Partition, string Status, IReadOnlyList<string> ProductInputFiles, string OracleFile,
    string OracleProvenanceFile, IReadOnlyList<SourceClaimFileIdentity> FileIdentities, bool AnswerFree,
    bool NetworkRequired);
public sealed record SourceClaimOracleIdentity(
    string ExecutionSchemaId, string ContextSchemaId, string TranscriptSchemaId, string SourceClaimOutputSchemaId,
    string SchemaVersion, string AcquisitionRunId, string OperationId, string HostAuthorizationId, string OwnerKind, string OwnerId,
    string ParentAnalysisRunId, string ApplicationScopeId, string CostAttributionScopeId, string SourceRevisionId,
    string DeclaredPurpose, string SelectionPolicy, string PromptId, string PromptFingerprint,
    IReadOnlyList<string> PassageIds, IReadOnlyList<string> PassageFingerprints);
public sealed record SourceClaimExpectedProposal(
    string ProposalId, string PassageId, string Claim, IReadOnlyList<string> ConditionIds, string ClaimKind,
    string ConditionScope, string AuthorityCategory, string ApplicationSemantics, string InputState, string Reason,
    string ExpectedState);
public sealed record SourceClaimScenarioOracle(
    string TranscriptId, string ExpectedResponseRecordId, string ExpectedResponseState,
    string ExpectedResponseFingerprint, bool ExpectedModelUsed, string ExpectedResult,
    IReadOnlyList<SourceClaimExpectedProposal> ExpectedProposals,
    IReadOnlyDictionary<string, string> ExpectedProposalStates,
    IReadOnlyList<string> AdmittedProposalIds, IReadOnlyList<string> RejectedProposalIds,
    IReadOnlyList<string> ContradictionEvidenceIds, IReadOnlyList<string> ExpectedAbstentions,
    IReadOnlyList<string> ExpectedGaps, IReadOnlyList<string> ExpectedAbstentionKinds,
    IReadOnlyList<string> ExpectedGapKinds, int ExpectedAbstentionCount, int ExpectedGapCount,
    int ExpectedAdmittedCorrelationCount, bool ProviderUsed, string ReplayState, bool AuditOnly,
    bool ForbiddenAuthorityDetected);
public sealed record SourceClaimAggregateOracle(
    int ScenarioCount, int AcceptedProposalCount, int RejectedProposalCount, int AdmittedCorrelationCount,
    int ModelUsedScenarioCount, int DistinctOperationIdCount, int NoModelScenarioCount,
    int ProposalCount, int AbstentionCount, int GapCount, int RetainedResponseScenarioCount,
    int AuditOnlyScenarioCount, int FailedIdentityDriftScenarioCount, int ForbiddenAuthorityScenarioCount,
    int NetworkSendCount, int CredentialOperationCount);
public sealed record SourceClaimOracle(
    string SchemaId, string SchemaVersion, string PackageId, string Partition, IReadOnlyList<string> Authority,
    SourceClaimOracleIdentity ExpectedIdentity, IReadOnlyList<SourceClaimScenarioOracle> Scenarios,
    SourceClaimAggregateOracle AggregateExpectations, IReadOnlyDictionary<string, bool> FrozenBoundaries,
    IReadOnlyList<string> ForbiddenClaims);
public sealed record SourceClaimPartitionHistory(string State, string Reason);
public sealed record SourceClaimOracleProvenance(
    string SchemaId, string SchemaVersion, string PackageId, string Partition, DateTimeOffset AuthoredAtUtc,
    string AuthorRole, string Method, bool ProductOutputUsed, bool ProductImplementationUsed,
    bool PrivateOrHeldOutMaterialUsed, IReadOnlyDictionary<string, string> InputIdentities,
    IReadOnlyList<SourceClaimPartitionHistory> PartitionHistory, string ReplacementRule);
public sealed record SourceClaimFixturePackage(
    string PackageId, string Partition, SourceClaimExecutionInput ExecutionInput,
    IReadOnlyList<SourceClaimRetainedTranscript> Transcripts, SourceClaimOracle Oracle,
    SourceClaimOracleProvenance OracleProvenance);

public static class SourceClaimFixtureReader
{
    private const long MaximumDocumentBytes = 128 * 1024;
    private static readonly string[] ExactFiles =
    [
        "context-manifest.v1.json", "execution-input.v1.json", "oracle-provenance.v1.json",
        "oracle.v1.json", "public-manifest.json", "retained-transcripts.v1.json",
    ];
    private static readonly string[] ProductInputs =
        ["execution-input.v1.json", "context-manifest.v1.json", "retained-transcripts.v1.json"];
    private static readonly string[] ForbiddenAnswerTokens =
        ["expected_", "oracle", "ground_truth", "matched_negative", "correct_answer"];
    private static readonly string[] AcceptedAuthority =
    [
        "docs/plans/milestones/m1/slices/s6/plan.md#17-m1s6wp6--source-claim-acquisition-and-deterministic-admission",
        "docs/architecture/decisions/ADR-0001-evidence-authority-boundary.md",
        "docs/architecture/decisions/ADR-0002-snapshot-context-binding.md",
        "docs/architecture/decisions/ADR-0013-openai-first-llm-capability-boundary.md",
    ];

    [Obsolete("Historical source-claim semantic oracles cannot be loaded by current product gates.", error: true)]
    public static SourceClaimFixturePackage Read(string directory) =>
        throw new InvalidOperationException(
            "Historical source-claim semantic oracles are retained bytes and grant no current authority.");

    internal static SourceClaimFixturePackage ReadForContractTest(string directory)
        => ReadCore(directory, validateRegistry: false);

    internal static void AssertAnswerFreeForContractTest(JsonElement element)
        => AssertAnswerFree(element, propertyName: null);

    private static SourceClaimFixturePackage ReadCore(string directory, bool validateRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string fullDirectory = Path.GetFullPath(directory);
        string[] files = Directory.EnumerateFiles(fullDirectory).Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] expectedFiles = File.Exists(Path.Combine(fullDirectory, "reclassification.v1.json"))
            ? ExactFiles.Append("reclassification.v1.json")
                .OrderBy(value => value, StringComparer.Ordinal).ToArray()
            : ExactFiles;
        if (!files.SequenceEqual(expectedFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Source-claim fixture package closure is not exact.");
        }

        using BoundedJsonDocumentSnapshot manifestDocument = ReadBounded(Path.Combine(fullDirectory, "public-manifest.json"));
        SourceClaimFixtureManifest manifest = Deserialize<SourceClaimFixtureManifest>(manifestDocument.Document.RootElement);
        ValidateManifest(fullDirectory, manifest);
        if (validateRegistry)
        {
            ValidateRegistryAuthority(fullDirectory, manifest);
        }

        Dictionary<string, BoundedJsonDocumentSnapshot> productDocuments = [];
        try
        {
            foreach (string productInput in manifest.ProductInputFiles)
            {
                BoundedJsonDocumentSnapshot document = ReadBounded(Path.Combine(fullDirectory, productInput));
                productDocuments.Add(productInput, document);
                AssertAnswerFree(document.Document.RootElement, propertyName: null);
            }
            ActiveJsonSchemaValidator.Validate(productDocuments["execution-input.v1.json"].Document.RootElement,
                "source-claim-execution-input.v1.schema.json");
            ActiveJsonSchemaValidator.Validate(productDocuments["retained-transcripts.v1.json"].Document.RootElement,
                "source-claim-retained-transcripts.v1.schema.json");
            ActiveJsonSchemaValidator.Validate(productDocuments["context-manifest.v1.json"].Document.RootElement,
                "source-claim-context.v1.schema.json");
            SourceClaimExecutionInput input = Deserialize<SourceClaimExecutionInput>(
                productDocuments["execution-input.v1.json"].Document.RootElement);
            if (input.PackageId != manifest.PackageIdentity)
            {
                throw new InvalidDataException("Source-claim execution input is not bound to the manifest package identity.");
            }
            SourceClaimRetainedTranscript[] transcripts = Deserialize<SourceClaimRetainedTranscript[]>(
                productDocuments["retained-transcripts.v1.json"].Document.RootElement.GetProperty("transcripts"));
            SourceClaimContextMinimizer.ValidateInput(input);
            byte[] contextBytes = File.ReadAllBytes(Path.Combine(fullDirectory, "context-manifest.v1.json"));
            if (!contextBytes.AsSpan().SequenceEqual(SourceClaimContextMinimizer.CreateManifest(input)))
            {
                throw new InvalidDataException("Source-claim context manifest is not the exact deterministic minimizer output.");
            }

            using BoundedJsonDocumentSnapshot oracleDocument = ReadBounded(Path.Combine(fullDirectory, manifest.OracleFile));
            using BoundedJsonDocumentSnapshot provenanceDocument = ReadBounded(
                Path.Combine(fullDirectory, manifest.OracleProvenanceFile));
            SourceClaimOracle oracle = Deserialize<SourceClaimOracle>(oracleDocument.Document.RootElement);
            SourceClaimOracleProvenance provenance = Deserialize<SourceClaimOracleProvenance>(
                provenanceDocument.Document.RootElement);
            ValidateOracleClosure(manifest, input, transcripts, oracle, provenance, fullDirectory);
            return new(input.PackageId, manifest.Partition, input, transcripts, oracle, provenance);
        }
        finally
        {
            foreach (BoundedJsonDocumentSnapshot document in productDocuments.Values)
            {
                document.Dispose();
            }
        }
    }

    private static void ValidateManifest(string directory, SourceClaimFixtureManifest manifest)
    {
        string package = Path.GetFileName(directory);
        if (manifest.SchemaIdentity != "infinium.public-fixture.source-claim/1.0.0"
            || manifest.PackageIdentity != package || manifest.FixtureId != package
            || manifest.PackageVersion != "1.0.0" || manifest.FixtureVersion != "1.0.0"
            || manifest.Partition is not ("development" or "validation")
            || manifest.Status != "oracle-frozen-pre-comparison" || !manifest.AnswerFree || manifest.NetworkRequired
            || !manifest.ProductInputFiles.SequenceEqual(ProductInputs, StringComparer.Ordinal)
            || manifest.OracleFile != "oracle.v1.json"
            || manifest.OracleProvenanceFile != "oracle-provenance.v1.json")
        {
            throw new InvalidDataException("Source-claim fixture manifest identity, partition, or reference closure is invalid.");
        }
        string[] expectedIdentityPaths = ExactFiles.Where(x => x != "public-manifest.json").ToArray();
        if (!manifest.FileIdentities.Select(x => x.Path).SequenceEqual(expectedIdentityPaths, StringComparer.Ordinal)
            || manifest.FileIdentities.Any(x => x.Role is not ("product-input" or "oracle" or "oracle-provenance")))
        {
            throw new InvalidDataException("Source-claim manifest file identity closure is not exact.");
        }
        foreach (SourceClaimFileIdentity identity in manifest.FileIdentities)
        {
            string path = Path.Combine(directory, identity.Path);
            FileInfo file = new(path);
            if (file.Length != identity.Bytes || identity.Bytes <= 0 || Sha256(path) != identity.Sha256)
            {
                throw new InvalidDataException($"Source-claim manifest byte identity mismatch for {identity.Path}.");
            }
            string expectedRole = identity.Path == manifest.OracleFile ? "oracle"
                : identity.Path == manifest.OracleProvenanceFile ? "oracle-provenance" : "product-input";
            if (identity.Role != expectedRole)
            {
                throw new InvalidDataException($"Source-claim manifest role mismatch for {identity.Path}.");
            }
        }
    }

    private static void ValidateRegistryAuthority(string directory, SourceClaimFixtureManifest manifest)
    {
        string repositoryRoot = FindRepositoryRoot(directory);
        using BoundedJsonDocumentSnapshot registry = ReadBounded(
            Path.Combine(repositoryRoot, "fixtures", "public", "public-fixture-registry.v1.json"));
        string relativeDirectory = Path.GetRelativePath(repositoryRoot, directory).Replace('\\', '/');
        JsonElement[] matches = registry.Document.RootElement.GetProperty("packages").EnumerateArray()
            .Where(x => x.GetProperty("package_identity").GetString() == manifest.PackageIdentity).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException("Source-claim package is not uniquely closed by the public registry.");
        }
        JsonElement entry = matches[0];
        string manifestPath = Path.Combine(directory, "public-manifest.json");
        if (entry.GetProperty("package_version").GetString() != manifest.PackageVersion
            || entry.GetProperty("partition").GetString() != manifest.Partition
            || entry.GetProperty("package_path").GetString() != relativeDirectory
            || entry.GetProperty("authority_file").GetString() != relativeDirectory + "/public-manifest.json"
            || entry.GetProperty("authority_bytes").GetInt64() != new FileInfo(manifestPath).Length
            || entry.GetProperty("authority_sha256").GetString() != Sha256(manifestPath))
        {
            throw new InvalidDataException("Source-claim registry authority does not bind the exact manifest bytes.");
        }
    }

    private static void ValidateOracleClosure(
        SourceClaimFixtureManifest manifest, SourceClaimExecutionInput input,
        IReadOnlyList<SourceClaimRetainedTranscript> transcripts, SourceClaimOracle oracle,
        SourceClaimOracleProvenance provenance, string directory)
    {
        if (oracle.SchemaId != "infinium.evaluation.source-claim-oracle/v1" || oracle.SchemaVersion != "1"
            || provenance.SchemaId != "infinium.evaluation.source-claim-oracle-provenance/v1"
            || provenance.SchemaVersion != "1" || oracle.PackageId != manifest.PackageIdentity
            || provenance.PackageId != manifest.PackageIdentity || oracle.Partition != manifest.Partition
            || provenance.Partition != manifest.Partition || provenance.ProductOutputUsed
            || provenance.ProductImplementationUsed || provenance.PrivateOrHeldOutMaterialUsed
            || !oracle.Authority.SequenceEqual(AcceptedAuthority, StringComparer.Ordinal)
            || oracle.ForbiddenClaims.Count == 0
            || oracle.FrozenBoundaries.Count == 0 || provenance.PartitionHistory.Count == 0
            || string.IsNullOrWhiteSpace(provenance.AuthorRole) || string.IsNullOrWhiteSpace(provenance.Method)
            || string.IsNullOrWhiteSpace(provenance.ReplacementRule)
            || provenance.PartitionHistory.Any(x => string.IsNullOrWhiteSpace(x.State) || string.IsNullOrWhiteSpace(x.Reason)))
        {
            throw new InvalidDataException("Source-claim oracle or provenance closure is invalid.");
        }
        SourceClaimOracleIdentity identity = oracle.ExpectedIdentity;
        if (identity.ExecutionSchemaId != input.SchemaId
            || identity.ContextSchemaId != "infinium.llm.source-claim-context/v1"
            || identity.TranscriptSchemaId != "infinium.llm.source-claim-retained-transcripts/v1"
            || identity.SourceClaimOutputSchemaId != ContractConstants.SourceClaimExtractionSchemaId
            || identity.SchemaVersion != "1" || identity.AcquisitionRunId != input.AcquisitionRunId
            || identity.OperationId != input.OperationId
            || identity.HostAuthorizationId != input.HostAuthorizationId || identity.OwnerKind != input.OwnerKind
            || identity.OwnerId != input.OwnerId || identity.ParentAnalysisRunId != input.ParentAnalysisRunId
            || identity.ApplicationScopeId != input.ApplicationScopeId
            || identity.CostAttributionScopeId != input.CostAttributionScopeId
            || identity.SourceRevisionId != input.SourceRevisionId || identity.DeclaredPurpose != input.DeclaredPurpose
            || identity.SelectionPolicy != "exact-declared-passages-in-declared-order/v1"
            || identity.PromptId != input.PromptId
            || identity.PromptFingerprint != input.PromptFingerprint
            || !identity.PassageIds.SequenceEqual(input.Passages.Select(x => x.PassageId), StringComparer.Ordinal)
            || !identity.PassageFingerprints.SequenceEqual(input.Passages.Select(x => x.TextSha256), StringComparer.Ordinal)
            || !oracle.Scenarios.Select(x => x.TranscriptId)
                .SequenceEqual(transcripts.Select(x => x.TranscriptId), StringComparer.Ordinal))
        {
            throw new InvalidDataException("Source-claim oracle identity or scenario closure does not match product input.");
        }
        if (provenance.InputIdentities.Count != ProductInputs.Length
            || ProductInputs.Any(path => !provenance.InputIdentities.TryGetValue(path, out string? hash)
                || hash != Sha256(Path.Combine(directory, path))))
        {
            throw new InvalidDataException("Source-claim oracle provenance does not bind every exact product input.");
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
                        throw new InvalidDataException("Product-reachable source-claim input contains an answer-authority field.");
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
            case JsonValueKind.String when propertyName != "text":
                string value = element.GetString()!;
                if (ForbiddenAnswerTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException("Product-reachable source-claim input contains answer-authority data.");
                }
                break;
        }
    }

    private static T Deserialize<T>(JsonElement element) =>
        JsonSerializer.Deserialize<T>(element, SourceClaimContextMinimizer.JsonOptions)
        ?? throw new InvalidDataException($"Source-claim fixture omitted {typeof(T).Name}.");

    private static string Sha256(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string FindRepositoryRoot(string start)
    {
        DirectoryInfo? current = new(start);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static BoundedJsonDocumentSnapshot ReadBounded(string path) =>
        BoundedJsonDocumentReader.Read(path, MaximumDocumentBytes, maximumDepth: 32);
}

public sealed record SourceClaimHistoricalAuditReceipt(
    string PackageId,
    int HistoricalFileCount,
    string ManifestSha256,
    string OracleSha256,
    string RetainedTranscriptsSha256,
    bool CurrentSemanticAuthority);

public static class SourceClaimHistoricalAudit
{
    public static SourceClaimHistoricalAuditReceipt Verify(string packageRoot)
    {
        string Sha(string file) => Convert.ToHexStringLower(SHA256.HashData(
            File.ReadAllBytes(Path.Combine(packageRoot, file))));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(packageRoot, "public-manifest.json")));
        JsonElement root = manifest.RootElement;
        string packageId = root.GetProperty("package_identity").GetString()
            ?? throw new InvalidDataException("Historical source manifest has no package identity.");
        int fileCount = 0;
        foreach (JsonElement identity in root.GetProperty("file_identities").EnumerateArray())
        {
            string path = identity.GetProperty("path").GetString()!;
            byte[] bytes = File.ReadAllBytes(Path.Combine(packageRoot, path));
            if (bytes.Length != identity.GetProperty("bytes").GetInt32()
                || Convert.ToHexStringLower(SHA256.HashData(bytes)) != identity.GetProperty("sha256").GetString())
            {
                throw new InvalidDataException($"Historical source package byte drifted: {path}");
            }
            fileCount++;
        }
        return new SourceClaimHistoricalAuditReceipt(
            packageId,
            fileCount,
            Sha("public-manifest.json"),
            Sha("oracle.v1.json"),
            Sha("retained-transcripts.v1.json"),
            CurrentSemanticAuthority: false);
    }
}

[Obsolete("The frozen single-state oracle is historical evidence and cannot verify current product semantics.", error: true)]
public static class SourceClaimOracleVerifier
{
    public static void Verify(SourceClaimFixturePackage package, SourceClaimAcquisitionResult actual)
    {
        SourceClaimOracle expected = package.Oracle;
        if (actual.PromptId != expected.ExpectedIdentity.PromptId
            || actual.PromptFingerprint != expected.ExpectedIdentity.PromptFingerprint
            || actual.NetworkUsed || actual.CredentialUsed || actual.SourceRefreshUsed
            || actual.Scenarios.Count != expected.Scenarios.Count)
        {
            throw new InvalidDataException("Source-claim aggregate identity or external-effect expectation failed.");
        }
        foreach (SourceClaimScenarioOracle scenarioOracle in expected.Scenarios)
        {
            SourceClaimScenarioResult scenario = actual.Scenarios.Single(x => x.TranscriptId == scenarioOracle.TranscriptId);
            SourceClaimRetainedTranscript transcript = package.Transcripts.Single(x => x.TranscriptId == scenario.TranscriptId);
            Dictionary<string, string> actualStates = scenario.Extraction.ClaimProposals.ToDictionary(
                x => x.ProposalId.Value, x => x.ExtractionState == SemanticProposalState.Extracted ? "admitted" : "rejected",
                StringComparer.Ordinal);
            string[] admitted = scenario.Extraction.ClaimProposals
                .Where(x => x.ExtractionState == SemanticProposalState.Extracted).Select(x => x.ProposalId.Value).ToArray();
            string[] rejected = scenario.Extraction.ClaimProposals
                .Where(x => x.ExtractionState != SemanticProposalState.Extracted).Select(x => x.ProposalId.Value).ToArray();
            bool forbidden = scenario.Extraction.ClaimProposals.Any(x => x.Reason == "model-proposed-forbidden-authority");
            bool providerUsed = transcript.ModelUsed;
            int admittedCorrelations = scenario.Extraction.AdmissionCorrelations.Count(
                x => scenario.Extraction.ClaimProposals.Single(p => p.ProposalId == x.ProposalId).ExtractionState
                    == SemanticProposalState.Extracted);
            SourceClaimOracleIdentity identity = expected.ExpectedIdentity;
            if (scenario.Extraction.SchemaId != identity.SourceClaimOutputSchemaId
                || scenario.Extraction.SchemaVersion != identity.SchemaVersion
                || scenario.Extraction.AcquisitionRunId.Value != identity.AcquisitionRunId
                || scenario.Extraction.OperationId.Value != identity.OperationId
                || scenario.Extraction.OwnerKind != identity.OwnerKind || scenario.Extraction.OwnerId.Value != identity.OwnerId
                || scenario.Extraction.ParentAnalysisRunId.Value != identity.ParentAnalysisRunId
                || scenario.Extraction.ApplicationScopeId.Value != identity.ApplicationScopeId
                || scenario.Extraction.CostAttributionScopeId.Value != identity.CostAttributionScopeId
                || scenario.Extraction.SourceRevisionId.Value != identity.SourceRevisionId
                || scenario.Extraction.DeclaredPurpose != identity.DeclaredPurpose
                || !scenario.Extraction.PassageIds.Select(x => x.Value)
                    .SequenceEqual(identity.PassageIds, StringComparer.Ordinal)
                || scenario.Extraction.AdmissionCorrelations.Any(
                    x => x.AuthorizationId.Value != identity.HostAuthorizationId
                    || x.ResponseRecordId.Value != transcript.ResponseRecordId)
                || LegacyDisposition(scenario, transcript) != scenarioOracle.ExpectedResult
                || transcript.ResponseRecordId != scenarioOracle.ExpectedResponseRecordId
                || transcript.ResponseState != scenarioOracle.ExpectedResponseState
                || transcript.ResponseFingerprint != scenarioOracle.ExpectedResponseFingerprint
                || transcript.ModelUsed != scenarioOracle.ExpectedModelUsed
                || !ExpectedProposalsEqual(transcript.Proposals, scenario.Extraction.ClaimProposals,
                    scenarioOracle.ExpectedProposals)
                || !DictionaryEqual(actualStates, scenarioOracle.ExpectedProposalStates)
                || !admitted.SequenceEqual(scenarioOracle.AdmittedProposalIds, StringComparer.Ordinal)
                || !rejected.SequenceEqual(scenarioOracle.RejectedProposalIds, StringComparer.Ordinal)
                || !scenario.Extraction.ContradictionEvidenceIds.Select(x => x.Value)
                    .SequenceEqual(scenarioOracle.ContradictionEvidenceIds, StringComparer.Ordinal)
                || scenario.Extraction.Abstentions.Count != scenarioOracle.ExpectedAbstentionCount
                || scenario.Extraction.Gaps.Count != scenarioOracle.ExpectedGapCount
                || !transcript.Abstentions.SequenceEqual(scenarioOracle.ExpectedAbstentions, StringComparer.Ordinal)
                || !transcript.Gaps.SequenceEqual(scenarioOracle.ExpectedGaps, StringComparer.Ordinal)
                || !LegacyAbstentionKinds(scenario).SequenceEqual(scenarioOracle.ExpectedAbstentionKinds, StringComparer.Ordinal)
                || !LegacyGapKinds(scenario).SequenceEqual(scenarioOracle.ExpectedGapKinds, StringComparer.Ordinal)
                || admittedCorrelations != scenarioOracle.ExpectedAdmittedCorrelationCount
                || providerUsed != scenarioOracle.ProviderUsed
                || scenario.ReplayState != scenarioOracle.ReplayState
                || (scenario.ReplayState == "audit-only") != scenarioOracle.AuditOnly
                || forbidden != scenarioOracle.ForbiddenAuthorityDetected)
            {
                throw new InvalidDataException($"Source-claim scenario oracle mismatch: {scenario.TranscriptId}; "
                    + $"disposition={LegacyDisposition(scenario, transcript)}/{scenarioOracle.ExpectedResult}; "
                    + $"states={string.Join(',', actualStates.Select(item => item.Key + '=' + item.Value))}/"
                    + $"{string.Join(',', scenarioOracle.ExpectedProposalStates.Select(item => item.Key + '=' + item.Value))}; "
                    + $"abstention-kinds={string.Join(',', LegacyAbstentionKinds(scenario))}/"
                    + $"{string.Join(',', scenarioOracle.ExpectedAbstentionKinds)}; "
                    + $"gap-kinds={string.Join(',', LegacyGapKinds(scenario))}/"
                    + $"{string.Join(',', scenarioOracle.ExpectedGapKinds)}; "
                    + $"admitted-correlations={admittedCorrelations}/{scenarioOracle.ExpectedAdmittedCorrelationCount}.");
            }
        }
        int accepted = actual.Scenarios.Sum(x => x.Extraction.ClaimProposals.Count(
            p => p.ExtractionState == SemanticProposalState.Extracted));
        int rejectedCount = actual.Scenarios.Sum(x => x.Extraction.ClaimProposals.Count(
            p => p.ExtractionState != SemanticProposalState.Extracted));
        int totalAdmittedCorrelations = actual.Scenarios.Sum(x => x.Extraction.AdmissionCorrelations.Count(
            p => x.Extraction.ClaimProposals.Single(proposal => proposal.ProposalId == p.ProposalId).ExtractionState
                == SemanticProposalState.Extracted));
        int providers = package.Transcripts.Count(x => x.ModelUsed);
        int proposals = actual.Scenarios.Sum(x => x.Extraction.ClaimProposals.Count);
        int abstentions = actual.Scenarios.Sum(x => x.Extraction.Abstentions.Count);
        int gaps = actual.Scenarios.Sum(x => x.Extraction.Gaps.Count);
        SourceClaimAggregateOracle aggregate = expected.AggregateExpectations;
        string transparency = System.Text.Encoding.UTF8.GetString(SourceClaimTransparencyRenderer.RenderJson(actual))
            + SourceClaimTransparencyRenderer.RenderHuman(actual);
        if (actual.Scenarios.Count != aggregate.ScenarioCount
            || accepted != aggregate.AcceptedProposalCount || rejectedCount != aggregate.RejectedProposalCount
            || totalAdmittedCorrelations != aggregate.AdmittedCorrelationCount
            || providers != aggregate.ModelUsedScenarioCount
            || package.Transcripts.Select(x => x.OperationId).Distinct(StringComparer.Ordinal).Count()
                != aggregate.DistinctOperationIdCount
            || package.Transcripts.Count - providers != aggregate.NoModelScenarioCount
            || proposals != aggregate.ProposalCount || abstentions != aggregate.AbstentionCount
            || gaps != aggregate.GapCount
            || actual.Scenarios.Count(x => x.ReplayState == "retained-response") != aggregate.RetainedResponseScenarioCount
            || actual.Scenarios.Count(x => x.ReplayState == "audit-only") != aggregate.AuditOnlyScenarioCount
            || actual.Scenarios.Count(x => x.ReplayState == "failed-identity-drift")
                != aggregate.FailedIdentityDriftScenarioCount
            || actual.Scenarios.Count(x => x.Extraction.ClaimProposals.Any(
                p => p.Reason == "model-proposed-forbidden-authority")) != aggregate.ForbiddenAuthorityScenarioCount
            || aggregate.NetworkSendCount != 0 || aggregate.CredentialOperationCount != 0
            || expected.FrozenBoundaries.Any(x => !x.Value)
            || expected.ForbiddenClaims.Any(x => string.IsNullOrWhiteSpace(x)
                || transparency.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Source-claim aggregate, frozen-boundary, or forbidden-claim oracle mismatch.");
        }
    }

    private static bool ExpectedProposalsEqual(
        IReadOnlyList<SourceClaimTranscriptProposal> inputs,
        IReadOnlyList<CitationProposalContract> actual,
        IReadOnlyList<SourceClaimExpectedProposal> expected)
    {
        if (inputs.Count != expected.Count || actual.Count != expected.Count)
        {
            return false;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            SourceClaimTranscriptProposal input = inputs[index];
            CitationProposalContract output = actual[index];
            SourceClaimExpectedProposal item = expected[index];
            if (input.ProposalId != item.ProposalId || input.PassageId != item.PassageId || input.Claim != item.Claim
                || !input.ConditionIds.SequenceEqual(item.ConditionIds, StringComparer.Ordinal)
                || input.ClaimKind != item.ClaimKind || input.ConditionScope != item.ConditionScope
                || input.AuthorityCategory != item.AuthorityCategory
                || input.ApplicationSemantics != item.ApplicationSemantics || input.State != item.InputState
                || input.Reason != item.Reason || output.ProposalId.Value != item.ProposalId
                || output.PassageId.Value != item.PassageId || output.Claim != item.Claim
                || !output.ConditionIds.Select(x => x.Value).SequenceEqual(item.ConditionIds, StringComparer.Ordinal)
                || (output.ExtractionState == SemanticProposalState.Extracted ? "admitted" : "rejected") != item.ExpectedState)
            {
                return false;
            }
        }
        return true;
    }

    private static bool DictionaryEqual(
        Dictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count && left.All(x => right.TryGetValue(x.Key, out string? value) && value == x.Value);

    private static string[] LegacyGapKinds(SourceClaimScenarioResult scenario) =>
        scenario.GapKinds.Select(kind => kind == "unsupported-source-claim" ? "unsupported-claim" : kind).ToArray();

    private static string[] LegacyAbstentionKinds(SourceClaimScenarioResult scenario) =>
        scenario.AbstentionKinds.Where(kind => kind != "insufficient-support").ToArray();

    private static string LegacyDisposition(
        SourceClaimScenarioResult scenario, SourceClaimRetainedTranscript transcript)
    {
        if (scenario.Disposition == "abstained-unsupported")
        { return "rejected-unsupported"; }
        if (scenario.Disposition == "abstained-explicit")
        { return "rejected-explicit-abstention"; }
        if (scenario.Disposition == "extracted-contradicted-abstained")
        { return "rejected-contradiction-abstained"; }
        if (scenario.Disposition is not ("accepted-source-extraction" or "extracted-condition-unestablished"))
        { return scenario.Disposition; }
        return transcript.Proposals.Any(item => item.ApplicationSemantics == "applicability-only")
            ? "accepted-conditional-applicability"
            : transcript.Proposals.Any(item => item.ConditionScope == "version-scoped")
                ? "accepted-conditional" : "accepted";
    }
}
