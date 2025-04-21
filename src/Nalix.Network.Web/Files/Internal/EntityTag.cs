using Nalix.Network.Web.Enums;
using Nalix.Network.Web.Utilities;
using System;
using System.Text;

namespace Nalix.Network.Web.Files.Internal;

internal static class EntityTag
{
    public static string Compute(DateTime lastModifiedUtc, long length, CompressionMethod compressionMethod)
    {
        StringBuilder sb = new StringBuilder()
            .Append('"')
            .Append(Base64Utility.LongToBase64(lastModifiedUtc.Ticks))
            .Append(Base64Utility.LongToBase64(length));

        switch (compressionMethod)
        {
            case CompressionMethod.Deflate:
                _ = sb.Append('-').Append(CompressionMethodNames.Deflate);
                break;

            case CompressionMethod.Gzip:
                _ = sb.Append('-').Append(CompressionMethodNames.Gzip);
                break;
        }

        return sb.Append('"').ToString();
    }
}