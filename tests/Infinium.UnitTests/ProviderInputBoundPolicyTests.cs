using System.Reflection;
using System.Text;
using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Microsoft.ML.Tokenizers;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderInputBoundPolicyTests
{
    private static readonly int[] HelloWorldO200kIds = [24_912, 2_375];
    private static readonly ProviderFiniteLimitsContract QualificationLimits =
        new(16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
    private static readonly ProviderFiniteLimitsContract SemanticLimits =
        new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);

    [TestMethod]
    [TestCategory("Unit")]
    public void O200kOfficialRoundTripVectorUsesPinnedEncodingAndIds()
    {
        TiktokenTokenizer tokenizer = TiktokenTokenizer.CreateForEncoding("o200k_base");
        IReadOnlyList<int> ids = tokenizer.EncodeToIds("hello world", false, false);
        CollectionAssert.AreEqual(HelloWorldO200kIds, ids.ToArray());
        Assert.AreEqual("hello world", tokenizer.Decode(ids));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void LocalByteEnvelopeRetainsExactUnicodeJsonAndTokenizerEvidence()
    {
        byte[] request = Request(ProviderOperationKind.SourceClaimExtraction, 4_096,
            "line one\nCafe\u0301 and café: 👩🏽‍💻 \"quoted\"");
        ProviderInputBoundEvidence first = OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.SourceClaimExtraction, request, SemanticLimits);
        ProviderInputBoundEvidence second = OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.SourceClaimExtraction, request, SemanticLimits);

        Assert.AreEqual(OpenAiResponsesInputBoundPolicy.PolicyId, first.Proof.PolicyId);
        Assert.AreEqual(OpenAiResponsesInputBoundPolicy.PolicyVersion, first.Proof.PolicyVersion);
        Assert.AreEqual(request.Length, first.CanonicalUtf8Bytes);
        Assert.IsTrue(first.O200kTokenCount <= first.CanonicalUtf8Bytes);
        Assert.AreEqual(request.Length + 8_192, first.ConservativeInputTokenUpperBound);
        Assert.AreEqual(first.CanonicalRequestFingerprint, second.CanonicalRequestFingerprint);
        Assert.AreEqual(first.TokenIdsFingerprint, second.TokenIdsFingerprint);
        CollectionAssert.AreEqual(first.O200kTokenIds.ToArray(), second.O200kTokenIds.ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CanonicalQualificationRequestHasFrozenByteHashAndTokenGolden()
    {
        byte[] request = Request(ProviderOperationKind.TransportQualification, 256, "hello");
        ProviderInputBoundEvidence evidence = OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.TransportQualification, request, QualificationLimits);
        Assert.AreEqual(808, request.Length);
        Assert.AreEqual(224, evidence.O200kTokenCount);
        Assert.AreEqual("089219973bbfc48c3edbeceb3b007e9908132ce175bea0c2eeda1ef08218af9d", evidence.CanonicalRequestFingerprint.Value);
        Assert.AreEqual("30d27453cc37145f69f00d32f43139e3b14895b56c560bd296f1d43cb400be1c", evidence.TokenIdsFingerprint.Value);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void LocalByteEnvelopeRejectsManifestUnderstatementOfExactRequestAndConservativeInputProof()
    {
        byte[] qualification = Request(ProviderOperationKind.TransportQualification, 256, "bounded");
        ProviderInputBoundEvidence qualificationEvidence = OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.TransportQualification, qualification, QualificationLimits);
        Assert.AreEqual(qualification.Length + 4_096,
            qualificationEvidence.ConservativeInputTokenUpperBound);
        Assert.ThrowsExactly<InvalidDataException>(() => OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.TransportQualification, qualification,
            QualificationLimits with { MaximumRequestBytes = qualification.Length - 1 }));
        Assert.ThrowsExactly<InvalidDataException>(() => OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.TransportQualification, qualification,
            QualificationLimits with
            {
                MaximumInputTokens = qualificationEvidence.ConservativeInputTokenUpperBound - 1,
            }));

        foreach (ProviderOperationKind kind in new[]
        {
            ProviderOperationKind.SourceClaimExtraction,
            ProviderOperationKind.CandidateInvestigation,
        })
        {
            byte[] semantic = Request(kind, 4_096, new string('x', 48_000));
            ProviderInputBoundEvidence evidence = OpenAiResponsesInputBoundPolicy.Prove(kind, semantic, SemanticLimits);
            Assert.AreEqual(semantic.Length + 8_192, evidence.ConservativeInputTokenUpperBound);
            Assert.ThrowsExactly<InvalidDataException>(() => OpenAiResponsesInputBoundPolicy.Prove(
                kind, semantic, SemanticLimits with { MaximumRequestBytes = semantic.Length - 1 }));
            Assert.ThrowsExactly<InvalidDataException>(() => OpenAiResponsesInputBoundPolicy.Prove(
                kind, semantic, SemanticLimits with
                {
                    MaximumInputTokens = evidence.ConservativeInputTokenUpperBound - 1,
                }));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtendedProfileProofUsesDedicatedAdjustableLimitsWithoutBroadeningHistoricalProfile()
    {
        using JsonDocument schema = JsonDocument.Parse(ProviderAdapterTestData.OutputSchemaBytes);
        byte[] request = OpenAiResponsesCanonicalSerializer.SerializeExtendedProfile(new(
            ProviderOperationKind.SourceClaimExtraction, "Infinium closed provider instruction", "bounded",
            schema.RootElement.Clone(), 8_192, ProviderAdapterTestData.SafetyIdentifier));
        ProviderFiniteLimitsContract limits = new(1_000_000, 100_000, 8_192,
            1_048_576, 1, 1_000_000_000, 300_000);

        ProviderInputBoundEvidence evidence = OpenAiResponsesInputBoundPolicy.ProveExtendedProfile(
            ProviderOperationKind.SourceClaimExtraction, request, limits);
        Assert.AreEqual(request.Length + 8_192, evidence.ConservativeInputTokenUpperBound);
        OpenAiResponsesCanonicalSerializer.ValidateExtendedProfileProfile(request, 8_192);
        Assert.ThrowsExactly<InvalidOperationException>(() => OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.SourceClaimExtraction, request, limits));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            OpenAiResponsesCanonicalSerializer.ValidateExactProfile(request, 8_192));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void LocalByteEnvelopeRejectsMalformedUtf8AndEveryOutOfBandInputClass()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.TransportQualification, new byte[] { 0xff }, QualificationLimits));

        byte[] valid = Request(ProviderOperationKind.TransportQualification, 256, "hello");
        using JsonDocument document = JsonDocument.Parse(valid);
        Dictionary<string, object?> root = document.RootElement.EnumerateObject()
            .ToDictionary(x => x.Name, x => (object?)x.Value.Clone(), StringComparer.Ordinal);
        foreach ((string label, Action<Dictionary<string, object?>> mutate) in new[]
        {
            ("multi-turn", (Action<Dictionary<string, object?>>)(x => x["input"] = new[] { "first", "second" })),
            ("tools", x => x["tools"] = new[] { new { type = "web_search" } }),
            ("files", x => x["input"] = new { type = "input_file", file_id = "file-1" }),
            ("images", x => x["input"] = new { type = "input_image", image_url = "https://invalid.example" }),
            ("previous-response", x => x["previous_response_id"] = "response-1"),
        })
        {
            Dictionary<string, object?> copy = new(root, StringComparer.Ordinal);
            mutate(copy);
            byte[] adversary = JsonSerializer.SerializeToUtf8Bytes(copy);
            InvalidDataException exception = Assert.ThrowsExactly<InvalidDataException>(() =>
                OpenAiResponsesInputBoundPolicy.Prove(
                    ProviderOperationKind.TransportQualification, adversary, QualificationLimits));
            Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message), label);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void LocalByteEnvelopePinsPolicyModelEncodingVocabularyAndOfflineResource()
    {
        StringAssert.Matches(OpenAiResponsesInputBoundPolicy.PolicyIdentity,
            new System.Text.RegularExpressions.Regex("^openai-responses-o200k-byte-envelope/v2$"));
        StringAssert.Contains(OpenAiResponsesInputBoundPolicy.TokenizerPackageIdentity, "/2.0.0");
        StringAssert.Contains(OpenAiResponsesInputBoundPolicy.VocabularyPackageIdentity, "/2.0.0");
        _ = TiktokenTokenizer.CreateForEncoding(OpenAiResponsesInputBoundPolicy.EncodingName);
        Assembly vocabulary = Assembly.Load("Microsoft.ML.Tokenizers.Data.O200kBase");
        Assert.Contains("o200k_base.tiktoken.deflate",
            vocabulary.GetManifestResourceNames());

        ProviderInputBoundProofContract accepted = new(
            OpenAiResponsesInputBoundPolicy.PolicyId,
            OpenAiResponsesInputBoundPolicy.PolicyVersion,
            ProviderInputBoundProofState.Proved);
        OpenAiResponsesInputBoundPolicy.ValidateProofIdentity(accepted);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            OpenAiResponsesInputBoundPolicy.ValidateProofIdentity(accepted with { PolicyVersion = "v3" }));

        byte[] request = Request(ProviderOperationKind.TransportQualification, 256, "hello");
        string text = Encoding.UTF8.GetString(request).Replace("gpt-5.6-sol", "gpt-5.6-terra", StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(() => OpenAiResponsesInputBoundPolicy.Prove(
            ProviderOperationKind.TransportQualification, Encoding.UTF8.GetBytes(text), QualificationLimits));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void LocalByteEnvelopePinsLockHashesLicensesAndSourceProvenance()
    {
        using JsonDocument lockDocument = TestRepository.ReadJson(
            "src", "Infinium.Application", "packages.lock.json");
        JsonElement dependencies = lockDocument.RootElement.GetProperty("dependencies").GetProperty("net10.0");
        AssertLockedPackage(
            dependencies,
            "Microsoft.ML.Tokenizers",
            "2.0.0",
            OpenAiResponsesInputBoundPolicy.TokenizerPackageContentHash);
        AssertLockedPackage(
            dependencies,
            "Microsoft.ML.Tokenizers.Data.O200kBase",
            "2.0.0",
            OpenAiResponsesInputBoundPolicy.VocabularyPackageContentHash);
        AssertLockedPackage(
            dependencies,
            "Microsoft.Bcl.Memory",
            "9.0.14",
            "laQCcjTM2FnbyznkAu9hwBuQh3z2/OtJKwjGkJOUevylFpFXZIUVq/+MU2jM0HQm1n5wsjjwtLIDOiWJ6Nai3A==");

        using JsonDocument manifestDocument = TestRepository.ReadJson(
            "dependencies", "dependency-manifest.json");
        Dictionary<string, JsonElement> packages = manifestDocument.RootElement
            .GetProperty("directPackages")
            .EnumerateArray()
            .ToDictionary(package => package.GetProperty("id").GetString()!, package => package);
        foreach (string packageId in new[]
        {
            "Microsoft.Bcl.Memory",
            "Microsoft.ML.Tokenizers",
            "Microsoft.ML.Tokenizers.Data.O200kBase",
        })
        {
            Assert.AreEqual("MIT", packages[packageId].GetProperty("license").GetString(), packageId);
        }
        Assert.AreEqual(
            "efefa92f4486a43047c5b47618885a71bf7f0967",
            packages["Microsoft.ML.Tokenizers"].GetProperty("repositoryCommit").GetString());
        Assert.AreEqual(
            "efefa92f4486a43047c5b47618885a71bf7f0967",
            packages["Microsoft.ML.Tokenizers.Data.O200kBase"].GetProperty("repositoryCommit").GetString());
        Assert.AreEqual(
            "19c07820cb72aafc554c3bc8fe3c54010f5123f0",
            packages["Microsoft.Bcl.Memory"].GetProperty("repositoryCommit").GetString());
    }

    private static void AssertLockedPackage(
        JsonElement dependencies,
        string packageId,
        string expectedVersion,
        string expectedContentHash)
    {
        JsonElement package = dependencies.GetProperty(packageId);
        Assert.AreEqual("Direct", package.GetProperty("type").GetString(), packageId);
        Assert.AreEqual(expectedVersion, package.GetProperty("resolved").GetString(), packageId);
        Assert.AreEqual(expectedContentHash, package.GetProperty("contentHash").GetString(), packageId);
    }

    private static byte[] Request(ProviderOperationKind kind, long maximumOutputTokens, string input)
    {
        using JsonDocument schema = JsonDocument.Parse(ProviderAdapterTestData.OutputSchemaBytes);
        return OpenAiResponsesCanonicalSerializer.Serialize(new(
            kind,
            "Infinium closed provider instruction",
            input,
            schema.RootElement.Clone(),
            maximumOutputTokens,
            ProviderAdapterTestData.SafetyIdentifier));
    }
}
