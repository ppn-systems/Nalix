using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Lite.Extensions;

/// <summary>
/// Provides various extension methods for byte arrays and streams.
/// </summary>
public static class ByteArrayExtensions
{
    /// <summary>
    /// Converts an array of bytes to a base-64 encoded string.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A <see cref="string" /> converted from an array of bytes.</returns>
    public static string ToBase64(this byte[] bytes) => Convert.ToBase64String(bytes);

    /// <summary>
    /// Converts a set of hexadecimal characters (uppercase or lowercase)
    /// to a byte array. String length must be a multiple of 2 and
    /// any prefix (such as 0x) has to be avoided for this to work properly.
    /// </summary>
    /// <param name="this">The hexadecimal.</param>
    /// <returns>
    /// A byte array containing the results of encoding the specified set of characters.
    /// </returns>
    /// <exception cref="ArgumentNullException">hex.</exception>
    public static byte[] ConvertHexadecimalToBytes(this string @this)
    {
        if (string.IsNullOrWhiteSpace(@this))
            throw new ArgumentNullException(nameof(@this));

        return Enumerable
            .Range(0, @this.Length / 2)
            .Select(x => Convert.ToByte(@this.Substring(x * 2, 2), 16))
            .ToArray();
    }

    /// <summary>
    /// Returns the first instance of the matched sequence based on the given offset.
    /// If no matches are found then this method returns -1.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="sequence">The sequence.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The index of the sequence.</returns>
    /// <exception cref="ArgumentNullException">
    /// buffer
    /// or
    /// sequence.
    /// </exception>
    public static int GetIndexOf(this byte[] buffer, byte[] sequence, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(sequence);

        if (sequence.Length == 0)
            return -1;
        if (sequence.Length > buffer.Length)
            return -1;

        var seqOffset = offset < 0 ? 0 : offset;

        var matchedCount = 0;
        for (var i = seqOffset; i < buffer.Length; i++)
        {
            if (buffer[i] == sequence[matchedCount])
                matchedCount++;
            else
                matchedCount = 0;

            if (matchedCount == sequence.Length)
                return i - (matchedCount - 1);
        }

        return -1;
    }

    /// <summary>
    /// Converts an array of bytes into text with the specified encoding.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="encoding">The encoding.</param>
    /// <returns>A <see cref="string" /> that contains the results of decoding the specified sequence of bytes.</returns>
    public static string ToText(this IEnumerable<byte> buffer, Encoding encoding) =>
        encoding == null
            ? throw new ArgumentNullException(nameof(encoding))
            : encoding.GetString(buffer.ToArray());

    /// <summary>
    /// Converts an array of bytes into text with UTF8 encoding.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns>A <see cref="string" /> that contains the results of decoding the specified sequence of bytes.</returns>
    public static string ToText(this IEnumerable<byte> buffer) => buffer.ToText(Encoding.UTF8);

    /// <summary>
    /// Reads the bytes asynchronous.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="length">The length.</param>
    /// <param name="bufferLength">Length of the buffer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A byte array containing the results of encoding the specified set of characters.
    /// </returns>
    /// <exception cref="ArgumentNullException">stream.</exception>
    public static async Task<byte[]> ReadBytesAsync(this Stream stream, long length, int bufferLength, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var dest = new MemoryStream();

        try
        {
            var buff = new byte[bufferLength];
            while (length > 0)
            {
                if (length < bufferLength)
                    bufferLength = (int)length;

                var read = await stream.ReadAsync(buff.AsMemory(0, bufferLength), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                dest.Write(buff, 0, read);
                length -= read;
            }
        }
        catch
        {
            // ignored
        }

        return dest.ToArray();
    }

    /// <summary>
    /// Reads the bytes asynchronous.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A byte array containing the results of encoding the specified set of characters.
    /// </returns>
    /// <exception cref="ArgumentNullException">stream.</exception>
    public static async Task<byte[]> ReadBytesAsync(this Stream stream, int length, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var buff = new byte[length];
        var offset = 0;

        try
        {
            while (length > 0)
            {
                var read = await stream.ReadAsync(buff.AsMemory(offset, length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                offset += read;
                length -= read;
            }
        }
        catch
        {
            // ignored
        }

        return new ArraySegment<byte>(buff, 0, offset).ToArray();
    }
}