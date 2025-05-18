using Nalix.Network.Snapshot;

namespace Nalix.Network.Listeners;

public abstract partial class Listener : ISnapshot<ListenerSnapshot>
{
    /// <summary>
    /// Retrieves a snapshot of the current state of the listener, including its port, listening status,
    /// disposal status, socket address, and the status of the listener's socket.
    /// </summary>
    /// <returns>
    /// A <see cref="ListenerSnapshot"/> object containing the current state of the listener.
    /// </returns>
    public ListenerSnapshot GetSnapshot()
        => new()
        {
            Port = _port,
            IsDisposed = _isDisposed,
            IsListening = IsListening,
            Address = ListenerSnapshot.GetIpAddress(_listenerSocket),
            ListenerSocketStatus = ListenerSnapshot.GetSocketStatus(_listenerSocket)
        };
}
