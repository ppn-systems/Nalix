// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Tasks;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.SDK.Remote.Configuration;
using Nalix.SDK.Remote.Internal;
using Nalix.Shared.Configuration;
using Nalix.Shared.Memory.Caches;
using System.Linq;

namespace Nalix.SDK.Remote;

/// <summary>
/// Represents a network client that connects to a remote server using Reliable.
/// </summary>
/// <remarks>
/// The <see cref="ReliableClient"/> class is a singleton that manages the connection,
/// network stream, and client disposal. It supports both synchronous and asynchronous connection.
/// </remarks>
[System.Diagnostics.DebuggerDisplay("Remote={Options.Address}:{Options.Port}, Connected={IsConnected}")]
public sealed class ReliableClient : System.IDisposable
{
    #region Fields

    private readonly System.Threading.SemaphoreSlim _connGate;

    private readonly IIdentifier[] _workerId;
    private System.Net.Sockets.TcpClient _client;
    private System.Net.Sockets.NetworkStream _stream;
    private System.Threading.CancellationTokenSource _lifectx;
    private System.Threading.Channels.Channel<IPacket> _sendQueue;

    private StreamSender<IPacket> _outbound;
    private StreamReceiver<IPacket> _inbound;

    private volatile System.Boolean _closed;
    private volatile System.Boolean _ioHealthy;

    private System.Int32 _discNotified; // 0/1 gate for Disconnected
    private System.Int32 _sendWorkerStarted;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the context associated with the network connection.
    /// </summary>
    public TransportOptions Options { get; }

    /// <summary>
    /// Gets the FIFO cache that stores incoming packets received from the remote server.
    /// </summary>
    public FifoCache<IPacket> Incoming { get; }

    // In ReliableClient class
    /// <summary>
    /// Indicates whether a 32-byte session key has been installed (handshake completed).
    /// </summary>
    public System.Boolean IsHandshaked => Options?.EncryptionKey is { Length: 32 };

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(_stream), nameof(_outbound), nameof(_inbound))]
    public System.Boolean IsConnected => !_closed && _ioHealthy && _stream is { CanRead: true, CanWrite: true };

    #endregion Properties

    #region Events

    /// <summary>
    /// Raised after a successful connection is established.
    /// Executed on the calling thread of ConnectAsync.
    /// </summary>
    public event System.Action Connected;

    /// <summary>
    /// Raised whenever a packet is received on the background network worker.
    /// Executed on a background thread; do not touch Unity API here.
    /// </summary>
    public event System.Action<IPacket> PacketReceived;

    /// <summary>
    /// Raised when the connection is closed or the receive loop exits due to an error.
    /// Executed on a background thread; ex is null for normal Dispose().
    /// </summary>
    public event System.Action<System.Exception> Disconnected;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ReliableClient"/> class.
    /// </summary>
    public ReliableClient()
    {
        _connGate = new(1, 1);
        _workerId = new IIdentifier[2];
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };

        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Incoming = new FifoCache<IPacket>(Options.IncomingSize);
    }

    #endregion Constructor

    #region APIs

    /// <summary>
    /// Asynchronously connects to a remote server within a specified timeout period.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.SkipLocalsInit]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_stream))]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_outbound))]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_inbound))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    public async System.Threading.Tasks.Task ConnectAsync(
            System.Int32 timeout = 30000,
            System.Threading.CancellationToken cancellationToken = default)
    {
        _closed = false;
        _discNotified = 0;

        _client?.Close();
        _client = new System.Net.Sockets.TcpClient
        {
            NoDelay = true,
            LingerState = new System.Net.Sockets.LingerOption(false, 0)
        };
        _client.Client.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.KeepAlive, true);


        // Optional: allow address reuse during dev/testing
        _client.Client.SetSocketOption(
            System.Net.Sockets.SocketOptionLevel.Socket,
            System.Net.Sockets.SocketOptionName.ReuseAddress, true);

        _lifectx?.Dispose();
        _lifectx = new System.Threading.CancellationTokenSource();
        _sendQueue = System.Threading.Channels.Channel.CreateBounded<IPacket>(
            new System.Threading.Channels.BoundedChannelOptions(capacity: Options.OutboundQueueSize > 0 ? Options.OutboundQueueSize : 1024)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest, // backpressure
                SingleReader = true,
                SingleWriter = false
            });

        // Start send worker exactly once per connection
        System.Threading.Interlocked.Exchange(ref _sendWorkerStarted, 0);
        this.StartSendWorker();

        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        await _connGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (IsConnected)
            {
                return;
            }

            await _client.ConnectAsync(Options.Address, Options.Port, cts.Token);

            _stream = _client.GetStream();

            _stream.ReadTimeout = 10_000;
            _stream.WriteTimeout = 10_000;

            _outbound = new StreamSender<IPacket>(_stream);
            _inbound = new StreamReceiver<IPacket>(_stream);

            // Notify connected
            SafeInvoke(Connected, InstanceManager.Instance.GetExistingInstance<ILogger>());

            // Start background receive worker through TaskManager
            IWorkerHandle woker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
                name: $"tcp-recv-{Options.Address}:{Options.Port}",
                group: "network",
                async (ctx, ct) =>
                {
                    while (!ct.IsCancellationRequested && IsConnected)
                    {
                        IPacket packet = null;

                        try
                        {
                            packet = await _inbound!.ReceiveAsync(ct).ConfigureAwait(false);
                        }
                        catch (System.OperationCanceledException)
                        {
                            break;
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Error($"Network receive loop error: {ex.Message}");

                            // Push FIFO (with simple backpressure policy)
                            if (packet != null)
                            {
                                if (Options.IncomingSize > 0 || PacketReceived == null)
                                {
                                    Incoming.Push(packet);
                                    continue;
                                }

                                SafeInvoke(PacketReceived, packet, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }

                            if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
                            {
                                SafeInvoke(Disconnected, ex, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }

                            this.MarkIoDead(ex);
                            this.Disconnect();
                            break;
                        }

                        SafeInvoke(PacketReceived, packet, InstanceManager.Instance.GetExistingInstance<ILogger>());
                    }
                },
                new WorkerOptions
                {
                    Tag = "tcp",
                    OnFailed = (st, ex) => InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn($"Worker failed: {ex.Message}")
                });

            _workerId[0] = woker.Id;
        }
        catch (System.OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // internal timeout
            throw new System.TimeoutException($"ConnectAsync timeout after {timeout} ms.");
        }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // propagate user cancel
            throw;
        }
        finally
        {
            _connGate.Release();
        }
    }

    /// <summary>
    /// Asynchronously sends a packet over the active connection.
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="ct">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the client is not connected.
    /// </exception>
    /// <exception cref="System.OperationCanceledException">
    /// Thrown if <paramref name="ct"/> is canceled before or during the send.
    /// </exception>
    /// <exception cref="System.IO.IOException">
    /// Thrown if an I/O error occurs while writing to the underlying stream.
    /// </exception>
    public System.Threading.Tasks.ValueTask SendAsync(IPacket packet, System.Threading.CancellationToken ct = default)
    {
        if (_outbound is null || !IsConnected)
        {
            throw new System.InvalidOperationException("Not connected.");
        }

        // Enqueue with back-pressure; avoids drops under load
        return _sendQueue.Writer.WriteAsync(packet, ct);
    }

    /// <summary>
    /// Closes the network connection and releases resources.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Disconnect()
    {
        _closed = true;
        _lifectx?.Cancel();

        if (_workerId is not null)
        {
            for (System.Int32 i = 0; i < _workerId.Length; i++)
            {
                if (_workerId[i] is null)
                {
                    continue;
                }

                _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                            .CancelWorker(_workerId[i]);
            }

        }

        this.DeepClose();

        try { _stream?.Dispose(); } catch { /* swallow */ }
        try { _client?.Close(); } catch { /* swallow */ }
        try { _sendQueue?.Writer.TryComplete(); } catch { /* ignore */ }

        _outbound = null;
        _inbound = null;

        // Notify once on explicit disconnect as well
        if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
        {
            SafeInvoke(Disconnected, null, InstanceManager.Instance.GetExistingInstance<ILogger>());
        }
    }

    /// <summary>
    /// Releases the resources used by the <see cref="ReliableClient"/> instance.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Dispose()
    {
        this.Disconnect();

        _lifectx?.Dispose();
        System.GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private Methods

    private void MarkIoDead(System.Exception ex = null)
    {
        _ioHealthy = false;
        if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
        {
            SafeInvoke(Disconnected, ex, InstanceManager.Instance.GetExistingInstance<ILogger>());
        }
    }

    private void StartSendWorker()
    {
        if (System.Threading.Interlocked.Exchange(ref _sendWorkerStarted, 1) == 1)
        {
            return;
        }

        IWorkerHandle worker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
            name: $"tcp-send-{Options.Address}:{Options.Port}",
            group: "network",
            async (_, ct) =>
            {
                var token = System.Threading.CancellationTokenSource
                    .CreateLinkedTokenSource(ct, _lifectx.Token).Token;

                while (!token.IsCancellationRequested && IsConnected)
                {
                    if (!await _sendQueue.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                    {
                        break;
                    }

                    while (_sendQueue.Reader.TryRead(out var pkt))
                    {
                        try
                        {
                            await _outbound.SendAsync(pkt, token).ConfigureAwait(false);
                        }
                        catch (System.OperationCanceledException)
                        {
                            throw;
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn($"Send failed: {ex.Message}");

                            MarkIoDead(ex);
                            Disconnect(); // fail-fast đóng phiên
                            return;
                        }
                    }
                }
            },
            new WorkerOptions { Tag = "tcp" }
        );

        _workerId[1] = worker.Id;
    }

    // Add to ReliableClient
    private static void AbortiveClose(System.Net.Sockets.Socket s)
    {
        if (s == null)
        {
            return;
        }

        try
        {
            // Enable abortive close (RST) to avoid TIME_WAIT
            s.LingerState = new System.Net.Sockets.LingerOption(true, 0);
        }
        catch { /* ignore */ }

        try
        {
            // Best effort shutdown; may throw if already closed
            s.Shutdown(System.Net.Sockets.SocketShutdown.Both);
        }
        catch { /* ignore */ }

        try
        {
            // Close immediately; Close(0) is equivalent to Dispose()
            s.Close(0);
        }
        catch { /* ignore */ }

        try { s.Dispose(); } catch { /* ignore */ }
    }

    private void DeepClose()
    {
        // 1) Dispose the stream first to stop pending I/O
        try { _stream?.Dispose(); } catch { /* ignore */ }
        _stream = null;

        // 2) Abortive-close underlying socket
        try { AbortiveClose(_client?.Client); } catch { /* ignore */ }

        // 3) Dispose TcpClient wrapper
        try { _client?.Dispose(); } catch { /* ignore */ }
        _client = null;

        // 4) Clear transport pipe refs
        _outbound = null;
        _inbound = null;
    }

    private static void SafeInvoke(System.Action evt, ILogger log)
    {
        var d = evt;
        if (d is null)
        {
            return;
        }

        foreach (System.Action h in d.GetInvocationList().Cast<System.Action>())
        {
            try { h(); } catch (System.Exception ex) { log?.Warn($"Subscriber threw: {ex}"); }
        }
    }

    private static void SafeInvoke<T>(System.Action<T> evt, T arg, ILogger log)
    {
        var d = evt; if (d is null)
        {
            return;
        }

        foreach (System.Action<T> h in d.GetInvocationList().Cast<System.Action<T>>())
        {
            try { h(arg); }
            catch (System.Exception ex) { log?.Warn($"Subscriber threw: {ex}"); }
        }
    }

    #endregion Private Methods
}