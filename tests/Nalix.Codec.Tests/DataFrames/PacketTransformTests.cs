using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;
// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Codec.Transforms;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Environment.Random;
using Nalix.Codec.Security;
using Xunit;

namespace Nalix.Codec.Tests.DataFrames;

public sealed class PacketTransformTests
{
    private static readonly byte[] s_testKey = new byte[32];

    static PacketTransformTests() => Csprng.NextBytes(s_testKey);

    [Fact]
    public void FrameCipher_Roundtrip_ShouldSucceed()
    {
        // 1. Arrange
        byte[] originalPayload = new byte[100];
        Csprng.NextBytes(originalPayload);

        using BufferLease src = BufferLease.Rent(FrameTransformer.Offset + originalPayload.Length);
        src.CommitLength(FrameTransformer.Offset + originalPayload.Length);

        // Zero the header area and set flags
        src.Span[..FrameTransformer.Offset].Clear();
        src.Span.AsHeaderRef() = new PacketHeader { Flags = PacketFlags.NONE };

        // Copy payload
        originalPayload.CopyTo(src.Span[FrameTransformer.Offset..]);

        // 2. Encrypt
        using IBufferLease encrypted = FrameCipher.EncryptFrame(src, s_testKey, CipherSuiteType.Chacha20Poly1305);

        Assert.True(encrypted.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.ENCRYPTED));
        Assert.NotEqual(0, encrypted.Length);
        Assert.True(encrypted.Length >= FrameTransformer.Offset + EnvelopeCipher.HeaderSize);

        // 3. Decrypt
        using IBufferLease decrypted = FrameCipher.DecryptFrame(encrypted, s_testKey, CipherSuiteType.Chacha20Poly1305);

        Assert.False(decrypted.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.ENCRYPTED));
        Assert.Equal(src.Length, decrypted.Length);

        byte[] resultPayload = decrypted.Span[FrameTransformer.Offset..].ToArray();
        Assert.Equal(originalPayload, resultPayload);
    }

    [Fact]
    public void FrameCompression_Roundtrip_ShouldSucceed()
    {
        // 1. Arrange
        byte[] originalPayload = new byte[1000]; // Larger payload for compression
        Csprng.NextBytes(originalPayload);

        using BufferLease src = BufferLease.Rent(FrameTransformer.Offset + originalPayload.Length);
        src.CommitLength(FrameTransformer.Offset + originalPayload.Length);

        src.Span[..FrameTransformer.Offset].Clear();
        src.Span.AsHeaderRef() = new PacketHeader { Flags = PacketFlags.NONE };
        originalPayload.CopyTo(src.Span[FrameTransformer.Offset..]);

        // 2. Compress
        using IBufferLease compressed = FrameCompression.CompressFrame(src);

        Assert.True(compressed.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.COMPRESSED));

        // 3. Decompress
        using IBufferLease decompressed = FrameCompression.DecompressFrame(compressed);

        Assert.False(decompressed.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.COMPRESSED));
        Assert.Equal(src.Length, decompressed.Length);

        byte[] resultPayload = decompressed.Span[FrameTransformer.Offset..].ToArray();
        Assert.Equal(originalPayload, resultPayload);
    }

    [Fact]
    public void PacketTransform_Combined_ShouldSucceed()
    {
        // 1. Arrange
        byte[] originalPayload = new byte[500];
        Csprng.NextBytes(originalPayload);

        using BufferLease src = BufferLease.Rent(FrameTransformer.Offset + originalPayload.Length);
        src.CommitLength(FrameTransformer.Offset + originalPayload.Length);

        src.Span[..FrameTransformer.Offset].Clear();
        src.Span.AsHeaderRef() = new PacketHeader { Flags = PacketFlags.NONE };
        originalPayload.CopyTo(src.Span[FrameTransformer.Offset..]);

        // 2. Encrypt then Compress
        using IBufferLease encrypted = FrameCipher.EncryptFrame(src, s_testKey, CipherSuiteType.Chacha20Poly1305);
        using IBufferLease compressed = FrameCompression.CompressFrame(encrypted);

        Assert.True(compressed.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.ENCRYPTED));
        Assert.True(compressed.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.COMPRESSED));

        // 3. Decompress then Decrypt
        using IBufferLease decompressed = FrameCompression.DecompressFrame(compressed);
        using IBufferLease decrypted = FrameCipher.DecryptFrame(decompressed, s_testKey, CipherSuiteType.Chacha20Poly1305);

        Assert.False(decrypted.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.ENCRYPTED));
        Assert.False(decrypted.Span.AsHeaderRef().Flags.HasFlag(PacketFlags.COMPRESSED));
        Assert.Equal(src.Length, decrypted.Length);

        byte[] resultPayload = decrypted.Span[FrameTransformer.Offset..].ToArray();
        Assert.Equal(originalPayload, resultPayload);
    }
}


















