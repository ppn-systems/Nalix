// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Infrastructure.Caching;
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
            : tcs.TrySetException(new System.Net.Sockets.SocketException((System.Int32)e.SocketError));
    };

    // Always access through BindArgs(...) to keep handler wiring correct.
    [System.Diagnostics.CodeAnalysis.AllowNull]
    private System.Net.Sockets.SocketAsyncEventArgs _args;

    /// <summary>
    /// The SAEA currently bound to this context.
    /// </summary>
    public System.Net.Sockets.SocketAsyncEventArgs Args
        => _args ?? throw new System.InvalidOperationException("Args not bound.");

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledAcceptContext"/> class and binds to a pooled args.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "<Pending>")]
    public PooledAcceptContext()
    {
        PooledSocketAsyncEventArgs pooledArgs = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                        .Get<PooledSocketAsyncEventArgs>();
        if (pooledArgs == null)
        {
            throw new System.InvalidOperationException("Failed to acquire a pooled SocketAsyncEventArgs instance.");
        }

        this.BindArgs(pooledArgs);
    }

    /// <summary>
    /// Rebinds this context to a new <see cref="System.Net.Sockets.SocketAsyncEventArgs"/>:
    /// detaches from old args (if any) and attaches the shared completion handler to the new args.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_args))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void BindArgs(System.Net.Sockets.SocketAsyncEventArgs newArgs)
    {
        if (_args != null)
        {
            _args.Completed -= AsyncAcceptCompleted;
        }

        _args = newArgs ?? throw new System.ArgumentNullException(nameof(newArgs));
        _args.Completed += AsyncAcceptCompleted;
    }

    /// <summary>
    /// Binds this context to a new <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> without attaching the completion handler.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_args))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void BindArgsForSync(System.Net.Sockets.SocketAsyncEventArgs newArgs)
    {
        if (_args != null)
        {
            _args.Completed -= AsyncAcceptCompleted;
        }

        _args = newArgs ?? throw new System.ArgumentNullException(nameof(newArgs));
    }

    /// <summary>
    /// Starts an accept with the correct order: prepare TCS first, then call AcceptAsync.
    /// Works for both sync and async completion.
    /// </summary>
    [System.Diagnostics.Contracts.Pure]
    public System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket> BeginAcceptAsync(System.Net.Sockets.Socket listener)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket>(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        System.Net.Sockets.SocketAsyncEventArgs args = this.Args;          // throws if not bound
        args.UserToken = tcs;
        args.AcceptSocket = null;

        if (!listener.AcceptAsync(args))
        {
            if (args.SocketError != System.Net.Sockets.SocketError.Success)
            {
                args.AcceptSocket = null;
                _ = tcs.TrySetException(new System.Net.Sockets.SocketException((System.Int32)args.SocketError));
            }
            else
            {
                System.Net.Sockets.Socket s = args.AcceptSocket!;
                args.AcceptSocket = null;
                _ = tcs.TrySetResult(s);
            }
        }

        return new System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket>(tcs.Task);
    }

    /// <summary>
    /// Resets the internal state of this context before returning to the pool.
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
        }
    }
}
