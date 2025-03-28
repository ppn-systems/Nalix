using Notio.Common.Caching;
using Notio.Common.Connection;
using Notio.Common.Exceptions;
using Notio.Common.Logging;
using Notio.Network.Networking.Protocols;
using Notio.Shared.Configuration;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Network.Listeners;

/// <summary>
/// An abstract base class for network listeners.
/// This class manages the process of accepting incoming network connections
/// and handling the associated protocol processing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Listener"/> class with the specified port, protocol, buffer pool, and logger.
/// </remarks>
/// <param name="port">The port to listen on.</param>
/// <param name="protocol">The protocol to handle the connections.</param>
/// <param name="bufferPool">The buffer pool for managing connection buffers.</param>
/// <param name="logger">The logger to log events and errors.</param>
public abstract class Listener(int port, IProtocol protocol, IBufferPool bufferPool, ILogger logger)
    : IListener, IDisposable
{
    private const int True = 1;
    private const int False = 0;
    private static ListenerConfig Config => ConfigurationStore.Instance.Get<ListenerConfig>();

    private readonly int _port = port;
    private readonly SemaphoreSlim _listenerLock = new(1, 1);
    private readonly TcpListener _tcpListener = new(IPAddress.Any, port);
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IProtocol _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
    private readonly IBufferPool _bufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));

    private bool _isDisposed;
    private Thread? _listenerThread;
    private CancellationTokenSource? _cts;

    #region Public Methods

    /// <summary>
    /// Gets the current state of the listener.
    /// </summary>
    public bool IsListening => _listenerThread != null && _listenerThread.IsAlive;

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

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public void BeginListening(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed || _tcpListener.Server == null, this);

        if (this.IsListening) return;

        _logger.Debug("Starting to listen for incoming connections.");

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
                    _logger.Info($"{_protocol} is online on port {_port}");

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
                                _logger.Debug($"Accept thread {threadIndex} cancelled");
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Error in accept thread {threadIndex}: {ex.Message}");
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
                    _logger.Info($"Listener on port {_port} stopped gracefully");
                }
                catch (SocketException ex)
                {
                    _logger.Error($"Could not start {_protocol} on port {_port}: {ex.Message}");
                    throw new InternalErrorException($"Could not start {_protocol} on port {_port}", ex);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Critical error in listener on port {_port}: {ex.Message}");
                    throw new InternalErrorException($"Critical error in listener on port {_port}", ex);
                }
                finally
                {
                    _tcpListener.Stop();
                    _listenerLock.Release();
                    _logger.Debug("Stopped listening for incoming connections.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Unhandled exception in listener thread: {ex.Message}");
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
                Connection.Connection connection = new(socket, _bufferPool, _logger);
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
                _logger.Error($"Error accepting connection on port {_port}", ex);
                // Brief delay to prevent CPU spinning on repeated errors
                Task.Delay(50, cancellationToken);
            }
        }

        _logger.Debug("Stopped accepting incoming connections.");
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

        _logger.Debug("Starting to listen for incoming connections.");
        const int maxParallelAccepts = 5;

        // Create a linked token source to combine external cancellation with internal cancellation
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        await _listenerLock.WaitAsync(linkedToken)
                           .ConfigureAwait(false);

        try
        {
            _tcpListener.Start();
            _logger.Info($"{_protocol} is online on port {_port}");

            // Create multiple accept tasks in parallel for higher throughput
            Task[] acceptTasks = new Task[maxParallelAccepts];

            for (int i = 0; i < maxParallelAccepts; i++)
                acceptTasks[i] = AcceptConnectionsAsync(linkedToken);

            await Task.WhenAll(acceptTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Info($"Listener on port {_port} stopped gracefully");
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
            _logger.Debug("Stopped listening for incoming connections.");
        }
    }

    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void EndListening()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _logger.Debug("Stopping the listener.");

        _cts?.Cancel();

        if (_tcpListener?.Server != null)
        {
            _logger.Info($"Stopping listener on port {_port}");
            _tcpListener.Stop();
        }

        // Wait for the listener thread to complete with a timeout
        if (_listenerThread?.IsAlive == true)
            _listenerThread.Join(TimeSpan.FromSeconds(5));

        _logger.Debug("Listener stopped.");
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the listener.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            _logger.Info($"Disposing listener on port {_port}");

            _cts?.Cancel();
            _cts?.Dispose();

            Interlocked.Exchange(ref _listenerThread, null)?.Join();

            _tcpListener.Stop();
            _listenerLock.Dispose();
        }

        _isDisposed = true;
        _logger.Debug("Listener disposed.");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Accepts connections in a loop until cancellation is requested
    /// </summary>
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
                _logger.Error($"Error accepting connection on port {_port}", ex);
                // Brief delay to prevent CPU spinning on repeated errors
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.Debug("Stopped accepting incoming connections.");
    }

    /// <summary>
    /// Processes a new connection using the protocol handler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessConnection(IConnection connection)
    {
        try
        {
            _logger.Debug($"Processing new connection from {connection.RemoteEndPoint}");
            _protocol.OnAccept(connection);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing new connection from {connection.RemoteEndPoint}", ex);
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

        Connection.Connection connection = new(socket, _bufferPool, _logger);

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
        _logger.Debug($"Closing connection from {args.Connection.RemoteEndPoint}");
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
    private static void ConfigureHighPerformanceSocket(Socket socket)
    {
        // Performance tuning
        socket.NoDelay = Config.NoDelay;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            socket.SetSocketOption(SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress, Config.ReuseAddress ? True : False);
    }

    #endregion
}
