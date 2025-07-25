using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Packets;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// Implementation of the IDispatchChannel interface with optimized memory usage.
/// </summary>
/// <typeparam name="TPacket">The type of packet used in the dispatch channel.</typeparam>
public class DispatchChannel<TPacket>(ILogger? logger = null) : IDispatchChannel<TPacket> where TPacket : IPacket
{
    #region Fields

    private System.Int32 _totalPackets;

    private readonly ILogger? _logger = logger;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        IConnection, System.Collections.Concurrent.ConcurrentQueue<TPacket>> _connectionChannels = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<IConnection, System.Byte> _activeConnections = new();

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the current number of active connections in the dispatch channel.
    /// </summary>
    public System.Int32 ActiveConnectionCount => _activeConnections.Count;

    /// <summary>
    /// Gets the total number of packets across all connection queues in the dispatch channel.
    /// </summary>
    public System.Int32 TotalPackets => System.Threading.Volatile.Read(ref _totalPackets);

    #endregion Properties

    #region APIs

    /// <summary>
    /// Adds a packet to the dispatch queue, associating it with a hash computed automatically.
    /// </summary>
    /// <param name="packet">The packet to be added to the queue.</param>
    /// <param name="connection">The connection associated with the packet.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="packet"/> or <paramref name="connection"/> is null.</exception>
    public void Push(TPacket packet, IConnection connection)
    {
        if (packet is null)
        {
            _logger?.Error("Failed to enqueue packet because the packet is null.");
            throw new System.ArgumentNullException(nameof(packet));
        }

        System.ArgumentNullException.ThrowIfNull(connection);

        _activeConnections.TryAdd(connection, 0);

        var queue = _connectionChannels.GetOrAdd(connection, conn =>
        {
            conn.OnCloseEvent += OnConnectionClosed;
            return new System.Collections.Concurrent.ConcurrentQueue<TPacket>();
        });

        queue.Enqueue(packet);
        System.Threading.Interlocked.Increment(ref _totalPackets);

#if DEBUG
        _logger?.Trace("Packet enqueued successfully. Packet: {0}, Connection: {1}", packet, connection);
#endif
    }

    /// <summary>
    /// Attempts to retrieve a packet and its associated connection from the dispatch queue.
    /// </summary>
    /// <param name="packet">When this method returns, contains the retrieved packet, 
    /// or the default value of <typeparamref name="TPacket"/> if the queue is empty.</param>
    /// <param name="connection">When this method returns, 
    /// contains the connection associated with the retrieved packet, or null if the queue is empty.</param>
    /// <returns><c>true</c> if a packet was successfully retrieved; otherwise, <c>false</c> if the queue is empty.</returns>
    public System.Boolean Pull(
    [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out TPacket packet,
    [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out IConnection connection)
    {
        foreach (var kvp in _connectionChannels)
        {
            if (kvp.Value.TryDequeue(out packet!) && packet != null)
            {
                _ = System.Threading.Interlocked.Decrement(ref _totalPackets);
                connection = kvp.Key;
                return true;
            }
        }

        packet = default!;
        connection = null!;
        return false;
    }

    /// <summary>
    /// Handles the cleanup or state update when a connection is closed.
    /// </summary>
    /// <param name="sender">The object that triggered the event.</param>
    /// <param name="e">Event arguments containing information about the connection that was closed.</param>
    private void OnConnectionClosed(System.Object? sender, IConnectEventArgs e)
    {
        if (sender is not IConnection conn)
        {
            return;
        }

        _ = _activeConnections.TryRemove(conn, out _);
        _ = _connectionChannels.TryRemove(conn, out _);

        conn.OnCloseEvent -= OnConnectionClosed;
    }

    #endregion APIs
}