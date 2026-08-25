using System.Text.Json;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class SourceClaimFixtureIntegrityTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void SourceClaimAnswerIsolationIsRecursiveButTreatsPassageTextAsInert()
    {
        using JsonDocument inert = JsonDocument.Parse(
            """{"passages":[{"text":"An inert passage may literally mention oracle and expected_answer."}],"safe":"data"}""");
        AnswerFreeJsonGuard.Validate(inert.RootElement);

        using JsonDocument hostileKey = JsonDocument.Parse("""{"nested":{"expected_answer":"x"}}""");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            AnswerFreeJsonGuard.Validate(hostileKey.RootElement));
        using JsonDocument hostileValue = JsonDocument.Parse("""{"nested":{"value":"oracle authority"}}""");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            AnswerFreeJsonGuard.Validate(hostileValue.RootElement));
    }
}
