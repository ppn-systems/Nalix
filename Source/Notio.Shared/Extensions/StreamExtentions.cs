using System.IO;

namespace Notio.Shared.Extensions;

public static class StreamExtentions
{
    public static byte[] ToByteArray(this Stream input)
    {
        int read;
        byte[] buffer = new byte[16 * 1024];
        using MemoryStream ms = new();

        while ((read = input.ReadAsync(buffer, 0, buffer.Length).GetAwaiter().GetResult()) > 0)
            ms.Write(buffer, 0, read);

        return ms.ToArray();
    }
}