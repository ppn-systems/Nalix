// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Shared;

/// <summary>
/// Defines a reference-counted lease over a pooled byte buffer.
/// Provides access to the valid payload region, underlying capacity,
/// and methods for safe sharing/disposal.
/// </summary>
public interface IBufferLease : System.IDisposable
{
    /// <summary>Gets the valid payload length within the buffer.</summary>
    int Length { get; }

    /// <summary>Gets the capacity of the underlying buffer.</summary>
    int Capacity { get; }

    /// <summary>Gets a writable span covering the valid payload.</summary>
    System.Span<byte> Span { get; }

    /// <summary>Gets a writable span covering the entire capacity.</summary>
    System.Span<byte> SpanFull { get; }

    /// <summary>Gets a read-only view of the valid payload.</summary>
    System.ReadOnlyMemory<byte> Memory { get; }

    /// <summary>
    /// Increases the reference count so multiple consumers can hold this lease safely.
    /// </summary>
    void Retain();

    /// <summary>
    /// Sets the valid payload length (must be between 0 and Capacity).
    /// </summary>
    void CommitLength([System.Diagnostics.CodeAnalysis.NotNull] int length);

    /// <summary>
    /// Attempts to detach the underlying array, transferring ownership to the caller.
    /// </summary>
    bool ReleaseOwnership(
        [System.Diagnostics.CodeAnalysis.MaybeNull] out byte[] buffer,
        [System.Diagnostics.CodeAnalysis.NotNull] out int start,
        [System.Diagnostics.CodeAnalysis.NotNull] out int length);
}