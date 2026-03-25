// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Logging.Tests")]
[assembly: InternalsVisibleTo("Nalix.Logging.Benchmarks")]
#endif

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
    public static string EscapeString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder sb = new(value.Length + 16);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': _ = sb.Append("\\\""); break;
                case '\b': _ = sb.Append("\\b"); break;
                case '\f': _ = sb.Append("\\f"); break;
                case '\n': _ = sb.Append("\\n"); break;
                case '\r': _ = sb.Append("\\r"); break;
                case '\t': _ = sb.Append("\\t"); break;
                case '\\': _ = sb.Append("\\\\"); break;
                default:
                    if (c is < (char)32 or (>= (char)0x7f and <= (char)0x9f))
                    {
                        _ = sb.Append("\\u");
                        _ = sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        _ = sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Wraps and escapes a string as a JSON string literal.
    /// </summary>
    public static string Quote(string value) => $"\"{EscapeString(value)}\"";

    /// <summary>
    /// Formats a DateTime as ISO 8601 (round-trip 'o' format).
    /// </summary>
    public static string FormatDateTime(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);
}
