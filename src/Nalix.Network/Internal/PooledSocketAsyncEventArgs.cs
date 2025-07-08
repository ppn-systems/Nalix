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
        // Reset SocketAsyncEventArgs

        base.UserToken = null;
        base.AcceptSocket = null;

        // Optional: Clear buffer if you use SetBuffer()
        base.SetBuffer(null, 0, 0);

        // Optional: Reset other states like remote endpoint if needed
        base.RemoteEndPoint = null;
    }
}