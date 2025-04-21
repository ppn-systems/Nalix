using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nalix.Network.Web.Net.Internal;

internal static class StringExtensions
{
    private const string TokenSpecialChars = "()<>@,;:\\\"/[]?={} \t";

    internal static bool IsToken(this string @this)
    {
        return @this.All(c => c >= 0x20 && c < 0x7f && TokenSpecialChars.IndexOf(c) < 0);
    }

    internal static IEnumerable<string> SplitHeaderValue(this string @this, bool useCookieSeparators)
    {
        int len = @this.Length;

        StringBuilder buff = new(32);
        bool escaped = false;
        bool quoted = false;

        for (int i = 0; i < len; i++)
        {
            char c = @this[i];

            if (c == '"')
            {
                if (escaped)
                {
                    escaped = false;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (c == '\\')
            {
                if (i < len - 1 && @this[i + 1] == '"')
                {
                    escaped = true;
                }
            }
            else if (c == ',' || useCookieSeparators && c == ';')
            {
                if (!quoted)
                {
                    yield return buff.ToString();
                    buff.Length = 0;

                    continue;
                }
            }

            _ = buff.Append(c);
        }

        if (buff.Length > 0)
        {
            yield return buff.ToString();
        }
    }

    internal static string Unquote(this string str)
    {
        int start = str.IndexOf('\"');
        int end = str.LastIndexOf('\"');

        if (start >= 0 && end >= 0)
        {
            str = str.Substring(start + 1, end - 1);
        }

        return str.Trim();
    }
}