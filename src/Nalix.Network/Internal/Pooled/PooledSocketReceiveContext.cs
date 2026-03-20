// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Objects;

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// Represents a pooled context for receiving TCP data asynchronously via SAEA.
/// Mirrors the pattern of <see cref="PooledAcceptContext"/>:
/// wraps a reusable <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> with
/// built-in pooling and TCS-bridge logic.
/// </summary>
/// <remarks>
/// Lifecycle per connection:
/// <code>
/// // Acquire
/// PooledReceiveContext ctx = ObjectPoolManager.Get&lt;PooledReceiveContext&gt;();
/// ctx.EnsureArgsBound();
///
/// // Use (many times — SAEA is reused for every packet on this connection)
/// int n = await ctx.ReceiveAsync(socket, buffer, offset, count);
///
/// // Release on Dispose
/// ctx.ResetForPool();
/// ObjectPoolManager.Return&lt;PooledReceiveContext&gt;(ctx);
/// </code>
///
/// <para>
/// <b>Bug fixes vs previous revision:</b>
/// <list type="bullet">
///   <item>
///     Static <c>AsyncReceiveCompleted</c> now carries the <see cref="PooledSocketReceiveContext"/>
///     reference via a dedicated wrapper object stored in <see cref="System.Net.Sockets.SocketAsyncEventArgs.UserToken"/>,
///     so <c>EndOperation()</c> is correctly called on every async completion path.
///   </item>
///   <item>
///     <see cref="ReceiveAsync"/> is no longer <c>async</c>: it returns
///     <see cref="System.Threading.Tasks.ValueTask{T}"/> directly, preserving the
///     synchronous fast-path and <c>AggressiveInlining</c>.
///   </item>
/// </list>
/// </para>
/// </remarks>
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("Args={Args}, ActiveOps={_activeOps}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
internal sealed class PooledSocketReceiveContext : IPoolable
{
    // -------------------------------------------------------------------------
    // Token wrapper — stored in SAEA.UserToken so the static handler can reach
    // both the TCS and the context instance without a closure allocation.
    // Sealed struct-like class kept allocation-cheap (one alloc per receive).
    // -------------------------------------------------------------------------

    /// <summary>
    /// Carries the per-receive TCS and the owning <see cref="PooledSocketReceiveContext"/>
    /// so the static completion handler can resolve the TCS AND call
    /// <see cref="EndOperation"/> without a closure.
    /// </summary>
    private sealed class ReceiveToken(
        System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs,
        PooledSocketReceiveContext owner)
    {
        public readonly System.Threading.Tasks.TaskCompletionSource<System.Int32> Tcs = tcs;
        public readonly PooledSocketReceiveContext Owner = owner;
    }

    // -------------------------------------------------------------------------
    // Static completion handler — shared across ALL instances.
    // No closure, no lambda capture → zero delegate allocation per receive.
    // Resolves the TCS AND decrements the active-op counter (EndOperation).
    // -------------------------------------------------------------------------
    private static readonly System.EventHandler<System.Net.Sockets.SocketAsyncEventArgs>
        AsyncReceiveCompleted = static (_, e) =>
        {
            ReceiveToken token = (ReceiveToken)e.UserToken!;

#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                $"[PooledSocketReceiveContext] async-complete " +
                $"err={e.SocketError} bytes={e.BytesTransferred} " +
                $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(token.Owner)}");
#endif

            try
            {
                _ = e.SocketError == System.Net.Sockets.SocketError.Success
                    ? token.Tcs.TrySetResult(e.BytesTransferred)
                    : token.Tcs.TrySetException(
                        new System.Net.Sockets.SocketException((System.Int32)e.SocketError));
            }
            finally
            {
                // Always decrement — even if TrySet* fails (duplicate completion guard).
                token.Owner.EndOperation();
            }
        };

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    // Always access through BindArgs(...) to keep handler wiring correct.
    [System.Diagnostics.CodeAnalysis.AllowNull]
    private System.Net.Sockets.SocketAsyncEventArgs _args = null;

    // Active operations counter.
    // 0 = idle, 1 = one receive in-flight (SAEA is single-op per instance).
    // Incremented before ReceiveAsync, decremented when OS completes (sync or async).
    // Prevents ResetForPool() from returning the SAEA while the kernel still holds it.
    private System.Int32 _activeOps = 0;

    // Signaled when _activeOps == 0. ResetForPool() waits on this before cleanup.
    // Initialized to signaled (no ops outstanding).
    private readonly System.Threading.ManualResetEventSlim _idle =
        new(initialState: true);

    // -------------------------------------------------------------------------
    // Public surface
    // -------------------------------------------------------------------------

    /// <summary>
    /// The SAEA currently bound to this context.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when <see cref="EnsureArgsBound"/> has not been called yet.
    /// </exception>
    public System.Net.Sockets.SocketAsyncEventArgs Args
        => _args ?? throw new System.InvalidOperationException("Args not bound.");

    /// <summary>
    /// Ensures this context has a bound SAEA, acquiring one from
    /// <see cref="ObjectPoolManager"/> if necessary.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style", "IDE0270:Use coalesce expression", Justification = "<Pending>")]
    public void EnsureArgsBound()
    {
        if (_args != null)
        {
            return;
        }

        PooledSocketAsyncEventArgs pooledArgs = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                        .Get<PooledSocketAsyncEventArgs>();

        if (pooledArgs == null)
        {
            throw new System.InvalidOperationException(
                "Failed to acquire PooledSocketAsyncEventArgs from pool.");
        }

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[PooledSocketReceiveContext] EnsureArgsBound acquired saea " +
            $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}");
#endif

        BindArgs(pooledArgs);
    }

    /// <summary>
    /// Rebinds this context to <paramref name="newArgs"/>:
    /// detaches the completion handler from the old SAEA (if any) and
    /// attaches it to the new one.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_args))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void BindArgs(System.Net.Sockets.SocketAsyncEventArgs newArgs)
    {
        _args?.Completed -= AsyncReceiveCompleted;

        _args = newArgs ?? throw new System.ArgumentNullException(nameof(newArgs));
        _args.Completed += AsyncReceiveCompleted;

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[PooledSocketReceiveContext] BindArgs " +
            $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}");
#endif
    }

    /// <summary>
    /// Issues a single receive of up to <paramref name="count"/> bytes into
    /// <paramref name="buffer"/> at <paramref name="offset"/>.
    /// <para>
    /// <b>Sync fast-path:</b> when <see cref="System.Net.Sockets.Socket.ReceiveAsync(System.Net.Sockets.SocketAsyncEventArgs)"/>
    /// returns <see langword="false"/>, the result is returned via
    /// <see cref="System.Threading.Tasks.ValueTask{T}"/> — no Task allocation,
    /// no TCS await. Common on LAN/loopback.
    /// </para>
    /// </summary>
    /// <param name="socket">The connected socket to read from.</param>
    /// <param name="buffer">The backing byte array (pooled).</param>
    /// <param name="offset">Start offset inside <paramref name="buffer"/>.</param>
    /// <param name="count">Maximum bytes to receive.</param>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.ValueTask{T}"/> resolving to the number of bytes
    /// received. Returns 0 when the peer has closed the connection.
    /// </returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Threading.Tasks.ValueTask<System.Int32> ReceiveAsync(
        System.Net.Sockets.Socket socket,
        System.Byte[] buffer,
        System.Int32 offset,
        System.Int32 count)
    {
        System.Net.Sockets.SocketAsyncEventArgs args = this.Args; // throws if not bound

        // Point the SAEA window at the requested slice.
        args.SetBuffer(buffer, offset, count);

        // Fresh TCS per receive.
        // RunContinuationsAsynchronously → continuations post to thread-pool,
        // preventing stack-dives when the OS fires many completions synchronously.
        System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        // Store BOTH the TCS and this context in UserToken so the static handler
        // can call EndOperation() without a closure capture.
        args.UserToken = new ReceiveToken(tcs, this);

        // Mark that a kernel operation is now in-flight.
        BeginOperation();

        System.Boolean pending;
        try
        {
            pending = socket.ReceiveAsync(args);
        }
        catch
        {
            // socket.ReceiveAsync threw synchronously (e.g. disposed socket).
            EndOperation();
            throw;
        }

        if (!pending)
        {
            // ── Sync fast-path: OS already has data ──────────────────────
            // Capture result before calling EndOperation (no re-entrancy risk here
            // because the static handler is NOT called on the sync path).
            System.Net.Sockets.SocketError err = args.SocketError;
            System.Int32 bytes = args.BytesTransferred;

            EndOperation(); // Decrement here — static handler won't fire.

#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                $"[PooledSocketReceiveContext] recv-sync " +
                $"err={err} bytes={bytes} offset={offset} count={count} " +
                $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}");
#endif

            return err != System.Net.Sockets.SocketError.Success
                ? System.Threading.Tasks.ValueTask.FromException<System.Int32>(
                    new System.Net.Sockets.SocketException((System.Int32)err))
                : System.Threading.Tasks.ValueTask.FromResult(bytes);
        }

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[PooledSocketReceiveContext] recv-async-pending " +
            $"offset={offset} count={count} " +
            $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}");
#endif

        // ── Async path: static handler fires when OS completes ───────────
        // EndOperation() is called inside AsyncReceiveCompleted via the token.
        return new System.Threading.Tasks.ValueTask<System.Int32>(tcs.Task);
    }

    /// <summary>
    /// Resets the internal state of this context before returning to the pool.
    /// Waits (up to 5 s) for any in-flight SAEA operation to finish so the
    /// kernel is guaranteed to have released the buffer before we clear the SAEA.
    /// </summary>
    public void ResetForPool()
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[PooledSocketReceiveContext] ResetForPool begin activeOps={_activeOps} " +
            $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}");
#endif

        // Wait for in-flight op. 5 s is generous; a real connection teardown
        // should cancel the socket first (which causes the OS to abort the op).
        if (!_idle.Wait(System.TimeSpan.FromSeconds(5)))
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(
                $"[PooledSocketReceiveContext] ResetForPool TIMEOUT waiting for idle " +
                $"activeOps={_activeOps} " +
                $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}");
#endif
            // Still proceed — better to risk a brief race than to leak the context.
        }

        if (_args != null)
        {
            _args.Completed -= AsyncReceiveCompleted;
            _args.UserToken = null;
            _args.SetBuffer(null, 0, 0);

            if (_args is PooledSocketAsyncEventArgs pooled)
            {
                pooled.ResetForPool();
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return<PooledSocketAsyncEventArgs>(pooled);
            }

            _args = null;
        }

        // Re-arm for next use: reset counter and event to idle state.
        System.Threading.Volatile.Write(ref _activeOps, 0);
        _idle.Set();

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[PooledSocketReceiveContext] ResetForPool done " +
            $"ctx={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)}");
#endif
    }

    // -------------------------------------------------------------------------
    // Private: active-op counter helpers
    // -------------------------------------------------------------------------

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void BeginOperation()
    {
        // First in-flight op → reset the idle event.
        if (System.Threading.Interlocked.Increment(ref _activeOps) == 1)
        {
            _idle.Reset();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void EndOperation()
    {
        // Last completing op → signal idle.
        if (System.Threading.Interlocked.Decrement(ref _activeOps) == 0)
        {
            _idle.Set();
        }
    }
}