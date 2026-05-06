using Nalix.Examples.Contracts;
using Nalix.Examples.Dashboard.Domain.Logs;
using Nalix.Examples.Dashboard.Domain.Metrics;
using Nalix.Examples.Dashboard.Domain.Reports;

namespace Nalix.Examples.Dashboard.Application.State;

internal interface IDashboardStateReader
{
    event Action? Changed;

    bool IsConnected { get; }

    bool IsPollingPaused { get; }

    bool IsReportNavigationOpen { get; }

    GenerationReportTarget? ActiveReportTarget { get; }

    string BackendEndpoint { get; }

    string? LastError { get; }

    DateTimeOffset? LastRefreshAt { get; }

    double? LastPingMilliseconds { get; }

    DateTimeOffset? LastPingAt { get; }

    bool HasApiKey { get; }

    IReadOnlyDictionary<GenerationReportTarget, DashboardReportSnapshot> Reports { get; }

    IReadOnlyList<DashboardPingSample> PingSamples { get; }

    IReadOnlyList<DashboardLogEntry> Logs { get; }
}
