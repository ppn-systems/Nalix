using System.Globalization;
using Nalix.Examples.Dashboard.Domain.Metrics;

namespace Nalix.Examples.Dashboard.Presentation.Metrics;

internal static class PingChartPointBuilder
{
    public static string Build(IReadOnlyList<DashboardPingSample> samples)
    {
        if (samples.Count < 2)
        {
            return string.Empty;
        }

        const double width = 320;
        const double height = 96;
        const double top = 10;
        const double bottom = 12;

        double max = Math.Max(120, samples.Max(static sample => sample.Milliseconds));
        double usableHeight = height - top - bottom;

        return string.Join(" ", samples.Select((sample, index) =>
        {
            double x = index * width / (samples.Count - 1);
            double y = top + usableHeight - (Math.Clamp(sample.Milliseconds, 0, max) / max * usableHeight);

            return string.Create(CultureInfo.InvariantCulture, $"{x:F1},{y:F1}");
        }));
    }
}
