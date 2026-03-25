// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Objects;

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// Represents a pooled context for receiving TCP data asynchronously via SAEA.
/// Mirrors the pattern of <see cref="PooledAcceptContext"/>:
/// wraps a reusable <see cref="SocketAsyncEventArgs"/> with
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
///     reference via a dedicated wrapper object stored in <see cref="SocketAsyncEventArgs.UserToken"/>,
///     so <c>EndOperation()</c> is correctly called on every async completion path.
///   </item>
///   <item>
///     <see cref="ReceiveAsync"/> is no longer <c>async</c>: it returns
///     <see cref="ValueTask{T}"/> directly, preserving the
///     synchronous fast-path and <c>AggressiveInlining</c>.
///   </item>
/// </list>
/// </para>
/// </remarks>
[DebuggerStepThrough]
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("Args={Args}, ActiveOps={_activeOps}")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PooledSocketReceiveContext : IPoolable, IDisposable
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
    /// <param name="tcs"></param>
    /// <param name="owner"></param>
    private sealed class ReceiveToken(
        TaskCompletionSource<int> tcs,
        PooledSocketReceiveContext owner)
    {
        public readonly TaskCompletionSource<int> Tcs = tcs;
        public readonly PooledSocketReceiveContext Owner = owner;
    }

    /// <summary>
    /// -------------------------------------------------------------------------
    /// Static completion handler — shared across ALL instances.
    /// No closure, no lambda capture → zero delegate allocation per receive.
    /// Resolves the TCS AND decrements the active-op counter (EndOperation).
    /// -------------------------------------------------------------------------
    /// </summary>
    private static readonly EventHandler<SocketAsyncEventArgs>
        AsyncReceiveCompleted = static (_, e) =>
        {
            if (e.UserToken is not ReceiveToken token)
            {
                return;
            }

#if DEBUG
            Debug.WriteLine(
                "[PooledSocketReceiveContext] async-complete " +
                $"err={e.SocketError} bytes={e.BytesTransferred} " +
                $"ctx={RuntimeHelpers.GetHashCode(token.Owner)}");
#endif

            try
            {
                _ = e.SocketError == SocketError.Success
                    ? token.Tcs.TrySetResult(e.BytesTransferred)
                    : token.Tcs.TrySetException(
                        new SocketException((int)e.SocketError));
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

    /// <summary>
    /// Always access through BindArgs(...) to keep handler wiring correct.
    /// </summary>
    private SocketAsyncEventArgs? _args;

    /// <summary>
    /// Active operations counter.
    /// 0 = idle, 1 = one receive in-flight (SAEA is single-op per instance).
    /// Incremented before ReceiveAsync, decremented when OS completes (sync or async).
    /// Prevents ResetForPool() from returning the SAEA while the kernel still holds it.
    /// </summary>
    private int _activeOps;

    /// <summary>
    /// Signaled when _activeOps == 0. ResetForPool() waits on this before cleanup.
    /// Initialized to signaled (no ops outstanding).
    /// </summary>
    private readonly ManualResetEventSlim _idle =
        new(initialState: true);

    // -------------------------------------------------------------------------
    // Public surface
    // -------------------------------------------------------------------------

    /// <summary>
    /// The SAEA currently bound to this context.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="EnsureArgsBound"/> has not been called yet.
    /// </exception>
    public SocketAsyncEventArgs Args
        => _args ?? throw new InvalidOperationException("Args not bound.");

    /// <summary>
    /// Ensures this context has a bound SAEA, acquiring one from
    /// <see cref="ObjectPoolManager"/> if necessary.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    [SuppressMessage(
        "Style", "IDE0270:Use coalesce expression", Justification = "<Pending>")]
    public void EnsureArgsBound()
    {
        if (_args != null)
        {
            return;
        }

        PooledSocketAsyncEventArgs pooledArgs = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                        .Get<PooledSocketAsyncEventArgs>();

#if DEBUG
        Debug.WriteLine(
            "[PooledSocketReceiveContext] EnsureArgsBound acquired saea " +
            $"ctx={RuntimeHelpers.GetHashCode(this)}");
#endif

        BindArgs(pooledArgs);
    }

    /// <summary>
    /// Rebinds this context to <paramref name="newArgs"/>:
    /// detaches the completion handler from the old SAEA (if any) and
    /// attaches it to the new one.
    /// </summary>
    /// <param name="newArgs"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [MemberNotNull(nameof(_args))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindArgs(SocketAsyncEventArgs newArgs)
    {
        _args?.Completed -= AsyncReceiveCompleted;

        _args = newArgs ?? throw new ArgumentNullException(nameof(newArgs));
        _args.Completed += AsyncReceiveCompleted;

#if DEBUG
        Debug.WriteLine(
            "[PooledSocketReceiveContext] BindArgs " +
            $"ctx={RuntimeHelpers.GetHashCode(this)}");
#endif
    }

    /// <summary>
    /// Issues a single receive of up to <paramref name="count"/> bytes into
    /// <paramref name="buffer"/> at <paramref name="offset"/>.
    /// <para>
    /// <b>Sync fast-path:</b> when <see cref="Socket.ReceiveAsync(SocketAsyncEventArgs)"/>
    /// returns <see langword="false"/>, the result is returned via
    /// <see cref="ValueTask{T}"/> — no Task allocation,
    /// no TCS await. Common on LAN/loopback.
    /// </para>
    /// </summary>
    /// <param name="socket">The connected socket to read from.</param>
    /// <param name="buffer">The backing byte array (pooled).</param>
    /// <param name="offset">Start offset inside <paramref name="buffer"/>.</param>
    /// <param name="count">Maximum bytes to receive.</param>
    /// <returns>
    /// A <see cref="ValueTask{T}"/> resolving to the number of bytes
    /// received. Returns 0 when the peer has closed the connection.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> ReceiveAsync(
        Socket socket,
        byte[] buffer,
        int offset,
        int count)
    {
        SocketAsyncEventArgs args = Args; // throws if not bound

        // Point the SAEA window at the requested slice.
        args.SetBuffer(buffer, offset, count);

        // Fresh TCS per receive.
        // RunContinuationsAsynchronously → continuations post to thread-pool,
        // preventing stack-dives when the OS fires many completions synchronously.
        TaskCompletionSource<int> tcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Store BOTH the TCS and this context in UserToken so the static handler
        // can call EndOperation() without a closure capture.
        args.UserToken = new ReceiveToken(tcs, this);

        // Mark that a kernel operation is now in-flight.
        BeginOperation();

        bool pending;
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
            SocketError err = args.SocketError;
            int bytes = args.BytesTransferred;

            EndOperation(); // Decrement here — static handler won't fire.

#if DEBUG
            Debug.WriteLine(
                "[PooledSocketReceiveContext] recv-sync " +
                $"err={err} bytes={bytes} offset={offset} count={count} " +
                $"ctx={RuntimeHelpers.GetHashCode(this)}");
#endif

            return err != SocketError.Success
                ? ValueTask.FromException<int>(
                    new SocketException((int)err))
                : ValueTask.FromResult(bytes);
        }

#if DEBUG
        Debug.WriteLine(
            "[PooledSocketReceiveContext] recv-async-pending " +
            $"offset={offset} count={count} " +
            $"ctx={RuntimeHelpers.GetHashCode(this)}");
#endif

        // ── Async path: static handler fires when OS completes ───────────
        // EndOperation() is called inside AsyncReceiveCompleted via the token.
        return new ValueTask<int>(tcs.Task);
    }

    /// <summary>
    /// Resets the internal state of this context before returning to the pool.
    /// Waits (up to 5 s) for any in-flight SAEA operation to finish so the
    /// kernel is guaranteed to have released the buffer before we clear the SAEA.
    /// </summary>
    public void ResetForPool()
    {
#if DEBUG
        Debug.WriteLine(
            $"[PooledSocketReceiveContext] ResetForPool begin activeOps={_activeOps} " +
            $"ctx={RuntimeHelpers.GetHashCode(this)}");
#endif

        // Wait for in-flight op. 5 s is generous; a real connection teardown
        // should cancel the socket first (which causes the OS to abort the op).
        if (!_idle.Wait(TimeSpan.FromSeconds(5)))
        {
#if DEBUG
            Debug.WriteLine(
                "[PooledSocketReceiveContext] ResetForPool TIMEOUT waiting for idle " +
                $"activeOps={_activeOps} " +
                $"ctx={RuntimeHelpers.GetHashCode(this)}");
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
                                        .Return(pooled);
            }

            _args = null;
        }

        // Re-arm for next use: reset counter and event to idle state.
        Volatile.Write(ref _activeOps, 0);
        _idle.Set();

#if DEBUG
        Debug.WriteLine(
            "[PooledSocketReceiveContext] ResetForPool done " +
            $"ctx={RuntimeHelpers.GetHashCode(this)}");
#endif
    }

    // -------------------------------------------------------------------------
    // Private: active-op counter helpers
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BeginOperation()
    {
        // First in-flight op → reset the idle event.
        if (Interlocked.Increment(ref _activeOps) == 1)
        {
            _idle.Reset();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EndOperation()
    {
        // Last completing op → signal idle.
        if (Interlocked.Decrement(ref _activeOps) == 0)
        {
            _idle.Set();
        }
    }

    public void Dispose() => throw new NotImplementedException();
}
