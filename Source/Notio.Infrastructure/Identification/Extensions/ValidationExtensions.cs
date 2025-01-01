using System;

namespace Notio.Infrastructure.Identification.Extensions;

internal static class ValidationExtensions
{
    internal static bool IsHexString(this ReadOnlySpan<char> input)
    {
        foreach (char c in input)
        {
            if (!(c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f'))
                return false;
        }
        return true;
    }
}
