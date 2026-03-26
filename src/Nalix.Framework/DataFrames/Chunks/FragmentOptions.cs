// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration.Binding;

namespace Nalix.Framework.DataFrames.Chunks;

/// <summary>
/// Options for fragmentation and reassembly of large frames.
/// </summary>
public class FragmentOptions : ConfigurationLoader
{
    /// <summary>
    /// Maximum allowed size (in bytes) of the raw payload the caller can pass to <c>SendAsync</c>.
    /// Exceeding this limit will cause <see cref="ArgumentOutOfRangeException"/> to be thrown.
    /// <para>
    /// This is different from <see cref="PacketConstants.PacketSizeLimit"/>: the payload can be larger than a single framed packet,
    /// as it will be automatically chunked.
    /// </para>
    /// Default: 16 MB.
    /// </summary>
    public int MaxPayloadSize { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Threshold (in bytes) to activate automatic chunking.
    /// When <c>payload.Length &gt; ChunkThreshold</c>, <c>SendAsync</c> will split the data into multiple chunks.
    /// <para>
    /// This value must be less than <see cref="PacketConstants.PacketSizeLimit"/> minus 9 bytes (header overhead).
    /// </para>
    /// Default: 1,400 bytes (fits a single Ethernet MTU after TCP/IP overhead).
    /// </summary>
    public int ChunkThreshold { get; set; } = 1_400;

    /// <summary>
    /// Maximum body size (in bytes) for each chunk — not including header bytes.
    /// Typically set equal to <see cref="ChunkThreshold"/>.
    /// <para>
    /// <c>ChunkBodySize</c> plus 9 bytes of overhead must be less than or equal to <see cref="PacketConstants.PacketSizeLimit"/>.
    /// </para>
    /// Default: 1,400 bytes.
    /// </summary>
    public int ChunkBodySize { get; set; } = 1_400;

    /// <summary>
    /// Maximum total bytes that <see cref="FragmentAssembler"/> will accumulate for a single stream.
    /// If a stream exceeds this limit, it will be discarded immediately.
    /// Default: 16 MB.
    /// </summary>
    public int MaxReassemblyBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Maximum time (in milliseconds) that a stream can wait for the next chunk before it is evicted.
    /// Default: 30,000 ms.
    /// </summary>
    public long ReassemblyTimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// Validates the chunking configuration to ensure it meets the necessary constraints for proper operation.
    /// </summary>
    public void Validate()
    {
        // ChunkBodySize + 2B (frame header) + 7B (ChunkHeader) ≤ PacketSizeLimit
        int minPacketSize = ChunkBodySize
                          + sizeof(ushort)                // FramedSocket frame header
                          + FragmentHeader.WireSize;      // ChunkedFrameHeader

        if (minPacketSize > PacketConstants.PacketSizeLimit)
        {
            throw new InvalidOperationException(
                $"ChunkBodySize={ChunkBodySize} + overhead={sizeof(ushort) + FragmentHeader.WireSize} " +
                $"= {minPacketSize} over MaxPacketSize={PacketConstants.PacketSizeLimit}. " +
                $"Decrease ChunkBodySize or increase PacketSizeLimit.");
        }

        if (ChunkThreshold <= 0)
        {
            throw new InvalidOperationException("ChunkThreshold must be > 0.");
        }

        if (MaxPayloadSize < ChunkThreshold)
        {
            throw new InvalidOperationException(
                $"MaxPayloadSize={MaxPayloadSize} must be >= ChunkThreshold={ChunkThreshold}.");
        }
    }
}
