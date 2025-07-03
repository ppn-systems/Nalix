namespace Nalix.Network.Listeners.Internal;

internal sealed class SocketAsyncEventArgsPool
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<
        System.Net.Sockets.SocketAsyncEventArgs> _pool = new();

    public System.Net.Sockets.SocketAsyncEventArgs Rent()
    {
        if (_pool.TryDequeue(out System.Net.Sockets.SocketAsyncEventArgs? args))
        {
            args.AcceptSocket = null;
            args.UserToken = null;
            return args;
        }

        return new System.Net.Sockets.SocketAsyncEventArgs();
    }

    public void Return(System.Net.Sockets.SocketAsyncEventArgs args)
    {
        args.Completed -= null; // clear all event handlers
        _pool.Enqueue(args);
    }
}