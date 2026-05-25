// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Nalix.LoadTester.Metrics;
using Nalix.LoadTester.Scenarios;

namespace Nalix.LoadTester.Reporting;

internal static class ReportExporter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public static async Task ExportAsync(
        String outputPath,
        LoadTestOptions options,
        ILoadScenario scenario,
        LoadTestReport report,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(report);

        String fullPath = Path.GetFullPath(outputPath);
        String? directory = Path.GetDirectoryName(fullPath);
        if (!String.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        String extension = Path.GetExtension(fullPath);
        String content = extension.ToUpperInvariant() switch
        {
            ".JSON" => WriteJson(options, scenario, report),
            ".CSV" => WriteCsv(options, scenario, report),
            ".MD" => WriteMarkdown(options, scenario, report),
            _ => throw new InvalidOperationException("Unsupported report extension.")
        };

        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static String WriteJson(LoadTestOptions options, ILoadScenario scenario, LoadTestReport report)
    {
        var payload = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            scenario = scenario.Name,
            target = new
            {
                host = options.Host,
                port = options.Port
            },
            workload = new
            {
                peakConnections = options.Connections,
                startConnections = options.RampUpSeconds > 0 ? options.StartConnections : options.Connections,
                rampUpSeconds = options.RampUpSeconds,
                warmupSeconds = options.WarmupSeconds,
                measuredDurationSeconds = options.DurationSeconds,
                cooldownSeconds = options.CooldownSeconds,
                timeoutMs = options.TimeoutMs,
                payloadSizeBytes = options.PayloadSize
            },
            results = new
            {
                totalRuntimeSeconds = report.Elapsed.TotalSeconds,
                measuredDurationSeconds = report.MeasuredDuration.TotalSeconds,
                successfulRequests = report.SuccessfulRequests,
                failedRequests = report.FailedRequests,
                timeoutErrors = report.TimeoutErrors,
                socketErrors = report.SocketErrors,
                otherErrors = report.OtherErrors,
                requestsPerSecond = report.RequestsPerSecond,
                averageLatencyMs = report.AverageLatencyMs,
                p50LatencyMs = report.P50LatencyMs,
                p95LatencyMs = report.P95LatencyMs,
                p99LatencyMs = report.P99LatencyMs,
                p999LatencyMs = report.P999LatencyMs
            }
        };

        return JsonSerializer.Serialize(payload, s_jsonOptions);
    }

    private static String WriteCsv(LoadTestOptions options, ILoadScenario scenario, LoadTestReport report)
    {
        String[] headers =
        [
            "generated_at_utc",
            "scenario",
            "host",
            "port",
            "peak_connections",
            "start_connections",
            "ramp_up_seconds",
            "warmup_seconds",
            "measured_duration_seconds",
            "cooldown_seconds",
            "timeout_ms",
            "payload_size_bytes",
            "total_runtime_seconds",
            "successful_requests",
            "failed_requests",
            "timeout_errors",
            "socket_errors",
            "other_errors",
            "requests_per_second",
            "average_latency_ms",
            "p50_latency_ms",
            "p95_latency_ms",
            "p99_latency_ms",
            "p999_latency_ms"
        ];

        String[] values =
        [
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            scenario.Name,
            options.Host,
            options.Port.ToString(CultureInfo.InvariantCulture),
            options.Connections.ToString(CultureInfo.InvariantCulture),
            (options.RampUpSeconds > 0 ? options.StartConnections : options.Connections).ToString(CultureInfo.InvariantCulture),
            options.RampUpSeconds.ToString(CultureInfo.InvariantCulture),
            options.WarmupSeconds.ToString(CultureInfo.InvariantCulture),
            options.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            options.CooldownSeconds.ToString(CultureInfo.InvariantCulture),
            options.TimeoutMs.ToString(CultureInfo.InvariantCulture),
            options.PayloadSize.ToString(CultureInfo.InvariantCulture),
            report.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture),
            report.SuccessfulRequests.ToString(CultureInfo.InvariantCulture),
            report.FailedRequests.ToString(CultureInfo.InvariantCulture),
            report.TimeoutErrors.ToString(CultureInfo.InvariantCulture),
            report.SocketErrors.ToString(CultureInfo.InvariantCulture),
            report.OtherErrors.ToString(CultureInfo.InvariantCulture),
            report.RequestsPerSecond.ToString("F2", CultureInfo.InvariantCulture),
            report.AverageLatencyMs.ToString("F2", CultureInfo.InvariantCulture),
            report.P50LatencyMs.ToString("F2", CultureInfo.InvariantCulture),
            report.P95LatencyMs.ToString("F2", CultureInfo.InvariantCulture),
            report.P99LatencyMs.ToString("F2", CultureInfo.InvariantCulture),
            report.P999LatencyMs.ToString("F2", CultureInfo.InvariantCulture)
        ];

        return String.Join(',', headers) + global::System.Environment.NewLine +
               String.Join(',', values.Select(EscapeCsv)) + global::System.Environment.NewLine;
    }

    private static String WriteMarkdown(LoadTestOptions options, ILoadScenario scenario, LoadTestReport report)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("# Nalix.LoadTester Report");
        _ = sb.AppendLine();
        AppendInvariant(sb, $"Generated UTC: `{DateTimeOffset.UtcNow:O}`");
        _ = sb.AppendLine();
        _ = sb.AppendLine("| Field | Value |");
        _ = sb.AppendLine("| --- | ---: |");
        AppendInvariant(sb, $"| Scenario | {scenario.Name} |");
        AppendInvariant(sb, $"| Target | {options.Host}:{options.Port} |");
        AppendInvariant(sb, $"| Peak Connections | {options.Connections} |");
        AppendInvariant(sb, $"| Start Connections | {(options.RampUpSeconds > 0 ? options.StartConnections : options.Connections)} |");
        AppendInvariant(sb, $"| Ramp-up Seconds | {options.RampUpSeconds} |");
        AppendInvariant(sb, $"| Warmup Seconds | {options.WarmupSeconds} |");
        AppendInvariant(sb, $"| Measured Duration Seconds | {report.MeasuredDuration.TotalSeconds:F2} |");
        AppendInvariant(sb, $"| Cooldown Seconds | {options.CooldownSeconds} |");
        AppendInvariant(sb, $"| Total Runtime Seconds | {report.Elapsed.TotalSeconds:F2} |");
        AppendInvariant(sb, $"| Successful Requests | {report.SuccessfulRequests} |");
        AppendInvariant(sb, $"| Failed Requests | {report.FailedRequests} |");
        AppendInvariant(sb, $"| Timeout Errors | {report.TimeoutErrors} |");
        AppendInvariant(sb, $"| Socket Errors | {report.SocketErrors} |");
        AppendInvariant(sb, $"| Other Errors | {report.OtherErrors} |");
        AppendInvariant(sb, $"| Requests/sec | {report.RequestsPerSecond:F2} |");
        AppendInvariant(sb, $"| Average Latency ms | {report.AverageLatencyMs:F2} |");
        AppendInvariant(sb, $"| P50 Latency ms | {report.P50LatencyMs:F2} |");
        AppendInvariant(sb, $"| P95 Latency ms | {report.P95LatencyMs:F2} |");
        AppendInvariant(sb, $"| P99 Latency ms | {report.P99LatencyMs:F2} |");
        AppendInvariant(sb, $"| P99.9 Latency ms | {report.P999LatencyMs:F2} |");

        return sb.ToString();
    }

    private static void AppendInvariant(StringBuilder builder, FormattableString value) =>
        builder.AppendLine(FormattableString.Invariant(value));

    private static String EscapeCsv(String value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
