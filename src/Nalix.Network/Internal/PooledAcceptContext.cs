using Nalix.Common.Caching;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Network.Internal;

internal sealed class PooledAcceptContext : IPoolable
{
    private static readonly System.EventHandler<
        System.Net.Sockets.SocketAsyncEventArgs> AsyncAcceptCompleted = static (s, e) =>
        {
            System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> tcs =
                (System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket>)e.UserToken!;

            if (e.SocketError == System.Net.Sockets.SocketError.Success)
            {
                tcs.TrySetResult(e.AcceptSocket!);
            }
            else
            {
                tcs.TrySetException(new System.Net.Sockets.SocketException((System.Int32)e.SocketError));
            }
        };

    public System.Net.Sockets.SocketAsyncEventArgs Args;
    public System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> Tcs;

    public PooledAcceptContext()
    {
        this.Args = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();
        this.Args.Completed += AsyncAcceptCompleted;
        this.Tcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        this.Args.UserToken = this;
    }

    public void ResetForPool()
    {
        this.Tcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        this.Args.AcceptSocket = null;
        this.Args.UserToken = this;

        this.Args.Completed -= AsyncAcceptCompleted;
        this.Args.Completed += AsyncAcceptCompleted;
    }
}