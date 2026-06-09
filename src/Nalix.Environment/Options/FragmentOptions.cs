// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Validation;
using Nalix.Environment.Configuration.Binding;
using Nalix.Environment.Fragments;

namespace Nalix.Environment.Options;

/// <summary>
/// Options for fragmentation and reassembly of large frames.
/// </summary>
[IniComment("Fragmentation configuration — controls chunking and reassembly of large data payloads")]
public sealed partial class FragmentOptions : ConfigurationLoader, IValidatableConfiguration
{
    /// <summary>
    /// Maximum allowed size (in bytes) of the raw payload the caller can pass to <c>SendAsync</c>.
    /// Exceeding this limit will cause <see cref="ArgumentOutOfRangeException"/> to be thrown.
    /// Default: 16 MB.
    /// </summary>
    [IniComment("Max allowed payload size in bytes before sending (default 16MB)")]
    [ValueRange(1, int.MaxValue)]
    public int MaxPayloadSize { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Threshold (in bytes) to activate automatic chunking.
    /// When <c>payload.Length &gt; ChunkThreshold</c>, <c>SendAsync</c> will split the data into multiple chunks.
    /// Default: 32KB (fits a single Ethernet MTU after TCP/IP overhead).
    /// </summary>
    [IniComment("Max chunk size in bytes (default 32KB)")]
    [ValueRange(4096, 65000)]
    public int MaxChunkSize { get; set; } = 32_000;

    /// <summary>
    /// Maximum total bytes that <see cref="FragmentAssembler"/> will accumulate for a single stream.
    /// If a stream exceeds this limit, it will be discarded immediately.
    /// Default: 16 MB.
    /// </summary>
    [IniComment("Max reassembly buffer per stream (default 16MB)")]
    [ValueRange(1, int.MaxValue)]
    public int MaxReassemblyBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Maximum time (in milliseconds) that a stream can wait for the next chunk before it is evicted.
    /// Default: 30,000 ms.
    /// </summary>
    [IniComment("Incomplete stream reassembly timeout in milliseconds (default 30,000)")]
    [ValueRange(100, 3600000)]
    public long ReassemblyTimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// The base size (in bytes) of the elastic receive buffer.
    /// <para>
    /// This dictates the starting and resting memory footprint of a socket connection.
    /// It should ideally align with the OS page size (4096 bytes) and exceed a standard network MTU (1500 bytes).
    /// </para>
    /// Default: 4096.
    /// </summary>
    [IniComment("Base size of the elastic receive buffer in bytes (default 4096)")]
    [ValueRange(1024, int.MaxValue)]
    public int MinReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// Validates the chunking configuration to ensure it meets the necessary constraints for proper operation.
    /// </summary>
    /// <exception cref="Abstractions.Exceptions.ValidationException">Thrown when any fragmentation limit is invalid.</exception>
    public void Validate()
    {
        this.ValidateDataAnnotations();

        if (this.MaxPayloadSize < this.MaxChunkSize)
        {
            throw new Nalix.Abstractions.Validation.ValidationException(
                $"MaxPayloadSize={this.MaxPayloadSize} must be >= MaxChunkSize={this.MaxChunkSize}.");
        }

        long maxChunkCount = ((long)this.MaxPayloadSize + this.MaxChunkSize - 1) / this.MaxChunkSize;
        if (maxChunkCount > ushort.MaxValue)
        {
            throw new Nalix.Abstractions.Validation.ValidationException(
                $"MaxChunkSize={this.MaxChunkSize} can produce {maxChunkCount} chunks for MaxPayloadSize={this.MaxPayloadSize}, which exceeds the {ushort.MaxValue}-chunk wire header limit.");
        }

        int maxChunkFrameSize = PacketConstants.HeaderSize + FragmentHeader.WireSize + this.MaxChunkSize;
        if (maxChunkFrameSize > ushort.MaxValue)
        {
            throw new Nalix.Abstractions.Validation.ValidationException(
                $"MaxChunkSize={this.MaxChunkSize} produces a fragment frame of {maxChunkFrameSize} bytes, which exceeds the {ushort.MaxValue}-byte wire header limit.");
        }
    }
}
