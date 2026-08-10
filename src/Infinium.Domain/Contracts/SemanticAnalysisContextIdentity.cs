using System.Security.Cryptography;
using System.Text;

namespace Infinium.Domain.Contracts;

public static class SemanticAnalysisContextIdentity
{
    public static Sha256Fingerprint ComputeFingerprint(SemanticAnalysisContextContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        List<string> fields =
        [
            Frame("context-id", value.ContextId.Value),
            Frame("schema-version", value.SchemaVersion.ToString()),
            Frame("semantic-input-revisions", string.Concat(value.SemanticInputRevisionIds
                .Select(item => item.Value).Order(StringComparer.Ordinal).Select(item => Frame("revision", item)))),
            Frame("policies", string.Concat(value.Policies.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => Frame("policy", Frame("key", item.Key) + Frame("value", item.Value))))),
        ];
        return new Sha256Fingerprint(Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(fields)))));
    }

    public static void Validate(SemanticAnalysisContextContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaVersion.Major < 1
            || value.ContextId.Value.Length > 128
            || value.SemanticInputRevisionIds.Count > 128
            || value.SemanticInputRevisionIds.Distinct().Count() != value.SemanticInputRevisionIds.Count
            || value.Policies.Count > 128
            || value.Policies.Any(item => string.IsNullOrWhiteSpace(item.Key)
                || item.Key.Length > 128 || item.Value.Length > 1024)
            || value.CanonicalFingerprint != ComputeFingerprint(value))
        {
            throw new InvalidDataException("The semantic analysis context identity is malformed or drifted.");
        }
    }

    private static string Frame(string label, string value) =>
        label.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + label
        + value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;
}
