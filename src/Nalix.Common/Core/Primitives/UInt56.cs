// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Core.Primitives;

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
    System.IFormattable,
    System.ISpanFormattable,
    System.IEquatable<UInt56>,
    System.IComparable<UInt56>,
    System.IUtf8SpanFormattable,
    System.ISpanParsable<UInt56>,
    System.Numerics.INumber<UInt56>,
    System.Numerics.IMinMaxValue<UInt56>,
    System.Numerics.IIncrementOperators<UInt56>,
    System.Numerics.IDecrementOperators<UInt56>,
    System.Numerics.IAdditiveIdentity<UInt56, UInt56>,
    System.Numerics.IUnaryPlusOperators<UInt56, UInt56>,
    System.Numerics.IMultiplicativeIdentity<UInt56, UInt56>,
    System.Numerics.IUnaryNegationOperators<UInt56, UInt56>,
    System.Numerics.IModulusOperators<UInt56, UInt56, UInt56>,
    System.Numerics.IBitwiseOperators<UInt56, UInt56, UInt56>,
    System.Numerics.IAdditionOperators<UInt56, UInt56, UInt56>,
    System.Numerics.IMultiplyOperators<UInt56, UInt56, UInt56>,
    System.Numerics.IDivisionOperators<UInt56, UInt56, UInt56>,
    System.Numerics.IShiftOperators<UInt56, System.Int32, UInt56>,
    System.Numerics.ISubtractionOperators<UInt56, UInt56, UInt56>,
    System.Numerics.IEqualityOperators<UInt56, UInt56, System.Boolean>,
    System.Numerics.IComparisonOperators<UInt56, UInt56, System.Boolean>
{
    #region Private Fields

    private readonly System.UInt64 _value;

    #endregion Private Fields

    #region Constants and static fields

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
    /// Gets the additive identity of the current type.
    /// </summary>
    public static UInt56 AdditiveIdentity => Zero;

    /// <summary>
    /// Gets the multiplicative identity of the current type.
    /// </summary>
    public static UInt56 MultiplicativeIdentity => new(1UL, true);

    /// <summary>
    /// Gets the value 1 for the type.
    /// </summary>
    static UInt56 System.Numerics.INumberBase<UInt56>.One => new(1UL, true);

    /// <summary>
    /// Gets the radix, or base, for the type.
    /// </summary>
    static System.Int32 System.Numerics.INumberBase<UInt56>.Radix => 2;

    /// <summary>
    /// Gets the value 0 for the type.
    /// </summary>
    static UInt56 System.Numerics.INumberBase<UInt56>.Zero => Zero;

    #endregion Constants and static fields

    #region IMinMaxValue<T> Implementation

    /// <summary>
    /// Gets the maximum value of the current type.
    /// </summary>
    static UInt56 System.Numerics.IMinMaxValue<UInt56>.MaxValue => Max;

    /// <summary>
    /// Gets the minimum value of the current type.
    /// </summary>
    static UInt56 System.Numerics.IMinMaxValue<UInt56>.MinValue => Zero;

    #endregion IMinMaxValue<T> Implementation

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct
    /// from a value with optional validation.
    /// </summary>
    /// <param name="value">The underlying value. </param>
    /// <param name="trusted">
    /// If <c>true</c>, assumes the value is already validated and within range.
    /// If <c>false</c>, performs validation and may throw on overflow.
    /// </param>
    /// <exception cref="System.OverflowException">
    /// <paramref name="trusted"/> is <c>false</c> and <paramref name="value"/>
    /// is greater than <see cref="MaxValue"/>.
    /// </exception>
    private UInt56(System.UInt64 value, System.Boolean trusted)
    {
        if (trusted)
        {
            // Trusted path: value assumed valid, just assign
            _value = value;
        }
        else
        {
            // Untrusted path: validate then assign
            if (value > MaxValue)
            {
                throw new System.OverflowException(
                    $"Value {value} is outside the range of a UInt56 (0..{MaxValue}).");
            }
            _value = value;
        }
    }

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
        : this(value, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct from its component parts.
    /// </summary>
    /// <param name="a">The type component (upper 8 bits).</param>
    /// <param name="b">The machine identifier component (next 16 bits).</param>
    /// <param name="c">The value component (lower 32 bits).</param>
    public UInt56(System.Byte a, System.UInt16 b, System.UInt32 c)
        : this(((System.UInt64)a << 48) | ((System.UInt64)b << 32) | c)
    {
    }

    #endregion Constructors

    #region Conversions

    /// <summary>
    /// Defines an implicit conversion of a <see cref="System.Byte"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(System.Byte value) => new(value, true);

    /// <summary>
    /// Defines an implicit conversion of a <see cref="System.UInt16"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(System.UInt16 value) => new(value, true);

    /// <summary>
    /// Defines an implicit conversion of a <see cref="System.UInt32"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(System.UInt32 value) => new(value, true);

    /// <summary>
    /// Defines an implicit conversion of a non-negative <see cref="System.Int32"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    /// <exception cref="System.OverflowException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public static implicit operator UInt56(System.Int32 value)
        => value < 0 ? throw new System.OverflowException("Cannot convert negative int to UInt56.") : new UInt56((System.UInt64)value, true);

    /// <summary>
    /// Defines an explicit conversion of a <see cref="UInt56"/> to a <see cref="System.UInt64"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="System.UInt64"/> that is equivalent to <paramref name="value"/>.</returns>
    public static explicit operator System.UInt64(UInt56 value) => value._value;

    /// <summary>
    /// Defines an explicit conversion of a <see cref="System.UInt64"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    /// <exception cref="System.OverflowException">
    /// <paramref name="value"/> is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static explicit operator UInt56(System.UInt64 value) => new(value, false);

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
        return obj is null
            ? 1
            : obj is UInt56 other ? CompareTo(other) : throw new System.ArgumentException("Object must be of type UInt56.", nameof(obj));
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

    #region Parsing

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
    public System.String ToString(System.String format, System.IFormatProvider formatProvider) => _value.ToString(format, formatProvider);

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
        return !TryParse(s, style, provider, out UInt56 result)
            ? throw new System.FormatException("Input string was not in a correct format or was out of range for UInt56.") : result;
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

        if (!System.UInt64.TryParse(s, style, provider, out System.UInt64 u))
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

    #endregion Parsing

    #region Arithmetic

    #region Byte

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Byte b)
    {
        System.UInt64 result = a._value + b;
        CheckOverflow(result); // Chỉ cần check overflow
        return new UInt56(result, true);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Byte b)
    {
        return a._value < b
            ? throw new System.OverflowException("Result would be negative; UInt56 is unsigned.")
            : new UInt56(a._value - b, true);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Byte b) => a * new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Byte b) => a / new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Byte b) => a % new UInt56(b);

    #endregion Byte

    #region SByte

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.SByte b) => b < 0 ? throw new System.OverflowException("Do not add negative numbers to UInt56.") : a + new UInt56((System.Byte)b);


    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.SByte b) => b < 0 ? throw new System.OverflowException("Do not subtract negative numbers from UInt56.") : a - new UInt56((System.Byte)b);


    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.SByte b) => b < 0 ? throw new System.OverflowException("Do not multiply negative numbers by UInt56.") : a * new UInt56((System.Byte)b);


    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.SByte b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a / new UInt56((System.Byte)b);


    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.SByte b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a % new UInt56((System.Byte)b);

    #endregion SByte

    #region Int16

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Int16 b) => b < 0 ? throw new System.OverflowException("Do not add negative numbers to UInt56.") : a + new UInt56((System.UInt16)b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Int16 b)
    {
        return b < 0
            ? throw new System.OverflowException("Do not subtract negative numbers from UInt56.")
            : a - new UInt56((System.UInt16)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Int16 b) => b < 0 ? throw new System.OverflowException("Do not multiply negative numbers by UInt56.") : a * new UInt56((System.UInt16)b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Int16 b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a / new UInt56((System.UInt16)b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Int16 b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a % new UInt56((System.UInt16)b);


    #endregion Int16

    #region UInt16

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.UInt16 b)
    {
        System.UInt64 result = a._value + b;
        CheckOverflow(result);
        return new UInt56(result, true);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.UInt16 b)
    {
        return a._value < b
            ? throw new System.OverflowException("Result would be negative; UInt56 is unsigned.")
            : new UInt56(a._value - b, true);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.UInt16 b) => a * new UInt56(b);


    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.UInt16 b) => b == 0 ? throw new System.DivideByZeroException() : a / new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.UInt16 b) => b == 0 ? throw new System.DivideByZeroException() : a % new UInt56(b);


    #endregion UInt16

    #region Int32

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Int32 b) => b < 0 ? throw new System.OverflowException("Do not add negative numbers to UInt56.") : a + new UInt56((System.UInt32)b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Int32 b)
    {
        return b < 0
            ? throw new System.OverflowException("Do not subtract negative numbers from UInt56.")
            : a - new UInt56((System.UInt32)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Int32 b) => b < 0 ? throw new System.OverflowException("Do not multiply negative numbers by UInt56.") : a * new UInt56((System.UInt32)b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Int32 b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a / new UInt56((System.UInt32)b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Int32 b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a % new UInt56((System.UInt32)b);

    #endregion Int32

    #region UInt32

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.UInt32 b)
    {
        System.UInt64 result = a._value + b;
        CheckOverflow(result);
        return new UInt56(result, true);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.UInt32 b)
    {
        return a._value < b
            ? throw new System.OverflowException("Result would be negative; UInt56 is unsigned.")
            : new UInt56(a._value - b, true);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.UInt32 b) => a * new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.UInt32 b) => b == 0 ? throw new System.DivideByZeroException() : a / new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.UInt32 b) => b == 0 ? throw new System.DivideByZeroException() : a % new UInt56(b);

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
        return a._value < b._value
            ? throw new System.OverflowException("Result would be negative; UInt56 is unsigned.")
            : new UInt56(a._value - b._value, true);
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
        return a._value > MaxValue / b._value
            ? throw new System.OverflowException("Multiplication overflow for UInt56.")
            : new UInt56(a._value * b._value, true);
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
    public static UInt56 operator /(UInt56 a, UInt56 b) => b._value == 0UL ? throw new System.DivideByZeroException() : new UInt56(a._value / b._value, true);

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
    public static UInt56 operator %(UInt56 a, UInt56 b) => b._value == 0UL ? throw new System.DivideByZeroException() : new UInt56(a._value % b._value, true);

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
    /// Shifts a <see cref="UInt56"/> value right by a specified number of bits using unsigned (logical) right shift.
    /// </summary>
    /// <param name="value">The value to shift. </param>
    /// <param name="shiftAmount">The number of bits to shift <paramref name="value"/> to the right.</param>
    /// <returns>
    /// The result of shifting <paramref name="value"/> right by <paramref name="shiftAmount"/> bits using unsigned right shift.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This operator performs an unsigned (logical) right shift, which means that the high-order bits
    /// are always filled with zeros regardless of the sign of the original number.  For unsigned types
    /// like <see cref="UInt56"/>, this behavior is identical to the standard right shift operator (<c>&gt;&gt;</c>).
    /// </para>
    /// <para>
    /// The shift amount is masked to ensure it stays within the valid range for a 56-bit value.
    /// If <paramref name="shiftAmount"/> is negative, an <see cref="System.ArgumentOutOfRangeException"/> is thrown.
    /// </para>
    /// <para>
    /// Unlike signed right shift, unsigned right shift always fills the vacated positions with zeros,
    /// ensuring that the result is always positive (or zero).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(0xFF00000000000000UL); // Large value using all 56 bits
    /// UInt56 result = value &gt;&gt;&gt; 8;                      // Shift right by 8 bits
    /// Console.WriteLine($"0x{result:X}");               // Output: 0x00FF000000000000
    ///
    /// // Comparison with signed vs unsigned shift (both same for unsigned types)
    /// UInt56 unsignedShift = value &gt;&gt;&gt; 4;   // Unsigned right shift
    /// UInt56 signedShift = value &gt;&gt; 4;     // Signed right shift
    /// Console.WriteLine(unsignedShift == signedShift); // Output: True
    /// </code>
    /// </example>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="shiftAmount"/> is negative.
    /// </exception>
    public static UInt56 operator >>>(UInt56 value, System.Int32 shiftAmount)
    {
        // Validate shift amount - negative shifts are not allowed
        System.ArgumentOutOfRangeException.ThrowIfNegative(shiftAmount);

        // For performance, mask the shift amount to avoid unnecessary work
        // Since UInt56 is 56-bit, we only need the lower 6 bits of shiftAmount
        // This handles cases where shiftAmount >= 56 efficiently
        shiftAmount &= 63; // Equivalent to shiftAmount % 64

        // If shift amount is >= 56, result is always 0
        if (shiftAmount >= 56)
        {
            return Zero;
        }

        // Perform the unsigned right shift
        // For unsigned types, >>> and >> are identical - both fill with zeros
        System.UInt64 result = value._value >>> shiftAmount;

        // Since we're shifting right, the result will always be <= original value
        // and thus within the valid UInt56 range, so we can use trusted constructor
        return new UInt56(result, true);
    }

    /// <summary>
    /// Increments a <see cref="UInt56"/> value by 1.
    /// </summary>
    /// <param name="a">The value to increment.</param>
    /// <returns>The value of <paramref name="a"/> incremented by 1.</returns>
    /// <exception cref="System.OverflowException">
    /// The result is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static UInt56 operator ++(UInt56 a) => a._value == MaxValue ? throw new System.OverflowException("Overflow on increment.") : new UInt56(a._value + 1UL, true);

    /// <summary>
    /// Decrements a <see cref="UInt56"/> value by 1.
    /// </summary>
    /// <param name="a">The value to decrement.</param>
    /// <returns>The value of <paramref name="a"/> decremented by 1.</returns>
    /// <exception cref="System.OverflowException">
    /// The result would be less than <see cref="MinValue"/>.
    /// </exception>
    public static UInt56 operator --(UInt56 a) => a._value == 0UL ? throw new System.OverflowException("Underflow on decrement.") : new UInt56(a._value - 1UL, true);

    /// <summary>
    /// Returns the arithmetic negation of a specified <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="value">The value to negate.</param>
    /// <returns>The result of negating <paramref name="value"/> using two's complement arithmetic.</returns>
    /// <remarks>
    /// <para>
    /// The unary minus operator performs two's complement negation. For unsigned types,
    /// this operation wraps around according to modular arithmetic rules.
    /// </para>
    /// <para>
    /// The result follows the mathematical formula: <c>result = (2^56) - value</c>
    /// where the computation is performed modulo 2^56.
    /// </para>
    /// <para>
    /// When <paramref name="value"/> is zero, the result is zero.
    /// For any non-zero value, the result will be a large positive number
    /// due to the modular arithmetic behavior of unsigned integer negation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 zero = UInt56.Zero;
    /// UInt56 negatedZero = -zero;  // Result: 0
    /// Console.WriteLine(negatedZero); // Output: 0
    ///
    /// UInt56 value = new UInt56(1);
    /// UInt56 negated = -value;     // Result:  MaxValue (wraparound)
    /// Console.WriteLine(negated);   // Output: 72057594037927935 (2^56 - 1)
    ///
    /// UInt56 five = new UInt56(5);
    /// UInt56 negatedFive = -five;  // Result: MaxValue - 4
    /// Console.WriteLine(negatedFive); // Output: 72057594037927931
    /// </code>
    /// </example>
    public static UInt56 operator -(UInt56 value)
    {
        if (value._value == 0)
        {
            return Zero; // -0 = 0
        }

        // Two's complement negation:  result = (2^56) - value
        // Since we're working with 56-bit values, this is equivalent to:
        // result = (MaxValue + 1) - value = MaxValue - value + 1
        System.UInt64 result = MaxValue + 1 - value._value;

        // The result is guaranteed to be within UInt56 range due to modular arithmetic
        return new UInt56(result, true);
    }

    /// <summary>
    /// Returns the value of the <see cref="UInt56"/> operand.  The sign of the operand is unchanged.
    /// </summary>
    /// <param name="value">The operand to return.</param>
    /// <returns>The value of the <paramref name="value"/> operand.</returns>
    /// <remarks>
    /// <para>
    /// The unary plus operator performs an identity operation, returning the same value
    /// that was passed to it. This operation has no computational overhead.
    /// </para>
    /// <para>
    /// This operator is provided for symmetry with other numeric types and can be
    /// useful in generic programming contexts where a unary plus operation might be expected.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(42);
    /// UInt56 positive = +value; // Identical to: UInt56 positive = value;
    ///
    /// Console.WriteLine(value == positive); // Output: True
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static UInt56 operator +(UInt56 value) => value;

    #endregion UInt56

    #region Int64

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Int64 b) => b < 0 ? throw new System.OverflowException("Do not add negative numbers to UInt56.") : a + new UInt56((System.UInt64)b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Int64 b)
    {
        return b < 0
            ? throw new System.OverflowException("Do not subtract negative numbers from UInt56.")
            : a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Int64 b) => b < 0 ? throw new System.OverflowException("Do not multiply negative numbers by UInt56.") : a * new UInt56((System.UInt64)b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Int64 b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a / new UInt56((System.UInt64)b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Int64 b) => b <= 0 ? throw new System.OverflowException("Divisor must be > 0 for UInt56.") : a % new UInt56((System.UInt64)b);

    #endregion Int64

    #region UInt64 

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.UInt64 b) => a + new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.UInt64 b) => a - new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.UInt64 b) => a * new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.UInt64 b) => b == 0 ? throw new System.DivideByZeroException() : a / new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.UInt64 b) => b == 0 ? throw new System.DivideByZeroException() : a % new UInt56(b);

    #endregion UInt64

    #region Single 

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Single b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid float value for UInt56.")
            : a + new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Single b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid float value for UInt56.")
            : a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Single b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid float value for UInt56.")
            : a * new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Single b)
    {
        return b is <= 0 or > MaxValue
            ? throw new System.OverflowException("Invalid float division value for UInt56.")
            : a / new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Single b)
    {
        return b is <= 0 or > MaxValue
            ? throw new System.OverflowException("Invalid float division value for UInt56.")
            : a % new UInt56((System.UInt64)b);
    }

    #endregion Single

    #region Double

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Double b)
    {
        if (b is < 0 or > MaxValue)
        {
            throw new System.OverflowException("Invalid double value for UInt56.");
        }

        System.UInt64 result = a._value + (System.UInt64)b;
        CheckOverflow(result);
        return new UInt56(result, true);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Double b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid double value for UInt56.")
            : a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Double b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid double value for UInt56.")
            : a * new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Double b)
    {
        return b is <= 0 or > MaxValue
            ? throw new System.OverflowException("Double value division is not suitable for UInt56.")
            : a / new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Double b)
    {
        return b is <= 0 or > MaxValue
            ? throw new System.OverflowException("Double value division is not suitable for UInt56.")
            : a % new UInt56((System.UInt64)b);
    }

    #endregion Double

    #region Decimal

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, System.Decimal b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid decimal value for UInt56.")
            : a + new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, System.Decimal b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid decimal value for UInt56.")
            : a - new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, System.Decimal b)
    {
        return b is < 0 or > MaxValue
            ? throw new System.OverflowException("Invalid decimal value for UInt56.")
            : a * new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, System.Decimal b)
    {
        return b is <= 0 or > MaxValue
            ? throw new System.OverflowException("Invalid decimal division value for UInt56.")
            : a / new UInt56((System.UInt64)b);
    }

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, System.Decimal b)
    {
        return b is <= 0 or > MaxValue
            ? throw new System.OverflowException("Invalid decimal division value for UInt56.")
            : a % new UInt56((System.UInt64)b);
    }

    #endregion Decimal

    #endregion Arithmetic

    #region ISpanFormattable Implementation

    /// <summary>
    /// Tries to format the value of the current <see cref="UInt56"/> instance into the provided span of characters.
    /// </summary>
    /// <param name="destination">
    /// When this method returns, contains the formatted representation of this instance as a span of characters.
    /// </param>
    /// <param name="charsWritten">
    /// When this method returns, contains the number of characters that were written in <paramref name="destination"/>.
    /// </param>
    /// <param name="format">
    /// A span containing the characters that represent a standard or custom format string that defines the acceptable format for this instance.
    /// </param>
    /// <param name="provider">
    /// An optional object that supplies culture-specific formatting information for <paramref name="destination"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides zero-allocation formatting by writing directly to the provided span.
    /// It supports all standard numeric format strings (D, X, N, F, etc.) and custom format strings.
    /// </para>
    /// <para>
    /// If the <paramref name="destination"/> span is too small to contain the formatted representation,
    /// the method returns <see langword="false"/> and <paramref name="charsWritten"/> is set to 0.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(12345678);
    /// Span&lt;char&gt; buffer = stackalloc char[32];
    ///
    /// if (value.TryFormat(buffer, out int charsWritten, "X", null))
    /// {
    ///     Console.WriteLine(buffer[..charsWritten]. ToString()); // Output: BC614E
    /// }
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryFormat(
        System.Span<System.Char> destination,
        out System.Int32 charsWritten,
        System.ReadOnlySpan<System.Char> format,
        System.IFormatProvider provider) =>
        // Delegate to the underlying UInt64's TryFormat method
        // This provides full format string support while maintaining performance
        _value.TryFormat(destination, out charsWritten, format, provider);

    #endregion ISpanFormattable Implementation

    #region IUtf8SpanFormattable Implementation

    /// <summary>
    /// Tries to format the value of the current <see cref="UInt56"/> instance as UTF-8 into the provided span of bytes.
    /// </summary>
    /// <param name="utf8Destination">
    /// When this method returns, contains the formatted representation of this instance as a span of UTF-8 bytes.
    /// </param>
    /// <param name="bytesWritten">
    /// When this method returns, contains the number of bytes that were written in <paramref name="utf8Destination"/>.
    /// </param>
    /// <param name="format">
    /// A span containing the characters that represent a standard or custom format string that defines the acceptable format for this instance.
    /// </param>
    /// <param name="provider">
    /// An optional object that supplies culture-specific formatting information for <paramref name="utf8Destination"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides zero-allocation UTF-8 formatting, which is optimal for network protocols,
    /// JSON serialization, and other scenarios where UTF-8 byte sequences are preferred.
    /// </para>
    /// <para>
    /// The method supports all standard numeric format strings and produces UTF-8 encoded output
    /// directly without intermediate string allocation.
    /// </para>
    /// <para>
    /// If the <paramref name="utf8Destination"/> span is too small to contain the formatted representation,
    /// the method returns <see langword="false"/> and <paramref name="bytesWritten"/> is set to 0.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(0xABCDEF123456UL);
    /// Span&lt;byte&gt; utf8Buffer = stackalloc byte[32];
    ///
    /// if (value.TryFormat(utf8Buffer, out int bytesWritten, "X", null))
    /// {
    ///     string result = System.Text.Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]);
    ///     Console.WriteLine(result); // Output: ABCDEF123456
    /// }
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryFormat(
        System.Span<System.Byte> utf8Destination,
        out System.Int32 bytesWritten,
        System.ReadOnlySpan<System.Char> format,
        System.IFormatProvider provider) =>
        // Delegate to the underlying UInt64's UTF-8 formatting method
        // This leverages the optimized UTF-8 formatting implementation in . NET
        _value.TryFormat(utf8Destination, out bytesWritten, format, provider);

    #endregion IUtf8SpanFormattable Implementation

    #region ISpanParsable<UInt56> Implementation

    /// <summary>
    /// Parses a span of characters into a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <returns>The result of parsing <paramref name="s"/>.</returns>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="s"/> is empty or contains only white space.
    /// </exception>
    /// <exception cref="System.FormatException">
    /// <paramref name="s"/> is not in the correct format.
    /// </exception>
    /// <exception cref="System. OverflowException">
    /// <paramref name="s"/> represents a value that is outside the range of <see cref="UInt56"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method provides zero-allocation parsing from character spans, making it ideal
    /// for high-performance scenarios where string allocation should be avoided.
    /// </para>
    /// <para>
    /// The method supports the same format as <see cref="Parse(System.String, System.IFormatProvider)"/>
    /// but operates directly on memory without creating intermediate strings.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; span = "123456789".AsSpan();
    /// UInt56 value = UInt56.Parse(span, CultureInfo.InvariantCulture);
    /// Console.WriteLine(value); // Output: 123456789
    /// </code>
    /// </example>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static UInt56 Parse(System.ReadOnlySpan<System.Char> s, System.IFormatProvider provider) => Parse(s, System.Globalization.NumberStyles.Integer, provider);

    /// <summary>
    /// Parses a span of characters into a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="style">
    /// A bitwise combination of the enumeration values that indicates the style elements
    /// that can be present in <paramref name="s"/>.
    /// </param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <returns>The result of parsing <paramref name="s"/>.</returns>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="s"/> is empty or contains only white space.
    /// </exception>
    /// <exception cref="System.FormatException">
    /// <paramref name="s"/> is not in the correct format.
    /// </exception>
    /// <exception cref="System.OverflowException">
    /// <paramref name="s"/> represents a value that is outside the range of <see cref="UInt56"/>.
    /// </exception>
    /// <remarks>
    /// This method provides fine-grained control over parsing behavior through the <paramref name="style"/> parameter,
    /// while maintaining zero-allocation performance characteristics.
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; hexSpan = "ABCDEF123456".AsSpan();
    /// UInt56 value = UInt56.Parse(hexSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    /// Console.WriteLine($"0x{value:X}"); // Output: 0xABCDEF123456
    /// </code>
    /// </example>
    [System.Diagnostics.Contracts.Pure]
    public static UInt56 Parse(
        System.ReadOnlySpan<System.Char> s,
        System.Globalization.NumberStyles style,
        System.IFormatProvider provider)
    {
        if (!TryParse(s, style, provider, out UInt56 result))
        {
            ThrowFormatException(s.ToString());
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a span of characters into a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the result of successfully parsing <paramref name="s"/>,
    /// or an undefined value on failure.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="s"/> was parsed successfully; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method provides zero-allocation parsing with graceful error handling.
    /// It's the preferred method for parsing when the input might be invalid.
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; span = "999999999999999". AsSpan();
    /// if (UInt56.TryParse(span, CultureInfo.InvariantCulture, out UInt56 value))
    /// {
    ///     Console.WriteLine($"Parsed:  {value}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("Failed to parse");
    /// }
    /// </code>
    /// </example>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryParse(
        System.ReadOnlySpan<System.Char> s,
        System.IFormatProvider provider,
        out UInt56 result) => TryParse(s, System.Globalization.NumberStyles.Integer, provider, out result);

    /// <summary>
    /// Tries to parse a span of characters into a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="style">
    /// A bitwise combination of enumeration values that indicates the permitted format of <paramref name="s"/>.
    /// </param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the result of successfully parsing <paramref name="s"/>,
    /// or an undefined value on failure.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="s"/> was parsed successfully; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible zero-allocation parsing method, supporting all number styles
    /// and culture-specific formatting while operating directly on character spans.
    /// </para>
    /// <para>
    /// The method validates that the parsed value is within the valid range for <see cref="UInt56"/>
    /// (0 to <see cref="MaxValue"/>) and returns <see langword="false"/> if the value is too large.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; span = "  1,234,567  ".AsSpan();
    /// var style = NumberStyles.Integer | NumberStyles.AllowThousands | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
    ///
    /// if (UInt56.TryParse(span, style, CultureInfo.InvariantCulture, out UInt56 result))
    /// {
    ///     Console.WriteLine(result); // Output: 1234567
    /// }
    /// </code>
    /// </example>
    [System.Diagnostics.Contracts.Pure]
    public static System.Boolean TryParse(
        System.ReadOnlySpan<System.Char> s,
        System.Globalization.NumberStyles style,
        System.IFormatProvider provider,
        out UInt56 result)
    {
        result = default;

        // Handle empty or whitespace-only spans
        if (s.IsEmpty || System.MemoryExtensions.IsWhiteSpace(s))
        {
            return false;
        }

        // Delegate to UInt64's span-based TryParse for efficient parsing
        if (!System.UInt64.TryParse(s, style, provider, out System.UInt64 ulongValue))
        {
            return false;
        }

        // Validate that the parsed value is within UInt56 range
        if (ulongValue > MaxValue)
        {
            return false;
        }

        // Use trusted constructor since we've validated the range
        result = new UInt56(ulongValue, true);
        return true;
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="UInt56"/> using the specified format provider.
    /// </summary>
    /// <param name="s">
    /// The string representation of a number to parse.  The string is interpreted using the
    /// <see cref="System.Globalization.NumberStyles. Integer"/> style.
    /// </param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// If <paramref name="provider"/> is <see langword="null"/>, the thread current culture is used.
    /// </param>
    /// <param name="result">
    /// When this method returns <see langword="true"/>, contains the <see cref="UInt56"/> value
    /// equivalent to the number contained in <paramref name="s"/>. When this method returns
    /// <see langword="false"/>, contains the default value for <see cref="UInt56"/>.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="s"/> was converted successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is part of the <see cref="System.Numerics. INumberBase{TSelf}"/> interface
    /// and provides a standardized way to parse numeric types in generic contexts.
    /// It uses <see cref="System.Globalization.NumberStyles.Integer"/> as the default parsing style.
    /// </para>
    /// <para>
    /// The method accepts decimal integer representations and validates that the parsed value
    /// is within the valid range for <see cref="UInt56"/> (0 to <see cref="MaxValue"/>).
    /// Leading and trailing whitespaces are automatically handled according to the integer number style.
    /// </para>
    /// <para>
    /// For more control over parsing behavior, use the overloaded methods that accept
    /// <see cref="System.Globalization.NumberStyles"/> parameters.
    /// </para>
    /// <para>
    /// This method will never throw an exception. If parsing fails for any reason
    /// (invalid format, null input, out of range), it returns <see langword="false"/>
    /// and sets <paramref name="result"/> to the default <see cref="UInt56"/> value (zero).
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Basic usage:</para>
    /// <code>
    /// // Simple decimal parsing
    /// if (UInt56.TryParse("123456789", CultureInfo.InvariantCulture, out UInt56 value))
    /// {
    ///     Console.WriteLine($"Parsed: {value}"); // Output:  Parsed: 123456789
    /// }
    ///
    /// // Handling invalid input gracefully
    /// if (UInt56.TryParse("invalid", CultureInfo.InvariantCulture, out UInt56 invalid))
    /// {
    ///     Console.WriteLine("This won't execute");
    /// }
    /// else
    /// {
    ///     Console. WriteLine("Parsing failed as expected");
    /// }
    ///
    /// // Culture-specific parsing
    /// var germanCulture = CultureInfo. GetCultureInfo("de-DE");
    /// if (UInt56.TryParse("1. 234.567", germanCulture, out UInt56 germanValue))
    /// {
    ///     Console.WriteLine($"German format: {germanValue}"); // May work depending on culture settings
    /// }
    /// </code>
    /// <para>Generic usage in algorithms:</para>
    /// <code>
    /// public static bool ParseNumber&lt;T&gt;(string input, IFormatProvider provider, out T result)
    ///     where T : INumberBase&lt;T&gt;
    /// {
    ///     return T.TryParse(input, provider, out result);
    /// }
    ///
    /// // Usage
    /// bool success = ParseNumber("42", CultureInfo.InvariantCulture, out UInt56 number);
    /// </code>
    /// </example>
    /// <seealso cref="Parse(System.String, System.IFormatProvider)"/>
    /// <seealso cref="TryParse(System.String, System.Globalization.NumberStyles, System.IFormatProvider, out UInt56)"/>
    /// <seealso cref="TryParse(System.ReadOnlySpan{System.Char}, System.IFormatProvider, out UInt56)"/>
    [System.Diagnostics.Contracts.Pure]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean TryParse(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] System.String s,
        System.IFormatProvider provider,
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out UInt56 result) =>
        // Use the existing, more comprehensive TryParse method with Integer number style
        // This ensures consistent behavior across all parsing methods
        TryParse(s, System.Globalization.NumberStyles.Integer, provider, out result);

    #endregion ISpanParsable<UInt56> Implementation

    #region INumber<UInt56> Implementation

    #region INumberBase<UInt56> Methods

    /// <summary>
    /// Computes the absolute of a value.
    /// </summary>
    /// <param name="value">The value for which to get its absolute.</param>
    /// <returns>The absolute of <paramref name="value"/>.</returns>
    /// <remarks>
    /// For unsigned types like <see cref="UInt56"/>, the absolute value is always the value itself.
    /// </remarks>
    static UInt56 System.Numerics.INumberBase<UInt56>.Abs(UInt56 value) => value;

    /// <summary>
    /// Determines if a value represents an even integral number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is an even integer; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsEvenInteger(UInt56 value) => (value._value & 1) == 0;

    /// <summary>
    /// Determines if a value represents an odd integral number.
    /// </summary>
    /// <param name="value">The value to be checked. </param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is an odd integer; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsOddInteger(UInt56 value) => (value._value & 1) != 0;

    /// <summary>
    /// Determines if a value is zero.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is zero; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsZero(UInt56 value) => value._value == 0UL;

    /// <summary>
    /// Determines if a value represents a value greater than or equal to zero.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is positive; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For unsigned types like <see cref="UInt56"/>, all values are considered positive or zero.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsPositive(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents zero or a positive real number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is zero or positive; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, positive infinity is not representable.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsPositiveInfinity(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a negative real number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> represents negative infinity; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, negative infinity is not representable.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsNegativeInfinity(UInt56 value) => false;

    /// <summary>
    /// Determines if a value is negative.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is negative; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For unsigned types like <see cref="UInt56"/>, no values are negative.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsNegative(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a finite value.
    /// </summary>
    /// <param name="value">The value to be checked. </param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is finite; otherwise, <see langword="false"/>. </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, all values are finite.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsFinite(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents an infinite value.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is infinite; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, infinity is not representable.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsInfinity(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents an integral number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is an integer; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, all values are integers.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsInteger(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents <c>NaN</c>.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is <c>NaN</c>; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, NaN is not representable.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsNaN(UInt56 value) => false;

    /// <summary>
    /// Determines if a value is normal.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is normal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, all non-zero values are considered normal.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsNormal(UInt56 value) => value._value != 0UL;

    /// <summary>
    /// Determines if a value is subnormal.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is subnormal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, subnormal values do not exist.
    /// </remarks>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsSubnormal(UInt56 value) => false;

    /// <summary>
    /// Compares two values to compute which is greater.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>.</param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>The greater of <paramref name="x"/> or <paramref name="y"/>.</returns>
    static UInt56 System.Numerics.INumberBase<UInt56>.MaxMagnitude(UInt56 x, UInt56 y) => x > y ? x : y;

    /// <summary>
    /// Compares two values to compute which has the greater magnitude and returning the other value if an input is <c>NaN</c>.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>. </param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>The value with the greater magnitude; or whichever is not <c>NaN</c> if there is only one.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, this behaves identically to <see cref="System. Numerics.INumberBase{TSelf}.MaxMagnitude"/>.
    /// </remarks>
    static UInt56 System.Numerics.INumberBase<UInt56>.MaxMagnitudeNumber(UInt56 x, UInt56 y) => x > y ? x : y;

    /// <summary>
    /// Compares two values to compute which is lesser.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>.</param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>The lesser of <paramref name="x"/> or <paramref name="y"/>. </returns>
    static UInt56 System.Numerics.INumberBase<UInt56>.MinMagnitude(UInt56 x, UInt56 y) => x < y ? x : y;

    /// <summary>
    /// Compares two values to compute which has the lesser magnitude and returning the other value if an input is <c>NaN</c>.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>. </param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>The value with the lesser magnitude; or whichever is not <c>NaN</c> if there is only one.</returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, this behaves identically to <see cref="System.Numerics.INumberBase{TSelf}.MinMagnitude"/>.
    /// </remarks>
    static UInt56 System.Numerics.INumberBase<UInt56>.MinMagnitudeNumber(UInt56 x, UInt56 y) => x < y ? x : y;

    /// <summary>
    /// Determines if a value is in its canonical representation.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is in its canonical representation; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// For integer types like <see cref="UInt56"/>, all values are always in their canonical representation.
    /// The canonical representation is the standard, unique way to represent a number.
    /// </para>
    /// <para>
    /// This concept is primarily relevant for floating-point types where multiple bit patterns
    /// can represent the same mathematical value (e.g., different NaN encodings). For integers,
    /// each distinct mathematical value has exactly one bit pattern representation.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(12345);
    /// bool isCanonical = UInt56.IsCanonical(value); // Always true for integers
    /// Console.WriteLine(isCanonical); // Output: True
    /// </code>
    /// </example>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsCanonical(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents a complex number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a complex number; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// For real number types like <see cref="UInt56"/>, values are not complex numbers.
    /// Complex numbers have both real and imaginary components, while <see cref="UInt56"/>
    /// represents only real, non-negative integer values.
    /// </para>
    /// <para>
    /// This method always returns <see langword="false"/> for integer types, as integers
    /// are a subset of real numbers, not complex numbers.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(42);
    /// bool isComplex = UInt56.IsComplexNumber(value); // Always false for integers
    /// Console.WriteLine(isComplex); // Output: False
    /// </code>
    /// </example>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsComplexNumber(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a pure imaginary number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is an imaginary number; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// For real number types like <see cref="UInt56"/>, values cannot be pure imaginary numbers.
    /// Imaginary numbers are complex numbers with zero real part and non-zero imaginary part,
    /// while <see cref="UInt56"/> represents only real, non-negative integer values.
    /// </para>
    /// <para>
    /// This method always returns <see langword="false"/> for integer types, as integers
    /// have no imaginary component.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(100);
    /// bool isImaginary = UInt56.IsImaginaryNumber(value); // Always false for integers
    /// Console. WriteLine(isImaginary); // Output: False
    /// </code>
    /// </example>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsImaginaryNumber(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a real number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a real number; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    /// For integer types like <see cref="UInt56"/>, all values represent real numbers.
    /// Real numbers include all integers, rational numbers, and irrational numbers,
    /// but exclude complex numbers with non-zero imaginary parts.
    /// </para>
    /// <para>
    /// Since <see cref="UInt56"/> represents non-negative integers, and all integers
    /// are real numbers, this method always returns <see langword="true"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(999999);
    /// bool isReal = UInt56.IsRealNumber(value); // Always true for integers
    /// Console.WriteLine(isReal); // Output: True
    ///
    /// UInt56 zero = UInt56.Zero;
    /// bool isRealZero = UInt56.IsRealNumber(zero); // Zero is also real
    /// Console. WriteLine(isRealZero); // Output: True
    /// </code>
    /// </example>
    static System.Boolean System.Numerics.INumberBase<UInt56>.IsRealNumber(UInt56 value) => true;

    #endregion INumberBase<UInt56> Methods

    #region Generic Conversion Methods

    /// <summary>
    /// Tries to convert a value to a <see cref="UInt56"/> instance, throwing an overflow exception for any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to a <see cref="UInt56"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryConvertFromChecked<TOther>(TOther value, out UInt56 result)
        => TryConvertFrom(value, out result);

    /// <summary>
    /// Tries to convert a value to a <see cref="UInt56"/> instance, saturating any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type of the value to convert. </typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to a <see cref="UInt56"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryConvertFromSaturating<TOther>(TOther value, out UInt56 result)
        => TryConvertFrom(value, out result);

    /// <summary>
    /// Tries to convert a value to a <see cref="UInt56"/> instance, truncating any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert. </param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to a <see cref="UInt56"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryConvertFromTruncating<TOther>(TOther value, out UInt56 result)
        => TryConvertFrom(value, out result);

    /// <summary>
    /// Tries to convert a <see cref="UInt56"/> instance to another type, throwing an overflow exception for any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type to convert the <see cref="UInt56"/> to.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to <typeparamref name="TOther"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryConvertToChecked<TOther>(UInt56 value, out TOther result)
        where TOther : default
        => TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// Tries to convert a <see cref="UInt56"/> instance to another type, saturating any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type to convert the <see cref="UInt56"/> to.</typeparam>
    /// <param name="value">The value to convert. </param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to <typeparamref name="TOther"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryConvertToSaturating<TOther>(UInt56 value, out TOther result)
        where TOther : default
        => TryConvertTo<TOther>(value, out result);

    /// <summary>
    /// Tries to convert a <see cref="UInt56"/> instance to another type, truncating any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type to convert the <see cref="UInt56"/> to.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to <typeparamref name="TOther"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryConvertToTruncating<TOther>(UInt56 value, out TOther result)
        where TOther : default
        => TryConvertTo<TOther>(value, out result);

    #endregion Generic Conversion Methods

    #region Parsing Interface Methods

    /// <summary>
    /// Parses a span of characters into a value.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <param name="style">A bitwise combination of number styles that can be present in <paramref name="s"/>.</param>
    /// <param name="provider">An object that provides culture-specific formatting information about <paramref name="s"/>.</param>
    /// <param name="result">On return, contains the result of successfully parsing <paramref name="s"/> or an undefined value on failure.</param>
    /// <returns><see langword="true"/> if <paramref name="s"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryParse(
        System.ReadOnlySpan<System.Char> s,
        System.Globalization.NumberStyles style,
        System.IFormatProvider provider,
        out UInt56 result)
        => TryParse(s, style, provider, out result);

    /// <summary>
    /// Parses an array of UTF-8 characters into a value.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 characters to parse.</param>
    /// <param name="style">A bitwise combination of number styles that can be present in <paramref name="utf8Text"/>.</param>
    /// <param name="provider">An object that provides culture-specific formatting information about <paramref name="utf8Text"/>.</param>
    /// <param name="result">On return, contains the result of successfully parsing <paramref name="utf8Text"/> or an undefined value on failure.</param>
    /// <returns><see langword="true"/> if <paramref name="utf8Text"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    static System.Boolean System.Numerics.INumberBase<UInt56>.TryParse(
        System.ReadOnlySpan<System.Byte> utf8Text,
        System.Globalization.NumberStyles style,
        System.IFormatProvider provider,
        out UInt56 result)
    {
        // Convert UTF-8 bytes to string and delegate to existing parsing logic
        var stringValue = System.Text.Encoding.UTF8.GetString(utf8Text);
        return TryParse(stringValue, style, provider, out result);
    }
    #endregion Parsing Interface Methods

    #endregion INumber<UInt56> Implementation

    #region Helper Methods

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

    /// <summary>
    /// Throws a <see cref="System.FormatException"/> with a descriptive message.
    /// </summary>
    /// <param name="input">The input that failed to parse.</param>
    /// <exception cref="System.FormatException">Always thrown.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void ThrowFormatException(System.String input)
    {
        throw new System.FormatException(
            $"Input string '{input}' was not in a correct format or was out of range for UInt56.");
    }

    /// <summary>
    /// Helper method to convert from other numeric types to UInt56.
    /// </summary>
    /// <typeparam name="TOther">The type to convert from.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted result.</param>
    /// <returns><see langword="true"/> if conversion was successful; otherwise, <see langword="false"/>.</returns>
    private static System.Boolean TryConvertFrom<TOther>(TOther value, out UInt56 result)
        where TOther : System.Numerics.INumberBase<TOther>
    {
        result = default;

        if (typeof(TOther) == typeof(System.Byte))
        {
            result = new UInt56((System.Byte)(System.Object)value!, true);
            return true;
        }
        else if (typeof(TOther) == typeof(System.UInt16))
        {
            result = new UInt56((System.UInt16)(System.Object)value!, true);
            return true;
        }
        else if (typeof(TOther) == typeof(System.UInt32))
        {
            result = new UInt56((System.UInt32)(System.Object)value!, true);
            return true;
        }
        else if (typeof(TOther) == typeof(System.UInt64))
        {
            var ulongValue = (System.UInt64)(System.Object)value!;
            if (ulongValue <= MaxValue)
            {
                result = new UInt56(ulongValue, true);
                return true;
            }
        }
        else if (typeof(TOther) == typeof(System.Int32))
        {
            var intValue = (System.Int32)(System.Object)value!;
            if (intValue >= 0)
            {
                result = new UInt56((System.UInt64)intValue, true);
                return true;
            }
        }
        else if (typeof(TOther) == typeof(System.Int64))
        {
            var longValue = (System.Int64)(System.Object)value!;
            if (longValue >= 0 && (System.UInt64)longValue <= MaxValue)
            {
                result = new UInt56((System.UInt64)longValue, true);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Helper method to convert from UInt56 to other numeric types.
    /// </summary>
    /// <typeparam name="TOther">The type to convert to.</typeparam>
    /// <param name="value">The UInt56 value to convert.</param>
    /// <param name="result">The converted result.</param>
    /// <returns><see langword="true"/> if conversion was successful; otherwise, <see langword="false"/>.</returns>
    private static System.Boolean TryConvertTo<TOther>(UInt56 value, out TOther result)
        where TOther : System.Numerics.INumberBase<TOther>
    {
        if (typeof(TOther) == typeof(System.Byte))
        {
            if (value._value <= System.Byte.MaxValue)
            {
                result = (TOther)(System.Object)(System.Byte)value._value;
                return true;
            }
        }
        else if (typeof(TOther) == typeof(System.UInt16))
        {
            if (value._value <= System.UInt16.MaxValue)
            {
                result = (TOther)(System.Object)(System.UInt16)value._value;
                return true;
            }
        }
        else if (typeof(TOther) == typeof(System.UInt32))
        {
            if (value._value <= System.UInt32.MaxValue)
            {
                result = (TOther)(System.Object)(System.UInt32)value._value;
                return true;
            }
        }
        else if (typeof(TOther) == typeof(System.UInt64))
        {
            result = (TOther)(System.Object)value._value;
            return true;
        }
        else if (typeof(TOther) == typeof(System.Int32))
        {
            if (value._value <= System.Int32.MaxValue)
            {
                result = (TOther)(System.Object)(System.Int32)value._value;
                return true;
            }
        }
        else if (typeof(TOther) == typeof(System.Int64))
        {
            if (value._value <= System.Int64.MaxValue)
            {
                result = (TOther)(System.Object)(System.Int64)value._value;
                return true;
            }
        }

        result = default!;
        return false;
    }

    #endregion Helper Methods
}
