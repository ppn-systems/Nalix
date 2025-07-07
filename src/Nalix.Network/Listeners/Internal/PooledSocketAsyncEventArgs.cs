using Nalix.Common.Caching;
using System.Net.Sockets;

namespace Nalix.Network.Listeners.Internal;

/// <summary>
/// A pooled wrapper around <see cref="SocketAsyncEventArgs"/> that resets state before returning to the pool.
/// </summary>
internal sealed class PooledSocketAsyncEventArgs : SocketAsyncEventArgs, IPoolable
{
    /// <summary>
    /// Resets the internal state before returning to the pool.
    /// </summary>
    public void ResetForPool()
    {
        AcceptSocket = null;
        UserToken = null;

        // Important: Clear all event handlers
        Completed -= Listener.AsyncAcceptCompleted;

        // Optional: Clear buffer if you use SetBuffer()
        SetBuffer(null, 0, 0);

        // Optional: Reset other states like remote endpoint if needed
        RemoteEndPoint = null;
    }
}