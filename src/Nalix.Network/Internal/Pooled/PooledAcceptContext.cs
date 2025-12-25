// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Shared;
using Nalix.Framework.Injection;
using Nalix.Shared.Memory.Objects;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// Represents a pooled context for accepting TCP socket connections asynchronously.
/// Wraps a reusable <see cref="SocketAsyncEventArgs"/> with built-in pooling logic.
/// </summary>
[DebuggerStepThrough]
[DebuggerNonUserCode]
[SkipLocalsInit]
[DebuggerDisplay("Args={Args}")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class PooledAcceptContext : IPoolable
{
    private static readonly EventHandler<SocketAsyncEventArgs> AsyncAcceptCompleted = static (s, e) =>
    {
        TaskCompletionSource<Socket> tcs =
            (TaskCompletionSource<Socket>)e.UserToken;

        _ = e.SocketError == SocketError.Success
            ? tcs.TrySetResult(e.AcceptSocket)
            : tcs.TrySetException(new SocketException((int)e.SocketError));
    };

    [AllowNull]
    private SocketAsyncEventArgs _args;

    /// <summary>The SAEA currently bound to this context.</summary>
    /// <exception cref="InvalidOperationException"></exception>
    public SocketAsyncEventArgs Args =>
        _args ?? throw new InvalidOperationException("Args not bound.");

    /// <summary>
    /// Ensures that this context has a bound SAEA, acquiring one from the pool if necessary.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    [SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "<Pending>")]
    public void EnsureArgsBound()
    {
        if (_args == null)
        {
            PooledSocketAsyncEventArgs pooledArgs = InstanceManager.Instance
                .GetOrCreateInstance<ObjectPoolManager>()
                .Get<PooledSocketAsyncEventArgs>();

            if (pooledArgs == null)
            {
                throw new InvalidOperationException("Failed to acquire PooledSocketAsyncEventArgs.");
            }

            BindArgs(pooledArgs);
        }
    }

    /// <summary>
    /// Rebinds this context to a new <see cref="SocketAsyncEventArgs"/>:
    /// detaches from old args (if any) and attaches the shared completion handler.
    /// </summary>
    /// <param name="newArgs"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [MemberNotNull(nameof(_args))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindArgs(SocketAsyncEventArgs newArgs)
    {
        _args?.Completed -= AsyncAcceptCompleted;
        _args = newArgs ?? throw new ArgumentNullException(nameof(newArgs));
        _args.Completed += AsyncAcceptCompleted;
    }

    /// <summary>
    /// Binds this context to a new SAEA without attaching the async completion handler.
    /// </summary>
    /// <param name="newArgs"></param>
    /// <exception cref="ArgumentNullException"></exception>
    [MemberNotNull(nameof(_args))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindArgsForSync(SocketAsyncEventArgs newArgs)
    {
        _args?.Completed -= AsyncAcceptCompleted;
        _args = newArgs ?? throw new ArgumentNullException(nameof(newArgs));
    }

    /// <summary>
    /// Starts an async accept. Supports cancellation — when the token is cancelled the
    /// returned task transitions to <see cref="OperationCanceledException"/> and
    /// the caller is responsible for returning this context to the pool.
    /// </summary>
    /// <param name="listener"></param>
    /// <param name="cancellationToken"></param>
    [Pure]
    public ValueTask<Socket> BeginAcceptAsync(
        Socket listener,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureArgsBound();

        TaskCompletionSource<Socket> tcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Register cancellation — when fired, the awaiter gets OperationCanceledException.
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(static state =>
            {
                (TaskCompletionSource<Socket> t, CancellationToken ct) = ((TaskCompletionSource<Socket>,
                                CancellationToken))state;
                _ = t.TrySetCanceled(ct);
            }, (tcs, cancellationToken));
        }

        SocketAsyncEventArgs args = Args;
        args.UserToken = tcs;
        args.AcceptSocket = null;

        if (!listener.AcceptAsync(args))
        {
            // Completed synchronously — dispose registration immediately.
            reg.Dispose();

            if (args.SocketError != SocketError.Success)
            {
                args.AcceptSocket = null;
                _ = tcs.TrySetException(new SocketException((int)args.SocketError));
            }
            else
            {
                Socket s = args.AcceptSocket;
                args.AcceptSocket = null;
                _ = tcs.TrySetResult(s);
            }
        }
        else
        {
            // Completed asynchronously — dispose registration when task finishes.
            _ = tcs.Task.ContinueWith(
                static (_, state) => ((CancellationTokenRegistration)state).Dispose(),
                reg,
                TaskContinuationOptions.ExecuteSynchronously);
        }

        return new ValueTask<Socket>(tcs.Task);
    }

    /// <summary>
    /// Resets internal state before returning to the pool.
    /// Also returns the inner <see cref="PooledSocketAsyncEventArgs"/> to its pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetForPool()
    {
        if (_args != null)
        {
            _args.Completed -= AsyncAcceptCompleted;
            _args.UserToken = null;
            _args.AcceptSocket = null;

            if (_args is PooledSocketAsyncEventArgs pooled)
            {
                pooled.ResetForPool();
                InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                        .Return(pooled);
            }

            _args = null;
        }
    }
}
