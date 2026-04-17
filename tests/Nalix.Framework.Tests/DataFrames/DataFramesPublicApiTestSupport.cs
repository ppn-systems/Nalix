
using System;
using System.Text;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.DataFrames.Transforms;

using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed partial class DataFramesPublicApiTests
{
    public enum TextFrameKind
    {
    }

    public enum PacketRoundTripKind
    {
        Control,
        Directive,
        Handshake
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1825:Avoid zero-length array allocations", Justification = "<Pending>")]
    public static TheoryData<PacketRoundTripKind> PacketRoundTripCases()
    {
        return
        [
            PacketRoundTripKind.Control,
            PacketRoundTripKind.Directive,
            PacketRoundTripKind.Handshake
        ];
    }

    private static byte[] CreatePacketBytes(string payload)
    {
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        byte[] buffer = new byte[FrameTransformer.Offset + payloadBytes.Length];

        // Fill header with predictable dummy data that won't fail FrameTransformer checks
        for (int i = 0; i < FrameTransformer.Offset; i++)
        {
            buffer[i] = (byte)(i + 1);
        }

        payloadBytes.CopyTo(buffer, FrameTransformer.Offset);
        return buffer;
    }
    private static FrameBase CreateRoundTripPacket(PacketRoundTripKind packetKind)
        => packetKind switch
        {
            PacketRoundTripKind.Control => CreateControlPacket(),
            PacketRoundTripKind.Directive => CreateDirectivePacket(),
            PacketRoundTripKind.Handshake => CreateHandshakePacket(),
            _ => throw new InvalidOperationException("Unexpected packet round-trip kind.")
        };

    private static Control CreateControlPacket()
    {
        Control packet = new();
        packet.Initialize(14, ControlType.HEARTBEAT, 55, PacketFlags.SYSTEM | PacketFlags.RELIABLE, ProtocolReason.NONE);
        return packet;
    }

    private static Directive CreateDirectivePacket()
    {
        Directive packet = new();
        packet.Initialize(91, ControlType.THROTTLE, ProtocolReason.THROTTLED, ProtocolAdvice.SLOW_DOWN, 12, PacketFlags.SYSTEM | PacketFlags.RELIABLE, ControlFlags.SLOW_DOWN, 9, 8, 7);
        return packet;
    }

    private static Handshake CreateHandshakePacket()
    {
        Span<byte> pubKeyArr = stackalloc byte[32]; pubKeyArr[0] = 1; pubKeyArr[1] = 2; pubKeyArr[2] = 3; pubKeyArr[3] = 4;
        Span<byte> nonceArr = stackalloc byte[32]; nonceArr[0] = 5; nonceArr[1] = 6; nonceArr[2] = 7; nonceArr[3] = 8;
        Span<byte> proofArr = stackalloc byte[32]; proofArr[0] = 9; proofArr[1] = 10; proofArr[2] = 11; proofArr[3] = 12;
        Span<byte> hashArr = stackalloc byte[32]; hashArr[0] = 13; hashArr[1] = 14; hashArr[2] = 15; hashArr[3] = 16;
        
        Handshake packet = new(HandshakeStage.SERVER_HELLO, new Bytes32(pubKeyArr), new Bytes32(nonceArr), new Bytes32(proofArr), flags: PacketFlags.SYSTEM | PacketFlags.UNRELIABLE);
        packet.TranscriptHash = new Bytes32(hashArr);
        return packet;
    }


    private static void AssertRoundTripPacketEquivalent(PacketRoundTripKind packetKind, FrameBase expected, FrameBase actual)
    {
        switch (packetKind)
        {
            case PacketRoundTripKind.Control:
                {
                    Control expectedControl = Assert.IsType<Control>(expected);
                    Control actualControl = Assert.IsType<Control>(actual);
                    Assert.Equal(expectedControl.MagicNumber, actualControl.MagicNumber);
                    Assert.Equal(expectedControl.OpCode, actualControl.OpCode);
                    Assert.Equal(expectedControl.Type, actualControl.Type);
                    Assert.Equal(expectedControl.Reason, actualControl.Reason);
                    Assert.Equal(expectedControl.Flags, actualControl.Flags);
                    Assert.Equal(expectedControl.SequenceId, actualControl.SequenceId);
                    break;
                }
            case PacketRoundTripKind.Directive:
                {
                    Directive expectedDirective = Assert.IsType<Directive>(expected);
                    Directive actualDirective = Assert.IsType<Directive>(actual);
                    Assert.Equal(expectedDirective.OpCode, actualDirective.OpCode);
                    Assert.Equal(expectedDirective.Type, actualDirective.Type);
                    Assert.Equal(expectedDirective.Reason, actualDirective.Reason);
                    Assert.Equal(expectedDirective.Action, actualDirective.Action);
                    Assert.Equal(expectedDirective.Control, actualDirective.Control);
                    Assert.Equal(expectedDirective.Arg0, actualDirective.Arg0);
                    Assert.Equal(expectedDirective.Arg1, actualDirective.Arg1);
                    Assert.Equal(expectedDirective.Arg2, actualDirective.Arg2);
                    Assert.Equal(expectedDirective.SequenceId, actualDirective.SequenceId);
                    break;
                }
            case PacketRoundTripKind.Handshake:
                {
                    Handshake expectedHandshake = Assert.IsType<Handshake>(expected);
                    Handshake actualHandshake = Assert.IsType<Handshake>(actual);
                    Assert.Equal(expectedHandshake.OpCode, actualHandshake.OpCode);
                    Assert.Equal(expectedHandshake.Flags, actualHandshake.Flags);
                    Assert.Equal(expectedHandshake.Stage, actualHandshake.Stage);
                    Assert.Equal(expectedHandshake.PublicKey, actualHandshake.PublicKey);
                    Assert.Equal(expectedHandshake.Nonce, actualHandshake.Nonce);
                    Assert.Equal(expectedHandshake.Proof, actualHandshake.Proof);
                    Assert.Equal(expectedHandshake.TranscriptHash, actualHandshake.TranscriptHash);
                    break;
                }
            default:
                throw new InvalidOperationException("Unexpected packet round-trip kind.");
        }
    }

    private static byte[] CreateFragmentPayload(FragmentHeader header, ReadOnlySpan<byte> body)
    {
        byte[] payload = new byte[FragmentHeader.WireSize + body.Length];
        header.WriteTo(payload);
        body.CopyTo(payload.AsSpan(FragmentHeader.WireSize));
        return payload;
    }
}
