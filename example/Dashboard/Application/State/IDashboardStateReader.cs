using Contracts;
using Nalix.Dashboard.Domain.Logs;
using Nalix.Dashboard.Domain.Metrics;
using Nalix.Dashboard.Domain.Reports;

namespace Nalix.Dashboard.Application.State;

internal interface IDashboardStateReader
{
    event Action? Changed;

    bool IsConnected { get; }

    bool HasApiKey { get; }

    string? LastError { get; }

    double? LastPingMilliseconds { get; }

    DateTimeOffset? LastPingAt { get; }

    IReadOnlyDictionary<GenerationReportTarget, DashboardReportSnapshot> Reports { get; }

    IReadOnlyList<DashboardPingSample> PingSamples { get; }

    IReadOnlyList<DashboardLogEntry> Logs { get; }
}
