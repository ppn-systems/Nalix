using Nalix.Examples.Contracts.Packets;
using Nalix.Examples.Dashboard.Domain.Logs;
using Nalix.Examples.Dashboard.Domain.Metrics;
using Nalix.Examples.Dashboard.Domain.Reports;

namespace Nalix.Examples.Dashboard.Application.State;

internal sealed class DashboardState : IDashboardStateReader, IDashboardStateWriter
{
    private readonly Lock _gate = new();
    private readonly Dictionary<GenerationReportTarget, DashboardReportSnapshot> _reports = [];
    private readonly Queue<DashboardLogEntry> _logs = [];
    private readonly Queue<DashboardPingSample> _pingSamples = [];
    private const int MaxLogEntries = 250;
    private const int MaxPingSamples = 48;

    public event Action? Changed;

    public bool IsConnected { get; private set; }

    public bool IsPollingPaused { get; private set; }

    public bool IsReportNavigationOpen { get; private set; } = true;

    public GenerationReportTarget? ActiveReportTarget { get; private set; } = GenerationReportTarget.DISPATCH;

    public string BackendEndpoint { get; private set; } = "127.0.0.1:57206";

    public string? LastError { get; private set; }

    public DateTimeOffset? LastRefreshAt { get; private set; }

    public double? LastPingMilliseconds { get; private set; }

    public DateTimeOffset? LastPingAt { get; private set; }

    public bool HasApiKey { get; private set; }

    public IReadOnlyDictionary<GenerationReportTarget, DashboardReportSnapshot> Reports
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<GenerationReportTarget, DashboardReportSnapshot>(_reports);
            }
        }
    }

    public IReadOnlyList<DashboardPingSample> PingSamples
    {
        get
        {
            lock (_gate)
            {
                return [.. _pingSamples];
            }
        }
    }

    public IReadOnlyList<DashboardLogEntry> Logs
    {
        get
        {
            lock (_gate)
            {
                return [.. _logs];
            }
        }
    }

    public void SetEndpoint(string endpoint)
    {
        lock (_gate)
        {
            this.BackendEndpoint = endpoint;
        }

        this.NotifyChanged();
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            this.IsPollingPaused = paused;
        }

        this.NotifyChanged();
    }

    public void SetReportNavigationOpen(bool open)
    {
        lock (_gate)
        {
            this.IsReportNavigationOpen = open;
        }

        this.NotifyChanged();
    }

    public void SetActiveReportTarget(GenerationReportTarget? target)
    {
        lock (_gate)
        {
            this.ActiveReportTarget = target;
        }

        this.NotifyChanged();
    }

    public void SetApiKeyConfigured(bool configured)
    {
        lock (_gate)
        {
            this.HasApiKey = configured;
            if (!configured)
            {
                this.IsConnected = false;
                this.LastPingMilliseconds = null;
                this.LastPingAt = null;
            }
        }

        this.NotifyChanged();
    }

    public void Log(string level, string message)
    {
        lock (_gate)
        {
            _logs.Enqueue(new DashboardLogEntry(DateTimeOffset.Now, level, message));
            while (_logs.Count > MaxLogEntries)
            {
                _ = _logs.Dequeue();
            }
        }

        this.NotifyChanged();
    }

    public void ClearLogs()
    {
        lock (_gate)
        {
            _logs.Clear();
        }

        this.NotifyChanged();
    }

    public void MarkConnected()
    {
        lock (_gate)
        {
            this.IsConnected = true;
            this.LastError = null;
        }

        this.NotifyChanged();
    }

    public void MarkDisconnected(string? error)
    {
        lock (_gate)
        {
            this.IsConnected = false;
            this.LastError = error;
            this.LastPingMilliseconds = null;
            this.LastPingAt = null;
        }

        this.NotifyChanged();
    }

    public void UpdatePing(double milliseconds)
    {
        lock (_gate)
        {
            this.IsConnected = true;
            this.LastError = null;
            this.LastPingMilliseconds = milliseconds;
            this.LastPingAt = DateTimeOffset.Now;
            _pingSamples.Enqueue(new DashboardPingSample(this.LastPingAt.Value, milliseconds));
            while (_pingSamples.Count > MaxPingSamples)
            {
                _ = _pingSamples.Dequeue();
            }
        }

        this.NotifyChanged();
    }

    public void UpdateReport(DashboardReportSnapshot report)
    {
        lock (_gate)
        {
            _reports[report.Target] = report;
            this.IsConnected = true;
            this.LastError = null;
            this.LastRefreshAt = report.ReceivedAt;
        }

        this.NotifyChanged();
    }

    private void NotifyChanged() => this.Changed?.Invoke();
}
