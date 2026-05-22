// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions.Exceptions;
using Nalix.Framework.Memory.Pools;

namespace Nalix.Framework.Memory.Objects;

public sealed partial class ObjectPoolManager
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TRIM_EXCESS_OBJECTS()
    {
        // Increment trim cycle counter (used for deep trim scheduling)
        int cycle = Interlocked.Increment(ref _trimCycleCount);
        bool isDeepTrim = this.SHOULD_RUN_DEEP_TRIM(cycle);

        if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
        {
            DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolTrimmed, new
            {
                Cycle = cycle,
                DeepTrim = isDeepTrim,
                Phase = "ObjectTrimRun"
            });
        }

        int totalRemoved = 0;

        // Take a snapshot to safely iterate while trimming (prevents CollectionModifiedException)
        foreach (KeyValuePair<Type, ObjectPool> kvp in _poolDict.ToArray())
        {
            Type type = kvp.Key;
            if (!_metricsDict.TryGetValue(type, out PoolMetrics? metrics))
            {
                continue;
            }

            int trimPercentage = this.CALCULATE_PER_TYPE_TRIM_PERCENTAGE(type, metrics, isDeepTrim);

            try
            {
                int removed = kvp.Value.Trim(trimPercentage);
                totalRemoved += removed;
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                // One pool failing must not crash the entire trim job
                if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolFailure))
                {
                    DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolFailure, new
                    {
                        Type = type.Name,
                        Error = ex.Message,
                        Phase = "TrimSinglePool"
                    });
                }
            }
        }

        if (totalRemoved > 0)
        {
            _ = Interlocked.Add(ref _totalTrimmedObjects, totalRemoved);

            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Memory.PoolTrimmed))
            {
                DiagnosticsEvents.Source.Write(DiagnosticsEvents.Memory.PoolTrimmed, new
                {
                    Cycle = cycle,
                    DeepTrim = isDeepTrim,
                    TotalRemoved = totalRemoved,
                });
            }
        }

        _ = this.PerformHealthCheck();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SHOULD_RUN_DEEP_TRIM(int cycle)
    {
        // Deep trim runs less frequently than normal trim (e.g. every 6 normal cycles if DeepTrimIntervalMinutes = 30)
        int deepEvery = Math.Max(1, _config.DeepTrimIntervalMinutes / Math.Max(1, _config.TrimIntervalMinutes));
        return cycle % deepEvery == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CALCULATE_PER_TYPE_TRIM_PERCENTAGE(Type type, PoolMetrics metrics, bool isDeepTrim)
    {
        if (isDeepTrim)
        {
            return _config.DeepTrimPercentage; // aggressive trim on deep cycle
        }

        if (metrics.TotalGets == 0)
        {
            // Pool has never been used → trim more aggressively
            return Math.Max(40, _config.BaseKeepPercentage + 15);
        }

        double hitRate = (double)metrics.CacheHits / metrics.TotalGets * 100.0;

        // Get current pool state (available count and capacity)
        int available = 0;
        int maxCap = this.DefaultMaxPoolSize;
        if (_poolDict.TryGetValue(type, out ObjectPool? pool))
        {
            Dictionary<string, object> info = pool.GetTypeInfoByType(type);
            maxCap = info.TryGetValue("MaxCapacity", out object? mc) ? Convert.ToInt32(mc, CultureInfo.InvariantCulture) : maxCap;
            available = info.TryGetValue("AvailableCount", out object? av) ? Convert.ToInt32(av, CultureInfo.InvariantCulture) : 0;
        }

        double freeRatio = maxCap > 0 ? (double)available / maxCap : 0.0;

        // === SAFETY FLOOR ===
        // Never trim below this threshold to prevent excessive churn and keep recovery fast
        int minKeep = Math.Max(_config.MinimumKeepObjects, maxCap / 12);
        if (available <= minKeep)
        {
            return 0; // already at minimum safe level
        }

        // === HOT POOL (high hit rate) → keep more objects ===
        if (hitRate >= _config.HotHitRateThreshold)
        {
            return 75; // light trim
        }

        // === COLD / UNHEALTHY / IDLE POOL ===
        bool needsAggressive = hitRate < (_config.HotHitRateThreshold - 20.0) || freeRatio > 0.78 || metrics.ConsecutiveFailures > 2;

        if (needsAggressive)
        {
            // Aggressive trim when cache is poor or too many idle objects
            return _config.BaseKeepPercentage + 27;
        }

        // Normal routine trim
        return _config.BaseKeepPercentage;
    }

}

