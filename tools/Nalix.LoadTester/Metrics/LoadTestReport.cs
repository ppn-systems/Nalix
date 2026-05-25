// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.LoadTester.Metrics;

internal sealed class LoadTestReport
{
    public LoadTestReport(
        TimeSpan elapsed,
        TimeSpan measuredDuration,
        Int64 successfulRequests,
        Int64 failedRequests,
        Int64 timeoutErrors,
        Int64 socketErrors,
        Int64 otherErrors,
        Double averageLatencyMs,
        Double p50LatencyMs,
        Double p95LatencyMs,
        Double p99LatencyMs,
        Double p999LatencyMs)
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

    public Int64 SuccessfulRequests { get; }

    public Int64 FailedRequests { get; }

    public Int64 TimeoutErrors { get; }

    public Int64 SocketErrors { get; }

    public Int64 OtherErrors { get; }

    public Double AverageLatencyMs { get; }

    public Double P50LatencyMs { get; }

    public Double P95LatencyMs { get; }

    public Double P99LatencyMs { get; }

    public Double P999LatencyMs { get; }

    public Double RequestsPerSecond => this.MeasuredDuration.TotalSeconds > 0
        ? this.SuccessfulRequests / this.MeasuredDuration.TotalSeconds
        : 0;
}
