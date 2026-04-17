
using System;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed partial class DataFramesPublicApiTests
{

    [Fact]
    public void InitializeControlPacketUpdatesPublicProperties()
    {
        Control packet = new();

        packet.Initialize(123, ControlType.PING, 42, ProtocolReason.TIMEOUT, ProtocolType.UDP);

        Assert.Equal((ushort)123, packet.OpCode);
        Assert.Equal(ControlType.PING, packet.Type);
        Assert.Equal(42u, packet.SequenceId);
        Assert.Equal(ProtocolReason.TIMEOUT, packet.Reason);
        Assert.Equal(ProtocolType.UDP, packet.Protocol);
        Assert.Equal(PacketPriority.HIGH, packet.Priority);
        Assert.NotEqual(0L, packet.Timestamp);
        Assert.NotEqual(0L, packet.MonoTicks);
    }

    [Fact]
    public void ResetForPoolWhenControlPacketWasInitializedRestoresControlDefaults()
    {
        Control packet = new();
        packet.Initialize(555, ControlType.ERROR, 7, ProtocolReason.INTERNAL_ERROR, ProtocolType.UDP);
        packet.Flags = PacketFlags.SYSTEM;

        packet.ResetForPool();

        Assert.Equal(ControlType.NONE, packet.Type);
        Assert.Equal(ProtocolReason.NONE, packet.Reason);
        Assert.Equal(0u, packet.SequenceId);
        Assert.Equal(0L, packet.Timestamp);
        Assert.Equal(0L, packet.MonoTicks);
        Assert.Equal(PacketPriority.HIGH, packet.Priority);
        Assert.Equal(PacketFlags.NONE, packet.Flags);
        Assert.Equal(ProtocolType.NONE, packet.Protocol);
    }

    [Theory]
    [MemberData(nameof(PacketRoundTripCases))]
    public void SerializeThenDeserializePublicPacketPreservesPublicState(PacketRoundTripKind packetKind)
    {
        FrameBase original = CreateRoundTripPacket(packetKind);

        byte[] bytes = original.Serialize();
        FrameBase deserialized = original switch
        {
            Control => Control.Deserialize(bytes),
            Directive => Directive.Deserialize(bytes),
            Handshake => Handshake.Deserialize(bytes),
            _ => throw new InvalidOperationException("Unexpected frame type.")
        };

        AssertRoundTripPacketEquivalent(packetKind, original, deserialized);
    }

    [Fact]
    public void InitializeDirectivePacketUpdatesPublicProperties()
    {
        Directive packet = new();

        packet.Initialize(
            77,
            ControlType.REDIRECT,
            ProtocolReason.REDIRECT,
            ProtocolAdvice.RECONNECT,
            99,
            ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT,
            1000,
            2000,
            33);

        Assert.Equal((ushort)77, packet.OpCode);
        Assert.Equal(ControlType.REDIRECT, packet.Type);
        Assert.Equal(ProtocolReason.REDIRECT, packet.Reason);
        Assert.Equal(ProtocolAdvice.RECONNECT, packet.Action);
        Assert.Equal(99u, packet.SequenceId);
        Assert.Equal(ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT, packet.Control);
        Assert.Equal(1000u, packet.Arg0);
        Assert.Equal(2000u, packet.Arg1);
        Assert.Equal((ushort)33, packet.Arg2);
        Assert.Equal(PacketPriority.HIGH, packet.Priority);
        Assert.Equal(ProtocolType.TCP, packet.Protocol);
    }

    [Fact]
    public void ResetForPoolWhenHandshakeContainsDataClearsPayload()
    {
        Handshake packet = new(HandshakeStage.CLIENT_HELLO, Bytes32.Zero, Bytes32.Zero, transport: ProtocolType.UDP);

        packet.ResetForPool();

        Assert.True(packet.PublicKey.IsZero);
        Assert.True(packet.Nonce.IsZero);
        Assert.True(packet.Proof.IsZero);
        Assert.True(packet.TranscriptHash.IsZero);
        Assert.Equal(HandshakeStage.NONE, packet.Stage);
        Assert.Equal(PacketFlags.NONE, packet.Flags);
        Assert.Equal(PacketPriority.URGENT, packet.Priority);
        Assert.Equal(ProtocolType.NONE, packet.Protocol);
    }

    [Fact]
    public void ControlFixedSizeMatchesComputedLengthAndSerializedBytes()
    {
        Control packet = new();
        packet.Initialize(123, ControlType.PING, 42, ProtocolReason.TIMEOUT, ProtocolType.UDP);

        byte[] bytes = packet.Serialize();

        Assert.Equal(Control.Size, packet.Length);
        Assert.Equal(Control.Size, bytes.Length);
    }

    [Fact]
    public void DirectiveFixedSizeMatchesComputedLengthAndSerializedBytes()
    {
        Directive packet = new();
        packet.Initialize(
            77,
            ControlType.REDIRECT,
            ProtocolReason.REDIRECT,
            ProtocolAdvice.RECONNECT,
            99,
            ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT,
            1000,
            2000,
            33);

        byte[] bytes = packet.Serialize();

        Assert.Equal(Directive.Size, packet.Length);
        Assert.Equal(Directive.Size, bytes.Length);
    }

    [Fact]
    public void HandshakeLengthWhenHandshakePayloadExistsMatchesActualSerializedBytes()
    {
        Handshake packet = new(HandshakeStage.SERVER_HELLO, Bytes32.Zero, Bytes32.Zero, Bytes32.Zero, ProtocolType.UDP);
        packet.UpdateTranscriptHash([1, 2, 3, 4, 5]);

        byte[] bytes = packet.Serialize();

        Assert.Equal(bytes.Length, packet.Length);
    }

    [Fact]
    public void HandshakeSerializeIntoLengthSizedBufferWhenHandshakePayloadExistsSucceeds()
    {
        Handshake packet = new(HandshakeStage.SERVER_HELLO, Bytes32.Zero, Bytes32.Zero, Bytes32.Zero, ProtocolType.UDP);
        packet.UpdateTranscriptHash([1, 2, 3, 4, 5]);

        byte[] buffer = new byte[packet.Length];
        int written = packet.Serialize(buffer);

        Assert.Equal(packet.Length, written);
    }

    [Fact]
    public void SessionResumeSerializeThenDeserializePreservesPublicState()
    {
        SessionResume original = new();
        original.Initialize(
            SessionResumeStage.REQUEST,
            Nalix.Framework.Identifiers.Snowflake.Empty,
            ProtocolReason.NONE,
            ProtocolType.UDP,
            Bytes32.Zero);

        byte[] bytes = original.Serialize();
        SessionResume deserialized = SessionResume.Deserialize(bytes);

        Assert.Equal(original.OpCode, deserialized.OpCode);
        Assert.Equal(original.Stage, deserialized.Stage);
        Assert.Equal(original.SessionToken, deserialized.SessionToken);
        Assert.Equal(original.Reason, deserialized.Reason);
        Assert.Equal(original.Proof, deserialized.Proof);
        Assert.Equal(original.Protocol, deserialized.Protocol);
        Assert.Equal(original.Priority, deserialized.Priority);
    }

    [Fact]
    public void ResetForPoolWhenSessionResumeContainsDataRestoresDefaults()
    {
        SessionResume packet = new();
        packet.Initialize(
            SessionResumeStage.RESPONSE,
            Nalix.Framework.Identifiers.Snowflake.Empty,
            ProtocolReason.TIMEOUT,
            ProtocolType.UDP,
            Bytes32.Zero);
        packet.Flags = PacketFlags.SYSTEM;

        packet.ResetForPool();

        Assert.Equal((ushort)ProtocolOpCode.SESSION_SIGNAL, packet.OpCode);
        Assert.Equal(SessionResumeStage.NONE, packet.Stage);
        Assert.Equal(Nalix.Framework.Identifiers.Snowflake.Empty, packet.SessionToken);
        Assert.Equal(ProtocolReason.NONE, packet.Reason);
        Assert.True(packet.Proof.IsZero);
        Assert.Equal(PacketFlags.NONE, packet.Flags);
        Assert.Equal(ProtocolType.TCP, packet.Protocol);
        Assert.Equal(PacketPriority.URGENT, packet.Priority);
    }

    [Fact]
    public void DeserializeHelpersAcceptReadOnlySpanForAllSignalFrames()
    {
        byte[] controlBytes = CreateControlPacket().Serialize();
        byte[] directiveBytes = CreateDirectivePacket().Serialize();
        byte[] handshakeBytes = CreateHandshakePacket().Serialize();

        SessionResume resume = new();
        resume.Initialize(SessionResumeStage.REQUEST, Nalix.Framework.Identifiers.Snowflake.Empty, ProtocolReason.NONE, ProtocolType.TCP, Bytes32.Zero);
        byte[] sessionResumeBytes = resume.Serialize();

        Control control = Control.Deserialize(controlBytes.AsSpan());
        Directive directive = Directive.Deserialize(directiveBytes.AsSpan());
        Handshake handshake = Handshake.Deserialize(handshakeBytes.AsSpan());
        SessionResume sessionResume = SessionResume.Deserialize(sessionResumeBytes.AsSpan());

        Assert.Equal(ControlType.HEARTBEAT, control.Type);
        Assert.Equal(ControlType.THROTTLE, directive.Type);
        Assert.Equal(HandshakeStage.SERVER_HELLO, handshake.Stage);
        Assert.Equal(SessionResumeStage.REQUEST, sessionResume.Stage);
    }

    [Fact]
    public void InitializeErrorWhenCalledSetsHandshakeToErrorState()
    {
        Handshake packet = new();

        packet.InitializeError(ProtocolReason.INTERNAL_ERROR, ProtocolType.UDP);

        Assert.Equal((ushort)ProtocolOpCode.HANDSHAKE, packet.OpCode);
        Assert.Equal(HandshakeStage.ERROR, packet.Stage);
        Assert.Equal(ProtocolReason.INTERNAL_ERROR, packet.Reason);
        Assert.Equal(ProtocolType.UDP, packet.Protocol);
        Assert.Equal(PacketPriority.URGENT, packet.Priority);
        Assert.True(packet.PublicKey.IsZero);
        Assert.True(packet.Nonce.IsZero);
        Assert.True(packet.Proof.IsZero);
        Assert.True(packet.TranscriptHash.IsZero);
        Assert.Equal(Nalix.Framework.Identifiers.Snowflake.Empty, packet.SessionToken);
    }

    [Fact]
    public void HandshakeValidityWhenPublicKeyOrNonceMissingReturnsFalseOtherwiseTrue()
    {
        byte[] nonZeroKey = new byte[32];
        nonZeroKey[0] = 1;
        byte[] nonZeroNonce = new byte[32];
        nonZeroNonce[0] = 2;

        Handshake invalidKey = new(HandshakeStage.CLIENT_HELLO, Bytes32.Zero, new Bytes32(nonZeroNonce));
        Handshake invalidNonce = new(HandshakeStage.CLIENT_HELLO, new Bytes32(nonZeroKey), Bytes32.Zero);
        Handshake valid = new(HandshakeStage.CLIENT_HELLO, new Bytes32(nonZeroKey), new Bytes32(nonZeroNonce));

        Assert.False(Handshake.IsValid(invalidKey));
        Assert.False(Handshake.IsValid(invalidNonce));
        Assert.True(Handshake.IsValid(valid));
    }

    [Fact]
    public void ComputeTranscriptHashWhenGivenSameInputReturnsSameBytes32()
    {
        byte[] transcript = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        Bytes32 first = Handshake.ComputeTranscriptHash(transcript);
        Bytes32 second = Handshake.ComputeTranscriptHash(transcript);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SessionResumeFixedSizeMatchesSerializedLength()
    {
        SessionResume packet = new();
        packet.Initialize(
            SessionResumeStage.REQUEST,
            Nalix.Framework.Identifiers.Snowflake.Empty,
            ProtocolReason.NONE,
            ProtocolType.TCP,
            Bytes32.Zero);

        byte[] bytes = packet.Serialize();

        Assert.Equal(SessionResume.Size, packet.Length);
        Assert.Equal(SessionResume.Size, bytes.Length);
    }
}
