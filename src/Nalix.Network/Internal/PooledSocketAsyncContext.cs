using Nalix.Common.Caching;

namespace Nalix.Network.Internal;

internal class PooledSocketAsyncContext : System.Net.Sockets.SocketAsyncEventArgs, IPoolable
{
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

    public PooledSocketAsyncContext() => base.Completed += ReceiveCompletedHandler;

    public void ResetForPool()
    {
        base.UserToken = null;

        // Optional: Clear buffer if you use SetBuffer()
        base.SetBuffer(null, 0, 0);

        // Optional: Reset other states like remote endpoint if needed
        base.RemoteEndPoint = null;
    }
}