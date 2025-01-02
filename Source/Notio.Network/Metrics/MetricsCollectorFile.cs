using Notio.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Metrics;

public class MetricsCollectorFile : IDisposable
{
    private readonly ConcurrentQueue<string> _metricsQueue;
    private readonly CancellationTokenSource _cancellationSource;
    private readonly Task _processingTask;
    private readonly int _maxQueueSize;
    private bool _disposed;

    public MetricsCollectorFile()
    {
        _metricsQueue = new ConcurrentQueue<string>();
        _cancellationSource = new CancellationTokenSource();
        _maxQueueSize = 1000; // Giới hạn queue để tránh tràn bộ nhớ

        // Khởi động task xử lý ghi file
        _processingTask = ProcessMetricsQueueAsync(_cancellationSource.Token);
    }

    public void LogMetric(string metricType, string data)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MetricsCollectorFile));

        if (_metricsQueue.Count >= _maxQueueSize)
        {
            NotioLog.Instance.Trace("Metrics queue is full. Some metrics may be dropped.");
            return;
        }

        var metricEntry = FormatMetricEntry(metricType, data);
        _metricsQueue.Enqueue(metricEntry);
    }

    private static string FormatMetricEntry(string metricType, string data)
        => $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}|{metricType}|{data}";

    private async Task ProcessMetricsQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await MetricsFileWriter.WriteMetricsToFileAsync(_metricsQueue, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                NotioLog.Instance.Error("Error processing metrics queue", ex);
            }
        }

        // Ghi nốt metrics còn lại khi shutdown
        await MetricsFileWriter.WriteMetricsToFileAsync(_metricsQueue, CancellationToken.None);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cancellationSource.Cancel();
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            NotioLog.Instance.Error("Error during metrics collector shutdown", ex);
        }
        finally
        {
            _cancellationSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}