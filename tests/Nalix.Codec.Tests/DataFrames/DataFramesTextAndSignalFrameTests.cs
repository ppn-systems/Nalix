using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.Codec.ProtocolFrames;

namespace Nalix.Codec.Tests.DataFrames;

public sealed partial class DataFramesPublicApiTests
{

    [Fact]
    public void InitializeControlPacketUpdatesPublicProperties()
    {
        Control packet = new();

        packet.Initialize(ControlType.PING, 42, PacketFlags.SYSTEM, ProtocolReason.TIMEOUT);
        PacketHeader h = packet.Header;
        h.OpCode = 123;
        packet.Header = h;

        Assert.Equal((ushort)123, packet.Header.OpCode);
        Assert.Equal(ControlType.PING, packet.Type);
        Assert.Equal(42u, packet.Header.SequenceId);
        Assert.Equal(ProtocolReason.TIMEOUT, packet.Reason);
        Assert.True(packet.Header.Flags.HasFlag(PacketFlags.NONE));
        Assert.Equal(PacketPriority.HIGH, packet.Header.Priority);
    }

    [Fact]
    public void ResetForPoolWhenControlPacketWasInitializedRestoresControlDefaults()
    {
        Control packet = new();
        packet.Initialize(ControlType.ERROR, 7, PacketFlags.SYSTEM, ProtocolReason.INTERNAL_ERROR);
        PacketHeader h2 = packet.Header;
        h2.OpCode = 555;
        packet.Header = h2;
        packet.Header = new PacketHeader { Flags = PacketFlags.SYSTEM };

        packet.ResetForPool();

        Assert.Equal(ControlType.NONE, packet.Type);
        Assert.Equal(ProtocolReason.NONE, packet.Reason);
        Assert.Equal(0u, packet.Header.SequenceId);
        Assert.Equal(PacketPriority.HIGH, packet.Header.Priority);
        Assert.Equal(PacketFlags.SYSTEM, packet.Header.Flags);
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
            _ => throw new InvalidOperationException("Unexpected frame type.")
        };

        AssertRoundTripPacketEquivalent(packetKind, original, deserialized);
    }

    [Fact]
    public void InitializeDirectivePacketUpdatesPublicProperties()
    {
        Directive packet = new();

        packet.Initialize(
            type: ControlType.REDIRECT,
            reason: ProtocolReason.REDIRECT,
            action: ProtocolAdvice.RECONNECT,
            sequenceId: 99,
            flags: PacketFlags.SYSTEM,
            controlFlags: ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT,
            arg0: 1000,
            arg1: 2000,
            arg2: 33);
        PacketHeader h3 = packet.Header;
        h3.OpCode = 77;
        packet.Header = h3;

        Assert.Equal((ushort)77, packet.Header.OpCode);
        Assert.Equal(ControlType.REDIRECT, packet.Type);
        Assert.Equal(ProtocolReason.REDIRECT, packet.Reason);
        Assert.Equal(ProtocolAdvice.RECONNECT, packet.Action);
        Assert.Equal(99u, packet.Header.SequenceId);
        Assert.Equal(ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT, packet.Control);
        Assert.Equal(1000u, packet.Arg0);
        Assert.Equal(2000u, packet.Arg1);
        Assert.Equal((ushort)33, packet.Arg2);
        Assert.Equal(PacketPriority.HIGH, packet.Header.Priority);
        Assert.True(packet.Header.Flags.HasFlag(PacketFlags.NONE));
    }



    [Fact]
    public void ControlFixedSizeMatchesComputedLengthAndSerializedBytes()
    {
        Control packet = new();
        packet.Initialize(ControlType.PING, 42, PacketFlags.SYSTEM, ProtocolReason.TIMEOUT);
        PacketHeader h4 = packet.Header;
        h4.OpCode = 123;
        packet.Header = h4;

        byte[] bytes = packet.Serialize();

        Assert.Equal(Control.Size, packet.Length);
        Assert.Equal(Control.Size, bytes.Length);
    }

    [Fact]
    public void DirectiveFixedSizeMatchesComputedLengthAndSerializedBytes()
    {
        Directive packet = new();
        packet.Initialize(
            type: ControlType.REDIRECT,
            reason: ProtocolReason.REDIRECT,
            action: ProtocolAdvice.RECONNECT,
            sequenceId: 99,
            flags: PacketFlags.SYSTEM,
            controlFlags: ControlFlags.HAS_REDIRECT | ControlFlags.IS_TRANSIENT,
            arg0: 1000,
            arg1: 2000,
            arg2: 33);
        PacketHeader h5 = packet.Header;
        h5.OpCode = 77;
        packet.Header = h5;

        byte[] bytes = packet.Serialize();

        Assert.Equal(Directive.Size, packet.Length);
        Assert.Equal(Directive.Size, bytes.Length);
    }



    [Fact]
    public void SessionResumeSerializeThenDeserializePreservesPublicState()
    {
        SessionResume original = new();
        original.Initialize(
            SessionResumeStage.REQUEST,
            0UL,
            ProtocolReason.NONE,
            Bytes32.Zero,
            PacketFlags.SYSTEM);

        byte[] bytes = original.Serialize();
        SessionResume deserialized = SessionResume.Deserialize(bytes);

        Assert.Equal(original.Header.OpCode, deserialized.Header.OpCode);
        Assert.Equal(original.Stage, deserialized.Stage);
        Assert.Equal(original.SessionToken, deserialized.SessionToken);
        Assert.Equal(original.Reason, deserialized.Reason);
        Assert.Equal(original.Proof, deserialized.Proof);
        Assert.Equal(original.Header.Flags, deserialized.Header.Flags);
        Assert.Equal(original.Header.Priority, deserialized.Header.Priority);
    }

    [Fact]
    public void ResetForPoolWhenSessionResumeContainsDataRestoresDefaults()
    {
        SessionResume packet = new();
        packet.Initialize(
            SessionResumeStage.RESPONSE,
            0UL,
            ProtocolReason.TIMEOUT,
            Bytes32.Zero,
            PacketFlags.SYSTEM);
        PacketHeader h = packet.Header;
        h.Flags = PacketFlags.SYSTEM;
        packet.Header = h;

        packet.ResetForPool();

        Assert.Equal((ushort)ProtocolOpCode.SESSION_SIGNAL, packet.Header.OpCode);
        Assert.Equal(SessionResumeStage.NONE, packet.Stage);
        Assert.Equal(0UL, packet.SessionToken);
        Assert.Equal(ProtocolReason.NONE, packet.Reason);
        Assert.True(packet.Proof.IsZero);
        Assert.Equal(PacketFlags.SYSTEM, packet.Header.Flags);
        Assert.Equal(PacketPriority.URGENT, packet.Header.Priority);
    }

    [Fact]
    public void DeserializeHelpersAcceptReadOnlySpanForAllSignalFrames()
    {
        byte[] controlBytes = CreateControlPacket().Serialize();
        byte[] directiveBytes = CreateDirectivePacket().Serialize();

        SessionResume resume = new();
        resume.Initialize(SessionResumeStage.REQUEST, 0UL, ProtocolReason.NONE, Bytes32.Zero, PacketFlags.SYSTEM);
        byte[] sessionResumeBytes = resume.Serialize();

        Control control = Control.Deserialize(controlBytes.AsSpan());
        Directive directive = Directive.Deserialize(directiveBytes.AsSpan());
        SessionResume sessionResume = SessionResume.Deserialize(sessionResumeBytes.AsSpan());

        Assert.Equal(ControlType.PING, control.Type);
        Assert.Equal(ControlType.REDIRECT, directive.Type);
        Assert.Equal(SessionResumeStage.REQUEST, sessionResume.Stage);
    }



    [Fact]
    public void ComputeTranscriptHashWhenGivenSameInputReturnsSameBytes32()
    {
        byte[] transcript = [1, 2, 3, 4, 5, 6, 7, 8, 9];

        Bytes32 first = Security.Hashing.Keccak256.HashDataToFixed(transcript);
        Bytes32 second = Security.Hashing.Keccak256.HashDataToFixed(transcript);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SessionResumeFixedSizeMatchesSerializedLength()
    {
        SessionResume packet = new();
        packet.Initialize(
            SessionResumeStage.REQUEST,
            0UL,
            ProtocolReason.NONE,
            Bytes32.Zero,
            PacketFlags.SYSTEM);

        byte[] bytes = packet.Serialize();

        Assert.Equal(SessionResume.Size, packet.Length);
        Assert.Equal(SessionResume.Size, bytes.Length);
    }
}



















