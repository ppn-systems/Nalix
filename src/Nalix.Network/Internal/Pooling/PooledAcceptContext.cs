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
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Pooling;

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
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    private static readonly EventHandler<SocketAsyncEventArgs> AsyncAcceptCompleted = static (s, e) =>
    {
        if (e.UserToken is not TaskCompletionSource<Socket> tcs)
        {
            return;
        }

        _ = e.SocketError == SocketError.Success
            ? e.AcceptSocket is Socket acceptedSocket
                ? tcs.TrySetResult(acceptedSocket)
                : tcs.TrySetException(new InternalErrorException("TryAccept completed successfully without a socket."))
            : tcs.TrySetException(new SocketException((int)e.SocketError));
    };

    private SocketAsyncEventArgs? _args;

    /// <summary>The SAEA currently bound to this context.</summary>
    /// <exception cref="InvalidOperationException"></exception>
    public SocketAsyncEventArgs Args => _args ?? throw new InternalErrorException("Args not bound.");

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
                throw new InternalErrorException("Failed to acquire PooledSocketAsyncEventArgs.");
            }

            this.BindArgs(pooledArgs);
        }
    }

    /// <summary>
    /// Rebinds this context to a new <see cref="SocketAsyncEventArgs"/>.
    /// This detaches the shared completion handler from the old args, attaches it
    /// to the new args, and keeps the pooled accept context reusable across accepts.
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
    /// Binds this context to a new SAEA without wiring the async completion handler.
    /// This is used only when the caller wants to reuse the same SAEA binding
    /// without subscribing the shared async completion callback.
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
    public ValueTask<Socket> BeginAcceptAsync(Socket listener, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        this.EnsureArgsBound();

        TaskCompletionSource<Socket> tcs = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Register cancellation separately from the SAEA completion path so the
        // accept task can be canceled even if the socket never completes or the
        // accept operation gets stuck in-flight for longer than expected.
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(static state =>
            {
                if (state is not ValueTuple<TaskCompletionSource<Socket>, CancellationToken> tuple)
                {
                    return;
                }

                (TaskCompletionSource<Socket> t, CancellationToken ct) = tuple;
                _ = t.TrySetCanceled(ct);
            }, (tcs, cancellationToken));
        }

        SocketAsyncEventArgs args = this.Args;
        args.UserToken = tcs;
        args.AcceptSocket = null;

        if (!listener.AcceptAsync(args))
        {
            // Synchronous completion means the OS already produced a socket, so we
            // can resolve the TCS inline and dispose the cancellation registration now.
            reg.Dispose();

            if (args.SocketError != SocketError.Success)
            {
                args.AcceptSocket = null;
                _ = tcs.TrySetException(new SocketException((int)args.SocketError));
            }
            else
            {
                Socket? s = args.AcceptSocket;
                args.AcceptSocket = null;
                if (s is null)
                {
                    _ = tcs.TrySetException(new InternalErrorException("TryAccept completed successfully without a socket."));
                }
                else
                {
                    _ = tcs.TrySetResult(s);
                }
            }
        }
        else
        {
            // Asynchronous completion keeps the cancellation registration alive
            // until the accept task settles, then disposes it on the continuation
            // so the cancellation callback cannot outlive the accept task.
            _ = tcs.Task.ContinueWith(
                static (_, state) =>
                {
                    if (state is CancellationTokenRegistration registration)
                    {
                        registration.Dispose();
                    }
                },
                reg,
                TaskContinuationOptions.ExecuteSynchronously);
        }

        return new ValueTask<Socket>(tcs.Task);
    }

    /// <summary>
    /// Resets internal state before returning to the pool and returns the inner
    /// <see cref="PooledSocketAsyncEventArgs"/> to its own pool.
    /// This prevents stale completion handlers, sockets, or user tokens from
    /// leaking into the next accept operation.
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
