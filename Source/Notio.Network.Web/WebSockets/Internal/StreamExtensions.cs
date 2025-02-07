using Notio.Network.Web.Enums;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Web.WebSockets.Internal;

internal static class StreamExtensions
{
    private static readonly byte[] LastByte = [0x00];

    // Compresses or decompresses a stream using the specified compression method.
    public static async Task<MemoryStream> CompressAsync(
        this Stream @this,
        CompressionMethod method,
        bool compress,
        CancellationToken cancellationToken)
    {
        @this.Position = 0;
        MemoryStream targetStream = new();

        switch (method)
        {
            case CompressionMethod.Deflate:
                if (compress)
                {
                    using DeflateStream compressor = new(targetStream, CompressionMode.Compress, true);
                    await @this.CopyToAsync(compressor, 1024, cancellationToken).ConfigureAwait(false);
                    await @this.CopyToAsync(compressor, cancellationToken).ConfigureAwait(false);

                    // WebSocket use this
                    targetStream.Write(LastByte, 0, 1);
                    targetStream.Position = 0;
                }
                else
                {
                    using DeflateStream compressor = new(@this, CompressionMode.Decompress);
                    await compressor.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
                }

                break;

            case CompressionMethod.Gzip:
                if (compress)
                {
                    using GZipStream compressor = new(targetStream, CompressionMode.Compress, true);
                    await @this.CopyToAsync(compressor, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    using GZipStream compressor = new(@this, CompressionMode.Decompress);
                    await compressor.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
                }

                break;

            case CompressionMethod.None:
                await @this.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(method), method, null);
        }

        return targetStream;
    }
}