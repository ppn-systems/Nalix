using System;
using System.Linq;
using System.Text;

namespace Notio.Extensions;

internal static class HexExtensions
{
    /// <summary>
    /// Returns the hex string representation of the
    /// provided array of bytes.
    /// </summary>
    /// <param name="bytes">The array of bytes</param>
    /// <param name="separator">The separator to use</param>
    /// <returns>
    /// A hex string of the provided bytes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Throws when the input byte array is null.
    /// </exception>
    public static string ToHexString(this byte[] bytes, string separator = "")
    {
        ArgumentNullException.ThrowIfNull(bytes);

        StringBuilder sb = new(bytes.Length * 2);
        foreach (byte b in bytes) sb.AppendFormat("{0:x2}", b);

        return separator.Length == 0 ? sb.ToString() :
            string.Join(separator, sb.ToString().Chunk(2).Select(c => new string(c)));
    }


    /// <summary>
    /// Converts a string to a byte array using the specified encoding or as a hex string.
    /// </summary>
    /// <param name="str">The input string.</param>
    /// <param name="encoding">The encoding to use (default is UTF-8).</param>
    /// <returns>The byte array representation.</returns>
    public static byte[] ToBytes(this string str, Encoding encoding = null)
    {
        if (string.IsNullOrWhiteSpace(str))
            throw new ArgumentException("Value cannot be null or empty.", nameof(str));

        // If the string looks like a hex string, parse it as hex
        return str.IsValidHex() ?
            str.FromHexString() : (encoding ?? Encoding.UTF8).GetBytes(str);
    }

    /// <summary>
    /// Converts a hex string to a byte array.
    /// </summary>
    /// <param name="hex">The hex string.</param>
    /// <returns>The byte array representation.</returns>
    /// <exception cref="ArgumentException">Thrown if the input is not a valid hex string.</exception>
    public static byte[] FromHexString(this string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Hex string cannot be null or empty.", nameof(hex));

        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string length must be even.", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    /// <summary>
    /// Determines whether a string is a valid hexadecimal representation.
    /// </summary>
    /// <param name="hex">The hex string to check.</param>
    /// <returns>True if the string is valid hex; otherwise, false.</returns>
    public static bool IsValidHex(this string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0)
            return false;

        foreach (char c in hex)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return true;
    }
}
