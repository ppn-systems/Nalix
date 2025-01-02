using System;

namespace Notio.Network.Metrics;

public class MetricsFirewall
{
    private readonly MetricsCollectorFile _collector = new();
    private static readonly Lazy<MetricsFirewall> _instance = new(() => new());

    /// <summary>
    /// Lấy instance duy nhất của lớp MetricsFirewall.
    /// </summary>
    public static MetricsFirewall Instance => _instance.Value;

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