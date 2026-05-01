// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel.DataAnnotations;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.DataFrames.Chunks;
using Nalix.Environment.Configuration.Binding;

namespace Nalix.Codec.Options;

/// <summary>
/// Options for fragmentation and reassembly of large frames.
/// </summary>
[IniComment("Fragmentation configuration — controls chunking and reassembly of large data payloads")]
public sealed class FragmentOptions : ConfigurationLoader
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
    [IniComment("Max allowed payload size in bytes before sending (default 16MB)")]
    public int MaxPayloadSize { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Threshold (in bytes) to activate automatic chunking.
    /// When <c>payload.Length &gt; ChunkThreshold</c>, <c>SendAsync</c> will split the data into multiple chunks.
    /// <para>
    /// This value must be less than <see cref="PacketConstants.PacketSizeLimit"/> minus 9 bytes (header overhead).
    /// </para>
    /// Default: 1,400 bytes (fits a single Ethernet MTU after TCP/IP overhead).
    /// </summary>
    [IniComment("Max chunk size in bytes (default 1400)")]
    public int MaxChunkSize { get; set; } = 1_400;

    /// <summary>
    /// Maximum total bytes that <see cref="FragmentAssembler"/> will accumulate for a single stream.
    /// If a stream exceeds this limit, it will be discarded immediately.
    /// Default: 16 MB.
    /// </summary>
    [IniComment("Max reassembly buffer per stream (default 16MB)")]
    public int MaxReassemblyBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Maximum time (in milliseconds) that a stream can wait for the next chunk before it is evicted.
    /// Default: 30,000 ms.
    /// </summary>
    [IniComment("Incomplete stream reassembly timeout in milliseconds (default 30,000)")]
    public long ReassemblyTimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// Validates the chunking configuration to ensure it meets the necessary constraints for proper operation.
    /// </summary>
    /// <exception cref="ValidationException">Thrown when any fragmentation limit is invalid.</exception>
    public void Validate()
    {
        ValidationContext context = new(this);
        Validator.ValidateObject(this, context, validateAllProperties: true);

        if (this.MaxPayloadSize < 0)
        {
            throw new ValidationException($"MaxPayloadSize={this.MaxPayloadSize} must be non-negative.");
        }

        if (this.MaxChunkSize < 0 || this.MaxChunkSize > 65000)
        {
            throw new ValidationException($"MaxChunkSize={this.MaxChunkSize} must be a non-negative number and not greater than 65000.");
        }

        if (this.MaxReassemblyBytes < 0)
        {
            throw new ValidationException($"MaxReassemblyBytes={this.MaxReassemblyBytes} must be non-negative.");
        }

        if (this.MaxReassemblyBytes > int.MaxValue)
        {
            throw new ValidationException("MaxReassemblyBytes must be less than or equal to int.MaxValue.");
        }

        if (this.ReassemblyTimeoutMs < 100)
        {
            throw new ValidationException($"ReassemblyTimeoutMs={this.ReassemblyTimeoutMs} must be at least 100 ms.");
        }

        if (this.ReassemblyTimeoutMs > 3600000)
        {
            throw new ValidationException($"ReassemblyTimeoutMs={this.ReassemblyTimeoutMs} must be at most 1 hour (3600000 ms).");
        }

        if (this.MaxChunkSize <= 0)
        {
            throw new ValidationException($"MaxChunkSize={this.MaxChunkSize} must be positive.");
        }

        if (this.MaxPayloadSize < this.MaxChunkSize)
        {
            throw new ValidationException(
                $"MaxPayloadSize={this.MaxPayloadSize} must be >= MaxChunkSize={this.MaxChunkSize}.");
        }

        int maxChunkCount = (this.MaxPayloadSize + this.MaxChunkSize - 1) / this.MaxChunkSize;
        if (maxChunkCount > ushort.MaxValue)
        {
            throw new ValidationException(
                $"MaxChunkSize={this.MaxChunkSize} can produce {maxChunkCount} chunks for MaxPayloadSize={this.MaxPayloadSize}, " +
                $"which exceeds the {ushort.MaxValue}-chunk wire header limit.");
        }

        int maxChunkFrameSize = PacketConstants.HeaderSize + FragmentHeader.WireSize + this.MaxChunkSize;
        if (maxChunkFrameSize > ushort.MaxValue)
        {
            throw new ValidationException(
                $"MaxChunkSize={this.MaxChunkSize} produces a fragment frame of {maxChunkFrameSize} bytes, " +
                $"which exceeds the {ushort.MaxValue}-byte wire header limit.");
        }
    }
}
