// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Abstractions;
using Nalix.Common.Logging;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Tasks;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.Framework.Tasks;
using Nalix.Framework.Tasks.Options;
using Nalix.SDK.Remote.Configuration;
using Nalix.SDK.Remote.Internal;
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

    private FRAME_SENDER<IPacket> _outbound;
    private FRAME_READER<IPacket> _inbound;

    private volatile System.Boolean _closed;
    private volatile System.Boolean _ioHealthy;

    private System.Int32 _discNotified; // 0/1 gate for Disconnected

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
    public System.Boolean IsConnected
    {
        get
        {
            if (_closed || !_ioHealthy || _stream is null)
            {
                return false;
            }

            try
            {
                return _stream.CanRead && _stream.CanWrite;
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
        }
    }

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
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_inbound))]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_outbound))]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1163:Unused parameter", Justification = "<Pending>")]
    public async System.Threading.Tasks.Task ConnectAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] System.Int32 timeout = 30000,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken cancellationToken = default)
    {
        _closed = false;
        _ioHealthy = true;
        _discNotified = 0;

        _client?.Close();
        CONFIGURE_SOCKET(_client);

        using System.Threading.CancellationTokenSource cts =
            System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        cts.CancelAfter(timeout);

        await _connGate.WaitAsync(cts.Token).ConfigureAwait(false);

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

            _outbound = new FRAME_SENDER<IPacket>(_stream);
            _inbound = new FRAME_READER<IPacket>(_stream);

            // Notify connected
            SAFE_INVOKE(Connected, InstanceManager.Instance.GetExistingInstance<ILogger>());

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
                            packet = await _inbound!.RECEIVE_ASYNC(ct).ConfigureAwait(false);
                        }
                        catch (System.OperationCanceledException)
                        {
                            break;
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Error($"Network receive loop error: {ex.Message}");

                            if (packet != null)
                            {
                                SAFE_INVOKE(PacketReceived, packet, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }

                            if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
                            {
                                SAFE_INVOKE(Disconnected, ex, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }

                            this.MARK_IO_DEAD(ex);
                            this.Disconnect();
                            break;
                        }

                        SAFE_INVOKE(PacketReceived, packet, InstanceManager.Instance.GetExistingInstance<ILogger>());
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
            _ = _connGate.Release();
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
    /// Thrown if an IEndpointKey /O error occurs while writing to the underlying stream.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_outbound))]
    public System.Threading.Tasks.Task SendAsync(
        [System.Diagnostics.CodeAnalysis.NotNull] IPacket packet,
        [System.Diagnostics.CodeAnalysis.NotNull] System.Threading.CancellationToken ct = default)
        => (_outbound ?? throw new System.InvalidOperationException("Not connected.")).SEND_ASYNC(packet, ct);

    /// <summary>
    /// Closes the network connection and releases resources.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Disconnect()
    {
        _closed = true;
        _ioHealthy = false;

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

        this.DEEP_CLOSE();

        try { _stream?.Dispose(); } catch { /* swallow */ }
        try { _client?.Close(); } catch { /* swallow */ }

        _outbound = null;
        _inbound = null;

        // Notify once on explicit disconnect as well
        if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
        {
            SAFE_INVOKE(Disconnected, null, InstanceManager.Instance.GetExistingInstance<ILogger>());
        }
    }

    /// <summary>
    /// Releases the resources used by the <see cref="ReliableClient"/> instance.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Dispose()
    {
        this.Disconnect();
        System.GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private Methods

    private static void CONFIGURE_SOCKET([System.Diagnostics.CodeAnalysis.NotNull] System.Net.Sockets.TcpClient client)
    {
        if (client is null)
        {
            return;
        }

        try
        {
            client.NoDelay = true;
            client.SendBufferSize = 8192;
            client.ReceiveBufferSize = 8192;
        }
        catch { /* ignore */ }

        try
        {
            // Default to graceful linger of 0 (no lingering) during normal operation.
            // Final abortive close is performed in DEEP_CLOSE via ABORTIVE_CLOSE.
            client.LingerState = new System.Net.Sockets.LingerOption(false, 0);
        }
        catch { /* ignore */ }

        try
        {
            client.Client?.SetSocketOption(
                System.Net.Sockets.SocketOptionLevel.Socket,
                System.Net.Sockets.SocketOptionName.KeepAlive, true);
        }
        catch { /* ignore */ }

        if (System.OperatingSystem.IsWindows())
        {
            _ = client.Client.IOControl(System.Net.Sockets.IOControlCode.KeepAliveValues,
                              KEEP_ALIVE_CONFIG(keepAliveTimeMs: 20_000, keepAliveIntervalMs: 5_000), null);
        }
    }

    private static System.Byte[] KEEP_ALIVE_CONFIG(System.UInt32 keepAliveTimeMs, System.UInt32 keepAliveIntervalMs)
    {
        System.Byte[] buffer = new System.Byte[12];
        System.BitConverter.GetBytes(1u).CopyTo(buffer, 0); // Enable
        System.BitConverter.GetBytes(keepAliveTimeMs).CopyTo(buffer, 4); // Idle time
        System.BitConverter.GetBytes(keepAliveIntervalMs).CopyTo(buffer, 8); // Interval
        return buffer;
    }

    private void MARK_IO_DEAD(System.Exception ex = null)
    {
        _ioHealthy = false;
        if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
        {
            SAFE_INVOKE(Disconnected, ex, InstanceManager.Instance.GetExistingInstance<ILogger>());
        }
    }

    private static void ABORTIVE_CLOSE(System.Net.Sockets.Socket s)
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

    private void DEEP_CLOSE()
    {
        // 1) Dispose the stream first to stop pending IEndpointKey /O
        try { _stream?.Dispose(); } catch { /* ignore */ }
        _stream = null;

        // 2) Abortive-close underlying socket
        try { ABORTIVE_CLOSE(_client?.Client); } catch { /* ignore */ }

        // 3) Dispose TcpClient wrapper
        try { _client?.Dispose(); } catch { /* ignore */ }
        _client = null;

        // 4) Clear transport pipe refs
        _outbound = null;
        _inbound = null;
    }

    private static void SAFE_INVOKE(System.Action evt, ILogger log)
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

    private static void SAFE_INVOKE<T>(System.Action<T> evt, T arg, ILogger log)
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