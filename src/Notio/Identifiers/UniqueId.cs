using Notio.Common.Data;
using Notio.Common.Enums;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Notio.Identifiers;

/// <summary>
/// Represents a high-performance, space-efficient unique identifier that supports both Base36 and hexadecimal representations.
/// </summary>
/// <remarks>
/// This implementation provides fast conversion between numeric and string representations,
/// with optimized memory usage and performance characteristics.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="UniqueId"/> struct with the specified value.
/// </remarks>
/// <param name="value">The 32-bit unsigned integer value.</param>
public readonly struct UniqueId(uint value) : IUniqueId, IEquatable<UniqueId>, IComparable<UniqueId>
{
    /// <summary>
    /// Lookup table for converting characters to their Base36 values.
    /// </summary>
    private static readonly byte[] CharToValue;

    /// <summary>
    /// The underlying 32-bit value.
    /// </summary>
    private readonly uint _value = value;

    /// <summary>
    /// Empty/default instance with a value of 0.
    /// </summary>
    public static readonly UniqueId Empty = new(0);

    /// <summary>
    /// Static constructor to initialize the character lookup table.
    /// </summary>
    static UniqueId()
    {
        // Initialize lookup table with 'invalid' marker
        CharToValue = new byte[128];
        for (int i = 0; i < CharToValue.Length; i++)
        {
            CharToValue[i] = byte.MaxValue;
        }

        // Populate lookup table for valid Base36 characters
        for (byte i = 0; i < UniqueIdConstants.Alphabet.Length; i++)
        {
            char c = UniqueIdConstants.Alphabet[i];
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
    /// Gets the TypeId encoded within this UniqueId.
    /// </summary>
    public TypeId Type => (TypeId)(_value >> 24);

    /// <summary>
    /// Gets the machine ID component encoded within this UniqueId.
    /// </summary>
    public ushort MachineId => (ushort)(_value & 0xFFFF);

    /// <summary>
    /// Generate a new ID from random and system elements.
    /// </summary>
    /// <param name="type">The unique ID type to generate.</param>
    /// <param name="machineId">The unique ID for each different server.</param>
    /// <returns>A new <see cref="UniqueId"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if type exceeds the allowed limit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UniqueId NewId(TypeId type = TypeId.Generic, ushort machineId = 0)
    {
        // Validate type
        if ((int)type >= (int)TypeId.Limit)
            throw new ArgumentOutOfRangeException(nameof(type), "TypeId exceeds the allowed limit.");

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
        return new UniqueId(
            typeComponent |                // Type in high 8 bits
            (uniqueValue & 0x00FFFF00) |   // Unique value in middle 16 bits
            (uint)(machineId & 0xFFFF)     // Machine ID in low 16 bits
        );
    }

    /// <summary>
    /// Converts the ID to a string representation.
    /// </summary>
    /// <param name="isHex">If true, returns an 8-digit hexadecimal string; otherwise, returns a Base36 string.</param>
    /// <returns>The string representation of the ID.</returns>
    public string ToString(bool isHex = false)
    {
        if (isHex)
            return _value.ToString("X8");

        return ToBase36String();
    }

    /// <summary>
    /// Returns the default string representation (Base36).
    /// </summary>
    public override string ToString() => ToBase36String();

    /// <summary>
    /// Parses a string representation into a <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <returns>The parsed <see cref="UniqueId"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if input is empty.</exception>
    /// <exception cref="FormatException">Thrown if input is in an invalid format.</exception>
    public static UniqueId Parse(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        // Check if it's likely a hex string (exactly 8 characters)
        if (input.Length == UniqueIdConstants.HexLength)
        {
            // Try to parse as hex first
            if (TryParseHex(input, out uint value))
                return new UniqueId(value);
        }

        // Otherwise parse as Base36
        return ParseBase36(input);
    }

    /// <summary>
    /// Parses a string representation into a <see cref="UniqueId"/>, with explicit format specification.
    /// </summary>
    /// <param name="input">The string to parse.</param>
    /// <param name="isHex">If true, parse as hexadecimal; otherwise, parse as Base36.</param>
    /// <returns>The parsed <see cref="UniqueId"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if input is empty.</exception>
    /// <exception cref="ArgumentException">Thrown if input is in an invalid format.</exception>
    /// <exception cref="FormatException">Thrown if input contains invalid characters.</exception>
    public static UniqueId Parse(ReadOnlySpan<char> input, bool isHex)
    {
        if (input.IsEmpty)
            throw new ArgumentNullException(nameof(input));

        if (isHex)
        {
            if (input.Length != UniqueIdConstants.HexLength)
                throw new ArgumentException(
                    $"Invalid Hex length. Must be {UniqueIdConstants.HexLength} characters.", nameof(input));

            // Parse as hex (uint.Parse validates hex digits)
            return new UniqueId(uint.Parse(input, System.Globalization.NumberStyles.HexNumber));
        }

        // Parse as Base36
        return ParseBase36(input);
    }

    /// <summary>
    /// Attempts to parse a string into a <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed value if successful; otherwise, the default value.</param>
    /// <returns>true if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, out UniqueId result)
    {
        result = Empty;

        if (input.IsEmpty || input.Length > 13)
            return false;

        // Try to parse as hex first if it's the right length
        if (input.Length == UniqueIdConstants.HexLength && TryParseHex(input, out uint hexValue))
        {
            result = new UniqueId(hexValue);
            return true;
        }

        // Otherwise try Base36
        return TryParseBase36(input, out result);
    }

    /// <summary>
    /// Determines whether the current instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>true if the specified object is a <see cref="UniqueId"/> and equals the current instance; otherwise, false.</returns>
    public override bool Equals(object obj) => obj is UniqueId other && Equals(other);

    /// <summary>
    /// Determines whether the current instance is equal to another <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="other">The <see cref="UniqueId"/> to compare with the current instance.</param>
    /// <returns>true if both instances have the same value; otherwise, false.</returns>
    public bool Equals(UniqueId other) => _value == other._value;

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>A hash code for the current <see cref="UniqueId"/>.</returns>
    public override int GetHashCode() => (int)_value;

    /// <summary>
    /// Compares this instance with another <see cref="UniqueId"/>.
    /// </summary>
    /// <param name="other">The <see cref="UniqueId"/> to compare with this instance.</param>
    /// <returns>A value indicating the relative order of the instances.</returns>
    public int CompareTo(UniqueId other) => _value.CompareTo(other._value);

    /// <summary>
    /// Gets a value indicating whether this ID is empty (has a value of 0).
    /// </summary>
    /// <returns>True if this ID is empty; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty() => _value == 0;

    /// <summary>
    /// Creates a UniqueId from its type and machine components plus a random portion.
    /// </summary>
    /// <param name="type">The type identifier.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="randomValue">A custom random value (if not provided, a secure random value will be generated).</param>
    /// <returns>A new UniqueId with the specified components.</returns>
    public static UniqueId FromComponents(TypeId type, ushort machineId, uint? randomValue = null)
    {
        if ((int)type >= (int)TypeId.Limit)
            throw new ArgumentOutOfRangeException(nameof(type), "TypeId exceeds the allowed limit.");

        uint random = randomValue ?? GetStrongRandomUInt32();

        return new UniqueId(
            ((uint)type << 24) |              // Type in high 8 bits
            ((random & 0x00FFFF00) |          // Random value in middle bits
            ((uint)machineId & 0xFFFF))       // Machine ID in low 16 bits
        );
    }

    /// <summary>
    /// Converts the UniqueId to a byte array.
    /// </summary>
    /// <returns>A 4-byte array representing this UniqueId.</returns>
    public byte[] ToByteArray()
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, _value);
        return bytes;
    }

    /// <summary>
    /// Tries to write the UniqueId to a span of bytes.
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
    /// Creates a UniqueId from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the UniqueId value.</param>
    /// <returns>A UniqueId created from the bytes.</returns>
    /// <exception cref="ArgumentException">Thrown if the byte array is not exactly 4 bytes long.</exception>
    public static UniqueId FromByteArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 4)
            throw new ArgumentException("Byte array must be exactly 4 bytes long.", nameof(bytes));

        return new UniqueId(BinaryPrimitives.ReadUInt32LittleEndian(bytes));
    }

    /// <summary>
    /// Tries to parse a UniqueId from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the UniqueId value.</param>
    /// <param name="result">The resulting UniqueId if parsing was successful.</param>
    /// <returns>True if parsing was successful; otherwise, false.</returns>
    public static bool TryFromByteArray(ReadOnlySpan<byte> bytes, out UniqueId result)
    {
        result = Empty;

        if (bytes.Length != 4)
            return false;

        result = new UniqueId(BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        return true;
    }

    /// <summary>
    /// Creates a new UniqueId with the same Type but a different machine ID.
    /// </summary>
    /// <param name="newMachineId">The new machine ID.</param>
    /// <returns>A new UniqueId with the updated machine ID.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UniqueId WithMachineId(ushort newMachineId)
        => new((_value & 0xFFFF0000) | (uint)(newMachineId & 0xFFFF));

    /// <summary>
    /// Creates a new UniqueId with the same machine ID but a different Type.
    /// </summary>
    /// <param name="newType">The new Type.</param>
    /// <returns>A new UniqueId with the updated Type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the new type exceeds the allowed limit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UniqueId WithType(TypeId newType)
    {
        if ((int)newType >= (int)TypeId.Limit)
            throw new ArgumentOutOfRangeException(nameof(newType), "TypeId exceeds the allowed limit.");

        return new UniqueId((_value & 0x00FFFFFF) | ((uint)newType << 24));
    }

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is less than another.
    /// </summary>
    public static bool operator <(UniqueId left, UniqueId right) => left._value < right._value;

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is less than or equal to another.
    /// </summary>
    public static bool operator <=(UniqueId left, UniqueId right) => left._value <= right._value;

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is greater than another.
    /// </summary>
    public static bool operator >(UniqueId left, UniqueId right) => left._value > right._value;

    /// <summary>
    /// Determines whether one <see cref="UniqueId"/> is greater than or equal to another.
    /// </summary>
    public static bool operator >=(UniqueId left, UniqueId right) => left._value >= right._value;

    /// <summary>
    /// Determines whether two <see cref="UniqueId"/> instances are equal.
    /// </summary>
    public static bool operator ==(UniqueId left, UniqueId right) => left._value == right._value;

    /// <summary>
    /// Determines whether two <see cref="UniqueId"/> instances are not equal.
    /// </summary>
    public static bool operator !=(UniqueId left, UniqueId right) => left._value != right._value;

    /// <summary>
    /// Implicit conversion from UniqueId to uint.
    /// </summary>
    /// <param name="id">The UniqueId to convert.</param>
    public static implicit operator uint(UniqueId id) => id._value;

    /// <summary>
    /// Explicit conversion from uint to UniqueId.
    /// </summary>
    /// <param name="value">The uint value to convert.</param>
    public static explicit operator UniqueId(uint value) => new(value);

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
    /// Converts the ID to a Base36 string with minimum padding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ToBase36String()
    {
        // For efficiency, allocate a stack buffer for the maximum possible length
        // Base36 representation of uint.MaxValue is at most 7 characters
        Span<char> buffer = stackalloc char[13];
        int position = buffer.Length;
        uint remaining = _value;

        // Generate digits from right to left
        do
        {
            uint digit = remaining % UniqueIdConstants.Base;
            remaining /= UniqueIdConstants.Base;
            buffer[--position] = UniqueIdConstants.Alphabet[(int)digit];
        } while (remaining > 0);

        // Apply padding to minimum length if necessary
        int actualLength = buffer.Length - position;
        int finalLength = Math.Max(actualLength, UniqueIdConstants.MinBase36Length);

        // Create a new string with proper padding
        return new string('0', finalLength - actualLength) + new string(buffer[position..]);
    }


    /// <summary>
    /// Attempts to parse a hexadecimal string into a uint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseHex(ReadOnlySpan<char> input, out uint value)
        => uint.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out value);

    /// <summary>
    /// Parses a Base36 string into a UniqueId.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UniqueId ParseBase36(ReadOnlySpan<char> input)
    {
        if (input.Length > 13)
            throw new ArgumentException("Input is too long", nameof(input));

        uint result = 0;

        foreach (char c in input)
        {
            // Check character validity
            if (c > 127 || CharToValue[c] == byte.MaxValue)
                throw new FormatException($"Invalid character '{c}' in Base36 input");

            // Accumulate value
            byte digitValue = CharToValue[c];
            result = result * UniqueIdConstants.Base + digitValue;
        }

        return new UniqueId(result);
    }

    /// <summary>
    /// Attempts to parse a Base36 string into a UniqueId.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseBase36(ReadOnlySpan<char> input, out UniqueId result)
    {
        result = Empty;
        uint value = 0;

        foreach (char c in input)
        {
            // Validate character
            if (c > 127 || CharToValue[c] == byte.MaxValue)
                return false;

            // Check for potential overflow
            if (value > (uint.MaxValue / UniqueIdConstants.Base))
                return false;

            byte digitValue = CharToValue[c];
            uint newValue = value * UniqueIdConstants.Base + digitValue;

            // Check for overflow
            if (newValue < value)
                return false;

            value = newValue;
        }

        result = new UniqueId(value);
        return true;
    }
}
