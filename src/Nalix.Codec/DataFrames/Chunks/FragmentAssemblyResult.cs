// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Codec.Memory;

namespace Nalix.Codec.DataFrames.Chunks;

/// <summary>
/// Represents a completed fragment reassembly operation.
/// </summary>
/// <remarks>
/// The contained <see cref="Lease"/> is the same pooled buffer that the assembler used while
/// accumulating chunk bodies, so returning this result does not allocate another
/// <see cref="BufferLease"/> wrapper.
/// </remarks>
public readonly struct FragmentAssemblyResult(BufferLease lease)
{
    /// <summary>
    /// Gets the pooled lease that owns the reassembled payload.
    /// </summary>
    /// <remarks>
    /// The caller becomes responsible for disposing this lease after processing completes.
    /// </remarks>
    public BufferLease Lease { get; } = lease;

    /// <summary>
    /// Gets the number of valid payload bytes in the reassembled result.
    /// </summary>
    public int Length => this.Lease.Length;

    /// <summary>
    /// Gets a read-only span over the reassembled payload.
    /// </summary>
    public ReadOnlySpan<byte> Span => this.Lease.Span;

    /// <summary>
    /// Gets a read-only memory view over the reassembled payload.
    /// </summary>
    public ReadOnlyMemory<byte> Memory => this.Lease.Memory;
}
