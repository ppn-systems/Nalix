
using System;
using System.Collections.Generic;
using System.Text;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.DataFrames.SignalFrames;

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

    public static TheoryData<TextFrameKind, string, ProtocolType, int> TextFrameInitializeValidCases()
    {
        return new TheoryData<TextFrameKind, string, ProtocolType, int>
        {
        };
    }

    public static TheoryData<TextFrameKind> TextFrameInitializeOverflowCases()
        =>
        [
        ];

    public static TheoryData<TextFrameKind> TextFrameResetCases()
    {
        return
        [
        ];
    }

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
        for (int i = 0; i < FrameTransformer.Offset; i++) buffer[i] = (byte)(i + 1);
        
        payloadBytes.CopyTo(buffer, FrameTransformer.Offset);
        return buffer;
    }

    public static IEnumerable<object[]> PacketSerializeBufferTooSmallCases()
    {
        Control control = new();
        control.Initialize(8, ControlType.PING, 1, ProtocolReason.NONE, ProtocolType.TCP);
        yield return [control];

        Directive directive = new();
        directive.Initialize(ControlType.ACK, ProtocolReason.NONE, ProtocolAdvice.NONE, 1);
        yield return [directive];

        yield return [new Handshake(5, HandshakeStage.CLIENT_HELLO, [1, 2, 3], [4, 5, 6], transport: ProtocolType.TCP)];
    }


    private static FrameBase CreateAndInitializeTextFrame(TextFrameKind frameKind, string content, ProtocolType protocol)
    {
        return frameKind switch
        {
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };
    }

    private static FrameBase InitializeTextFrame(FrameBase frame, string content, ProtocolType protocol)
    {
        switch (frame)
        {
            default:
                throw new InvalidOperationException("Unexpected text frame type.");
        }
    }

    private static int GetTextFrameDynamicSize(TextFrameKind frameKind)
        => frameKind switch
        {
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };

    private static char GetOverflowFillCharacter(TextFrameKind frameKind)
        => frameKind switch
        {
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };

    private static FrameBase CreateDirtyTextFrame(TextFrameKind frameKind)
        => frameKind switch
        {
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };


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
        Handshake packet = new(17, HandshakeStage.SERVER_HELLO, [1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 12], ProtocolType.UDP);
        packet.UpdateTranscriptHash([13, 14, 15, 16]);
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
