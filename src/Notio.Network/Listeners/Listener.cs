using Notio.Common.Caching;
using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Network.Configurations;
using Notio.Network.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
public abstract class Listener : IListener, IDisposable
{
    #region Fields

    private static readonly TcpConfig Config;

    private readonly int _port;
    private readonly ILogger _logger;
    private readonly IProtocol _protocol;
    private readonly IBufferPool _buffer;
    private readonly TcpListener _tcpListener;
    private readonly SemaphoreSlim _listenerLock;

    private bool _isDisposed;
    private Thread? _listenerThread;
    private CancellationTokenSource? _cts;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    public bool IsListening => _listenerThread != null && _listenerThread.IsAlive;

    #endregion

    #region Constructors

    static Listener()
    {
        Config = ConfigurationStore.Instance.Get<TcpConfig>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="port">The port to listen on.</param>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentNullException.ThrowIfNull(protocol, nameof(protocol));
        ArgumentNullException.ThrowIfNull(bufferPool, nameof(bufferPool));

        _port = port;
        _logger = logger;
        _protocol = protocol;
        _buffer = bufferPool;
        _listenerLock = new SemaphoreSlim(1, 1);
        _tcpListener = new TcpListener(IPAddress.Any, port);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Listener"/> class using the port defined in the configuration,
    /// and the specified protocol, buffer pool, and logger.
    /// </summary>
    /// <param name="protocol">The protocol to handle the connections.</param>
    /// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
    /// <param name="logger">The logger to log events and errors.</param>
    protected Listener(IProtocol protocol, IBufferPool bufferPool, ILogger logger)
        : this(Config.Port, protocol, bufferPool, logger)
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public void BeginListening(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed || _tcpListener.Server == null, this);

        if (this.IsListening) return;

        _logger.Debug("Starting listener");

        // Create a linked token source to combine external cancellation with internal cancellation
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        // Create and start listener thread
        var newThread = new Thread(() =>
        {
            try
            {
                // Wait for the lock synchronously
                _listenerLock.Wait(linkedToken);

                try
                {
                    _tcpListener.Start();
                    _logger.Info("{0} online on {1}", _protocol, _port);

                    // Create worker threads for accepting connections
                    const int maxParallelAccepts = 5;
                    Thread[] acceptThreads = new Thread[maxParallelAccepts];

                    for (int i = 0; i < maxParallelAccepts; i++)
                    {
                        int threadIndex = i; // Capture for closure
                        acceptThreads[i] = new Thread(() =>
                        {
                            try
                            {
                                this.AcceptConnections(linkedToken);
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.Debug("Accept thread {0} cancelled", threadIndex);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("Accept thread {0} error: {1}", threadIndex, ex.Message);
                            }
                        })
                        {
                            IsBackground = true,
                            Name = $"AcceptThread-{_port}-{i}"
                        };

                        acceptThreads[i].Start();
                    }

                    // Wait for cancellation
                    try
                    {
                        linkedToken.WaitHandle.WaitOne();
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                    }

                    // Optionally wait for worker threads to complete
                    foreach (var thread in acceptThreads)
                    {
                        if (thread.IsAlive)
                            thread.Join(1000); // Wait max 1 second for each thread
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("Listener on {0} stopped", _port);
                }
                catch (SocketException ex)
                {
                    _logger.Error("{0} start failed on {1}: {2}", _protocol, _port, ex.Message);
                    throw new InternalErrorException($"Could not start {_protocol} on port {_port}", ex);
                }
                catch (Exception ex)
                {
                    _logger.Error("Critical error on {0}: {1}", _port, ex.Message);
                    throw new InternalErrorException($"Critical error in listener on port {_port}", ex);
                }
                finally
                {
                    _tcpListener.Stop();
                    _listenerLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Thread error: {0}", ex.Message);
            }
        })
        {
            IsBackground = true,
            Name = $"{_protocol}Listener-{_port}"
        };

        Interlocked.Exchange(ref _listenerThread, newThread)?.Join();
        newThread.Start();
    }

    /// <summary>
    /// Synchronous method for accepting connections
    /// </summary>
    /// <param name="cancellationToken">Token for cancellation</param>
    private void AcceptConnections(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Accept client socket synchronously with polling to support cancellation
                if (!_tcpListener.Pending())
                {
                    Thread.Sleep(50); // Small delay to prevent CPU spinning
                    continue;
                }

                Socket socket = _tcpListener.AcceptSocket();
                ConfigureHighPerformanceSocket(socket);

                // Create and process connection similar to async version
                Connection.Connection connection = new(socket, _buffer, _logger);
                connection.OnCloseEvent += OnConnectionClose;
                connection.OnProcessEvent += _protocol.ProcessMessage!;
                connection.OnPostProcessEvent += _protocol.PostProcessMessage!;

                // Process the connection
                ProcessConnection(connection);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted ||
                                             ex.SocketErrorCode == SocketError.ConnectionAborted)
            {
                // Socket was closed or interrupted
                break;
            }
            catch (ObjectDisposedException)
            {
                // Socket was disposed
                break;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Error("Accept error on {0}: {1}", _port, ex.Message);
                // Brief delay to prevent CPU spinning on repeated errors
                Task.Delay(50, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public async Task BeginListeningAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed || _tcpListener.Server == null, this);

        if (this.IsListening) return;

        _logger.Debug("Starting listener");
        const int maxParallelAccepts = 5;

        // Create a linked token source to combine external cancellation with internal cancellation
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        await _listenerLock.WaitAsync(linkedToken)
                           .ConfigureAwait(false);

        try
        {
            _tcpListener.Start();
            _logger.Info("{0} online on {1}", _protocol, _port);

            // Create multiple accept tasks in parallel for higher throughput
            Task[] acceptTasks = new Task[maxParallelAccepts];

            for (int i = 0; i < maxParallelAccepts; i++)
                acceptTasks[i] = AcceptConnectionsAsync(linkedToken);

            await Task.WhenAll(acceptTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Listener on {0} stopped", _port);
        }
        catch (SocketException ex)
        {
            throw new InternalErrorException($"Could not start {_protocol} on port {_port}", ex);
        }
        catch (Exception ex)
        {
            throw new InternalErrorException($"Critical error in listener on port {_port}", ex);
        }
        finally
        {
            _tcpListener.Stop();
            _listenerLock.Release();
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void EndListening()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _cts?.Cancel();

        if (_tcpListener?.Server != null)
        {
            _logger.Info("Stopping on {0}", _port);
            _tcpListener.Stop();
        }

        // Wait for the listener thread to complete with a timeout
        if (_listenerThread?.IsAlive == true)
        {
            _listenerThread.Join(TimeSpan.FromSeconds(5));
        }

        _logger.Info("Listener stopped.");
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _logger.Info("Disposing on {0}", _port);

            _cts?.Cancel();
            _cts?.Dispose();

            Interlocked.Exchange(ref _listenerThread, null)?.Join();

            _tcpListener.Stop();
            _listenerLock.Dispose();
        }

        _isDisposed = true;
        _logger.Debug("Listener disposed");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Accepts connections in a loop until cancellation is requested
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IConnection connection = await CreateConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);

                this.ProcessConnection(connection);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break; // Exit loop on cancellation
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.Error("Accept error on {0}: {1}", _port, ex.Message);
                // Brief delay to prevent CPU spinning on repeated errors
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Processes a new connection using the protocol handler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessConnection(IConnection connection)
    {
        try
        {
            _logger.Debug("New connection from {0}", connection.RemoteEndPoint);
            _protocol.OnAccept(connection);
        }
        catch (Exception ex)
        {
            _logger.Error("Process error from {0}: {1}", connection.RemoteEndPoint, ex.Message);
            connection.Close();
        }
    }

    /// <summary>
    /// Creates a new connection from an incoming socket.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the connection creation process.</param>
    /// <returns>A task representing the connection creation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        Socket socket = await _tcpListener.AcceptSocketAsync(cancellationToken)
            .ConfigureAwait(false);

        ConfigureHighPerformanceSocket(socket);

        Connection.Connection connection = new(socket, _buffer, _logger);

        // Use weak event pattern to avoid memory leaks
        connection.OnCloseEvent += OnConnectionClose;
        connection.OnProcessEvent += _protocol.ProcessMessage!;
        connection.OnPostProcessEvent += _protocol.PostProcessMessage!;
        return connection;
    }

    /// <summary>
    /// Handles the closure of a connection by unsubscribing from its events.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="args">The connection event arguments.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnConnectionClose(object? sender, IConnectEventArgs args)
    {
        _logger.Debug("Closing {0}", args.Connection.RemoteEndPoint);
        // De-subscribe to prevent memory leaks
        args.Connection.OnCloseEvent -= OnConnectionClose;
        args.Connection.OnProcessEvent -= _protocol.ProcessMessage!;
        args.Connection.OnPostProcessEvent -= _protocol.PostProcessMessage!;

        args.Connection.Dispose();
    }

    /// <summary>
    /// Configures the socket for high-performance operation by setting buffer sizes, timeouts, and keep-alive options.
    /// </summary>
    /// <param name="socket">The socket to configure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    private static void ConfigureHighPerformanceSocket(Socket socket)
    {
        // Performance tuning
        socket.NoDelay = Config.NoDelay;
        socket.SendBufferSize = Config.BufferSize;
        socket.ReceiveBufferSize = Config.BufferSize;
        socket.LingerState = new LingerOption(true, TcpConfig.False);

        if (Config.KeepAlive)
        {
            // Windows specific settings
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            if (Config.IsWindows)
            {
                // Windows specific settings
                socket.IOControl(IOControlCode.KeepAliveValues, KeepAliveConfig(), null);
            }
        }

        if (!Config.IsWindows)
        {
            // Linux, MacOS, etc.
            socket.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress, Config.ReuseAddress ?
                TcpConfig.True : TcpConfig.False);
        }
    }

    /// <summary>
    /// Creates the byte array for configuring Keep-Alive on Windows.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] KeepAliveConfig()
    {
        int on = 1;              // Turning on Keep-Alive
        int time = 10_000;       // 10 seconds without data, send Keep-Alive
        int interval = 5_000;    // SendPacket every 5 seconds if there is no response

        byte[] keepAlive = new byte[12];
        BitConverter.GetBytes(on).CopyTo(keepAlive, 0);
        BitConverter.GetBytes(time).CopyTo(keepAlive, 4);
        BitConverter.GetBytes(interval).CopyTo(keepAlive, 8);

        return keepAlive;
    }


    #endregion
}
