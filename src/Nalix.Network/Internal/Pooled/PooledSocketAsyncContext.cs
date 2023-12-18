// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Caching;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Network.Benchmarks")]
#endif

namespace Nalix.Network.Internal.Pooled;

/// <summary>
/// Represents a reusable wrapper for <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> with built-in pooling support.
/// Designed to reduce allocation overhead when handling high-performance socket operations.
/// </summary>
/// <remarks>
/// Use with an object pool to efficiently handle repeated async socket operations.
/// The context must be reset before being reused.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("HasBuffer={Buffer != null}, RemoteEndPoint={RemoteEndPoint}")]
internal class PooledSocketAsyncContext : System.Net.Sockets.SocketAsyncEventArgs, IPoolable
{
    /// <summary>
    /// Cached static handler for socket receive completion.
    /// Resolves and completes the <see cref="System.Threading.Tasks.TaskCompletionSource{TResult}"/> in <c>UserToken</c>.
    /// </summary>
    private static readonly System.EventHandler<System.Net.Sockets.SocketAsyncEventArgs> ReceiveCompletedHandler =
        (sender, args) =>
        {
            if (args.UserToken is System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs)
            {
                if (args.SocketError == System.Net.Sockets.SocketError.Success)
                {
                    tcs.SetResult(args.BytesTransferred);
                }
                else
                {
                    tcs.SetException(new System.Net.Sockets.SocketException((System.Int32)args.SocketError));
                }
            }
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledSocketAsyncContext"/> class.
    /// Registers the static receive completion handler.
    /// </summary>
    public PooledSocketAsyncContext() => Completed += ReceiveCompletedHandler;

    /// <summary>
    /// Resets the internal state of this instance for reuse by the object pool.
    /// Clears <c>UserToken</c>, buffer, and optionally other stateful properties.
    /// </summary>    
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ResetForPool()
    {
        UserToken = null;

        // Optional: Clear buffer if you use SetBuffer()
        SetBuffer(null, 0, 0);

        // Optional: Reset other states like remote endpoint if needed
        RemoteEndPoint = null;
    }
}