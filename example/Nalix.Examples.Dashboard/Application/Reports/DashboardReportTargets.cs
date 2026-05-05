using Nalix.Examples.Contracts.Packets;

namespace Nalix.Examples.Dashboard.Application.Reports;

internal static class DashboardReportTargets
{
    private static readonly GenerationReportTarget[] s_all =
    [
        GenerationReportTarget.DISPATCH,
        GenerationReportTarget.TASKS,
        GenerationReportTarget.BUFFERS,
        GenerationReportTarget.OBJECT_POOLS,
        GenerationReportTarget.CONNECTIONS,
        GenerationReportTarget.CONNECTION_GUARD,
        GenerationReportTarget.INSTANCES
    ];

    public static IReadOnlyList<GenerationReportTarget> All => s_all;

    public static int Count => s_all.Length;

    public static int IndexOf(GenerationReportTarget target)
        => Array.IndexOf(s_all, target);

    public static GenerationReportTarget? Resolve(int index)
        => index >= 0 && index < s_all.Length ? s_all[index] : null;
}
