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

    private IIdentifier _workerId;
    private System.Net.Sockets.TcpClient _client;
    private System.Net.Sockets.NetworkStream _stream;

    private StreamSender<IPacket> _outbound;
    private StreamReceiver<IPacket> _inbound;

    private volatile System.Boolean _closed;
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

    /// <summary>
    /// Gets a value indicating whether the client is connected to the server.
    /// </summary>
    public System.Boolean IsConnected => !_closed && _client is { Connected: true } && _stream?.CanRead == true && _stream.CanWrite;

    #endregion Properties

    #region Events

    /// <summary>
    /// Raised whenever a packet is received on the background network worker.
    /// Executed on a background thread; do not touch Unity API here.
    /// </summary>
    public event System.Action<IPacket> PacketReceived;

    /// <summary>
    /// Raised after a successful connection is established.
    /// Executed on the calling thread of ConnectAsync.
    /// </summary>
    public event System.Action Connected;

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
        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Incoming = new FifoCache<IPacket>(Options.IncomingSize);
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };
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
        _client = new System.Net.Sockets.TcpClient { NoDelay = true };

        using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await _client.ConnectAsync(Options.Address, Options.Port, cts.Token);

            _stream = _client.GetStream();
            _outbound = new StreamSender<IPacket>(_stream);
            _inbound = new StreamReceiver<IPacket>(_stream);

            // Notify connected
            Connected?.Invoke();

            // Start background receive worker through TaskManager
            IWorkerHandle woker = InstanceManager.Instance.GetOrCreateInstance<TaskManager>().StartWorker(
                name: $"tcp-recv-{Options.Address}:{Options.Port}",
                group: "network",
                async (ctx, ct) =>
                {
                    while (!ct.IsCancellationRequested && IsConnected)
                    {
                        IPacket packet;
                        try
                        {
                            packet = await _inbound!.ReceiveAsync(ct).ConfigureAwait(false);

                            // Raise event for reactive waiters (AwaitControlAsync, etc.)
                            PacketReceived?.Invoke(packet);
                        }
                        catch (System.OperationCanceledException)
                        {
                            break;
                        }
                        catch (System.Exception ex)
                        {
                            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                    .Error($"Network receive loop error: {ex.Message}");

                            if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
                            {
                                SafeInvoke(Disconnected, ex, InstanceManager.Instance.GetExistingInstance<ILogger>());
                            }

                            Disconnect();
                            break;
                        }

                        // Push FIFO (with simple backpressure policy)
                        if (Options.IncomingSize > 0)
                        {
                            this.Incoming.Push(packet);
                        }

                        // Isolate subscriber faults
                        SafeInvoke(PacketReceived, packet,
                                   InstanceManager.Instance.GetExistingInstance<ILogger>());
                    }
                },
                new WorkerOptions
                {
                    Tag = "tcp",
                    OnFailed = (st, ex) => InstanceManager.Instance.GetExistingInstance<ILogger>()?.Warn($"Worker failed: {ex.Message}")
                });

            _workerId = woker.Id;
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
    public System.Threading.Tasks.Task SendAsync(
        IPacket packet,
        System.Threading.CancellationToken ct = default)
        => (_outbound ?? throw new System.InvalidOperationException("Not connected.")).SendAsync(packet, ct);

    /// <summary>
    /// Closes the network connection and releases resources.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Disconnect()
    {
        _closed = true;
        _ = InstanceManager.Instance.GetOrCreateInstance<TaskManager>()
                                    .CancelWorker(_workerId);

        try { _stream?.Dispose(); } catch { /* swallow */ }
        try { _client?.Close(); } catch { /* swallow */ }

        _outbound = null;
        _inbound = null;
    }

    /// <summary>
    /// Releases the resources used by the <see cref="ReliableClient"/> instance.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    public void Dispose()
    {
        Disconnect();
        // invoke-once Disconnected(null)
        if (System.Threading.Interlocked.Exchange(ref _discNotified, 1) == 0)
        {
            SafeInvoke(Disconnected, null, InstanceManager.Instance.GetExistingInstance<ILogger>());
        }

        System.GC.SuppressFinalize(this);
    }

    #endregion APIs

    #region Private Methods

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