// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Primitives;

/// <summary>
/// Represents an unsigned 56-bit integer.
/// </summary>
/// <remarks>
/// The value is stored in a 64-bit unsigned integer, but only the lower
/// 56 bits are considered valid and are enforced by the constructor and
/// arithmetic operations.
/// </remarks>
[System.Runtime.InteropServices.ComVisible(true)]
[System.Diagnostics.DebuggerDisplay("{ToString(),nq}")]
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
public readonly struct UInt56 :
    System.IComparable,
    System.IComparable<UInt56>,
    System.IEquatable<UInt56>,
    System.IFormattable
{
    private readonly System.UInt64 _value;

    /// <summary>
    /// Represents the largest possible value of <see cref="UInt56"/>.
    /// </summary>
    public const System.UInt64 MaxValue = (1UL << 56) - 1; // 0x00FFFFFFFFFFFFFF

    /// <summary>
    /// Represents the smallest possible value of <see cref="UInt56"/>.
    /// </summary>
    public const System.UInt64 MinValue = 0UL;

    /// <summary>
    /// Represents the <see cref="UInt56"/> value 0.
    /// </summary>
    public static readonly UInt56 Zero = new(0UL);

    /// <summary>
    /// Represents the largest possible <see cref="UInt56"/> value.
    /// </summary>
    public static readonly UInt56 Max = new(MaxValue);

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct
    /// to a specified 64-bit unsigned integer value.
    /// </summary>
    /// <param name="value">
    /// The value to assign to the new instance. The value must be between
    /// <see cref="MinValue"/> and <see cref="MaxValue"/>, inclusive.
    /// </param>
    /// <exception cref="System.OverflowException">
    /// <paramref name="value"/> is less than <see cref="MinValue"/> or
    /// greater than <see cref="MaxValue"/>.
    /// </exception>
    public UInt56(System.UInt64 value)
    {
        if (value > MaxValue)
        {
            throw new System.OverflowException(
                $"Value {value} is outside the range of a UInt56 (0..{MaxValue}).");
        }

        _value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct
    /// from a pre-validated value.
    /// </summary>
    /// <param name="value">
    /// The underlying value. Only the lower 56 bits are preserved.
    /// </param>
    /// <param name="_">
    /// A dummy parameter used to differentiate this constructor from the
    /// public one. Callers are responsible for ensuring that <paramref name="value"/>
    /// is already within the valid range.
    /// </param>
    private UInt56(System.UInt64 value, System.Boolean _) => _value = value & MaxValue;

    #region Conversions

    /// <summary>
    /// Defines an implicit conversion of a <see cref="System.Byte"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(System.Byte value) => new(value);

    /// <summary>
    /// Defines an implicit conversion of a <see cref="System.UInt16"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(System.UInt16 value) => new(value);

    /// <summary>
    /// Defines an implicit conversion of a <see cref="System.UInt32"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(System.UInt32 value) => new(value);

    /// <summary>
    /// Defines an implicit conversion of a non-negative <see cref="System.Int32"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    /// <exception cref="System.OverflowException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public static implicit operator UInt56(System.Int32 value)
    {
        if (value < 0)
        {
            throw new System.OverflowException("Cannot convert negative int to UInt56.");
        }

        return new UInt56((System.UInt64)value);
    }

    /// <summary>
    /// Defines an explicit conversion of a <see cref="System.UInt64"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    /// <exception cref="System.OverflowException">
    /// <paramref name="value"/> is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static explicit operator UInt56(System.UInt64 value) => new(value);

    /// <summary>
    /// Defines an explicit conversion of a <see cref="UInt56"/> to a <see cref="System.UInt64"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="System.UInt64"/> that is equivalent to <paramref name="value"/>.</returns>
    public static explicit operator System.UInt64(UInt56 value) => value._value;

    /// <summary>
    /// Converts this instance to a 64-bit unsigned integer.
    /// </summary>
    /// <returns>The 64-bit unsigned integer equivalent of this value.</returns>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.UInt64 ToUInt64() => _value;

    #endregion Conversions

    #region Equality and comparison

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(UInt56 other) => _value == other._value;

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Boolean Equals(System.Object obj) => obj is UInt56 other && Equals(other);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.Int32 GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public System.Int32 CompareTo(UInt56 other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    System.Int32 System.IComparable.CompareTo(System.Object obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (obj is UInt56 other)
        {
            return CompareTo(other);
        }

        throw new System.ArgumentException("Object must be of type UInt56.", nameof(obj));
    }

    /// <summary>
    /// Indicates whether two <see cref="UInt56"/> values are equal.
    /// </summary>
    public static System.Boolean operator ==(UInt56 left, UInt56 right) => left._value == right._value;

    /// <summary>
    /// Indicates whether two <see cref="UInt56"/> values are not equal.
    /// </summary>
    public static System.Boolean operator !=(UInt56 left, UInt56 right) => left._value != right._value;

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is less than another specified <see cref="UInt56"/>.
    /// </summary>
    public static System.Boolean operator <(UInt56 left, UInt56 right) => left._value < right._value;

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is less than or equal to another specified <see cref="UInt56"/>.
    /// </summary>
    public static System.Boolean operator <=(UInt56 left, UInt56 right) => left._value <= right._value;

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is greater than another specified <see cref="UInt56"/>.
    /// </summary>
    public static System.Boolean operator >(UInt56 left, UInt56 right) => left._value > right._value;

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is greater than or equal to another specified <see cref="UInt56"/>.
    /// </summary>
    public static System.Boolean operator >=(UInt56 left, UInt56 right) => left._value >= right._value;

    #endregion Equality and comparison

    #region Formatting and parsing

    /// <summary>
    /// Creates a <see cref="UInt56"/> value from its component parts.
    /// </summary>
    /// <param name="type">The type component (upper 8 bits).</param>
    /// <param name="machineId">The machine identifier (next 16 bits).</param>
    /// <param name="value">The value component (lower 32 bits).</param>
    /// <returns>
    /// A <see cref="UInt56"/> constructed from the specified <paramref name="type"/>, <paramref name="machineId"/>, and <paramref name="value"/>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    public static UInt56 FromParts(System.Byte type, System.UInt16 machineId, System.UInt32 value)
        => new(((System.UInt64)type << 48) | ((System.UInt64)machineId << 32) | value);

    /// <summary>
    /// Decomposes this <see cref="UInt56"/> value into its component parts.
    /// </summary>
    /// <param name="type">Receives the type component (upper 8 bits).</param>
    /// <param name="machineId">Receives the machine identifier (next 16 bits).</param>
    /// <param name="value">Receives the value component (lower 32 bits).</param>
    [System.Diagnostics.Contracts.Pure]
    public void Decompose(out System.Byte type, out System.UInt16 machineId, out System.UInt32 value)
    {
        System.UInt64 raw = _value;
        value = (System.UInt32)(raw & 0xFFFFFFFFUL);
        machineId = (System.UInt16)((raw >> 32) & 0xFFFFUL);
        type = (System.Byte)((raw >> 48) & 0xFFUL);
    }

    /// <summary>
    /// Converts the numeric value of this instance to its equivalent string representation.
    /// </summary>
    /// <returns>
    /// The string representation of the value of this instance, formatted using the current culture.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString() => _value.ToString();

    /// <summary>
    /// Converts the numeric value of this instance to its equivalent string representation using the specified format provider.
    /// </summary>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information.
    /// </param>
    /// <returns>
    /// The string representation of the value of this instance, as specified by <paramref name="provider"/>.
    /// </returns>
    public System.String ToString(System.IFormatProvider provider) => _value.ToString(provider);

    /// <summary>
    /// Converts the numeric value of this instance to its equivalent string representation
    /// using the specified format and culture-specific format information.
    /// </summary>
    /// <param name="format">A numeric format string.</param>
    /// <param name="formatProvider">An object that supplies culture-specific formatting information.</param>
    /// <returns>
    /// The string representation of the value of this instance, as specified by <paramref name="format"/>
    /// and <paramref name="formatProvider"/>.
    /// </returns>
    public System.String ToString(System.String format, System.IFormatProvider formatProvider)
        => _value.ToString(format, formatProvider);

    /// <summary>
    /// Converts the string representation of a number to its <see cref="UInt56"/> equivalent.
    /// </summary>
    /// <param name="s">A string that contains the number to convert.</param>
    /// <returns>A <see cref="UInt56"/> equivalent of the number contained in <paramref name="s"/>.</returns>
    /// <exception cref="System.FormatException">
    /// <paramref name="s"/> is not in a valid format or represents a value that is outside
    /// the range of the <see cref="UInt56"/> type.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static UInt56 Parse(System.String s)
        => Parse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Converts the string representation of a number in a specified culture-specific format
    /// to its <see cref="UInt56"/> equivalent.
    /// </summary>
    /// <param name="s">A string that contains the number to convert.</param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <returns>A <see cref="UInt56"/> equivalent of the number contained in <paramref name="s"/>.</returns>
    /// <exception cref="System.FormatException">
    /// <paramref name="s"/> is not in a valid format or represents a value that is outside
    /// the range of the <see cref="UInt56"/> type.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static UInt56 Parse(System.String s, System.IFormatProvider provider)
        => Parse(s, System.Globalization.NumberStyles.Integer, provider);

    /// <summary>
    /// Converts the string representation of a number in a specified style and culture-specific format
    /// to its <see cref="UInt56"/> equivalent.
    /// </summary>
    /// <param name="s">A string that contains the number to convert.</param>
    /// <param name="style">
    /// A bitwise combination of enumeration values that indicates the permitted format of <paramref name="s"/>.
    /// </param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <returns>A <see cref="UInt56"/> equivalent of the number contained in <paramref name="s"/>.</returns>
    /// <exception cref="System.FormatException">
    /// <paramref name="s"/> is not in a valid format or represents a value that is outside
    /// the range of the <see cref="UInt56"/> type.
    /// </exception>
    [System.Diagnostics.Contracts.Pure]
    public static UInt56 Parse(System.String s, System.Globalization.NumberStyles style, System.IFormatProvider provider)
    {
        if (!TryParse(s, style, provider, out var result))
        {
            throw new System.FormatException(
                "Input string was not in a correct format or was out of range for UInt56.");
        }

        return result;
    }

    /// <summary>
    /// Tries to convert the string representation of a number to its <see cref="UInt56"/> equivalent.
    /// A return value indicates whether the conversion succeeded.
    /// </summary>
    /// <param name="s">A string that contains the number to convert.</param>
    /// <param name="result">
    /// When this method returns, contains the <see cref="UInt56"/> value equivalent to the number
    /// contained in <paramref name="s"/>, if the conversion succeeded, or <see cref="Zero"/> if it failed.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    public static System.Boolean TryParse(
        System.String s,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UInt56 result)
        => TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out result);

    /// <summary>
    /// Tries to convert the string representation of a number in a specified style and culture-specific format
    /// to its <see cref="UInt56"/> equivalent. A return value indicates whether the conversion succeeded.
    /// </summary>
    /// <param name="s">A string that contains the number to convert.</param>
    /// <param name="style">
    /// A bitwise combination of enumeration values that indicates the permitted format of <paramref name="s"/>.
    /// </param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the <see cref="UInt56"/> value equivalent to the number
    /// contained in <paramref name="s"/>, if the conversion succeeded, or <see cref="Zero"/> if it failed.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.
    /// </returns>
    [System.Diagnostics.Contracts.Pure]
    public static System.Boolean TryParse(
        System.String s,
        System.Globalization.NumberStyles style,
        System.IFormatProvider provider,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UInt56 result)
    {
        result = default;

        if (System.String.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        if (!System.UInt64.TryParse(s, style, provider, out var u))
        {
            return false;
        }

        if (u > MaxValue)
        {
            return false;
        }

        result = new UInt56(u, true);
        return true;
    }

    /// <inheritdoc />
    System.String System.IFormattable.ToString(System.String format, System.IFormatProvider formatProvider) => _value.ToString(format, formatProvider);

    #endregion Formatting and parsing

    #region Arithmetic

    /// <summary>
    /// Throws an <see cref="System.OverflowException"/> if the specified raw value
    /// is outside the range of the <see cref="UInt56"/> type.
    /// </summary>
    /// <param name="raw">The raw value to validate.</param>
    /// <exception cref="System.OverflowException">
    /// <paramref name="raw"/> is outside the valid range of <see cref="UInt56"/>.
    /// </exception>
    private static void CheckOverflow(System.UInt64 raw)
    {
        if ((raw & ~MaxValue) != 0UL)
        {
            throw new System.OverflowException(
                "Arithmetic operation produced a value that is out of range for UInt56.");
        }
    }

    #region Byte

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Byte b) => a + new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Byte b) => a - new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Byte b) => a * new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Byte b) => a / new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Byte b) => a % new UInt56(b);

    #endregion Byte

    #region SByte

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.SByte b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not add negative numbers to UInt56.");
        }

        return a + new UInt56((System.Byte)b);
    }


    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.SByte b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not subtract negative numbers from UInt56.");
        }

        return a - new UInt56((System.Byte)b);
    }


    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.SByte b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not multiply negative numbers by UInt56.");
        }

        return a * new UInt56((System.Byte)b);
    }


    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.SByte b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a / new UInt56((System.Byte)b);
    }


    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.SByte b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a % new UInt56((System.Byte)b);
    }

    #endregion SByte

    #region Int16

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Int16 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not add negative numbers to UInt56.");
        }

        return a + new UInt56((System.UInt16)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Int16 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not subtract negative numbers from UInt56.");
        }

        return a - new UInt56((System.UInt16)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Int16 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not multiply negative numbers by UInt56.");
        }

        return a * new UInt56((System.UInt16)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Int16 b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a / new UInt56((System.UInt16)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Int16 b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a % new UInt56((System.UInt16)b);
    }


    #endregion Int16

    #region UInt16

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.UInt16 b) => a + new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.UInt16 b) => a - new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.UInt16 b) => a * new UInt56(b);


    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.UInt16 b)
    {
        if (b == 0)
        {
            throw new System.DivideByZeroException();
        }

        return a / new UInt56(b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.UInt16 b)
    {
        if (b == 0)
        {
            throw new System.DivideByZeroException();
        }

        return a % new UInt56(b);
    }


    #endregion UInt16

    #region Int32

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Int32 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not add negative numbers to UInt56.");
        }

        return a + new UInt56((System.UInt32)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Int32 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not subtract negative numbers from UInt56.");
        }

        return a - new UInt56((System.UInt32)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Int32 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not multiply negative numbers by UInt56.");
        }

        return a * new UInt56((System.UInt32)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Int32 b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a / new UInt56((System.UInt32)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Int32 b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a % new UInt56((System.UInt32)b);
    }

    #endregion Int32

    #region UInt32

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.UInt32 b) => a + new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.UInt32 b) => a - new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.UInt32 b) => a * new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.UInt32 b)
    {
        if (b == 0)
        {
            throw new System.DivideByZeroException();
        }

        return a / new UInt56(b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.UInt32 b)
    {
        if (b == 0)
        {
            throw new System.DivideByZeroException();
        }

        return a % new UInt56(b);
    }

    #endregion UInt32

    #region UInt56

    /// <summary>
    /// Adds two specified <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The sum of <paramref name="a"/> and <paramref name="b"/>.</returns>
    /// <exception cref="System.OverflowException">
    /// The result is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static UInt56 operator +(UInt56 a, UInt56 b)
    {
        System.UInt64 raw = a._value + b._value;
        CheckOverflow(raw);
        return new UInt56(raw, true);
    }

    /// <summary>
    /// Subtracts one specified <see cref="UInt56"/> value from another.
    /// </summary>
    /// <param name="a">The value to subtract from.</param>
    /// <param name="b">The value to subtract.</param>
    /// <returns>The result of <paramref name="a"/> minus <paramref name="b"/>.</returns>
    /// <exception cref="System.OverflowException">
    /// The result would be negative.
    /// </exception>
    public static UInt56 operator -(UInt56 a, UInt56 b)
    {
        if (a._value < b._value)
        {
            throw new System.OverflowException("Result would be negative; UInt56 is unsigned.");
        }

        return new UInt56(a._value - b._value, true);
    }

    /// <summary>
    /// Multiplies two specified <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The product of <paramref name="a"/> and <paramref name="b"/>.</returns>
    /// <exception cref="System.OverflowException">
    /// The result is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static UInt56 operator *(UInt56 a, UInt56 b)
    {
        if (a._value == 0 || b._value == 0)
        {
            return Zero;
        }

        // if a > MaxValue / b => overflow
        if (a._value > MaxValue / b._value)
        {
            throw new System.OverflowException("Multiplication overflow for UInt56.");
        }

        return new UInt56(a._value * b._value, true);
    }

    /// <summary>
    /// Divides one specified <see cref="UInt56"/> value by another.
    /// </summary>
    /// <param name="a">The value to be divided.</param>
    /// <param name="b">The value to divide by.</param>
    /// <returns>The result of <paramref name="a"/> divided by <paramref name="b"/>.</returns>
    /// <exception cref="System.DivideByZeroException">
    /// <paramref name="b"/> is zero.
    /// </exception>
    public static UInt56 operator /(UInt56 a, UInt56 b)
    {
        if (b._value == 0UL)
        {
            throw new System.DivideByZeroException();
        }

        return new UInt56(a._value / b._value, true);
    }

    /// <summary>
    /// Calculates the remainder from division of one specified <see cref="UInt56"/> value by another.
    /// </summary>
    /// <param name="a">The value to be divided.</param>
    /// <param name="b">The value to divide by.</param>
    /// <returns>
    /// The remainder resulting from the division of <paramref name="a"/> by <paramref name="b"/>.
    /// </returns>
    /// <exception cref="System.DivideByZeroException">
    /// <paramref name="b"/> is zero.
    /// </exception>
    public static UInt56 operator %(UInt56 a, UInt56 b)
    {
        if (b._value == 0UL)
        {
            throw new System.DivideByZeroException();
        }

        return new UInt56(a._value % b._value, true);
    }

    /// <summary>
    /// Returns the bitwise complement of a <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="a">A value.</param>
    /// <returns>The bitwise complement of <paramref name="a"/>.</returns>
    public static UInt56 operator ~(UInt56 a) => new((~a._value) & MaxValue, true);

    /// <summary>
    /// Performs a bitwise AND operation on two <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>The bitwise AND of <paramref name="a"/> and <paramref name="b"/>.</returns>
    public static UInt56 operator &(UInt56 a, UInt56 b) => new(a._value & b._value, true);

    /// <summary>
    /// Performs a bitwise OR operation on two <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>The bitwise OR of <paramref name="a"/> and <paramref name="b"/>.</returns>
    public static UInt56 operator |(UInt56 a, UInt56 b) => new(a._value | b._value, true);

    /// <summary>
    /// Performs a bitwise exclusive OR (XOR) operation on two <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>The bitwise XOR of <paramref name="a"/> and <paramref name="b"/>.</returns>
    public static UInt56 operator ^(UInt56 a, UInt56 b) => new(a._value ^ b._value, true);

    /// <summary>
    /// Shifts a <see cref="UInt56"/> value left by a specified number of bits.
    /// </summary>
    /// <param name="a">The value to shift.</param>
    /// <param name="shift">The number of bits to shift.</param>
    /// <returns>The result of shifting <paramref name="a"/> left by <paramref name="shift"/> bits.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="shift"/> is negative.
    /// </exception>
    public static UInt56 operator <<(UInt56 a, System.Int32 shift)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegative(shift);
        return new UInt56((a._value << shift) & MaxValue, true);
    }

    /// <summary>
    /// Shifts a <see cref="UInt56"/> value right by a specified number of bits.
    /// </summary>
    /// <param name="a">The value to shift.</param>
    /// <param name="shift">The number of bits to shift.</param>
    /// <returns>The result of shifting <paramref name="a"/> right by <paramref name="shift"/> bits.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="shift"/> is negative.
    /// </exception>
    public static UInt56 operator >>(UInt56 a, System.Int32 shift)
    {
        System.ArgumentOutOfRangeException.ThrowIfNegative(shift);
        return new UInt56(a._value >> shift, true);
    }

    /// <summary>
    /// Increments a <see cref="UInt56"/> value by 1.
    /// </summary>
    /// <param name="a">The value to increment.</param>
    /// <returns>The value of <paramref name="a"/> incremented by 1.</returns>
    /// <exception cref="System.OverflowException">
    /// The result is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static UInt56 operator ++(UInt56 a)
    {
        if (a._value == MaxValue)
        {
            throw new System.OverflowException("Overflow on increment.");
        }

        return new UInt56(a._value + 1UL, true);
    }

    /// <summary>
    /// Decrements a <see cref="UInt56"/> value by 1.
    /// </summary>
    /// <param name="a">The value to decrement.</param>
    /// <returns>The value of <paramref name="a"/> decremented by 1.</returns>
    /// <exception cref="System.OverflowException">
    /// The result would be less than <see cref="MinValue"/>.
    /// </exception>
    public static UInt56 operator --(UInt56 a)
    {
        if (a._value == 0UL)
        {
            throw new System.OverflowException("Underflow on decrement.");
        }

        return new UInt56(a._value - 1UL, true);
    }

    #endregion UInt56

    #region Int64

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Int64 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not add negative numbers to UInt56.");
        }

        return a + new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Int64 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not subtract negative numbers from UInt56.");
        }

        return a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Int64 b)
    {
        if (b < 0)
        {
            throw new System.OverflowException("Do not multiply negative numbers by UInt56.");
        }

        return a * new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Int64 b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a / new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Int64 b)
    {
        if (b <= 0)
        {
            throw new System.OverflowException("Divisor must be > 0 for UInt56.");
        }

        return a % new UInt56((System.UInt64)b);
    }

    #endregion Int64

    #region UInt64 

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.UInt64 b) => a + new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.UInt64 b) => a - new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.UInt64 b) => a * new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.UInt64 b)
    {
        if (b == 0)
        {
            throw new System.DivideByZeroException();
        }

        return a / new UInt56(b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.UInt64 b)
    {
        if (b == 0)
        {
            throw new System.DivideByZeroException();
        }

        return a % new UInt56(b);
    }

    #endregion UInt64

    #region Single 

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Single b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid float value for UInt56.");
        }

        return a + new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Single b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid float value for UInt56.");
        }

        return a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Single b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid float value for UInt56.");
        }

        return a * new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Single b)
    {
        if (b <= 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid float division value for UInt56.");
        }

        return a / new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Single b)
    {
        if (b <= 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid float division value for UInt56.");
        }

        return a % new UInt56((System.UInt64)b);
    }

    #endregion Single

    #region Double

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Double b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid double value for UInt56.");
        }

        return a + new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Double b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid double value for UInt56.");
        }

        return a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Double b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid double value for UInt56.");
        }

        return a * new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Double b)
    {
        if (b <= 0 || b > MaxValue)
        {
            throw new System.OverflowException("Double value division is not suitable for UInt56.");
        }

        return a / new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Double b)
    {
        if (b <= 0 || b > MaxValue)
        {
            throw new System.OverflowException("Double value division is not suitable for UInt56.");
        }

        return a % new UInt56((System.UInt64)b);
    }

    #endregion Double

    #region Decimal

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Decimal b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid decimal value for UInt56.");
        }

        return a + new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Decimal b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid decimal value for UInt56.");
        }

        return a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Decimal b)
    {
        if (b < 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid decimal value for UInt56.");
        }

        return a * new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Decimal b)
    {
        if (b <= 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid decimal division value for UInt56.");
        }

        return a / new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Decimal b)
    {
        if (b <= 0 || b > MaxValue)
        {
            throw new System.OverflowException("Invalid decimal division value for UInt56.");
        }

        return a % new UInt56((System.UInt64)b);
    }

    #endregion Decimal

    #endregion Arithmetic
}
