using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.CredentialHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialNativeAuthorizationTests
{
    private static readonly string[] AllowedCalls = ["CredWriteW", "CredReadW", "CredDeleteW", "CredFree"];
    private static readonly string[] ExactImports =
    [
        "advapi32.dll!CredDeleteW",
        "advapi32.dll!CredFree",
        "advapi32.dll!CredReadW",
        "advapi32.dll!CredWriteW",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeManifestIsExactFiniteAndTargetBoundWithoutNativeEffect()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string path = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreEqual(WindowsCredentialNativeQualification.AcceptedManifestSha256,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));

        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement rootElement = document.RootElement;
        Assert.AreEqual(WindowsCredentialNativeQualification.AcceptedManifestId,
            rootElement.GetProperty("manifest_id").GetString());
        CollectionAssert.AreEqual(
            AllowedCalls,
            rootElement.GetProperty("native_boundary").GetProperty("allowed_calls")
                .EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual(0, rootElement.GetProperty("provider_boundary").GetProperty("network_operations").GetInt32());
        Assert.AreEqual(0, rootElement.GetProperty("provider_boundary").GetProperty("provider_operations").GetInt32());
        Assert.AreEqual(10, rootElement.GetProperty("required_scenarios").GetArrayLength());

        JsonElement targets = rootElement.GetProperty("disposable_namespace").GetProperty("targets");
        Assert.AreEqual(12, targets.GetArrayLength());
        HashSet<string> fingerprints = new(StringComparer.Ordinal);
        foreach (JsonElement target in targets.EnumerateArray())
        {
            string raw = $"Infinium:{target.GetProperty("access_profile_id").GetString()}:{target.GetProperty("generation_id").GetString()}";
            string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            Assert.AreEqual(expected, target.GetProperty("target_fingerprint_sha256").GetString());
            Assert.IsTrue(fingerprints.Add(expected));
            Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains(raw, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeInteropImportsOnlyExactReviewedAdvapiCalls()
    {
        string[] imports = typeof(WindowsCredentialManagerStore)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Select(method => (Method: method, Import: method.GetCustomAttribute<DllImportAttribute>()))
            .Where(item => item.Import is not null)
            .Select(item => $"{item.Import!.Value}!{item.Method.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(ExactImports, imports);
        Assert.IsFalse(imports.Any(value => value.Contains("Enumerate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeOversizeAndWrongTargetFailBeforeAnyNativeCall()
    {
        WindowsCredentialManagerStore store = new();
        NativeTarget target = new("test", "profile", "g001", new string('0', 64));
        Assert.ThrowsExactly<InvalidDataException>(() => store.WriteExact(target, new byte[2_561]));
        Assert.AreEqual(0, store.CallCounts.Total);

        string raw = "Infinium:profile:g001";
        target = target with
        {
            TargetFingerprintSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw))),
        };
        Assert.ThrowsExactly<InvalidDataException>(() => store.WriteExact(target, new byte[2_561]));
        Assert.AreEqual(0, store.CallCounts.Total);
    }
}
