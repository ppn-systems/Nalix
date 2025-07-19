using Nalix.Common.Caching;

namespace Nalix.Network.Internal;

/// <summary>
/// A pooled wrapper around <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> that resets state before returning to the pool.
/// </summary>
internal sealed class PooledSocketAsyncEventArgs : System.Net.Sockets.SocketAsyncEventArgs, IPoolable
{
    /// <summary>
    /// Resets the internal state before returning to the pool.
    /// </summary>
    public void ResetForPool()
    {
        // Remove all event handlers from Completed event
        // You cannot assign null directly; instead, unsubscribe known handlers if needed.
        // If you subscribe only one handler, you can do:
        // this.Completed -= YourHandler;
        // Otherwise, skip this line or manage handlers elsewhere.

        base.UserToken = null;
        base.AcceptSocket = null;

        // Optional: Clear buffer if you use SetBuffer()
        base.SetBuffer(null, 0, 0);

        // Optional: Reset other states like remote endpoint if needed
        base.RemoteEndPoint = null;
    }
}