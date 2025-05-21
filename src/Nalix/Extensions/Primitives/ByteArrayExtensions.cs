namespace Nalix.Extensions.Primitives;

/// <summary>
/// Provides various extension methods for byte arrays and streams.
/// </summary>
public static class ByteArrayExtensions
{
    /// <summary>
    /// Compares two byte arrays for value equality.
    /// </summary>
    /// <param name="a">The first byte array to compare.</param>
    /// <param name="b">The second byte array to compare.</param>
    /// <returns><c>true</c> if both arrays are non-null and contain the same sequence of bytes; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool IsEqualTo(this byte[] a, byte[] b)
        => a != null && b != null && System.Linq.Enumerable.SequenceEqual(a, b);

    /// <summary>
    /// Converts an array of bytes into text with the specified encoding.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="encoding">The encoding.</param>
    /// <returns>A <see cref="string" /> that contains the results of decoding the specified sequence of bytes.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string ToText(this System.Collections.Generic.IEnumerable<byte> buffer, System.Text.Encoding encoding)
        => encoding == null
            ? throw new System.ArgumentNullException(nameof(encoding))
            : encoding.GetString([.. buffer]);

    /// <summary>
    /// Converts an array of bytes into text with UTF8 encoding.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns>A <see cref="string" /> that contains the results of decoding the specified sequence of bytes.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static string ToText(this System.Collections.Generic.IEnumerable<byte> buffer)
        => buffer.ToText(System.Text.Encoding.UTF8);

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
    /// <exception cref="System.ArgumentNullException">stream.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static async System.Threading.Tasks.Task<byte[]> ReadBytesAsync(
        this System.IO.Stream stream,
        long length, int bufferLength,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(stream);

        using System.IO.MemoryStream dest = new();

        try
        {
            byte[] buff = new byte[bufferLength];
            while (length > 0)
            {
                if (length < bufferLength)
                    bufferLength = (int)length;

                int read = await stream.ReadAsync(
                    System.MemoryExtensions.AsMemory(buff, 0, bufferLength),
                    cancellationToken).ConfigureAwait(false);

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
    /// <exception cref="System.ArgumentNullException">stream.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static async System.Threading.Tasks.Task<byte[]> ReadBytesAsync(
        this System.IO.Stream stream, int length,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(stream);

        int offset = 0;
        byte[] buff = new byte[length];

        try
        {
            while (length > 0)
            {
                int read = await stream.ReadAsync(
                    System.MemoryExtensions.AsMemory(buff, offset, length),
                    cancellationToken).ConfigureAwait(false);

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

        return new System.ArraySegment<byte>(buff, 0, offset).ToArray();
    }
}
