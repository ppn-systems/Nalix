
using System;
using System.Text;
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
        packet.Initialize(14, ControlType.HEARTBEAT, 55, ProtocolReason.NONE, ProtocolType.TCP);
        return packet;
    }

    private static Directive CreateDirectivePacket()
    {
        Directive packet = new();
        packet.Initialize(91, ControlType.THROTTLE, ProtocolReason.THROTTLED, ProtocolAdvice.SLOW_DOWN, 12, ControlFlags.SLOW_DOWN, 9, 8, 7);
        return packet;
    }

    private static Handshake CreateHandshakePacket()
    {
        Span<byte> pubKey = stackalloc byte[32]; pubKey[0] = 1; pubKey[1] = 2; pubKey[2] = 3; pubKey[3] = 4;
        Span<byte> nonce = stackalloc byte[32]; nonce[0] = 5; nonce[1] = 6; nonce[2] = 7; nonce[3] = 8;
        Span<byte> proof = stackalloc byte[32]; proof[0] = 9; proof[1] = 10; proof[2] = 11; proof[3] = 12;
        Span<byte> hash = stackalloc byte[32]; hash[0] = 13; hash[1] = 14; hash[2] = 15; hash[3] = 16;
        
        Handshake packet = new(HandshakeStage.SERVER_HELLO, new Fixed256(pubKey), new Fixed256(nonce), new Fixed256(proof), ProtocolType.UDP);
        packet.TranscriptHash = new Fixed256(hash);
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
                    Assert.Equal(expectedControl.Protocol, actualControl.Protocol);
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
                    Assert.Equal(expectedHandshake.Protocol, actualHandshake.Protocol);
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
