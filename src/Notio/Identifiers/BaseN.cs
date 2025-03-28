using Notio.Common.Identity;
using Notio.Randomization;
using System;
using System.Buffers.Binary;

namespace Notio.Identifiers;

/// <summary>
/// Provides utility methods for encoding, decoding, and generating BaseN identifiers.
/// Supports Base36, Base58, Base64, and other customizable bases.
/// </summary>
public static class BaseN
{
    /// <summary>
    /// Creates a lookup table for fast character-to-value mapping in BaseN encoding.
    /// </summary>
    /// <param name="alphabet">The character set used for encoding.</param>
    /// <returns>A lookup table mapping characters to their numeric values.</returns>
    public static byte[] CreateCharLookupTable(string alphabet)
    {
        byte[] charToValue = new byte[128];
        for (int i = 0; i < charToValue.Length; i++)
        {
            charToValue[i] = byte.MaxValue;
        }

        for (byte i = 0; i < alphabet.Length; i++)
        {
            char c = alphabet[i];
            charToValue[c] = i;

            if (c >= 'A' && c <= 'Z')
            {
                charToValue[c + 32] = i; // +32 is the difference between uppercase and lowercase ASCII
            }
        }

        return charToValue;
    }

    /// <summary>
    /// Generates a unique identifier based on type, machine ID, timestamp, and randomness.
    /// </summary>
    /// <param name="type">The ID type used to differentiate categories of identifiers.</param>
    /// <param name="machineId">The machine identifier to support distributed environments.</param>
    /// <returns>A 32-bit unique identifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="type"/> exceeds its limit.</exception>
    public static uint GenerateId(IdType type, ushort machineId)
    {
        // Validate type
        if ((int)type >= (int)IdType.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(type), "IdType exceeds the allowed limit.");

        // Get a cryptographically strong random value
        uint randomValue = GenerateSecureRandomUInt();

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
        return (typeComponent) |
               (uniqueValue & 0x00FFFF00) |
               ((uint)(machineId & 0xFFFF));
    }

    /// <summary>
    /// Generates a cryptographically strong random unsigned 32-bit integer.
    /// </summary>
    /// <returns>A securely generated 32-bit unsigned integer.</returns>
    public static uint GenerateSecureRandomUInt()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandGenerator.NextBytes(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    /// <summary>
    /// Encodes a 32-bit unsigned integer into a BaseN string representation.
    /// </summary>
    /// <param name="value">The numeric value to encode.</param>
    /// <param name="alphabet">The character set defining the BaseN encoding.</param>
    /// <param name="baseValue">The numeric base (e.g., 36 for Base36, 58 for Base58).</param>
    /// <param name="maxLength">The maximum possible length of the output string.</param>
    /// <returns>A string representation of the encoded number.</returns>
    public static string EncodeToBaseN(uint value, string alphabet, uint baseValue, int maxLength)
    {
        Span<char> buffer = stackalloc char[maxLength];
        int position = buffer.Length;
        uint remaining = value;

        do
        {
            uint digit = remaining % baseValue;
            remaining /= baseValue;
            buffer[--position] = alphabet[(int)digit];
        } while (remaining > 0);

        return new string(buffer[position..]);
    }

    /// <summary>
    /// Decodes a BaseN string representation into a 32-bit unsigned integer.
    /// </summary>
    /// <param name="input">The BaseN encoded string.</param>
    /// <param name="charToValue">A lookup table mapping characters to numeric values.</param>
    /// <param name="baseValue">The numeric base of the encoding.</param>
    /// <returns>The decoded 32-bit unsigned integer.</returns>
    /// <exception cref="FormatException">Thrown if the input contains invalid characters.</exception>
    public static uint DecodeFromBaseN(ReadOnlySpan<char> input, byte[] charToValue, uint baseValue)
    {
        uint result = 0;

        foreach (char c in input)
        {
            if (c > 127 || charToValue[c] == byte.MaxValue)
                throw new FormatException($"Invalid character '{c}' in input");

            byte digitValue = charToValue[c];
            result = result * baseValue + digitValue;
        }

        return result;
    }

    /// <summary>
    /// Tries to decode a BaseN string representation into a 32-bit unsigned integer.
    /// </summary>
    /// <param name="input">The BaseN encoded string.</param>
    /// <param name="charToValue">A lookup table mapping characters to numeric values.</param>
    /// <param name="baseValue">The numeric base of the encoding.</param>
    /// <param name="result">Outputs the decoded 32-bit unsigned integer if successful.</param>
    /// <returns><see langword="true"/> if decoding succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryDecodeFromBaseN(
        ReadOnlySpan<char> input, byte[] charToValue, uint baseValue, out uint result)
    {
        result = 0;

        foreach (char c in input)
        {
            if (c > 127 || charToValue[c] == byte.MaxValue)
                return false;

            if (result > (uint.MaxValue / baseValue))
                return false;

            byte digitValue = charToValue[c];
            uint newValue = result * baseValue + digitValue;

            if (newValue < result)
                return false;

            result = newValue;
        }

        return true;
    }

    internal static bool TryParseHex(ReadOnlySpan<char> input, out uint value)
            => uint.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out value);
}
