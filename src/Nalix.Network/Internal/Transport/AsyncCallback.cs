// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Common.Diagnostics;
using Nalix.Common.Networking;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooled;

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
///         <see cref="FramedSocketConnection"/>) enforce per-connection limits (Layer 1)
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

    #endregion Options

    #region Fields

    /// <summary>
    /// ── Global counters ────────────────────────────────────────────────────────
    /// </summary>
    private static int s_pendingNormal;
    private static long s_droppedCallbacks;
    private static long s_totalInvoked;

    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// ── Per-IP pending counter ─────────────────────────────────────────────────
    /// Key   = remote IP string (e.g. "192.168.1.1")
    /// Value = number of normal callbacks currently queued for that IP
    /// Entries are removed when the counter reaches zero to avoid unbounded growth.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, int> s_perIpPending = new();

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

        // Decrement per-IP counter; remove key when it hits zero.
        if (w.Args.NetworkEndpoint is not null)
        {
            _ = s_perIpPending.AddOrUpdate(w.Args.NetworkEndpoint, addValueFactory: static (_, _) => 0,
                updateValueFactory: static (_, current, _) => current > 1 ? current - 1 : 0, factoryArgument: 0);

            // Clean up zero-valued entries to prevent dictionary growth.
            if (s_perIpPending.TryGetValue(w.Args.NetworkEndpoint, out int v) && v == 0)
            {
                _ = s_perIpPending.TryRemove(new KeyValuePair<INetworkEndpoint, int>(w.Args.NetworkEndpoint, 0));
            }
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
    /// <returns><see langword="true"/> if the callback was queued; <see langword="false"/> if dropped.</returns>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Invoke(
        EventHandler<IConnectEventArgs> callback,
        object? sender,
        IConnectEventArgs args)
    {
        if (callback is null)
        {
#if DEBUG
            s_logger?.Trace($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-null skipping");
#endif
            return false;
        }

        // ── Global backpressure check ──────────────────────────────────────────
        int globalPending = Volatile.Read(ref s_pendingNormal);

        if (globalPending >= s_opts.MaxPendingNormalCallbacks)
        {
            Interlocked.Increment(ref s_droppedCallbacks);
            s_logger?.Error($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] global-backpressure pending={globalPending} dropped={s_droppedCallbacks} ip={args.NetworkEndpoint}");
            return false;
        }

        // ── Per-IP backpressure check ──────────────────────────────────────────
        if (args.NetworkEndpoint is not null)
        {
            int ipPending = s_perIpPending.GetOrAdd(args.NetworkEndpoint, 0);

            if (ipPending >= s_opts.MaxPendingPerIp)
            {
                Interlocked.Increment(ref s_droppedCallbacks);
                s_logger?.Warn($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] per-ip-backpressure ip={args.NetworkEndpoint} pending={ipPending} max={s_opts.MaxPendingPerIp}");
                return false;
            }

            // Reserve the per-IP slot atomically.
            s_perIpPending.AddOrUpdate(
                args.NetworkEndpoint,
                addValueFactory: static (_, _) => 1,
                updateValueFactory: static (_, current, _) => current + 1,
                factoryArgument: 0);
        }

        // ── Warn when approaching global limit ────────────────────────────────
        int warnThreshold = s_opts.CallbackWarningThreshold;
        if (warnThreshold > 0 && globalPending >= warnThreshold && globalPending % 1_000 == 0)
        {
            s_logger?.Warn($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] high-backpressure pending={globalPending} max={s_opts.MaxPendingNormalCallbacks}");
        }

        Interlocked.Increment(ref s_pendingNormal);
        Interlocked.Increment(ref s_totalInvoked);

        return QUEUE(s_invokeNormal, callback, sender, args, isHigh: false);
    }

    /// <summary>
    /// Schedules a <b>high-priority</b> (close / disconnect) callback on the ThreadPool.
    /// <para>
    /// High-priority callbacks bypass all backpressure limits so that connection cleanup
    /// is never delayed by a flood of normal-priority callbacks. The counter
    /// <see cref="s_pendingNormal"/> is <b>not</b> incremented for these callbacks.
    /// </para>
    /// </summary>
    /// <param name="callback"></param>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InvokeHighPriority(
        EventHandler<IConnectEventArgs> callback,
        object? sender,
        IConnectEventArgs args)
    {
        if (callback is null)
        {
            return false;
        }

        _ = Interlocked.Increment(ref s_totalInvoked);

        return QUEUE(s_invokeHigh, callback, sender, args, isHigh: true);
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
        bool isHigh)
    {
        PooledConnectEventContext wrapper = PooledConnectEventContext.Get();
        wrapper.Initialize(callback, sender, args);

        if (!ThreadPool.UnsafeQueueUserWorkItem(invoker, wrapper, preferLocal: false))
        {
            // Queue failure — extremely rare. Undo the increments we already applied.
            if (!isHigh)
            {
                _ = Interlocked.Decrement(ref s_pendingNormal);

                if (args.NetworkEndpoint is not null)
                {
                    _ = s_perIpPending.AddOrUpdate(
                        args.NetworkEndpoint,
                        addValueFactory: static (_, _) => 0,
                        updateValueFactory: static (_, current, _) =>
                            current > 1 ? current - 1 : 0,
                        factoryArgument: 0);
                }
            }

            _ = Interlocked.Increment(ref s_droppedCallbacks);
            s_logger?.Error($"[NW.{nameof(AsyncCallback)}] failed-queue-work-item ip={args.NetworkEndpoint}");

            wrapper.Dispose();

            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EXECUTE_AND_RETURN(PooledConnectEventContext w)
    {
        try
        {
            w.Callback?.Invoke(w.Sender, w.Args);
        }
        catch (Exception ex)
        {
            s_logger?.Error($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-error", ex);
        }
        finally
        {
            if (w.Sender is Connection conn)
            {
                conn.ReleasePendingPacket();
            }

            w.Dispose();
        }
    }

    #endregion Private Helpers
}
