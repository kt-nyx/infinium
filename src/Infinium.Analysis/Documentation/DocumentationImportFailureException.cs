using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Documentation;

public sealed class DocumentationImportFailureException : Exception
{
    public DocumentationImportFailureException(string failureCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (message.Length > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Documentation diagnostics are bounded to 512 characters.");
        }

        string material = failureCode + "\n" + message;
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..32];
        Failure = new DocumentationFailureContract(
            new OpaqueId("docfailure-" + digest),
            failureCode,
            message,
            false);
    }

    public DocumentationFailureContract Failure { get; }
}
