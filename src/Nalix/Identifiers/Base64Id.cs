using Nalix.Common.Identity;
using Nalix.Identifiers.Internal;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Nalix.Identifiers;

/// <summary>
/// Represents a high-performance, space-efficient unique identifier that supports both Base64Value and hexadecimal representations.
/// </summary>
/// <remarks>
/// This implementation provides fast conversion between numeric and string representations,
/// with optimized memory usage and performance characteristics.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="Base64Id"/> struct with the specified value.
/// </remarks>
/// <param name="value">The 32-bit unsigned integer value.</param>
public readonly struct Base64Id(uint value) : IEncodedId, IEquatable<Base64Id>, IComparable<Base64Id>
{
    #region Fields and Static Constructor

    /// <summary>
    /// Lookup table for converting characters to their Base64Value values.
    /// </summary>
    private static readonly byte[] CharToValue;

    /// <summary>
    /// The underlying 32-bit value.
    /// </summary>
    private readonly uint _value = value;

    /// <summary>
    /// Empty/default instance with a value of 0.
    /// </summary>
    public static readonly Base64Id Empty = new(0);

    /// <summary>
    /// Static constructor to initialize the character lookup table.
    /// </summary>
    static Base64Id()
        => CharToValue = BaseNEncoding.CreateCharLookupTable(BaseConstants.Alphabet64);

    #endregion Fields and Static Constructor

    #region Properties

    /// <summary>
    /// Gets the underlying 32-bit unsigned integer value.
    /// </summary>
    public uint Value => _value;

    /// <summary>
    /// Gets the IdentifierType encoded within this Base64Id.
    /// </summary>
    public IdentifierType Type => (IdentifierType)(_value >> 24);

    /// <summary>
    /// Gets the machine Number component encoded within this Base64Id.
    /// </summary>
    public ushort MachineId => (ushort)(_value & 0xFFFF);

    #endregion Properties

    #region Methods

    #region Static Methods

    /// <summary>
    /// Generate a new Number from random and system elements.
    /// </summary>
    /// <param name="type">The unique Number type to generate.</param>
    /// <param name="machineId">The unique Number for each different server.</param>
    /// <returns>A new <see cref="Base64Id"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if type exceeds the allowed limit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base64Id NewId(IdentifierType type = IdentifierType.Unknown, ushort machineId = 0)
        => new(BaseNEncoding.GenerateId(type, machineId));

    /// <summary>
    /// Parses a string representation into a <see cref="Base64Id"/>.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <returns>The parsed <see cref="Base64Id"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if input is empty.</exception>
    /// <exception cref="FormatException">Thrown if input is in an invalid format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base64Id Parse(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        // Check if it's likely a hex string (exactly 8 characters)
        if (input.Length == BaseConstants.HexLength)
        {
            // Try to parse as hex first
            if (BaseNEncoding.TryParseHex(input, out uint value))
                return new Base64Id(value);
        }

        // Otherwise parse as Base64Value
        return new Base64Id(BaseNEncoding.DecodeFromBaseN(input, CharToValue, BaseConstants.Base64Value));
    }

    /// <summary>
    /// Parses a string representation into a <see cref="Base64Id"/>, with explicit format specification.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="isHex">If true, parse as hexadecimal; otherwise, parse as Base64Value.</param>
    /// <returns>The parsed <see cref="Base64Id"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if input is empty.</exception>
    /// <exception cref="ArgumentException">Thrown if input is in an invalid format.</exception>
    /// <exception cref="FormatException">Thrown if input contains invalid characters.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base64Id Parse(ReadOnlySpan<char> input, bool isHex)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        if (isHex)
        {
            if (input.Length != BaseConstants.HexLength)
                throw new ArgumentException(
                    $"Invalid Hex length. Must be {BaseConstants.HexLength} characters.", nameof(input));

            // Parse as hex (uint.Parse validates hex digits)
            return new Base64Id(uint.Parse(input, System.Globalization.NumberStyles.HexNumber));
        }

        // Parse as Base64Value
        return new Base64Id(BaseNEncoding.DecodeFromBaseN(input, CharToValue, BaseConstants.Base64Value));
    }

    /// <summary>
    /// Attempts to parse a string into a <see cref="Base64Id"/>.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed value if successful; otherwise, the default value.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> input, out Base64Id result)
    {
        result = Empty;

        if (input.IsEmpty || input.Length > 6)
            return false;

        // Try to parse as hex first if it's the right length
        if (input.Length == BaseConstants.HexLength && BaseNEncoding.TryParseHex(input, out uint hexValue))
        {
            result = new Base64Id(hexValue);
            return true;
        }

        // Otherwise try Base64Value
        if (BaseNEncoding.TryDecodeFromBaseN(input, CharToValue, BaseConstants.Base64Value, out uint value))
        {
            result = new Base64Id(value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a Base64Id from its type and machine components plus a random portion.
    /// </summary>
    /// <param name="type">The type identifier.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="randomValue">A custom random value (if not provided, a secure random value will be generated).</param>
    /// <returns>A new Base64Id with the specified components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base64Id FromComponents(IdentifierType type, ushort machineId, uint? randomValue = null)
    {
        if ((int)type >= (int)IdentifierType.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(type), "IdentifierType exceeds the allowed limit.");

        uint random = randomValue ?? BaseNEncoding.GenerateSecureRandomUInt();

        return new Base64Id(
            ((uint)type << 24) |              // Type in high 8 bits
            (random & 0x00FFFF00) |          // Random value in middle bits
            ((uint)machineId & 0xFFFF)       // Machine Number in low 16 bits
        );
    }

    /// <summary>
    /// Creates a Base64Id from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the Base64Id value.</param>
    /// <returns>A Base64Id created from the bytes.</returns>
    /// <exception cref="ArgumentException">Thrown if the byte array is not exactly 4 bytes long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base64Id FromByteArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 4)
            throw new ArgumentException("Byte array must be exactly 4 bytes long.", nameof(bytes));

        return new Base64Id(BinaryPrimitives.ReadUInt32LittleEndian(bytes));
    }

    /// <summary>
    /// Tries to parse a Base64Id from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the Base64Id value.</param>
    /// <param name="result">The resulting Base64Id if parsing was successful.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    public static bool TryFromByteArray(ReadOnlySpan<byte> bytes, out Base64Id result)
    {
        result = Empty;

        if (bytes.Length != 4)
            return false;

        result = new Base64Id(BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        return true;
    }

    #endregion Static Methods

    #region Instance Methods

    /// <summary>
    /// Converts the Number to a string representation.
    /// </summary>
    /// <param name="isHex">If true, returns an 8-digit hexadecimal string; otherwise, returns a Base64Value string.</param>
    /// <returns>The string representation of the Number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(bool isHex = false)
    {
        if (isHex)
            return _value.ToString("X8");

        return ToBase64String();
    }

    /// <summary>
    /// Returns the default string representation (Base64Value).
    /// </summary>
    public override string ToString() => ToBase64String();

    /// <summary>
    /// Converts the Number to a Base64Value string with minimum padding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ToBase64String()
    {
        // For efficiency, allocate a stack buffer for the maximum possible length
        // Base64Value representation of uint.MaxValue is at most 6 characters
        Span<char> buffer = stackalloc char[6];
        int position = buffer.Length;
        uint remaining = _value;

        // Generate digits from right to left
        do
        {
            uint digit = remaining % BaseConstants.Base64Value;
            remaining /= BaseConstants.Base64Value;
            buffer[--position] = BaseConstants.Alphabet64[(int)digit];
        } while (remaining > 0);

        // Create a new string from the buffer
        return new string(buffer[position..]);
    }

    /// <summary>
    /// Converts the Base64Id to a byte array.
    /// </summary>
    /// <returns>A 4-byte array representing this Base64Id.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToByteArray()
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, _value);
        return bytes;
    }

    /// <summary>
    /// Tries to write the Base64Id to a span of bytes.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="bytesWritten">The Number of bytes written.</param>
    /// <returns>True if successful; false if the destination is too small.</returns>
    public bool TryWriteBytes(Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < 4)
        {
            bytesWritten = 0;
            return false;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination, _value);
        bytesWritten = 4;
        return true;
    }

    #endregion Instance Methods

    #region Equality and Comparison

    /// <summary>
    /// Determines whether the current instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>true if the specified object is a <see cref="Base64Id"/> and equals the current instance; otherwise, false.</returns>
    public override bool Equals(object obj) => obj is Base64Id other && Equals(other);

    /// <summary>
    /// Determines whether the current instance is equal to another <see cref="Base64Id"/>.
    /// </summary>
    /// <param name="other">The <see cref="Base64Id"/> to compare with the current instance.</param>
    /// <returns>true if both instances have the same value; otherwise, false.</returns>
    public bool Equals(Base64Id other) => _value == other._value;

    /// <summary>
    /// Determines whether the current instance is equal to another <see cref="IEncodedId"/>.
    /// </summary>
    /// <param name="other">The <see cref="IEncodedId"/> to compare with the current instance.</param>
    /// <returns>true if both instances have the same value; otherwise, false.</returns>
    public bool Equals(IEncodedId other) => _value == other.Value;

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>A hash code for the current <see cref="Base64Id"/>.</returns>
    public override int GetHashCode() => (int)_value;

    /// <summary>
    /// Compares this instance with another <see cref="Base64Id"/>.
    /// </summary>
    /// <param name="other">The <see cref="Base64Id"/> to compare with this instance.</param>
    /// <returns>A value indicating the relative order of the instances.</returns>
    public int CompareTo(Base64Id other) => _value.CompareTo(other._value);

    /// <summary>
    /// Gets a value indicating whether this Number is empty (has a value of 0).
    /// </summary>
    /// <returns>True if this Number is empty; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty() => _value == 0;

    #endregion Equality and Comparison

    #region Operators

    /// <summary>
    /// Determines whether one <see cref="Base64Id"/> is less than another.
    /// </summary>
    public static bool operator <(Base64Id left, Base64Id right) => left._value < right._value;

    /// <summary>
    /// Determines whether one <see cref="Base64Id"/> is less than or equal to another.
    /// </summary>
    public static bool operator <=(Base64Id left, Base64Id right) => left._value <= right._value;

    /// <summary>
    /// Determines whether one <see cref="Base64Id"/> is greater than another.
    /// </summary>
    public static bool operator >(Base64Id left, Base64Id right) => left._value > right._value;

    /// <summary>
    /// Determines whether one <see cref="Base64Id"/> is greater than or equal to another.
    /// </summary>
    public static bool operator >=(Base64Id left, Base64Id right) => left._value >= right._value;

    /// <summary>
    /// Determines whether two <see cref="Base64Id"/> instances are equal.
    /// </summary>
    public static bool operator ==(Base64Id left, Base64Id right) => left._value == right._value;

    /// <summary>
    /// Determines whether two <see cref="Base64Id"/> instances are not equal.
    /// </summary>
    public static bool operator !=(Base64Id left, Base64Id right) => left._value != right._value;

    /// <summary>
    /// Implicit conversion from Base64Id to uint.
    /// </summary>
    /// <param name="id">The Base64Id to convert.</param>
    public static implicit operator uint(Base64Id id) => id._value;

    /// <summary>
    /// Explicit conversion from uint to Base64Id.
    /// </summary>
    /// <param name="value">The uint value to convert.</param>
    public static explicit operator Base64Id(uint value) => new(value);

    #endregion Operators

    #endregion Methods
}