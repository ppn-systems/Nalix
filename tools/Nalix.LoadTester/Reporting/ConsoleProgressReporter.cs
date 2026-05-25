// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Nalix.LoadTester.Metrics;
using Nalix.LoadTester.Scenarios;

namespace Nalix.LoadTester.Reporting;

internal sealed class ConsoleProgressReporter
{
    public void WriteStart(LoadTestOptions options, ILoadScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scenario);

        Console.WriteLine("=========================================================");
        Console.WriteLine("         Nalix High-Performance Load Testing Tool         ");
        Console.WriteLine("=========================================================");
        Console.WriteLine($"Scenario           : {scenario.Name}");
        Console.WriteLine($"Target Host        : {options.Host}");
        Console.WriteLine($"Target Port        : {options.Port}");
        Console.WriteLine($"Concurrent Clients : {options.Connections}");
        Console.WriteLine($"Duration           : {options.DurationSeconds} seconds");
        Console.WriteLine($"Request Timeout    : {options.TimeoutMs} ms");
        Console.WriteLine($"Payload Size       : {options.PayloadSize} bytes");
        Console.WriteLine("---------------------------------------------------------");
    }

    public async Task ReportProgressAsync(
        Stopwatch stopwatch,
        MetricsCollector metrics,
        Int32 intervalSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stopwatch);
        ArgumentNullException.ThrowIfNull(metrics);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken).ConfigureAwait(false);
                Double elapsed = stopwatch.Elapsed.TotalSeconds;
                Int64 successful = metrics.SuccessfulRequests;
                Int64 failed = metrics.FailedRequests;
                Double currentRps = elapsed > 0 ? successful / elapsed : 0;

                Console.WriteLine($"[{elapsed:F0}s] Successful: {successful} | Failed: {failed} | Current RPS: {currentRps:F1}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void WriteFinal(LoadTestReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        Console.WriteLine();
        Console.WriteLine("---------------------------------------------------------");
        Console.WriteLine("Load Test Completed!");
        Console.WriteLine($"Elapsed Time       : {report.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Successful Requests: {report.SuccessfulRequests}");
        Console.WriteLine($"Failed Requests    : {report.FailedRequests}");

        if (report.FailedRequests > 0)
        {
            Console.WriteLine($"  -> Timeouts      : {report.TimeoutErrors}");
            Console.WriteLine($"  -> Socket Drops  : {report.SocketErrors}");
            Console.WriteLine($"  -> Other Errors  : {report.OtherErrors}");
        }

        Console.WriteLine($"RPS (Throughput)   : {report.RequestsPerSecond:F2} req/sec");
        Console.WriteLine($"Average Latency    : {report.AverageLatencyMs:F2} ms");
        Console.WriteLine($"P50 (Median)       : {report.P50LatencyMs:F2} ms");
        Console.WriteLine($"P95 Latency        : {report.P95LatencyMs:F2} ms");
        Console.WriteLine($"P99 Latency        : {report.P99LatencyMs:F2} ms");
        Console.WriteLine($"P99.9 Latency      : {report.P999LatencyMs:F2} ms");
        Console.WriteLine("=========================================================");
    }
}
