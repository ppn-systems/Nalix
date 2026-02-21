using System;
using System.Globalization;
using System.Text;

namespace Nalix.SDK.Tools.Extensions;

/// <summary>
/// Provides helper methods for hexadecimal conversion.
/// </summary>
public static class HexExtensions
{
    /// <summary>
    /// Converts the specified byte array to a grouped upper-case hexadecimal string.
    /// </summary>
    /// <param name="bytes">The source bytes.</param>
    /// <returns>A formatted hexadecimal string.</returns>
    public static string ToHexString(this byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(bytes.Length * 3);
        for (int index = 0; index < bytes.Length; index++)
        {
            if (index > 0)
            {
                _ = builder.Append(' ');
            }

            _ = builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses a hexadecimal string into a byte array.
    /// </summary>
    /// <param name="hex">The source hexadecimal string.</param>
    /// <returns>The parsed bytes.</returns>
    public static byte[] ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Array.Empty<byte>();
        }

        StringBuilder sanitized = new(hex.Length);
        foreach (char character in hex)
        {
            if (Uri.IsHexDigit(character))
            {
                _ = sanitized.Append(character);
            }
        }

        if (sanitized.Length == 0)
        {
            return Array.Empty<byte>();
        }

        if (sanitized.Length % 2 != 0)
        {
            throw new FormatException("Hex input must contain an even number of digits.");
        }

        byte[] result = new byte[sanitized.Length / 2];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = byte.Parse(
                sanitized.ToString(index * 2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        return result;
    }

    /// <summary>
    /// Formats bytes into an offset-based hex dump with ASCII preview.
    /// </summary>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="bytesPerLine">The number of bytes per line.</param>
    /// <returns>The formatted hex dump.</returns>
    public static string ToHexDump(this byte[]? bytes, int bytesPerLine = 16)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new(bytes.Length * 5);
        for (int offset = 0; offset < bytes.Length; offset += bytesPerLine)
        {
            int lineLength = Math.Min(bytesPerLine, bytes.Length - offset);
            _ = builder.Append(offset.ToString("X4", CultureInfo.InvariantCulture));
            _ = builder.Append("  ");

            for (int index = 0; index < bytesPerLine; index++)
            {
                if (index < lineLength)
                {
                    _ = builder.Append(bytes[offset + index].ToString("X2", CultureInfo.InvariantCulture));
                }
                else
                {
                    _ = builder.Append("  ");
                }

                if (index < bytesPerLine - 1)
                {
                    _ = builder.Append(' ');
                }
            }

            _ = builder.Append("  |");
            for (int index = 0; index < lineLength; index++)
            {
                byte current = bytes[offset + index];
                _ = builder.Append(current >= 32 && current <= 126 ? (char)current : '.');
            }

            _ = builder.AppendLine("|");
        }

        return builder.ToString();
    }
}
