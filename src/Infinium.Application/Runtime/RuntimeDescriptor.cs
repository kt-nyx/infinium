using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace Infinium.Application.Runtime;

public sealed record RuntimeDescriptor(
    int SchemaVersion,
    string CoordinatorInstanceId,
    long FencingEpoch,
    int ProcessId,
    string ApplicationPipe,
    string WorkerPipe,
    string InstanceNonceBase64,
    string ProtocolVersion,
    string DomainContractVersion,
    string StorageContractVersion,
    string Elevation,
    DateTimeOffset CreatedAt)
{
    public const string FileName = "coordinator.v1.json";
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public byte[] GetNonce() => Convert.FromBase64String(InstanceNonceBase64);

    public static RuntimeDescriptor Create(
        string instanceId,
        long fencingEpoch,
        int processId,
        bool elevated,
        DateTimeOffset now)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(32);
        string suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        return new RuntimeDescriptor(
            1,
            instanceId,
            fencingEpoch,
            processId,
            $"infinium-{instanceId}-{suffix}-application",
            $"infinium-{instanceId}-{suffix}-worker",
            Convert.ToBase64String(nonce),
            $"{ProtocolConstants.Major}.{ProtocolConstants.Minor}",
            ProtocolConstants.ContractVersion,
            ProtocolConstants.ContractVersion,
            elevated ? "elevated" : "standard-user",
            now);
    }

    public static string GetPath(string productRoot) =>
        Path.Combine(Path.GetFullPath(productRoot), "runtime", FileName);

    public static RuntimeDescriptor Read(string productRoot)
    {
        string path = GetPath(productRoot);
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > 32_768)
        {
            throw new InvalidOperationException("The runtime descriptor exceeds its bound.");
        }

        return JsonSerializer.Deserialize<RuntimeDescriptor>(bytes)
            ?? throw new InvalidOperationException("The runtime descriptor is malformed.");
    }

    public void WriteRestrictedToAuthorizedPath(string authorizedDescriptorPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedDescriptorPath);
        if (!Path.IsPathFullyQualified(authorizedDescriptorPath)
            || !string.Equals(
                Path.GetFileName(authorizedDescriptorPath),
                FileName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The authorized runtime-descriptor path is invalid.");
        }

        string path = Path.GetFullPath(authorizedDescriptorPath);
        string runtime = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "The authorized runtime-descriptor directory is unavailable.");
        if (!Directory.Exists(runtime))
        {
            throw new InvalidOperationException(
                "The authorized runtime-descriptor directory does not exist.");
        }

        if ((File.GetAttributes(runtime) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("A reparse-point runtime directory is not authorized.");
        }

        if (File.Exists(path)
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("A reparse-point runtime descriptor is not authorized.");
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            this,
            IndentedJson);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            RestrictToCurrentUser(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void RestrictToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier sid = identity.User
            ?? throw new InvalidOperationException("The current Windows identity has no SID.");
        FileSecurity security = new();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            sid,
            FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete,
            AccessControlType.Allow));
        FileInfo file = new(path);
        file.SetAccessControl(security);
    }
}
