// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Caching;

/// <summary>
/// Defines a reference-counted lease over a pooled byte buffer.
/// Provides access to the valid payload region, underlying capacity,
/// and methods for safe sharing/disposal.
/// </summary>
public interface IBufferLease : System.IDisposable
{
    /// <summary>Gets the valid payload length within the buffer.</summary>
    System.Int32 Length { get; }

    /// <summary>Gets the capacity of the underlying buffer.</summary>
    System.Int32 Capacity { get; }

    /// <summary>Gets a read-only view of the valid payload.</summary>
    System.ReadOnlyMemory<System.Byte> Memory { get; }

    /// <summary>Gets a writable span covering the valid payload.</summary>
    System.Span<System.Byte> Span { get; }

    /// <summary>Gets a writable span covering the entire capacity.</summary>
    System.Span<System.Byte> SpanFull { get; }

    /// <summary>
    /// Increases the reference count so multiple consumers can hold this lease safely.
    /// </summary>
    void Retain();

    /// <summary>
    /// Sets the valid payload length (must be between 0 and Capacity).
    /// </summary>
    void SetLength(System.Int32 length);

    /// <summary>
    /// Attempts to detach the underlying array, transferring ownership to the caller.
    /// </summary>
    System.Boolean TryDetach(out System.Byte[] buffer, out System.Int32 length);
}
