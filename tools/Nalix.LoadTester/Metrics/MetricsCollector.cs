// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Nalix.LoadTester.Metrics;

internal sealed class MetricsCollector
{
    private readonly LatencySampleBuffer _latencySamples;
    private long _successfulRequests;
    private long _failedRequests;
    private long _timeoutErrors;
    private long _socketErrors;
    private long _otherErrors;
    private long _totalLatencyMs;
    private long _measurementStartTimestamp;
    private long _measuredTicks;
    private int _isMeasuring;

    public MetricsCollector(LatencySampleBuffer latencySamples) => _latencySamples = latencySamples ?? throw new ArgumentNullException(nameof(latencySamples));

    public long SuccessfulRequests => Volatile.Read(ref _successfulRequests);

    public long FailedRequests => Volatile.Read(ref _failedRequests);

    public bool IsMeasuring => Volatile.Read(ref _isMeasuring) != 0;

    public TimeSpan MeasuredElapsed
    {
        get
        {
            if (this.IsMeasuring)
            {
                long started = Volatile.Read(ref _measurementStartTimestamp);
                return started > 0 ? Stopwatch.GetElapsedTime(started) : TimeSpan.Zero;
            }

            return TimeSpan.FromTicks(Volatile.Read(ref _measuredTicks));
        }
    }

    public void StartMeasurement()
    {
        this.Reset();
        Volatile.Write(ref _measurementStartTimestamp, Stopwatch.GetTimestamp());
        Volatile.Write(ref _isMeasuring, 1);
    }

    public void StopMeasurement()
    {
        if (Interlocked.Exchange(ref _isMeasuring, 0) == 0)
        {
            return;
        }

        long started = Volatile.Read(ref _measurementStartTimestamp);
        if (started > 0)
        {
            Volatile.Write(ref _measuredTicks, Stopwatch.GetElapsedTime(started).Ticks);
        }
    }

    public void RecordSuccess(double latencyMs)
    {
        if (!this.IsMeasuring)
        {
            return;
        }

        _ = Interlocked.Increment(ref _successfulRequests);
        _ = Interlocked.Add(ref _totalLatencyMs, (long)Math.Round(latencyMs));
        _latencySamples.Add(latencyMs);
    }

    public long RecordFailure(ErrorKind kind)
    {
        if (!this.IsMeasuring)
        {
            return 0;
        }

        _ = Interlocked.Increment(ref _failedRequests);
        return kind switch
        {
            ErrorKind.Timeout => Interlocked.Increment(ref _timeoutErrors),
            ErrorKind.Socket => Interlocked.Increment(ref _socketErrors),
            ErrorKind.Other => throw new NotImplementedException(),
            _ => Interlocked.Increment(ref _otherErrors)
        };
    }

    private void Reset()
    {
        Volatile.Write(ref _successfulRequests, 0);
        Volatile.Write(ref _failedRequests, 0);
        Volatile.Write(ref _timeoutErrors, 0);
        Volatile.Write(ref _socketErrors, 0);
        Volatile.Write(ref _otherErrors, 0);
        Volatile.Write(ref _totalLatencyMs, 0);
        Volatile.Write(ref _measuredTicks, 0);
        _latencySamples.Reset();
    }

    public LoadTestReport CreateReport(TimeSpan elapsed, TimeSpan measuredDuration)
    {
        long successful = Volatile.Read(ref _successfulRequests);
        long failed = Volatile.Read(ref _failedRequests);
        double averageLatency = successful > 0
            ? Volatile.Read(ref _totalLatencyMs) / (double)successful
            : 0;

        double[] samples = _latencySamples.Snapshot(out long sampleCount);
        return new LoadTestReport(
            elapsed,
            measuredDuration,
            successful,
            failed,
            Volatile.Read(ref _timeoutErrors),
            Volatile.Read(ref _socketErrors),
            Volatile.Read(ref _otherErrors),
            averageLatency,
            Percentile(samples, sampleCount, 0.50),
            Percentile(samples, sampleCount, 0.95),
            Percentile(samples, sampleCount, 0.99),
            Percentile(samples, sampleCount, 0.999));
    }

    private static double Percentile(double[] samples, long sampleCount, double percentile)
    {
        if (sampleCount <= 0)
        {
            return 0;
        }

        int index = (int)Math.Ceiling(sampleCount * percentile) - 1;
        index = Math.Clamp(index, 0, (int)sampleCount - 1);
        return samples[index];
    }
}
