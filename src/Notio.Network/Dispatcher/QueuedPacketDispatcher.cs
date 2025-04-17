using Notio.Common.Connection;
using Notio.Common.Package;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Dispatcher;

/// <summary>
/// Ultra-high performance packet dispatcher with queue-based processing, advanced dependency injection (DI) 
/// integration and async support. This implementation uses reflection to map packet command IDs to controller methods.
/// </summary>
/// <remarks>
/// The <see cref="QueuedPacketDispatcher{TPacket}"/> enqueues incoming packets and processes them asynchronously.
/// It logs errors and warnings when handling failures or unregistered commands.
/// </remarks>
public sealed class QueuedPacketDispatcher<TPacket>(System.Action<Options.PacketDispatcherOptions<TPacket>> options)
    : PacketDispatcherBase<TPacket>(options), IPacketDispatcher<TPacket>
    where TPacket : IPacket, IPacketEncryptor<TPacket>, IPacketCompressor<TPacket>, IPacketDeserializer<TPacket>
{
    #region Fields

    // Queue for storing packet handling tasks
    private readonly Queue<(TPacket Packet, IConnection Connection)> _packetQueue = new();

    // Locks for thread safety
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _semaphore = new(0);

    // Processing state
    private bool _isProcessing = false;
    private readonly CancellationTokenSource _ctokens = new();

    #endregion

    #region Properties

    /// <summary>
    /// Maximum number of packets that can be queued
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Current number of packets in the queue
    /// </summary>
    public int QueueCount
    {
        get
        {
            lock (_lock)
            {
                return _packetQueue.Count;
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts the packet processing loop
    /// </summary>
    public void BeginDispatching()
    {
        if (_isProcessing) return;

        _isProcessing = true;
        Task.Run(RunQueueLoopAsync);
    }

    /// <summary>
    /// Stops the packet processing loop
    /// </summary>
    public void Shutdown()
    {
        if (!_isProcessing)
            return;

        _isProcessing = false;
        _ctokens.Cancel();
    }

    /// <inheritdoc />
    public void HandlePacket(byte[]? packet, IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error($"[Dispatcher] Null byte[] received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(System.MemoryExtensions.AsSpan(packet), connection);
    }

    /// <inheritdoc />
    public void HandlePacket(System.ReadOnlyMemory<byte>? packet, IConnection connection)
    {
        if (packet == null)
        {
            base.Logger?.Error(
                $"[Dispatcher] Null ReadOnlyMemory<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        this.HandlePacket(packet.Value.Span, connection);
    }

    /// <inheritdoc />
    public void HandlePacket(in System.ReadOnlySpan<byte> packet, IConnection connection)
    {
        if (packet.IsEmpty)
        {
            base.Logger?.Error(
                $"[Dispatcher] Empty ReadOnlySpan<byte> received from {connection.RemoteEndPoint}. Packet dropped.");
            return;
        }

        // Deserialize and enqueue the packet for processing
        this.EnqueuePacket(TPacket.Deserialize(packet), connection);
    }

    /// <inheritdoc />
    public void HandlePacketAsync(TPacket packet, IConnection connection)
        => this.EnqueuePacket(packet, connection);

    #endregion

    #region Private Methods

    /// <summary>
    /// Adds a packet to the processing queue
    /// </summary>
    private void EnqueuePacket(TPacket packet, IConnection connection)
    {
        lock (_lock)
        {
            // Check if queue is full
            if (MaxQueueSize > 0 && _packetQueue.Count >= MaxQueueSize)
            {
                base.Logger?.Warn($"[Dispatcher] Queue full ({MaxQueueSize} packets). Packet Id: {packet.Id} from {connection.RemoteEndPoint} dropped.");
                return;
            }

            _packetQueue.Enqueue((packet, connection));
            base.Logger?.Debug($"[Dispatcher] Packet Id: {packet.Id} from {connection.RemoteEndPoint} queued. Queue size: {_packetQueue.Count}");
        }

        // Signal that a new item is available for processing
        _semaphore.Release();
    }

    /// <summary>
    /// Continuously processes packets from the queue
    /// </summary>
    private async Task RunQueueLoopAsync()
    {
        try
        {
            while (_isProcessing && !_ctokens.Token.IsCancellationRequested)
            {
                // Wait for packets to be available
                await _semaphore.WaitAsync(_ctokens.Token);

                // Dequeue and process packet
                TPacket packet;
                IConnection connection;

                lock (_lock)
                {
                    if (_packetQueue.Count == 0)
                        continue;

                    (packet, connection) = _packetQueue.Dequeue();
                }

                await ExecutePacketHandlerAsync(packet, connection);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Normal cancellation, no need to log
        }
        catch (System.Exception ex)
        {
            base.Logger?.Error($"[Dispatcher] Error in packet processing loop: {ex.Message}", ex);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// Processes a single packet
    /// </summary>
    private async Task ExecutePacketHandlerAsync(TPacket packet, IConnection connection)
    {
        if (base.Options.TryResolveHandler(packet.Id, out var handler) && handler != null)
        {
            base.Logger?.Debug($"[Dispatcher] Processing packet Id: {packet.Id} from {connection.RemoteEndPoint}...");

            try
            {
                await PacketDispatcherBase<TPacket>
                    .ExecuteHandler(handler, packet, connection)
                    .ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                base.Logger?.Error(
                    $"[Dispatcher] Exception occurred while handling packet Id: " +
                    $"{packet.Id} from {connection.RemoteEndPoint}. " +
                    $"Error: {ex.GetType().Name} - {ex.Message}", ex);
            }

            return;
        }

        base.Logger?.Warn($"[Dispatcher] No handler found for packet Id: {packet.Id} from {connection.RemoteEndPoint}.");
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases resources used by the dispatcher
    /// </summary>
    public void Dispose()
    {
        this.Shutdown();
        _ctokens.Dispose();
        _semaphore.Dispose();
    }

    #endregion
}
