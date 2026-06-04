// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Environment.Time;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Options;


#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Specifies the processing lane for a network callback to ensure fairness and prevent starvation.
/// </summary>
internal enum CallbackLane
{
    /// <summary>
    /// Lane for incoming data processing (OnFrameReceived).
    /// </summary>
    Process,
    /// <summary>
    /// Lane for post-send processing (OnFrameSent).
    /// </summary>
    Post
}

/// <summary>
/// Contains metrics about the AsyncCallback dispatcher.
/// </summary>
internal readonly struct AsyncCallbackMetrics
{
    public readonly int PendingProcess;
    public readonly int PendingPost;
    public readonly long Dropped;
    public readonly long Total;
    public readonly long SlowCallbacks;
    public readonly long FairnessCollisions;
    public readonly long FairnessEvictions;

    public AsyncCallbackMetrics(int pendingProcess, int pendingPost, long dropped, long total, long slowCallbacks, long fairnessCollisions, long fairnessEvictions)
    {
        PendingProcess = pendingProcess;
        PendingPost = pendingPost;
        Dropped = dropped;
        Total = total;
        SlowCallbacks = slowCallbacks;
        FairnessCollisions = fairnessCollisions;
        FairnessEvictions = fairnessEvictions;
    }
}

/// <summary>
/// Fire-and-forget dispatcher that offloads callbacks to the ThreadPool.
///
/// <para><b>Layer 2 — Two-priority dispatch:</b><br/>
/// Callbacks are split into two lanes:
/// <list type="bullet">
///   <item><b>High priority</b> — close/disconnect events. Always dispatched immediately,
///         not subject to backpressure limits.</item>
///   <item><b>Normal priority</b> — process/post-process packet events. Subject to the
///         global caps. Separated into Process and Post lanes.</item>
/// </list>
/// </para>
/// </summary>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class AsyncCallback
{
    #region Options

    private static readonly NetworkCallbackOptions s_netOpts = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();
    private static readonly PoolingOptions s_pooling = ConfigurationManager.Instance.Get<PoolingOptions>();

    #endregion Options

    #region Fields

    /// <summary>
    /// ── Global counters ────────────────────────────────────────────────────────
    /// </summary>
    private static int s_pendingProcess;
    private static int s_pendingPost;
    private static long s_totalInvoked;
    private static long s_droppedCallbacks;

    // Metrics
    private static long s_slowCallbacks;
    private static long s_fairnessCollisions;
    private static long s_fairnessEvictions;

    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    private static readonly ThrottleKey s_keyGlobalBackpressure = new("async.global_backpressure");
    private static readonly ThrottleKey s_keyPerIpBackpressure = new("async.per_ip_backpressure");
    private static readonly ThrottleKey s_keyHighBackpressure = new("async.high_backpressure");
    private static readonly ThrottleKey s_keyQueueException = new("async.queue_exception");
    private static readonly ThrottleKey s_keyCallbackError = new("async.callback_error");
    private static readonly ThrottleKey s_keySlowCallback = new("async.slow_callback");
    private static readonly ThrottleKey s_keyQueueFailed = new("async.queue_failed");

    static AsyncCallback()
    {
        s_pooling.Validate();

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledConnectEventContext>(s_pooling.ConnectEventContextCapacity);

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledConnectEventContext>(s_pooling.ConnectEventContextPreallocate);

        s_perIpPostMap = new long[s_netOpts.FairnessMapSize];
        s_perIpProcessMap = new long[s_netOpts.FairnessMapSize];
    }

    /// <summary>
    /// ── Per-IP pending counter ─────────────────────────────────────────────────
    /// </summary>
    private static readonly long[] s_perIpPostMap;
    private static readonly long[] s_perIpProcessMap;

    /// <summary>
    /// ── Static delegates — no closures, no per-invocation allocations ──────────
    /// </summary>
    private static readonly Action<object> s_invokeProcess = static stateObj =>
    {
        if (stateObj is not PooledConnectEventContext w)
        {
            return;
        }

        _ = Interlocked.Decrement(ref s_pendingProcess);

        INetworkEndpoint? endpoint = GET_ENDPOINT_SAFE(w.Args);
        RELEASE_ENDPOINT_SLOT(endpoint, CallbackLane.Process);
        EXECUTE_AND_RETURN(w);
    };

    private static readonly Action<object> s_invokePost = static stateObj =>
    {
        if (stateObj is not PooledConnectEventContext w)
        {
            return;
        }

        _ = Interlocked.Decrement(ref s_pendingPost);

        INetworkEndpoint? endpoint = GET_ENDPOINT_SAFE(w.Args);
        RELEASE_ENDPOINT_SLOT(endpoint, CallbackLane.Post);
        EXECUTE_AND_RETURN(w);
    };

    private static readonly Action<object> s_invokeHigh = static stateObj =>
    {
        if (stateObj is not PooledConnectEventContext w)
        {
            return;
        }
        // High-priority callbacks are not counted against any limit — just run.
        EXECUTE_AND_RETURN(w);
    };

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Schedules a <b>normal-priority</b> (process / post-process) callback on the ThreadPool.
    /// Subject to global and per-IP backpressure limits.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Invoke(
        EventHandler<IConnectEventArgs>? callback,
        object? sender,
        IConnectEventArgs args,
        CallbackLane lane,
        bool releasePendingPacketOnCompletion = false)
    {
        if (callback is null)
        {
#if DEBUG
            if (s_logger != null && s_logger.IsEnabled(LogLevel.Trace))
            {
                s_logger.LogTrace("[NW.AsyncCallback:Invoke] callback-null skipping");
            }
#endif
            return false;
        }

        if (!TRY_RESERVE_GLOBAL_SLOT(lane, out int globalPending))
        {
            Interlocked.Increment(ref s_droppedCallbacks);
            LOG_THROTTLED_ERROR_SAFE(args, s_keyGlobalBackpressure, $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] global-backpressure lane={lane} pending={globalPending} dropped={s_droppedCallbacks} ip={GET_ENDPOINT_SAFE(args)}");
            return false;
        }

        INetworkEndpoint? endpoint = GET_ENDPOINT_SAFE(args);
        if (!TRY_RESERVE_ENDPOINT_SLOT(endpoint, lane, out int ipPending))
        {
            RELEASE_GLOBAL_SLOT(lane);
            Interlocked.Increment(ref s_droppedCallbacks);
            int maxPerIp = lane == CallbackLane.Process ? s_netOpts.MaxPendingPerIp : s_netOpts.MaxPendingPostPerIp;
            LOG_THROTTLED_WARN_SAFE(args, s_keyPerIpBackpressure, $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] per-ip-backpressure lane={lane} ip={endpoint?.ToString() ?? "unknown"} pending={ipPending} max={maxPerIp}");
            return false;
        }

        int warnThreshold = s_netOpts.CallbackWarningThreshold;
        if (warnThreshold > 0 && globalPending >= warnThreshold && globalPending % 1_000 == 0)
        {
            int maxLane = lane == CallbackLane.Process ? s_netOpts.MaxPendingNormalCallbacks : s_netOpts.MaxPendingPostCallbacks;
            LOG_THROTTLED_WARN_SAFE(args, s_keyHighBackpressure, $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] high-backpressure lane={lane} pending={globalPending} max={maxLane}");
        }

        Interlocked.Increment(ref s_totalInvoked);

        Action<object> invoker = lane == CallbackLane.Process ? s_invokeProcess : s_invokePost;
        return QUEUE(invoker, callback, sender, args, isHigh: false, releasePendingPacketOnCompletion, lane);
    }

    /// <summary>
    /// Schedules a <b>high-priority</b> (close / disconnect) callback on the ThreadPool.
    /// </summary>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InvokeHighPriority(
        EventHandler<IConnectEventArgs>? callback,
        object? sender,
        IConnectEventArgs args)
    {
        if (callback is null)
        {
            return false;
        }

        _ = Interlocked.Increment(ref s_totalInvoked);

        return QUEUE(s_invokeHigh, callback, sender, args, isHigh: true, releasePendingPacketOnCompletion: false, CallbackLane.Process);
    }

    /// <summary>Gets diagnostic statistics about callback processing.</summary>
    public static AsyncCallbackMetrics GetStatistics()
        => new(
            Volatile.Read(ref s_pendingProcess),
            Volatile.Read(ref s_pendingPost),
            Volatile.Read(ref s_droppedCallbacks),
            Volatile.Read(ref s_totalInvoked),
            Volatile.Read(ref s_slowCallbacks),
            Volatile.Read(ref s_fairnessCollisions),
            Volatile.Read(ref s_fairnessEvictions));

    /// <summary>Resets dropped/total counters. Used for testing or periodic reporting.</summary>
    internal static void ResetStatistics()
    {
        _ = Interlocked.Exchange(ref s_droppedCallbacks, 0);
        _ = Interlocked.Exchange(ref s_totalInvoked, 0);
        _ = Interlocked.Exchange(ref s_slowCallbacks, 0);
        _ = Interlocked.Exchange(ref s_fairnessCollisions, 0);
        _ = Interlocked.Exchange(ref s_fairnessEvictions, 0);
    }

    #endregion Public Methods

    #region Private Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool QUEUE(
        Action<object> invoker,
        EventHandler<IConnectEventArgs> callback,
        object? sender,
        IConnectEventArgs args,
        bool isHigh,
        bool releasePendingPacketOnCompletion,
        CallbackLane lane)
    {
        if (sender is not IPooledConnectContextPool owner)
        {
            throw new InvalidOperationException("Invalid sender type.");
        }

        PooledConnectEventContext wrapper = owner.AcquireContext()
            ?? throw new InvalidOperationException("Failed to acquire context.");

        wrapper.Initialize(callback, sender, args, releasePendingPacketOnCompletion);

        bool queued = QUEUE_SAFE(invoker, wrapper, args, preferLocal: isHigh);

        if (!queued)
        {
            INetworkEndpoint? endpoint = GET_ENDPOINT_SAFE(args);
            if (!isHigh)
            {
                RELEASE_GLOBAL_SLOT(lane);
                RELEASE_ENDPOINT_SLOT(endpoint, lane);
            }

            _ = Interlocked.Increment(ref s_droppedCallbacks);
            LOG_THROTTLED_ERROR_SAFE(args, s_keyQueueFailed, $"[NW.{nameof(AsyncCallback)}] failed-queue-work-item ip={endpoint}");

            wrapper.Dispose();

            return false;
        }

        return queued;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool QUEUE_SAFE(Action<object> invoker, PooledConnectEventContext wrapper, IConnectEventArgs args, bool preferLocal)
    {
        try
        {
            return ThreadPool.UnsafeQueueUserWorkItem(invoker, wrapper, preferLocal);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LOG_THROTTLED_ERROR_SAFE(args, s_keyQueueException, $"[NW.{nameof(AsyncCallback)}] exception-queue-work-item", ex);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TRY_RESERVE_GLOBAL_SLOT(CallbackLane lane, out int pendingAfter)
    {
        ref int pending = ref (lane == CallbackLane.Process ? ref s_pendingProcess : ref s_pendingPost);
        int max = lane == CallbackLane.Process ? s_netOpts.MaxPendingNormalCallbacks : s_netOpts.MaxPendingPostCallbacks;

        while (true)
        {
            int observed = Volatile.Read(ref pending);
            if (observed >= max)
            {
                pendingAfter = observed;
                return false;
            }

            pendingAfter = observed + 1;
            if (Interlocked.CompareExchange(ref pending, pendingAfter, observed) == observed)
            {
                return true;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RELEASE_GLOBAL_SLOT(CallbackLane lane)
    {
        if (lane == CallbackLane.Process)
        {
            _ = Interlocked.Decrement(ref s_pendingProcess);
        }
        else
        {
            _ = Interlocked.Decrement(ref s_pendingPost);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TRY_RESERVE_ENDPOINT_SLOT(INetworkEndpoint? endpoint, CallbackLane lane, out int pendingAfter)
    {
        int hash = endpoint?.GetHashCode() ?? 0;
        long[] map = lane == CallbackLane.Process ? s_perIpProcessMap : s_perIpPostMap;
        int max = lane == CallbackLane.Process ? s_netOpts.MaxPendingPerIp : s_netOpts.MaxPendingPostPerIp;

        int startIndex = (hash & 0x7FFFFFFF) % map.Length;

        for (int probe = 0; probe < 12; probe++) // Increased probe depth
        {
            int index = (startIndex + probe) % map.Length;
            if (probe > 0)
            {
                _ = Interlocked.Increment(ref s_fairnessCollisions);
            }

            while (true)
            {
                long current = Volatile.Read(ref map[index]);
                int currentHash = (int)(current >> 32);
                int currentCount = (int)current;

                if (currentCount > 0 && currentHash != hash)
                {
                    break; // Collision, try next slot
                }

                if (currentCount >= max)
                {
                    pendingAfter = currentCount;
                    return false; // Rate limited
                }

                long newValue = ((long)hash << 32) | (uint)(currentCount + 1);
                if (Interlocked.CompareExchange(ref map[index], newValue, current) == current)
                {
                    pendingAfter = currentCount + 1;
                    return true;
                }
            }
        }

        _ = Interlocked.Increment(ref s_fairnessEvictions);
        pendingAfter = max;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RELEASE_ENDPOINT_SLOT(INetworkEndpoint? endpoint, CallbackLane lane)
    {
        int hash = endpoint?.GetHashCode() ?? 0;
        long[] map = lane == CallbackLane.Process ? s_perIpProcessMap : s_perIpPostMap;
        int startIndex = (hash & 0x7FFFFFFF) % map.Length;

        for (int probe = 0; probe < 12; probe++) // Match new probe depth
        {
            int index = (startIndex + probe) % map.Length;
            while (true)
            {
                long current = Volatile.Read(ref map[index]);
                int currentHash = (int)(current >> 32);
                int currentCount = (int)current;

                if (currentCount == 0)
                {
                    break; // Empty slot, continue probing because it might have been freed!
                }

                if (currentHash != hash)
                {
                    break; // Not our slot, continue probing
                }

                long newValue = ((long)hash << 32) | (uint)(currentCount - 1);
                if (Interlocked.CompareExchange(ref map[index], newValue, current) == current)
                {
                    return;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IConnection? GET_CONNECTION_SAFE(IConnectEventArgs? args)
    {
        if (args is null)
        {
            return null;
        }

        try { return args.Connection; }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { return null; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static INetworkEndpoint? GET_ENDPOINT_SAFE(IConnectEventArgs? args)
    {
        if (args is null)
        {
            return null;
        }

        try { return args.NetworkEndpoint; }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { return null; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LOG_THROTTLED_WARN_SAFE(IConnectEventArgs? args, ThrottleKey key, string message)
    {
        IConnection? connection = GET_CONNECTION_SAFE(args);
        if (connection is not null)
        {
            connection.ThrottledWarn(s_logger, key, message);
            return;
        }

        if (s_logger != null && s_logger.IsEnabled(LogLevel.Warning))
        {
            s_logger.LogWarning(message);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LOG_THROTTLED_ERROR_SAFE(IConnectEventArgs? args, ThrottleKey key, string message, Exception? ex = null)
    {
        IConnection? connection = GET_CONNECTION_SAFE(args);
        if (connection is not null)
        {
            if (ex is null)
            {
                connection.ThrottledError(s_logger, key, message);
            }
            else
            {
                connection.ThrottledError(s_logger, key, message, ex);
            }

            return;
        }

        if (s_logger != null && s_logger.IsEnabled(LogLevel.Error))
        {
            if (ex is null)
            {
                s_logger.LogError(message);
            }
            else
            {
                s_logger.LogError(ex, message);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EXECUTE_AND_RETURN(PooledConnectEventContext w)
    {
        IConnectEventArgs? args = w.Args;

        long startMonoTicks = Clock.MonoTicksNow();
        try
        {
            w.Callback?.Invoke(w.Sender, args);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LOG_THROTTLED_ERROR_SAFE(args, s_keyCallbackError, $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-error", ex);
        }
        finally
        {
            double elapsedMs = Clock.MonoTicksToMilliseconds(Clock.MonoTicksNow() - startMonoTicks);
            if (elapsedMs > s_netOpts.MaxCallbackExecutionMs)
            {
                _ = Interlocked.Increment(ref s_slowCallbacks);
                LOG_THROTTLED_WARN_SAFE(args, s_keySlowCallback, $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] slow-callback execution_ms={elapsedMs:F2} limit_ms={s_netOpts.MaxCallbackExecutionMs} ip={GET_ENDPOINT_SAFE(args)}");
            }

            if (w.ReleasePendingPacketOnCompletion && w.Sender is IPooledConnectContextPool owner)
            {
                owner.ReleasePendingPacket();
            }

            w.Dispose();
        }
    }

    #endregion Private Helpers
}
