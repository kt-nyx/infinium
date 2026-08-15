using System.Buffers.Binary;
using System.Text.Json;

namespace Infinium.Application.Runtime;

public sealed record NativeHelperFailureEnvelope(
    string Stage,
    string Reason,
    bool CallCountsKnown,
    int CredWriteW,
    int CredReadW,
    int CredDeleteW,
    int CredFree,
    int Total,
    bool NetworkFactsKnown,
    int ListenerCount,
    int NetworkOperationCount,
    bool ExternalEffectFactsKnown,
    int DnsOperationCount,
    int ProviderOperationCount,
    int BillableOperationCount,
    string? NativeCallTraceJson,
    string? EntryCleanupJson,
    string? CanaryEvidenceJson,
    bool ManualUiAttempted,
    bool ContainmentDescendantStarted,
    int ContainmentDescendantProcessId,
    bool NamespaceReuseBlocked,
    string? NamespaceReuseBlockReason);

public static class NativeHelperRuntimeMetricsProtocol
{
    public const int MaximumBytes = 64 * 1024;

    public static int ValidateLength(uint length)
    {
        if (length is 0 or > MaximumBytes)
        {
            throw new InvalidDataException(
                "The native helper runtime measurement record is outside its closed byte bound.");
        }
        return checked((int)length);
    }
}

public static class NativeHelperFailureProtocol
{
    public const int MaximumBytes = 64 * 1024;
    private static readonly byte[] Magic = "NHF2"u8.ToArray();
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    private static readonly HashSet<string> Stages = new(StringComparer.Ordinal)
    {
        "handle-inheritance", "launch-boundary", "manifest-validation", "engine-execution",
        "evidence-collection", "metrics-write",
    };
    private static readonly HashSet<string> Reasons = new(StringComparer.Ordinal)
    {
        "handle-inheritance-failure", "launch-options-invalid", "manifest-rejected",
        "containment-launch-failure",
        "io-failure", "invalid-data", "invalid-operation",
        "cancelled", "timeout", "win32-failure", "controlled-failure",
    };

    public static bool IsMagic(ReadOnlySpan<byte> prefix) => prefix.SequenceEqual(Magic);

    public static async Task WriteAsync(
        Stream stream,
        NativeHelperFailureEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Validate(envelope);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(envelope, Json);
        if (payload.Length is 0 or > MaximumBytes)
        {
            throw new InvalidDataException("The native helper failure envelope exceeds its closed byte bound.");
        }
        byte[] frame = new byte[checked(8 + payload.Length)];
        Magic.CopyTo(frame, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), checked((uint)payload.Length));
        payload.CopyTo(frame, 8);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<NativeHelperFailureEnvelope> ReadAfterMagicAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] lengthBytes = new byte[4];
        await ReadExactlyAsync(stream, lengthBytes, cancellationToken).ConfigureAwait(false);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
        if (length is 0 or > MaximumBytes)
        {
            throw new InvalidDataException("The native helper failure envelope length is outside its closed bound.");
        }
        byte[] payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        NativeHelperFailureEnvelope envelope = JsonSerializer.Deserialize<NativeHelperFailureEnvelope>(payload, Json)
            ?? throw new InvalidDataException("The native helper failure envelope is absent.");
        Validate(envelope);
        if (!payload.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(envelope, Json)))
        {
            throw new InvalidDataException("The native helper failure envelope is not canonical.");
        }
        return envelope;
    }

    private static void Validate(NativeHelperFailureEnvelope value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Stages.Contains(value.Stage) || !Reasons.Contains(value.Reason)
            || value.CredWriteW < 0 || value.CredReadW < 0 || value.CredDeleteW < 0
            || value.CredFree < 0 || value.Total < 0 || value.ListenerCount < 0
            || value.NetworkOperationCount < 0 || value.DnsOperationCount < 0
            || value.ProviderOperationCount < 0 || value.BillableOperationCount < 0
            || !value.NetworkFactsKnown && (value.ListenerCount != 0 || value.NetworkOperationCount != 0)
            || !value.ExternalEffectFactsKnown && (value.DnsOperationCount != 0
                || value.ProviderOperationCount != 0 || value.BillableOperationCount != 0)
            || value.CallCountsKnown && value.Total != checked(
                value.CredWriteW + value.CredReadW + value.CredDeleteW + value.CredFree)
            || !value.CallCountsKnown && (value.CredWriteW != 0 || value.CredReadW != 0
                || value.CredDeleteW != 0 || value.CredFree != 0 || value.Total != 0
                || value.NativeCallTraceJson is not null)
            || value.CallCountsKnown && value.NativeCallTraceJson is null
            || !value.ManualUiAttempted && value.EntryCleanupJson is not null
            || value.ContainmentDescendantStarted != (value.ContainmentDescendantProcessId > 0)
            || value.NamespaceReuseBlockReason is not (null or "preflight-collision"
                or "cleanup-outcome-ambiguous-or-failed" or "injected-control-flow-proof")
            || value.NamespaceReuseBlocked != (value.NamespaceReuseBlockReason is not null))
        {
            throw new InvalidDataException("The native helper failure envelope is semantically invalid.");
        }
        ValidateJson(value.NativeCallTraceJson);
        ValidateJson(value.EntryCleanupJson);
        ValidateJson(value.CanaryEvidenceJson);
    }

    private static void ValidateJson(string? value)
    {
        if (value is null)
        {
            return;
        }
        if (value.Length > MaximumBytes)
        {
            throw new InvalidDataException("A native helper failure evidence member exceeds its closed bound.");
        }
        using JsonDocument document = JsonDocument.Parse(value);
        _ = document.RootElement.ValueKind;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int consumed = 0;
        while (consumed < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[consumed..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The native helper failure frame is truncated.");
            }
            consumed += read;
        }
    }
}
