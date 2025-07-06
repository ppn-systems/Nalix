using Nalix.Common.Identity;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.Shared.Identifiers;

/// <summary>
/// Represents a compact, high-performance identifier that encodes a 32-bit value,
/// 16-bit machine ID, and 8-bit type into a 7-byte structure.
/// This struct is optimized for use as dictionary keys and provides efficient
/// serialization capabilities.
/// </summary>
/// <remarks>
/// The Handle uses explicit layout to ensure consistent memory representation
/// across different platforms and provides both hexadecimal and Base36 string representations.
///
/// Memory layout:
/// - Bytes 0-3: Value (uint, little-endian)
/// - Bytes 4-5: Machine ID (ushort, little-endian)
/// - Byte 6: Handle type (byte)
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 7)]
public readonly struct Handle : IIdentifier, IEquatable<Handle>
{
    #region Private Fields

    /// <summary>
    /// The main identifier value (32-bit unsigned integer).
    /// </summary>
    [FieldOffset(0)]
    private readonly uint _value;

    /// <summary>
    /// The machine identifier (16-bit unsigned integer).
    /// </summary>
    [FieldOffset(4)]
    private readonly ushort _machineId;

    /// <summary>
    /// The identifier type (8-bit unsigned integer).
    /// </summary>
    [FieldOffset(6)]
    private readonly byte _type;

    #endregion Private Fields

    #region Public Properties

    /// <summary>
    /// Gets the main identifier value.
    /// </summary>
    /// <value>A 32-bit unsigned integer representing the core identifier.</value>
    public uint Value => _value;

    /// <summary>
    /// Gets the machine identifier.
    /// </summary>
    /// <value>A 16-bit unsigned integer representing the originating machine.</value>
    public ushort MachineId => _machineId;

    /// <summary>
    /// Gets the identifier type.
    /// </summary>
    /// <value>An enum value representing the type of this identifier.</value>
    public HandleType Type => (HandleType)_type;

    #endregion Public Properties

    #region Constructors and Factory Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="Handle"/> struct.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    private Handle(uint value, ushort machineId, HandleType type)
    {
        _value = value;
        _machineId = machineId;
        _type = (byte)type;
    }

    /// <summary>
    /// Creates a new <see cref="Handle"/> with the specified components.
    /// </summary>
    /// <param name="value">The main identifier value.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="type">The identifier type.</param>
    /// <returns>A new <see cref="Handle"/> instance.</returns>
    /// <example>
    /// <code>
    /// var id = Handle.CreateNew(12345, 1001, HandleType.User);
    /// Console.WriteLine(id.ToBase36String()); // Outputs Base36 representation
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Handle CreateNew(uint value, ushort machineId, HandleType type)
        => new(value, machineId, type);

    /// <summary>
    /// Creates an empty <see cref="Handle"/> with all components set to zero.
    /// </summary>
    /// <returns>An empty <see cref="Handle"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Handle CreateEmpty()
        => new(0, 0, 0);

    #endregion Constructors and Factory Methods

    #region State Checking Methods

    /// <summary>
    /// Determines whether this identifier is empty (all components are zero).
    /// </summary>
    /// <returns>
    /// <c>true</c> if all components (value, machine ID, and type) are zero; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty() => (_value | _machineId | _type) == 0;

    /// <summary>
    /// Determines whether this identifier is valid (not empty).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the identifier is not empty; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid() => !IsEmpty();

    #endregion State Checking Methods

    #region String Representation Methods

    /// <summary>
    /// Returns the Base36 string representation of this identifier.
    /// </summary>
    /// <returns>A Base36 encoded string representing this identifier.</returns>
    public override string ToString() => ToBase36String();

    /// <summary>
    /// Converts this identifier to its Base36 string representation.
    /// </summary>
    /// <returns>A Base36 encoded string representing this identifier.</returns>
    /// <remarks>
    /// Base36 encoding uses digits 0-9 and letters A-Z, providing a compact
    /// and URL-safe string representation.
    /// </remarks>
    public string ToBase36String()
    {
        ulong combinedValue = GetCombinedValue();
        return EncodeToBase36(combinedValue);
    }

    /// <summary>
    /// Converts this identifier to its hexadecimal string representation.
    /// </summary>
    /// <returns>A hexadecimal string representing this identifier.</returns>
    /// <remarks>
    /// The hexadecimal representation shows the raw byte values of the identifier.
    /// </remarks>
    public string ToHexString()
    {
        Span<byte> buffer = stackalloc byte[7];
        TryWriteBytes(buffer, out _);
        return Convert.ToHexString(buffer);
    }

    /// <summary>
    /// Converts this identifier to a string representation using the specified format.
    /// </summary>
    /// <param name="useHexFormat">
    /// <c>true</c> to use hexadecimal format; <c>false</c> to use Base36 format.
    /// </param>
    /// <returns>A string representation of this identifier in the specified format.</returns>
    public string ToString(bool useHexFormat)
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
    public byte[] ToByteArray()
    {
        byte[] result = new byte[7];
        TryWriteBytes(result, out _);
        return result;
    }

    /// <summary>
    /// Creates a <see cref="Handle"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Handle"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Handle FromByteArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 7)
            throw new ArgumentException("Input must be exactly 7 bytes.", nameof(bytes));

        uint value = Unsafe.ReadUnaligned<uint>(ref MemoryMarshal.GetReference(bytes));
        ushort machineId = Unsafe.ReadUnaligned<ushort>(ref MemoryMarshal.GetReference(bytes[4..]));
        byte type = bytes[6];

        return new Handle(value, machineId, (HandleType)type);
    }

    /// <summary>
    /// Creates a <see cref="Handle"/> from a 7-byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the identifier data.</param>
    /// <returns>A reconstructed <see cref="Handle"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the input array is null or not exactly 7 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Handle FromByteArray(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 7)
            throw new ArgumentException("Input must be a non-null array of exactly 7 bytes.", nameof(bytes));

        return FromByteArray(bytes.AsSpan());
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWriteBytes(Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < 7)
        {
            bytesWritten = 0;
            return false;
        }

        // Use unsafe operations for optimal performance
        ref byte destinationRef = ref MemoryMarshal.GetReference(destination);

        // Fix: Use MemoryMarshal to treat the Handle struct as a ReadOnlySpan<byte>
        ReadOnlySpan<byte> sourceSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this), 1));
        sourceSpan[..7].CopyTo(destination);

        bytesWritten = 7;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(IIdentifier? other)
        => other is Handle compactId && Equals(compactId);

    /// <summary>
    /// Determines whether this identifier is equal to another <see cref="Handle"/>.
    /// </summary>
    /// <param name="other">The identifier to compare with this instance.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Handle other)
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
    /// <c>true</c> if the object is a <see cref="Handle"/> and is equal to this instance;
    /// otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
        => obj is Handle other && Equals(other);

    /// <summary>
    /// Returns the hash code for this identifier.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    /// <remarks>
    /// The hash code is computed efficiently using all components of the identifier
    /// and is suitable for use in hash-based collections like <see cref="Dictionary{TKey,TValue}"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        // Use the combined value for consistent hashing
        ulong combinedValue = GetCombinedValue();
        return combinedValue.GetHashCode();
    }

    #endregion Equality and Comparison Methods

    #region Operators

    /// <summary>
    /// Determines whether two <see cref="Handle"/> instances are equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are equal; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Handle left, Handle right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Handle"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first identifier to compare.</param>
    /// <param name="right">The second identifier to compare.</param>
    /// <returns>
    /// <c>true</c> if the identifiers are not equal; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Handle left, Handle right)
        => !left.Equals(right);

    #endregion Operators

    #region Private Helper Methods

    /// <summary>
    /// Gets the combined 64-bit representation of this identifier.
    /// </summary>
    /// <returns>A 64-bit unsigned integer containing all identifier components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong GetCombinedValue()
    {
        // Mask to ensure we only use the lower 56 bits (7 bytes)
        return Unsafe.As<Handle, ulong>(ref Unsafe.AsRef(in this)) & 0x00FFFFFFFFFFFFFF;
    }

    /// <summary>
    /// Encodes a 64-bit unsigned integer to Base36 string representation.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>A Base36 encoded string.</returns>
    private static string EncodeToBase36(ulong value)
    {
        if (value == 0) return "0";

        const string base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Span<char> buffer = stackalloc char[13]; // Maximum length for 64-bit Base36
        int charIndex = 0;

        do
        {
            buffer[charIndex++] = base36Chars[(int)(value % 36)];
            value /= 36;
        } while (value > 0);

        // Reverse the buffer to get the correct order
        Span<char> result = buffer[..charIndex];
        result.Reverse();
        return new string(result);
    }

    #endregion Private Helper Methods
}