namespace Nalix.Dashboard.Application.Settings;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed class AdminSettings
{
    public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;

    /// <summary>
    /// Gets or sets the custom server host to connect to. If null or empty, the default host is used.
    /// </summary>
    public string? ServerHost { get; set; }

    /// <summary>
    /// Gets or sets the custom server port to connect to. If null, the default port is used.
    /// </summary>
    public ushort? ServerPort { get; set; }

    public int DefaultPollingIntervalMs { get; set; } = 3000;

    public Dictionary<string, int> PerPagePollingIntervalMs { get; set; } = [];

    public int PingIntervalMs { get; set; } = 5000;

    public int RequestTimeoutMs { get; set; } = 5000;

    public bool AutoReconnect { get; set; } = true;

    public int MaxReconnectAttempts { get; set; } = 10;

    public int ReconnectBackoffMinMs { get; set; } = 500;

    public int ReconnectBackoffMaxMs { get; set; } = 30000;

    public bool UseTls { get; set; }

    public string WebSocketPath { get; set; } = "/ws/";

    public bool SaveKeyForNextTime { get; set; }

    public bool ShowRawJsonDebug { get; set; }

    public bool CompactTableDensity { get; set; }

    public int ChartTimeWindowSeconds { get; set; } = 120;

    public int MaxChartSamples { get; set; } = 120;

    public int MaxLogEntries { get; set; } = 250;

    public int GetPollingInterval(string route)
    {
        if (PerPagePollingIntervalMs.TryGetValue(route, out int ms) && ms > 0)
        {
            return ms;
        }

        return DefaultPollingIntervalMs;
    }
}
