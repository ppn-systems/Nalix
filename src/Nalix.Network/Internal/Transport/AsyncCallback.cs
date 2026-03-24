// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Options;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Fire-and-forget dispatcher that offloads callbacks to the ThreadPool.
///
/// <para><b>Layer 2 — Two-priority dispatch:</b><br/>
/// Callbacks are split into two lanes:
/// <list type="bullet">
///   <item><b>High priority</b> — close/disconnect events. Always dispatched immediately,
///         not subject to backpressure limits. This ensures connections are cleaned up even
///         when the server is under heavy load.</item>
///   <item><b>Normal priority</b> — process/post-process packet events. Subject to the
///         global <see cref="NetworkCallbackOptions.MaxPendingNormalCallbacks"/> cap. When the cap is reached, new normal
///         callbacks are dropped and a warning is logged. Callers (e.g.
///         <see cref="SocketConnection"/>) enforce per-connection limits (Layer 1)
///         so legitimate connections are not affected by a single flooding IP.</item>
/// </list>
/// </para>
///
/// <para><b>Per-IP fairness tracking:</b><br/>
/// The dispatcher counts how many normal callbacks are currently pending per remote-IP string.
/// If a single IP exceeds <see cref="NetworkCallbackOptions.MaxPendingPerIp"/>, its callbacks are dropped
/// individually — regardless of how much global headroom remains. This prevents one
/// attacker IP from monopolising the global callback quota and starving other IPs.</para>
/// </summary>
[DebuggerNonUserCode]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class AsyncCallback
{
    #region Options

    /// <summary>
    /// Loaded once at startup from NetworkCallbackOptions via ConfigurationManager.
    /// All throttle values are read from config so they can be tuned without recompile.
    /// </summary>
    private static readonly NetworkCallbackOptions s_opts = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();
    private static readonly PoolingOptions s_pooling = ConfigurationManager.Instance.Get<PoolingOptions>();

    #endregion Options

    #region Fields

    /// <summary>
    /// ── Global counters ────────────────────────────────────────────────────────
    /// </summary>
    private static int s_pendingNormal;
    private static long s_totalInvoked;
    private static long s_droppedCallbacks;

    private static readonly ILogger? s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    static AsyncCallback()
    {
        s_pooling.Validate();

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .SetMaxCapacity<PooledConnectEventContext>(s_pooling.ConnectEventContextCapacity);

        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                    .Prealloc<PooledConnectEventContext>(s_pooling.ConnectEventContextPreallocate);

        s_perIpFairnessMap = new int[s_opts.FairnessMapSize];
    }

    /// <summary>
    /// ── Per-IP pending counter ─────────────────────────────────────────────────
    /// Uses a fixed-size hash map (open addressing / collisions managed by the same slot)
    /// to avoid Node allocations in ConcurrentDictionary.
    /// Capacity is configurable via <see cref="NetworkCallbackOptions.FairnessMapSize"/>.
    /// </summary>
    private static readonly int[] s_perIpFairnessMap;

    /// <summary>
    /// ── Static delegates — no closures, no per-invocation allocations ──────────
    /// </summary>
    private static readonly Action<object> s_invokeNormal = static stateObj =>
    {
        if (stateObj is not PooledConnectEventContext w)
        {
            return;
        }

        // Decrement global counter first.
        _ = Interlocked.Decrement(ref s_pendingNormal);

        // Decrement the per-IP counter only after the callback actually runs.
        // Once the count reaches zero, remove the key so the fairness map stays
        // bounded and a short-lived remote endpoint does not leave stale entries.
        INetworkEndpoint? endpoint = GET_ENDPOINT_SAFE(w.Args);
        if (endpoint is not null)
        {
            RELEASE_ENDPOINT_SLOT(endpoint);
        }

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
    /// <param name="callback">The event handler to invoke.</param>
    /// <param name="sender">The sender object (typically an <see cref="IConnection"/>).</param>
    /// <param name="args">The event arguments.</param>
    /// <param name="releasePendingPacketOnCompletion">
    /// <see langword="true"/> when the callback corresponds to a receive-path
    /// packet that already reserved one per-connection pending slot.
    /// </param>
    /// <returns><see langword="true"/> if the callback was queued; <see langword="false"/> if dropped.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Invoke(
        EventHandler<IConnectEventArgs>? callback,
        object? sender,
        IConnectEventArgs args,
        bool releasePendingPacketOnCompletion = false)
    {
        if (callback is null)
        {
#if DEBUG
            s_logger?.Trace($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-null skipping");
#endif
            return false;
        }

        if (!TRY_RESERVE_GLOBAL_SLOT(out int globalPending))
        {
            // Drop the callback before queuing work so one overloaded server path
            // cannot keep piling up work items and consuming the entire normal lane.
            Interlocked.Increment(ref s_droppedCallbacks);
            LOG_THROTTLED_ERROR_SAFE(args, "async.global_backpressure", $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] global-backpressure pending={globalPending} dropped={s_droppedCallbacks} ip={GET_ENDPOINT_SAFE(args)}");
            return false;
        }

        // Per-IP fairness is checked before the work item is queued so one remote
        // endpoint cannot reserve the global pool faster than others and starve
        // healthy peers.
        INetworkEndpoint? endpoint = GET_ENDPOINT_SAFE(args);
        if (endpoint is not null)
        {
            if (!TRY_RESERVE_ENDPOINT_SLOT(endpoint, out int ipPending))
            {
                // Apply per-IP fairness first so a single remote endpoint cannot
                // monopolize the queue even if there is still global headroom.
                RELEASE_GLOBAL_SLOT();
                Interlocked.Increment(ref s_droppedCallbacks);
                LOG_THROTTLED_WARN_SAFE(args, "async.per_ip_backpressure", $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] per-ip-backpressure ip={endpoint} pending={ipPending} max={s_opts.MaxPendingPerIp}");
                return false;
            }
        }

        // ── Warn when approaching global limit ────────────────────────────────
        int warnThreshold = s_opts.CallbackWarningThreshold;
        if (warnThreshold > 0 && globalPending >= warnThreshold && globalPending % 1_000 == 0)
        {
            LOG_THROTTLED_WARN_SAFE(args, "async.high_backpressure", $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] high-backpressure pending={globalPending} max={s_opts.MaxPendingNormalCallbacks}");
        }

        Interlocked.Increment(ref s_totalInvoked);

        return QUEUE(s_invokeNormal, callback, sender, args, isHigh: false, releasePendingPacketOnCompletion);
    }

    /// <summary>
    /// Schedules a <b>high-priority</b> (close / disconnect) callback on the ThreadPool.
    /// <para>
    /// High-priority callbacks bypass all backpressure limits so that connection cleanup
    /// is never delayed by a flood of normal-priority callbacks. The counter
    /// <see cref="s_pendingNormal"/> is <b>not</b> incremented for these callbacks.
    /// </para>
    /// </summary>
    /// <param name="callback">The event handler to invoke.</param>
    /// <param name="sender">The sender object.</param>
    /// <param name="args">The event arguments.</param>
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

        return QUEUE(s_invokeHigh, callback, sender, args, isHigh: true, releasePendingPacketOnCompletion: false);
    }

    /// <summary>Gets diagnostic statistics about callback processing.</summary>
    public static (int PendingNormal, long Dropped, long Total) GetStatistics()
        => (Volatile.Read(ref s_pendingNormal),
            Volatile.Read(ref s_droppedCallbacks),
            Volatile.Read(ref s_totalInvoked));

    /// <summary>Resets dropped/total counters. Used for testing or periodic reporting.</summary>
    internal static void ResetStatistics()
    {
        _ = Interlocked.Exchange(ref s_droppedCallbacks, 0);
        _ = Interlocked.Exchange(ref s_totalInvoked, 0);
        // Do NOT reset s_pendingNormal — it tracks live work.
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
        bool releasePendingPacketOnCompletion)
    {
        PooledConnectEventContext? wrapper = (sender as Connection)?.AcquireContext() ?? PooledConnectEventContext.Get();
        wrapper.Initialize(callback, sender, args, releasePendingPacketOnCompletion);

        if (!ThreadPool.UnsafeQueueUserWorkItem(invoker, wrapper, preferLocal: false))
        {
            INetworkEndpoint? endpoint = GET_ENDPOINT_SAFE(args);
            // Queue failure — extremely rare. Undo the increments we already applied.
            if (!isHigh)
            {
                // Roll back the reservation so counters stay consistent if the queue
                // rejects the work item after we already reserved capacity.
                RELEASE_GLOBAL_SLOT();

                if (endpoint is not null)
                {
                    RELEASE_ENDPOINT_SLOT(endpoint);
                }
            }

            _ = Interlocked.Increment(ref s_droppedCallbacks);
            LOG_THROTTLED_ERROR_SAFE(args, "async.queue_failed", $"[NW.{nameof(AsyncCallback)}] failed-queue-work-item ip={endpoint}");

            wrapper.Dispose();

            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TRY_RESERVE_GLOBAL_SLOT(out int pendingAfter)
    {
        while (true)
        {
            int observed = Volatile.Read(ref s_pendingNormal);
            if (observed >= s_opts.MaxPendingNormalCallbacks)
            {
                pendingAfter = observed;
                return false;
            }

            pendingAfter = observed + 1;
            if (Interlocked.CompareExchange(ref s_pendingNormal, pendingAfter, observed) == observed)
            {
                return true;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RELEASE_GLOBAL_SLOT() => _ = Interlocked.Decrement(ref s_pendingNormal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TRY_RESERVE_ENDPOINT_SLOT(INetworkEndpoint endpoint, out int pendingAfter)
    {
        // Use the endpoint's native GetHashCode which is already optimized for IP-only 
        // hashing in SocketEndpoint. This avoids calling .Address which allocates 
        // a string and multiple temporary objects per packet.
        int index = (endpoint.GetHashCode() & 0x7FFFFFFF) % s_perIpFairnessMap.Length;

        while (true)
        {
            int current = Volatile.Read(ref s_perIpFairnessMap[index]);
            if (current >= s_opts.MaxPendingPerIp)
            {
                pendingAfter = current;
                return false;
            }

            pendingAfter = current + 1;
            if (Interlocked.CompareExchange(ref s_perIpFairnessMap[index], pendingAfter, current) == current)
            {
                return true;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RELEASE_ENDPOINT_SLOT(INetworkEndpoint endpoint)
    {
        int index = (endpoint.GetHashCode() & 0x7FFFFFFF) % s_perIpFairnessMap.Length;
        _ = Interlocked.Decrement(ref s_perIpFairnessMap[index]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IConnection? GET_CONNECTION_SAFE(IConnectEventArgs? args)
    {
        if (args is null)
        {
            return null;
        }

        try
        {
            return args.Connection;
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static INetworkEndpoint? GET_ENDPOINT_SAFE(IConnectEventArgs? args)
    {
        if (args is null)
        {
            return null;
        }

        try
        {
            return args.NetworkEndpoint;
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LOG_THROTTLED_WARN_SAFE(IConnectEventArgs? args, string key, string message)
    {
        IConnection? connection = GET_CONNECTION_SAFE(args);
        if (connection is not null)
        {
            connection.ThrottledWarn(s_logger, key, message);
            return;
        }

        s_logger?.Warn(message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LOG_THROTTLED_ERROR_SAFE(IConnectEventArgs? args, string key, string message, Exception? ex = null)
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

        if (ex is null)
        {
            s_logger?.Error(message);
        }
        else
        {
            s_logger?.Error(message, ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EXECUTE_AND_RETURN(PooledConnectEventContext w)
    {
        IConnectEventArgs? args = w.Args;

        try
        {
            w.Callback?.Invoke(w.Sender, args);
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            LOG_THROTTLED_ERROR_SAFE(args, "async.callback_error", $"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-error", ex);
        }
        finally
        {
            if (w.ReleasePendingPacketOnCompletion && w.Sender is Connection conn)
            {
                conn.ReleasePendingPacket();
            }

            w.Dispose();
        }
    }

    #endregion Private Helpers
}
