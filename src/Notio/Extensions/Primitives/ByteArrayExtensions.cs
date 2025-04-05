using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Extensions.Primitives;

/// <summary>
/// Provides various extension methods for byte arrays and streams.
/// </summary>
public static class ByteArrayExtensions
{
    /// <summary>
    /// Converts an array of bytes into text with the specified encoding.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="encoding">The encoding.</param>
    /// <returns>A <see cref="string" /> that contains the results of decoding the specified sequence of bytes.</returns>
    public static string ToText(this IEnumerable<byte> buffer, Encoding encoding) =>
        encoding == null
            ? throw new ArgumentNullException(nameof(encoding))
            : encoding.GetString([.. buffer]);

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
