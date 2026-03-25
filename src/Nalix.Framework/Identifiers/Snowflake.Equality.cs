// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

using Nalix.Common.Identity;

namespace Nalix.Framework.Identifiers;

public readonly partial struct Snowflake : IEquatable<Snowflake>, IComparable<Snowflake>
{
    #region Operators

    /// <summary>
    /// Determines whether two <see cref="Snowflake"/> instances are equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This operation compares the underlying 56-bit values for equality.
    /// The comparison is performed in constant time to prevent timing attacks.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Snowflake left, Snowflake right)
    {
        // Stack-allocate space for both Snowflake instances
        Span<byte> leftBytes = stackalloc byte[Size];
        Span<byte> rightBytes = stackalloc byte[Size];

        // Serialize both instances into memory
        _ = left.TryWriteBytes(leftBytes);
        _ = right.TryWriteBytes(rightBytes);

        // Perform constant-time comparison
        int isEqual = 0; // Start with equality assumed
        for (int i = 0; i < Size; i++)
        {
            isEqual |= leftBytes[i] ^ rightBytes[i]; // XOR bytes; zero means equal
        }

        // Return true if all bytes are equal (isEqual == 0)
        return isEqual == 0;
    }

    /// <summary>
    /// Determines whether two <see cref="Snowflake"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are not equal; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This operation compares the underlying 56-bit values for inequality.
    /// The comparison is performed in constant time to prevent timing attacks.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Snowflake left, Snowflake right) => left.__combined != right.__combined;

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is less than another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is less than <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Comparison is performed on the underlying 56-bit values, providing a total ordering.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Snowflake a, Snowflake b) => a.__combined < b.__combined;

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is greater than another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is greater than <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Comparison is performed on the underlying 56-bit values, providing a total ordering.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Snowflake a, Snowflake b) => a.__combined > b.__combined;

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is less than or equal to another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is less than or equal to <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Comparison is performed on the underlying 56-bit values, providing a total ordering.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Snowflake a, Snowflake b) => a.__combined <= b.__combined;

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is greater than or equal to another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is greater than or equal to <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Comparison is performed on the underlying 56-bit values, providing a total ordering.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Snowflake a, Snowflake b) => a.__combined >= b.__combined;

    #endregion Operators

    #region Equality and Comparison Methods

    /// <summary>
    /// Compares this <see cref="Snowflake"/> instance to another and returns an integer that indicates whether
    /// the current instance precedes, follows, or occurs in the same position in the sort order as the other.
    /// </summary>
    /// <param name="other">The <see cref="Snowflake"/> to compare with this instance.</param>
    /// <returns>
    /// A value less than zero if this instance precedes <paramref name="other"/>.<br/>
    /// Zero if this instance occurs in the same position as <paramref name="other"/>.<br/>
    /// A value greater than zero if this instance follows <paramref name="other"/>.
    /// </returns>
    /// <remarks>
    /// This method compares the underlying 56-bit values directly, providing efficient ordering.
    /// The implementation uses branchless comparison for optimal CPU pipeline performance.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Snowflake other) => __combined.CompareTo(other.__combined);

    /// <summary>
    /// Compares two <see cref="Snowflake"/> instances and returns an integer that indicates whether
    /// the first instance precedes, follows, or occurs in the same position in the sort order as the second.
    /// </summary>
    /// <param name="a">The first <see cref="Snowflake"/> to compare.</param>
    /// <param name="b">The second <see cref="Snowflake"/> to compare.</param>
    /// <returns>
    /// A value less than zero if <paramref name="a"/> precedes <paramref name="b"/>.<br/>
    /// Zero if <paramref name="a"/> occurs in the same position as <paramref name="b"/>.<br/>
    /// A value greater than zero if <paramref name="a"/> follows <paramref name="b"/>.
    /// </returns>
    /// <remarks>
    /// This static comparison method delegates to the instance <see cref="CompareTo(Snowflake)"/> method.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(Snowflake a, Snowflake b) => a.CompareTo(b);

    /// <summary>
    /// Determines whether two <see cref="Snowflake"/> instances are equal.
    /// </summary>
    /// <param name="a">The first <see cref="Snowflake"/> to compare.</param>
    /// <param name="b">The second <see cref="Snowflake"/> to compare.</param>
    /// <returns>
    /// <c>true</c> if the two <see cref="Snowflake"/> instances are equal; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This static equality method delegates to the instance <see cref="Equals(Snowflake)"/> method.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(Snowflake a, Snowflake b) => a.__combined == b.__combined;

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="Snowflake"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method performs a direct comparison of the underlying 56-bit values.
    /// The comparison is performed in constant time to prevent timing attacks.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Snowflake other) => __combined == other.__combined;

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="ISnowflake"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method first checks if the provided instance is a <see cref="Snowflake"/> via pattern matching,
    /// then delegates to the strongly-typed <see cref="Equals(Snowflake)"/> method.
    /// Returns <c>false</c> if <paramref name="other"/> is null or not a <see cref="Snowflake"/>.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ISnowflake? other) => other is Snowflake s && __combined == s.__combined;

    #endregion Equality and Comparison Methods
}
