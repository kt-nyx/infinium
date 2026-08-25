using Infinium.Application.ScopeReversion;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionSecurityTests
{
    [TestMethod]
    [TestCategory("Security")]
    [TestCategory("Safety")]
    [TestProperty("Category", "ScopeReversion")]
    public void GenericMechanismContainsNoDomainFixtureOrRealModSelectors()
    {
        string path = Path.Combine(
            ScopeReversionTestSupport.RepositoryRoot(),
            "src", "Infinium.Analysis", "ScopeReversion", "ScopeReversionAnalyzer.cs");
        string source = File.ReadAllText(path);
        string[] prohibited =
        [
            "actor", "facegen", "pkid", "refr", "xlkr", "reference", "scope-reversion-synthetic",
            "plugin-name", "fixture-id", "mod-name",
        ];
        foreach (string token in prohibited)
        {
            Assert.IsFalse(source.Contains(token, StringComparison.OrdinalIgnoreCase), token);
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestCategory("Safety")]
    [TestProperty("Category", "ScopeReversion")]
    public void ProductionCompositionUsesOnlyNotUsedExternalBoundaries()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionPipelineResult result = ScopeReversionComposition.Execute(fixture.Request);
        Assert.IsTrue(result.Assignment.Boundaries.All(item => item.State == BoundaryUseState.NotUsed));
        Assert.IsTrue(result.Analysis.Boundaries.All(item => item.State == BoundaryUseState.NotUsed));
        Assert.IsTrue(result.Assignment.Analyzer.CanonicalDeclarationJson.Contains(
            "\"mode\":\"local-only\"", StringComparison.Ordinal));
        Assert.IsFalse(result.Assignment.Analyzer.CanonicalDeclarationJson.Contains(
            "scope-reversion-synthetic-bounded-cases", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestCategory("Safety")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void V2GenericDecisionCodeContainsNoControlledRealSelectorsAndAllProhibitedBoundariesRemainNotUsed()
    {
        string root = ScopeReversionTestSupport.RepositoryRoot();
        string[] productFiles =
        [
            Path.Combine(root, "src", "Infinium.Analysis", "ScopeReversion", "ScopeReversionV2Analyzer.cs"),
            Path.Combine(root, "src", "Infinium.Application", "ScopeReversion", "ControlledRealScopeReversionProjector.cs"),
        ];
        string source = string.Join('\n', productFiles.Select(File.ReadAllText));
        string[] prohibited =
        [
            "AI Overhaul", "Children of the Pariah", "Candlehearth", "Nightgate",
            "0001339A", "0001AA63", "00017061", "REAL-NPC", "REAL-REFR", "CotP",
        ];
        foreach (string token in prohibited)
        {
            Assert.IsFalse(source.Contains(token, StringComparison.OrdinalIgnoreCase), token);
        }

        ScopeReversionV2PipelineResult result = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request());
        Assert.HasCount(11, result.Analysis.Boundaries);
        CollectionAssert.Contains(result.Analysis.Boundaries.Select(item => item.BoundaryId).ToArray(), "loot");
        Assert.IsTrue(result.Analysis.Boundaries.All(item => item.State == BoundaryUseState.NotUsed));
        Assert.IsTrue(result.CanonicalJson.AsSpan().IndexOf(
            System.Text.Encoding.UTF8.GetBytes(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))) < 0);
    }
}
