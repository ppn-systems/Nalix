
using System;
using System.Collections.Generic;
using System.Text;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.DataFrames.TextFrames;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed partial class DataFramesPublicApiTests
{
    public enum TextFrameKind
    {
        Text256,
        Text512,
        Text1024
    }

    public enum PacketRoundTripKind
    {
        Control,
        Directive,
        Handshake,
        Text256,
        Text512,
        Text1024
    }

    public static TheoryData<TextFrameKind, string, ProtocolType, int> TextFrameInitializeValidCases()
    {
        return new TheoryData<TextFrameKind, string, ProtocolType, int>
        {
            { TextFrameKind.Text256, string.Empty, ProtocolType.TCP, 0 },
            { TextFrameKind.Text512, "hello", ProtocolType.UDP, Encoding.UTF8.GetByteCount("hello") },
            { TextFrameKind.Text1024, "Xin chao", ProtocolType.TCP, Encoding.UTF8.GetByteCount("Xin chao") }
        };
    }

    public static TheoryData<TextFrameKind> TextFrameInitializeOverflowCases()
        =>
        [
            TextFrameKind.Text256,
            TextFrameKind.Text512,
            TextFrameKind.Text1024
        ];

    public static TheoryData<TextFrameKind> TextFrameResetCases()
    {
        return
        [
            TextFrameKind.Text256,
            TextFrameKind.Text512,
            TextFrameKind.Text1024
        ];
    }

    public static TheoryData<PacketRoundTripKind> PacketRoundTripCases()
    {
        return
        [
            PacketRoundTripKind.Control,
            PacketRoundTripKind.Directive,
            PacketRoundTripKind.Handshake,
            PacketRoundTripKind.Text256,
            PacketRoundTripKind.Text512,
            PacketRoundTripKind.Text1024
        ];
    }

    public static IEnumerable<object[]> PacketSerializeBufferTooSmallCases()
    {
        Control control = new();
        control.Initialize(8, ControlType.PING, 1, ProtocolReason.NONE, ProtocolType.TCP);
        yield return [control];

        Directive directive = new();
        directive.Initialize(ControlType.ACK, ProtocolReason.NONE, ProtocolAdvice.NONE, 1);
        yield return [directive];

        yield return [new Handshake(5, [1, 2, 3], ProtocolType.TCP)];

        Text256 text256 = new();
        text256.Initialize("abc");
        yield return [text256];
    }

    private static byte[] CreatePacketBytes(string payload)
    {
        Text256 text = new();
        text.Initialize(payload, ProtocolType.TCP);
        return text.Serialize();
    }

    private static FrameBase CreateAndInitializeTextFrame(TextFrameKind frameKind, string content, ProtocolType protocol)
    {
        return frameKind switch
        {
            TextFrameKind.Text256 => InitializeTextFrame(new Text256(), content, protocol),
            TextFrameKind.Text512 => InitializeTextFrame(new Text512(), content, protocol),
            TextFrameKind.Text1024 => InitializeTextFrame(new Text1024(), content, protocol),
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };
    }

    private static FrameBase InitializeTextFrame(FrameBase frame, string content, ProtocolType protocol)
    {
        switch (frame)
        {
            case Text256 text256:
                text256.Initialize(content, protocol);
                return text256;
            case Text512 text512:
                text512.Initialize(content, protocol);
                return text512;
            case Text1024 text1024:
                text1024.Initialize(content, protocol);
                return text1024;
            default:
                throw new InvalidOperationException("Unexpected text frame type.");
        }
    }

    private static int GetTextFrameDynamicSize(TextFrameKind frameKind)
        => frameKind switch
        {
            TextFrameKind.Text256 => Text256.DynamicSize,
            TextFrameKind.Text512 => Text512.DynamicSize,
            TextFrameKind.Text1024 => Text1024.DynamicSize,
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };

    private static char GetOverflowFillCharacter(TextFrameKind frameKind)
        => frameKind switch
        {
            TextFrameKind.Text256 => 'a',
            TextFrameKind.Text512 => 'b',
            TextFrameKind.Text1024 => 'c',
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };

    private static FrameBase CreateDirtyTextFrame(TextFrameKind frameKind)
        => frameKind switch
        {
            TextFrameKind.Text256 => CreateDirtyText256(),
            TextFrameKind.Text512 => CreateDirtyText512(),
            TextFrameKind.Text1024 => CreateDirtyText1024(),
            _ => throw new InvalidOperationException("Unexpected text frame kind.")
        };

    private static Text256 CreateDirtyText256()
    {
        Text256 frame = new();
        frame.Initialize("alpha", ProtocolType.UDP);
        frame.Flags = PacketFlags.COMPRESSED;
        frame.Priority = PacketPriority.HIGH;
        frame.OpCode = 5;
        return frame;
    }

    private static Text512 CreateDirtyText512()
    {
        Text512 frame = new();
        frame.Initialize("beta", ProtocolType.TCP);
        frame.Flags = PacketFlags.ENCRYPTED;
        frame.Priority = PacketPriority.LOW;
        frame.OpCode = 6;
        return frame;
    }

    private static Text1024 CreateDirtyText1024()
    {
        Text1024 frame = new();
        frame.Initialize("gamma", ProtocolType.UDP);
        frame.Flags = PacketFlags.FRAGMENTED;
        frame.Priority = PacketPriority.MEDIUM;
        frame.OpCode = 7;
        return frame;
    }

    private static FrameBase CreateRoundTripPacket(PacketRoundTripKind packetKind)
        => packetKind switch
        {
            PacketRoundTripKind.Control => CreateControlPacket(),
            PacketRoundTripKind.Directive => CreateDirectivePacket(),
            PacketRoundTripKind.Handshake => new Handshake(17, [1, 2, 3, 4], ProtocolType.UDP),
            PacketRoundTripKind.Text256 => CreateText256Packet(),
            PacketRoundTripKind.Text512 => CreateText512Packet(),
            PacketRoundTripKind.Text1024 => CreateText1024Packet(),
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

    private static Text256 CreateText256Packet()
    {
        Text256 packet = new();
        packet.Initialize("short text", ProtocolType.TCP);
        return packet;
    }

    private static Text512 CreateText512Packet()
    {
        Text512 packet = new();
        packet.Initialize("mid sized text", ProtocolType.UDP);
        return packet;
    }

    private static Text1024 CreateText1024Packet()
    {
        Text1024 packet = new();
        packet.Initialize("larger text frame content", ProtocolType.TCP);
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
                    Assert.Equal(expectedHandshake.Data, actualHandshake.Data);
                    break;
                }
            case PacketRoundTripKind.Text256:
                {
                    Text256 expectedText = Assert.IsType<Text256>(expected);
                    Text256 actualText = Assert.IsType<Text256>(actual);
                    Assert.Equal(expectedText.Content, actualText.Content);
                    Assert.Equal(expectedText.Protocol, actualText.Protocol);
                    break;
                }
            case PacketRoundTripKind.Text512:
                {
                    Text512 expectedText = Assert.IsType<Text512>(expected);
                    Text512 actualText = Assert.IsType<Text512>(actual);
                    Assert.Equal(expectedText.Content, actualText.Content);
                    Assert.Equal(expectedText.Protocol, actualText.Protocol);
                    break;
                }
            case PacketRoundTripKind.Text1024:
                {
                    Text1024 expectedText = Assert.IsType<Text1024>(expected);
                    Text1024 actualText = Assert.IsType<Text1024>(actual);
                    Assert.Equal(expectedText.Content, actualText.Content);
                    Assert.Equal(expectedText.Protocol, actualText.Protocol);
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
