// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration.Binding;
using Nalix.Framework.DataFrames.Chunks;

namespace Nalix.Framework.Options;

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
    public int MaxChunkSize { get; set; } = 1_400;

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
    /// <exception cref="InvalidOperationException">Thrown when any fragmentation limit is non-positive, inconsistent, or exceeds wire-format limits.</exception>
    public void Validate()
    {
        // ChunkBodySize + 2B (frame header) + 7B (ChunkHeader) ≤ PacketSizeLimit

        if (this.MaxChunkSize <= 0)
        {
            throw new InvalidOperationException("MaxChunkSize must be > 0.");
        }

        if (this.MaxPayloadSize < this.MaxChunkSize)
        {
            throw new InvalidOperationException(
                $"MaxPayloadSize={this.MaxPayloadSize} must be >= MaxChunkSize={this.MaxChunkSize}.");
        }

        if (this.MaxChunkSize <= 0)
        {
            throw new InvalidOperationException("MaxChunkSize must be > 0.");
        }

        int maxChunkCount = (this.MaxPayloadSize + this.MaxChunkSize - 1) / this.MaxChunkSize;
        if (maxChunkCount > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"MaxChunkSize={this.MaxChunkSize} can produce {maxChunkCount} chunks for MaxPayloadSize={this.MaxPayloadSize}, " +
                $"which exceeds the {ushort.MaxValue}-chunk wire header limit.");
        }

        int maxChunkFrameSize = PacketConstants.HeaderSize + FragmentHeader.WireSize + this.MaxChunkSize;
        if (maxChunkFrameSize > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"MaxChunkSize={this.MaxChunkSize} produces a fragment frame of {maxChunkFrameSize} bytes, " +
                $"which exceeds the {ushort.MaxValue}-byte wire header limit.");
        }
    }
}
