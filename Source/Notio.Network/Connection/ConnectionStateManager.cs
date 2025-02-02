using Notio.Common.Connection.Enums;
using Notio.Common.Models;
using System.Threading;

namespace Notio.Network.Connection;

/// <summary>
/// Manages the connection state and authority, providing thread-safe updates.
/// </summary>
public class ConnectionStateManager
{
    private int _state;
    private int _authority;

    /// <summary>
    /// Gets the current connection state in a thread-safe manner.
    /// </summary>
    public ConnectionState State => (ConnectionState)Volatile.Read(ref _state);

    /// <summary>
    /// Gets the current authority in a thread-safe manner.
    /// </summary>
    public Authoritys Authority => (Authoritys)Volatile.Read(ref _authority);

    /// <summary>
    /// Updates the connection state in a thread-safe manner.
    /// </summary>
    /// <param name="newState">The new connection state to set.</param>
    public void UpdateState(ConnectionState newState)
        => Interlocked.Exchange(ref _state, (int)newState);

    /// <summary>
    /// Updates the authority in a thread-safe manner.
    /// </summary>
    /// <param name="newAuthority">The new authority to set.</param>
    public void UpdateAuthority(Authoritys newAuthority)
        => Interlocked.Exchange(ref _authority, (int)newAuthority);
}