// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Identity;

public readonly partial struct Identifier : System.ISpanFormattable, System.IUtf8SpanFormattable
{
    // ============
    // Fast access
    // ============

    /// <summary>
    /// Returns the little-endian 56-bit combined value: [type:8][machine:16][value:32].
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.UInt64 GetCombined() => ((System.UInt64)_type << 48) | ((System.UInt64)MachineId << 32) | Value;

    /// <summary>
    /// Creates an identifier from a 56-bit combined value. Upper 8 bits must be zero.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Identifier FromCombined(System.UInt64 combined)
    {
        if ((combined & ~MaxSevenByteValue) != 0UL)
        {
            throw new System.ArgumentOutOfRangeException(nameof(combined), "Must fit in 56 bits.");
        }

        System.UInt32 v = (System.UInt32)(combined & 0xFFFFFFFFUL);
        System.UInt16 m = (System.UInt16)((combined >> 32) & 0xFFFFUL);
        System.Byte t = (System.Byte)((combined >> 48) & 0xFFUL);
        return NewId(v, m, (Nalix.Common.Enums.IdentifierType)t);
    }

    // ==================
    // Faster byte format
    // ==================

    /// <summary>
    /// Writes the 7-byte little-endian layout directly. Destination must be >= 7 bytes.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryWriteBytes(System.Span<System.Byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        // Unsafe path: treat "this" as 7 bytes and copy.
        // We slice to 7 explicitly to avoid reading beyond struct size padding.
        System.ReadOnlySpan<System.Byte> src = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(
                ref System.Runtime.CompilerServices.Unsafe.AsRef(in this), 1));

        src[..Size].CopyTo(destination);
        return true;
    }

    // ===========
    // UTF-8 paths
    // ===========

    /// <summary>
    /// Formats the identifier into Base36 as UTF-8 without allocations.
    /// </summary>
    /// <remarks>Equivalent to ToBase36() but emits UTF-8 bytes.</remarks>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryFormatUtf8(System.Span<System.Byte> utf8Destination, out System.Int32 bytesWritten)
    {
        // We encode as Base36 directly into UTF-8 bytes.
        // Max 13 chars for 56-bit value in base-36 -> max 13 bytes UTF-8.
        System.Span<System.Char> tmp = stackalloc System.Char[13];
        if (!TryFormatBase36(tmp, out System.Byte chars))
        {
            bytesWritten = 0;
            return false;
        }

        // Fast path: ASCII subset -> 1 byte per char.
        if (utf8Destination.Length < chars)
        {
            bytesWritten = 0;
            return false;
        }

        for (System.Int32 i = 0; i < chars; i++)
        {
            utf8Destination[i] = (System.Byte)tmp[i];
        }

        bytesWritten = chars;
        return true;
    }

    /// <summary>
    /// Parses a Base36 UTF-8 byte sequence into an Identifier.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Boolean TryParse(System.ReadOnlySpan<System.Byte> utf8, out Identifier id)
    {
        // Accept only ASCII digits/letters; map lower->upper.
        System.Span<System.Char> tmp = stackalloc System.Char[13];
        if (utf8.Length is 0 or > 13) { id = default; return false; }

        System.Int32 len = utf8.Length;
        for (System.Int32 i = 0; i < len; i++)
        {
            System.Byte b = utf8[i];
            if (b is >= (System.Byte)'0' and <= (System.Byte)'9')
            {
                tmp[i] = (System.Char)b;
            }
            else if (b is >= (System.Byte)'A' and <= (System.Byte)'Z')
            {
                tmp[i] = (System.Char)b;
            }
            else if (b is >= (System.Byte)'a' and <= (System.Byte)'z')
            {
                tmp[i] = (System.Char)(b - 32); // to upper
            }
            else { id = default; return false; }
        }
        return TryParse(tmp[..len], out id);
    }

    // ==========================
    // ISpanFormattable/IUtf8 impl
    // ==========================

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public System.Boolean TryFormat(
        System.Span<System.Char> destination, out System.Int32 charsWritten,
        System.ReadOnlySpan<System.Char> format, System.IFormatProvider? _)
    {
        // If "X" -> Hex, else Base36 (default).
        System.Boolean hex = format.Length == 1 && (format[0] == 'X' || format[0] == 'x');
        if (hex)
        {
            // 14 hex chars for 7 bytes.
            System.Span<System.Byte> buf = stackalloc System.Byte[Size];
            if (!TryWriteBytes(buf)) { charsWritten = 0; return false; }
            // Convert.ToHex(Span<byte>) alloc-free in .NET 8+, but API returns string.
            // So we format manually into destination.
            const System.String HEX = "0123456789ABCDEF";
            if (destination.Length < Size * 2) { charsWritten = 0; return false; }
            System.Int32 j = 0;
            for (System.Int32 i = 0; i < Size; i++)
            {
                System.Byte b = buf[i];
                destination[j++] = HEX[b >> 4];
                destination[j++] = HEX[b & 0xF];
            }
            charsWritten = Size * 2;
            return true;
        }
        else
        {
            // Base36 (existing TryFormatBase36(char) returns byte charsWritten).
            if (!TryFormatBase36(destination, out System.Byte w)) { charsWritten = 0; return false; }
            charsWritten = w;
            return true;
        }
    }

    /// <inheritdoc />
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean TryFormat(
        System.Span<System.Byte> utf8Destination, out System.Int32 bytesWritten,
        System.ReadOnlySpan<System.Char> format, System.IFormatProvider? provider)
        => TryFormatUtf8(utf8Destination, out bytesWritten);
}
