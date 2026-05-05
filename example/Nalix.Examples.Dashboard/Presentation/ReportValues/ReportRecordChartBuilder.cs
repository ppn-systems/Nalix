using Nalix.Examples.Dashboard.Presentation;

namespace Nalix.Examples.Dashboard.Presentation.ReportValues;

internal sealed record ReportRecordChartRow(
    string Label,
    string MetricLabel,
    double Current,
    double Maximum)
{
    public double Percent => Maximum <= 0
        ? 0
        : Math.Clamp(Current / Maximum * 100d, 0d, 100d);

    public string ValueText =>
        $"{NumberDisplayFormatter.Format(Current)} / {NumberDisplayFormatter.Format(Maximum)}";
}

internal static class ReportRecordChartBuilder
{
    private static readonly ChartMetric[] s_metrics =
    [
        new("InUse", "Total", "In use", true),
        new("Outstanding", "MaxCapacity", "Outstanding", false),
        new("Available", "MaxCapacity", "Available", true),
        new("Running", "Total", "Running", true),
        new("Pending", "Concurrency", "Pending", true),
        new("Free", "Total", "Free", true)
    ];

    public static IReadOnlyList<ReportRecordChartRow> Build(
        string reportKey,
        IReadOnlyList<ParsedReportRecord> records)
    {
        if (!IsSupportedReportKey(reportKey))
        {
            return [];
        }

        List<ReportRecordChartRow> rows = [];

        foreach (ParsedReportRecord record in records)
        {
            if (TryBuildRow(record, out ReportRecordChartRow? row))
            {
                rows.Add(row!);
            }
        }

        return rows;
    }

    private static bool IsSupportedReportKey(string reportKey)
        => reportKey.Equals("Pools", StringComparison.Ordinal);

    private static bool TryBuildRow(ParsedReportRecord record, out ReportRecordChartRow? row)
    {
        foreach (ChartMetric metric in s_metrics)
        {
            if (!TryReadNumber(record.Fields, metric.CurrentField, out double current) ||
                !TryReadNumber(record.Fields, metric.MaximumField, out double maximum) ||
                maximum <= 0 ||
                (!metric.ShowWhenZero && current <= 0))
            {
                continue;
            }

            row = new ReportRecordChartRow(
                NormalizeLabel(record.Title),
                metric.Label,
                Math.Max(0, current),
                maximum);
            return true;
        }

        row = null;
        return false;
    }

    private static bool TryReadNumber(
        IReadOnlyList<ParsedReportField> fields,
        string fieldName,
        out double value)
    {
        ParsedReportField? field = fields.FirstOrDefault(
            item => item.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

        if (field is null ||
            !double.TryParse(
                field.Value,
                System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture,
                out value) ||
            !double.IsFinite(value))
        {
            value = 0;
            return false;
        }

        return true;
    }

    private static string NormalizeLabel(string label)
    {
        string formatted = ReportValueFormatter.Format(label);
        int typeSeparator = formatted.LastIndexOf('.');

        return typeSeparator >= 0 && typeSeparator + 1 < formatted.Length
            ? formatted[(typeSeparator + 1)..]
            : formatted;
    }

    private sealed record ChartMetric(
        string CurrentField,
        string MaximumField,
        string Label,
        bool ShowWhenZero);
}
