using Nalix.Common.Caching;

namespace Nalix.Network.Internal;

/// <summary>
/// A pooled wrapper around <see cref="System.Net.Sockets.SocketAsyncEventArgs"/> that resets state before returning to the pool.
/// </summary>
internal sealed class PooledSocketAsyncEventArgs : System.Net.Sockets.SocketAsyncEventArgs, IPoolable
{
    /// <summary>
    /// The pooled accept context associated with this event args.
    /// </summary>
    public PooledAcceptContext? Context { get; set; }

    /// <summary>
    /// Resets the internal state before returning to the pool.
    /// </summary>
    public void ResetForPool()
    {
        // Unsubscribe event handlers if needed here (e.g. this.Completed -= SomeHandler)

        base.UserToken = null;
        base.AcceptSocket = null;
        base.SetBuffer(null, 0, 0);
        base.RemoteEndPoint = null;

        this.Context = null; // 🧽 Very important
    }
}