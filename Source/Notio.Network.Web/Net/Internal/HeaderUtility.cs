using System;
using System.Linq;

namespace Notio.Network.Web.Net.Internal;

internal static class HeaderUtility
{
    public static string? GetCharset(string? contentType)
    {
        return contentType?
                    .Split(';')
                    .Select(p => p.Trim())
                    .Where(part => part.StartsWith("charset", StringComparison.OrdinalIgnoreCase))
                    .Select(GetAttributeValue)
                    .FirstOrDefault();
    }

    public static string? GetAttributeValue(string nameAndValue)
    {
        int idx = nameAndValue.IndexOf('=');

        return idx < 0 || idx == nameAndValue.Length - 1 ? null : nameAndValue[(idx + 1)..].Trim().Unquote();
    }
}