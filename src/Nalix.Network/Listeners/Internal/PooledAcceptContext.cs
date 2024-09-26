using Nalix.Common.Caching;
using Nalix.Shared.Memory.Pools;

namespace Nalix.Network.Listeners.Internal;

internal sealed class PooledAcceptContext : IPoolable
{
    public System.Net.Sockets.SocketAsyncEventArgs Args;
    public System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> Tcs;

    public PooledAcceptContext()
    {
        Args = ObjectPoolManager.Instance.Get<PooledSocketAsyncEventArgs>();
        Tcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        Args.UserToken = this;
    }

    public void ResetForPool()
    {
        Tcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        Args.AcceptSocket = null;
        Args.UserToken = this;
    }
}