// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Caching;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Pooling;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// Represents a pooled context for accepting TCP socket connections asynchronously.
/// Wraps a reusable <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> with built-in pooling logic.
/// </summary>
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Diagnostics.DebuggerDisplay("Args={Args}")]
internal sealed class PooledAcceptContext : IPoolable
{
    private static readonly System.EventHandler<System.Net.Sockets.SocketAsyncEventArgs> AsyncAcceptCompleted = static (s, e) =>
    {
        System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> tcs =
        (System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket>)e.UserToken!;

        _ = e.SocketError == System.Net.Sockets.SocketError.Success
            ? tcs.TrySetResult(e.AcceptSocket!)
            : tcs.TrySetException(
            new System.Net.Sockets.SocketException((System.Int32)e.SocketError));

        e.AcceptSocket = null;
        e.UserToken = null;
        if (e is PooledSocketAsyncEventArgs pooled)
        {
            pooled.ResetForPool();
            InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                .Return<PooledSocketAsyncEventArgs>(pooled);
        }
    };

    [System.Diagnostics.CodeAnalysis.AllowNull]
    private System.Net.Sockets.SocketAsyncEventArgs _args = null;

    /// <summary>The SAEA currently bound to this context.</summary>
    public System.Net.Sockets.SocketAsyncEventArgs Args =>
        _args ?? throw new System.InvalidOperationException("Args not bound.");

    /// <summary>
    /// Ensures that this context has a bound SAEA, acquiring one from the pool if necessary.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "<Pending>")]
    public void EnsureArgsBound()
    {
        if (_args == null)
        {
            PooledSocketAsyncEventArgs pooledArgs = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                            .Get<PooledSocketAsyncEventArgs>();

            if (pooledArgs == null)
            {
                throw new System.InvalidOperationException("Failed to acquire PooledSocketAsyncEventArgs.");
            }

            this.BindArgs(pooledArgs);
        }
    }

    /// <summary>
    /// Rebinds this context to a new <see cref="System.Net.Sockets.SocketAsyncEventArgs"/>:
    /// detaches from old args (if any) and attaches the shared completion handler.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_args))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void BindArgs(System.Net.Sockets.SocketAsyncEventArgs newArgs)
    {
        _args?.Completed -= AsyncAcceptCompleted;
        _args = newArgs ?? throw new System.ArgumentNullException(nameof(newArgs));
        _args.Completed += AsyncAcceptCompleted;
    }

    /// <summary>
    /// Binds this context to a new SAEA without attaching the async completion handler.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_args))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void BindArgsForSync(System.Net.Sockets.SocketAsyncEventArgs newArgs)
    {
        _args?.Completed -= AsyncAcceptCompleted;
        _args = newArgs ?? throw new System.ArgumentNullException(nameof(newArgs));
    }

    /// <summary>
    /// Starts an async accept. Supports cancellation — when the token is cancelled the
    /// returned task transitions to <see cref="System.OperationCanceledException"/> and
    /// the caller is responsible for returning this context to the pool.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket> BeginAcceptAsync(
    System.Net.Sockets.Socket listener,
    System.Threading.CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureArgsBound();

        System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> tcs = new(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        System.Net.Sockets.SocketAsyncEventArgs args = this.Args;
        args.UserToken = tcs;
        args.AcceptSocket = null;

        // ── Sync completion ───────────────────────────────────────────────────
        if (!listener.AcceptAsync(args))
        {
            if (args.SocketError != System.Net.Sockets.SocketError.Success)
            {
                args.AcceptSocket = null;
                tcs.TrySetException(
                    new System.Net.Sockets.SocketException((System.Int32)args.SocketError));
            }
            else
            {
                System.Net.Sockets.Socket s = args.AcceptSocket!;
                args.AcceptSocket = null;
                tcs.TrySetResult(s);
            }

            return new System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket>(tcs.Task);
        }

        // ── Async path ────────────────────────────────────────────────────────
        // Cancellation: đóng listener socket để force OS hoàn tất AcceptAsync ngay,
        // callback AsyncAcceptCompleted sẽ được gọi với OperationAborted/Interrupted.
        // KHÔNG dùng tcs.TrySetCanceled() trực tiếp vì args vẫn đang in-flight.
        System.Threading.CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(static state =>
            {
                // Chỉ cancel TCS — args vẫn in-flight, KHÔNG trả pool ở đây.
                // OS sẽ invoke AsyncAcceptCompleted sau khi listener close.
                var (t, ct) = ((System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket>,
                                System.Threading.CancellationToken))state!;
                t.TrySetCanceled(ct);
            }, (tcs, cancellationToken));
        }

        // Dispose reg khi task hoàn tất (dù success, cancel, hay fault).
        _ = tcs.Task.ContinueWith(
            static (_, state) => ((System.Threading.CancellationTokenRegistration)state!).Dispose(),
            reg,
            System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously);

        return new System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket>(tcs.Task);
    }

    /// <summary>
    /// Resets internal state before returning to the pool.
    /// Also returns the inner <see cref="PooledSocketAsyncEventArgs"/> to its pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetForPool()
    {
        if (_args != null)
        {
            _args.Completed -= AsyncAcceptCompleted;
            _args.UserToken = null;
            _args.AcceptSocket = null;
            _args = null;
        }
    }
}