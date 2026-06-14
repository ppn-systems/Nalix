// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace Nalix.Abstractions;

/// <summary>
/// Represents a high-performance unmanaged key for object maps, backed by a 64-bit integer hash.
/// </summary>
public readonly struct AttributeKey : IEquatable<AttributeKey>
{
    private readonly ulong _hash;

#if DEBUG
    private readonly string? _name;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeKey"/> struct with a precomputed hash.
    /// </summary>
    /// <param name="hash">The precomputed 64-bit hash.</param>
    /// <param name="name">The name of the attribute (retained only in debug builds).</param>
    public AttributeKey(ulong hash, string? name = null)
    {
        _hash = hash;
#if DEBUG
        _name = name;
#endif
    }

    /// <summary>
    /// Creates an <see cref="AttributeKey"/> from a string name by computing a stable 64-bit hash.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>A new <see cref="AttributeKey"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AttributeKey FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        ulong hash = ComputeStableHash64(name);
        return new AttributeKey(hash, name);
    }

    /// <summary>
    /// Computes a stable 64-bit FNV-1a hash for the given string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeStableHash64(string value)
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < value.Length; i++)
        {
            hash = (hash ^ value[i]) * 1099511628211UL;
        }
        return hash;
    }

    /// <inheritdoc/>
    public bool Equals(AttributeKey other) => _hash == other._hash;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AttributeKey other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _hash.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() =>
#if DEBUG
        _name ?? $"0x{_hash:X16}";
#else
        $"0x{_hash:X16}";
#endif


    /// <summary>
    /// Compares two <see cref="AttributeKey"/> instances for equality.
    /// </summary>
    public static bool operator ==(AttributeKey left, AttributeKey right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="AttributeKey"/> instances for inequality.
    /// </summary>
    public static bool operator !=(AttributeKey left, AttributeKey right) => !left.Equals(right);
}
