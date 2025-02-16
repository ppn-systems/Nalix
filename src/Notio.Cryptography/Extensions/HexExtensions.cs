using System;
using System.Text;

namespace Notio.Cryptography.Extensions;

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
    public static string ToHexString
    (
        this byte[] bytes,
        string separator = ""
    )
    {
        return bytes is null
            ? throw new ArgumentNullException(nameof(bytes))
            : BitConverter
            .ToString(bytes)
            .Replace("-", separator);
    }

    /// <summary>
    /// Returns the bytes of the string using the provided encoding.
    /// </summary>
    /// <param name="str">The string to get the bytes from</param>
    /// <param name="encoding">The encoding to use</param>
    /// <returns>
    /// The bytes of the string using the encoding if provided,
    /// otherwise the default encoding is used. <see cref="CiphersDefault.DefaultEncoding"/>
    /// </returns>
    public static byte[] ToBytes(this string str, Encoding encoding = null)
    {
        if (string.IsNullOrEmpty(str))
            throw new ArgumentException("Value cannot be null or an empty.", nameof(str));

        encoding ??= CiphersDefault.DefaultEncoding;
        return encoding.GetBytes(str);
    }
}
