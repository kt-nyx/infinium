using System.Security.Cryptography;
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
            ProtocolConstants.DomainContractVersion,
            ProtocolConstants.StorageContractVersion,
            elevated ? "elevated" : "standard-user",
            now);
    }

    public static string GetPath(string productRoot) =>
        Path.Combine(Path.GetFullPath(productRoot), "runtime", FileName);

    public static RuntimeDescriptor Read(string productRoot)
    {
        const int MaximumDescriptorBytes = 32_768;
        string path = GetPath(productRoot);
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaximumDescriptorBytes)
        {
            throw new InvalidOperationException("The runtime descriptor exceeds its bound.");
        }

        byte[] bytes = new byte[MaximumDescriptorBytes + 1];
        int total = 0;
        while (total < bytes.Length)
        {
            int read = stream.Read(bytes, total, bytes.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total > MaximumDescriptorBytes)
        {
            throw new InvalidOperationException("The runtime descriptor exceeds its bound.");
        }

        return JsonSerializer.Deserialize<RuntimeDescriptor>(bytes.AsSpan(0, total))
            ?? throw new InvalidOperationException("The runtime descriptor is malformed.");
    }

    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, IndentedJson);
}
