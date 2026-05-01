// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text;

namespace Nalix.Network.Examples.UI.Formatting;

/// <summary>
/// Converts <see cref="IDictionary{TKey,TValue}"/> report data into formatted text,
/// delegating list tables to <see cref="CompactTableWriter"/>.
/// </summary>
internal static class ReportDataFormatter
{
    /// <summary>
    /// Formats a report data dictionary into a full text block with header.
    /// </summary>
    public static string Format(IDictionary<string, object> data, string header)
    {
        StringBuilder sb = StringBuilderPool.Rent();
        _ = sb.Append('[');
        _ = sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        _ = sb.Append("] ");
        _ = sb.Append(header);
        _ = sb.AppendLine(":");
        _ = sb.AppendLine("---------------------------------------------------------------------");

        foreach (KeyValuePair<string, object> kvp in data)
        {
            AppendValue(sb, kvp.Key, kvp.Value, 0);
        }

        return sb.ToString();
    }

    // ── Recursive value renderer ─────────────────────────────────────────────

    private static void AppendValue(StringBuilder sb, string key, object? value, int indent)
    {
        AppendPad(sb, indent);

        switch (value)
        {
            case IDictionary<string, object> dict:
                AppendKeyColon(sb, key, indent);
                _ = sb.AppendLine();
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    AppendValue(sb, kvp.Key, kvp.Value, indent + 1);
                }

                break;

            case IList<Dictionary<string, object>> list:
                _ = sb.AppendLine();
                AppendPad(sb, indent);
                _ = sb.Append(key);
                _ = sb.Append(" (");
                _ = sb.Append(list.Count);
                _ = sb.AppendLine("):");
                if (list.Count > 0)
                {
                    CompactTableWriter.Write(sb, key, list);
                }

                break;

            case IDictionary<string, int> intDict:
                AppendKeyColon(sb, key, indent);
                _ = sb.AppendLine();
                foreach (KeyValuePair<string, int> kvp in intDict) { AppendPad(sb, indent + 1); AppendKV(sb, kvp.Key, kvp.Value); }
                break;

            case IDictionary<string, long> longDict:
                AppendKeyColon(sb, key, indent);
                _ = sb.AppendLine();
                foreach (KeyValuePair<string, long> kvp in longDict) { AppendPad(sb, indent + 1); AppendKV(sb, kvp.Key, kvp.Value); }
                break;

            default:
                AppendKeyColon(sb, key, indent);
                _ = sb.Append(' ');
                AppendScalar(sb, value);
                _ = sb.AppendLine();
                break;
        }
    }

    // ── Scalar rendering ─────────────────────────────────────────────────────

    internal static void AppendScalar(StringBuilder sb, object? value)
    {
        _ = value switch
        {
            null => sb.Append('-'),
            string s => sb.Append(s),
            int i => sb.Append(i),
            long l => sb.Append(l.ToString("N0", CultureInfo.InvariantCulture)),
            double d => sb.Append(d.ToString("F2", CultureInfo.InvariantCulture)),
            float f => sb.Append(f.ToString("F2", CultureInfo.InvariantCulture)),
            bool b => sb.Append(b ? "Yes" : "No"),
            DateTime dt => sb.Append(dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            DateTimeOffset dto => sb.Append(dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            _ => sb.Append(value),
        };
    }

    internal static string FormatScalar(object? value) => value switch
    {
        null => "-",
        string s => s,
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString("N0", CultureInfo.InvariantCulture),
        double d => d.ToString("F2", CultureInfo.InvariantCulture),
        float f => f.ToString("F2", CultureInfo.InvariantCulture),
        bool b => b ? "Yes" : "No",
        DateTime dt => dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "-"
    };

    // ── Inline helpers ───────────────────────────────────────────────────────

    internal static void AppendPad(StringBuilder sb, int indent)
    {
        for (int i = 0; i < indent * 2; i++)
        {
            _ = sb.Append(' ');
        }
    }

    private static void AppendKeyColon(StringBuilder sb, string key, int indent)
    {
        int width = 34 - (indent * 2);
        if (key.Length > width)
        {
            _ = sb.Append(key.AsSpan(0, width - 1));
            _ = sb.Append('…');
        }
        else
        {
            _ = sb.Append(key);
            for (int i = key.Length; i < width; i++)
            {
                _ = sb.Append(' ');
            }
        }
        _ = sb.Append(':');
    }

    private static void AppendKV(StringBuilder sb, string key, object value)
    {
        _ = sb.Append(key);
        _ = sb.Append(": ");
        _ = sb.Append(value);
        _ = sb.AppendLine();
    }
}
