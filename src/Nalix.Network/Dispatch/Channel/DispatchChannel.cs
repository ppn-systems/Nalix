using Nalix.Common.Connection;
using Nalix.Common.Logging;
using Nalix.Common.Packets;

namespace Nalix.Network.Dispatch.Channel;

/// <summary>
/// Implementation of the IDispatchChannel interface with integrated logging.
/// </summary>
/// <typeparam name="TPacket">The type of packet used in the dispatch channel.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="DispatchChannel{TPacket}"/> class.
/// </remarks>
/// <param name="logger">Logger used for logging system events and diagnostics.</param>
public class DispatchChannel<TPacket>(ILogger? logger = null) : IDispatchChannel<TPacket> where TPacket : IPacket
{
    #region Fields

    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        IConnection, System.Collections.Generic.HashSet<System.Int32>> _connectionHashes = new();

    private readonly System.Collections.Concurrent.ConcurrentQueue<(TPacket Packet, IConnection Connection)> _queue = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, IConnection> _connectionMapping = new();

    private readonly ILogger? _logger = logger;

    #endregion Fields

    /// <summary>
    /// Gets the current number of packets in the dispatch queue.
    /// </summary>
    public System.Int32 Count => _queue.Count;

    /// <summary>
    /// Adds a packet to the dispatch queue, associating it with a specific connection.
    /// </summary>
    /// <param name="packet">The packet to be added to the queue.</param>
    /// <param name="connection">The connection associated with the packet.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when 
    /// <paramref name="packet"/> or <paramref name="connection"/> is null.</exception>
    public void Add(TPacket packet, IConnection connection)
    {
        if (packet is null)
        {
            _logger.Error("Failed to enqueue packet because the packet is null.");
            throw new System.ArgumentNullException(nameof(packet));
        }

        System.ArgumentNullException.ThrowIfNull(connection);

        _queue.Enqueue((packet, connection));
        _logger.Trace("Packet enqueued successfully. Packet: {0}, Connection: {1}", packet, connection);
    }

    /// <summary>
    /// Attempts to retrieve a packet and its associated connection from the dispatch queue.
    /// </summary>
    /// <param name="packet">When this method returns, contains the retrieved packet, 
    /// or the default value of <typeparamref name="TPacket"/> if the queue is empty.</param>
    /// <param name="connection">When this method returns, 
    /// contains the connection associated with the retrieved packet, or null if the queue is empty.</param>
    /// <returns><c>true</c> if a packet was successfully retrieved; otherwise, <c>false</c> if the queue is empty.</returns>
    public System.Boolean TryGet(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out TPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out IConnection connection)
    {
        if (_queue.TryDequeue(out (TPacket Packet, IConnection Connection) item))
        {
            packet = item.Packet;
            connection = item.Connection;
            _logger.Debug("Packet dequeued successfully. Packet: {0}, Connection: {1}", packet, connection);
            return true;
        }

        packet = default!;
        connection = null!;

        return false;
    }

    /// <summary>
    /// Registers a connection with a specific packet hash for tracking or routing purposes.
    /// </summary>
    /// <param name="connection">The connection to register.</param>
    /// <param name="hash">The hash value associated with the packet, used for identification or routing.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="connection"/> is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="hash"/> is not a positive integer.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown when the 
    /// <paramref name="hash"/> is already registered.</exception>
    public void Register(IConnection connection, System.Int32 hash)
    {
        System.ArgumentNullException.ThrowIfNull(connection);

        if (hash <= 0)
        {
            _logger.Error("Invalid hash value: {0}. Hash must be a positive integer.", hash);
            throw new System.ArgumentException("Hash must be a positive integer.", nameof(hash));
        }

        if (_connectionMapping.ContainsKey(hash))
        {
            _logger.Error("Hash {0} is already registered.", hash);
            throw new System.InvalidOperationException($"Hash {hash} is already registered.");
        }

        _connectionMapping[hash] = connection;
        _ = _connectionHashes.AddOrUpdate(
            connection,
            [hash],
            (conn, existingHashes) =>
            {
                _ = existingHashes.Add(hash);
                return existingHashes;
            });

        connection.OnCloseEvent += OnConnectionClosed;
    }

    /// <summary>
    /// Handles the cleanup or state update when a connection is closed.
    /// </summary>
    /// <param name="sender">The object that triggered the event.</param>
    /// <param name="e">Event arguments containing information about the connection that was closed.</param>
    private void OnConnectionClosed(System.Object? sender, IConnectEventArgs e)
    {
        if (sender is not IConnection connection)
        {
            return;
        }

        System.ArgumentNullException.ThrowIfNull(connection);

        if (_connectionHashes.TryRemove(connection, out var hashes))
        {
            foreach (var hash in hashes)
            {
                _ = _connectionMapping.TryRemove(hash, out _);
            }
        }

        connection.OnCloseEvent -= OnConnectionClosed;
    }
}