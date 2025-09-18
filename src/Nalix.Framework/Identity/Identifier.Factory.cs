// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;

namespace Nalix.Framework.Identity;

public readonly partial struct Identifier : System.IEquatable<Identifier>
{
    #region Equality and Comparison Methods

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="IIdentifier"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(IIdentifier? other)
        => other is Identifier compactId && Equals(compactId);

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="Identifier"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(Identifier other)
    {
        // Optimize comparison by treating the struct as a single 64-bit value
        System.UInt64 thisValue = GetCombinedValue();
        System.UInt64 otherValue = other.GetCombinedValue();
        return thisValue == otherValue;
    }

    /// <summary>
    /// Determines whether this identifier is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the object is a <see cref="Identifier"/> and is equal to this instance;
    /// otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    public override System.Boolean Equals(System.Object? obj)
        => obj is Identifier other && Equals(other);

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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Int32 GetHashCode()
    {
        // Use the combined value for consistent hashing
        System.UInt64 combinedValue = GetCombinedValue();
        return combinedValue.GetHashCode();
    }

    #endregion Equality and Comparison Methods

    #region Operators

    /// <summary>
    /// Determines whether two <see cref="Identifier"/> instances are equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator ==(Identifier left, Identifier right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Identifier"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are not equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator !=(Identifier left, Identifier right) => !left.Equals(right);

    #endregion Operators

    #region Private Helper Methods

    /// <summary>
    /// Gets the combined 64-bit representation of this identifier.
    /// </summary>
    /// <returns>A 64-bit unsigned integer containing all identifier components.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.UInt64 GetCombinedValue()
        => ((System.UInt64)_type << 48) | ((System.UInt64)MachineId << 32) | Value;

    /// <summary>
    /// Encodes a 64-bit unsigned integer to Base36 string representation.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>A Base36 encoded string.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String EncodeToBase36(System.UInt64 value)
    {
        if (value == 0)
        {
            return "0";
        }

        System.Span<System.Char> buffer = stackalloc System.Char[13]; // Maximum length for 64-bit Base36
        System.Byte charIndex = 0;

        do
        {
            buffer[charIndex++] = Base36Chars[(System.Byte)(value % 36)];
            value /= 36;
        } while (value > 0);

        // Reverse the buffer to get the correct order
        System.Span<System.Char> result = buffer[..charIndex];
        System.MemoryExtensions.Reverse(result);
        return new System.String(result);
    }

    #endregion Private Helper Methods
}