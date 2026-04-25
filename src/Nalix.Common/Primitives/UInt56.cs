// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Nalix.Common.Primitives;

/// <summary>
/// Represents an unsigned 56-bit integer stored in exactly 7 bytes.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the standard approach of using a <see cref="ulong"/> field (8 bytes),
/// this implementation stores the value in a compact 3-field layout of exactly 7 bytes:
/// <list type="bullet">
///   <item><description><c>_lo</c>  — lower 32 bits  (4 bytes)</description></item>
///   <item><description><c>_mid</c> — middle 16 bits (2 bytes)</description></item>
///   <item><description><c>_hi</c>  — upper 8 bits   (1 byte)</description></item>
/// </list>
/// This layout is ideal for large arrays, memory-mapped structures, and binary protocols
/// where every byte matters, at the cost of a small pack/unpack overhead on arithmetic.
/// </para>
/// <para>
/// The value is stored across the three fields such that the full 56-bit value can be
/// reconstructed as: <c>_lo | ((ulong)_mid &lt;&lt; 32) | ((ulong)_hi &lt;&lt; 48)</c>.
/// All arithmetic operations unpack to <see cref="ulong"/>, compute, validate,
/// then repack — bitwise operations (&amp;, |, ^, ~) operate directly on the fields
/// without any unpack step for maximum performance.
/// </para>
/// <para>
/// <b>Alignment warning:</b> Because the struct is 7 bytes, elements in an array will
/// not be naturally aligned on 8-byte boundaries after the first element.
/// On x64 this causes a minor slowdown (~10–20%) on unaligned reads; on ARM ensure
/// the runtime supports unaligned access (all modern .NET targets do).
/// </para>
/// </remarks>
[ComVisible(true)]
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(
    LayoutKind.Sequential, Pack = 1)]
public readonly struct UInt56 :
    IComparable,
    IFormattable,

    IEquatable<UInt56>,
    IComparable<UInt56>,
    IUtf8SpanFormattable,

    INumber<UInt56>,
    IMinMaxValue<UInt56>,

    IBitwiseOperators<UInt56, UInt56, UInt56>,

    IShiftOperators<UInt56, int, UInt56>

{
    #region Private Fields — 7-byte layout

    /// <summary>
    /// bits  0–31
    /// </summary>
    private readonly uint _lo;

    /// <summary>
    /// bits 32–47
    /// </summary>
    private readonly ushort _mid;

    /// <summary>
    /// bits 48–55
    /// </summary>
    private readonly byte _hi;

    #endregion Private Fields — 7-byte layout

    #region Constants and static fields

    /// <summary>
    /// Represents the largest possible value of <see cref="UInt56"/>.
    /// </summary>
    public const ulong MaxValue = (1UL << 56) - 1; // 0x00FFFFFFFFFFFFFF

    /// <summary>
    /// Represents the smallest possible value of <see cref="UInt56"/>.
    /// </summary>
    public const ulong MinValue = 0UL;

    /// <summary>
    /// Represents the <see cref="UInt56"/> value 0.
    /// </summary>
    public static readonly UInt56 Zero = new(0u, 0, 0);

    /// <summary>
    /// Represents the largest possible <see cref="UInt56"/> value.
    /// </summary>
    public static readonly UInt56 Max = new(0xFFFFFFFFu, 0xFFFF, 0xFF);

    /// <summary>
    /// Gets the additive identity of the current type.
    /// </summary>
    public static UInt56 AdditiveIdentity => Zero;

    /// <summary>
    /// Gets the multiplicative identity of the current type.
    /// </summary>
    public static UInt56 MultiplicativeIdentity => new(1u, 0, 0);

    /// <summary>
    /// Gets the value 1 for the type.
    /// </summary>
    static UInt56 INumberBase<UInt56>.One => new(1u, 0, 0);

    /// <summary>
    /// Gets the radix, or base, for the type.
    /// </summary>
    static int INumberBase<UInt56>.Radix => 2;

    /// <summary>
    /// Gets the value 0 for the type.
    /// </summary>
    static UInt56 INumberBase<UInt56>.Zero => Zero;

    #endregion Constants and static fields
    
    #region Properties
    
    /// <summary>
    /// Gets a value indicating whether the current value is zero.
    /// </summary>
    /// <remarks>
    /// Checks all three fields directly without unpacking to <see cref="ulong"/>,
    /// making this faster than <c>ToUInt64() == 0</c>.
    /// </remarks>
    public bool IsZero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _lo == 0u && _mid == 0 && _hi == 0;
    }
    
    #endregion Properties

    #region IMinMaxValue<T> Implementation

    /// <summary>
    /// Gets the maximum value of the current type.
    /// </summary>
    static UInt56 IMinMaxValue<UInt56>.MaxValue => Max;

    /// <summary>
    /// Gets the minimum value of the current type.
    /// </summary>
    static UInt56 IMinMaxValue<UInt56>.MinValue => Zero;

    #endregion IMinMaxValue<T> Implementation

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct directly
    /// from its three component fields without any validation or bit manipulation.
    /// </summary>
    /// <param name="lo">The lower 32 bits (bits 0–31) of the value.</param>
    /// <param name="mid">The middle 16 bits (bits 32–47) of the value.</param>
    /// <param name="hi">The upper 8 bits (bits 48–55) of the value.</param>
    /// <remarks>
    /// <para>
    /// This is the fastest possible constructor — it assigns the three fields directly
    /// with no arithmetic, no validation, and no intermediate <see cref="ulong"/>
    /// allocation. The JIT can keep all three fields in registers.
    /// </para>
    /// <para>
    /// This constructor is <c>private</c> because callers must guarantee that the
    /// combination of <paramref name="lo"/>, <paramref name="mid"/>, and <paramref name="hi"/>
    /// represents a valid 56-bit value. In practice, any valid decomposition of a
    /// <see cref="ulong"/> value that is within [0, <see cref="MaxValue"/>] is safe.
    /// </para>
    /// </remarks>
    /*
     * [The Trusted Constructor]
     * This is the fastest way to create a UInt56. It bypasses all validation 
     * and assumes the caller has already decomposed the value correctly.
     * Ideal for hot-path arithmetic repacking.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UInt56(uint lo, ushort mid, byte hi)
    {
        _lo = lo;
        _mid = mid;
        _hi = hi;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct
    /// from a <see cref="ulong"/> value with optional validation.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    /// <param name="trusted">
    /// If <c>true</c>, assumes the value is already validated and within range.
    /// If <c>false</c>, performs validation and may throw on overflow.
    /// </param>
    /// <exception cref="OverflowException">
    /// <paramref name="trusted"/> is <c>false</c> and <paramref name="value"/>
    /// is greater than <see cref="MaxValue"/>.
    /// </exception>
    /// <remarks>
    /// The pack step decomposes the 64-bit value into the three storage fields
    /// in a single pass: <c>_lo</c> takes bits 0–31, <c>_mid</c> takes bits 32–47,
    /// and <c>_hi</c> takes bits 48–55. Bits 56–63 are discarded (they must be zero
    /// for a valid UInt56 value).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UInt56(ulong value, bool trusted)
    {
        if (!trusted && value > MaxValue)
        {
            throw new OverflowException(
                $"Value {value} is outside the range of a UInt56 (0..{MaxValue}).");
        }

        // Pack UInt64 -> 3 fields in a single pass
        _lo = (uint)value;
        _mid = (ushort)(value >> 32);
        _hi = (byte)(value >> 48);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct
    /// to a specified 64-bit unsigned integer value.
    /// </summary>
    /// <param name="value">
    /// The value to assign to the new instance. The value must be between
    /// <see cref="MinValue"/> and <see cref="MaxValue"/>, inclusive.
    /// </param>
    /// <exception cref="OverflowException">
    /// <paramref name="value"/> is less than <see cref="MinValue"/> or
    /// greater than <see cref="MaxValue"/>.
    /// </exception>
    public UInt56(ulong value) : this(value, false) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UInt56"/> struct from its component parts.
    /// </summary>
    /// <param name="a">The type component (upper 8 bits).</param>
    /// <param name="b">The machine identifier component (next 16 bits).</param>
    /// <param name="c">The value component (lower 32 bits).</param>
    public UInt56(byte a, ushort b, uint c)
        : this(((ulong)a << 48) | ((ulong)b << 32) | c, false) { }

    #endregion Constructors

    #region Core pack/unpack — hot path

    /// <summary>
    /// Converts this instance to a 64-bit unsigned integer.
    /// </summary>
    /// <returns>The 64-bit unsigned integer equivalent of this value.</returns>
    /// <remarks>
    /// <para>
    /// This method reconstructs the full <see cref="ulong"/> value from the three
    /// stored fields using two bitwise OR operations and two left-shift operations:
    /// <c>_lo | ((ulong)_mid &lt;&lt; 32) | ((ulong)_hi &lt;&lt; 48)</c>.
    /// </para>
    /// <para>
    /// This is the single unpack hot-path used internally by every arithmetic operator
    /// (+, -, *, /, %, shift, increment, decrement). The JIT inlines and optimizes this
    /// to approximately 4 native instructions with zero allocation.
    /// </para>
    /// <para>
    /// Bitwise operators (&amp;, |, ^, ~) do <b>not</b> call this method — they operate
    /// directly on the three fields for maximum performance.
    /// </para>
    /// </remarks>
    /*
     * [The Unpack Hot-Path]
     * This is used by every arithmetic operator. It reconstructs the 64-bit 
     * value from the 3 fields using shifts and ORs.
     * Reconstructed as: lo | (mid << 32) | (hi << 48)
     */
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ToUInt64() => _lo | ((ulong)_mid << 32) | ((ulong)_hi << 48);

    /// <summary>
    /// Creates a <see cref="UInt56"/> instance directly from a raw <see cref="ulong"/>
    /// value using the trusted (non-validating) repack path.
    /// </summary>
    /// <param name="raw">
    /// The raw 64-bit value to pack. The caller must guarantee that
    /// <c>raw &lt;= <see cref="MaxValue"/></c>; no range check is performed.
    /// </param>
    /// <returns>A new <see cref="UInt56"/> whose fields represent the given raw value.</returns>
    /// <remarks>
    /// <para>
    /// This is the single repack hot-path used internally after every arithmetic operation.
    /// It decomposes <paramref name="raw"/> into the three storage fields in one expression:
    /// <c>_lo = (uint)raw</c>, <c>_mid = (ushort)(raw >> 32)</c>, <c>_hi = (byte)(raw >> 48)</c>.
    /// </para>
    /// <para>
    /// Because this method is <c>private</c> and every call site has already validated the
    /// range (via <see cref="CheckOverflow"/> or explicit bounds logic), the three-field
    /// constructor is called in trusted mode with no additional checks.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt56 FromRaw(ulong raw) => new((uint)raw, (ushort)(raw >> 32), (byte)(raw >> 48));

    #endregion Core pack/unpack — hot path

    #region Serialization helpers

    /// <summary>
    /// Writes this <see cref="UInt56"/> as exactly 7 bytes in little-endian byte order
    /// into the provided destination span.
    /// </summary>
    /// <param name="destination">
    /// The destination span. Must have a length of at least 7 bytes.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> has fewer than 7 bytes.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method produces zero allocation and no intermediate string. It writes
    /// the three fields using <see cref="BinaryPrimitives"/> which
    /// the JIT can vectorize on platforms that support SIMD.
    /// </para>
    /// <para>
    /// Byte layout in <paramref name="destination"/> (little-endian):
    /// <list type="table">
    ///   <listheader><term>Offset</term><description>Content</description></listheader>
    ///   <item><term>0–3</term><description><c>_lo</c> (lower 32 bits)</description></item>
    ///   <item><term>4–5</term><description><c>_mid</c> (middle 16 bits)</description></item>
    ///   <item><term>6</term><description><c>_hi</c> (upper 8 bits)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(0x001122334455FFUL);
    /// Span&lt;byte&gt; buffer = stackalloc byte[7];
    /// value.WriteBytesLittleEndian(buffer);
    /// // buffer = [FF, 55, 44, 33, 22, 11, 00]
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytesLittleEndian(Span<byte> destination)
    {
        if (destination.Length < 7)
        {
            throw new ArgumentException("Destination must be at least 7 bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination, _lo);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], _mid);
        destination[6] = _hi;
    }

    /// <summary>
    /// Writes this <see cref="UInt56"/> as exactly 7 bytes in big-endian byte order
    /// into the provided destination span, suitable for network byte order (RFC 1700).
    /// </summary>
    /// <param name="destination">
    /// The destination span. Must have a length of at least 7 bytes.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> has fewer than 7 bytes.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method produces zero allocation and no intermediate string. It writes
    /// the three fields using <see cref="BinaryPrimitives"/> which
    /// the JIT can vectorize on platforms that support SIMD.
    /// </para>
    /// <para>
    /// Byte layout in <paramref name="destination"/> (big-endian):
    /// <list type="table">
    ///   <listheader><term>Offset</term><description>Content</description></listheader>
    ///   <item><term>0</term><description><c>_hi</c> (upper 8 bits)</description></item>
    ///   <item><term>1–2</term><description><c>_mid</c> (middle 16 bits)</description></item>
    ///   <item><term>3–6</term><description><c>_lo</c> (lower 32 bits)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(0x001122334455FFUL);
    /// Span&lt;byte&gt; buffer = stackalloc byte[7];
    /// value.WriteBytesBigEndian(buffer);
    /// // buffer = [00, 11, 22, 33, 44, 55, FF]
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytesBigEndian(Span<byte> destination)
    {
        if (destination.Length < 7)
        {
            throw new ArgumentException("Destination must be at least 7 bytes.", nameof(destination));
        }

        destination[0] = _hi;
        BinaryPrimitives.WriteUInt16BigEndian(destination[1..], _mid);
        BinaryPrimitives.WriteUInt32BigEndian(destination[3..], _lo);
    }

    /// <summary>
    /// Reads a <see cref="UInt56"/> from exactly 7 bytes in little-endian byte order.
    /// </summary>
    /// <param name="source">
    /// The source span. Must have a length of at least 7 bytes.
    /// </param>
    /// <returns>The <see cref="UInt56"/> value decoded from the source bytes.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> has fewer than 7 bytes.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is the zero-allocation counterpart to <see cref="WriteBytesLittleEndian"/>.
    /// It reads the three fields directly from the span without creating any intermediate
    /// objects or strings, making it ideal for high-throughput binary protocol parsing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;byte&gt; buffer = new byte[] { 0xFF, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 };
    /// UInt56 value = UInt56.ReadBytesLittleEndian(buffer);
    /// Console.WriteLine($"0x{value:X}"); // Output: 0x1122334455FF
    /// </code>
    /// </example>
    /*
     * [Binary Serialization: Little Endian]
     * Writes/Reads exactly 7 bytes.
     * [lo_0, lo_1, lo_2, lo_3, mid_0, mid_1, hi_0]
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt56 ReadBytesLittleEndian(ReadOnlySpan<byte> source)
    {
        if (source.Length < 7)
        {
            throw new ArgumentException("Source must be at least 7 bytes.", nameof(source));
        }

        uint lo = BinaryPrimitives.ReadUInt32LittleEndian(source);
        ushort mid = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        byte hi = source[6];
        return new UInt56(lo, mid, hi);
    }

    /// <summary>
    /// Reads a <see cref="UInt56"/> from exactly 7 bytes in big-endian byte order
    /// (network byte order, RFC 1700).
    /// </summary>
    /// <param name="source">
    /// The source span. Must have a length of at least 7 bytes.
    /// </param>
    /// <returns>The <see cref="UInt56"/> value decoded from the source bytes.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> has fewer than 7 bytes.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is the zero-allocation counterpart to <see cref="WriteBytesBigEndian"/>.
    /// It reads the three fields directly from the span without creating any intermediate
    /// objects or strings, making it ideal for network packet parsing where big-endian
    /// byte order is the standard.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;byte&gt; buffer = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0xFF };
    /// UInt56 value = UInt56.ReadBytesBigEndian(buffer);
    /// Console.WriteLine($"0x{value:X}"); // Output: 0x1122334455FF
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt56 ReadBytesBigEndian(ReadOnlySpan<byte> source)
    {
        if (source.Length < 7)
        {
            throw new ArgumentException("Source must be at least 7 bytes.", nameof(source));
        }

        byte hi = source[0];
        ushort mid = BinaryPrimitives.ReadUInt16BigEndian(source[1..]);
        uint lo = BinaryPrimitives.ReadUInt32BigEndian(source[3..]);
        return new UInt56(lo, mid, hi);
    }

    #endregion Serialization helpers

    #region Conversions

    /// <summary>
    /// Defines an implicit conversion of a <see cref="byte"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(byte value) => new((uint)value, 0, 0);

    /// <summary>
    /// Defines an implicit conversion of a <see cref="ushort"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(ushort value) => new(value, 0, 0);

    /// <summary>
    /// Defines an implicit conversion of a <see cref="uint"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    public static implicit operator UInt56(uint value) => new(value, 0, 0);

    /// <summary>
    /// Defines an implicit conversion of a non-negative <see cref="int"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    /// <exception cref="OverflowException">
    /// <paramref name="value"/> is negative.
    /// </exception>
    public static implicit operator UInt56(int value)
        => value < 0 ? throw new OverflowException("Cannot convert negative int to UInt56.") : new UInt56((uint)value, 0, 0);

    /// <summary>
    /// Defines an explicit conversion of a <see cref="UInt56"/> to a <see cref="ulong"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="ulong"/> that is equivalent to <paramref name="value"/>.</returns>
    public static explicit operator ulong(UInt56 value) => value.ToUInt64();

    /// <summary>
    /// Defines an explicit conversion of a <see cref="ulong"/> to a <see cref="UInt56"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A <see cref="UInt56"/> that represents the converted value.</returns>
    /// <exception cref="OverflowException">
    /// <paramref name="value"/> is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static explicit operator UInt56(ulong value) => new(value, false);

    #endregion Conversions

    #region Equality and comparison

    /// <inheritdoc />
    /// <remarks>
    /// Compares all three storage fields directly (<c>_lo</c>, <c>_mid</c>, <c>_hi</c>)
    /// without unpacking to <see cref="ulong"/>, making this the fastest possible
    /// equality check for the 7-byte layout.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(UInt56 other) => _lo == other._lo && _mid == other._mid && _hi == other._hi;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is UInt56 other && this.Equals(other);

    /// <inheritdoc />
    /// <remarks>
    /// Optimized bit-mixing for 56-bit values. This is significantly faster than
    /// <c>HashCode.Combine</c> while maintaining excellent distribution
    /// for identity-like values (e.g., Snowflake IDs).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        // XOR the lower 32 bits with the upper 24 bits (re-aligned) to ensure 
        // high entropy from all parts of the 56-bit value with minimal CPU cycles.
        return (int)_lo ^ (_mid | (_hi << 16));
    }

    /// <inheritdoc />
    public int CompareTo(UInt56 other) => this.ToUInt64().CompareTo(other.ToUInt64());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IComparable.CompareTo(object? obj)
    {
        if (obj is not null)
        {
            if (obj is UInt56 other)
            {
                return this.CompareTo(other);
            }

            throw new ArgumentException("Object must be of type UInt56.", nameof(obj));
        }

        return 1;
    }

    /// <summary>
    /// Indicates whether two <see cref="UInt56"/> values are equal.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    public static bool operator ==(UInt56 left, UInt56 right) => left.Equals(right);

    /// <summary>
    /// Indicates whether two <see cref="UInt56"/> values are not equal.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    public static bool operator !=(UInt56 left, UInt56 right) => !left.Equals(right);

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is less than another specified <see cref="UInt56"/>.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    public static bool operator <(UInt56 left, UInt56 right) => left.ToUInt64() < right.ToUInt64();

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is less than or equal to another specified <see cref="UInt56"/>.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    public static bool operator <=(UInt56 left, UInt56 right) => left.ToUInt64() <= right.ToUInt64();

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is greater than another specified <see cref="UInt56"/>.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    public static bool operator >(UInt56 left, UInt56 right) => left.ToUInt64() > right.ToUInt64();

    /// <summary>
    /// Indicates whether a specified <see cref="UInt56"/> is greater than or equal to another specified <see cref="UInt56"/>.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    public static bool operator >=(UInt56 left, UInt56 right) => left.ToUInt64() >= right.ToUInt64();

    #endregion Equality and comparison

    #region Parsing

    /// <summary>
    /// Converts the numeric value of this instance to its equivalent string representation.
    /// </summary>
    /// <returns>
    /// The string representation of the value of this instance, formatted using the current culture.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => this.ToUInt64().ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts the numeric value of this instance to its equivalent string representation
    /// using the specified format provider.
    /// </summary>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information.
    /// </param>
    /// <returns>
    /// The string representation of the value of this instance, as specified by <paramref name="provider"/>.
    /// </returns>
    public string ToString(IFormatProvider provider) => this.ToUInt64().ToString(provider);

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
    public string ToString(string format, IFormatProvider formatProvider) => this.ToUInt64().ToString(format, formatProvider);

    /// <summary>
    /// Converts the string representation of a number to its <see cref="UInt56"/> equivalent.
    /// </summary>
    /// <param name="s">A string that contains the number to convert.</param>
    /// <returns>A <see cref="UInt56"/> equivalent of the number contained in <paramref name="s"/>.</returns>
    /// <exception cref="FormatException">
    /// <paramref name="s"/> is not in a valid format or represents a value that is outside
    /// the range of the <see cref="UInt56"/> type.
    /// </exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt56 Parse(string s) => Parse(s, NumberStyles.Integer, CultureInfo.CurrentCulture);

    /// <summary>
    /// Converts the string representation of a number in a specified culture-specific format
    /// to its <see cref="UInt56"/> equivalent.
    /// </summary>
    /// <param name="s">A string that contains the number to convert.</param>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information about <paramref name="s"/>.
    /// </param>
    /// <returns>A <see cref="UInt56"/> equivalent of the number contained in <paramref name="s"/>.</returns>
    /// <exception cref="FormatException">
    /// <paramref name="s"/> is not in a valid format or represents a value that is outside
    /// the range of the <see cref="UInt56"/> type.
    /// </exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt56 Parse(string s, IFormatProvider? provider) => Parse(s, NumberStyles.Integer, provider);

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
    /// <exception cref="FormatException">
    /// <paramref name="s"/> is not in a valid format or represents a value that is outside
    /// the range of the <see cref="UInt56"/> type.
    /// </exception>
    [Pure]
    public static UInt56 Parse(string s, NumberStyles style, IFormatProvider? provider)
        => TryParse(s, style, provider, out UInt56 result)
            ? result : throw new FormatException("Input string was not in a correct format or was out of range for UInt56.");

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
    [Pure]
    public static bool TryParse(
        string s,
        [NotNullWhen(true)] out UInt56 result)
        => TryParse(s, NumberStyles.Integer, CultureInfo.CurrentCulture, out result);

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
    [Pure]
    public static bool TryParse(
        string? s,
        NumberStyles style,
        IFormatProvider? provider,
        [NotNullWhen(true)] out UInt56 result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        if (!ulong.TryParse(s, style, provider, out ulong u))
        {
            return false;
        }

        if (u > MaxValue)
        {
            return false;
        }

        result = FromRaw(u);
        return true;
    }

    /// <inheritdoc />
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => this.ToUInt64().ToString(format, formatProvider);

    #endregion Parsing

    #region Arithmetic

    #region Byte

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, byte b) => a + (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, byte b) => a - (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, byte b) => a * (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, byte b) => a / (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, byte b) => a % (UInt56)b;

    #endregion Byte

    #region SByte

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, sbyte b)
        => b < 0 ? throw new OverflowException("Do not add negative numbers to UInt56.") : a + new UInt56((uint)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, sbyte b)
        => b < 0 ? throw new OverflowException("Do not subtract negative numbers from UInt56.") : a - new UInt56((uint)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, sbyte b)
        => b < 0 ? throw new OverflowException("Do not multiply negative numbers by UInt56.") : a * new UInt56((uint)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, sbyte b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a / new UInt56((uint)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, sbyte b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a % new UInt56((uint)b, 0, 0);

    #endregion SByte

    #region Int16

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, short b)
        => b < 0 ? throw new OverflowException("Do not add negative numbers to UInt56.") : a + new UInt56((ushort)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, short b)
        => b < 0 ? throw new OverflowException("Do not subtract negative numbers from UInt56.") : a - new UInt56((ushort)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, short b)
        => b < 0 ? throw new OverflowException("Do not multiply negative numbers by UInt56.") : a * new UInt56((ushort)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, short b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a / new UInt56((ushort)b, 0, 0);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, short b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a % new UInt56((ushort)b, 0, 0);

    #endregion Int16

    #region UInt16

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, ushort b) => a + (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, ushort b) => a - (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, ushort b) => a * (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, ushort b)
        => b == 0 ? throw new DivideByZeroException() : a / (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, ushort b)
        => b == 0 ? throw new DivideByZeroException() : a % (UInt56)b;

    #endregion UInt16

    #region Int32

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, int b)
        => b < 0 ? throw new OverflowException("Do not add negative numbers to UInt56.") : a + (UInt56)(uint)b;

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, int b)
        => b < 0 ? throw new OverflowException("Do not subtract negative numbers from UInt56.") : a - (UInt56)(uint)b;

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, int b)
        => b < 0 ? throw new OverflowException("Do not multiply negative numbers by UInt56.") : a * (UInt56)(uint)b;

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, int b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a / (UInt56)(uint)b;

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, int b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a % (UInt56)(uint)b;

    #endregion Int32

    #region UInt32

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, uint b) => a + (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, uint b) => a - (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, uint b) => a * (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, uint b)
        => b == 0 ? throw new DivideByZeroException() : a / (UInt56)b;

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, uint b)
        => b == 0 ? throw new DivideByZeroException() : a % (UInt56)b;

    #endregion UInt32

    #region UInt56

    /// <summary>
    /// Adds two specified <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The sum of <paramref name="a"/> and <paramref name="b"/>.</returns>
    /// <exception cref="OverflowException">
    /// The result is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static UInt56 operator +(UInt56 a, UInt56 b)
    {
        ulong raw = a.ToUInt64() + b.ToUInt64();
        CheckOverflow(raw);
        return FromRaw(raw);
    }

    /// <summary>
    /// Subtracts one specified <see cref="UInt56"/> value from another.
    /// </summary>
    /// <param name="a">The value to subtract from.</param>
    /// <param name="b">The value to subtract.</param>
    /// <returns>The result of <paramref name="a"/> minus <paramref name="b"/>.</returns>
    /// <exception cref="OverflowException">
    /// The result would be negative.
    /// </exception>
    public static UInt56 operator -(UInt56 a, UInt56 b)
    {
        ulong av = a.ToUInt64(), bv = b.ToUInt64();
        return av < bv
            ? throw new OverflowException("Result would be negative; UInt56 is unsigned.")
            : FromRaw(av - bv);
    }

    /// <summary>
    /// Multiplies two specified <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The product of <paramref name="a"/> and <paramref name="b"/>.</returns>
    /// <exception cref="OverflowException">
    /// The result is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static UInt56 operator *(UInt56 a, UInt56 b)
    {
        ulong av = a.ToUInt64(), bv = b.ToUInt64();
        if (av == 0 || bv == 0)
        {
            return Zero;
        }
        else if (av > MaxValue / bv)
        {
            throw new OverflowException("Multiplication overflow for UInt56.");
        }
        else
        {
            return FromRaw(av * bv);
        }
    }

    /// <summary>
    /// Divides one specified <see cref="UInt56"/> value by another.
    /// </summary>
    /// <param name="a">The value to be divided.</param>
    /// <param name="b">The value to divide by.</param>
    /// <returns>The result of <paramref name="a"/> divided by <paramref name="b"/>.</returns>
    /// <exception cref="DivideByZeroException">
    /// <paramref name="b"/> is zero.
    /// </exception>
    public static UInt56 operator /(UInt56 a, UInt56 b)
    {
        ulong bv = b.ToUInt64();
        return bv == 0UL
            ? throw new DivideByZeroException()
            : FromRaw(a.ToUInt64() / bv);
    }

    /// <summary>
    /// Calculates the remainder from division of one specified <see cref="UInt56"/> value by another.
    /// </summary>
    /// <param name="a">The value to be divided.</param>
    /// <param name="b">The value to divide by.</param>
    /// <returns>
    /// The remainder resulting from the division of <paramref name="a"/> by <paramref name="b"/>.
    /// </returns>
    /// <exception cref="DivideByZeroException">
    /// <paramref name="b"/> is zero.
    /// </exception>
    public static UInt56 operator %(UInt56 a, UInt56 b)
    {
        ulong bv = b.ToUInt64();
        return bv == 0UL
            ? throw new DivideByZeroException()
            : FromRaw(a.ToUInt64() % bv);
    }

    /// <summary>
    /// Returns the bitwise complement of a <see cref="UInt56"/> value.
    /// </summary>
    /// <param name="value">A value.</param>
    /// <returns>The bitwise complement of <paramref name="value"/>.</returns>
    /// <remarks>
    /// Operates directly on the three storage fields without unpacking to
    /// <see cref="ulong"/>. The result is automatically masked to 56 bits because
    /// <c>~_hi</c> on a <see cref="byte"/> and <c>~_mid</c> on a
    /// <see cref="ushort"/> cannot exceed their respective field widths.
    /// </remarks>
    public static UInt56 operator ~(UInt56 value)
        => new(~value._lo, (ushort)~value._mid, (byte)~value._hi);

    /// <summary>
    /// Performs a bitwise AND operation on two <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="left">The first operand.</param>
    /// <param name="right">The second operand.</param>
    /// <returns>The bitwise AND of <paramref name="left"/> and <paramref name="right"/>.</returns>
    /// <remarks>
    /// Operates directly on the three storage fields without any unpack/repack step,
    /// making this the fastest possible bitwise operation for the 7-byte layout.
    /// </remarks>
    public static UInt56 operator &(UInt56 left, UInt56 right)
        => new(left._lo & right._lo, (ushort)(left._mid & right._mid), (byte)(left._hi & right._hi));

    /// <summary>
    /// Performs a bitwise OR operation on two <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="left">The first operand.</param>
    /// <param name="right">The second operand.</param>
    /// <returns>The bitwise OR of <paramref name="left"/> and <paramref name="right"/>.</returns>
    /// <remarks>
    /// Operates directly on the three storage fields without any unpack/repack step,
    /// making this the fastest possible bitwise operation for the 7-byte layout.
    /// </remarks>
    public static UInt56 operator |(UInt56 left, UInt56 right)
        => new(left._lo | right._lo, (ushort)(left._mid | right._mid), (byte)(left._hi | right._hi));

    /// <summary>
    /// Performs a bitwise exclusive OR (XOR) operation on two <see cref="UInt56"/> values.
    /// </summary>
    /// <param name="left">The first operand.</param>
    /// <param name="right">The second operand.</param>
    /// <returns>The bitwise XOR of <paramref name="left"/> and <paramref name="right"/>.</returns>
    /// <remarks>
    /// Operates directly on the three storage fields without any unpack/repack step,
    /// making this the fastest possible bitwise operation for the 7-byte layout.
    /// </remarks>
    public static UInt56 operator ^(UInt56 left, UInt56 right)
        => new(left._lo ^ right._lo, (ushort)(left._mid ^ right._mid), (byte)(left._hi ^ right._hi));

    /// <summary>
    /// Shifts a <see cref="UInt56"/> value left by a specified number of bits.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="shiftAmount">The number of bits to shift.</param>
    /// <returns>The result of shifting <paramref name="value"/> left by <paramref name="shiftAmount"/> bits.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="shiftAmount"/> is negative.
    /// </exception>
    public static UInt56 operator <<(UInt56 value, int shiftAmount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shiftAmount);
        return FromRaw((value.ToUInt64() << shiftAmount) & MaxValue);
    }

    /// <summary>
    /// Shifts a <see cref="UInt56"/> value right by a specified number of bits.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="shiftAmount">The number of bits to shift.</param>
    /// <returns>The result of shifting <paramref name="value"/> right by <paramref name="shiftAmount"/> bits.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="shiftAmount"/> is negative.
    /// </exception>
    public static UInt56 operator >>(UInt56 value, int shiftAmount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shiftAmount);
        return FromRaw(value.ToUInt64() >> shiftAmount);
    }

    /// <summary>
    /// Shifts a <see cref="UInt56"/> value right by a specified number of bits using unsigned (logical) right shift.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="shiftAmount">The number of bits to shift <paramref name="value"/> to the right.</param>
    /// <returns>
    /// The result of shifting <paramref name="value"/> right by <paramref name="shiftAmount"/> bits
    /// using unsigned right shift.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This operator performs an unsigned (logical) right shift, which means that the high-order bits
    /// are always filled with zeros regardless of the sign of the original number. For unsigned types
    /// like <see cref="UInt56"/>, this behavior is identical to the standard right shift operator (<c>&gt;&gt;</c>).
    /// </para>
    /// <para>
    /// The shift amount is masked to ensure it stays within the valid range for a 56-bit value.
    /// If <paramref name="shiftAmount"/> is negative, an <see cref="ArgumentOutOfRangeException"/> is thrown.
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
    /// UInt56 signedShift   = value &gt;&gt; 4;     // Signed right shift
    /// Console.WriteLine(unsignedShift == signedShift); // Output: True
    /// </code>
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="shiftAmount"/> is negative.
    /// </exception>
    public static UInt56 operator >>>(UInt56 value, int shiftAmount)
    {
        // Validate shift amount — negative shifts are not allowed
        ArgumentOutOfRangeException.ThrowIfNegative(shiftAmount);

        // Mask to lower 6 bits: shift amount is effectively modulo 64
        // If shiftAmount >= 56 every bit is shifted out -> result is 0
        shiftAmount &= 63;
        return shiftAmount >= 56 ? Zero : FromRaw(value.ToUInt64() >>> shiftAmount);
    }

    /// <summary>
    /// Increments a <see cref="UInt56"/> value by 1.
    /// </summary>
    /// <param name="a">The value to increment.</param>
    /// <returns>The value of <paramref name="a"/> incremented by 1.</returns>
    /// <exception cref="OverflowException">
    /// The result is greater than <see cref="MaxValue"/>.
    /// </exception>
    public static UInt56 operator ++(UInt56 a)
        => a.ToUInt64() == MaxValue
            ? throw new OverflowException("Overflow on increment.")
            : FromRaw(a.ToUInt64() + 1UL);

    /// <summary>
    /// Decrements a <see cref="UInt56"/> value by 1.
    /// </summary>
    /// <param name="a">The value to decrement.</param>
    /// <returns>The value of <paramref name="a"/> decremented by 1.</returns>
    /// <exception cref="OverflowException">
    /// The result would be less than <see cref="MinValue"/>.
    /// </exception>
    public static UInt56 operator --(UInt56 a)
        => a.ToUInt64() == 0UL
            ? throw new OverflowException("Underflow on decrement.")
            : FromRaw(a.ToUInt64() - 1UL);

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
    /// UInt56 negated = -value;     // Result: MaxValue (wraparound)
    /// Console.WriteLine(negated);  // Output: 72057594037927935 (2^56 - 1)
    ///
    /// UInt56 five = new UInt56(5);
    /// UInt56 negatedFive = -five;  // Result: MaxValue - 4
    /// Console.WriteLine(negatedFive); // Output: 72057594037927931
    /// </code>
    /// </example>
    public static UInt56 operator -(UInt56 value)
    {
        ulong v = value.ToUInt64();
        return v == 0 ? Zero : FromRaw(MaxValue + 1 - v);
    }

    /// <summary>
    /// Returns the value of the <see cref="UInt56"/> operand. The sign of the operand is unchanged.
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
    /// Console.WriteLine(value == positive); // Output: True
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt56 operator +(UInt56 value) => value;

    #endregion UInt56

    #region Int64

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, long b)
        => b < 0 ? throw new OverflowException("Do not add negative numbers to UInt56.") : a + new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, long b)
        => b < 0 ? throw new OverflowException("Do not subtract negative numbers from UInt56.") : a - new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, long b)
        => b < 0 ? throw new OverflowException("Do not multiply negative numbers by UInt56.") : a * new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, long b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a / new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, long b)
        => b <= 0 ? throw new OverflowException("Divisor must be > 0 for UInt56.") : a % new UInt56((ulong)b);

    #endregion Int64

    #region UInt64

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, ulong b) => a + new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, ulong b) => a - new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, ulong b) => a * new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, ulong b)
        => b == 0 ? throw new DivideByZeroException() : a / new UInt56(b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, ulong b)
        => b == 0 ? throw new DivideByZeroException() : a % new UInt56(b);

    #endregion UInt64

    #region Single

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, float b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid float value for UInt56.") : a + new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, float b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid float value for UInt56.") : a - new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, float b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid float value for UInt56.") : a * new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, float b)
        => b is <= 0 or > MaxValue ? throw new OverflowException("Invalid float division value for UInt56.") : a / new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, float b)
        => b is <= 0 or > MaxValue ? throw new OverflowException("Invalid float division value for UInt56.") : a % new UInt56((ulong)b);

    #endregion Single

    #region Double

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, double b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid double value for UInt56.") : a + new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, double b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid double value for UInt56.") : a - new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, double b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid double value for UInt56.") : a * new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, double b)
        => b is <= 0 or > MaxValue ? throw new OverflowException("Double value division is not suitable for UInt56.") : a / new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, double b)
        => b is <= 0 or > MaxValue ? throw new OverflowException("Double value division is not suitable for UInt56.") : a % new UInt56((ulong)b);

    #endregion Double

    #region Decimal

    /// <inheritdoc/>
    public static UInt56 operator +(UInt56 a, decimal b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid decimal value for UInt56.") : a + new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator -(UInt56 a, decimal b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid decimal value for UInt56.") : a - new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator *(UInt56 a, decimal b)
        => b is < 0 or > MaxValue ? throw new OverflowException("Invalid decimal value for UInt56.") : a * new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator /(UInt56 a, decimal b)
        => b is <= 0 or > MaxValue ? throw new OverflowException("Invalid decimal division value for UInt56.") : a / new UInt56((ulong)b);

    /// <inheritdoc/>
    public static UInt56 operator %(UInt56 a, decimal b)
        => b is <= 0 or > MaxValue ? throw new OverflowException("Invalid decimal division value for UInt56.") : a % new UInt56((ulong)b);

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
    /// A span containing the characters that represent a standard or custom format string that defines
    /// the acceptable format for this instance.
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
    ///     Console.WriteLine(buffer[..charsWritten].ToString()); // Output: BC614E
    /// }
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
        => this.ToUInt64().TryFormat(destination, out charsWritten, format, provider);

    #endregion ISpanFormattable Implementation

    #region IUtf8SpanFormattable Implementation

    /// <summary>
    /// Tries to format the value of the current <see cref="UInt56"/> instance as UTF-8
    /// into the provided span of bytes.
    /// </summary>
    /// <param name="utf8Destination">
    /// When this method returns, contains the formatted representation of this instance
    /// as a span of UTF-8 bytes.
    /// </param>
    /// <param name="bytesWritten">
    /// When this method returns, contains the number of bytes that were written
    /// in <paramref name="utf8Destination"/>.
    /// </param>
    /// <param name="format">
    /// A span containing the characters that represent a standard or custom format string
    /// that defines the acceptable format for this instance.
    /// </param>
    /// <param name="provider">
    /// An optional object that supplies culture-specific formatting information for
    /// <paramref name="utf8Destination"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.
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
    /// If the <paramref name="utf8Destination"/> span is too small to contain the formatted
    /// representation, the method returns <see langword="false"/> and
    /// <paramref name="bytesWritten"/> is set to 0.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// UInt56 value = new UInt56(0xABCDEF123456UL);
    /// Span&lt;byte&gt; utf8Buffer = stackalloc byte[32];
    ///
    /// if (value.TryFormat(utf8Buffer, out int bytesWritten, "X", null))
    /// {
    ///     string result = Encoding.UTF8.GetString(utf8Buffer[..bytesWritten]);
    ///     Console.WriteLine(result); // Output: ABCDEF123456
    /// }
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
        => this.ToUInt64().TryFormat(utf8Destination, out bytesWritten, format, provider);

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
    /// <exception cref="ArgumentException">
    /// <paramref name="s"/> is empty or contains only white space.
    /// </exception>
    /// <exception cref="FormatException">
    /// <paramref name="s"/> is not in the correct format.
    /// </exception>
    /// <exception cref="OverflowException">
    /// <paramref name="s"/> represents a value that is outside the range of <see cref="UInt56"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method provides zero-allocation parsing from character spans, making it ideal
    /// for high-performance scenarios where string allocation should be avoided.
    /// </para>
    /// <para>
    /// The method supports the same format as <see cref="Parse(string, IFormatProvider)"/>
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
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt56 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => Parse(s, NumberStyles.Integer, provider);

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
    /// <exception cref="ArgumentException">
    /// <paramref name="s"/> is empty or contains only white space.
    /// </exception>
    /// <exception cref="FormatException">
    /// <paramref name="s"/> is not in the correct format.
    /// </exception>
    /// <exception cref="OverflowException">
    /// <paramref name="s"/> represents a value that is outside the range of <see cref="UInt56"/>.
    /// </exception>
    /// <remarks>
    /// This method provides fine-grained control over parsing behavior through the
    /// <paramref name="style"/> parameter, while maintaining zero-allocation performance characteristics.
    /// </remarks>
    /// <example>
    /// <code>
    /// ReadOnlySpan&lt;char&gt; hexSpan = "ABCDEF123456".AsSpan();
    /// UInt56 value = UInt56.Parse(hexSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    /// Console.WriteLine($"0x{value:X}"); // Output: 0xABCDEF123456
    /// </code>
    /// </example>
    [Pure]
    public static UInt56 Parse(
        ReadOnlySpan<char> s,
        NumberStyles style,
        IFormatProvider? provider)
        => TryParse(s, style, provider, out UInt56 result)
            ? result
            : throw new FormatException(
                "Input string was not in a correct format or was out of range for UInt56.");

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
    /// ReadOnlySpan&lt;char&gt; span = "999999999999999".AsSpan();
    /// if (UInt56.TryParse(span, CultureInfo.InvariantCulture, out UInt56 value))
    /// {
    ///     Console.WriteLine($"Parsed: {value}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("Failed to parse");
    /// }
    /// </code>
    /// </example>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(
        ReadOnlySpan<char> s,
        IFormatProvider? provider,
        out UInt56 result)
        => TryParse(s, NumberStyles.Integer, provider, out result);

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
    /// var style = NumberStyles.Integer | NumberStyles.AllowThousands
    ///           | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
    ///
    /// if (UInt56.TryParse(span, style, CultureInfo.InvariantCulture, out UInt56 result))
    /// {
    ///     Console.WriteLine(result); // Output: 1234567
    /// }
    /// </code>
    /// </example>
    [Pure]
    public static bool TryParse(
        ReadOnlySpan<char> s,
        NumberStyles style,
        IFormatProvider? provider,
        out UInt56 result)
    {
        result = default;
        if (s.IsEmpty || s.IsWhiteSpace())
        {
            return false;
        }

        if (!ulong.TryParse(s, style, provider, out ulong u))
        {
            return false;
        }

        if (u > MaxValue)
        {
            return false;
        }

        result = FromRaw(u);
        return true;
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="UInt56"/> using the specified format provider.
    /// </summary>
    /// <param name="s">
    /// The string representation of a number to parse. The string is interpreted using the
    /// <see cref="NumberStyles.Integer"/> style.
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
    /// This method is part of the <see cref="INumberBase{TSelf}"/> interface
    /// and provides a standardized way to parse numeric types in generic contexts.
    /// It uses <see cref="NumberStyles.Integer"/> as the default parsing style.
    /// </para>
    /// <para>
    /// This method will never throw an exception. If parsing fails for any reason
    /// (invalid format, null input, out of range), it returns <see langword="false"/>
    /// and sets <paramref name="result"/> to the default <see cref="UInt56"/> value (zero).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// if (UInt56.TryParse("123456789", CultureInfo.InvariantCulture, out UInt56 value))
    /// {
    ///     Console.WriteLine($"Parsed: {value}"); // Output: Parsed: 123456789
    /// }
    ///
    /// // Generic usage
    /// public static bool ParseNumber&lt;T&gt;(string input, IFormatProvider provider, out T result)
    ///     where T : INumberBase&lt;T&gt;
    /// {
    ///     return T.TryParse(input, provider, out result);
    /// }
    /// bool success = ParseNumber("42", CultureInfo.InvariantCulture, out UInt56 number);
    /// </code>
    /// </example>
    /// <seealso cref="Parse(string, IFormatProvider)"/>
    /// <seealso cref="TryParse(string, NumberStyles, IFormatProvider, out UInt56)"/>
    /// <seealso cref="TryParse(ReadOnlySpan{char}, IFormatProvider, out UInt56)"/>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out UInt56 result)
        => TryParse(s, NumberStyles.Integer, provider, out result);

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
    static UInt56 INumberBase<UInt56>.Abs(UInt56 value) => value;

    /// <summary>
    /// Determines if a value represents an even integral number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is an even integer;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Tests bit 0 of <c>_lo</c> directly without unpacking to <see cref="ulong"/>.
    /// </remarks>
    static bool INumberBase<UInt56>.IsEvenInteger(UInt56 value)
        => (value._lo & 1u) == 0u;

    /// <summary>
    /// Determines if a value represents an odd integral number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is an odd integer;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Tests bit 0 of <c>_lo</c> directly without unpacking to <see cref="ulong"/>.
    /// </remarks>
    static bool INumberBase<UInt56>.IsOddInteger(UInt56 value)
        => (value._lo & 1u) != 0u;

    /// <summary>
    /// Determines if a value is zero.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is zero; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Checks all three fields directly without unpacking to <see cref="ulong"/>.
    /// </remarks>
    static bool INumberBase<UInt56>.IsZero(UInt56 value)
        => value._lo == 0u && value._mid == 0 && value._hi == 0;

    /// <summary>
    /// Determines if a value represents a value greater than or equal to zero.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is positive; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For unsigned types like <see cref="UInt56"/>, all values are considered positive or zero.
    /// </remarks>
    static bool INumberBase<UInt56>.IsPositive(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents zero or a positive real number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is zero or positive;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, positive infinity is not representable.
    /// </remarks>
    static bool INumberBase<UInt56>.IsPositiveInfinity(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a negative real number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> represents negative infinity;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, negative infinity is not representable.
    /// </remarks>
    static bool INumberBase<UInt56>.IsNegativeInfinity(UInt56 value) => false;

    /// <summary>
    /// Determines if a value is negative.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is negative; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For unsigned types like <see cref="UInt56"/>, no values are negative.
    /// </remarks>
    static bool INumberBase<UInt56>.IsNegative(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a finite value.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is finite; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, all values are finite.
    /// </remarks>
    static bool INumberBase<UInt56>.IsFinite(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents an infinite value.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is infinite; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, infinity is not representable.
    /// </remarks>
    static bool INumberBase<UInt56>.IsInfinity(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents an integral number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is an integer; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, all values are integers.
    /// </remarks>
    static bool INumberBase<UInt56>.IsInteger(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents <c>NaN</c>.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is <c>NaN</c>; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, NaN is not representable.
    /// </remarks>
    static bool INumberBase<UInt56>.IsNaN(UInt56 value) => false;

    /// <summary>
    /// Determines if a value is normal.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is normal; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, all non-zero values are considered normal.
    /// Checks all three fields directly without unpacking to <see cref="ulong"/>.
    /// </remarks>
    static bool INumberBase<UInt56>.IsNormal(UInt56 value)
        => value._lo != 0u || value._mid != 0 || value._hi != 0;

    /// <summary>
    /// Determines if a value is subnormal.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is subnormal; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, subnormal values do not exist.
    /// </remarks>
    static bool INumberBase<UInt56>.IsSubnormal(UInt56 value) => false;

    /// <summary>
    /// Compares two values to compute which is greater.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>.</param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>The greater of <paramref name="x"/> or <paramref name="y"/>.</returns>
    static UInt56 INumberBase<UInt56>.MaxMagnitude(UInt56 x, UInt56 y) => x > y ? x : y;

    /// <summary>
    /// Compares two values to compute which has the greater magnitude and returning the other value
    /// if an input is <c>NaN</c>.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>.</param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>
    /// The value with the greater magnitude; or whichever is not <c>NaN</c> if there is only one.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, this behaves identically to
    /// <see cref="INumberBase{TSelf}.MaxMagnitude"/>.
    /// </remarks>
    static UInt56 INumberBase<UInt56>.MaxMagnitudeNumber(UInt56 x, UInt56 y) => x > y ? x : y;

    /// <summary>
    /// Compares two values to compute which is lesser.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>.</param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>The lesser of <paramref name="x"/> or <paramref name="y"/>.</returns>
    static UInt56 INumberBase<UInt56>.MinMagnitude(UInt56 x, UInt56 y) => x < y ? x : y;

    /// <summary>
    /// Compares two values to compute which has the lesser magnitude and returning the other value
    /// if an input is <c>NaN</c>.
    /// </summary>
    /// <param name="x">The value to compare with <paramref name="y"/>.</param>
    /// <param name="y">The value to compare with <paramref name="x"/>.</param>
    /// <returns>
    /// The value with the lesser magnitude; or whichever is not <c>NaN</c> if there is only one.
    /// </returns>
    /// <remarks>
    /// For integer types like <see cref="UInt56"/>, this behaves identically to
    /// <see cref="INumberBase{TSelf}.MinMagnitude"/>.
    /// </remarks>
    static UInt56 INumberBase<UInt56>.MinMagnitudeNumber(UInt56 x, UInt56 y) => x < y ? x : y;

    /// <summary>
    /// Determines if a value is in its canonical representation.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is in its canonical representation;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// For integer types like <see cref="UInt56"/>, all values are always in their canonical
    /// representation. The canonical representation is the standard, unique way to represent a number.
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
    static bool INumberBase<UInt56>.IsCanonical(UInt56 value) => true;

    /// <summary>
    /// Determines if a value represents a complex number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is a complex number;
    /// otherwise, <see langword="false"/>.
    /// </returns>
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
    static bool INumberBase<UInt56>.IsComplexNumber(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a pure imaginary number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is an imaginary number;
    /// otherwise, <see langword="false"/>.
    /// </returns>
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
    /// Console.WriteLine(isImaginary); // Output: False
    /// </code>
    /// </example>
    static bool INumberBase<UInt56>.IsImaginaryNumber(UInt56 value) => false;

    /// <summary>
    /// Determines if a value represents a real number.
    /// </summary>
    /// <param name="value">The value to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is a real number;
    /// otherwise, <see langword="false"/>.
    /// </returns>
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
    /// Console.WriteLine(isRealZero); // Output: True
    /// </code>
    /// </example>
    static bool INumberBase<UInt56>.IsRealNumber(UInt56 value) => true;

    #endregion INumberBase<UInt56> Methods

    #region Generic Conversion Methods

    /// <summary>
    /// Tries to convert a value to a <see cref="UInt56"/> instance, throwing an overflow exception
    /// for any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to a <see cref="UInt56"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static bool INumberBase<UInt56>.TryConvertFromChecked<TOther>(TOther value, out UInt56 result)
        => TryConvertFrom(value, out result);

    /// <summary>
    /// Tries to convert a value to a <see cref="UInt56"/> instance, saturating any values
    /// that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to a <see cref="UInt56"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static bool INumberBase<UInt56>.TryConvertFromSaturating<TOther>(TOther value, out UInt56 result)
        => TryConvertFrom(value, out result);

    /// <summary>
    /// Tries to convert a value to a <see cref="UInt56"/> instance, truncating any values
    /// that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to a <see cref="UInt56"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static bool INumberBase<UInt56>.TryConvertFromTruncating<TOther>(TOther value, out UInt56 result)
        => TryConvertFrom(value, out result);

    /// <summary>
    /// Tries to convert a <see cref="UInt56"/> instance to another type, throwing an overflow
    /// exception for any values that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type to convert the <see cref="UInt56"/> to.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to <typeparamref name="TOther"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static bool INumberBase<UInt56>.TryConvertToChecked<TOther>(UInt56 value, out TOther result)
        where TOther : default
        => TryConvertTo(value, out result);

    /// <summary>
    /// Tries to convert a <see cref="UInt56"/> instance to another type, saturating any values
    /// that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type to convert the <see cref="UInt56"/> to.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to <typeparamref name="TOther"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static bool INumberBase<UInt56>.TryConvertToSaturating<TOther>(UInt56 value, out TOther result)
        where TOther : default
        => TryConvertTo(value, out result);

    /// <summary>
    /// Tries to convert a <see cref="UInt56"/> instance to another type, truncating any values
    /// that fall outside the representable range.
    /// </summary>
    /// <typeparam name="TOther">The type to convert the <see cref="UInt56"/> to.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">On return, contains the result of converting <paramref name="value"/> to <typeparamref name="TOther"/>.</param>
    /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
    static bool INumberBase<UInt56>.TryConvertToTruncating<TOther>(UInt56 value, out TOther result)
        where TOther : default
        => TryConvertTo(value, out result);

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
    static bool INumberBase<UInt56>.TryParse(
        ReadOnlySpan<char> s,
        NumberStyles style,
        IFormatProvider? provider,
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
    static bool INumberBase<UInt56>.TryParse(
        ReadOnlySpan<byte> utf8Text,
        NumberStyles style,
        IFormatProvider? provider,
        out UInt56 result)
    {
        // Convert UTF-8 bytes to string and delegate to existing parsing logic
        string s = Encoding.UTF8.GetString(utf8Text);
        return TryParse(s, style, provider, out result);
    }

    #endregion Parsing Interface Methods

    #endregion INumber<UInt56> Implementation

    #region Helper Methods

    /// <summary>
    /// Throws an <see cref="OverflowException"/> if the specified raw value
    /// is outside the range of the <see cref="UInt56"/> type.
    /// </summary>
    /// <param name="raw">The raw value to validate.</param>
    /// <exception cref="OverflowException">
    /// <paramref name="raw"/> is outside the valid range of <see cref="UInt56"/>.
    /// </exception>
    /// <remarks>
    /// Uses a bitmask check (<c>raw &amp; ~MaxValue</c>) which is a single AND instruction —
    /// faster than a comparison against <see cref="MaxValue"/> because it avoids a branch
    /// on most architectures when the value is in range.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckOverflow(ulong raw)
    {
        if ((raw & ~MaxValue) != 0UL)
        {
            throw new OverflowException(
                "Arithmetic operation produced a value that is out of range for UInt56.");
        }
    }

    /// <summary>
    /// Helper method to convert from other numeric types to <see cref="UInt56"/>.
    /// </summary>
    /// <typeparam name="TOther">The type to convert from.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="result">The converted result.</param>
    /// <returns><see langword="true"/> if conversion was successful; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertFrom<TOther>(TOther value, out UInt56 result)
        where TOther : INumberBase<TOther>
    {
        result = default;

        if (typeof(TOther) == typeof(byte))
        {
            result = new UInt56((uint)(object)value, 0, 0); return true;
        }
        else if (typeof(TOther) == typeof(ushort))
        {
            result = new UInt56((ushort)(object)value, 0, 0); return true;
        }
        else if (typeof(TOther) == typeof(uint))
        {
            result = new UInt56((uint)(object)value, 0, 0); return true;
        }
        else if (typeof(TOther) == typeof(ulong))
        {
            ulong v = (ulong)(object)value; if (v <= MaxValue)
            {
                result = FromRaw(v); return true;
            }
        }
        else if (typeof(TOther) == typeof(int))
        {
            int v = (int)(object)value; if (v >= 0)
            {
                result = new UInt56((uint)v, 0, 0); return true;
            }
        }
        else if (typeof(TOther) == typeof(long))
        {
            long v = (long)(object)value; if (v >= 0 && (ulong)v <= MaxValue)
            {
                result = FromRaw((ulong)v); return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Helper method to convert from <see cref="UInt56"/> to other numeric types.
    /// </summary>
    /// <typeparam name="TOther">The type to convert to.</typeparam>
    /// <param name="value">The <see cref="UInt56"/> value to convert.</param>
    /// <param name="result">The converted result.</param>
    /// <returns><see langword="true"/> if conversion was successful; otherwise, <see langword="false"/>.</returns>
    private static bool TryConvertTo<TOther>(UInt56 value, out TOther result)
        where TOther : INumberBase<TOther>
    {
        ulong v = value.ToUInt64();

        if (typeof(TOther) == typeof(byte))
        {
            if (v <= byte.MaxValue)
            {
                result = (TOther)(object)(byte)v; return true;
            }
        }
        else if (typeof(TOther) == typeof(ushort))
        {
            if (v <= ushort.MaxValue)
            {
                result = (TOther)(object)(ushort)v; return true;
            }
        }
        else if (typeof(TOther) == typeof(uint))
        {
            if (v <= uint.MaxValue)
            {
                result = (TOther)(object)(uint)v; return true;
            }
        }
        else if (typeof(TOther) == typeof(ulong))
        {
            result = (TOther)(object)v; return true;
        }
        else if (typeof(TOther) == typeof(int))
        {
            if (v <= int.MaxValue)
            {
                result = (TOther)(object)(int)v; return true;
            }
        }
        else if (typeof(TOther) == typeof(long))
        {
            if (v <= long.MaxValue)
            {
                result = (TOther)(object)(long)v; return true;
            }
        }

        result = default!;
        return false;
    }

    #endregion Helper Methods
}
