using System.Globalization;

namespace Dashboard.Presentation.ReportValues;

public enum ReportMetricKind
{
    Text,
    Boolean,
    Time,
    Bytes,
    Count,
    Rate,
    Date,
    Code
}

public sealed record ReportMetricItem(
    string Key,
    string Label,
    string Value,
    string RawValue,
    ReportMetricKind Kind);

public sealed record ReportMetricGroup(string Name, IReadOnlyList<ReportMetricItem> Items);

public sealed record ReportDetailRow(string Key, ParsedReportValue ParsedValue);

public sealed record ReportMetricLayout(
    IReadOnlyList<ReportMetricGroup> MetricGroups,
    IReadOnlyList<ReportDetailRow> DetailRows);

public static class ReportMetricLayoutBuilder
{
    private static readonly string[] s_groupOrder =
    [
        "State",
        "Performance",
        "Capacity",
        "Counters",
        "General"
    ];

    public static ReportMetricLayout Build(IEnumerable<KeyValuePair<string, object?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        List<MetricDraft> metrics = [];
        List<ReportDetailRow> details = [];

        foreach (KeyValuePair<string, object?> row in rows)
        {
            ParsedReportValue parsed = ReportValueParser.Parse(row.Key, row.Value);
            if (parsed.Kind is ParsedReportValueKind.Scalar or ParsedReportValueKind.Empty)
            {
                metrics.Add(CreateMetric(row.Key, parsed));
                continue;
            }

            details.Add(new ReportDetailRow(row.Key, parsed));
        }

        List<ReportMetricGroup> groups = [.. metrics
            .GroupBy(static metric => metric.Group, StringComparer.Ordinal)
            .OrderBy(static group => GroupRank(group.Key))
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new ReportMetricGroup(
                group.Key,
                [.. group.Select(static metric => metric.Item)]))];

        return new ReportMetricLayout(groups, details);
    }

    private static MetricDraft CreateMetric(string key, ParsedReportValue parsed)
    {
        string raw = parsed.Kind == ParsedReportValueKind.Empty ? string.Empty : parsed.Raw;
        string displayName = ReportValueParser.DisplayName(key);
        ReportMetricKind kind = InferKind(displayName, raw);

        return new MetricDraft(
            InferGroup(displayName, kind),
            new ReportMetricItem(
                key,
                FormatLabel(displayName, kind),
                FormatMetricValue(displayName, raw, kind),
                raw,
                kind));
    }

    private static ReportMetricKind InferKind(string label, string raw)
    {
        if (ReportValueFormatter.IsBooleanValue(raw))
        {
            return ReportMetricKind.Boolean;
        }

        if (ReportValueFormatter.IsDateValue(raw) || ContainsAny(label, "utc", "timestamp", "date"))
        {
            return ReportMetricKind.Date;
        }

        if (ReportValueFormatter.IsCodeValue(raw))
        {
            return ReportMetricKind.Code;
        }

        if (ContainsAny(label, " ms", "millisecond", "second", "minute", "hour", "time", "duration", "interval", "latency", "uptime"))
        {
            return ReportMetricKind.Time;
        }

        if (ContainsAny(label, "byte", "bytes", "size", "memory"))
        {
            return ReportMetricKind.Bytes;
        }

        if (ContainsAny(label, "rate", "ratio", "percent", "percentage", "threshold", "cpu"))
        {
            return ReportMetricKind.Rate;
        }

        if (ContainsAny(label, "count", "hits", "misses", "workers", "limit", "total", "current", "pending", "runs", "failures", "connections"))
        {
            return ReportMetricKind.Count;
        }

        return ReportMetricKind.Text;
    }

    private static string InferGroup(string label, ReportMetricKind kind)
    {
        if (kind == ReportMetricKind.Boolean)
        {
            return "State";
        }

        if (kind is ReportMetricKind.Time or ReportMetricKind.Rate)
        {
            return "Performance";
        }

        if (kind == ReportMetricKind.Bytes ||
            ContainsAny(label, "limit", "worker", "concurrency", "capacity", "buffer", "memory", "size", "pool"))
        {
            return "Capacity";
        }

        if (kind == ReportMetricKind.Count)
        {
            return "Counters";
        }

        return "General";
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases", Justification = "<Pending>")]
    private static string FormatLabel(string label, ReportMetricKind kind)
        => kind switch
        {
            ReportMetricKind.Time => TrimTrailingWords(label, "Ms", "Milliseconds", "Seconds", "Minutes", "Hours"),
            ReportMetricKind.Bytes => TrimTrailingWords(label, "Bytes"),
            _ => label
        };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases", Justification = "<Pending>")]
    private static string FormatMetricValue(string label, string raw, ReportMetricKind kind)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "-";
        }

        return kind switch
        {
            ReportMetricKind.Boolean => raw.Trim().ToLowerInvariant(),
            ReportMetricKind.Time => FormatTimeValue(label, raw),
            ReportMetricKind.Bytes => FormatByteValue(raw),
            ReportMetricKind.Rate => FormatRateValue(label, raw),
            _ => ReportValueFormatter.Format(raw)
        };
    }

    private static string FormatTimeValue(string label, string raw)
    {
        if (!TryReadNumber(raw, out double value))
        {
            return ReportValueFormatter.Format(raw);
        }

        string unit = "ms";
        if (ContainsAny(label, "minute"))
        {
            unit = "min";
        }
        else if (ContainsAny(label, "second"))
        {
            unit = "s";
        }
        else if (ContainsAny(label, "hour"))
        {
            unit = "h";
        }

        return string.Create(CultureInfo.InvariantCulture, $"{NumberDisplayFormatter.Format(value)} {unit}");
    }

    private static string FormatByteValue(string raw)
    {
        if (!TryReadNumber(raw, out double value))
        {
            return ReportValueFormatter.Format(raw);
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unitIndex = 0;
        double scaled = value;

        while (Math.Abs(scaled) >= 1024 && unitIndex < units.Length - 1)
        {
            scaled /= 1024;
            unitIndex++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{NumberDisplayFormatter.Format(scaled)} {units[unitIndex]}");
    }

    private static string FormatRateValue(string label, string raw)
    {
        if (!TryReadNumber(raw, out double value))
        {
            return ReportValueFormatter.Format(raw);
        }

        bool percentLike = ContainsAny(label, "ratio", "percent", "percentage", "threshold", "cpu") ||
                           (ContainsAny(label, "rate") && value is >= 0 and <= 1);

        if (!percentLike)
        {
            return NumberDisplayFormatter.Format(value);
        }

        double percent = value is >= 0 and <= 1 ? value * 100 : value;
        return string.Create(CultureInfo.InvariantCulture, $"{NumberDisplayFormatter.Format(percent)}%");
    }

    private static string TrimTrailingWords(string label, params string[] words)
    {
        string trimmed = label.Trim();
        foreach (string word in words)
        {
            string suffix = " " + word;
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[..^suffix.Length].TrimEnd();
            }
        }

        return trimmed;
    }

    private static bool ContainsAny(string label, params string[] terms)
    {
        foreach (string term in terms)
        {
            if (label.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadNumber(string raw, out double value)
        => double.TryParse(
            raw.Trim(),
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value) && double.IsFinite(value);

    private static int GroupRank(string group)
    {
        int index = Array.IndexOf(s_groupOrder, group);
        return index < 0 ? s_groupOrder.Length : index;
    }

    private sealed record MetricDraft(string Group, ReportMetricItem Item);
}
