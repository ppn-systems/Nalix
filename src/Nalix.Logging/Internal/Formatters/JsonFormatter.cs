// Copyright (c) 2026 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Internal.Formatters;

/// <summary>
/// Minimal JSON helpers used by our manual serializer.
/// This is intentionally small and focused on the types we need.
/// </summary>
internal static class JsonFormatter
{
    /// <summary>
    /// Escapes a string for inclusion in JSON string literal.
    /// </summary>
    public static System.String EscapeString(System.String value)
    {
        if (System.String.IsNullOrEmpty(value))
        {
            return System.String.Empty;
        }

        System.Text.StringBuilder sb = new(value.Length + 16);
        foreach (System.Char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c is < (System.Char)32 or (>= (System.Char)0x7f and <= (System.Char)0x9f))
                    {
                        sb.Append("\\u");
                        sb.Append(((System.Int32)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Wraps and escapes a string as a JSON string literal.
    /// </summary>
    public static System.String Quote(System.String value) => $"\"{EscapeString(value)}\"";

    /// <summary>
    /// Formats a DateTime as ISO 8601 (round-trip 'o' format).
    /// </summary>
    public static System.String FormatDateTime(System.DateTime value) => value.ToString("o");
}