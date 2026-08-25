using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Analysis;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class M1Slice9CompositionContractTests
{
    private const string IndexSchema = "m1-required-case-result-index.v1.schema.json";

    [TestMethod]
    [TestCategory("Contract")]
    public void PreregisteredRequiredCaseIndexIsClosedAndContainsTheExactOrderedBaseline()
    {
        string path = Path.Combine(TestRepository.Root, "docs", "plans", "milestones", "m1", "slices", "s9",
            "evidence", "required-case-results.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        ValidateIndex(document.RootElement);
        string[] ids = document.RootElement.GetProperty("rows").EnumerateArray()
            .Select(item => item.GetProperty("case_id").GetString()!).ToArray();
        Assert.HasCount(34, ids);
        Assert.HasCount(34, ids.Distinct(StringComparer.Ordinal));

        JsonObject unknown = JsonNode.Parse(bytes)!.AsObject();
        unknown["rows"]!.AsArray()[0]!["case_id"] = "EVAL-9999";
        using JsonDocument unknownDocument = JsonDocument.Parse(unknown.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ValidateIndex(unknownDocument.RootElement));

        JsonObject duplicate = JsonNode.Parse(bytes)!.AsObject();
        duplicate["rows"]!.AsArray()[1]!["case_id"] = "EVAL-0001";
        using JsonDocument duplicateDocument = JsonDocument.Parse(duplicate.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ValidateIndex(duplicateDocument.RootElement));

        JsonObject final = JsonNode.Parse(bytes)!.AsObject();
        final["mode"] = "final";
        foreach (JsonNode? node in final["rows"]!.AsArray())
        {
            JsonObject row = node!.AsObject();
            row["evidence_class"] = "final-execution";
            row["command"] = "final command";
            row["matched"] = 1;
            row["passed"] = 1;
            row["receipt_path"] = "completion-receipt.json";
            row["receipt_byte_length"] = 1;
            row["receipt_sha256"] = new string('a', 64);
            row["disposition"] = "passed";
            row["skip_explanation"] = null;
            row["reviewer"] = "reviewer";
            row["review_disposition"] = "accepted";
        }
        using JsonDocument finalDocument = JsonDocument.Parse(final.ToJsonString());
        ValidateIndex(finalDocument.RootElement);
        final["rows"]!.AsArray()[9]!["disposition"] = "not-run";
        using JsonDocument incompleteFinal = JsonDocument.Parse(final.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateIndex(incompleteFinal.RootElement));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CompositionRejectsEffectAndControlledHandoffIdentityDrift()
    {
        M1Slice9CompositionEnvelope valid = M1Slice9SyntheticComposition.Create();
        M1Slice9Composition.Validate(valid);
        Assert.AreEqual(M1Slice9Composition.ExactSyntheticEnvelopeSha256,
            M1Slice9Composition.Fingerprint(valid));
        Assert.ThrowsExactly<InvalidDataException>(() => M1Slice9Composition.Validate(valid with
        {
            Effects = valid.Effects.ToDictionary(
                item => item.Key,
                item => item.Key == "network" ? "used" : item.Value,
                StringComparer.Ordinal),
        }));

        M1Slice9ControlledIdentity controlled = new(
            M1Slice9Composition.ControlledHandoffId, M1Slice9Composition.ControlledManifestSha256,
            26, 3, ["declared-to-reachable", "reachable-to-analyzed"]);
        Assert.ThrowsExactly<InvalidDataException>(() => M1Slice9Composition.Validate(valid with
        {
            PackageKind = "controlled-real",
            ControlledIdentity = controlled with { InputCount = 27 },
        }));
        Assert.ThrowsExactly<InvalidDataException>(() => M1Slice9Composition.Validate(valid with
        {
            Artifacts = valid.Artifacts.Select(item => item with
            {
                State = item.ArtifactId == "m1-s9-observation-supported" ? "unsupported" : item.State,
            }).ToArray(),
        }));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void SyntheticPackageAndRetainedProviderDependenciesHaveExactActivatedBytes()
    {
        Dictionary<string, (long Bytes, string Sha)> expected = new(StringComparer.Ordinal)
        {
            ["fixtures/public/cross-stage/m1-slice9/M1-S9-SYNTHETIC-v1/manifest.v1.json"] =
                (1775, "b14a50bf341d467c5922c7a9be200a5f61ef974c4dfb95d656c5701ee2220ac6"),
            ["artifacts/m1-slice6/successor-campaign/composed-evidence.v2.json"] =
                (8653, "901f278825d3fdbab2971b9f6bb4462f84c12dea96f1c14c8f222d1f29a1df9d"),
            ["artifacts/m1-slice6/successor-campaign/wp9-attempt-11-development-2827013/attempt-evidence.v3.json"] =
                (6292, "6f51bc6d28799711e7d62d5e67ef7965d2be6d72d9c3453ec16f7e9cfbbc1270"),
            ["artifacts/m1-slice6/successor-campaign/wp10-attempt-2-development-c4f6aa8/attempt-evidence.v3.json"] =
                (6312, "5aa6391aba25fbdfcd5470e3dc0db9c8a108b392f14e3c024bf883d214d4b0af"),
            ["artifacts/m1-slice6/successor-campaign/wp11-attempt-1-development-439ccda/attempt-evidence.v3.json"] =
                (6313, "eaddaac9644359c1fe45bd0b726574b037f38e2449db50f51757ce03190d16ca"),
        };
        foreach ((string relative, (long bytes, string sha)) in expected)
        {
            string path = Path.Combine(TestRepository.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            byte[] actual = File.ReadAllBytes(path);
            Assert.AreEqual(bytes, actual.LongLength, relative);
            Assert.AreEqual(sha, Convert.ToHexStringLower(SHA256.HashData(actual)), relative);
        }
    }

    private static void ValidateIndex(JsonElement root)
    {
        ActiveJsonSchemaValidator.Validate(root, IndexSchema);
        string[] expected =
        [
            "EVAL-0001", "EVAL-0002", "EVAL-0016", "EVAL-0017", "EVAL-0026", "EVAL-0032",
            "EVAL-0037", "EVAL-0038", "EVAL-0033", "EVAL-0034", "EVAL-0035", "EVAL-0039",
            "EVAL-0040", "EVAL-0045", "EVAL-0046", "EVAL-0051", "EVAL-0052", "EVAL-0054",
            "EVAL-0064", "EVAL-0065", "EVAL-0067", "EVAL-0079", "EVAL-0076", "EVAL-0077",
            "EVAL-0080", "EVAL-0081", "EVAL-0082", "EVAL-0083", "EVAL-0084", "EVAL-0085",
            "EVAL-0086", "EVAL-0087", "EVAL-0088", "EVAL-0089",
        ];
        string[] actual = root.GetProperty("rows").EnumerateArray()
            .Select(item => item.GetProperty("case_id").GetString()!).ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The M1 required-case index is missing, duplicated, unknown, or out of canonical order.");
        }
    }
}
