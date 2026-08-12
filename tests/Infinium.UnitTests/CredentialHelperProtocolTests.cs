using System.Buffers.Binary;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.CredentialHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialHelperProtocolTests
{
    [TestMethod]
    public void HelperPrivateProtocolStrictRoundTripAndSessionAreOneShot()
    {
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap();
        byte[] encoded = HelperPrivateProtocolV2.Encode(bootstrap);
        HelperPrivateFrameV2 decoded = HelperPrivateProtocolV2.Decode(encoded, 1);
        Assert.AreEqual(bootstrap, decoded);

        HelperPrivateSessionV2 session = new();
        session.Admit(decoded);
        session.Admit(HelperTestFrames.Assignment());
        HelperReceiptV2 receipt = new()
        {
            AssignmentKind = HelperAssignmentKindV2.Enroll,
            Outcome = HelperOutcomeV2.Completed,
        };
        session.Admit(new HelperPrivateFrameV2
        {
            Sequence = 3,
            ProtocolFingerprintSha256 = HelperTestFrames.Fingerprint(),
            Receipt = receipt,
        });
        Assert.IsTrue(session.IsTerminal);
        Assert.ThrowsExactly<InvalidDataException>(() => session.Admit(HelperTestFrames.Bootstrap(4)));
    }

    [TestMethod]
    public void HelperPrivateProtocolRejectsUnknownDuplicateOneofOrderSequenceAndBounds()
    {
        byte[] canonical = HelperPrivateProtocolV2.Encode(HelperTestFrames.Bootstrap());
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(canonical, 2));

        byte[] unknown = AppendPayload(canonical, [0x80, 0x05, 0x00]);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(unknown, 1));
        byte[] duplicate = InsertPayload(canonical, 2, [0x08, 0x01]);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(duplicate, 1));
        byte[] conflictingOneof = AppendPayload(canonical, [0x5a, 0x00]);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(conflictingOneof, 1));
        byte[] outOfOrder = AppendPayload(canonical, [0x08, 0x01]);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(outOfOrder, 1));

        byte[] oversized = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(oversized, HelperPrivateProtocolV2.MaximumMessageBytes + 1U);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(oversized, 1));
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode([1, 0, 0], 1));
    }

    [TestMethod]
    public void HelperPrivateProtocolRejectsWrongWireTypeForEveryKnownFieldRecursively()
    {
        HashSet<MessageDescriptor> visited = [];
        Queue<MessageDescriptor> pending = new([HelperPrivateFrameV2.Descriptor]);
        int tested = 0;
        while (pending.TryDequeue(out MessageDescriptor? descriptor))
        {
            if (!visited.Add(descriptor))
            {
                continue;
            }
            foreach (FieldDescriptor field in descriptor.Fields.InFieldNumberOrder())
            {
                WireFormat.WireType expected = Expected(field.FieldType);
                WireFormat.WireType wrong = expected == WireFormat.WireType.Varint
                    ? WireFormat.WireType.LengthDelimited
                    : WireFormat.WireType.Varint;
                using MemoryStream bytes = new();
                using (CodedOutputStream output = new(bytes, leaveOpen: true))
                {
                    output.WriteTag(field.FieldNumber, wrong);
                    if (wrong == WireFormat.WireType.LengthDelimited)
                    {
                        output.WriteBytes(ByteString.Empty);
                    }
                    else
                    {
                        output.WriteUInt64(0);
                    }
                }
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    HelperPrivateProtocolV2.ValidateCanonicalPayloadForTesting(bytes.ToArray(), descriptor),
                    $"{descriptor.FullName}.{field.Name}");
                tested++;
                if (field.FieldType == FieldType.Message)
                {
                    pending.Enqueue(field.MessageType);
                }
            }
        }
        Assert.IsGreaterThanOrEqualTo(100, tested);

        static WireFormat.WireType Expected(FieldType type) => type switch
        {
            FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => WireFormat.WireType.Fixed64,
            FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => WireFormat.WireType.Fixed32,
            FieldType.String or FieldType.Bytes or FieldType.Message => WireFormat.WireType.LengthDelimited,
            _ => WireFormat.WireType.Varint,
        };
    }

    [TestMethod]
    public void CredentialFakeStoreIsExactBoundedUnavailableAndNeverEnumeratesNativeState()
    {
        using DeterministicFakeSecureStore store = new();
        SyntheticCredentialSlot slot = new("profile", "generation");
        byte[] canary = new byte[32];
        store.WriteExact(slot, canary);
        Assert.IsTrue(store.VerifyExact(slot));
        CollectionAssert.AreEqual(canary, store.ReadExact(slot));
        Assert.IsTrue(store.DeleteExact(slot));
        Assert.IsFalse(store.VerifyExact(slot));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            store.WriteExact(slot, new byte[DeterministicFakeSecureStore.MaximumSecretBytes + 1]));
        store.Available = false;
        Assert.ThrowsExactly<IOException>(() => store.VerifyExact(slot));
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
        Assert.AreEqual(0, DeterministicFakeSecureStore.EnumerationCount);
    }

    [TestMethod]
    public void HelperDispatchFinalRevalidationRetainsGenerationRevocationDeadlineFenceAndBudget()
    {
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.DispatchBootstrap();
        HelperPrivateFrameV2 assignment = HelperTestFrames.DispatchAssignment();
        HelperPrivateFrameV2 accepted = HelperTestFrames.Revalidation();
        HelperExecutionSemanticsV2.ValidateBootstrapAndAssignment(bootstrap.Bootstrap, assignment.Assignment);
        HelperExecutionSemanticsV2.ValidateFinalRevalidation(
            bootstrap.Bootstrap, assignment.Assignment, accepted.DispatchRevalidation);

        AssertRejected(x => x.GenerationId.Value = "generation-stale");
        AssertRejected(x => x.RevocationEpoch++);
        AssertRejected(x => x.DispatchDeadline.UnixSeconds--);
        AssertRejected(x => x.CoordinatorFencingEpoch++);
        AssertRejected(x => x.Limits.MaximumCalculatedNanoUsd++);
        AssertRejected(x => x.RequestFingerprintSha256 = Google.Protobuf.ByteString.CopyFrom(new byte[32]));

        HelperPrivateFrameV2 reboundBootstrap = bootstrap.Clone();
        reboundBootstrap.Bootstrap.ProviderDispatch.AttemptId.Value = "attempt-other";
        Assert.ThrowsExactly<InvalidDataException>(() =>
            HelperExecutionSemanticsV2.ValidateBootstrapAndAssignment(
                reboundBootstrap.Bootstrap, assignment.Assignment));

        void AssertRejected(Action<DispatchRevalidationV2> mutate)
        {
            HelperPrivateFrameV2 candidate = accepted.Clone();
            mutate(candidate.DispatchRevalidation);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                HelperExecutionSemanticsV2.ValidateFinalRevalidation(
                    bootstrap.Bootstrap, assignment.Assignment, candidate.DispatchRevalidation));
        }
    }

    private static byte[] AppendPayload(byte[] frame, byte[] suffix)
    {
        byte[] result = new byte[frame.Length + suffix.Length];
        frame.CopyTo(result, 0);
        suffix.CopyTo(result, frame.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)(result.Length - 4)));
        return result;
    }

    private static byte[] InsertPayload(byte[] frame, int payloadOffset, byte[] bytes)
    {
        byte[] result = new byte[frame.Length + bytes.Length];
        frame.AsSpan(0, 4 + payloadOffset).CopyTo(result);
        bytes.CopyTo(result, 4 + payloadOffset);
        frame.AsSpan(4 + payloadOffset).CopyTo(result.AsSpan(4 + payloadOffset + bytes.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)(result.Length - 4)));
        return result;
    }
}
