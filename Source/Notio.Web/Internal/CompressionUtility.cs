using Notio.Web.Enums;
using System.IO;
using System.IO.Compression;

namespace Notio.Web.Internal;

internal static class CompressionUtility
{
    public static byte[]? ConvertCompression(byte[] source, CompressionMethod sourceMethod, CompressionMethod targetMethod)
    {
        if (source == null)
        {
            return null;
        }

        if (sourceMethod == targetMethod)
        {
            return source;
        }

        switch (sourceMethod)
        {
            case CompressionMethod.Deflate:
                using (MemoryStream sourceStream = new(source, false))
                {
                    using DeflateStream decompressionStream = new(sourceStream, CompressionMode.Decompress, true);
                    using MemoryStream targetStream = new();
                    if (targetMethod == CompressionMethod.Gzip)
                    {
                        using GZipStream compressionStream = new(targetStream, CompressionMode.Compress, true);
                        decompressionStream.CopyTo(compressionStream);
                    }
                    else
                    {
                        decompressionStream.CopyTo(targetStream);
                    }

                    return targetStream.ToArray();
                }

            case CompressionMethod.Gzip:
                using (MemoryStream sourceStream = new(source, false))
                {
                    using GZipStream decompressionStream = new(sourceStream, CompressionMode.Decompress, true);
                    using MemoryStream targetStream = new();
                    if (targetMethod == CompressionMethod.Deflate)
                    {
                        using DeflateStream compressionStream = new(targetStream, CompressionMode.Compress, true);
                        _ = decompressionStream.CopyToAsync(compressionStream);
                    }
                    else
                    {
                        decompressionStream.CopyTo(targetStream);
                    }

                    return targetStream.ToArray();
                }

            default:
                using (MemoryStream sourceStream = new(source, false))
                {
                    using MemoryStream targetStream = new();
                    switch (targetMethod)
                    {
                        case CompressionMethod.Deflate:
                            using (DeflateStream compressionStream = new(targetStream, CompressionMode.Compress, true))
                            {
                                sourceStream.CopyTo(compressionStream);
                            }

                            break;

                        case CompressionMethod.Gzip:
                            using (GZipStream compressionStream = new(targetStream, CompressionMode.Compress, true))
                            {
                                sourceStream.CopyTo(compressionStream);
                            }

                            break;

                        default:
                            // Just in case. Consider all other values as None.
                            return source;
                    }

                    return targetStream.ToArray();
                }
        }
    }
}