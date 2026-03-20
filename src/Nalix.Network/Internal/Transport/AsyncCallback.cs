// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Network.Configurations;
using Nalix.Network.Connections;
using Nalix.Network.Internal.Pooled;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
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
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class AsyncCallback
{
    #region Options

    // Loaded once at startup from NetworkCallbackOptions via ConfigurationManager.
    // All throttle values are read from config so they can be tuned without recompile.
    private static readonly NetworkCallbackOptions s_opts = ConfigurationManager.Instance.Get<NetworkCallbackOptions>();

    #endregion Options

    #region Fields

    // ── Global counters ────────────────────────────────────────────────────────
    private static System.Int32 s_pendingNormal;
    private static System.Int64 s_droppedCallbacks;
    private static System.Int64 s_totalInvoked;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private static readonly ILogger s_logger = InstanceManager.Instance.GetExistingInstance<ILogger>();

    // ── Per-IP pending counter ─────────────────────────────────────────────────
    // Key   = remote IP string (e.g. "192.168.1.1")
    // Value = number of normal callbacks currently queued for that IP
    // Entries are removed when the counter reaches zero to avoid unbounded growth.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<INetworkEndpoint, System.Int32> s_perIpPending = new();

    // ── Static delegates — no closures, no per-invocation allocations ──────────
    private static readonly System.Action<System.Object> s_invokeNormal = static stateObj =>
    {
        if (stateObj is not PooledConnectEventContext w)
        {
            return;
        }

        // Decrement global counter first.
        System.Threading.Interlocked.Decrement(ref s_pendingNormal);

        // Decrement per-IP counter; remove key when it hits zero.
        if (w.Args.NetworkEndpoint is not null)
        {
            s_perIpPending.AddOrUpdate(w.Args.NetworkEndpoint, addValueFactory: static (_, __) => 0,
                updateValueFactory: static (_, current, __) => current > 1 ? current - 1 : 0, factoryArgument: (System.Object)null!);

            // Clean up zero-valued entries to prevent dictionary growth.
            if (s_perIpPending.TryGetValue(w.Args.NetworkEndpoint, out System.Int32 v) && v == 0)
            {
                s_perIpPending.TryRemove(new System.Collections.Generic.KeyValuePair<INetworkEndpoint, System.Int32>(w.Args.NetworkEndpoint, 0));
            }
        }

        EXECUTE_AND_RETURN(w);
    };

    private static readonly System.Action<System.Object> s_invokeHigh = static stateObj =>
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
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean Invoke(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.EventHandler<IConnectEventArgs> callback,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args)
    {
        if (callback is null)
        {
#if DEBUG
            s_logger?.Trace($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-null skipping");
#endif
            return false;
        }

        // ── Global backpressure check ──────────────────────────────────────────
        System.Int32 globalPending = System.Threading.Volatile.Read(ref s_pendingNormal);

        if (globalPending >= s_opts.MaxPendingNormalCallbacks)
        {
            System.Threading.Interlocked.Increment(ref s_droppedCallbacks);
            s_logger?.Error($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] global-backpressure pending={globalPending} dropped={s_droppedCallbacks} ip={args.NetworkEndpoint}");
            return false;
        }

        // ── Per-IP backpressure check ──────────────────────────────────────────
        if (args.NetworkEndpoint is not null)
        {
            System.Int32 ipPending = s_perIpPending.GetOrAdd(args.NetworkEndpoint, 0);

            if (ipPending >= s_opts.MaxPendingPerIp)
            {
                System.Threading.Interlocked.Increment(ref s_droppedCallbacks);
                s_logger?.Warn($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] per-ip-backpressure ip={args.NetworkEndpoint} pending={ipPending} max={s_opts.MaxPendingPerIp}");
                return false;
            }

            // Reserve the per-IP slot atomically.
            s_perIpPending.AddOrUpdate(
                args.NetworkEndpoint,
                addValueFactory: static (_, __) => 1,
                updateValueFactory: static (_, current, __) => current + 1,
                factoryArgument: (System.Object)null!);
        }

        // ── Warn when approaching global limit ────────────────────────────────
        System.Int32 warnThreshold = s_opts.CallbackWarningThreshold;
        if (warnThreshold > 0 && globalPending >= warnThreshold && globalPending % 1_000 == 0)
        {
            s_logger?.Warn($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] high-backpressure pending={globalPending} max={s_opts.MaxPendingNormalCallbacks}");
        }

        System.Threading.Interlocked.Increment(ref s_pendingNormal);
        System.Threading.Interlocked.Increment(ref s_totalInvoked);

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
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean InvokeHighPriority(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.EventHandler<IConnectEventArgs> callback,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.NotNull] IConnectEventArgs args)
    {
        if (callback is null)
        {
            return false;
        }

        System.Threading.Interlocked.Increment(ref s_totalInvoked);

        return QUEUE(s_invokeHigh, callback, sender, args, isHigh: true);
    }

    /// <summary>Gets diagnostic statistics about callback processing.</summary>
    public static (System.Int32 PendingNormal, System.Int64 Dropped, System.Int64 Total) GetStatistics()
        => (System.Threading.Volatile.Read(ref s_pendingNormal),
            System.Threading.Volatile.Read(ref s_droppedCallbacks),
            System.Threading.Volatile.Read(ref s_totalInvoked));

    /// <summary>Resets dropped/total counters. Used for testing or periodic reporting.</summary>
    internal static void ResetStatistics()
    {
        System.Threading.Interlocked.Exchange(ref s_droppedCallbacks, 0);
        System.Threading.Interlocked.Exchange(ref s_totalInvoked, 0);
        // Do NOT reset s_pendingNormal — it tracks live work.
    }

    #endregion Public Methods

    #region Private Helpers

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.Boolean QUEUE(
        System.Action<System.Object> invoker,
        System.EventHandler<IConnectEventArgs> callback,
        System.Object sender,
        IConnectEventArgs args,
        System.Boolean isHigh)
    {
        PooledConnectEventContext wrapper = PooledConnectEventContext.Get();
        wrapper.Initialize(callback, sender, args);

        if (!System.Threading.ThreadPool.UnsafeQueueUserWorkItem(invoker, wrapper, preferLocal: false))
        {
            // Queue failure — extremely rare. Undo the increments we already applied.
            if (!isHigh)
            {
                System.Threading.Interlocked.Decrement(ref s_pendingNormal);

                if (args.NetworkEndpoint is not null)
                {
                    s_perIpPending.AddOrUpdate(
                        args.NetworkEndpoint,
                        addValueFactory: static (_, __) => 0,
                        updateValueFactory: static (_, current, __) =>
                            current > 1 ? current - 1 : 0,
                        factoryArgument: (System.Object)null!);
                }
            }

            System.Threading.Interlocked.Increment(ref s_droppedCallbacks);
            s_logger?.Error($"[NW.{nameof(AsyncCallback)}] failed-queue-work-item ip={args.NetworkEndpoint}");

            wrapper.Dispose();

            return false;
        }

        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void EXECUTE_AND_RETURN(PooledConnectEventContext w)
    {
        try
        {
            w.Callback?.Invoke(w.Sender, w.Args);
        }
        catch (System.Exception ex)
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