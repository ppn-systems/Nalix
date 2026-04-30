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
        sb.Append('[');
        sb.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        sb.Append("] ");
        sb.Append(header);
        sb.AppendLine(":");
        sb.AppendLine("---------------------------------------------------------------------");

        foreach (var kvp in data)
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
                sb.AppendLine();
                foreach (var kvp in dict) AppendValue(sb, kvp.Key, kvp.Value, indent + 1);
                break;

            case IList<Dictionary<string, object>> list:
                sb.AppendLine();
                AppendPad(sb, indent);
                sb.Append(key);
                sb.Append(" (");
                sb.Append(list.Count);
                sb.AppendLine("):");
                if (list.Count > 0) CompactTableWriter.Write(sb, key, list);
                break;

            case IDictionary<string, int> intDict:
                AppendKeyColon(sb, key, indent);
                sb.AppendLine();
                foreach (var kvp in intDict) { AppendPad(sb, indent + 1); AppendKV(sb, kvp.Key, kvp.Value); }
                break;

            case IDictionary<string, long> longDict:
                AppendKeyColon(sb, key, indent);
                sb.AppendLine();
                foreach (var kvp in longDict) { AppendPad(sb, indent + 1); AppendKV(sb, kvp.Key, kvp.Value); }
                break;

            default:
                AppendKeyColon(sb, key, indent);
                sb.Append(' ');
                AppendScalar(sb, value);
                sb.AppendLine();
                break;
        }
    }

    // ── Scalar rendering ─────────────────────────────────────────────────────

    internal static void AppendScalar(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:               sb.Append('-'); break;
            case string s:           sb.Append(s); break;
            case int i:              sb.Append(i); break;
            case long l:             sb.Append(l.ToString("N0", CultureInfo.InvariantCulture)); break;
            case double d:           sb.Append(d.ToString("F2", CultureInfo.InvariantCulture)); break;
            case float f:            sb.Append(f.ToString("F2", CultureInfo.InvariantCulture)); break;
            case bool b:             sb.Append(b ? "Yes" : "No"); break;
            case DateTime dt:        sb.Append(dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); break;
            case DateTimeOffset dto: sb.Append(dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)); break;
            default:                 sb.Append(value); break;
        }
    }

    internal static string FormatScalar(object? value) => value switch
    {
        null                => "-",
        string s            => s,
        int i               => i.ToString(CultureInfo.InvariantCulture),
        long l              => l.ToString("N0", CultureInfo.InvariantCulture),
        double d            => d.ToString("F2", CultureInfo.InvariantCulture),
        float f             => f.ToString("F2", CultureInfo.InvariantCulture),
        bool b              => b ? "Yes" : "No",
        DateTime dt         => dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto  => dto.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        _                   => value.ToString() ?? "-"
    };

    // ── Inline helpers ───────────────────────────────────────────────────────

    internal static void AppendPad(StringBuilder sb, int indent)
    {
        for (int i = 0; i < indent * 2; i++) sb.Append(' ');
    }

    private static void AppendKeyColon(StringBuilder sb, string key, int indent)
    {
        int width = 34 - indent * 2;
        if (key.Length > width)
        {
            sb.Append(key.AsSpan(0, width - 1));
            sb.Append('…');
        }
        else
        {
            sb.Append(key);
            for (int i = key.Length; i < width; i++) sb.Append(' ');
        }
        sb.Append(':');
    }

    private static void AppendKV(StringBuilder sb, string key, object value)
    {
        sb.Append(key);
        sb.Append(": ");
        sb.Append(value);
        sb.AppendLine();
    }
}
