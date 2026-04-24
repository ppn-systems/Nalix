using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Memory.Buffers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport.Internal;

namespace Nalix.SDK.Transport;

/// <summary>
/// Provides a TCP transport session built on <see cref="FrameReader"/> and <see cref="FrameSender"/>.
/// </summary>
public class TcpSession : TransportSession
{
    #region Fields

    // Low-level components for reading and sending frames
    private readonly FrameSender _sender;
    private readonly FrameReader _reader;

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private Socket? _socket;
    private CancellationTokenSource? _loopCts;
    private int _disposed;

    #endregion Fields

    #region Properties

    /// <summary>Gets the fixed framing header size in bytes.</summary>
    public const int HeaderSize = 2;

    /// <inheritdoc/>
    public override TransportOptions Options { get; }

    /// <inheritdoc/>
    public override IPacketRegistry Catalog { get; }

    /// <inheritdoc/>
    public override bool IsConnected => _socket?.Connected == true && Volatile.Read(ref _disposed) == 0;

    #endregion Properties

    #region Events

    /// <inheritdoc/>
    public override event EventHandler? OnConnected;

    /// <inheritdoc/>
    public override event EventHandler<Exception>? OnDisconnected;

    /// <inheritdoc/>
    public override event EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public override event EventHandler<Exception>? OnError;

    /// <summary>Occurs when a complete frame is received and decoded asynchronously.</summary>
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageAsync;

    #endregion Events

    #region Constructor

    /// <summary>Initializes a new instance of the <see cref="TcpSession"/> class.</summary>
    /// <param name="options">The transport options for this session.</param>
    /// <param name="catalog">The packet registry used to resolve packet metadata.</param>
    public TcpSession(TransportOptions options, IPacketRegistry catalog)
    {
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
        this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        // Initialize frame helpers with a factory to get the latest socket instance
        _sender = new FrameSender(() => _socket!, options, this.HandleError);
        _reader = new FrameReader(() => _socket!, options, this.HandleReceiveMessage, this.HandleError);
    }

    #endregion Constructor

    #region APIs

    /// <inheritdoc/>
    public override async Task ConnectAsync(string? host = null, ushort? port = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, nameof(TcpSession));

        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string effectiveHost = string.IsNullOrWhiteSpace(host) ? this.Options.Address : host;
            ushort effectivePort = port ?? this.Options.Port;

            // Ensure single connection at a time
            if (this.IsConnected)
            {
                await this.DisconnectInternalAsync().ConfigureAwait(false);
            }

            // Initialize socket with NoDelay to reduce latency
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (this.Options.ConnectTimeoutMillis > 0)
            {
                connectCts.CancelAfter(TimeSpan.FromMilliseconds(this.Options.ConnectTimeoutMillis));
            }

            await _socket.ConnectAsync(effectiveHost, effectivePort, connectCts.Token).ConfigureAwait(false);
            this.OnConnected?.Invoke(this, EventArgs.Empty);

            // Start background worker for reading frames
            _loopCts = new CancellationTokenSource();

            _ = Task.Factory.StartNew(() => _reader.ReceiveLoopAsync(_loopCts.Token),
                _loopCts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }
        catch (Exception ex)
        {
            await this.DisconnectInternalAsync().ConfigureAwait(false);
            this.OnError?.Invoke(this, ex);
            throw new NetworkException($"Connection failed: {ex.Message}", ex);
        }
        finally
        {
            _ = _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public override async Task DisconnectAsync()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await this.DisconnectInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _ = _connectionLock.Release();
        }
    }

    private Task DisconnectInternalAsync()
    {
        CancellationTokenSource? loopCts = Interlocked.Exchange(ref _loopCts, null);
        Socket? socket = Interlocked.Exchange(ref _socket, null);

        try
        {
            loopCts?.Cancel();
        }
        catch (ObjectDisposedException ex)
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                this.OnError?.Invoke(this, ex);
            }
        }
        finally
        {
            loopCts?.Dispose();
        }

        if (socket is not null)
        {
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch (SocketException ex)
            {
                if (Volatile.Read(ref _disposed) == 0)
                {
                    this.OnError?.Invoke(this, ex);
                }
            }
            catch (ObjectDisposedException ex)
            {
                if (Volatile.Read(ref _disposed) == 0)
                {
                    this.OnError?.Invoke(this, ex);
                }
            }

            socket.Dispose();
            this.OnDisconnected?.Invoke(this, new NetworkException("The TCP session was disconnected."));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task SendAsync(IPacket packet, CancellationToken ct = default)
        => await this.SendAsync(packet, this.Options.EncryptionEnabled, ct).ConfigureAwait(false);

    /// <summary>Sends a packet asynchronously with an optional encryption override.</summary>
    /// <param name="packet">The packet to serialize and send.</param>
    /// <param name="encrypt">A value that overrides packet encryption when provided.</param>
    /// <param name="ct">The token to observe while sending.</param>
    public override async Task SendAsync(IPacket packet, bool? encrypt = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packet);

        packet.Flags = (packet.Flags & ~PacketFlags.UNRELIABLE) | PacketFlags.RELIABLE;

        using BufferLease lease = BufferLease.Rent(packet.Length);
        lease.CommitLength(packet.Serialize(lease.SpanFull));
        bool sent = await _sender.SendAsync(lease, encrypt, ct).ConfigureAwait(false);
        if (!sent)
        {
            throw new NetworkException("Failed to send TCP packet: the frame was not delivered to the socket.");
        }
    }

    /// <inheritdoc/>
    public override async Task SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
        => await _sender.SendAsync(payload, encrypt, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = this.DisconnectInternalAsync();
        _sender.Dispose();
        _reader.Dispose();
        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private

    private void HandleError(Exception ex)
    {
        this.OnError?.Invoke(this, ex);
        _ = this.DisconnectAsync();
    }

    /// <summary>
    /// Handles messages received by <see cref="FrameReader"/>.
    /// </summary>
    private void HandleReceiveMessage(IBufferLease lease)
    {
        try
        {
            // Direct synchronous dispatch (hot path for benchmarks)
            this.OnMessageReceived?.Invoke(this, lease);

            // Concurrent asynchronous dispatch
            if (this.OnMessageAsync is { } asyncHandler)
            {
                lease.Retain();

                Task dispatchTask;
                try
                {
                    dispatchTask = asyncHandler(lease.Memory);
                }
                catch (Exception ex)
                {
                    lease.Dispose();
                    this.OnError?.Invoke(this, ex);
                    return;
                }

                if (dispatchTask.IsCompletedSuccessfully)
                {
                    lease.Dispose();
                }
                else
                {
                    _ = dispatchTask.ContinueWith(static (task, state) =>
                    {
                        if (state is not Tuple<TcpSession, IBufferLease> payload)
                        {
                            return;
                        }

                        TcpSession self = payload.Item1;
                        IBufferLease retained = payload.Item2;
                        try
                        {
                            if (task.Exception?.GetBaseException() is Exception ex)
                            {
                                self.OnError?.Invoke(self, ex);
                            }
                        }
                        finally
                        {
                            retained.Dispose();
                        }
                    }, Tuple.Create(this, (IBufferLease)lease), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
        }
        catch (Exception ex)
        {
            this.OnError?.Invoke(this, ex);
        }
    }

    #endregion Private
}
