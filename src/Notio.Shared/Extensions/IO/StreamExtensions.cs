using System;
using System.IO;

namespace Notio.Shared.Extensions.IO;

/// <summary>
/// Provides extension methods for working with <see cref="Stream"/> objects.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Converts the contents of the provided <see cref="Stream"/> to a byte array.
    /// </summary>
    /// <param name="this">The <see cref="Stream"/> to be converted to a byte array.</param>
    /// <returns>A byte array containing the content of the stream.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the provided stream is <see langword="null"/>.
    /// </exception>
    public static byte[] ToByteArray(this Stream @this)
    {
        if (@this == null)
            throw new ArgumentNullException(nameof(@this), "Stream cannot be null.");

        int read;
        byte[] buffer = new byte[16 * 1024];
        using MemoryStream ms = new();

        // Read the stream asynchronously and write to the memory stream
        while ((read = @this.ReadAsync(buffer, 0, buffer.Length).GetAwaiter().GetResult()) > 0)
            ms.Write(buffer, 0, read);

        return ms.ToArray();
    }
}
