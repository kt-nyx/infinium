using Infinium.Application.Analysis;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class AnalysisCompositionContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void CompositionValidatesGenericStructureWhileAcceptanceFixturesRetainExactIdentity()
    {
        AnalysisCompositionEnvelope valid = AcceptedAnalysisCompositionFixtures.CreateSynthetic();
        AnalysisComposition.Validate(valid);
        Assert.AreEqual(AcceptedAnalysisCompositionFixtures.ExactSyntheticEnvelopeSha256,
            AnalysisComposition.Fingerprint(valid));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisComposition.Validate(valid with
        {
            Effects = valid.Effects.ToDictionary(
                item => item.Key,
                item => item.Key == "network" ? "used" : item.Value,
                StringComparer.Ordinal),
        }));

        ControlledAnalysisIdentity controlled = new(
            "future-controlled-handoff", new string('a', 64),
            27, 4, ["declared-to-reachable", "reachable-to-analyzed"]);
        AnalysisCompositionEnvelope futureControlled = valid with
        {
            PackageId = "future-controlled-package",
            PackageKind = "controlled-real",
            ControlledIdentity = controlled,
        };
        AnalysisComposition.Validate(futureControlled);
        Assert.AreNotEqual(AcceptedAnalysisCompositionFixtures.ExactSyntheticEnvelopeSha256,
            AnalysisComposition.Fingerprint(futureControlled));

        AnalysisCompositionEnvelope differentButValid = valid with
        {
            Artifacts = valid.Artifacts.Select(item => item with
            {
                State = item.ArtifactId == "analysis-composition-observation-supported" ? "unsupported" : item.State,
            }).ToArray(),
        };
        AnalysisComposition.Validate(differentButValid);
        Assert.AreNotEqual(AnalysisComposition.Fingerprint(valid),
            AnalysisComposition.Fingerprint(differentButValid));

        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisComposition.Validate(valid with
        {
            Artifacts = valid.Artifacts.Select((item, index) => index == 0
                ? item with { Payload = item.Payload with { Fingerprint = new string('b', 64) } }
                : item).ToArray(),
        }));
        Assert.ThrowsExactly<InvalidDataException>(() => AnalysisComposition.Validate(futureControlled with
        {
            ControlledIdentity = controlled with { InputCount = 0 },
        }));
    }
}
