using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.ScopeReversion;

public static class ScopeReversionIdentity
{
    public static OpaqueId StableId(string prefix, params string[] parts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        string canonical = string.Join("\u001f", parts.Prepend(prefix));
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new OpaqueId(prefix + "-" + hash[..32]);
    }
}
