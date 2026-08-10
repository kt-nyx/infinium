using System.Text.Json;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaMutagenFixtureConformanceTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void NpcRaceControlsAreReadableThroughThePinnedSemanticApi()
    {
        using ParsedFixture fixture = ParseAcceptedPlugins("BETH-NPC-DEV");
        IRaceGetter[] races = fixture.Mods
            .SelectMany(mod => mod.EnumerateMajorRecords<IRaceGetter>())
            .ToArray();

        Assert.AreEqual(2, races.Length);
        Assert.AreEqual(
            1,
            races.Count(race => race.Flags.HasFlag(Race.Flag.FaceGenHead)));
        Assert.AreEqual(
            1,
            races.Count(race => !race.Flags.HasFlag(Race.Flag.FaceGenHead)));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void RefrControlsAreReadableThroughCanonicalCellChildren()
    {
        using ParsedFixture fixture = ParseAcceptedPlugins("BETH-REFR-DEV");
        IPlacedObjectGetter[] references = fixture.Mods
            .SelectMany(mod => mod.EnumerateMajorRecords<IPlacedObjectGetter>())
            .ToArray();

        Assert.AreEqual(7, references.Length);
        Assert.IsTrue(references.Any(reference => !reference.Base.IsNull));
        Assert.IsTrue(references.Any(reference => reference.LinkedReferences.Count > 0));
        Assert.IsTrue(references.Any(reference => !reference.LocationReference.IsNull));
        Assert.IsTrue(references.Any(reference => !reference.Owner.IsNull));
        Assert.IsTrue(references.Any(reference => reference.Placement is not null));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void LightPluginReferenceControlsAlsoUseCanonicalCellChildren()
    {
        using ParsedFixture fixture = ParseAcceptedPlugins("BETH-LIGHT-VAL");
        IPlacedObjectGetter[] references = fixture.Mods
            .SelectMany(mod => mod.EnumerateMajorRecords<IPlacedObjectGetter>())
            .ToArray();

        Assert.AreEqual(2, references.Length);
        Assert.IsTrue(references.All(reference => !reference.Base.IsNull));
    }

    private static ParsedFixture ParseAcceptedPlugins(string fixtureId)
    {
        string fixtureRoot = TestRepository.PathFromRoot(
            "fixtures",
            "public",
            "bethesda",
            fixtureId);
        using JsonDocument snapshot = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(fixtureRoot, "inputs", "snapshot", "accepted-order.json")));
        string[] artifactIds = snapshot.RootElement
            .GetProperty("plugin_order")
            .EnumerateArray()
            .Select(plugin => plugin.GetProperty("artifact_id").GetString()!)
            .ToArray();
        string[] paths = artifactIds
            .Select(artifactId => Path.Combine(
                [fixtureRoot, .. artifactId.Split('/')]))
            .ToArray();
        KeyedMasterStyle[] masterStyles = paths
            .Select(path => KeyedMasterStyle.FromPath(
                new ModPath(ModKey.FromFileName(Path.GetFileName(path)), path),
                GameRelease.SkyrimSE))
            .ToArray();

        ISkyrimModDisposableGetter[] mods = paths
            .Zip(masterStyles)
            .Select(pair => SkyrimMod.Create(SkyrimRelease.SkyrimSE)
                .FromStreamFactory(
                    () => new MemoryStream(File.ReadAllBytes(pair.First), writable: false),
                    pair.Second.ModKey)
                .WithLoadOrder(masterStyles)
                .WithNoDataFolder()
                .SingleThread()
                .ThrowIfUnknownSubrecord(true)
                .Construct())
            .ToArray();
        return new ParsedFixture(mods);
    }

    private sealed class ParsedFixture(ISkyrimModDisposableGetter[] mods) : IDisposable
    {
        public IReadOnlyList<ISkyrimModDisposableGetter> Mods { get; } = mods;

        public void Dispose()
        {
            foreach (ISkyrimModDisposableGetter mod in Mods)
            {
                mod.Dispose();
            }
        }
    }
}
