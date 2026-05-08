using Contracts;
using Dashboard.Domain.Logs;
using Dashboard.Domain.Metrics;
using Dashboard.Domain.Reports;

namespace Dashboard.Application.State;

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

    public bool IsConfigView { get; private set; }

    public GenerationReportTarget? ActiveReportTarget { get; private set; } = GenerationReportTarget.DISPATCH;

    public string BackendEndpoint { get; private set; } = "127.0.0.1:57206";

    public string BackendAddress { get; private set; } = "127.0.0.1";

    public int BackendPort { get; private set; } = 57206;

    public string? LastError { get; private set; }

    public DateTimeOffset? LastRefreshAt { get; private set; }

    public double? LastPingMilliseconds { get; private set; }

    public DateTimeOffset? LastPingAt { get; private set; }

    public bool HasApiKey { get; private set; }

    public int PollIntervalMs { get; private set; } = 250;

    public int PingIntervalMs { get; private set; } = 2000;

    public int RequestTimeoutMs { get; private set; } = 5000;

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
            this.EnqueueLogUnsafe("INFO", $"Dashboard endpoint configured endpoint={endpoint}.");
        }

        this.NotifyChanged();
    }

    public void SetBackendAddress(string address)
    {
        lock (_gate)
        {
            string trimmed = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            if (this.BackendAddress == trimmed)
            {
                return;
            }

            this.BackendAddress = trimmed;
            this.BackendEndpoint = $"{trimmed}:{this.BackendPort}";
            this.EnqueueLogUnsafe("INFO", $"Backend address changed value={trimmed}.");
        }

        this.NotifyChanged();
    }

    public void SetBackendPort(int port)
    {
        lock (_gate)
        {
            int clamped = Math.Clamp(port, 1, 65535);
            if (this.BackendPort == clamped)
            {
                return;
            }

            this.BackendPort = clamped;
            this.BackendEndpoint = $"{this.BackendAddress}:{clamped}";
            this.EnqueueLogUnsafe("INFO", $"Backend port changed value={clamped}.");
        }

        this.NotifyChanged();
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (this.IsPollingPaused == paused)
            {
                return;
            }

            this.IsPollingPaused = paused;
            this.EnqueueLogUnsafe("INFO", paused
                ? "Report polling paused; keepalive ping remains active."
                : "Report polling resumed.");
        }

        this.NotifyChanged();
    }

    public void SetReportNavigationOpen(bool open)
    {
        lock (_gate)
        {
            if (this.IsReportNavigationOpen == open)
            {
                return;
            }

            this.IsReportNavigationOpen = open;
            this.EnqueueLogUnsafe("DEBUG", open ? "Report navigation opened." : "Report navigation collapsed.");
        }

        this.NotifyChanged();
    }

    public void SetConfigView(bool isConfig)
    {
        lock (_gate)
        {
            if (this.IsConfigView == isConfig)
            {
                return;
            }

            this.IsConfigView = isConfig;
            if (isConfig)
            {
                this.ActiveReportTarget = null;
            }

            this.EnqueueLogUnsafe("DEBUG", isConfig ? "Config view opened." : "Config view closed.");
        }

        this.NotifyChanged();
    }

    public void SetActiveReportTarget(GenerationReportTarget? target)
    {
        lock (_gate)
        {
            if (this.ActiveReportTarget == target)
            {
                return;
            }

            this.ActiveReportTarget = target;
            this.IsConfigView = false;
            this.EnqueueLogUnsafe("DEBUG", target is null
                ? "Active view changed view=logs."
                : $"Active report target changed target={target}.");
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

    public void SetPollIntervalMs(int ms)
    {
        lock (_gate)
        {
            int clamped = Math.Clamp(ms, 100, 10000);
            if (this.PollIntervalMs == clamped)
            {
                return;
            }

            this.PollIntervalMs = clamped;
            this.EnqueueLogUnsafe("INFO", $"Poll interval changed value={clamped}ms.");
        }

        this.NotifyChanged();
    }

    public void SetPingIntervalMs(int ms)
    {
        lock (_gate)
        {
            int clamped = Math.Clamp(ms, 1000, 30000);
            if (this.PingIntervalMs == clamped)
            {
                return;
            }

            this.PingIntervalMs = clamped;
            this.EnqueueLogUnsafe("INFO", $"Ping interval changed value={clamped}ms.");
        }

        this.NotifyChanged();
    }

    public void SetRequestTimeoutMs(int ms)
    {
        lock (_gate)
        {
            int clamped = Math.Clamp(ms, 1000, 30000);
            if (this.RequestTimeoutMs == clamped)
            {
                return;
            }

            this.RequestTimeoutMs = clamped;
            this.EnqueueLogUnsafe("INFO", $"Request timeout changed value={clamped}ms.");
        }

        this.NotifyChanged();
    }

    public void Log(string level, string message)
    {
        lock (_gate)
        {
            this.EnqueueLogUnsafe(level, message);
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

    private void EnqueueLogUnsafe(string level, string message)
    {
        _logs.Enqueue(new DashboardLogEntry(
            DateTimeOffset.Now,
            NormalizeLevel(level),
            NormalizeMessage(message)));

        while (_logs.Count > MaxLogEntries)
        {
            _ = _logs.Dequeue();
        }
    }

    private static string NormalizeLevel(string? level)
    {
        string normalized = string.IsNullOrWhiteSpace(level)
            ? "INFO"
            : level.Trim().ToUpperInvariant();

        return normalized switch
        {
            "TRACE" or "DEBUG" or "INFO" or "WARN" or "ERROR" or "CRITICAL" => normalized,
            "INFORMATION" => "INFO",
            "WARNING" => "WARN",
            _ => "INFO"
        };
    }

    private static string NormalizeMessage(string? message)
        => string.IsNullOrWhiteSpace(message) ? "(empty log message)" : message.Trim();
}

