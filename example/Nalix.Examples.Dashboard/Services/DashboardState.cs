using Nalix.Examples.Contracts.Packets;

namespace Nalix.Examples.Dashboard.Services;

internal sealed class DashboardState
{
    private readonly Lock _gate = new();
    private readonly Dictionary<GenerationReportTarget, DashboardReportSnapshot> _reports = [];
    private readonly Queue<DashboardLogEntry> _logs = [];
    private const int MaxLogEntries = 250;

    public event Action? Changed;

    public bool IsConnected { get; private set; }

    public bool IsPollingPaused { get; private set; }

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
