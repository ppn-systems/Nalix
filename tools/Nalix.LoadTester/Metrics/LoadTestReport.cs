// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Metrics;

internal sealed class LoadTestReport
{
    public LoadTestReport(
        TimeSpan elapsed,
        TimeSpan measuredDuration,
        long successfulRequests,
        long failedRequests,
        long timeoutErrors,
        long socketErrors,
        long otherErrors,
        double averageLatencyMs,
        double p50LatencyMs,
        double p95LatencyMs,
        double p99LatencyMs,
        double p999LatencyMs)
    {
        this.Elapsed = elapsed;
        this.MeasuredDuration = measuredDuration;
        this.SuccessfulRequests = successfulRequests;
        this.FailedRequests = failedRequests;
        this.TimeoutErrors = timeoutErrors;
        this.SocketErrors = socketErrors;
        this.OtherErrors = otherErrors;
        this.AverageLatencyMs = averageLatencyMs;
        this.P50LatencyMs = p50LatencyMs;
        this.P95LatencyMs = p95LatencyMs;
        this.P99LatencyMs = p99LatencyMs;
        this.P999LatencyMs = p999LatencyMs;
    }

    public TimeSpan Elapsed { get; }

    public TimeSpan MeasuredDuration { get; }

    public long SuccessfulRequests { get; }

    public long FailedRequests { get; }

    public long TimeoutErrors { get; }

    public long SocketErrors { get; }

    public long OtherErrors { get; }

    public double AverageLatencyMs { get; }

    public double P50LatencyMs { get; }

    public double P95LatencyMs { get; }

    public double P99LatencyMs { get; }

    public double P999LatencyMs { get; }

    public double RequestsPerSecond => this.MeasuredDuration.TotalSeconds > 0
        ? this.SuccessfulRequests / this.MeasuredDuration.TotalSeconds
        : 0;
}
