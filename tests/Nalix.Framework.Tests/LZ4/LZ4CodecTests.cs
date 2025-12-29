// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
using System;
using Nalix.Framework.LZ4;
using Nalix.Framework.Memory.Buffers;
using Xunit;

namespace Nalix.Framework.Tests.LZ4;

/// <summary>
/// Unit tests for <see cref="LZ4Codec"/>.
/// </summary>
public sealed class LZ4CodecTests
{
    /// <summary>
    /// Creates a deterministic sample payload for testing.
    /// </summary>
    /// <param name="length">The desired payload length.</param>
    /// <returns>A byte array with deterministic content.</returns>
    private static byte[] CreateSamplePayload(int length)
    {
        byte[] data = new byte[length];

        for (int i = 0; i < data.Length; i++)
        {
            // Simple repeating pattern for better compression.
            data[i] = (byte)(i % 256);
        }

        return data;
    }

    [Fact]
    public void EncodeDecodeWithSpanRoundTripsData()
    {
        // Arrange
        byte[] original = CreateSamplePayload(4 * 1024); // 4 KB

        // Estimate a safe compressed buffer size.
        int maxCompressedLength = Framework.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        byte[] compressed = new byte[maxCompressedLength];

        // The decode API needs a buffer for decompressed data.
        byte[] decompressed = new byte[original.Length];

        // Act
        int writtenCompressed = LZ4Codec.Encode(original, compressed);

        // Assert
        Assert.True(writtenCompressed > 0);

        int writtenDecompressed = LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            decompressed);

        Assert.Equal(original.Length, writtenDecompressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void EncodeWithTooSmallOutputThrowsArgumentOutOfRangeException()
    {
        // Arrange
        byte[] original = CreateSamplePayload(1024);
        byte[] tooSmallOutput = new byte[LZ4BlockHeader.Size - 1];

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => LZ4Codec.Encode(original, tooSmallOutput));
    }

    [Fact]
    public void EncodeWithLeaseRoundTripsData()
    {
        // Arrange
        byte[] original = CreateSamplePayload(8 * 1024);

        // Act
        LZ4Codec.Encode(
            original,
            out BufferLease lease,
            out int compressedLength);

        // Assert
        Assert.NotNull(lease);
        Assert.True(compressedLength > 0);

        using (lease)
        {
            // Decode using lease content.
            byte[] decoded = new byte[original.Length];

            int written = LZ4Codec.Decode(
                lease.Span[..compressedLength],
                decoded);

            Assert.Equal(original.Length, written);
            Assert.Equal(original, decoded);
        }
    }

    [Fact]
    public void DecodeToByteArrayRoundTripsData()
    {
        // Arrange
        byte[] original = CreateSamplePayload(4096);

        int maxCompressedLength = Framework.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        byte[] compressed = new byte[maxCompressedLength];

        int writtenCompressed = LZ4Codec.Encode(original, compressed);
        Assert.True(writtenCompressed > 0);

        // Act
        LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            out byte[] output,
            out int bytesWritten);

        // Assert
        Assert.NotNull(output);
        Assert.Equal(original.Length, bytesWritten);

        byte[] actual = output.AsSpan(0, bytesWritten).ToArray();
        Assert.Equal(original, actual);
    }

    [Fact]
    public void DecodeToLeaseRoundTripsData()
    {
        // Arrange
        byte[] original = CreateSamplePayload(10 * 1024);

        int maxCompressedLength = Framework.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        byte[] compressed = new byte[maxCompressedLength];

        int writtenCompressed = LZ4Codec.Encode(original, compressed);
        Assert.True(writtenCompressed > 0);

        // Act
        LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            out BufferLease lease,
            out int bytesWritten);

        Assert.NotNull(lease);

        using (lease)
        {
            Assert.True(bytesWritten > 0);

            byte[] span = lease.Span[..bytesWritten].ToArray();
            Assert.Equal(original, span);
        }
    }

    [Fact]
    public void EncodeDecodeWithEmptyInputWorks()
    {
        // Arrange
        ReadOnlySpan<byte> original = [];

        int maxCompressedLength = Framework.LZ4.Encoders.LZ4BlockEncoder.GetMaxLength(original.Length);
        byte[] compressed = new byte[maxCompressedLength];

        byte[] decompressed = [];

        // Act
        int writtenCompressed = LZ4Codec.Encode(original, compressed);

        // Empty input may still produce a small header, so we mainly
        // check that Encode and Decode do not throw and are consistent.
        Assert.True(writtenCompressed >= 0);

        if (writtenCompressed == 0)
        {
            // Nothing to decode.
            return;
        }

        int writtenDecompressed = LZ4Codec.Decode(
            compressed.AsSpan(0, writtenCompressed),
            decompressed);

        Assert.Equal(0, writtenDecompressed);
    }
}
