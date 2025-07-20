using Nalix.Common.Abstractions;
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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ListenerSnapshot GetSnapshot()
        => new()
        {
            Port = Config.Port,
            IsDisposed = this._isDisposed,
            IsListening = this.IsListening,
            Address = ListenerSnapshot.GetIpAddress(this._listener
                ?? throw new System.InvalidOperationException("Listener socket is not initialized.")),
            SocketInfo = ListenerSnapshot.GetSocketStatus(this._listener
                ?? throw new System.InvalidOperationException("Listener socket is not initialized."))
        };
}
