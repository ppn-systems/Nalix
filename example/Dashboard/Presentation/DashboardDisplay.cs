using System.Globalization;
using Contracts;
using Nalix.Framework.Extensions;

namespace Dashboard.Presentation;

internal static class DashboardDisplay
{
    public static string TargetLabel(GenerationReportTarget target) => target switch
    {
        GenerationReportTarget.DISPATCH => "Dispatch",
        GenerationReportTarget.TASKS => "Tasks",
        GenerationReportTarget.BUFFERS => "Buffers",
        GenerationReportTarget.OBJECT_POOLS => "Object Pools",
        GenerationReportTarget.CONNECTIONS => "Connections",
        GenerationReportTarget.CONNECTION_GUARD => "Connection Guard",
        GenerationReportTarget.INSTANCES => "Instances",
        GenerationReportTarget.NONE => throw new NotImplementedException(),
        _ => target.ToString()
    };

    public static string TargetLabel(GenerationReportTarget? target)
        => target is null ? "Logs" : TargetLabel(target.Value);

    public static string FormatTime(DateTimeOffset? value)
        => value is null ? "never" : value.Value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string FormatPing(double? milliseconds)
        => milliseconds is null
            ? "-- ms"
            : $"{NumberDisplayFormatter.Format(milliseconds.Value)} ms";

    public static string PingStateClass(double? milliseconds) => milliseconds switch
    {
        null => "is-muted",
        < 50 => "is-good",
        < 120 => "is-warn",
        _ => "is-bad"
    };

    public static string FormatCompactNumber(long value) => value.FormatCompact();

    public static string FormatCompactNumber(int value) => value.FormatCompact();

    public static string FormatGroup(long current, long total, bool compact = true)
        => ReportExtensions.FormatGroup(current, total, compact);

    public static string FormatTimeSpan(TimeSpan value) => value.FormatTimeSpan();

    public static string FormatPollingRate(int intervalMs)
    {
        if (intervalMs <= 0)
        {
            return "--";
        }

        double hz = 1000.0 / intervalMs;
        return hz >= 1
            ? $"{hz:F1} Hz"
            : $"{(60.0 / hz):F0}/min";
    }
}

