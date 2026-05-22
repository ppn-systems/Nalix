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
using Nalix.Framework.Memory.Pools;

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

        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot = this.COLLECT_STATE_SNAPSHOT(now, out int totalEndpoints, out int hardBlockedCount);

        try
        {
            return this.BUILD_REPORT_STRING(snapshot, totalEndpoints, hardBlockedCount, now);
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

        long now = Stopwatch.GetTimestamp();

        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot = this.COLLECT_STATE_SNAPSHOT(now, out int totalEndpoints, out int hardBlockedCount);
        try
        {
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
            writer.WriteNumber("HardBlockedCount", hardBlockedCount);

            writer.WriteStartArray("Endpoints");

            int count = 0;
            foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in snapshot)
            {
                if (count++ >= 20)
                {
                    break;
                }

                EndpointState state = kv.Value;
                long micro, blockedUntil;
                bool lockTaken = false;

                try
                {
                    state.SpinLock.Enter(ref lockTaken);
                    micro = state.MicroBalance;
                    blockedUntil = state.HardBlockedUntilSw;
                }
                finally
                {
                    if (lockTaken) { state.SpinLock.Exit(); }
                }

                bool isBlocked = blockedUntil > now;
                ushort credit = CALCULATE_REMAINING_CREDIT(micro, _options.TokenScale);

                writer.WriteStartObject();
                writer.WriteString("Endpoint", kv.Key.Address);
                writer.WriteBoolean("Blocked", isBlocked);
                writer.WriteNumber("Credit", credit);
                writer.WriteNumber("MicroBalance", micro);
                writer.WriteNumber("RetryAfterMs", isBlocked
                    ? this.CALCULATE_DELAY_MS(now, blockedUntil)
                    : (micro >= _options.TokenScale ? 0 : this.CALCULATE_RETRY_DELAY_MS(_options.TokenScale - micro)));
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


    #region Report Generation

    private List<KeyValuePair<INetworkEndpoint, EndpointState>> COLLECT_STATE_SNAPSHOT(long now, out int totalEndpoints, out int hardBlockedCount)
    {
        int currentCount = Interlocked.CompareExchange(ref _totalEndpointCount, 0, 0);
        int estimatedCapacity = currentCount > 0 ? currentCount : (_shards.Length * 8);
        int initialCapacity = Math.Max(_options.MinReportCapacity, estimatedCapacity);

        ListPool<KeyValuePair<INetworkEndpoint, EndpointState>> pool = ListPool<KeyValuePair<INetworkEndpoint, EndpointState>>.Instance;
        List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot = pool.Rent(minimumCapacity: initialCapacity);

        totalEndpoints = 0;
        hardBlockedCount = 0;

        // Collect snapshot from all shards
        foreach (Shard shard in _shards)
        {
            totalEndpoints += shard.Map.Count;

            foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in shard.Map)
            {
                snapshot.Add(kv);

                // Count hard-blocked during collection
                bool isBlocked;
                bool lockTaken = false;

                try
                {
                    kv.Value.SpinLock.Enter(ref lockTaken);
                    isBlocked = kv.Value.HardBlockedUntilSw > now;
                }
                finally
                {
                    if (lockTaken)
                    {
                        kv.Value.SpinLock.Exit();
                    }
                }

                if (isBlocked)
                {
                    hardBlockedCount++;
                }
            }
        }

        this.SORT_SNAPSHOT_BY_PRESSURE(snapshot, now);

        return snapshot;
    }

    private void SORT_SNAPSHOT_BY_PRESSURE(List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot, long now)
    {
        // BUG-23 fix: Snapshot state values BEFORE sorting to avoid O(N log N) lock
        // acquisitions inside the comparator. This prevents CPU DoS and lock contention
        // when the endpoint count is large.
        int count = snapshot.Count;
        if (count <= 1)
        {
            return;
        }

        // Pre-snapshot all state values under lock, once per endpoint
        long[] microValues = new long[count];
        bool[] blockedValues = new bool[count];

        for (int i = 0; i < count; i++)
        {
            EndpointState state = snapshot[i].Value;
            bool lockTaken = false;

            try
            {
                state.SpinLock.Enter(ref lockTaken);
                blockedValues[i] = state.HardBlockedUntilSw > now;
                microValues[i] = state.MicroBalance;
            }
            finally
            {
                if (lockTaken)
                {
                    state.SpinLock.Exit();
                }
            }
        }

        // Build index array and sort by index to avoid allocating tuples
        int[] indices = new int[count];
        for (int i = 0; i < count; i++)
        {
            indices[i] = i;
        }

        Array.Sort(indices, (ai, bi) =>
        {
            // Blocked endpoints first
            if (blockedValues[ai] != blockedValues[bi])
            {
                return blockedValues[bi].CompareTo(blockedValues[ai]);
            }

            // Then by deficit (bigger deficit = higher pressure)
            long aDef = this.CALCULATE_DEFICIT(microValues[ai]);
            long bDef = this.CALCULATE_DEFICIT(microValues[bi]);

            int cmpDef = bDef.CompareTo(aDef);
            if (cmpDef != 0)
            {
                return cmpDef;
            }

            // Tie-breaker by address
            return string.CompareOrdinal(snapshot[ai].Key.Address, snapshot[bi].Key.Address);
        });

        // Reorder snapshot in-place using sorted indices
        KeyValuePair<INetworkEndpoint, EndpointState>[] sorted = new KeyValuePair<INetworkEndpoint, EndpointState>[count];
        for (int i = 0; i < count; i++)
        {
            sorted[i] = snapshot[indices[i]];
        }

        for (int i = 0; i < count; i++)
        {
            snapshot[i] = sorted[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long CALCULATE_DEFICIT(long microBalance)
    {
        long clamped = microBalance < 0 ? 0 : microBalance > _capacityMicro ? _capacityMicro : microBalance;
        return _capacityMicro - clamped;
    }

    private string BUILD_REPORT_STRING(List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot, int totalEndpoints, int hardBlockedCount, long now)
    {
        StringBuilder sb = new();

        this.APPEND_REPORT_HEADER(sb, totalEndpoints, hardBlockedCount);
        this.APPEND_ENDPOINT_DETAILS(sb, snapshot, now);

        return sb.ToString();
    }

    private void APPEND_REPORT_HEADER(StringBuilder sb, int totalEndpoints, int hardBlockedCount)
    {
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
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"HardBlockedCount    : {hardBlockedCount}");
        _ = sb.AppendLine();
    }

    private void APPEND_ENDPOINT_DETAILS(StringBuilder sb, List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot, long now)
    {
        _ = sb.AppendLine("Top Endpoints by Pressure:");
        _ = sb.AppendLine("-------------------------------------------------------------------------------");
        _ = sb.AppendLine("Endpoint(Key)    | Blocked | Credit | MicroBalance/Capacity | RetryAfter(ms)");
        _ = sb.AppendLine("-------------------------------------------------------------------------------");

        if (snapshot.Count == 0)
        {
            _ = sb.AppendLine("(no endpoints tracked)");
        }
        else
        {
            this.APPEND_TOP_ENDPOINTS(sb, snapshot, now, maxCount: 20);
        }

        _ = sb.AppendLine("-------------------------------------------------------------------------------");
    }

    private void APPEND_TOP_ENDPOINTS(StringBuilder sb, List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot, long now, int maxCount)
    {
        int shown = 0;

        foreach (KeyValuePair<INetworkEndpoint, EndpointState> kv in snapshot)
        {
            if (shown++ >= maxCount)
            {
                break;
            }

            this.APPEND_ENDPOINT_ROW(sb, kv.Key, kv.Value, now);
        }
    }

    private void APPEND_ENDPOINT_ROW(StringBuilder sb, INetworkEndpoint key, EndpointState state, long now)
    {
        long micro, blockedUntil;

        bool lockTaken = false;
        try
        {
            state.SpinLock.Enter(ref lockTaken);
            micro = state.MicroBalance;
            blockedUntil = state.HardBlockedUntilSw;
        }
        finally
        {
            if (lockTaken)
            {
                state.SpinLock.Exit();
            }
        }

        bool isBlocked = blockedUntil > now;
        ushort credit = CALCULATE_REMAINING_CREDIT(micro, _options.TokenScale);
        int retryMs = this.CALCULATE_RETRY_FOR_REPORT(micro, isBlocked, blockedUntil, now);

        string keyCol = FORMAT_ENDPOINT_KEY(key.Address);
        string blockedCol = isBlocked ? "yes" : " no ";

        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"{keyCol} | {blockedCol}   | {credit,6} | {micro,10}/{_capacityMicro,-10} | {retryMs,12}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_RETRY_FOR_REPORT(long micro, bool isBlocked, long blockedUntil, long now)
    {
        if (isBlocked)
        {
            return this.CALCULATE_DELAY_MS(now, blockedUntil);
        }

        long needed = (micro >= _options.TokenScale) ? 0 : (_options.TokenScale - micro);
        return needed > 0 ? this.CALCULATE_RETRY_DELAY_MS(needed) : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FORMAT_ENDPOINT_KEY(string address)
    {
        const int MaxLength = 15;
        return address.Length > MaxLength
            ? (address[..MaxLength] + "…")
            : address.PadRight(MaxLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RETURN_SNAPSHOT_TO_POOL(List<KeyValuePair<INetworkEndpoint, EndpointState>> snapshot)
    {
        ListPool<KeyValuePair<INetworkEndpoint, EndpointState>> pool = ListPool<KeyValuePair<INetworkEndpoint, EndpointState>>.Instance;
        pool.Return(snapshot, clearItems: true);
    }

    #endregion Report Generation
}

