// Copyright (c) 2025-20266 PPN Corporation. All rights reserved.

using Nalix.Common.Diagnostics;
using Nalix.Common.Infrastructure.Connection;
using Nalix.Framework.Injection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Fire-and-forget dispatcher that offloads callbacks to the ThreadPool,
/// avoiding hot-path blocking. Provides backpressure protection to prevent DoS attacks.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class AsyncCallback
{
    #region Constants

    private const System.Int32 MaxPooledStates = 1_000;
    private const System.Int32 WarningThreshold = 5_000;
    private const System.Int32 MaxPendingCallbacks = 10_000;

    #endregion Constants

    #region Fields

    private static System.Int32 s_pendingCallbacks;
    private static System.Int64 s_droppedCallbacks;
    private static System.Int64 s_totalInvoked;
    private static readonly System.Collections.Concurrent.ConcurrentBag<StateWrapper> s_statePool = [];

    // Static delegate to avoid capturing closures.
    private static readonly System.Action<System.Object> s_Invoke = static stateObj =>
    {
        if (stateObj is not StateWrapper wrapper)
        {
            return;
        }

        // Decrement counter when work starts
        System.Threading.Interlocked.Decrement(ref s_pendingCallbacks);

        try
        {
            wrapper.Callback?.Invoke(wrapper.Sender, wrapper.Args);
        }
        catch (System.Exception ex)
        {
            // Never let subscriber exceptions kill a pool thread.
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-error", ex);
        }
        finally
        {
            wrapper.Clear();
            if (s_statePool.Count < MaxPooledStates)
            {
                s_statePool.Add(wrapper);
            }
        }
    };

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Schedule the callback to run on the ThreadPool without flowing ExecutionContext.
    /// Returns false if the callback was dropped due to backpressure.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    /// <param name="sender">The sender object.</param>
    /// <param name="args">The event arguments.</param>
    /// <returns>True if the callback was queued successfully; false if dropped due to backpressure.</returns>
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
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Trace($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] callback is null, skipping");
#endif
            return false;
        }

        // Check backpressure before queuing
        System.Int32 pending = System.Threading.Volatile.Read(ref s_pendingCallbacks);

        if (pending >= MaxPendingCallbacks)
        {
            System.Threading.Interlocked.Increment(ref s_droppedCallbacks);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] backpressure limit reached, " +
                       $"dropped callback (pending={pending}, dropped={s_droppedCallbacks})");

            return false;
        }

        // Warn when approaching limit
        if (pending >= WarningThreshold && pending % 1000 == 0)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Warn($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] high-backpressure (pending={pending}, threshold={MaxPendingCallbacks})");
        }

        // Increment counter before queuing
        System.Threading.Interlocked.Increment(ref s_pendingCallbacks);
        System.Threading.Interlocked.Increment(ref s_totalInvoked);

        // Pack state; small struct reduces GC pressure.
        if (!s_statePool.TryTake(out StateWrapper wrapper))
        {
            wrapper = new StateWrapper();
        }

        wrapper.Set(callback, sender, args);

        // Prefer UnsafeQueueUserWorkItem for lower overhead (no EC flow).
        // Set preferLocal=false to allow global work-stealing and better distribution.
        if (!System.Threading.ThreadPool.UnsafeQueueUserWorkItem(s_Invoke, wrapper, preferLocal: false))
        {
            // Handle queue failure (rare but possible)
            System.Threading.Interlocked.Decrement(ref s_pendingCallbacks);
            System.Threading.Interlocked.Increment(ref s_droppedCallbacks);

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[NW.{nameof(AsyncCallback)}:{nameof(Invoke)}] failed to queue work item");

            // Return to pool
            wrapper.Clear();
            if (s_statePool.Count < MaxPooledStates)
            {
                s_statePool.Add(wrapper);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets diagnostic statistics about callback processing.
    /// </summary>
    /// <returns>A tuple containing (pending, dropped, total) callback counts.</returns>
    public static (System.Int32 Pending, System.Int64 Dropped, System.Int64 Total) GetStatistics()
    {
        return (
            System.Threading.Volatile.Read(ref s_pendingCallbacks),
            System.Threading.Volatile.Read(ref s_droppedCallbacks),
            System.Threading.Volatile.Read(ref s_totalInvoked)
        );
    }

    /// <summary>
    /// Resets the dropped callbacks counter. Used for testing or periodic reset.
    /// </summary>
    internal static void ResetStatistics()
    {
        System.Threading.Interlocked.Exchange(ref s_droppedCallbacks, 0);
        System.Threading.Interlocked.Exchange(ref s_totalInvoked, 0);
        // Note: Do NOT reset s_pendingCallbacks as it tracks active work
    }

    #endregion Public Methods

    #region Nested Types

    [System.Runtime.CompilerServices.SkipLocalsInit]
    private sealed class StateWrapper
    {
        public System.EventHandler<IConnectEventArgs> Callback;
        public System.Object Sender;
        public IConnectEventArgs Args;

        public void Set(
            System.EventHandler<IConnectEventArgs> callback,
            System.Object sender,
            IConnectEventArgs args)
        {
            Callback = callback;
            Sender = sender;
            Args = args;
        }

        public void Clear()
        {
            Callback = null;
            Sender = null;
            Args = null;
        }
    }

    #endregion Nested Types
}