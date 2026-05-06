using Contracts;
using Dashboard.Domain.Logs;
using Dashboard.Domain.Metrics;
using Dashboard.Domain.Reports;

namespace Dashboard.Application.State;

internal interface IDashboardStateReader
{
    event Action? Changed;

    bool IsConnected { get; }

    bool IsPollingPaused { get; }

    bool IsReportNavigationOpen { get; }

    bool IsConfigView { get; }

    GenerationReportTarget? ActiveReportTarget { get; }

    string BackendEndpoint { get; }

    string? LastError { get; }

    DateTimeOffset? LastRefreshAt { get; }

    double? LastPingMilliseconds { get; }

    DateTimeOffset? LastPingAt { get; }

    bool HasApiKey { get; }

    int PollIntervalMs { get; }

    int PingIntervalMs { get; }

    int RequestTimeoutMs { get; }

    IReadOnlyDictionary<GenerationReportTarget, DashboardReportSnapshot> Reports { get; }

    IReadOnlyList<DashboardPingSample> PingSamples { get; }

    IReadOnlyList<DashboardLogEntry> Logs { get; }
}
