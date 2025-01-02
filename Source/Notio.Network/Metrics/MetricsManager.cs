using System;

namespace Notio.Network.Metrics;

public class MetricsManager
{
    private readonly MetricsCollectorFile _collector = new();
    private static readonly Lazy<MetricsManager> _instance = new(() => new());

    /// <summary>
    /// Lấy instance duy nhất của lớp MetricsManager.
    /// </summary>
    public static MetricsManager Instance => _instance.Value;

    public void TrackBandwidthUsage(string endPoint, long bytes, bool isUpload)
    {
        string data = $"{endPoint}|{bytes}|{(isUpload ? "Upload" : "Download")}";
        _collector.LogMetric("Bandwidth", data);
    }

    public void TrackConnection(string endPoint, bool isNew)
    {
        string data = $"{endPoint}|{(isNew ? "New" : "Closed")}";
        _collector.LogMetric("Connection", data);
    }

    public void TrackRequest(string endPoint, bool success, int responseTime)
    {
        string data = $"{endPoint}|{success}|{responseTime}ms";
        _collector.LogMetric("Request", data);
    }
}