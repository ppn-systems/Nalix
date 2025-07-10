using Nalix.Common.Identity;
using Nalix.Framework.Randomization;

namespace Nalix.Framework.Identity;

/// <summary>
/// Represents a compact, high-performance identifier that encodes a 32-bit value,
/// 16-bit machine ID, and 8-bit type into a 7-byte structure.
/// This struct is optimized for use as dictionary keys and provides efficient
/// serialization capabilities.
/// </summary>
/// <remarks>
/// The Token uses explicit layout to ensure consistent memory representation
/// across different platforms and provides both hexadecimal and Base36 string representations.
///
/// Memory layout:
/// - Bytes 0-3: Value (uint, little-endian)
/// - Bytes 4-5: Machine ID (ushort, little-endian)
/// - Byte 6: Token type (byte)
/// </remarks>
[System.Runtime.InteropServices.StructLayout(
    System.Runtime.InteropServices.LayoutKind.Explicit, Size = 7)]
public readonly struct Token : IIdentifier, System.IEquatable<Token>
{
    #region Private Fields

    /// <summary>
    /// The main identifier value (32-bit unsigned integer).
    /// </summary>
    [System.Runtime.InteropServices.FieldOffset(0)]
    private readonly System.UInt32 _value;

    /// <summary>
    /// The machine identifier (16-bit unsigned integer).
    /// </summary>
    [System.Runtime.InteropServices.FieldOffset(4)]
    private readonly System.UInt16 _machineId;

    /// <summary>
    /// The identifier type (8-bit unsigned integer).
    /// </summary>
    [System.Runtime.InteropServices.FieldOffset(6)]
    private readonly System.Byte _type;

    #endregion Private Fields

    #region Public Properties

    /// <summary>
    /// Gets the main identifier value.
    /// </summary>
    /// <value>A 32-bit unsigned integer representing the core identifier.</value>
    public System.UInt32 Value => _value;

    /// <summary>
    /// Gets the machine identifier.
    /// </summary>
    /// <value>A 16-bit unsigned integer representing the originating machine.</value>
    public System.UInt16 MachineId => _machineId;

    /// <summary>
    /// Gets the identifier type.
    /// </summary>
    /// <value>An enum value representing the type of this identifier.</value>
    public TokenType Type => (TokenType)_type;

    #endregion Public Properties

    #region Constructors and Factory Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    private Token(System.UInt32 value, System.UInt16 machineId, TokenType type)
    {
        _value = value;
        _machineId = machineId;
        _type = (System.Byte)type;
    }

    /// <summary>
    /// Creates a new <see cref="Token"/> with the specified components.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    /// <returns>A new <see cref="Token"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Token.NewId(12345, 1001, TokenType.User);
    /// Console.WriteLine(id.ToBase36String()); // Outputs Base36 representation
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Token NewId(System.UInt32 value, System.UInt16 machineId, TokenType type)
        => new(value, machineId, type);

    /// <summary>
    /// Creates a new <see cref="Token"/> with the specified components.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <returns>A new <see cref="Token"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Token.NewId(TokenType.System);
    /// Console.WriteLine(id.ToBase36String()); // Outputs Base36 representation
    /// </code>
    /// </example>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Token NewId(TokenType type, System.UInt16 machineId = 1)
        => new(RandGenerator.NextUInt32(), machineId, type);

    /// <summary>
    /// Creates an empty <see cref="Token"/> with all components set to zero.
    /// </summary>
    /// <returns>An empty <see cref="Token"/> instance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Token CreateEmpty()
        => new(0, 0, 0);

    #endregion Constructors and Factory Methods

    #region State Checking Methods

    /// <summary>
    /// Determines whether this identifier is empty (all components are zero).
    /// </summary>
    /// <returns>
    /// <c>true</c> if all components (value, machine ID, and type) are zero; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsEmpty() => (_value | _machineId | _type) == 0;

    /// <summary>
    /// Determines whether this identifier is valid (not empty).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the identifier is not empty; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean IsValid() => !IsEmpty();

    #endregion State Checking Methods

    #region String Representation Methods

    /// <summary>
    /// Returns the Base36 string representation of this identifier.
    /// </summary>
    /// <returns>A Base36 encoded string representing this identifier.</returns>
    public override System.String ToString() => ToBase36String();

    /// <summary>
    /// Converts this identifier to its Base36 string representation.
    /// </summary>
    /// <returns>A Base36 encoded string representing this identifier.</returns>
    /// <remarks>
    /// Base36 encoding uses digits 0-9 and letters A-Z, providing a compact
    /// and URL-safe string representation.
    /// </remarks>
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
    public System.String ToHexString()
    {
        System.Span<System.Byte> buffer = stackalloc System.Byte[7];
        TryWriteBytes(buffer, out _);
        return System.Convert.ToHexString(buffer);
    }

    /// <summary>
    /// Converts this identifier to a string representation using the specified format.
    /// </summary>
    /// <param name="useHexFormat">
    /// <c>true</c> to use hexadecimal format; <c>false</c> to use Base36 format.
    /// </param>
    /// <returns>A string representation of this identifier in the specified format.</returns>
    public System.String ToString(bool useHexFormat)
        => useHexFormat ? ToHexString() : ToBase36String();

    #endregion String Representation Methods

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
    public System.Byte[] ToByteArray()
    {
        System.Byte[] result = new System.Byte[7];
        TryWriteBytes(result, out _);
        return result;
    }

    /// <summary>
    /// Creates a <see cref="Token"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Token"/> instance.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Token FromByteArray(System.ReadOnlySpan<System.Byte> bytes)
    {
        if (bytes.Length != 7)
            throw new System.ArgumentException("Input must be exactly 7 bytes.", nameof(bytes));

        System.UInt32 value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt32>(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes));

        System.UInt16 machineId = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<System.UInt16>(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(bytes[4..]));

        byte type = bytes[6];

        return new Token(value, machineId, (TokenType)type);
    }

    /// <summary>
    /// Creates a <see cref="Token"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Token"/> instance.</returns>
    /// <exception cref="System.ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Token FromByteArray(System.Byte[] bytes)
    {
        if (bytes == null || bytes.Length != 7)
            throw new System.ArgumentException("Input must be a non-null array of exactly 7 bytes.", nameof(bytes));

        return FromByteArray(System.MemoryExtensions.AsSpan(bytes));
    }

    /// <summary>
    /// Parses a Base36 string representation of a <see cref="Token"/>.
    /// </summary>
    /// <param name="text">The Base36 string to parse.</param>
    /// <returns>A <see cref="Token"/> instance equivalent to the Base36 string.</returns>
    /// <exception cref="System.FormatException">
    /// Thrown if the input string is not a valid Base36 representation of a <see cref="Token"/>.
    /// </exception>
    public static Token ParseBase36(System.String text)
    {
        if (!TryParseBase36(System.MemoryExtensions.AsSpan(text), out Token result))
            throw new System.FormatException($"Invalid Base36 handle: '{text}'");

        return result;
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryWriteBytes(System.Span<System.Byte> destination, out System.Int32 bytesWritten)
    {
        if (destination.Length < 7)
        {
            bytesWritten = 0;
            return false;
        }

        // Use unsafe operations for optimal performance
        ref byte destinationRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(destination);

        // Fix: Use MemoryMarshal to treat the Token struct as a ReadOnlySpan<byte>
        System.ReadOnlySpan<System.Byte> sourceSpan =
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                ref System.Runtime.CompilerServices.Unsafe.AsRef(in this), 1));

        sourceSpan[..7].CopyTo(destination);

        bytesWritten = 7;
        return true;
    }

    /// <summary>
    /// Attempts to parse a Base36 string representation of a <see cref="Token"/>.
    /// </summary>
    /// <param name="text">The Base36 string to parse.</param>
    /// <param name="handle">
    /// When this method returns, contains the <see cref="Token"/> value equivalent to the Base36 string,
    /// if the conversion succeeded, or the default value if the conversion failed.
    /// </param>
    /// <returns>
    /// <c>true</c> if the string was successfully parsed; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool TryParseBase36(System.ReadOnlySpan<System.Char> text, out Token handle)
    {
        handle = default;

        if (text.Length == 0 || text.Length > 13) // Max length of 7-byte base36
            return false;

        System.UInt64 value = 0;

        for (System.Byte i = 0; i < text.Length; i++)
        {
            System.Char c = text[i];
            System.Int32 digit;

            if (c >= '0' && c <= '9') digit = c - '0';
            else if (c >= 'A' && c <= 'Z') digit = c - 'A' + 10;
            else if (c >= 'a' && c <= 'z') digit = c - 'a' + 10; // allow lowercase
            else return false;

            if (digit >= 36) return false;

            value = value * 36 + (System.UInt32)digit;
        }

        if (value > 0x00FFFFFFFFFFFFFFUL) // must be <= 7 bytes (56 bits)
            return false;

        // Split to struct layout
        System.UInt32 rawValue = (System.UInt32)(value & 0xFFFFFFFF);
        System.UInt16 machineId = (System.UInt16)(value >> 32 & 0xFFFF);
        System.Byte type = (System.Byte)(value >> 48 & 0xFF);

        handle = new Token(rawValue, machineId, (TokenType)type);
        return true;
    }

    #endregion Serialization Methods

    #region Equality and Comparison Methods

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="IIdentifier"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(IIdentifier? other)
        => other is Token compactId && Equals(compactId);

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="Token"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Equals(Token other)
    {
        // Optimize comparison by treating the struct as a single 64-bit value
        ulong thisValue = GetCombinedValue();
        ulong otherValue = other.GetCombinedValue();
        return thisValue == otherValue;
    }

    /// <summary>
    /// Determines whether this identifier is equal to the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the object is a <see cref="Token"/> and is equal to this instance;
    /// otherwise, <c>false</c>.
    /// </returns>
    public override System.Boolean Equals(System.Object? obj)
        => obj is Token other && Equals(other);

    /// <summary>
    /// Returns the hash code for this identifier.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    /// <remarks>
    /// The hash code is computed efficiently using all components of the identifier
    /// and is suitable for use in hash-based collections like <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.
    /// </remarks>
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
    /// Determines whether two <see cref="Token"/> instances are equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator ==(Token left, Token right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Token"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are not equal; otherwise, <c>false</c>.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean operator !=(Token left, Token right) => !left.Equals(right);

    #endregion Operators

    #region Private Helper Methods

    /// <summary>
    /// Gets the combined 64-bit representation of this identifier.
    /// </summary>
    /// <returns>A 64-bit unsigned integer containing all identifier components.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private System.UInt64 GetCombinedValue()
    {
        // Mask to ensure we only use the lower 56 bits (7 bytes)
        return System.Runtime.CompilerServices.Unsafe.As<Token, System.UInt64>(
            ref System.Runtime.CompilerServices.Unsafe.AsRef(in this)) & 0x00FFFFFFFFFFFFFF;
    }

    /// <summary>
    /// Encodes a 64-bit unsigned integer to Base36 string representation.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>A Base36 encoded string.</returns>
    private static System.String EncodeToBase36(System.UInt64 value)
    {
        if (value == 0) return "0";

        const System.String base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        System.Span<System.Char> buffer = stackalloc System.Char[13]; // Maximum length for 64-bit Base36
        System.Byte charIndex = 0;

        do
        {
            buffer[charIndex++] = base36Chars[(System.Byte)(value % 36)];
            value /= 36;
        } while (value > 0);

        // Reverse the buffer to get the correct order
        System.Span<System.Char> result = buffer[..charIndex];
        System.MemoryExtensions.Reverse(result);
        return new System.String(result);
    }

    #endregion Private Helper Methods
}