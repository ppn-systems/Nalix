using Notio.Logging;
using Notio.Shared;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Metrics;

public static class MetricsFileWriter
{
    public static async Task WriteMetricsToFileAsync(ConcurrentQueue<string> metricsQueue, CancellationToken cancellationToken)
    {
        if (metricsQueue.IsEmpty) return;

        string currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        string filePath = Path.Combine(DefaultDirectories.MetricPath, $"metrics_{currentDate}.log");
        string tempFilePath = Path.Combine(DefaultDirectories.TempPath, $"metrics_{currentDate}.tmp");

        StringBuilder metrics = new();
        while (metricsQueue.TryDequeue(out string? metric))
        {
            metrics.AppendLine(metric);
        }

        if (metrics.Length == 0) return;

        try
        {
            // Ghi vào file tạm trước
            await File.AppendAllTextAsync(tempFilePath, metrics.ToString(), cancellationToken);

            // Sau đó mới chuyển sang file chính
            if (File.Exists(tempFilePath))
            {
                string tempContent = await File.ReadAllTextAsync(tempFilePath, cancellationToken);
                await File.AppendAllTextAsync(filePath, tempContent, cancellationToken);
                File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            NotioLog.Instance.Error($"Error writing metrics to file", ex);

            foreach (string line in metrics.ToString().Split(Environment.NewLine))
                if (!string.IsNullOrEmpty(line)) metricsQueue.Enqueue(line);
        }
    }
}