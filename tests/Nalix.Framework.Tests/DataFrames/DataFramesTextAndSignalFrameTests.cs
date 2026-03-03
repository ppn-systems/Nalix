
using System;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
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
        Handshake packet = new(HandshakeStage.CLIENT_HELLO, new byte[32], new byte[32], transport: ProtocolType.UDP);

        packet.ResetForPool();

        Assert.NotNull(packet.PublicKey);
        Assert.NotNull(packet.Nonce);
        Assert.NotNull(packet.Proof);
        Assert.NotNull(packet.TranscriptHash);
        Assert.Equal(Handshake.DynamicSize, packet.PublicKey.Length);
        Assert.Equal(Handshake.DynamicSize, packet.Nonce.Length);
        Assert.Equal(Handshake.DynamicSize, packet.Proof.Length);
        Assert.Equal(Handshake.DynamicSize, packet.TranscriptHash.Length);
        
        Assert.All(packet.PublicKey, b => Assert.Equal(0, b));
        Assert.All(packet.Nonce, b => Assert.Equal(0, b));
        Assert.All(packet.Proof, b => Assert.Equal(0, b));
        Assert.All(packet.TranscriptHash, b => Assert.Equal(0, b));
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
        Handshake packet = new(HandshakeStage.SERVER_HELLO, new byte[32], new byte[32], new byte[32], ProtocolType.UDP);
        packet.UpdateTranscriptHash([1, 2, 3, 4, 5]);

        byte[] bytes = packet.Serialize();

        Assert.Equal(bytes.Length, packet.Length);
    }

    [Fact]
    public void HandshakeSerializeIntoLengthSizedBufferWhenHandshakePayloadExistsSucceeds()
    {
        Handshake packet = new(HandshakeStage.SERVER_HELLO, new byte[32], new byte[32], new byte[32], ProtocolType.UDP);
        packet.UpdateTranscriptHash([1, 2, 3, 4, 5]);

        byte[] buffer = new byte[packet.Length];
        int written = packet.Serialize(buffer);

        Assert.Equal(packet.Length, written);
    }
}
