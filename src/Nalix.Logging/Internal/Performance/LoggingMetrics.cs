// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System.Runtime.CompilerServices;

namespace Nalix.Logging.Internal.Performance;

/// <summary>
/// High-performance metrics collector for logging operations.
/// Tracks throughput, latency, and memory usage with minimal overhead.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class LoggingMetrics
{
    #region Fields

    private System.Int64 _totalLogsProcessed;
    private System.Int64 _totalBytesAllocated;
    private System.Int64 _totalFormattingTimeNs;
    private System.Int64 _peakMemoryBytes;

    private readonly System.Diagnostics.Stopwatch _uptime;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingMetrics"/> class.
    /// </summary>
    public LoggingMetrics()
    {
        _uptime = System.Diagnostics.Stopwatch.StartNew();
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Gets the total number of logs processed.
    /// </summary>
    public System.Int64 TotalLogsProcessed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Interlocked.Read(ref _totalLogsProcessed);
    }

    /// <summary>
    /// Gets the total bytes allocated for logging operations.
    /// </summary>
    public System.Int64 TotalBytesAllocated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Interlocked.Read(ref _totalBytesAllocated);
    }

    /// <summary>
    /// Gets the total time spent formatting logs in nanoseconds.
    /// </summary>
    public System.Int64 TotalFormattingTimeNs
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Interlocked.Read(ref _totalFormattingTimeNs);
    }

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public System.Int64 PeakMemoryBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Threading.Interlocked.Read(ref _peakMemoryBytes);
    }

    /// <summary>
    /// Gets the uptime of the metrics collector in milliseconds.
    /// </summary>
    public System.Int64 UptimeMs
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _uptime.ElapsedMilliseconds;
    }

    /// <summary>
    /// Gets the average logs per second.
    /// </summary>
    public System.Double LogsPerSecond
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            System.Int64 uptimeMs = UptimeMs;
            if (uptimeMs == 0)
            {
                return 0;
            }

            return (TotalLogsProcessed * 1000.0) / uptimeMs;
        }
    }

    /// <summary>
    /// Gets the average formatting time in microseconds per log.
    /// </summary>
    public System.Double AverageFormattingTimeMicros
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            System.Int64 total = TotalLogsProcessed;
            if (total == 0)
            {
                return 0;
            }

            return (TotalFormattingTimeNs / 1000.0) / total;
        }
    }

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Records a log processing operation.
    /// </summary>
    /// <param name="bytesAllocated">The number of bytes allocated during formatting.</param>
    /// <param name="formattingTimeNs">The time spent formatting in nanoseconds.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordLog(System.Int32 bytesAllocated, System.Int64 formattingTimeNs)
    {
        _ = System.Threading.Interlocked.Increment(ref _totalLogsProcessed);
        _ = System.Threading.Interlocked.Add(ref _totalBytesAllocated, bytesAllocated);
        _ = System.Threading.Interlocked.Add(ref _totalFormattingTimeNs, formattingTimeNs);
    }

    /// <summary>
    /// Updates the peak memory usage.
    /// </summary>
    /// <param name="currentMemoryBytes">The current memory usage in bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdatePeakMemory(System.Int64 currentMemoryBytes)
    {
        System.Int64 current;
        System.Int64 newPeak;

        do
        {
            current = System.Threading.Interlocked.Read(ref _peakMemoryBytes);
            if (currentMemoryBytes <= current)
            {
                return;
            }

            newPeak = currentMemoryBytes;
        }
        while (System.Threading.Interlocked.CompareExchange(ref _peakMemoryBytes, newPeak, current) != current);
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _ = System.Threading.Interlocked.Exchange(ref _totalLogsProcessed, 0);
        _ = System.Threading.Interlocked.Exchange(ref _totalBytesAllocated, 0);
        _ = System.Threading.Interlocked.Exchange(ref _totalFormattingTimeNs, 0);
        _ = System.Threading.Interlocked.Exchange(ref _peakMemoryBytes, 0);
        _uptime.Restart();
    }

    /// <summary>
    /// Gets a snapshot of current metrics.
    /// </summary>
    /// <returns>A dictionary containing all current metrics.</returns>
    public System.Collections.Generic.Dictionary<System.String, System.Object> GetSnapshot()
    {
        return new System.Collections.Generic.Dictionary<System.String, System.Object>
        {
            ["TotalLogsProcessed"] = TotalLogsProcessed,
            ["TotalBytesAllocated"] = TotalBytesAllocated,
            ["TotalFormattingTimeNs"] = TotalFormattingTimeNs,
            ["PeakMemoryBytes"] = PeakMemoryBytes,
            ["UptimeMs"] = UptimeMs,
            ["LogsPerSecond"] = LogsPerSecond,
            ["AverageFormattingTimeMicros"] = AverageFormattingTimeMicros
        };
    }

    /// <summary>
    /// Returns a formatted string representation of the metrics.
    /// </summary>
    /// <returns>A formatted string with current metrics.</returns>
    public override System.String ToString()
    {
        return $"[LoggingMetrics] " +
               $"Processed: {TotalLogsProcessed:N0} logs, " +
               $"Throughput: {LogsPerSecond:N2} logs/sec, " +
               $"Avg Latency: {AverageFormattingTimeMicros:N3} µs, " +
               $"Memory: {TotalBytesAllocated:N0} bytes allocated, " +
               $"Peak: {PeakMemoryBytes:N0} bytes, " +
               $"Uptime: {UptimeMs:N0} ms";
    }

    #endregion Public Methods
}
