using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinium.BethesdaFixtures.Generator;

internal static class Program
{
    private const ulong DefaultSeed = 3_520_260_730;
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static int Main(string[] args)
    {
        try
        {
            var command = args.Length == 0 ? "generate" : args[0];
            var seed = ReadSeed(args);

            return command switch
            {
                "generate" => Generate(ReadOutput(args), seed),
                "verify" => Verify(seed),
                _ => throw new ArgumentException(
                    "Usage: generate [--output <directory>] [--seed <uint64>] | verify [--seed <uint64>]"),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int Generate(string output, ulong seed)
    {
        FixtureGenerator.GenerateAll(Path.GetFullPath(output), seed);
        Console.WriteLine($"Generated deterministic Bethesda fixture inputs at {Path.GetFullPath(output)}");
        return 0;
    }

    private static int Verify(ulong seed)
    {
        var root = Path.Combine(Path.GetTempPath(), $"infinium-bethesda-fixtures-{Guid.NewGuid():N}");
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");

        try
        {
            FixtureGenerator.GenerateAll(first, seed);
            FixtureGenerator.GenerateAll(second, seed);
            VerifyTreesMatch(first, second);
            VerifyConstructionCoverage(first);
            VerifyConstructionCoverage(second);
            FixtureGenerator.VerifyControlledMutationInvariants(first);
            FixtureGenerator.VerifyControlledMutationInvariants(second);
            Console.WriteLine($"Verified two clean byte-identical runs for seed {seed.ToString(CultureInfo.InvariantCulture)}.");
            Console.WriteLine("Verified complete construction-region coverage for every emitted byte.");
            Console.WriteLine("Verified controlled-mutation byte-diff, record-order, and origin-basename invariants.");
            return 0;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void VerifyTreesMatch(string first, string second)
    {
        var firstFiles = Directory.GetFiles(first, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(first, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var secondFiles = Directory.GetFiles(second, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(second, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!firstFiles.SequenceEqual(secondFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Clean runs emitted different path sets.");
        }

        foreach (var relativePath in firstFiles)
        {
            var firstBytes = File.ReadAllBytes(Path.Combine(first, relativePath));
            var secondBytes = File.ReadAllBytes(Path.Combine(second, relativePath));
            if (!firstBytes.AsSpan().SequenceEqual(secondBytes))
            {
                throw new InvalidDataException($"Clean runs differ at {relativePath}.");
            }
        }
    }

    private static void VerifyConstructionCoverage(string root)
    {
        foreach (var package in FixtureGenerator.PackageIds)
        {
            var inputs = Path.Combine(root, package, "inputs");
            var manifestPath = Path.Combine(inputs, "construction-manifest.json");
            var manifest = JsonSerializer.Deserialize<ConstructionManifest>(
                File.ReadAllBytes(manifestPath),
                JsonOptions) ?? throw new InvalidDataException($"Unreadable construction manifest for {package}.");
            var filesByPath = manifest.Files.ToDictionary(entry => entry.Path, StringComparer.Ordinal);

            foreach (var path in Directory.GetFiles(inputs, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(inputs, path).Replace('\\', '/');
                if (!filesByPath.TryGetValue(relativePath, out var entry))
                {
                    throw new InvalidDataException($"{package}/{relativePath} has no construction entry.");
                }

                var bytes = File.ReadAllBytes(path);
                if (entry.ByteLength != bytes.Length)
                {
                    throw new InvalidDataException($"{package}/{relativePath} length does not match construction entry.");
                }

                var cursor = 0L;
                foreach (var region in entry.Regions.OrderBy(region => region.Offset))
                {
                    if (region.Offset != cursor || region.Length <= 0)
                    {
                        throw new InvalidDataException($"{package}/{relativePath} has a construction-region gap or overlap.");
                    }

                    cursor += region.Length;
                }

                if (cursor != bytes.Length)
                {
                    throw new InvalidDataException($"{package}/{relativePath} is not fully covered.");
                }

                if (entry.Sha256 is not null
                    && !StringComparer.Ordinal.Equals(entry.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes))))
                {
                    throw new InvalidDataException($"{package}/{relativePath} hash does not match construction entry.");
                }
            }
        }
    }

    private static ulong ReadSeed(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(args[index], "--seed"))
            {
                return ulong.Parse(args[index + 1], CultureInfo.InvariantCulture);
            }
        }

        return DefaultSeed;
    }

    private static string ReadOutput(string[] args)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(args[index], "--output"))
            {
                return args[index + 1];
            }
        }

        return Path.Combine("test-data", "evaluation", "m1-semantic");
    }
}

internal static class FixtureGenerator
{
    public static readonly string[] PackageIds =
    [
        "BETH-NPC-DEV",
        "BETH-REFR-DEV",
        "BETH-LIGHT-VAL",
        "BETH-MALFORMED-VAL",
        "BETH-UNSUPPORTED-VAL",
    ];

    private const uint CompressedRecord = 0x0004_0000;
    private const uint DeletedRecord = 0x0000_0020;
    private const uint LightPlugin = 0x0000_0200;
    private const uint FaceGenHead = 0x0000_0002;
    private const uint FixtureCellLocalId = 0x00000900;

    public static void GenerateAll(string root, ulong seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var packages = new Dictionary<string, PackageOutput>(StringComparer.Ordinal)
        {
            ["BETH-NPC-DEV"] = BuildNpc(seed),
            ["BETH-REFR-DEV"] = BuildRefr(seed),
            ["BETH-LIGHT-VAL"] = BuildLight(seed),
            ["BETH-MALFORMED-VAL"] = BuildMalformed(seed),
            ["BETH-UNSUPPORTED-VAL"] = BuildUnsupported(seed),
        };

        foreach (var (packageId, package) in packages)
        {
            WritePackage(Path.Combine(root, packageId, "inputs"), packageId, package, seed);
        }
    }

    public static void VerifyControlledMutationInvariants(string root)
    {
        var npc = Path.Combine(root, "BETH-NPC-DEV", "inputs");
        AssertOneByteDifference(
            Path.Combine(npc, "plugins", "02-Behavior.esp"),
            Path.Combine(npc, "mutations", "one-byte-aidt", "02-Behavior.esp"),
            "AIDT",
            7,
            isRecordHeader: false);
        AssertOneByteDifference(
            Path.Combine(npc, "plugins", "01-Actors.esm"),
            Path.Combine(npc, "mutations", "local-id", "01-Actors.esm"),
            "NPC_",
            12,
            isRecordHeader: true);
        AssertRecordOrderOnly(
            Path.Combine(npc, "plugins", "01-Actors.esm"),
            Path.Combine(npc, "mutations", "record-order", "01-Actors.esm"));

        var refr = Path.Combine(root, "BETH-REFR-DEV", "inputs");
        AssertOneByteDifference(
            Path.Combine(refr, "plugins", "03-Placement.esp"),
            Path.Combine(refr, "mutations", "one-byte-data", "03-Placement.esp"),
            "DATA",
            23,
            isRecordHeader: false);
        AssertRecordOrderOnly(
            Path.Combine(refr, "plugins", "01-World.esm"),
            Path.Combine(refr, "mutations", "record-order", "01-World.esm"));
    }

    private static PackageOutput BuildNpc(ulong seed)
    {
        var output = new PackageOutput();
        const uint npcLocal = 0x00000800;
        const uint racePositive = 0x00000810;
        const uint raceNegative = 0x00000811;
        const uint classLocal = 0x00000820;
        const uint packageA = 0x00000830;
        const uint packageB = 0x00000831;
        const uint colorLocal = 0x00000840;
        const uint templateLocal = 0x00000850;
        var actorsRecords = new[]
        {
            Record("RACE", FormId(1, racePositive), Sub("EDID", Z("FixtureRaceFaceGen")), Sub("DATA", RaceData(FaceGenHead))),
            Record("RACE", FormId(1, raceNegative), Sub("EDID", Z("FixtureRaceNoFaceGen")), Sub("DATA", RaceData(0))),
            Record("CLAS", FormId(1, classLocal), Sub("EDID", Z("FixtureClass"))),
            Record("PACK", FormId(1, packageA), Sub("EDID", Z("FixturePackageA"))),
            Record("PACK", FormId(1, packageB), Sub("EDID", Z("FixturePackageB"))),
            Record("CLFM", FormId(1, colorLocal), Sub("EDID", Z("FixtureHairColor"))),
            Record("NPC_", FormId(1, templateLocal),
                Sub("EDID", Z("FixtureTemplate")),
                Sub("ACBS", Acbs(0x0000_0010)),
                Sub("RNAM", U32(FormId(1, racePositive)))),
            Record("NPC_", FormId(1, npcLocal),
                Sub("EDID", Z("FixtureNpc")),
                Sub("ACBS", Acbs(0)),
                Sub("RNAM", U32(FormId(1, racePositive))),
                Sub("AIDT", Pattern(20, seed, 0x11)),
                Sub("PKID", U32(FormId(1, packageA))),
                Sub("PKID", U32(FormId(1, packageB))),
                Sub("PNAM", U32(FormId(1, colorLocal))),
                Sub("PNAM", U32(FormId(1, colorLocal))),
                Sub("HCLF", U32(FormId(1, colorLocal)))),
        };
        var actorsPlugin = Plugin("01-Actors.esm", ["00-Pad.esm"], actorsRecords);
        var behaviorPlugin = Plugin(
            "02-Behavior.esp",
            ["00-Pad.esm", "01-Actors.esm"],
            [
                Record("NPC_", FormId(1, npcLocal),
                    Sub("EDID", Z("FixtureNpc")),
                    Sub("ACBS", Acbs(0x0000_1000)),
                    Sub("RNAM", U32(FormId(1, racePositive))),
                    Sub("AIDT", Pattern(20, seed, 0x22)),
                    Sub("PKID", U32(FormId(1, packageB))),
                    Sub("PKID", U32(FormId(1, packageA)))),
            ]);

        output.Plugin("plugins/00-Pad.esm", Plugin("00-Pad.esm", [], []));
        output.Plugin("plugins/01-Actors.esm", actorsPlugin);
        output.Plugin("plugins/02-Behavior.esp", behaviorPlugin);

        var appearanceBody = Subs(
            Sub("EDID", Z("FixtureNpc")),
            Sub("ACBS", Acbs(0x0004_0000, 0x0001)),
            Sub("RNAM", U32(FormId(1, raceNegative))),
            Sub("HCLF", U32(FormId(1, colorLocal))),
            Sub("PNAM", U32(FormId(1, colorLocal))),
            Sub("TPLT", U32(FormId(1, templateLocal))));
        output.Plugin("plugins/03-Appearance.esp", Plugin(
            "03-Appearance.esp",
            ["00-Pad.esm", "01-Actors.esm", "02-Behavior.esp"],
            [RecordRaw("NPC_", FormId(1, npcLocal), appearanceBody, CompressedRecord)]));

        output.Plugin("plugins/04-LightActors.esl", Plugin(
            "04-LightActors.esl",
            ["01-Actors.esm"],
            [
                Record("NPC_", FormId(1, 0x00000800),
                    Sub("EDID", Z("LightFixtureNpc")),
                    Sub("ACBS", Acbs(0)),
                    Sub("RNAM", U32(FormId(0, racePositive)))),
            ],
            LightPlugin));
        output.Plugin("plugins/05-LightWinner.esp", Plugin(
            "05-LightWinner.esp",
            ["01-Actors.esm", "04-LightActors.esl"],
            [
                Record("NPC_", FormId(1, npcLocal),
                    Sub("EDID", Z("FixtureNpc")),
                    Sub("ACBS", Acbs(0x0002_0000)),
                    Sub("RNAM", U32(FormId(1, racePositive))),
                    Sub("HCLF", U32(0))),
            ]));
        output.Plugin("plugins/06-DeletedWinner.esp", Plugin(
            "06-DeletedWinner.esp",
            ["01-Actors.esm"],
            [Record("NPC_", FormId(0, npcLocal), DeletedRecord, Sub("EDID", Z("FixtureNpc")))]));

        output.Plugin("mutations/one-byte-aidt/02-Behavior.esp", MutateSubrecordByte(behaviorPlugin, "AIDT", 7, 0x01));
        output.Plugin("mutations/master-order-reindexed/03-Appearance.esp", Plugin(
            "03-Appearance.esp",
            ["02-Behavior.esp", "01-Actors.esm", "00-Pad.esm"],
            [
                Record("NPC_", FormId(1, npcLocal),
                    Sub("EDID", Z("FixtureNpc")),
                    Sub("ACBS", Acbs(0, 0x0001)),
                    Sub("RNAM", U32(FormId(1, racePositive))),
                    Sub("TPLT", U32(FormId(1, templateLocal)))),
            ]));
        output.Plugin("mutations/master-order-unreindexed/03-Appearance.esp", Plugin(
            "03-Appearance.esp",
            ["02-Behavior.esp", "01-Actors.esm", "00-Pad.esm"],
            [
                Record("NPC_", FormId(1, npcLocal),
                    Sub("EDID", Z("FixtureNpc")),
                    Sub("ACBS", Acbs(0, 0x0001)),
                    Sub("RNAM", U32(FormId(2, racePositive))),
                    Sub("TPLT", U32(FormId(2, templateLocal)))),
            ]));
        output.Plugin("mutations/record-order/01-Actors.esm", Plugin(
            "01-Actors.esm",
            ["00-Pad.esm"],
            [
                actorsRecords[7],
                actorsRecords[6],
                actorsRecords[5],
                actorsRecords[4],
                actorsRecords[3],
                actorsRecords[2],
                actorsRecords[1],
                actorsRecords[0],
            ]));
        output.Plugin("mutations/uncompressed/03-Appearance.esp", Plugin(
            "03-Appearance.esp",
            ["00-Pad.esm", "01-Actors.esm", "02-Behavior.esp"],
            [RecordRaw("NPC_", FormId(1, npcLocal), appearanceBody, 0)]));
        output.Plugin("mutations/Behavior-RepeatedPKIDOrder.esp", Plugin(
            "Behavior-RepeatedPKIDOrder.esp",
            ["01-Actors.esm"],
            [
                Record("NPC_", FormId(0, npcLocal),
                    Sub("EDID", Z("FixtureNpc")),
                    Sub("ACBS", Acbs(0)),
                    Sub("PKID", U32(FormId(0, packageB))),
                    Sub("PKID", U32(FormId(0, packageA)))),
            ]));
        output.Plugin("mutations/Appearance-RepeatedPNAMOrder.esp", Plugin(
            "Appearance-RepeatedPNAMOrder.esp",
            ["01-Actors.esm"],
            [
                Record("NPC_", FormId(0, npcLocal),
                    Sub("EDID", Z("FixtureNpc")),
                    Sub("ACBS", Acbs(0)),
                    Sub("PNAM", U32(FormId(0, colorLocal))),
                    Sub("PNAM", U32(0)),
                    Sub("PNAM", U32(FormId(0, colorLocal)))),
            ]));
        output.Plugin("mutations/local-id/01-Actors.esm", MutateLastRecordFormId(actorsPlugin, "NPC_", FormId(1, 0x00000860)));

        output.CaseMatrix("BETH-NPC-DEV",
        [
            Case("npc-layered-winner", "scan", ["plugins/00-Pad.esm", "plugins/01-Actors.esm", "plugins/02-Behavior.esp", "plugins/03-Appearance.esp"]),
            Case("npc-deleted-winner", "scan", ["plugins/01-Actors.esm", "plugins/06-DeletedWinner.esp"]),
            Case("npc-light-plugin", "scan", ["plugins/01-Actors.esm", "plugins/04-LightActors.esl", "plugins/05-LightWinner.esp"]),
            Case("npc-compression-equivalence", "compare", ["plugins/03-Appearance.esp", "mutations/uncompressed/03-Appearance.esp"]),
            Case("npc-one-byte-field-change", "compare", ["plugins/02-Behavior.esp", "mutations/one-byte-aidt/02-Behavior.esp"]),
            Case("npc-master-reindexing", "compare", ["mutations/master-order-reindexed/03-Appearance.esp", "mutations/master-order-unreindexed/03-Appearance.esp"]),
            Case("npc-record-order", "compare", ["plugins/01-Actors.esm", "mutations/record-order/01-Actors.esm"]),
            Case("npc-repeated-pkid-order", "scan", ["mutations/Behavior-RepeatedPKIDOrder.esp"]),
            Case("npc-repeated-pnam-order", "scan", ["mutations/Appearance-RepeatedPNAMOrder.esp"]),
            Case("npc-local-id-change", "compare", ["plugins/01-Actors.esm", "mutations/local-id/01-Actors.esm"]),
        ]);
        return output;
    }

    private static PackageOutput BuildRefr(ulong seed)
    {
        var output = new PackageOutput();
        const uint baseLocal = 0x00000800;
        const uint ownerLocal = 0x00000810;
        const uint locationLocal = 0x00000820;
        const uint keywordLocal = 0x00000830;
        const uint referenceLocal = 0x00000840;
        var worldRecords = new[]
        {
            Record("STAT", FormId(1, baseLocal), Sub("EDID", Z("FixtureBase"))),
            Record("FACT", FormId(1, ownerLocal), Sub("EDID", Z("FixtureOwner"))),
            Record("LCTN", FormId(1, locationLocal), Sub("EDID", Z("FixtureLocation"))),
            Record("KYWD", FormId(1, keywordLocal), Sub("EDID", Z("FixtureKeyword"))),
            Record("REFR", FormId(1, referenceLocal),
                Sub("EDID", Z("FixtureReference")),
                Sub("NAME", U32(FormId(1, baseLocal))),
                Sub("XLKR", Pair(FormId(1, keywordLocal), FormId(1, referenceLocal))),
                Sub("XLRL", U32(FormId(1, locationLocal))),
                Sub("XOWN", U32(FormId(1, ownerLocal))),
                Sub("DATA", Placement(1, 2, 3, 0, 0.5f, 1))),
        };
        var worldPlugin = PluginWithInteriorCell(
            "01-World.esm", ["00-Pad.esm"], worldRecords, FormId(1, FixtureCellLocalId));

        output.Plugin("plugins/00-Pad.esm", Plugin("00-Pad.esm", [], []));
        output.Plugin("plugins/01-World.esm", worldPlugin);
        output.Plugin("plugins/02-Relations.esp", PluginWithInteriorCell(
            "02-Relations.esp",
            ["01-World.esm"],
            [
                Record("REFR", FormId(0, referenceLocal),
                    Sub("EDID", Z("FixtureReference")),
                    Sub("NAME", U32(FormId(0, baseLocal))),
                    Sub("XLKR", Pair(FormId(0, keywordLocal), 0)),
                    Sub("XLKR", Pair(FormId(0, keywordLocal), FormId(0, referenceLocal))),
                    Sub("XLRL", U32(0)),
                    Sub("XOWN", U32(FormId(0, ownerLocal))),
                    Sub("DATA", Placement(-1, -2, -3, 1, 2, 3))),
            ], FormId(0, FixtureCellLocalId)));
        var placementBody = Subs(
            Sub("EDID", Z("FixtureReference")),
            Sub("NAME", U32(FormId(0, baseLocal))),
            Sub("XLKR", Pair(FormId(0, keywordLocal), FormId(0, referenceLocal))),
            Sub("XLRL", U32(FormId(0, locationLocal))),
            Sub("XOWN", U32(FormId(0, ownerLocal))),
            Sub("DATA", Placement(10, 20, 30, 0.1f, 0.2f, 0.3f)));
        var placementPlugin = PluginWithInteriorCell(
            "03-Placement.esp",
            ["01-World.esm", "02-Relations.esp"],
            [RecordRaw("REFR", FormId(0, referenceLocal), placementBody, 0)],
            FormId(0, FixtureCellLocalId));
        output.Plugin("plugins/03-Placement.esp", placementPlugin);
        output.Plugin("plugins/04-MergedWinner.esp", PluginWithInteriorCell(
            "04-MergedWinner.esp",
            ["01-World.esm"],
            [
                Record("REFR", FormId(0, referenceLocal),
                    Sub("EDID", Z("FixtureReference")),
                    Sub("NAME", U32(FormId(0, baseLocal))),
                    Sub("XLKR", Pair(FormId(0, keywordLocal), 0)),
                    Sub("XLRL", U32(FormId(0, locationLocal))),
                    Sub("XOWN", U32(0)),
                    Sub("DATA", Placement(11, 21, 31, 0, 0, 0))),
            ], FormId(0, FixtureCellLocalId)));
        output.Plugin("plugins/05-DeletedWinner.esp", PluginWithInteriorCell(
            "05-DeletedWinner.esp",
            ["01-World.esm"],
            [Record("REFR", FormId(0, referenceLocal), DeletedRecord, Sub("EDID", Z("FixtureReference")))],
            FormId(0, FixtureCellLocalId)));
        output.Plugin("plugins/06-Boundaries.esp", PluginWithInteriorCell(
            "06-Boundaries.esp",
            ["01-World.esm"],
            [
                Record("REFR", FormId(1, 0x00000800),
                    Sub("EDID", Z("AbsentFields")),
                    Sub("NAME", U32(0)),
                    Sub("DATA", Placement(float.MinValue, float.MaxValue, -0.0f, float.Epsilon, -float.Epsilon, 0))),
                Record("REFR", FormId(1, 0x00000801),
                    Sub("EDID", Z("UnresolvedLinks")),
                    Sub("NAME", U32(0x00FF_FFFF)),
                    Sub("XLKR", Pair(0x00FF_FFFE, 0x00FF_FFFD)),
                    Sub("XLRL", U32(0x00FF_FFFC)),
                    Sub("XOWN", U32(0x00FF_FFFB)),
                    Sub("DATA", Pattern(24, seed, 0x44))),
            ], FormId(0, FixtureCellLocalId)));

        output.Plugin("mutations/Refr-SubrecordHeaderTruncated.esp", TruncateInsideLastRecord(PluginWithInteriorCell(
            "Refr-SubrecordHeaderTruncated.esp", ["01-World.esm"],
            [Record("REFR", FormId(0, referenceLocal), Sub("NAME", U32(FormId(0, baseLocal))))],
            FormId(0, FixtureCellLocalId)), "REFR", 8));
        output.Plugin("mutations/Refr-SubrecordBodyOverrun.esp", MutateLastSubrecordSize(PluginWithInteriorCell(
            "Refr-SubrecordBodyOverrun.esp", ["01-World.esm"],
            [Record("REFR", FormId(0, referenceLocal), Sub("NAME", U32(FormId(0, baseLocal))))],
            FormId(0, FixtureCellLocalId)), 0x7FFF));
        output.Plugin("mutations/Refr-DanglingExtendedSize.esp", PluginWithInteriorCell(
            "Refr-DanglingExtendedSize.esp", ["01-World.esm"],
            [RecordRaw("REFR", FormId(0, referenceLocal), Subs(Sub("XXXX", U32(0x10000))), 0)],
            FormId(0, FixtureCellLocalId)));
        output.Plugin(
            "mutations/one-byte-data/03-Placement.esp",
            MutateLastSubrecordByte(placementPlugin, "DATA", 23, 0x01));
        output.Plugin("mutations/master-order-reindexed/03-Placement.esp", PluginWithInteriorCell(
            "03-Placement.esp", ["02-Relations.esp", "01-World.esm"],
            [Record("REFR", FormId(1, referenceLocal), Sub("NAME", U32(FormId(1, baseLocal))), Sub("XOWN", U32(FormId(1, ownerLocal))), Sub("DATA", Placement(10, 20, 30, 0, 0, 0)))],
            FormId(1, FixtureCellLocalId)));
        output.Plugin("mutations/master-order-unreindexed/03-Placement.esp", PluginWithInteriorCell(
            "03-Placement.esp", ["02-Relations.esp", "01-World.esm"],
            [Record("REFR", FormId(0, referenceLocal), Sub("NAME", U32(FormId(0, baseLocal))), Sub("XOWN", U32(FormId(0, ownerLocal))), Sub("DATA", Placement(10, 20, 30, 0, 0, 0)))],
            FormId(0, FixtureCellLocalId)));
        output.Plugin("mutations/record-order/01-World.esm", PluginWithInteriorCell(
            "01-World.esm", ["00-Pad.esm"],
            [
                worldRecords[4],
                worldRecords[3],
                worldRecords[2],
                worldRecords[1],
                worldRecords[0],
            ], FormId(1, FixtureCellLocalId)));
        output.Plugin("mutations/compression/03-Placement.esp", PluginWithInteriorCell(
            "03-Placement.esp", ["01-World.esm", "02-Relations.esp"],
            [RecordRaw("REFR", FormId(0, referenceLocal), placementBody, CompressedRecord)],
            FormId(0, FixtureCellLocalId)));
        output.Plugin("mutations/Boundaries-RepeatedXLKROrder.esp", PluginWithInteriorCell(
            "Boundaries-RepeatedXLKROrder.esp", ["01-World.esm"],
            [
                Record("REFR", FormId(0, referenceLocal),
                    Sub("NAME", U32(FormId(0, baseLocal))),
                    Sub("XLKR", Pair(FormId(0, keywordLocal), FormId(0, referenceLocal))),
                    Sub("XLKR", Pair(FormId(0, keywordLocal), 0)),
                    Sub("XLKR", Pair(0, FormId(0, referenceLocal))),
                    Sub("DATA", Placement(0, 0, 0, 0, 0, 0))),
            ], FormId(0, FixtureCellLocalId)));

        output.CaseMatrix("BETH-REFR-DEV",
        [
            Case("refr-layered-winner", "scan", ["plugins/00-Pad.esm", "plugins/01-World.esm", "plugins/02-Relations.esp", "plugins/03-Placement.esp", "plugins/04-MergedWinner.esp"]),
            Case("refr-deleted-winner", "scan", ["plugins/01-World.esm", "plugins/05-DeletedWinner.esp"]),
            Case("refr-boundaries", "scan", ["plugins/01-World.esm", "plugins/06-Boundaries.esp"]),
            Case("refr-compression-equivalence", "compare", ["plugins/03-Placement.esp", "mutations/compression/03-Placement.esp"]),
            Case("refr-one-byte-data-change", "compare", ["plugins/03-Placement.esp", "mutations/one-byte-data/03-Placement.esp"]),
            Case("refr-master-reindexing", "compare", ["mutations/master-order-reindexed/03-Placement.esp", "mutations/master-order-unreindexed/03-Placement.esp"]),
            Case("refr-record-order", "compare", ["plugins/01-World.esm", "mutations/record-order/01-World.esm"]),
            Case("refr-repeated-xlkr-order", "scan", ["mutations/Boundaries-RepeatedXLKROrder.esp"]),
            Case("refr-truncated-subrecord-header", "scan", ["mutations/Refr-SubrecordHeaderTruncated.esp"]),
            Case("refr-subrecord-body-overrun", "scan", ["mutations/Refr-SubrecordBodyOverrun.esp"]),
            Case("refr-dangling-extended-size", "scan", ["mutations/Refr-DanglingExtendedSize.esp"]),
        ]);
        return output;
    }

    private static PackageOutput BuildLight(ulong seed)
    {
        _ = seed;
        var output = new PackageOutput();
        output.Plugin("plugins/00-Pad.esm", Plugin("00-Pad.esm", [], []));
        output.Plugin("plugins/01-Native.esl", Plugin(
            "01-Native.esl", ["00-Pad.esm"],
            [
                Record("STAT", FormId(1, 0x00000800), Sub("EDID", Z("NativeLightMinimum"))),
                Record("STAT", FormId(1, 0x00000FFF), Sub("EDID", Z("NativeLightMaximum"))),
            ],
            LightPlugin));
        output.Plugin("plugins/02-Flagged.esp", Plugin(
            "02-Flagged.esp", ["00-Pad.esm"],
            [
                Record("STAT", FormId(1, 0x00000800), Sub("EDID", Z("FlaggedLightMinimum"))),
                Record("STAT", FormId(1, 0x00000FFF), Sub("EDID", Z("FlaggedLightMaximum"))),
            ],
            LightPlugin));
        output.Plugin("plugins/03-Consumer.esp", PluginWithInteriorCell(
            "03-Consumer.esp", ["01-Native.esl", "02-Flagged.esp"],
            [
                Record("REFR", FormId(2, 0x00000800), Sub("NAME", U32(FormId(0, 0x800))), Sub("DATA", Placement(0, 0, 0, 0, 0, 0))),
                Record("REFR", FormId(2, 0x00000801), Sub("NAME", U32(FormId(1, 0xFFF))), Sub("DATA", Placement(1, 1, 1, 0, 0, 0))),
            ], FormId(2, FixtureCellLocalId)));
        output.Plugin("plugins/04-Winner.esp", Plugin(
            "04-Winner.esp", ["01-Native.esl"],
            [Record("STAT", FormId(0, 0x800), Sub("EDID", Z("NativeLightMinimumOverride")))]));

        output.Plugin("mutations/Native-BelowObjectRange.esl", Plugin(
            "Native-BelowObjectRange.esl", ["00-Pad.esm"], [Record("STAT", FormId(1, 0x000007FF), Sub("EDID", Z("BelowRange")))], LightPlugin));
        output.Plugin("mutations/Native-AboveLightMaximum.esl", Plugin(
            "Native-AboveLightMaximum.esl", ["00-Pad.esm"], [Record("STAT", FormId(1, 0x00001000), Sub("EDID", Z("AboveRange")))], LightPlugin));
        output.Plugin("mutations/FlaggedEsp-BelowObjectRange.esp", Plugin(
            "FlaggedEsp-BelowObjectRange.esp", ["00-Pad.esm"], [Record("STAT", FormId(1, 0x000007FF), Sub("EDID", Z("BelowRange")))], LightPlugin));
        output.Plugin("mutations/FlaggedEsp-AboveLightMaximum.esp", Plugin(
            "FlaggedEsp-AboveLightMaximum.esp", ["00-Pad.esm"], [Record("STAT", FormId(1, 0x00001000), Sub("EDID", Z("AboveRange")))], LightPlugin));
        output.Plugin("mutations/Native-HeaderFlagRemoved.esl", Plugin(
            "Native-HeaderFlagRemoved.esl", ["00-Pad.esm"], [Record("STAT", FormId(1, 0x00000800), Sub("EDID", Z("ExtensionHeaderMismatch")))]));
        output.Plugin("mutations/Consumer-LightReferenceOutOfRange.esp", PluginWithInteriorCell(
            "Consumer-LightReferenceOutOfRange.esp", ["01-Native.esl"],
            [Record("REFR", FormId(1, 0x00000800), Sub("NAME", U32(FormId(0, 0x1000))), Sub("DATA", Placement(0, 0, 0, 0, 0, 0)))],
            FormId(1, FixtureCellLocalId)));

        output.CaseMatrix("BETH-LIGHT-VAL",
        [
            Case("light-native-minimum", "scan", ["plugins/01-Native.esl"]),
            Case("light-native-maximum", "scan", ["plugins/01-Native.esl"]),
            Case("light-flagged-esp-minimum", "scan", ["plugins/02-Flagged.esp"]),
            Case("light-flagged-esp-maximum", "scan", ["plugins/02-Flagged.esp"]),
            Case("light-native-below-range", "scan", ["mutations/Native-BelowObjectRange.esl"]),
            Case("light-native-above-range", "scan", ["mutations/Native-AboveLightMaximum.esl"]),
            Case("light-flagged-below-range", "scan", ["mutations/FlaggedEsp-BelowObjectRange.esp"]),
            Case("light-flagged-above-range", "scan", ["mutations/FlaggedEsp-AboveLightMaximum.esp"]),
            Case("light-extension-header-mismatch", "scan", ["mutations/Native-HeaderFlagRemoved.esl"]),
            Case("light-reference-out-of-range", "scan", ["mutations/Consumer-LightReferenceOutOfRange.esp"]),
            Case("light-consumer-and-winner", "scan", ["plugins/01-Native.esl", "plugins/02-Flagged.esp", "plugins/03-Consumer.esp", "plugins/04-Winner.esp"]),
        ]);
        return output;
    }

    private static PackageOutput BuildMalformed(ulong seed)
    {
        var output = new PackageOutput();
        var valid = Plugin(
            "MinimalValid.esp", [],
            [Record("STAT", 0x00000800, Sub("EDID", Z("MinimalValid")), Sub("DATA", Pattern(4, seed, 0x55)))]);
        output.Plugin("plugins/MinimalValid.esp", valid);
        output.Plugin("mutations/TruncatedRecordHeader.esp", valid[..10]);
        output.Plugin("mutations/TruncatedRecordPayload.esp", TruncateInsideLastGroup(valid, 3));
        output.Plugin("mutations/RecordSizeOverflow.esp", MutateU32(valid, 4, uint.MaxValue));
        output.Plugin("mutations/GroupSizeTooSmall.esp", MutateFirstGroupSize(valid, 20));
        output.Plugin("mutations/GroupSizePastEnd.esp", MutateFirstGroupSize(valid, (uint)(valid.Length + 0x100)));
        output.Plugin("mutations/GroupSizeOverflow.esp", MutateFirstGroupSize(valid, uint.MaxValue));
        output.Plugin("mutations/CompressedMissingLength.esp", Plugin(
            "CompressedMissingLength.esp", [], [RecordRaw("STAT", 0x800, [0x78, 0x9C], CompressedRecord, alreadyCompressed: true)]));
        output.Plugin("mutations/CompressedInvalidZlib.esp", Plugin(
            "CompressedInvalidZlib.esp", [], [RecordRaw("STAT", 0x800, [0x10, 0, 0, 0, 0x01, 0x02, 0x03], CompressedRecord, alreadyCompressed: true)]));
        output.Plugin("mutations/CompressedSizeMismatch.esp", Plugin(
            "CompressedSizeMismatch.esp", [], [CompressedRecordWithDeclaredSize("STAT", 0x800, Subs(Sub("EDID", Z("Mismatch"))), 1)]));
        output.Plugin("mutations/CompressedDeclaredSizeOverLimit.esp", Plugin(
            "CompressedDeclaredSizeOverLimit.esp", [], [CompressedRecordWithDeclaredSize("STAT", 0x800, Subs(Sub("EDID", Z("BoundedTinyStream"))), 0x4000_0000)]));
        output.Plugin("mutations/NestedGroupsOverLimit.esp", PluginWithNestedGroups(70));
        output.Plugin("mutations/RecordCountOverLimit.esp", PluginWithRepeatedRecords(4_097));
        output.Plugin("mutations/SubrecordCountOverLimit.esp", Plugin(
            "SubrecordCountOverLimit.esp", [], [RecordRaw("STAT", 0x800, RepeatSubrecord(Sub("DATA", []), 4_097), 0)]));
        output.Plugin("mutations/InvalidRecordMasterIndex.esp", Plugin(
            "InvalidRecordMasterIndex.esp", ["MinimalValid.esp"], [Record("STAT", FormId(3, 0x800), Sub("EDID", Z("InvalidRecordMasterIndex")))]));
        output.Plugin("mutations/InvalidLinkMasterIndex.esp", PluginWithInteriorCell(
            "InvalidLinkMasterIndex.esp", ["MinimalValid.esp"],
            [Record("REFR", FormId(1, 0x800), Sub("NAME", U32(FormId(3, 0x800))), Sub("DATA", Placement(0, 0, 0, 0, 0, 0)))],
            FormId(1, FixtureCellLocalId)));
        output.Plugin("mutations/MasterMissingDataPair.esp", PluginWithUnpairedMaster());
        output.Plugin("mutations/ChangedDuringRead-A.esp", Plugin(
            "ChangedDuringRead-A.esp", [], [Record("STAT", 0x800, Sub("EDID", Z("ChangedDuringReadA")))]));
        output.Plugin("mutations/ChangedDuringRead-B.esp", Plugin(
            "ChangedDuringRead-B.esp", [], [Record("STAT", 0x800, Sub("EDID", Z("ChangedDuringReadB")))]));

        output.Json("requests/changed-during-read-plan.json", new
        {
            schema = "infinium.fixture.changed-during-read-plan",
            schema_version = 1,
            operation = "replace_between_identity_and_content_reads",
            initial_path = "mutations/ChangedDuringRead-A.esp",
            replacement_path = "mutations/ChangedDuringRead-B.esp",
            resource_limits = new { replacement_count = 1, maximum_file_bytes = 4096 },
        });
        output.CaseMatrix("BETH-MALFORMED-VAL",
        [
            Case("malformed-control", "scan", ["plugins/MinimalValid.esp"]),
            Case("malformed-truncated-record-header", "scan", ["mutations/TruncatedRecordHeader.esp"]),
            Case("malformed-truncated-record-payload", "scan", ["mutations/TruncatedRecordPayload.esp"]),
            Case("malformed-record-size-overflow", "scan", ["mutations/RecordSizeOverflow.esp"]),
            Case("malformed-group-size-too-small", "scan", ["mutations/GroupSizeTooSmall.esp"]),
            Case("malformed-group-size-past-end", "scan", ["mutations/GroupSizePastEnd.esp"]),
            Case("malformed-group-size-overflow", "scan", ["mutations/GroupSizeOverflow.esp"]),
            Case("malformed-compressed-missing-length", "scan", ["mutations/CompressedMissingLength.esp"]),
            Case("malformed-compressed-invalid-zlib", "scan", ["mutations/CompressedInvalidZlib.esp"]),
            Case("malformed-compressed-size-mismatch", "scan", ["mutations/CompressedSizeMismatch.esp"]),
            Case("malformed-compressed-declared-size-over-limit", "scan", ["mutations/CompressedDeclaredSizeOverLimit.esp"]),
            Case("malformed-nested-groups-over-limit", "scan", ["mutations/NestedGroupsOverLimit.esp"]),
            Case("malformed-record-count-over-limit", "scan", ["mutations/RecordCountOverLimit.esp"]),
            Case("malformed-subrecord-count-over-limit", "scan", ["mutations/SubrecordCountOverLimit.esp"]),
            Case("malformed-record-master-index", "scan", ["mutations/InvalidRecordMasterIndex.esp"]),
            Case("malformed-link-master-index", "scan", ["mutations/InvalidLinkMasterIndex.esp"]),
            Case("malformed-unpaired-master", "scan", ["mutations/MasterMissingDataPair.esp"]),
            Case("malformed-changed-during-read", "orchestrated-read", ["requests/changed-during-read-plan.json"]),
        ]);
        return output;
    }

    private static PackageOutput BuildUnsupported(ulong seed)
    {
        _ = seed;
        var output = new PackageOutput();
        output.Plugin("plugins/UnsupportedFamily.esp", Plugin(
            "UnsupportedFamily.esp", [], [Record("WEAP", 0x800, Sub("EDID", Z("UnsupportedFamily")))]));
        output.Plugin("plugins/UnsupportedNpcField.esp", Plugin(
            "UnsupportedNpcField.esp", [],
            [
                Record("NPC_", 0x800,
                    Sub("EDID", Z("UnsupportedNpcField")),
                    Sub("ACBS", Acbs(0)),
                    Sub("DOFT", U32(0))),
            ]));
        output.Plugin("plugins/LocalizedDependency.esp", Plugin(
            "LocalizedDependency.esp", [],
            [Record("STAT", 0x800, Sub("EDID", Z("LocalizedDependency")), Sub("FULL", U32(1)))],
            headerFlags: 0x0000_0080));
        output.Plugin("plugins/LocalizedDependency_English.strings", StringsFile([(1, "Project-authored localized fixture text")]));

        output.Json("requests/localized-string-resolution.json", new
        {
            schema = "infinium.fixture.unsupported-request",
            schema_version = 1,
            operation = "resolve_localized_string",
            plugin = "plugins/LocalizedDependency.esp",
            strings = "plugins/LocalizedDependency_English.strings",
            string_id = 1,
        });
        output.Json("requests/archive-member.json", new
        {
            schema = "infinium.fixture.unsupported-request",
            schema_version = 1,
            operation = "read_archive_member",
            archive_kind = "project-authored-placeholder",
            archive_path = "absent/FixtureArchive.bsa",
            member_path = "meshes/fixture.nif",
        });
        output.Json("requests/automatic-environment-discovery.json", new
        {
            schema = "infinium.fixture.unsupported-request",
            schema_version = 1,
            operation = "automatic_environment_discovery",
            requested_sources = new[] { "installed_game", "mod_manager", "registry" },
        });
        output.CaseMatrix("BETH-UNSUPPORTED-VAL",
        [
            Case("unsupported-record-family", "scan", ["plugins/UnsupportedFamily.esp"]),
            Case("unsupported-npc-field", "scan", ["plugins/UnsupportedNpcField.esp"]),
            Case("unsupported-localized-string", "request", ["requests/localized-string-resolution.json"]),
            Case("unsupported-archive-member", "request", ["requests/archive-member.json"]),
            Case("unsupported-environment-discovery", "request", ["requests/automatic-environment-discovery.json"]),
        ]);
        return output;
    }

    private static void WritePackage(string inputsRoot, string packageId, PackageOutput output, ulong seed)
    {
        if (Directory.Exists(inputsRoot))
        {
            Directory.Delete(inputsRoot, recursive: true);
        }

        Directory.CreateDirectory(inputsRoot);
        foreach (var (relativePath, bytes) in output.Files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(inputsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        var entries = output.Files
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => ConstructionEntry.ForFile(pair.Key, pair.Value))
            .ToList();
        entries.Add(new ConstructionEntry(
            "construction-manifest.json",
            0,
            null,
            [new ConstructionRegion(0, 1, "project-authored-construction-metadata")]));
        var manifest = new ConstructionManifest(
            "infinium.bethesda-fixture-construction",
            1,
            packageId,
            "Infinium.BethesdaFixtures.Generator",
            1,
            seed.ToString(CultureInfo.InvariantCulture),
            ".NET/BCL only",
            [
                "No game or mod bytes consumed.",
                "No Mutagen or xEdit input consumed.",
                "No network input consumed.",
                "No production parser input consumed.",
                "No oracle, taxonomy answer, held-out content, or snapshot capture input consumed.",
            ],
            entries);

        var manifestBytes = StabilizeSelfLength(manifest);
        File.WriteAllBytes(Path.Combine(inputsRoot, "construction-manifest.json"), manifestBytes);
    }

    private static byte[] StabilizeSelfLength(ConstructionManifest manifest)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var bytes = JsonBytes(manifest);
            var self = manifest.Files.Single(entry => entry.Path == "construction-manifest.json");
            if (self.ByteLength == bytes.Length && self.Regions[0].Length == bytes.Length)
            {
                return bytes;
            }

            self.ByteLength = bytes.Length;
            self.Regions[0].Length = bytes.Length;
        }

        throw new InvalidDataException("Construction-manifest self-length did not stabilize.");
    }

    private static byte[] Plugin(
        string name,
        IReadOnlyList<string> masters,
        IReadOnlyList<byte[]> records,
        uint headerFlags = 0)
    {
        if (records.Any(record => record.AsSpan(0, 4).SequenceEqual(Sig("REFR"))))
        {
            throw new InvalidDataException(
                $"{name} contains a REFR outside an explicit CELL child topology.");
        }

        var groups = records
            .GroupBy(record => Encoding.ASCII.GetString(record, 0, 4), StringComparer.Ordinal)
            .Select(group => Group(group.Key, Concat(group)))
            .ToArray();
        return BuildPlugin(name, masters, groups, records.Count, headerFlags);
    }

    private static byte[] PluginWithInteriorCell(
        string name,
        IReadOnlyList<string> masters,
        IReadOnlyList<byte[]> records,
        uint cellFormId,
        uint headerFlags = 0)
    {
        var references = records
            .Where(record => record.AsSpan(0, 4).SequenceEqual(Sig("REFR")))
            .ToArray();
        if (references.Length == 0)
        {
            throw new InvalidDataException($"{name} requested a CELL topology without any REFR records.");
        }

        var ordinaryRecords = records
            .Where(record => !record.AsSpan(0, 4).SequenceEqual(Sig("REFR")))
            .ToArray();
        var ordinaryGroups = ordinaryRecords
            .GroupBy(record => Encoding.ASCII.GetString(record, 0, 4), StringComparer.Ordinal)
            .Select(group => Group(group.Key, Concat(group)))
            .ToList();

        var cell = Record(
            "CELL",
            cellFormId,
            Sub("EDID", Z($"InfiniumFixtureCell{cellFormId:X8}")),
            Sub("DATA", U16(0x0001)));
        var persistentChildren = Group(cellFormId, Concat(references), groupType: 8);
        var cellChildren = Group(cellFormId, persistentChildren, groupType: 6);
        var cellSubBlock = Group(0, Concat(cell, cellChildren), groupType: 3);
        var cellBlock = Group(0, cellSubBlock, groupType: 2);
        ordinaryGroups.Add(Group("CELL", cellBlock));

        return BuildPlugin(name, masters, ordinaryGroups, records.Count + 1, headerFlags);
    }

    private static byte[] BuildPlugin(
        string name,
        IReadOnlyList<string> masters,
        IReadOnlyList<byte[]> topLevelGroups,
        int recordCount,
        uint headerFlags)
    {
        var headerSubs = new List<byte[]>
        {
            Sub("HEDR", Concat(F32(1.7f), U32((uint)recordCount), U32(0x800))),
            Sub("CNAM", Z("Infinium project-authored fixture generator")),
            Sub("SNAM", Z(name)),
        };
        foreach (var master in masters)
        {
            headerSubs.Add(Sub("MAST", Z(master)));
            headerSubs.Add(Sub("DATA", U64(0)));
        }

        var header = RecordRaw("TES4", 0, Concat(headerSubs), headerFlags);
        if (topLevelGroups.Count == 0)
        {
            return header;
        }

        return Concat(header, Concat(topLevelGroups));
    }

    private static byte[] PluginWithNestedGroups(int depth)
    {
        var bytes = Record("STAT", 0x800, Sub("EDID", Z("Nested")));
        for (var index = 0; index < depth; index++)
        {
            bytes = Group("STAT", bytes, index + 1);
        }

        return Concat(Plugin("NestedGroupsOverLimit.esp", [], []), bytes);
    }

    private static byte[] PluginWithRepeatedRecords(int count)
    {
        var records = Enumerable.Range(0, count)
            .Select(index => Record("STAT", (uint)(0x800 + index), Sub("EDID", Z($"R{index:D4}"))))
            .ToArray();
        return Plugin("RecordCountOverLimit.esp", [], records);
    }

    private static byte[] PluginWithUnpairedMaster()
    {
        var body = Subs(
            Sub("HEDR", Concat(F32(1.7f), U32(0), U32(0x800))),
            Sub("CNAM", Z("Infinium project-authored fixture generator")),
            Sub("SNAM", Z("MasterMissingDataPair.esp")),
            Sub("MAST", Z("MinimalValid.esp")));
        return RecordRaw("TES4", 0, body, 0);
    }

    private static byte[] Record(string signature, uint formId, params byte[][] subrecords)
        => RecordRaw(signature, formId, Concat(subrecords), 0);

    private static byte[] Record(string signature, uint formId, uint flags, params byte[][] subrecords)
        => RecordRaw(signature, formId, Concat(subrecords), flags);

    private static byte[] RecordRaw(
        string signature,
        uint formId,
        byte[] body,
        uint flags,
        bool alreadyCompressed = false)
    {
        var storedBody = body;
        if ((flags & CompressedRecord) != 0 && !alreadyCompressed)
        {
            using var buffer = new MemoryStream();
            buffer.Write(U32((uint)body.Length));
            using (var zlib = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(body);
            }

            storedBody = buffer.ToArray();
        }

        return Concat(
            Sig(signature),
            U32((uint)storedBody.Length),
            U32(flags),
            U32(formId),
            U32(0),
            U16(44),
            U16(0),
            storedBody);
    }

    private static byte[] CompressedRecordWithDeclaredSize(string signature, uint formId, byte[] body, uint declaredSize)
    {
        using var buffer = new MemoryStream();
        buffer.Write(U32(declaredSize));
        using (var zlib = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(body);
        }

        return RecordRaw(signature, formId, buffer.ToArray(), CompressedRecord, alreadyCompressed: true);
    }

    private static byte[] Group(string signature, byte[] body, int groupType = 0)
        => Concat(Sig("GRUP"), U32((uint)(24 + body.Length)), Sig(signature), I32(groupType), U16(0), U16(0), U16(0), U16(0), body);

    private static byte[] Group(uint labelFormId, byte[] body, int groupType)
        => Concat(Sig("GRUP"), U32((uint)(24 + body.Length)), U32(labelFormId), I32(groupType), U16(0), U16(0), U16(0), U16(0), body);

    private static byte[] Sub(string signature, byte[] body)
    {
        if (body.Length > ushort.MaxValue)
        {
            return Concat(Sub("XXXX", U32((uint)body.Length)), Sig(signature), U16(0), body);
        }

        return Concat(Sig(signature), U16((ushort)body.Length), body);
    }

    private static byte[] Subs(params byte[][] values) => Concat(values);

    private static byte[] RepeatSubrecord(byte[] value, int count)
    {
        using var stream = new MemoryStream(value.Length * count);
        for (var index = 0; index < count; index++)
        {
            stream.Write(value);
        }

        return stream.ToArray();
    }

    private static byte[] Acbs(uint flags, ushort templateFlags = 0)
        => Concat(
            U32(flags),
            U16(0),
            U16(0),
            U16(1),
            U16(1),
            U16(0),
            U16(0),
            U16(0),
            U16(templateFlags),
            U16(0),
            U16(0));

    private static byte[] Placement(float x, float y, float z, float rx, float ry, float rz)
        => Concat(F32(x), F32(y), F32(z), F32(rx), F32(ry), F32(rz));

    private static byte[] RaceData(uint flags)
    {
        var data = new byte[0x80];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x20, 4), flags);
        return data;
    }

    private static byte[] Pair(uint first, uint second) => Concat(U32(first), U32(second));

    private static uint FormId(int masterIndex, uint localId)
        => ((uint)masterIndex << 24) | (localId & 0x00FF_FFFF);

    private static byte[] Pattern(int length, ulong seed, byte discriminator)
    {
        var bytes = new byte[length];
        var state = seed ^ ((ulong)discriminator << 56);
        for (var index = 0; index < bytes.Length; index++)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
            }

            bytes[index] = unchecked((byte)state);
        }

        return bytes;
    }

    private static byte[] StringsFile(IReadOnlyList<(uint Id, string Value)> entries)
    {
        var encoded = entries.Select(entry => Encoding.UTF8.GetBytes(entry.Value + "\0")).ToArray();
        var dataOffset = 0u;
        using var stream = new MemoryStream();
        stream.Write(U32((uint)entries.Count));
        stream.Write(U32((uint)encoded.Sum(bytes => bytes.Length)));
        for (var index = 0; index < entries.Count; index++)
        {
            stream.Write(U32(entries[index].Id));
            stream.Write(U32(dataOffset));
            dataOffset += (uint)encoded[index].Length;
        }

        foreach (var bytes in encoded)
        {
            stream.Write(bytes);
        }

        return stream.ToArray();
    }

    private static byte[] MutateU32(byte[] source, int offset, uint value)
    {
        var result = source.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset, 4), value);
        return result;
    }

    private static byte[] MutateFirstGroupSize(byte[] source, uint value)
    {
        var result = source.ToArray();
        var offset = FindSignature(result, "GRUP");
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset + 4, 4), value);
        return result;
    }

    private static byte[] MutateLastSubrecordSize(byte[] source, ushort value)
    {
        var result = source.ToArray();
        var offset = FindLastSignature(result, "NAME");
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 4, 2), value);
        return result;
    }

    private static byte[] MutateSubrecordByte(byte[] source, string signature, int bodyOffset, byte mask)
        => MutateSubrecordByteAt(source, FindSignature(source, signature), bodyOffset, mask);

    private static byte[] MutateLastSubrecordByte(byte[] source, string signature, int bodyOffset, byte mask)
        => MutateSubrecordByteAt(source, FindLastSignature(source, signature), bodyOffset, mask);

    private static byte[] MutateSubrecordByteAt(byte[] source, int offset, int bodyOffset, byte mask)
    {
        var result = source.ToArray();
        var size = BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(offset + 4, 2));
        if ((uint)bodyOffset >= size)
        {
            throw new ArgumentOutOfRangeException(nameof(bodyOffset));
        }

        result[offset + 6 + bodyOffset] ^= mask;
        return result;
    }

    private static void AssertOneByteDifference(
        string baselinePath,
        string mutationPath,
        string signature,
        int byteOffset,
        bool isRecordHeader)
    {
        if (!StringComparer.Ordinal.Equals(Path.GetFileName(baselinePath), Path.GetFileName(mutationPath)))
        {
            throw new InvalidDataException($"Controlled mutation changes origin basename: {mutationPath}.");
        }

        var baseline = File.ReadAllBytes(baselinePath);
        var mutation = File.ReadAllBytes(mutationPath);
        if (baseline.Length != mutation.Length)
        {
            throw new InvalidDataException($"Controlled mutation changes byte length: {mutationPath}.");
        }

        var differences = Enumerable.Range(0, baseline.Length)
            .Where(index => baseline[index] != mutation[index])
            .ToArray();
        var signatureOffset = FindLastSignature(baseline, signature);
        var expectedOffset = signatureOffset + byteOffset + (isRecordHeader ? 0 : 6);
        if (differences.Length != 1 || differences[0] != expectedOffset)
        {
            throw new InvalidDataException(
                $"Controlled mutation must change exactly byte {expectedOffset}: {mutationPath}.");
        }
    }

    private static void AssertRecordOrderOnly(string baselinePath, string mutationPath)
    {
        if (!StringComparer.Ordinal.Equals(Path.GetFileName(baselinePath), Path.GetFileName(mutationPath)))
        {
            throw new InvalidDataException($"Record-order mutation changes origin basename: {mutationPath}.");
        }

        var baseline = File.ReadAllBytes(baselinePath);
        var mutation = File.ReadAllBytes(mutationPath);
        if (baseline.Length != mutation.Length)
        {
            throw new InvalidDataException($"Record-order mutation changes byte length: {mutationPath}.");
        }

        var baselineHeaderEnd = checked(24 + (int)BinaryPrimitives.ReadUInt32LittleEndian(baseline.AsSpan(4, 4)));
        var mutationHeaderEnd = checked(24 + (int)BinaryPrimitives.ReadUInt32LittleEndian(mutation.AsSpan(4, 4)));
        if (baselineHeaderEnd != mutationHeaderEnd
            || !baseline.AsSpan(0, baselineHeaderEnd).SequenceEqual(mutation.AsSpan(0, mutationHeaderEnd)))
        {
            throw new InvalidDataException($"Record-order mutation changes the TES4 header: {mutationPath}.");
        }

        var baselineBlocks = ReadCompleteRecordBlocks(baseline);
        var mutationBlocks = ReadCompleteRecordBlocks(mutation);
        var baselineMultiset = baselineBlocks
            .GroupBy(Convert.ToBase64String, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var mutationMultiset = mutationBlocks
            .GroupBy(Convert.ToBase64String, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (baselineMultiset.Count != mutationMultiset.Count
            || baselineMultiset.Any(pair =>
                !mutationMultiset.TryGetValue(pair.Key, out var count) || count != pair.Value))
        {
            throw new InvalidDataException($"Record-order mutation changes a complete record block: {mutationPath}.");
        }

        if (baselineBlocks.Select(Convert.ToBase64String)
            .SequenceEqual(mutationBlocks.Select(Convert.ToBase64String), StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Record-order mutation does not change record order: {mutationPath}.");
        }
    }

    private static List<byte[]> ReadCompleteRecordBlocks(byte[] plugin)
    {
        var blocks = new List<byte[]>();
        var position = checked(24 + (int)BinaryPrimitives.ReadUInt32LittleEndian(plugin.AsSpan(4, 4)));
        ReadCompleteRecordBlocks(plugin, position, plugin.Length, blocks);
        return blocks;
    }

    private static void ReadCompleteRecordBlocks(
        byte[] plugin,
        int start,
        int end,
        List<byte[]> blocks)
    {
        var position = start;
        while (position < end)
        {
            if (plugin.AsSpan(position, 4).SequenceEqual(Sig("GRUP")))
            {
                var groupSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(plugin.AsSpan(position + 4, 4)));
                var groupEnd = checked(position + groupSize);
                if (groupEnd > end)
                {
                    throw new InvalidDataException("Group crosses its containing boundary.");
                }

                ReadCompleteRecordBlocks(plugin, position + 24, groupEnd, blocks);
                position = groupEnd;
                continue;
            }

            var recordSize = checked(
                24 + (int)BinaryPrimitives.ReadUInt32LittleEndian(plugin.AsSpan(position + 4, 4)));
            if (position + recordSize > end)
            {
                throw new InvalidDataException("Record crosses its containing boundary.");
            }

            blocks.Add(plugin.AsSpan(position, recordSize).ToArray());
            position += recordSize;
        }

        if (position != end)
        {
            throw new InvalidDataException("Element block crosses its containing boundary.");
        }
    }

    private static byte[] MutateLastRecordFormId(byte[] source, string signature, uint formId)
    {
        var result = source.ToArray();
        var offset = FindLastSignature(result, signature);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset + 12, 4), formId);
        return result;
    }

    private static int FindSignature(byte[] bytes, string signature)
    {
        var needle = Sig(signature);
        for (var index = 0; index <= bytes.Length - needle.Length; index++)
        {
            if (bytes.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return index;
            }
        }

        throw new InvalidDataException($"Signature {signature} not found.");
    }

    private static int FindLastSignature(byte[] bytes, string signature)
    {
        var needle = Sig(signature);
        for (var index = bytes.Length - needle.Length; index >= 0; index--)
        {
            if (bytes.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return index;
            }
        }

        throw new InvalidDataException($"Signature {signature} not found.");
    }

    private static byte[] Truncate(byte[] source, int count) => source[..^count];

    private static byte[] TruncateInsideLastGroup(byte[] source, int count)
    {
        if (count <= 0 || count >= source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var terminalGroupOffsets = new List<int>();
        var firstElementOffset = checked(
            24 + (int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(4, 4)));
        CollectTerminalGroupOffsets(
            source,
            firstElementOffset,
            source.Length,
            source.Length,
            terminalGroupOffsets);
        if (terminalGroupOffsets.Count == 0)
        {
            throw new InvalidDataException("No terminal enclosing group was found.");
        }

        var result = source[..^count];
        foreach (var groupOffset in terminalGroupOffsets)
        {
            var groupSize = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(groupOffset + 4, 4));
            if (groupSize < 24 + count)
            {
                throw new InvalidDataException("Cannot truncate beyond the enclosing group body.");
            }

            BinaryPrimitives.WriteUInt32LittleEndian(
                result.AsSpan(groupOffset + 4, 4),
                groupSize - (uint)count);
        }

        return result;
    }

    private static void CollectTerminalGroupOffsets(
        byte[] source,
        int start,
        int end,
        int terminalEnd,
        List<int> offsets)
    {
        var position = start;
        while (position < end)
        {
            var bodySize = checked(
                (int)BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(position + 4, 4)));
            if (source.AsSpan(position, 4).SequenceEqual(Sig("GRUP")))
            {
                var groupEnd = checked(position + bodySize);
                if (groupEnd == terminalEnd)
                {
                    offsets.Add(position);
                }

                CollectTerminalGroupOffsets(source, position + 24, groupEnd, terminalEnd, offsets);
                position = groupEnd;
            }
            else
            {
                position = checked(position + 24 + bodySize);
            }
        }

        if (position != end)
        {
            throw new InvalidDataException("Element crosses its containing boundary.");
        }
    }

    private static byte[] TruncateInsideLastRecord(byte[] source, string recordSignature, int count)
    {
        var recordOffset = FindLastSignature(source, recordSignature);
        var recordDataSize = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(recordOffset + 4, 4));
        if (recordDataSize < count)
        {
            throw new InvalidDataException("Cannot truncate beyond the enclosing record body.");
        }

        var result = TruncateInsideLastGroup(source, count);
        BinaryPrimitives.WriteUInt32LittleEndian(
            result.AsSpan(recordOffset + 4, 4),
            recordDataSize - (uint)count);
        return result;
    }

    private static byte[] JsonBytes<T>(T value)
        => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Program.JsonOptions) + "\n");

    private static byte[] Sig(string value)
    {
        if (value.Length != 4 || value.Any(character => character > 0x7F))
        {
            throw new ArgumentException("Signatures must contain exactly four ASCII characters.", nameof(value));
        }

        return Encoding.ASCII.GetBytes(value);
    }

    private static byte[] Z(string value) => Encoding.UTF8.GetBytes(value + "\0");

    private static byte[] U16(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] U32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] I32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] U64(ulong value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] F32(float value) => I32(BitConverter.SingleToInt32Bits(value));

    private static byte[] Concat(IEnumerable<byte[]> values) => Concat(values.ToArray());

    private static byte[] Concat(params byte[][] values)
    {
        var length = values.Sum(value => value.Length);
        var bytes = new byte[length];
        var offset = 0;
        foreach (var value in values)
        {
            value.CopyTo(bytes, offset);
            offset += value.Length;
        }

        return bytes;
    }

    private static Dictionary<string, object?> Case(
        string id,
        string operation,
        string[] paths)
        => new(StringComparer.Ordinal)
        {
            ["scenario_id"] = id,
            ["operation"] = operation,
            ["input_artifact_ids"] = paths.Select(path => $"inputs/{path}").ToArray(),
        };

    private sealed class PackageOutput
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public void Plugin(string path, byte[] bytes) => Files.Add(path, bytes);

        public void Json<T>(string path, T value) => Files.Add(path, JsonBytes(value));

        public void CaseMatrix(
            string fixtureId,
            IReadOnlyList<Dictionary<string, object?>> scenarios)
        {
            Json("case-matrix.json", new
            {
                schema_id = "infinium.evaluation.bethesda-case-matrix/v1",
                schema_version = "1",
                fixture_id = fixtureId,
                fixture_version = "1.2.0",
                source_basis = "accepted-slice-3.5-plan-and-retained-execution-inputs",
                cases = scenarios,
            });
            Json("effective-scan-configuration.json", EffectiveScanConfiguration(fixtureId));
        }

        private static object EffectiveScanConfiguration(string fixtureId)
            => new
            {
                schema_id = "infinium.scan.effective-configuration/v1",
                schema_version = "1",
                configuration_id = $"{fixtureId.ToLowerInvariant()}.slice4-fixture",
                configuration_version = "1.2.0",
                resolved_at = "2026-08-02T16:00:00.0000000+00:00",
                saved_configuration_reference = JsonDocument.Parse("null").RootElement.Clone(),
                analyzers = new[]
                {
                    new
                    {
                        analyzer_id = "bethesda-semantic-slice4",
                        analyzer_version = "1",
                        declaration_fingerprint = new string('0', 64),
                        enabled = true,
                        origin = "default",
                    },
                },
                sources = new[]
                {
                    new
                    {
                        source_id = "retained-bethesda-fixture-inputs",
                        mode = "local-fixture",
                        enabled = true,
                        origin = "default",
                    },
                },
                budgets = new
                {
                    max_dispatch_count = 0,
                    max_input_tokens = 0,
                    max_output_tokens = 0,
                    max_hosted_search_calls = 0,
                    max_nano_usd = 0,
                    dispatch_deadline = "2026-08-02T16:02:00.0000000+00:00",
                    origin = "default",
                },
                cache_policy = new
                {
                    analytical_mode = "force-clean-recomputation",
                    source_mode = "reuse-resolved-source",
                    provider_cache_mode = "disabled",
                    origin = "default",
                },
                tracing = new
                {
                    enabled = false,
                    level = "off",
                    sensitivity_label = "sensitive-development-diagnostic",
                    origin = "default",
                },
                candidate_breadth = new
                {
                    mode = "declared-mandatory-and-causal-lanes",
                    max_candidates = 1,
                    all_pairs_llm_comparison = false,
                    origin = "default",
                },
                thresholds = Array.Empty<object>(),
                provider = new
                {
                    mode = "disabled",
                    origin = "default",
                },
                resources = new
                {
                    max_general_workers = 1,
                    max_memory_bytes = 536870912,
                    max_output_bytes = 16777216,
                    origin = "default",
                },
                semantic_context_overrides = Array.Empty<object>(),
            };
    }
}

internal sealed record ConstructionManifest(
    string Schema,
    int SchemaVersion,
    string PackageId,
    string Generator,
    int GeneratorVersion,
    string FixedSeed,
    string Dependencies,
    IReadOnlyList<string> InputProhibitions,
    List<ConstructionEntry> Files);

internal sealed record ConstructionEntry(
    string Path,
    long ByteLength,
    string? Sha256,
    List<ConstructionRegion> Regions)
{
    public long ByteLength { get; set; } = ByteLength;

    public static ConstructionEntry ForFile(string path, byte[] bytes)
        => new(
            path,
            bytes.Length,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            [new ConstructionRegion(0, bytes.Length, ConstructionKind(path))]);

    private static string ConstructionKind(string path)
        => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".esm" or ".esp" or ".esl" => "project-authored-tes4-bytes",
            ".strings" => "project-authored-strings-table-bytes",
            ".json" => "project-authored-request-or-matrix-metadata",
            _ => "project-authored-input-bytes",
        };
}

internal sealed record ConstructionRegion(long Offset, long Length, string Kind)
{
    public long Length { get; set; } = Length;
}
