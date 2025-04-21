namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Retrieves a snapshot of the current state of the listener, including its port, listening status,
    /// disposal status, socket address, and the status of the listener's socket.
    /// </summary>
    /// <returns>
    /// A <see cref="Snapshot.ListenerSnapshot"/> object containing the current state of the listener.
    /// </returns>
    public Snapshot.ListenerSnapshot GetSnapshot()
        => new()
        {
            Port = _port,
            IsDisposed = _isDisposed,
            IsListening = IsListening,
            Address = Snapshot.ListenerSnapshot.GetIpAddress(_listenerSocket),
            ListenerSocketStatus = Snapshot.ListenerSnapshot.GetSocketStatus(_listenerSocket)
        };
}
