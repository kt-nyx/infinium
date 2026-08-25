using Infinium.Application.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class M1Slice9ControlledCompositionIntegrationTests
{
    private static string ExactRoot => Path.Combine(
        Path.GetTempPath(),
        "infinium-s8-final-c79661c-6c369a1c04634278adcb69b5f2c2e231");

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Evaluation")]
    [TestCategory("Replay")]
    [TestCategory("Security")]
    public void ExactSlice8RetainedHandoffPreflightsAndProjectsFourControlledResultsWithoutSourceMutation()
    {
        M1Slice9CompositionEnvelope envelope = M1Slice9ControlledHandoff.LoadControlledComposition();
        Assert.AreEqual(M1Slice9Composition.ExactControlledEnvelopeSha256,
            M1Slice9Composition.Fingerprint(envelope));
        Assert.AreEqual("controlled-real", envelope.PackageKind);
        Assert.AreEqual(M1Slice9Composition.ControlledHandoffId, envelope.ControlledIdentity?.HandoffId);
        Assert.AreEqual(26, envelope.ControlledIdentity?.InputCount);
        Assert.AreEqual(3, envelope.ControlledIdentity?.PublicManifestCount);
        Assert.HasCount(4, envelope.Artifacts.Where(item => item.Collection == "candidate_decisions"));
        Assert.HasCount(4, envelope.Artifacts.Where(item => item.Collection == "hypotheses"));
        Assert.HasCount(2, envelope.Artifacts.Where(item => item.Collection == "findings"));
        Assert.HasCount(2, envelope.Artifacts.Where(item => item.Collection == "supported_cases"));
        Assert.HasCount(2, envelope.Artifacts.Where(item => item.Collection == "recommendations"));
        Assert.HasCount(14, envelope.Artifacts.Where(item => item.Collection == "coverage_gaps"));
        Assert.HasCount(1, envelope.Artifacts.Where(item => item.Collection == "model_proposals"));
        Assert.HasCount(1, envelope.Artifacts.Where(item => item.Collection == "proposal_admissions"));
        Assert.IsTrue(envelope.Artifacts.Where(item => item.Collection == "candidate_decisions")
            .Count(item => item.State == "resolved-negative") == 2);
        Assert.IsTrue(envelope.Taxonomy.All(item => item.SubjectType is "actor-cohort" or "placed-reference"));
        Assert.IsTrue(envelope.Taxonomy.All(item => item.SubjectId.Contains('.', StringComparison.Ordinal)));
        Assert.IsTrue(envelope.Effects.Values.All(value => value == "not-used"));
        Assert.IsTrue(envelope.Dependencies.Any(item => item.Kind == "controlled-identity-receipt"));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestCategory("Security")]
    public void MissingOrDriftedControlledReceiptFailsBeforeProductStateAccess()
    {
        string? previous = Environment.GetEnvironmentVariable("INFINIUM_SLICE8_RETAINED_OUTPUT_ROOT");
        string root = Path.Combine(Path.GetTempPath(), "infinium-s9-controlled-drift-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("INFINIUM_SLICE8_RETAINED_OUTPUT_ROOT", TestRepository.Root);
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice9ControlledHandoff.LoadControlledComposition());

            Environment.SetEnvironmentVariable("INFINIUM_SLICE8_RETAINED_OUTPUT_ROOT",
                Path.Combine(root, "unavailable"));
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice9ControlledHandoff.LoadControlledComposition());

            Directory.CreateDirectory(root);
            byte[] receipt = File.ReadAllBytes(Path.Combine(ExactRoot, "slice8-verification-receipt.json"));
            receipt[^2] ^= 0x01;
            File.WriteAllBytes(Path.Combine(root, "slice8-verification-receipt.json"), receipt);
            File.Copy(Path.Combine(ExactRoot, "controlled-real-results.json"),
                Path.Combine(root, "controlled-real-results.json"));
            Environment.SetEnvironmentVariable("INFINIUM_SLICE8_RETAINED_OUTPUT_ROOT", root);
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice9ControlledHandoff.LoadControlledComposition());
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "product-state")),
                "Receipt drift must reject before retained product-state access is needed.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("INFINIUM_SLICE8_RETAINED_OUTPUT_ROOT", previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
