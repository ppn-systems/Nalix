using Nalix.Common.Caching;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Internal;

/// <summary>
/// Represents a pooled context for accepting TCP socket connections asynchronously.
/// Wraps a reusable <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> with built-in pooling logic.
/// </summary>
internal sealed class PooledAcceptContext : IPoolable
{
    /// <summary>
    /// Static cached event handler that resolves the 
    /// <see cref="System.Threading.Tasks.TaskCompletionSource{Socket}"/> 
    /// stored in <see cref="System.Net.Sockets.SocketAsyncEventArgs.UserToken"/>.
    /// </summary>
    private static readonly System.EventHandler<System.Net.Sockets.SocketAsyncEventArgs>
        AsyncAcceptCompleted = static (s, e) =>
    {
        System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> tcs =
            (System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket>)e.UserToken!;

        _ = e.SocketError == System.Net.Sockets.SocketError.Success
            ? tcs.TrySetResult(e.AcceptSocket!)
            : tcs.TrySetException(new System.Net.Sockets.SocketException((System.Int32)e.SocketError));
    };

    /// <summary>
    /// Gets the reusable <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> used for the accept operation.
    /// </summary>
    public System.Net.Sockets.SocketAsyncEventArgs Args;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledAcceptContext"/> class.
    /// Acquires a pooled <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> and registers a shared event handler.
    /// </summary>
    public PooledAcceptContext()
    {
        this.Args = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();
        this.Args.Completed += AsyncAcceptCompleted;
    }

    /// <summary>
    /// Prepares the context for a new asynchronous accept operation.
    /// Returns a task that completes when the accept is done.
    /// </summary>
    /// <returns>
    /// A <see cref="System.Threading.Tasks.ValueTask{Socket}"/> that completes with 
    /// the accepted <see cref="System.Net.Sockets.Socket"/>, or throws an exception on error.
    /// </returns>
    public System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket> PrepareAsync()
    {
        System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> tcs =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        this.Args.UserToken = tcs;
        this.Args.AcceptSocket = null; // reset previous
        return new System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket>(tcs.Task);
    }

    /// <summary>
    /// Resets the internal state of the accept context for reuse in the object pool.
    /// Clears <see cref="System.Net.Sockets.SocketAsyncEventArgs.UserToken"/> 
    /// and <see cref="System.Net.Sockets.SocketAsyncEventArgs.AcceptSocket"/>.
    /// </summary>
    public void ResetForPool()
    {
        this.Args.UserToken = null;
        this.Args.AcceptSocket = null;
    }
}
