// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Framework.Injection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Fire-and-forget dispatcher that offloads callbacks to the ThreadPool,
/// avoiding hot-path blocking. It does not provide ordering or backpressure.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class AsyncCallback
{
    // Static delegate to avoid capturing closures.
    private static readonly System.Action<System.Object> s_Invoke = static stateObj =>
    {
        State state = (State)stateObj!;
        try
        {
            state.Callback(state.Sender, state.Args);
        }
        catch (System.Exception ex)
        {
            // Never let subscriber exceptions kill a pool thread.
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(AsyncCallback)}:{nameof(Invoke)}] callback-error", ex);
        }
    };

    /// <summary>
    /// Schedule the callback to run on the ThreadPool without flowing ExecutionContext.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Invoke(
        [System.Diagnostics.CodeAnalysis.AllowNull] System.EventHandler<IConnectEventArgs> callback,
        [System.Diagnostics.CodeAnalysis.DisallowNull] System.Object sender,
        [System.Diagnostics.CodeAnalysis.DisallowNull] IConnectEventArgs args)
    {
        if (callback is null)
        {
            return;
        }

        // Pack state; small struct reduces GC pressure. Boxing occurs when passed as object
        // but remains cheap in practice. Pooling is usually unnecessary here.
        State state = new(callback, sender, args);

        // Prefer UnsafeQueueUserWorkItem for lower overhead (no EC flow).
        // Set preferLocal=false to allow global work-stealing and better distribution.
        _ = System.Threading.ThreadPool.UnsafeQueueUserWorkItem(s_Invoke, state, preferLocal: false);
    }

    [System.Runtime.CompilerServices.SkipLocalsInit]
    private readonly struct State(
        System.EventHandler<IConnectEventArgs> cb,
        System.Object sender, IConnectEventArgs args)
    {
        public readonly System.Object Sender = sender;
        public readonly IConnectEventArgs Args = args;
        public readonly System.EventHandler<IConnectEventArgs> Callback = cb;
    }
}
