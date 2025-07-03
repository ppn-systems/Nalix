namespace Nalix.Network.Listeners.Internal;

internal sealed class AcceptState
{
    public System.Threading.Tasks.TaskCompletionSource<System.Net.Sockets.Socket> Tcs =
        new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

    public System.Net.Sockets.SocketAsyncEventArgs Args = new();

    public void Reset()
    {
        Tcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        Args.AcceptSocket = null;
        Args.UserToken = this;
    }
}