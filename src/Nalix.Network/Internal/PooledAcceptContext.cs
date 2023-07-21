using Nalix.Common.Caching;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Internal;

internal sealed class PooledAcceptContext : IPoolable
{
    private static readonly System.EventHandler<System.Net.Sockets.SocketAsyncEventArgs>
        AsyncAcceptCompleted = static (s, e) =>
    {
        System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> tcs =
            (System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket>)e.UserToken!;

        _ = e.SocketError == System.Net.Sockets.SocketError.Success
            ? tcs.TrySetResult(e.AcceptSocket!)
            : tcs.TrySetException(new System.Net.Sockets.SocketException((System.Int32)e.SocketError));
    };

    public System.Net.Sockets.SocketAsyncEventArgs Args;

    public PooledAcceptContext()
    {
        this.Args = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();
        this.Args.Completed += AsyncAcceptCompleted;
    }

    public System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket> PrepareAsync()
    {
        System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> tcs =
            new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        this.Args.UserToken = tcs;
        this.Args.AcceptSocket = null; // reset previous
        return new System.Threading.Tasks.ValueTask<System.Net.Sockets.Socket>(tcs.Task);
    }

    public void ResetForPool()
    {
        this.Args.UserToken = null;
        this.Args.AcceptSocket = null;
    }
}
