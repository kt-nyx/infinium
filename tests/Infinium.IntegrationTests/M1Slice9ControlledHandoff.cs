using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Analysis;
using Infinium.Persistence;

namespace Infinium.Tests;

internal static class M1Slice9ControlledHandoff
{
    private static string DefaultRoot => Path.Combine(
        Path.GetTempPath(),
        "infinium-s8-final-c79661c-6c369a1c04634278adcb69b5f2c2e231");
    private const string ReceiptSha = "571507a1622a4bd598573466da79c40782ace16ac0a9b30707f65e841e72700f";
    private const string ResultsSha = "23d20c4646d14ece1ba209043c6de94da2f87c68b5c869e4c6169adb4a01f633";

    internal static M1Slice9CompositionEnvelope LoadControlledComposition()
    {
        string declared = Environment.GetEnvironmentVariable("INFINIUM_SLICE8_RETAINED_OUTPUT_ROOT") ?? DefaultRoot;
        string root = Path.GetFullPath(declared);
        string temp = Path.GetFullPath(Path.GetTempPath());
        if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(root)
            || new DirectoryInfo(root).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException("The Slice 8 retained-output root failed containment and non-reparse admission.");
        }
        string receiptPath = ContainedFile(root, "slice8-verification-receipt.json");
        string resultsPath = ContainedFile(root, "controlled-real-results.json");
        byte[] receiptBytes = File.ReadAllBytes(receiptPath);
        byte[] resultsBytes = File.ReadAllBytes(resultsPath);
        if (receiptBytes.LongLength != 4005 || Hash(receiptBytes) != ReceiptSha
            || resultsBytes.LongLength != 10553 || Hash(resultsBytes) != ResultsSha)
        {
            throw new InvalidDataException("The exact accepted Slice 8 identity receipts drifted before retained-result access.");
        }
        using JsonDocument receipt = JsonDocument.Parse(receiptBytes);
        using JsonDocument results = JsonDocument.Parse(resultsBytes);
        if (receipt.RootElement.GetProperty("status").GetString() != "passed"
            || receipt.RootElement.GetProperty("handoff_id").GetString() != M1Slice9Composition.ControlledHandoffId
            || receipt.RootElement.GetProperty("input_manifest_sha256").GetString()
                != M1Slice9Composition.ControlledManifestSha256
            || receipt.RootElement.GetProperty("controlled_input_count").GetInt32() != 26
            || receipt.RootElement.GetProperty("public_manifests").GetArrayLength() != 3
            || receipt.RootElement.GetProperty("candidate_commit").GetString()
                != "c79661cd8eb016e483fa8b7396e7d4997b85d590"
            || results.RootElement.GetProperty("cases").GetArrayLength() != 4
            || results.RootElement.GetProperty("controlled_inputs").GetArrayLength() != 26)
        {
            throw new InvalidDataException("The Slice 8 receipt content failed exact activation identity admission.");
        }

        string productState = Path.Combine(root, "product-state");
        if (!Directory.Exists(productState)
            || new DirectoryInfo(productState).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException("The retained product-state root is missing or reparsed.");
        }
        EnsureNoReparseTree(productState);
        Dictionary<string, (long Length, string Sha)> before = Snapshot(productState);
        string copy = Path.Combine(Path.GetTempPath(), "infinium-s9-controlled-retained-" + Guid.NewGuid().ToString("N"));
        try
        {
            using StoragePaths paths = new(copy);
            paths.Create();
            CopyTree(productState, copy);
            using AuthoritativeStore store = new(paths);
            byte[][] payloads = results.RootElement.GetProperty("cases").EnumerateArray()
                .Select(item => store.ReadScopeReversionV2AnalysisBytes(
                    item.GetProperty("payload_id").GetString()!)).ToArray();
            M1Slice9CompositionEnvelope envelope = M1Slice9ScopeV2Composition.CreateControlled(
                payloads,
                [
                    new("m1-s8-verification-receipt", "controlled-identity-receipt", "1.0.0",
                        ReceiptSha, receiptBytes.LongLength),
                    new("m1-s8-controlled-real-results", "controlled-result-index", "1.0.0",
                        ResultsSha, resultsBytes.LongLength),
                ]);
            Dictionary<string, (long Length, string Sha)> after = Snapshot(productState);
            if (before.Count != after.Count || before.Any(item =>
                    !after.TryGetValue(item.Key, out (long Length, string Sha) value) || value != item.Value))
            {
                throw new InvalidDataException("The accepted Slice 8 retained-output root changed during read-only composition.");
            }
            return envelope;
        }
        finally
        {
            if (Directory.Exists(copy))
            {
                Directory.Delete(copy, recursive: true);
            }
        }
    }

    private static string ContainedFile(string root, string name)
    {
        string path = Path.GetFullPath(Path.Combine(root, name));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path)
            || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException("A Slice 8 identity receipt escaped or reparsed its accepted root.");
        }
        return path;
    }

    private static Dictionary<string, (long Length, string Sha)> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                path =>
                {
                    FileInfo file = new(path);
                    if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        throw new InvalidDataException("The retained product-state contains a reparsed file.");
                    }
                    return (file.Length, Hash(File.ReadAllBytes(path)));
                },
                StringComparer.OrdinalIgnoreCase);

    private static void EnsureNoReparseTree(string root)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(
                         directory, "*", SearchOption.TopDirectoryOnly))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new InvalidDataException("The retained product-state contains a reparsed entry.");
                }
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    pending.Push(entry);
                }
            }
        }
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            DirectoryInfo info = new(directory);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException("The retained product-state contains a reparsed directory.");
            }
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
