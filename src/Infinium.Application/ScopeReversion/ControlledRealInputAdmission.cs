using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Application.ScopeReversion;

public enum ControlledRealInputRole
{
    OfficialMaster,
    PositivePluginOrAsset,
    MatchedPatchControl,
    RequiredExtractionDependency,
}

public sealed record ControlledRealExpectedInput(
    string CaseId,
    string FileName,
    long ByteLength,
    Sha256Fingerprint Sha256,
    ControlledRealInputRole Role);

public sealed record ControlledRealAdmittedInput(
    string CaseId,
    string RelativePath,
    long ByteLength,
    Sha256Fingerprint Sha256,
    ControlledRealInputRole Role);

public sealed record ControlledRealInputAdmissionReceipt(
    string Schema,
    string HandoffId,
    Sha256Fingerprint ManifestFingerprint,
    IReadOnlyList<ControlledRealAdmittedInput> Inputs);

public static class ControlledRealInputAdmission
{
    public const string Schema = "infinium-controlled-real-input-handoff/1";

    private static readonly HashSet<string> ForbiddenPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "answer", "expected", "expected_result", "expectedResult", "oracle", "private", "score", "truth", "verdict",
    };

    public static ControlledRealInputAdmissionReceipt Validate(
        string manifestPath,
        IReadOnlyList<ControlledRealExpectedInput> expectedInputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(expectedInputs);
        string fullManifestPath = Path.GetFullPath(manifestPath);
        RejectReparseTraversal(fullManifestPath, includeLeaf: true);
        byte[] manifestBytes = ReadExactFile(fullManifestPath, out _);
        Sha256Fingerprint manifestFingerprint = new(Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant());

        using JsonDocument document = JsonDocument.Parse(manifestBytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        RejectForbiddenProperties(document.RootElement);
        JsonElement root = document.RootElement;
        RequireObject(root, "manifest");
        RequireExactProperties(root, "manifest", "schema", "handoff_id", "root", "read_only", "redistribution_allowed", "inputs");
        RequireString(root, "schema", Schema);
        string handoffId = RequireNonEmptyString(root, "handoff_id");
        string declaredRoot = RequireNonEmptyString(root, "root");
        if (!Path.IsPathFullyQualified(declaredRoot))
        {
            throw new InvalidDataException("The controlled-real root must be an absolute local path.");
        }
        if (!root.GetProperty("read_only").GetBoolean() || root.GetProperty("redistribution_allowed").GetBoolean())
        {
            throw new InvalidDataException("The handoff must be read-only and must prohibit redistribution.");
        }

        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(declaredRoot));
        RejectReparseTraversal(fullRoot, includeLeaf: true);
        string rootPrefix = fullRoot + Path.DirectorySeparatorChar;
        JsonElement inputs = root.GetProperty("inputs");
        if (inputs.ValueKind != JsonValueKind.Array || inputs.GetArrayLength() == 0)
        {
            throw new InvalidDataException("The handoff input allowlist must be a non-empty array.");
        }

        Dictionary<(string CaseId, string FileName), ControlledRealExpectedInput> expected = expectedInputs.ToDictionary(
            item => (item.CaseId, item.FileName), StringTupleComparer.OrdinalIgnoreCase);
        if (expected.Count != expectedInputs.Count)
        {
            throw new InvalidDataException("The tracked authority contains a duplicate case/file identity.");
        }

        List<ControlledRealAdmittedInput> admitted = [];
        HashSet<string> relativePaths = new(StringComparer.OrdinalIgnoreCase);
        HashSet<(string CaseId, string FileName)> observed = new(StringTupleComparer.OrdinalIgnoreCase);
        string? previousPath = null;
        foreach (JsonElement item in inputs.EnumerateArray())
        {
            RequireObject(item, "input");
            RequireExactProperties(item, "input", "case_id", "relative_path", "bytes", "sha256", "role");
            string caseId = RequireNonEmptyString(item, "case_id");
            string relativePath = NormalizeRelativePath(RequireNonEmptyString(item, "relative_path"));
            if (previousPath is not null && StringComparer.Ordinal.Compare(previousPath, relativePath) >= 0)
            {
                throw new InvalidDataException("Input allowlist entries must be unique and sorted by relative_path.");
            }
            previousPath = relativePath;
            if (!relativePaths.Add(relativePath))
            {
                throw new InvalidDataException($"Duplicate or case-colliding input path: {relativePath}");
            }
            long byteLength = item.GetProperty("bytes").GetInt64();
            string shaText = RequireNonEmptyString(item, "sha256");
            if (shaText.Length != 64 || !shaText.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException($"Invalid SHA-256 for {relativePath}.");
            }
            Sha256Fingerprint sha256 = new(shaText.ToLowerInvariant());
            ControlledRealInputRole role = ParseRole(RequireNonEmptyString(item, "role"));
            string fileName = Path.GetFileName(relativePath);
            if (!expected.TryGetValue((caseId, fileName), out ControlledRealExpectedInput? expectedInput)
                || !observed.Add((caseId, fileName)))
            {
                throw new InvalidDataException($"Undeclared, duplicate, or case-colliding controlled input: {caseId}/{fileName}");
            }
            if (byteLength != expectedInput.ByteLength || sha256 != expectedInput.Sha256 || role != expectedInput.Role)
            {
                throw new InvalidDataException($"Controlled input identity or role drifted: {caseId}/{fileName}");
            }

            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Controlled input escapes the declared root: {relativePath}");
            }
            RejectReparseTraversal(fullPath, includeLeaf: true);
            byte[] bytes = ReadExactFile(fullPath, out long actualLength);
            Sha256Fingerprint actualSha = new(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            if (actualLength != byteLength || actualSha != sha256)
            {
                throw new InvalidDataException($"Controlled input bytes drifted: {relativePath}");
            }
            admitted.Add(new(caseId, relativePath, byteLength, sha256, role));
        }

        if (observed.Count != expected.Count)
        {
            string missing = string.Join(", ", expected.Keys.Except(observed, StringTupleComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.CaseId}/{item.FileName}"));
            throw new InvalidDataException($"The handoff is missing tracked controlled inputs: {missing}");
        }
        return new(Schema, handoffId, manifestFingerprint, admitted);
    }

    private static byte[] ReadExactFile(string path, out long length)
    {
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
        length = RandomAccess.GetLength(handle);
        if (length < 0 || length > int.MaxValue)
        {
            throw new InvalidDataException($"Controlled input exceeds the supported local verification limit: {path}");
        }
        byte[] bytes = GC.AllocateUninitializedArray<byte>((int)length);
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = RandomAccess.Read(handle, bytes.AsSpan(offset), offset);
            if (read == 0)
            {
                throw new EndOfStreamException($"Controlled input was truncated while reading: {path}");
            }
            offset += read;
        }
        if (RandomAccess.GetLength(handle) != length)
        {
            throw new InvalidDataException($"Controlled input changed while it was being read: {path}");
        }
        return bytes;
    }

    private static string NormalizeRelativePath(string value)
    {
        if (Path.IsPathFullyQualified(value))
        {
            throw new InvalidDataException("Allowlisted input paths must be relative.");
        }
        string normalized = value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (normalized.Split(Path.DirectorySeparatorChar).Any(part => part is "" or "." or ".."))
        {
            throw new InvalidDataException($"Invalid controlled input relative path: {value}");
        }
        return normalized.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static void RejectReparseTraversal(string path, bool includeLeaf)
    {
        string? current = includeLeaf ? path : Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException($"Reparse points are forbidden in controlled input paths: {path}");
                }
            }
            string? parent = Path.GetDirectoryName(current);
            if (StringComparer.OrdinalIgnoreCase.Equals(parent, current))
            {
                break;
            }
            current = parent;
        }
    }

    private static void RejectForbiddenProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (ForbiddenPropertyNames.Contains(property.Name))
                {
                    throw new InvalidDataException($"Answer-bearing field is forbidden in a controlled input handoff: {property.Name}");
                }
                RejectForbiddenProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectForbiddenProperties(item);
            }
        }
    }

    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{label} must be an object.");
        }
    }

    private static void RequireExactProperties(JsonElement value, string label, params string[] names)
    {
        HashSet<string> expected = names.ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = value.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(actual))
        {
            throw new InvalidDataException($"{label} has missing or undeclared fields.");
        }
    }

    private static string RequireNonEmptyString(JsonElement value, string property)
    {
        JsonElement item = value.GetProperty(property);
        string? text = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
        return string.IsNullOrWhiteSpace(text) ? throw new InvalidDataException($"{property} must be a non-empty string.") : text;
    }

    private static void RequireString(JsonElement value, string property, string expected)
    {
        if (!StringComparer.Ordinal.Equals(RequireNonEmptyString(value, property), expected))
        {
            throw new InvalidDataException($"Unsupported {property}.");
        }
    }

    private static ControlledRealInputRole ParseRole(string value) => value switch
    {
        "official-master" => ControlledRealInputRole.OfficialMaster,
        "positive-plugin-or-asset" => ControlledRealInputRole.PositivePluginOrAsset,
        "matched-patch-control" => ControlledRealInputRole.MatchedPatchControl,
        "required-extraction-dependency" => ControlledRealInputRole.RequiredExtractionDependency,
        _ => throw new InvalidDataException($"Unsupported controlled input role: {value}"),
    };

    private sealed class StringTupleComparer : IEqualityComparer<(string CaseId, string FileName)>
    {
        public static StringTupleComparer OrdinalIgnoreCase { get; } = new();

        public bool Equals((string CaseId, string FileName) x, (string CaseId, string FileName) y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.CaseId, y.CaseId)
            && StringComparer.OrdinalIgnoreCase.Equals(x.FileName, y.FileName);

        public int GetHashCode((string CaseId, string FileName) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.CaseId), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FileName));
    }
}
