// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Enums;

namespace Nalix.Framework.Identity;

public readonly partial struct Identifier
{
    #region Deserialize Methods

    /// <summary>
    /// Creates a <see cref="Identifier"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Identifier"/> instance.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Identifier Deserialize(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (bytes.Length != 7)
        {
            throw new System.ArgumentException("Input must be exactly 7 bytes.", nameof(bytes));
        }

        System.UInt32 value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes));

        System.UInt16 machineId = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt16>(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes[4..]));

        System.Byte type = bytes[6];

        return new Identifier(value, machineId, (IdentifierType)type);
    }

    /// <summary>
    /// Creates a <see cref="Identifier"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Identifier"/> instance.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Identifier Deserialize(System.Byte[] bytes)
    {
        return bytes == null || bytes.Length != 7
            ? throw new System.ArgumentException("Input must be a non-null array of exactly 7 bytes.", nameof(bytes))
            : Deserialize(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Parses a Base36 string representation of a <see cref="Identifier"/>.
    /// </summary>
    /// <param name="text">The Base36 string to parse.</param>
    /// <returns>A <see cref="Identifier"/> instance equivalent to the Base36 string.</returns>
    /// <exception cref="System.FormatException">
    /// Thrown if the input string is not a valid Base36 representation of a <see cref="Identifier"/>.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static Identifier Deserialize(System.String text)
    {
        return !TryDeserialize(System.MemoryExtensions.AsSpan(text), out Identifier result)
            ? throw new System.FormatException($"Invalid Base36 handle: '{text}'")
            : result;
    }

    /// <summary>
    /// Attempts to parse a Base36 string representation of a <see cref="Identifier"/>.
    /// </summary>
    /// <param name="text">The Base36 string to parse.</param>
    /// <param name="handle">
    /// When this method returns, contains the <see cref="Identifier"/> value equivalent to the Base36 string,
    /// if the conversion succeeded, or the default value if the conversion failed.
    /// </param>
    /// <returns>
    /// <c>true</c> if the string was successfully parsed; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean TryDeserialize(
        System.ReadOnlySpan<System.Char> text, out Identifier handle)
    {
        handle = default;

        if (text.Length is 0 or > 13) // Max length of 7-byte base36
        {
            return false;
        }

        System.UInt64 value = 0;

        for (System.Byte i = 0; i < text.Length; i++)
        {
            System.Char c = text[i];
            System.Int32 digit;

            if (c is >= '0' and <= '9')
            {
                digit = c - '0';
            }
            else if (c is >= 'A' and <= 'Z')
            {
                digit = c - 'A' + 10;
            }
            else if (c is >= 'a' and <= 'z')
            {
                digit = c - 'a' + 10; // allow lowercase
            }
            else
            {
                return false;
            }

            if (digit >= 36)
            {
                return false;
            }

            value = (value * 36) + (System.UInt32)digit;
        }

        if (value > MaxSevenByteValue) // must be <= 7 bytes (56 bits)
        {
            return false;
        }

        // Split to struct layout
        System.UInt32 rawValue = (System.UInt32)(value & 0xFFFFFFFF);
        System.UInt16 machineId = (System.UInt16)((value >> 32) & 0xFFFF);
        System.Byte type = (System.Byte)((value >> 48) & 0xFF);

        handle = new Identifier(rawValue, machineId, (IdentifierType)type);
        return true;
    }

    #endregion Deserialize Methods

    #region Serialization Methods

    /// <summary>
    /// Converts this identifier to a byte array.
    /// </summary>
    /// <returns>A 7-byte array containing the binary representation of this identifier.</returns>
    /// <remarks>
    /// The returned array contains the identifier in little-endian format:
    /// - Bytes 0-3: Value (uint)
    /// - Bytes 4-5: Machine ID (ushort)
    /// - Byte 6: Type (byte)
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Byte[] Serialize()
    {
        System.Byte[] result = new System.Byte[7];
        _ = TrySerialize(result, out _);
        return result;
    }

    /// <summary>
    /// Attempts to format the token as a Base36 string representation into the provided character span.
    /// </summary>
    /// <param name="destination">The span to write the formatted string to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written to the destination.</param>
    /// <returns>
    /// <c>true</c> if the token was successfully formatted; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TrySerialize(
        System.Span<System.Char> destination, out System.Byte charsWritten)
    {
        System.UInt64 value = GetCombinedValue();
        System.Span<System.Char> buffer = stackalloc System.Char[13];
        System.Byte i = 0;

        do
        {
            buffer[i++] = Base36Chars[(System.Byte)(value % 36)];
            value /= 36;
        } while (value > 0);

        if (destination.Length < i)
        {
            charsWritten = 0;
            return false;
        }

        // Copy reversed
        for (System.Byte j = 0; j < i; j++)
        {
            destination[j] = buffer[i - j - 1];
        }

        charsWritten = i;
        return true;
    }

    /// <summary>
    /// Attempts to write the binary representation of this identifier to the specified span.
    /// </summary>
    /// <param name="destination">The span to write the bytes to.</param>
    /// <param name="bytesWritten">
    /// When this method returns, contains the number of bytes written to the destination.
    /// </param>
    /// <returns>
    /// <c>true</c> if the identifier was successfully written; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method requires at least 7 bytes of space in the destination span.
    /// The bytes are written in little-endian format.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TrySerialize(
        System.Span<System.Byte> destination, out System.Int32 bytesWritten)
    {
        if (destination.Length < 7)
        {
            bytesWritten = 0;
            return false;
        }

        // Use unsafe operations for optimal performance
        ref System.Byte destinationRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(destination);

        // Fix: Use MemoryMarshal to treat the Identifier struct as a ReadOnlySpan<byte>
        System.ReadOnlySpan<System.Byte> sourceSpan =
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                ref System.Runtime.CompilerServices.Unsafe.AsRef(in this), 1));

        sourceSpan[..7].CopyTo(destination);

        bytesWritten = 7;
        return true;
    }

    #endregion Serialization Methods

    #region String Representation Methods

    /// <summary>
    /// Returns the Base36 string representation of this identifier.
    /// </summary>
    /// <returns>A Base36 encoded string representing this identifier.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString() => ToBase36String();

    /// <summary>
    /// Converts this identifier to its Base36 string representation.
    /// </summary>
    /// <returns>A Base36 encoded string representing this identifier.</returns>
    /// <remarks>
    /// Base36 encoding uses digits 0-9 and letters A-Z, providing a compact
    /// and URL-safe string representation.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String ToBase36String()
    {
        System.UInt64 combinedValue = GetCombinedValue();
        return EncodeToBase36(combinedValue);
    }

    /// <summary>
    /// Converts this identifier to its hexadecimal string representation.
    /// </summary>
    /// <returns>A hexadecimal string representing this identifier.</returns>
    /// <remarks>
    /// The hexadecimal representation shows the raw byte values of the identifier.
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String ToHexString()
    {
        System.Span<System.Byte> buffer = stackalloc System.Byte[7];
        _ = TrySerialize(buffer, out _);
        return System.Convert.ToHexString(buffer);
    }

    /// <summary>
    /// Converts this identifier to a string representation using the specified format.
    /// </summary>
    /// <param name="useHexFormat">
    /// <c>true</c> to use hexadecimal format; <c>false</c> to use Base36 format.
    /// </param>
    /// <returns>A string representation of this identifier in the specified format.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.String ToString(System.Boolean useHexFormat)
        => useHexFormat ? ToHexString() : ToBase36String();

    #endregion String Representation Methods
}
