// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Common.Serialization;

namespace Nalix.Common.Primitives;

/// <summary>
/// Represents a fixed-size 256-bit buffer (32 bytes).
/// Used for storing cryptographic hashes, proofs, or signatures.
/// </summary>
[SerializePackable(SerializeLayout.Explicit)]
public struct Fixed256 : IEquatable<Fixed256>
{
    /// <summary>
    /// The size of the <see cref="Fixed256"/> buffer in bytes.
    /// </summary>
    public const int Size = 32;

    [SerializeOrder(0)]
    private readonly ulong _v1;

    [SerializeOrder(1)]
    private readonly ulong _v2;

    [SerializeOrder(2)]
    private readonly ulong _v3;

    [SerializeOrder(3)]
    private readonly ulong _v4;

    /// <summary>
    /// Initializes a new instance of the <see cref="Fixed256"/> struct from a 32-byte span.
    /// </summary>
    /// <param name="source">The source span containing at least 32 bytes.</param>
    /// <exception cref="ArgumentException">Thrown when the source span is smaller than 32 bytes.</exception>
    public Fixed256(ReadOnlySpan<byte> source)
    {
        if (source.Length < Size)
        {
            throw new ArgumentException("Source must be at least 32 bytes.", nameof(source));
        }

        ref byte srcRef = ref MemoryMarshal.GetReference(source);
        _v1 = Unsafe.ReadUnaligned<ulong>(ref srcRef);
        _v2 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref srcRef, 8));
        _v3 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref srcRef, 16));
        _v4 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref srcRef, 24));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Fixed256"/> struct from a 32-byte array.
    /// </summary>
    /// <param name="source">The source array containing exactly 32 bytes.</param>
    public Fixed256(byte[] source) : this(source.AsSpan()) { }

    /// <summary>
    /// Copies exactly 32 bytes from the source span into this buffer.
    /// </summary>
    /// <param name="source">The source span (at least 32 bytes).</param>
    /// <returns>A new <see cref="Fixed256"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the source span is smaller than 32 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed256 CopyFrom(ReadOnlySpan<byte> source) => new(source);

    /// <summary>
    /// Writes the content of this 256-bit buffer into the destination span.
    /// </summary>
    /// <param name="destination">The destination span (at least 32 bytes).</param>
    /// <exception cref="ArgumentException">Thrown when the destination span is smaller than 32 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void WriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException("Destination must be at least 32 bytes.", nameof(destination));
        }

        ref byte dstRef = ref MemoryMarshal.GetReference(destination);
        Unsafe.WriteUnaligned(ref dstRef, _v1);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dstRef, 8), _v2);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dstRef, 16), _v3);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dstRef, 24), _v4);
    }

    /// <summary>
    /// Attempts to write the content of this 256-bit buffer into the destination span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <returns><see langword="true"/> if the buffer was written successfully; otherwise, <see langword="false"/> (destination too short).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryWriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        this.WriteTo(destination);
        return true;
    }

    /// <summary>
    /// Converts this 256-bit buffer to a 32-byte array.
    /// </summary>
    /// <returns>A new 32-byte array containing the buffer state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte[] ToByteArray()
    {
        byte[] arr = new byte[Size];
        this.WriteTo(arr);
        return arr;
    }

    /// <summary>
    /// Returns a hexadecimal string representation of the 256-bit buffer.
    /// </summary>
    public override readonly string ToString()
    {
        Span<byte> data = stackalloc byte[Size];
        this.WriteTo(data);
        return Convert.ToHexString(data);
    }

    /// <summary>
    /// Gets a 256-bit buffer with all bits set to zero.
    /// </summary>
    public static Fixed256 Empty => default;

    /// <summary>
    /// Returns <see langword="true"/> if all bits in the buffer are zero.
    /// This comparison is performed in constant-time.
    /// </summary>
    public readonly bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        get => (_v1 | _v2 | _v3 | _v4) == 0;
    }

    /// <summary>
    /// Compares two <see cref="Fixed256"/> instances in constant-time.
    /// </summary>
    /// <param name="other">The other instance to compare.</param>
    /// <returns><see langword="true"/> if both buffers are bitwise identical.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public readonly bool Equals(Fixed256 other)
    {
        ulong res = (_v1 ^ other._v1) | (_v2 ^ other._v2) | (_v3 ^ other._v3) | (_v4 ^ other._v4);
        return res == 0;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Fixed256 other && this.Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(_v1, _v2, _v3, _v4);

    /// <summary>
    /// Returns a read-only span covering the 32-byte buffer.
    /// </summary>
    public readonly ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref Unsafe.AsRef(in _v1)), Size);

    /// <inheritdoc/>
    public static bool operator ==(Fixed256 left, Fixed256 right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(Fixed256 left, Fixed256 right) => !left.Equals(right);
}
