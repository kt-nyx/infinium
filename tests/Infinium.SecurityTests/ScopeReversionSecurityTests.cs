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
            "actor", "facegen", "pkid", "refr", "xlkr", "reference", "m1-s7-synthetic",
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
            "M1-S7-SYNTHETIC", StringComparison.Ordinal));
    }
}
