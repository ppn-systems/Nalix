using System;
using System.Linq;

namespace Notio.FileStorage.MimeTypes;

/// <summary>
/// Represents a MIME type pattern to match against file data.
/// </summary>
internal class MimeTypePattern(byte[] pattern, ushort offset = 0)
{
    /// <summary>
    /// Gets the byte pattern that represents the MIME type.
    /// </summary>
    public byte[] Bytes { get; private set; } = pattern ?? throw new ArgumentNullException(nameof(pattern));

    /// <summary>
    /// Gets the offset in the data to start matching.
    /// </summary>
    public ushort Offset { get; private set; } = offset;

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeTypePattern"/> class with a hexadecimal string pattern.
    /// </summary>
    /// <param name="hexPattern">The hex pattern string.</param>
    /// <param name="offset">The byte offset where the pattern matching starts.</param>
    public MimeTypePattern(string hexPattern, ushort offset = 0)
        : this(StringToByteArray(hexPattern), offset)
    { }

    /// <summary>
    /// Converts a hexadecimal string to a byte array.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>A byte array representing the hexadecimal string.</returns>
    /// <exception cref="ArgumentException">Thrown when the hex string is not valid.</exception>
    private static byte[] StringToByteArray(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0)
        {
            throw new ArgumentException("Invalid hexadecimal string.", nameof(hex));
        }

        int numberChars = hex.Length;
        byte[] bytes = new byte[numberChars / 2];
        for (int i = 0; i < numberChars; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// Checks if the given data matches the pattern.
    /// </summary>
    /// <param name="data">The data to compare with the pattern.</param>
    /// <returns><c>true</c> if the data matches the pattern; otherwise, <c>false</c>.</returns>
    public bool IsMatch(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return data.Length >= Bytes.Length + Offset &&
               data.Skip(Offset).Take(Bytes.Length).SequenceEqual(Bytes);
    }
}