using Notio.Common.Identity;
using Notio.Defaults;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Notio.Identifiers;

/// <summary>
/// Represents a high-performance, space-efficient unique identifier that supports both Base32 and hexadecimal representations.
/// </summary>
/// <remarks>
/// This implementation provides fast conversion between numeric and string representations,
/// with optimized memory usage and performance characteristics.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="Base32Id"/> struct with the specified value.
/// </remarks>
/// <param name="value">The 32-bit unsigned integer value.</param>
public readonly struct Base32Id(uint value) : IEncodedId, IEquatable<Base32Id>, IComparable<Base32Id>
{
    /// <summary>
    /// Lookup table for converting characters to their Base32 values.
    /// </summary>
    private static readonly byte[] CharToValue;

    /// <summary>
    /// The underlying 32-bit value.
    /// </summary>
    private readonly uint _value = value;

    /// <summary>
    /// Empty/default instance with a value of 0.
    /// </summary>
    public static readonly Base32Id Empty = new(0);

    /// <summary>
    /// Static constructor to initialize the character lookup table.
    /// </summary>
    static Base32Id()
    {
        // Initialize lookup table with 'invalid' marker
        CharToValue = new byte[128];
        for (int i = 0; i < CharToValue.Length; i++)
        {
            CharToValue[i] = byte.MaxValue;
        }

        // Populate lookup table for valid Base32 characters
        for (byte i = 0; i < DefaultEncodings.Base32Alphabet.Length; i++)
        {
            char c = DefaultEncodings.Base32Alphabet[i];
            CharToValue[c] = i;

            // Map lowercase letters to their uppercase equivalents
            if (c >= 'A' && c <= 'Z')
            {
                CharToValue[c + 32] = i; // +32 is the difference between uppercase and lowercase ASCII
            }
        }
    }

    /// <summary>
    /// Gets the underlying 32-bit unsigned integer value.
    /// </summary>
    public uint Value => _value;

    /// <summary>
    /// Gets the IdType encoded within this Base32Id.
    /// </summary>
    public IdType Type => (IdType)(_value >> 24);

    /// <summary>
    /// Gets the machine ID component encoded within this Base32Id.
    /// </summary>
    public ushort MachineId => (ushort)(_value & 0xFFFF);

    /// <summary>
    /// Generate a new ID from random and system elements.
    /// </summary>
    /// <param name="type">The unique ID type to generate.</param>
    /// <param name="machineId">The unique ID for each different server.</param>
    /// <returns>A new <see cref="Base32Id"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if type exceeds the allowed limit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base32Id NewId(IdType type = IdType.Generic, ushort machineId = 0)
    {
        // Validate type
        if ((int)type >= (int)IdType.Limit)
            throw new ArgumentOutOfRangeException(nameof(type), "IdType exceeds the allowed limit.");

        // Get a cryptographically strong random value
        uint randomValue = GetStrongRandomUInt32();

        // Use current timestamp (milliseconds since Unix epoch)
        uint timestamp = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0xFFFFFFFF);

        // Combine the random value and timestamp with bit-shifting for better distribution
        uint uniqueValue = randomValue ^ ((timestamp << 5) | (timestamp >> 27));

        // Incorporate type ID in the high 8 bits
        uint typeComponent = (uint)type << 24;

        // Combine all components:
        // - High 8 bits: Type ID
        // - Middle 16 bits: Unique value (from random + timestamp mix)
        // - Low 8 bits: Machine ID
        return new Base32Id(
            typeComponent |                // Type in high 8 bits
            (uniqueValue & 0x00FFFF00) |   // Unique value in middle 16 bits
            (uint)(machineId & 0xFFFF)     // Machine ID in low 16 bits
        );
    }

    /// <summary>
    /// Converts the ID to a string representation.
    /// </summary>
    /// <param name="isHex">If true, returns an 8-digit hexadecimal string; otherwise, returns a Base32 string.</param>
    /// <returns>The string representation of the ID.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(bool isHex = false)
    {
        if (isHex)
            return _value.ToString("X8");

        return ToBase32String();
    }

    /// <summary>
    /// Returns the default string representation (Base32).
    /// </summary>
    public override string ToString() => ToBase32String();

    /// <summary>
    /// Parses a string representation into a <see cref="Base32Id"/>.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <returns>The parsed <see cref="Base32Id"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if input is empty.</exception>
    /// <exception cref="FormatException">Thrown if input is in an invalid format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base32Id Parse(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        // Check if it's likely a hex string (exactly 8 characters)
        if (input.Length == DefaultEncodings.HexLength)
        {
            // Try to parse as hex first
            if (TryParseHex(input, out uint value))
                return new Base32Id(value);
        }

        // Otherwise parse as Base32
        return ParseBase32(input);
    }

    /// <summary>
    /// Parses a string representation into a <see cref="Base32Id"/>, with explicit format specification.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="isHex">If true, parse as hexadecimal; otherwise, parse as Base32.</param>
    /// <returns>The parsed <see cref="Base32Id"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if input is empty.</exception>
    /// <exception cref="ArgumentException">Thrown if input is in an invalid format.</exception>
    /// <exception cref="FormatException">Thrown if input contains invalid characters.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base32Id Parse(ReadOnlySpan<char> input, bool isHex)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        if (isHex)
        {
            if (input.Length != DefaultEncodings.HexLength)
                throw new ArgumentException(
                    $"Invalid Hex length. Must be {DefaultEncodings.HexLength} characters.", nameof(input));

            // Parse as hex (uint.Parse validates hex digits)
            return new Base32Id(uint.Parse(input, System.Globalization.NumberStyles.HexNumber));
        }

        // Parse as Base32
        return ParseBase32(input);
    }

    /// <summary>
    /// Attempts to parse a string into a <see cref="Base32Id"/>.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed value if successful; otherwise, the default value.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> input, out Base32Id result)
    {
        result = Empty;

        if (input.IsEmpty || input.Length > 8)
            return false;

        // Try to parse as hex first if it's the right length
        if (input.Length == DefaultEncodings.HexLength && TryParseHex(input, out uint hexValue))
        {
            result = new Base32Id(hexValue);
            return true;
        }

        // Otherwise try Base32
        return TryParseBase32(input, out result);
    }

    /// <summary>
    /// Determines whether the current instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>true if the specified object is a <see cref="Base32Id"/> and equals the current instance; otherwise, false.</returns>
    public override bool Equals(object obj) => obj is Base32Id other && Equals(other);

    /// <summary>
    /// Determines whether the current instance is equal to another <see cref="Base32Id"/>.
    /// </summary>
    /// <param name="other">The <see cref="Base32Id"/> to compare with the current instance.</param>
    /// <returns>true if both instances have the same value; otherwise, false.</returns>
    public bool Equals(Base32Id other) => _value == other._value;

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>A hash code for the current <see cref="Base32Id"/>.</returns>
    public override int GetHashCode() => (int)_value;

    /// <summary>
    /// Compares this instance with another <see cref="Base32Id"/>.
    /// </summary>
    /// <param name="other">The <see cref="Base32Id"/> to compare with this instance.</param>
    /// <returns>A value indicating the relative order of the instances.</returns>
    public int CompareTo(Base32Id other) => _value.CompareTo(other._value);

    /// <summary>
    /// Gets a value indicating whether this ID is empty (has a value of 0).
    /// </summary>
    /// <returns>True if this ID is empty; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty() => _value == 0;

    /// <summary>
    /// Creates a Base32Id from its type and machine components plus a random portion.
    /// </summary>
    /// <param name="type">The type identifier.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="randomValue">A custom random value (if not provided, a secure random value will be generated).</param>
    /// <returns>A new Base32Id with the specified components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base32Id FromComponents(IdType type, ushort machineId, uint? randomValue = null)
    {
        if ((int)type >= (int)IdType.Limit)
            throw new ArgumentOutOfRangeException(nameof(type), "IdType exceeds the allowed limit.");

        uint random = randomValue ?? GetStrongRandomUInt32();

        return new Base32Id(
            ((uint)type << 24) |              // Type in high 8 bits
            ((random & 0x00FFFF00) |          // Random value in middle bits
            ((uint)machineId & 0xFFFF))       // Machine ID in low 16 bits
        );
    }

    /// <summary>
    /// Converts the Base32Id to a byte array.
    /// </summary>
    /// <returns>A 4-byte array representing this Base32Id.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToByteArray()
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, _value);
        return bytes;
    }

    /// <summary>
    /// Tries to write the Base32Id to a span of bytes.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
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

    /// <summary>
    /// Creates a Base32Id from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the Base32Id value.</param>
    /// <returns>A Base32Id created from the bytes.</returns>
    /// <exception cref="ArgumentException">Thrown if the byte array is not exactly 4 bytes long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Base32Id FromByteArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 4)
            throw new ArgumentException("Byte array must be exactly 4 bytes long.", nameof(bytes));

        return new Base32Id(BinaryPrimitives.ReadUInt32LittleEndian(bytes));
    }

    /// <summary>
    /// Tries to parse a Base32Id from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the Base32Id value.</param>
    /// <param name="result">The resulting Base32Id if parsing was successful.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    public static bool TryFromByteArray(ReadOnlySpan<byte> bytes, out Base32Id result)
    {
        result = Empty;

        if (bytes.Length != 4)
            return false;

        result = new Base32Id(BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        return true;
    }

    /// <summary>
    /// Creates a new Base32Id with the same Type but a different machine ID.
    /// </summary>
    /// <param name="newMachineId">The new machine ID.</param>
    /// <returns>A new Base32Id with the updated machine ID.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base32Id WithMachineId(ushort newMachineId)
        => new((_value & 0xFFFF0000) | (uint)(newMachineId & 0xFFFF));

    /// <summary>
    /// Creates a new Base32Id with the same machine ID but a different Type.
    /// </summary>
    /// <param name="newType">The new Type.</param>
    /// <returns>A new Base32Id with the updated Type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the new type exceeds the allowed limit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base32Id WithType(IdType newType)
    {
        if ((int)newType >= (int)IdType.Limit)
            throw new ArgumentOutOfRangeException(nameof(newType), "IdType exceeds the allowed limit.");

        return new Base32Id((_value & 0x00FFFFFF) | ((uint)newType << 24));
    }

    /// <summary>
    /// Determines whether one <see cref="Base32Id"/> is less than another.
    /// </summary>
    public static bool operator <(Base32Id left, Base32Id right) => left._value < right._value;

    /// <summary>
    /// Determines whether one <see cref="Base32Id"/> is less than or equal to another.
    /// </summary>
    public static bool operator <=(Base32Id left, Base32Id right) => left._value <= right._value;

    /// <summary>
    /// Determines whether one <see cref="Base32Id"/> is greater than another.
    /// </summary>
    public static bool operator >(Base32Id left, Base32Id right) => left._value > right._value;

    /// <summary>
    /// Determines whether one <see cref="Base32Id"/> is greater than or equal to another.
    /// </summary>
    public static bool operator >=(Base32Id left, Base32Id right) => left._value >= right._value;

    /// <summary>
    /// Determines whether two <see cref="Base32Id"/> instances are equal.
    /// </summary>
    public static bool operator ==(Base32Id left, Base32Id right) => left._value == right._value;

    /// <summary>
    /// Determines whether two <see cref="Base32Id"/> instances are not equal.
    /// </summary>
    public static bool operator !=(Base32Id left, Base32Id right) => left._value != right._value;

    /// <summary>
    /// Implicit conversion from Base32Id to uint.
    /// </summary>
    /// <param name="id">The Base32Id to convert.</param>
    public static implicit operator uint(Base32Id id) => id._value;

    /// <summary>
    /// Explicit conversion from uint to Base32Id.
    /// </summary>
    /// <param name="value">The uint value to convert.</param>
    public static explicit operator Base32Id(uint value) => new(value);

    /// <summary>
    /// Attempts to parse a hexadecimal string into a uint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHex(ReadOnlySpan<char> input, out uint value)
        => uint.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out value);

    /// <summary>
    /// Generates a cryptographically strong random uint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetStrongRandomUInt32()
    {
        // Use Random.Shared which is thread-safe and high-quality
        Span<byte> bytes = stackalloc byte[4];
        Random.Shared.NextBytes(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    /// <summary>
    /// Converts the ID to a Base32 string with minimum padding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ToBase32String()
    {
        // For efficiency, allocate a stack buffer for the maximum possible length
        // Base32 representation of uint.MaxValue is at most 7 characters
        Span<char> buffer = stackalloc char[8];
        int position = buffer.Length;
        uint remaining = _value;

        // Generate digits from right to left
        do
        {
            uint digit = remaining % DefaultEncodings.Base32;
            remaining /= DefaultEncodings.Base32;
            buffer[--position] = DefaultEncodings.Base32Alphabet[(int)digit];
        } while (remaining > 0);

        // Create a new string from the buffer
        return new string(buffer[position..]);
    }

    /// <summary>
    /// Attempts to parse a hexadecimal string into a uint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Base32Id ParseBase32(ReadOnlySpan<char> input)
    {
        if (input.Length > 8)
            throw new ArgumentException("Input is too long", nameof(input));

        uint result = 0;

        foreach (char c in input)
        {
            // Check character validity
            if (c > 127 || CharToValue[c] == byte.MaxValue)
                throw new FormatException($"Invalid character '{c}' in Base32 input");

            // Accumulate value
            byte digitValue = CharToValue[c];
            result = result * DefaultEncodings.Base32 + digitValue;
        }

        return new Base32Id(result);
    }

    /// <summary>
    /// Attempts to parse a Base32 string into a Base32Id.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseBase32(ReadOnlySpan<char> input, out Base32Id result)
    {
        result = Empty;
        uint value = 0;

        foreach (char c in input)
        {
            // Validate character
            if (c > 127 || CharToValue[c] == byte.MaxValue)
                return false;

            // Check for potential overflow
            if (value > (uint.MaxValue / DefaultEncodings.Base32))
                return false;

            byte digitValue = CharToValue[c];
            uint newValue = value * DefaultEncodings.Base32 + digitValue;

            // Check for overflow
            if (newValue < value)
                return false;

            value = newValue;
        }

        result = new Base32Id(value);
        return true;
    }
}
