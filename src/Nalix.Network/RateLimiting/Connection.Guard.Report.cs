// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Time;
using Nalix.Framework.Memory.Pools;
using Nalix.Network.Internal.Transport;

namespace Nalix.Network.RateLimiting;

public sealed partial class ConnectionGuard
{
    #region APIs

    /// <summary>
    /// Generates a human-readable diagnostic report of connection limiter state.
    /// </summary>
    /// <returns>Formatted report string.</returns>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public string GenerateReport()
    {
        List<
            KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = this.COLLECT_SNAPSHOT();

        try
        {
            SORT_SNAPSHOT_BY_LOAD(snapshot);
            return this.BUILD_REPORT(snapshot);
        }
        finally
        {
            RETURN_SNAPSHOT_TO_POOL(snapshot);
        }
    }

    /// <inheritdoc/>
    public void WriteReportData(System.Text.Json.Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = this.COLLECT_SNAPSHOT();
        try
        {
            SORT_SNAPSHOT_BY_LOAD(snapshot);
            GlobalMetrics metrics = this.CALCULATE_GLOBAL_METRICS(snapshot);

            writer.WriteStartObject();
            writer.WriteString("UtcNow", Clock.NowUtc());
            writer.WriteNumber("MaxPerEndpoint", _maxPerEndpoint);
            writer.WriteNumber("CleanupIntervalSeconds", _cleanupInterval.TotalSeconds);
            writer.WriteNumber("InactivityThresholdSeconds", _inactivityThreshold.TotalSeconds);
            writer.WriteNumber("TrackedEndpoints", metrics.TotalEndpoints);
            writer.WriteNumber("TotalConcurrent", metrics.TotalConcurrent);
            writer.WriteNumber("TotalAttempts", metrics.TotalAttempts);
            writer.WriteNumber("TotalRejections", metrics.TotalRejections);
            writer.WriteNumber("TotalCleaned", metrics.TotalCleaned);
            writer.WriteNumber("RejectionRate", metrics.TotalAttempts > 0 ? (metrics.TotalRejections * 100.0 / metrics.TotalAttempts) : 0.0);

            writer.WriteStartArray("TopEndpoints");
            int count = 0;
            foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitInfo> kvp in snapshot)
            {
                if (count++ >= 50)
                {
                    break;
                }

                ConnectionLimitInfo info = kvp.Value;
                writer.WriteStartObject();
                writer.WriteString("Address", kvp.Key.Address ?? "unknown");
                writer.WriteNumber("CurrentConnections", info.CurrentConnections);
                writer.WriteNumber("TotalConnectionsToday", info.TotalConnectionsToday);
                writer.WriteString("LastConnectionUtc", info.LastConnectionTime);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
        finally
        {
            RETURN_SNAPSHOT_TO_POOL(snapshot);
        }
    }

    #endregion APIs

    #region Report Generation

    /// <summary>
    /// Collects a point-in-time snapshot of all tracked endpoints.
    /// Reads Info under lock for consistency.
    /// </summary>
    private List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> COLLECT_SNAPSHOT()
    {
        int estimatedCapacity = Math.Clamp(_map.Count, MinReportCapacity, MaxReportCapacity);

        ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> pool = ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>>.Instance;
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot = pool.Rent(minimumCapacity: estimatedCapacity);

        foreach (KeyValuePair<SocketEndpoint, ConnectionLimitEntry> kvp in _map)
        {
            ConnectionLimitInfo info;
            bool lockTaken = false;
            try
            {
                kvp.Value.SpinLock.Enter(ref lockTaken);
                info = kvp.Value.Info;
            }
            finally
            {
                if (lockTaken)
                {
                    kvp.Value.SpinLock.Exit();
                }
            }
            snapshot.Add(new KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>(kvp.Key, info));
        }

        return snapshot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SORT_SNAPSHOT_BY_LOAD(
        List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        snapshot.Sort(static (a, b) =>
        {
            int byCurrent = b.Value.CurrentConnections.CompareTo(a.Value.CurrentConnections);
            return byCurrent != 0 ? byCurrent : b.Value.TotalConnectionsToday.CompareTo(a.Value.TotalConnectionsToday);
        });
    }

    private string BUILD_REPORT(List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        GlobalMetrics metrics = this.CALCULATE_GLOBAL_METRICS(snapshot);
        StringBuilder sb = new(512);
        this.APPEND_REPORT_HEADER(sb, metrics);
        APPEND_CONNECTION_DETAILS(sb, snapshot);
        return sb.ToString();
    }

    private readonly struct GlobalMetrics
    {
        public int TotalEndpoints { get; init; }
        public int TotalConcurrent { get; init; }
        public long TotalAttempts { get; init; }
        public long TotalRejections { get; init; }
        public long TotalCleaned { get; init; }
    }

    private GlobalMetrics CALCULATE_GLOBAL_METRICS(List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        int totalConcurrent = 0;
        foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitInfo> kvp in snapshot)
        {
            totalConcurrent += kvp.Value.CurrentConnections;
        }

        return new GlobalMetrics
        {
            TotalEndpoints = snapshot.Count,
            TotalConcurrent = totalConcurrent,
            TotalAttempts = Interlocked.Read(ref _totalConnectionAttempts),
            TotalRejections = Interlocked.Read(ref _totalRejections),
            TotalCleaned = Interlocked.Read(ref _totalCleanedEntries)
        };
    }

    private void APPEND_REPORT_HEADER(StringBuilder sb, GlobalMetrics metrics)
    {
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"[{Clock.NowUtc():yyyy-MM-dd HH:mm:ss}] ConnectionGuard Status:");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"MaxPerEndpoint     : {_maxPerEndpoint}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"CleanupInterval    : {_cleanupInterval.TotalSeconds:F0}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"InactivityThreshold: {_inactivityThreshold.TotalSeconds:F0}s");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TrackedEndpoints   : {metrics.TotalEndpoints}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalConcurrent    : {metrics.TotalConcurrent}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalAttempts      : {metrics.TotalAttempts:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalRejections    : {metrics.TotalRejections:N0}");
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"TotalCleaned       : {metrics.TotalCleaned:N0}");

        if (metrics.TotalAttempts > 0)
        {
            double rejectionRate = metrics.TotalRejections * 100.0 / metrics.TotalAttempts;
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"RejectionRate      : {rejectionRate:F2}%");
        }

        _ = sb.AppendLine();
    }

    private static void APPEND_CONNECTION_DETAILS(StringBuilder sb, List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        _ = sb.AppendLine("Top Endpoints by CurrentConnections:");
        _ = sb.AppendLine("---------------------------------------------------------------");
        _ = sb.AppendLine("Endpoint                   | Current | Today     | LastUtc     ");
        _ = sb.AppendLine("---------------------------------------------------------------");

        if (snapshot.Count == 0)
        {
            _ = sb.AppendLine("(no tracked endpoints)");
        }
        else
        {
            APPEND_TOP_ENDPOINTS(sb, snapshot, maxRows: 100);
        }

        _ = sb.AppendLine("---------------------------------------------------------------");
    }

    private static void APPEND_TOP_ENDPOINTS(StringBuilder sb, List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot, int maxRows)
    {
        int rows = 0;

        foreach (KeyValuePair<INetworkEndpoint, ConnectionLimitInfo> kvp in snapshot)
        {
            if (rows++ >= maxRows)
            {
                break;
            }

            string address = kvp.Key.Address ?? "unknown";
            ConnectionLimitInfo info = kvp.Value;

            string addressCol = address.Length > 27
                ? $"{address[..27]}\u2026"
                : address.PadRight(27);

            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{addressCol} | {info.CurrentConnections,7} | {info.TotalConnectionsToday,9} | {info.LastConnectionTime:u}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RETURN_SNAPSHOT_TO_POOL(List<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>> snapshot)
    {
        ListPool<KeyValuePair<INetworkEndpoint, ConnectionLimitInfo>>.Instance
            .Return(snapshot, clearItems: true);
    }

    #endregion Report Generation
}
