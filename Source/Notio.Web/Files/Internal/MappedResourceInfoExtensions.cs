using Notio.Web.Enums;

namespace Notio.Web.Files.Internal
{
    internal static class MappedResourceInfoExtensions
    {
        public static string GetEntityTag(this MappedResourceInfo @this, CompressionMethod compressionMethod)
        {
            return EntityTag.Compute(@this.LastModifiedUtc, @this.Length, compressionMethod);
        }
    }
}