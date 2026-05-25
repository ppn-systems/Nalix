// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace Nalix.LoadTester.Metrics;

internal sealed class MetricsCollector
{
    private readonly LatencySampleBuffer _latencySamples;
    private Int64 _successfulRequests;
    private Int64 _failedRequests;
    private Int64 _timeoutErrors;
    private Int64 _socketErrors;
    private Int64 _otherErrors;
    private Int64 _totalLatencyMs;
    private Int64 _measurementStartTimestamp;
    private Int64 _measuredTicks;
    private Int32 _isMeasuring;

    public MetricsCollector(LatencySampleBuffer latencySamples)
    {
        _latencySamples = latencySamples ?? throw new ArgumentNullException(nameof(latencySamples));
    }

    public Int64 SuccessfulRequests => Volatile.Read(ref _successfulRequests);

    public Int64 FailedRequests => Volatile.Read(ref _failedRequests);

    public Boolean IsMeasuring => Volatile.Read(ref _isMeasuring) != 0;

    public TimeSpan MeasuredElapsed
    {
        get
        {
            if (this.IsMeasuring)
            {
                Int64 started = Volatile.Read(ref _measurementStartTimestamp);
                return started > 0 ? Stopwatch.GetElapsedTime(started) : TimeSpan.Zero;
            }

            return TimeSpan.FromTicks(Volatile.Read(ref _measuredTicks));
        }
    }

    public void StartMeasurement()
    {
        Reset();
        Volatile.Write(ref _measurementStartTimestamp, Stopwatch.GetTimestamp());
        Volatile.Write(ref _isMeasuring, 1);
    }

    public void StopMeasurement()
    {
        if (Interlocked.Exchange(ref _isMeasuring, 0) == 0)
        {
            return;
        }

        Int64 started = Volatile.Read(ref _measurementStartTimestamp);
        if (started > 0)
        {
            Volatile.Write(ref _measuredTicks, Stopwatch.GetElapsedTime(started).Ticks);
        }
    }

    public void RecordSuccess(Double latencyMs)
    {
        if (!this.IsMeasuring)
        {
            return;
        }

        _ = Interlocked.Increment(ref _successfulRequests);
        _ = Interlocked.Add(ref _totalLatencyMs, (Int64)Math.Round(latencyMs));
        _latencySamples.Add(latencyMs);
    }

    public Int64 RecordFailure(ErrorKind kind)
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
        Int64 successful = Volatile.Read(ref _successfulRequests);
        Int64 failed = Volatile.Read(ref _failedRequests);
        Double averageLatency = successful > 0
            ? Volatile.Read(ref _totalLatencyMs) / (Double)successful
            : 0;

        Double[] samples = _latencySamples.Snapshot(out Int64 sampleCount);
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

    private static Double Percentile(Double[] samples, Int64 sampleCount, Double percentile)
    {
        if (sampleCount <= 0)
        {
            return 0;
        }

        Int32 index = (Int32)Math.Ceiling(sampleCount * percentile) - 1;
        index = Math.Clamp(index, 0, (Int32)sampleCount - 1);
        return samples[index];
    }
}
