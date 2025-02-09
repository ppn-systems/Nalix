using System.IO;

namespace Notio.Shared.Extensions.IO;

public static class StreamExtensions
{
    public static byte[] ToByteArray(this Stream @this)
    {
        int read;
        byte[] buffer = new byte[16 * 1024];
        using MemoryStream ms = new();

        while ((read = @this.ReadAsync(buffer, 0, buffer.Length).GetAwaiter().GetResult()) > 0)
            ms.Write(buffer, 0, read);

        return ms.ToArray();
    }
}