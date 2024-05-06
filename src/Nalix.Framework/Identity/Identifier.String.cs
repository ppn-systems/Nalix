// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Identity;

public readonly partial struct Identifier
{
    /// <summary>
    /// Returns the Base36 string representation of this identifier.
    /// </summary>
    /// <returns>A Base36 encoded string representing this identifier.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString() => ToBase36();

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
    public System.String ToBase36()
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
    public System.String ToHex()
    {
        System.Span<System.Byte> buffer = stackalloc System.Byte[7];
        _ = TryWriteBytes(buffer, out _);
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
        => useHexFormat ? ToHex() : ToBase36();

    /// <summary>
    /// Converts this identifier to a string representation using the specified format string.
    /// Supported formats:
    /// "X" or "x" → hexadecimal string (14 hex chars for 7 bytes).
    /// "B" or "b" → Base36 string (default).
    /// null or empty → Base36.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.String ToString(System.String? format, System.IFormatProvider? formatProvider)
    {
        if (System.String.IsNullOrEmpty(format))
        {
            return ToBase36();
        }

        System.Char f = format[0];
        return f switch
        {
            'X' or 'x' => ToHex(),
            'B' or 'b' => ToBase36(),
            _ => throw new System.FormatException(
                $"Unsupported format string '{format}'. Use \"X\" for hex or \"B\" for Base36.")
        };
    }
}
