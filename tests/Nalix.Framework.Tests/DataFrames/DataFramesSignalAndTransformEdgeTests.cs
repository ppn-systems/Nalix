using System;
using System.IO;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Extensions;
using Nalix.Framework.Identifiers;
using Nalix.Framework.Memory.Buffers;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

public sealed class DataFramesSignalAndTransformEdgeTests
{
    [Fact]
    public void FragmentAssemblerWhenTotalChunksChangesEvictsStreamAndThrows()
    {
        using FragmentAssembler assembler = new();
        _ = assembler.Add(new FragmentHeader(50, 0, 2, false), [1], out _);

        bool evicted = false;
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            assembler.Add(new FragmentHeader(50, 1, 3, true), [2], out evicted));

        Assert.True(evicted);
        Assert.Contains("changed TotalChunks", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, assembler.OpenStreamCount);
    }

    [Fact]
    public void FragmentAssemblerWhenSecondChunkIsOutOfOrderThrowsAndKeepsStreamOpen()
    {
        using FragmentAssembler assembler = new();
        _ = assembler.Add(new FragmentHeader(51, 0, 3, false), [1], out _);

        bool evicted = false;
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            assembler.Add(new FragmentHeader(51, 2, 3, true), [2], out evicted));

        Assert.False(evicted);
        Assert.Contains("out-of-order", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, assembler.OpenStreamCount);
    }

    [Fact]
    public void FragmentAssemblerWhenMaxOpenStreamsReachedReturnsNullAndMarksEvicted()
    {
        using FragmentAssembler assembler = new() { MaxOpenStreams = 1 };
        _ = assembler.Add(new FragmentHeader(60, 0, 2, false), [1], out _);

        FragmentAssemblyResult? result = assembler.Add(new FragmentHeader(61, 0, 2, false), [2], out bool evicted);

        Assert.Null(result);
        Assert.True(evicted);
        Assert.Equal(1, assembler.OpenStreamCount);
    }

    [Fact]
    public void HandshakeInitializeWhenProofIsOmittedUsesZeroProofAndUrgentPriority()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[32];
        key[0] = 0x11;
        nonce[0] = 0x22;

        Handshake packet = new();
        packet.Initialize(HandshakeStage.CLIENT_HELLO, new Bytes32(key), new Bytes32(nonce), proof: null, transport: ProtocolType.UDP);

        Assert.Equal((ushort)ProtocolOpCode.HANDSHAKE, packet.OpCode);
        Assert.Equal(HandshakeStage.CLIENT_HELLO, packet.Stage);
        Assert.Equal(ProtocolType.UDP, packet.Protocol);
        Assert.Equal(PacketPriority.URGENT, packet.Priority);
        Assert.True(packet.Proof.IsZero);
        Assert.Equal(Snowflake.Empty, packet.SessionToken);
    }

    [Fact]
    public void HandshakeIsValidWhenPacketIsNullReturnsFalse()
    {
        Assert.False(Handshake.IsValid(null!));
    }

    [Fact]
    public void SessionResumeInitializeWhenOptionalValuesAreOmittedUsesExpectedDefaults()
    {
        SessionResume packet = new();
        packet.Initialize(SessionResumeStage.REQUEST, Snowflake.Empty);

        Assert.Equal((ushort)ProtocolOpCode.SESSION_SIGNAL, packet.OpCode);
        Assert.Equal(SessionResumeStage.REQUEST, packet.Stage);
        Assert.Equal(ProtocolReason.NONE, packet.Reason);
        Assert.True(packet.Proof.IsZero);
        Assert.Equal(ProtocolType.TCP, packet.Protocol);
        Assert.Equal(PacketPriority.URGENT, packet.Priority);
    }

    [Fact]
    public void ControlInitializeOverloadWithoutOpcodeUpdatesCoreProperties()
    {
        Control packet = new();
        packet.Initialize(ControlType.HEARTBEAT, sequenceId: 44, reasonCode: ProtocolReason.TIMEOUT, transport: ProtocolType.UDP);

        Assert.Equal(ControlType.HEARTBEAT, packet.Type);
        Assert.Equal(44u, packet.SequenceId);
        Assert.Equal(ProtocolReason.TIMEOUT, packet.Reason);
        Assert.Equal(ProtocolType.UDP, packet.Protocol);
        Assert.NotEqual(0, packet.Timestamp);
        Assert.NotEqual(0, packet.MonoTicks);
    }

    [Fact]
    public void DirectiveInitializeOverloadWithoutOpcodeKeepsSystemControlOpcode()
    {
        Directive packet = new();
        packet.Initialize(ControlType.THROTTLE, ProtocolReason.THROTTLED, ProtocolAdvice.SLOW_DOWN, sequenceId: 9, flags: ControlFlags.SLOW_DOWN, arg0: 1, arg1: 2, arg2: 3);

        Assert.Equal((ushort)ProtocolOpCode.SYSTEM_CONTROL, packet.OpCode);
        Assert.Equal(ControlType.THROTTLE, packet.Type);
        Assert.Equal(ProtocolReason.THROTTLED, packet.Reason);
        Assert.Equal(ProtocolAdvice.SLOW_DOWN, packet.Action);
        Assert.Equal(ControlFlags.SLOW_DOWN, packet.Control);
        Assert.Equal(ProtocolType.TCP, packet.Protocol);
        Assert.Equal(PacketPriority.HIGH, packet.Priority);
    }

    [Fact]
    public void PacketCipherWhenSourceIsNullThrowsArgumentNullException()
    {
        byte[] key = new byte[32];
        _ = Assert.Throws<ArgumentNullException>(() => PacketCipher.EncryptFrame(null!, key, CipherSuiteType.Chacha20Poly1305));
        _ = Assert.Throws<ArgumentNullException>(() => PacketCipher.DecryptFrame(null!, key, CipherSuiteType.Chacha20Poly1305));
    }

    [Fact]
    public void PacketCompressionWhenSourceIsNullThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => PacketCompression.CompressFrame(null!));
        _ = Assert.Throws<ArgumentNullException>(() => PacketCompression.DecompressFrame(null!));
    }

    [Fact]
    public void PacketCipherDecryptRemovesOnlyEncryptedFlagAndKeepsOtherFlags()
    {
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + 1);
        }

        byte[] payload = [1, 2, 3, 4, 5, 6];
        using BufferLease src = BufferLease.Rent(FrameTransformer.Offset + payload.Length);
        src.CommitLength(FrameTransformer.Offset + payload.Length);
        src.Span[..FrameTransformer.Offset].Clear();
        src.Span.WriteFlagsLE(PacketFlags.COMPRESSED);
        payload.CopyTo(src.Span[FrameTransformer.Offset..]);

        using var encrypted = PacketCipher.EncryptFrame(src, key, CipherSuiteType.Chacha20Poly1305);
        using var decrypted = PacketCipher.DecryptFrame(encrypted, key, CipherSuiteType.Chacha20Poly1305);

        PacketFlags flags = decrypted.Span.ReadFlagsLE();
        Assert.True(flags.HasFlag(PacketFlags.COMPRESSED));
        Assert.False(flags.HasFlag(PacketFlags.ENCRYPTED));
    }

    [Fact]
    public void PacketCompressionDecompressRemovesOnlyCompressedFlagAndKeepsOtherFlags()
    {
        byte[] payload = new byte[128];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 17);
        }

        using BufferLease src = BufferLease.Rent(FrameTransformer.Offset + payload.Length);
        src.CommitLength(FrameTransformer.Offset + payload.Length);
        src.Span[..FrameTransformer.Offset].Clear();
        src.Span.WriteFlagsLE(PacketFlags.ENCRYPTED);
        payload.CopyTo(src.Span[FrameTransformer.Offset..]);

        using var compressed = PacketCompression.CompressFrame(src);
        using var decompressed = PacketCompression.DecompressFrame(compressed);

        PacketFlags flags = decompressed.Span.ReadFlagsLE();
        Assert.True(flags.HasFlag(PacketFlags.ENCRYPTED));
        Assert.False(flags.HasFlag(PacketFlags.COMPRESSED));
    }

    [Fact]
    public void PacketCipherDecryptWhenCiphertextFrameIsTooShortThrowsCipherException()
    {
        byte[] key = new byte[32];
        using BufferLease shortFrame = BufferLease.Rent(FrameTransformer.Offset);
        shortFrame.CommitLength(FrameTransformer.Offset);

        _ = Assert.Throws<CipherException>(() => PacketCipher.DecryptFrame(shortFrame, key, CipherSuiteType.Chacha20));
    }
}
