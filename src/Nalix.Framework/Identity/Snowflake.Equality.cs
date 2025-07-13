// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Primitives;

namespace Nalix.Framework.Identity;

public readonly partial struct Snowflake : System.IEquatable<Snowflake>, System.IComparable<Snowflake>
{
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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Int32 CompareTo(Snowflake other)
    {
        UInt56 a = ToUInt56();
        UInt56 b = other.ToUInt56();

        if (a > b)
        {
            return 1;
        }

        if (a < b)
        {
            return -1;
        }

        return 0;
    }

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
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 Compare(Snowflake a, Snowflake b) => a.CompareTo(b);

    /// <summary>
    /// Determines whether two <see cref="Snowflake"/> instances are equal.
    /// </summary>
    /// <param name="a">The first <see cref="Snowflake"/> to compare.</param>
    /// <param name="b">The second <see cref="Snowflake"/> to compare.</param>
    /// <returns>
    /// <c>true</c> if the two <see cref="Snowflake"/> instances are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean Equals(Snowflake a, Snowflake b) => a.Equals(b);

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="ISnowflake"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean Equals(ISnowflake? other) => other is Snowflake s && Equals(s);

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="Snowflake"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean Equals(Snowflake other)
        => __combined == other.__combined;

    /// <summary>
    /// Determines whether this identifier is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the object is a <see cref="Snowflake"/> and is equal to this instance;
    /// otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public override System.Boolean Equals(System.Object? obj) => obj is Snowflake other && Equals(other);

    /// <summary>
    /// Returns the hash code for this identifier.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    /// <remarks>
    /// The hash code is computed efficiently using all components of the identifier
    /// and is suitable for use in hash-based collections like <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.
    /// </remarks>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public override System.Int32 GetHashCode() => ToUInt56().GetHashCode();

    #endregion Equality and Comparison Methods

    #region Operators

    /// <summary>
    /// Determines whether two <see cref="Snowflake"/> instances are equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean operator ==(Snowflake left, Snowflake right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Snowflake"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are not equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean operator !=(Snowflake left, Snowflake right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is less than another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is less than <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean operator <(Snowflake a, Snowflake b) => a.ToUInt56() < b.ToUInt56();

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is greater than another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is greater than <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean operator >(Snowflake a, Snowflake b) => a.ToUInt56() > b.ToUInt56();

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is less than or equal to another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is less than or equal to <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean operator <=(Snowflake a, Snowflake b) => a.ToUInt56() <= b.ToUInt56();

    /// <summary>
    /// Determines whether one <see cref="Snowflake"/> identifier is greater than or equal to another.
    /// </summary>
    /// <param name="a">The first identifier to compare.</param>
    /// <param name="b">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="a"/> is greater than or equal to <paramref name="b"/>; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean operator >=(Snowflake a, Snowflake b) => a.ToUInt56() >= b.ToUInt56();

    #endregion Operators
}