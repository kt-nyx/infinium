using System.Buffers.Binary;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.Application.Runtime;

/// <summary>
/// Canonical private-handle framing for the one-shot helper. This is not an
/// application IPC transport and deliberately has no negotiation surface.
/// </summary>
public static class HelperPrivateProtocolV2
{
    public const int PrefixBytes = sizeof(uint);
    public const int HistoricalMaximumMessageBytes = 1_000_000;
    public const int MaximumMessageBytes = 1_100_000;
    public const int MaximumStagingBytes = 4_194_304;

    private static readonly byte[] ProtocolFingerprint =
        Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256);

    public static byte[] Encode(HelperPrivateFrameV2 message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateEnvelope(message);
        byte[] payload = message.ToByteArray();
        int maximum = IsExtendedProfile(message) ? MaximumMessageBytes : HistoricalMaximumMessageBytes;
        if (payload.Length is 0 || payload.Length > maximum)
        {
            throw new InvalidDataException("The helper message exceeds its closed byte bound.");
        }

        byte[] frame = new byte[checked(PrefixBytes + payload.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)payload.Length));
        payload.CopyTo(frame, PrefixBytes);
        return frame;
    }

    public static HelperPrivateFrameV2 Decode(ReadOnlySpan<byte> frame, ulong expectedSequence)
    {
        if (frame.Length < PrefixBytes)
        {
            throw new InvalidDataException("The helper frame is truncated.");
        }

        uint declared = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        if (declared is 0 or > MaximumMessageBytes || frame.Length != PrefixBytes + declared)
        {
            throw new InvalidDataException("The helper frame length is non-canonical or out of bounds.");
        }

        byte[] payload = frame[PrefixBytes..].ToArray();
        RejectUnknownDuplicateAndNonCanonical(payload, HelperPrivateFrameV2.Descriptor);
        HelperPrivateFrameV2 result;
        try
        {
            result = HelperPrivateFrameV2.Parser.ParseFrom(payload);
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException("The helper protobuf payload is malformed.", exception);
        }

        ValidateEnvelope(result);
        int semanticMaximum = IsExtendedProfile(result) ? MaximumMessageBytes : HistoricalMaximumMessageBytes;
        if (payload.Length > semanticMaximum)
        { throw new InvalidDataException("The helper message exceeds its authority-version byte bound."); }
        if (result.Sequence != expectedSequence)
        {
            throw new InvalidDataException("The helper sequence is stale, skipped, or replayed.");
        }

        if (!payload.AsSpan().SequenceEqual(result.ToByteArray()))
        {
            throw new InvalidDataException("The helper payload is not in canonical protobuf field order/encoding.");
        }

        return result;
    }

    public static async Task WriteAsync(Stream stream, HelperPrivateFrameV2 message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] frame = Encode(message);
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<HelperPrivateFrameV2> ReadAsync(
        Stream stream,
        ulong expectedSequence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] prefix = new byte[PrefixBytes];
        await ReadExactlyAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        return await ReadAfterPrefixAsync(stream, prefix, expectedSequence, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<HelperPrivateFrameV2> ReadAfterPrefixAsync(
        Stream stream,
        ReadOnlyMemory<byte> prefix,
        ulong expectedSequence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (prefix.Length != PrefixBytes)
        {
            throw new InvalidDataException("The helper frame prefix has the wrong size.");
        }
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(prefix.Span);
        if (length is 0 or > MaximumMessageBytes)
        {
            throw new InvalidDataException("The helper message length is outside the closed bound.");
        }

        byte[] frame = new byte[checked(PrefixBytes + (int)length)];
        prefix.CopyTo(frame.AsMemory());
        await ReadExactlyAsync(stream, frame.AsMemory(PrefixBytes), cancellationToken).ConfigureAwait(false);
        return Decode(frame, expectedSequence);
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int consumed = 0;
        while (consumed < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[consumed..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("The private helper pipe closed before one complete frame.");
            }
            consumed += read;
        }
    }

    private static void ValidateEnvelope(HelperPrivateFrameV2 message)
    {
        if (message.Sequence == 0 || message.PayloadCase == HelperPrivateFrameV2.PayloadOneofCase.None)
        {
            throw new InvalidDataException("The helper frame requires one sequence and one payload.");
        }
        if (!message.ProtocolFingerprintSha256.Span.SequenceEqual(ProtocolFingerprint))
        {
            throw new InvalidDataException("The helper protocol fingerprint is incompatible.");
        }
    }

    private static bool IsExtendedProfile(HelperPrivateFrameV2 message)
    {
        string? requestId = message.PayloadCase switch
        {
            HelperPrivateFrameV2.PayloadOneofCase.Assignment => message.Assignment.ProviderRequest?.RequestId,
            HelperPrivateFrameV2.PayloadOneofCase.DispatchRevalidation => message.DispatchRevalidation.RequestId,
            HelperPrivateFrameV2.PayloadOneofCase.Receipt => message.Receipt.RequestId,
            _ => null,
        };
        return requestId?.StartsWith("m1-s6-successor-v6-", StringComparison.Ordinal) == true;
    }

    private static void RejectUnknownDuplicateAndNonCanonical(byte[] payload, MessageDescriptor descriptor)
    {
        CodedInputStream input = new(payload);
        HashSet<int> singular = [];
        Dictionary<OneofDescriptor, int> oneofs = [];
        int priorField = 0;
        while (!input.IsAtEnd)
        {
            uint tag = input.ReadTag();
            if (tag == 0)
            {
                throw new InvalidDataException("A zero protobuf tag is forbidden.");
            }
            int fieldNumber = WireFormat.GetTagFieldNumber(tag);
            FieldDescriptor? field = descriptor.FindFieldByNumber(fieldNumber);
            if (field is null)
            {
                throw new InvalidDataException($"Unknown helper field {fieldNumber} is forbidden recursively.");
            }
            if (fieldNumber < priorField)
            {
                throw new InvalidDataException("Helper fields must use canonical ascending order.");
            }
            priorField = fieldNumber;
            if (!field.IsRepeated && !singular.Add(fieldNumber))
            {
                throw new InvalidDataException($"Duplicate singular helper field {fieldNumber} is forbidden.");
            }
            if (field.ContainingOneof is not null)
            {
                if (oneofs.TryGetValue(field.ContainingOneof, out int prior) && prior != fieldNumber)
                {
                    throw new InvalidDataException("Conflicting helper oneof alternatives are forbidden.");
                }
                oneofs[field.ContainingOneof] = fieldNumber;
            }

            WireFormat.WireType wire = WireFormat.GetTagWireType(tag);
            WireFormat.WireType expectedWire = ExpectedWireType(field);
            if (wire != expectedWire)
            {
                throw new InvalidDataException(
                    $"Helper field {fieldNumber} used wire type {wire}; {expectedWire} is required.");
            }
            if (field.FieldType == FieldType.Message)
            {
                ByteString nestedValue = input.ReadBytes();
                byte[] nested = nestedValue.ToByteArray();
                RejectUnknownDuplicateAndNonCanonical(nested, field.MessageType);
            }
            else
            {
                SkipKnownValue(input, wire);
            }
        }
    }

    internal static void ValidateCanonicalPayloadForTesting(byte[] payload, MessageDescriptor descriptor) =>
        RejectUnknownDuplicateAndNonCanonical(payload, descriptor);

    private static WireFormat.WireType ExpectedWireType(FieldDescriptor field) => field.FieldType switch
    {
        FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => WireFormat.WireType.Fixed64,
        FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => WireFormat.WireType.Fixed32,
        FieldType.String or FieldType.Bytes or FieldType.Message => WireFormat.WireType.LengthDelimited,
        FieldType.Bool or FieldType.Enum or FieldType.Int32 or FieldType.Int64 or FieldType.UInt32
            or FieldType.UInt64 or FieldType.SInt32 or FieldType.SInt64 => WireFormat.WireType.Varint,
        _ => throw new InvalidDataException($"Unsupported helper protobuf field type {field.FieldType}."),
    };

    private static void SkipKnownValue(CodedInputStream input, WireFormat.WireType wire)
    {
        switch (wire)
        {
            case WireFormat.WireType.Varint:
                _ = input.ReadUInt64();
                break;
            case WireFormat.WireType.Fixed32:
                _ = input.ReadFixed32();
                break;
            case WireFormat.WireType.Fixed64:
                _ = input.ReadFixed64();
                break;
            case WireFormat.WireType.LengthDelimited:
                _ = input.ReadBytes();
                break;
            default:
                throw new InvalidDataException("Groups and unsupported wire types are forbidden.");
        }
    }
}

public sealed class HelperPrivateSessionV2
{
    private HelperSessionState state;
    private HelperAssignmentKindV2 assignmentKind;
    private ulong nextSequence = 1;

    public bool IsTerminal => state == HelperSessionState.Terminal;
    public ulong NextSequence => nextSequence;

    public void Admit(HelperPrivateFrameV2 frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Sequence != NextSequence)
        {
            throw new InvalidDataException("The helper session sequence is invalid.");
        }

        switch (state, frame.PayloadCase)
        {
            case (HelperSessionState.New, HelperPrivateFrameV2.PayloadOneofCase.Bootstrap):
                state = HelperSessionState.Bootstrapped;
                break;
            case (HelperSessionState.Bootstrapped, HelperPrivateFrameV2.PayloadOneofCase.Assignment):
                if (frame.Assignment.AssignmentKind == HelperAssignmentKindV2.Unspecified)
                {
                    throw new InvalidDataException("The one-shot assignment kind is required.");
                }
                assignmentKind = frame.Assignment.AssignmentKind;
                state = assignmentKind == HelperAssignmentKindV2.ProviderDispatch
                    ? HelperSessionState.AssignedDispatch
                    : HelperSessionState.AssignedCredential;
                break;
            case (HelperSessionState.AssignedDispatch, HelperPrivateFrameV2.PayloadOneofCase.DispatchRevalidation):
                state = HelperSessionState.Revalidated;
                break;
            case (HelperSessionState.AssignedCredential, HelperPrivateFrameV2.PayloadOneofCase.Receipt):
            case (HelperSessionState.Revalidated, HelperPrivateFrameV2.PayloadOneofCase.Receipt):
                if (frame.Receipt.AssignmentKind != assignmentKind)
                {
                    throw new InvalidDataException("The helper receipt reinterprets its immutable assignment.");
                }
                state = HelperSessionState.Terminal;
                break;
            default:
                throw new InvalidDataException("The helper permits exactly one bootstrap, assignment, operation, and terminal receipt.");
        }
        nextSequence = checked(nextSequence + 1);
    }

    private enum HelperSessionState
    {
        New,
        Bootstrapped,
        AssignedCredential,
        AssignedDispatch,
        Revalidated,
        Terminal,
    }
}
