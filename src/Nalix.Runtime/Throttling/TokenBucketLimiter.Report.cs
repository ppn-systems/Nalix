// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Nalix.Runtime.Throttling;

public sealed partial class TokenBucketLimiter
{
    /// <summary>
    /// Generates a human-readable diagnostic report of the limiter state.
    /// </summary>
    /// <returns>Formatted string report.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        long now = Stopwatch.GetTimestamp();

        this.COLLECT_STATE_SNAPSHOT(now, out int totalEndpoints, out int hardBlockedCount, out int softWarningCount);

        return this.BUILD_REPORT_STRING(totalEndpoints, hardBlockedCount, softWarningCount);
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        long now = Stopwatch.GetTimestamp();

        this.COLLECT_STATE_SNAPSHOT(now, out int totalEndpoints, out int hardBlockedCount, out int softWarningCount);

        writer.WriteStartObject();
        writer.WriteString("UtcNow", DateTime.UtcNow);
        writer.WriteNumber("CapacityTokens", _options.CapacityTokens);
        writer.WriteNumber("RefillPerSecond", _options.RefillTokensPerSecond);
        writer.WriteNumber("TokenScale", _options.TokenScale);
        writer.WriteNumber("Shards", _options.ShardCount);
        writer.WriteNumber("HardLockoutSeconds", _options.HardLockoutSeconds);
        writer.WriteNumber("StaleEntrySeconds", _options.StaleEntrySeconds);
        writer.WriteNumber("CleanupIntervalSecs", _options.CleanupIntervalSeconds);
        writer.WriteNumber("MaxTrackedEndpoints", _options.MaxTrackedEndpoints);
        writer.WriteNumber("TrackedEndpoints", totalEndpoints);
        writer.WriteNumber("PeakTrackedEndpoints", Volatile.Read(ref _peakTrackedEndpoints));
        writer.WriteNumber("HardBlockedCount", hardBlockedCount);
        writer.WriteNumber("SoftWarningCount", softWarningCount);
        writer.WriteNumber("TotalCleaned", Volatile.Read(ref _totalCleaned));
        writer.WriteEndObject();
    }


    #region Report Generation

    private void COLLECT_STATE_SNAPSHOT(long now, out int totalEndpoints, out int hardBlockedCount, out int softWarningCount)
    {
        totalEndpoints = 0;
        hardBlockedCount = 0;
        softWarningCount = 0;

        // Collect stats from all shards (Zero Allocation & Lock-Free approximation)
        foreach (Shard shard in _shards)
        {
            totalEndpoints += shard.Map.Count;

            foreach (EndpointState state in shard.Map.Values)
            {
                // Unlocked read for telemetry
                if (Volatile.Read(ref state.HardBlockedUntilSw) > now)
                {
                    hardBlockedCount++;
                }
                else if (Volatile.Read(ref state.SoftViolations) > 0)
                {
                    softWarningCount++;
                }
            }
        }

        int currentEndpoints = totalEndpoints;
        if (currentEndpoints > Volatile.Read(ref _peakTrackedEndpoints))
        {
            int currentPeak;
            while (currentEndpoints > (currentPeak = Volatile.Read(ref _peakTrackedEndpoints)))
            {
                if (Interlocked.CompareExchange(ref _peakTrackedEndpoints, currentEndpoints, currentPeak) == currentPeak)
                {
                    break;
                }
            }
        }
    }

    private string BUILD_REPORT_STRING(int totalEndpoints, int hardBlockedCount, int softWarningCount)
    {
        StringBuilder sb = new();

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] TokenBucketLimiter Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CapacityTokens      :  {_options.CapacityTokens}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RefillPerSecond     : {_options.RefillTokensPerSecond}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TokenScale          : {_options.TokenScale}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Shards              : {_options.ShardCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"HardLockoutSeconds  : {_options.HardLockoutSeconds}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"StaleEntrySeconds   : {_options.StaleEntrySeconds}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CleanupIntervalSecs : {_options.CleanupIntervalSeconds}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MaxTrackedEndpoints : {_options.MaxTrackedEndpoints}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TrackedEndpoints    : {totalEndpoints}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"PeakTrackedEndpoints: {Volatile.Read(ref _peakTrackedEndpoints)}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"HardBlockedCount    : {hardBlockedCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"SoftWarningCount    : {softWarningCount}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalCleaned        : {Volatile.Read(ref _totalCleaned)}");
        _ = sb.AppendLine();

        return sb.ToString();
    }

    #endregion Report Generation
}
