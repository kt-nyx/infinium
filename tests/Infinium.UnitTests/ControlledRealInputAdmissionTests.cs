using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.ScopeReversion;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ControlledRealInputAdmissionTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void ExactAnswerFreeAllowlistIsAdmittedAndDriftIsRejected()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "infinium-controlled-input-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(testRoot, "case"));
        string inputPath = Path.Combine(testRoot, "case", "input.esp");
        byte[] bytes = Encoding.UTF8.GetBytes("synthetic controlled input");
        File.WriteAllBytes(inputPath, bytes);
        Sha256Fingerprint fingerprint = new(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        string manifestPath = Path.Combine(testRoot, "handoff.json");
        try
        {
            WriteManifest(manifestPath, testRoot, bytes.Length, fingerprint.Value);
            ControlledRealExpectedInput expected = new("CASE-1", "input.esp", bytes.Length, fingerprint,
                ControlledRealInputRole.PositivePluginOrAsset);
            ControlledRealInputAdmissionReceipt receipt = ControlledRealInputAdmission.Validate(manifestPath, [expected]);
            Assert.AreEqual("handoff-test", receipt.HandoffId);
            Assert.HasCount(1, receipt.Inputs);

            File.AppendAllText(inputPath, "drift", Encoding.UTF8);
            Assert.ThrowsExactly<InvalidDataException>(() => ControlledRealInputAdmission.Validate(manifestPath, [expected]));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void AnswerBearingAndEscapingManifestsFailBeforePayloadRead()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "infinium-controlled-input-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        string manifestPath = Path.Combine(testRoot, "handoff.json");
        try
        {
            File.WriteAllText(manifestPath,
                "{\"schema\":\"infinium-controlled-real-input-handoff/1\",\"handoff_id\":\"x\",\"root\":"
                + JsonSerializer.Serialize(testRoot) + ",\"read_only\":true,\"redistribution_allowed\":false,"
                + "\"expected_result\":\"finding\",\"inputs\":[]}");
            Assert.ThrowsExactly<InvalidDataException>(() => ControlledRealInputAdmission.Validate(manifestPath, []));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void ReparseRootIsRejectedBeforeAnAllowlistedPayloadIsOpened()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "infinium-controlled-input-reparse-" + Guid.NewGuid().ToString("N"));
        string target = Path.Combine(testRoot, "target");
        string junction = Path.Combine(testRoot, "junction");
        Directory.CreateDirectory(Path.Combine(target, "case"));
        byte[] bytes = Encoding.UTF8.GetBytes("synthetic controlled input");
        File.WriteAllBytes(Path.Combine(target, "case", "input.esp"), bytes);
        string manifestPath = Path.Combine(testRoot, "handoff.json");
        try
        {
            TestFileSystem.CreateJunctionOrInconclusive(junction, target);
            Sha256Fingerprint fingerprint = new(Convert.ToHexStringLower(SHA256.HashData(bytes)));
            WriteManifest(manifestPath, junction, bytes.Length, fingerprint.Value);
            ControlledRealExpectedInput expected = new("CASE-1", "input.esp", bytes.Length, fingerprint,
                ControlledRealInputRole.PositivePluginOrAsset);
            Assert.ThrowsExactly<InvalidDataException>(() => ControlledRealInputAdmission.Validate(manifestPath, [expected]));
        }
        finally
        {
            TestFileSystem.DeleteJunction(junction);
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static void WriteManifest(string path, string root, int length, string sha256)
    {
        string json = "{\n"
            + "  \"schema\": \"infinium-controlled-real-input-handoff/1\",\n"
            + "  \"handoff_id\": \"handoff-test\",\n"
            + "  \"root\": " + JsonSerializer.Serialize(root) + ",\n"
            + "  \"read_only\": true,\n"
            + "  \"redistribution_allowed\": false,\n"
            + "  \"inputs\": [{\"case_id\":\"CASE-1\",\"relative_path\":\"case/input.esp\",\"bytes\":"
            + length.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ",\"sha256\":\"" + sha256 + "\",\"role\":\"positive-plugin-or-asset\"}]\n} ";
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
