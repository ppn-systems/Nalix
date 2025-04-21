using Nalix.Common.Exceptions;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Network.Listeners;

public abstract partial class Listener
{
    /// <summary>
    /// Stops the listener from accepting further connections.
    /// </summary>
    public void EndListening()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _cts?.Cancel();

        try
        {
            // Close the socket listener to deactivate the accept
            _listenerSocket.Close();
        }
        catch (Exception ex)
        {
            _logger.Error("Error closing listener socket: {0}", ex.Message);
        }

        // Wait for the listener thread to complete with a timeout
        if (_listenerThread?.IsAlive == true)
        {
            _listenerThread.Join(TimeSpan.FromSeconds(5));
        }

        _isListening = false;
        _logger.Info("Listener stopped.");
    }

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public void BeginListening(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isListening) return;

        _isListening = true;
        _logger.Debug("Starting listener");

        // Create a linked token source to combine external cancellation with Internal cancellation
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
                    // Bind and Listen
                    _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
                    _listenerSocket.Listen(Listener.SocketBacklog);

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
                    foreach (Thread thread in acceptThreads)
                    {
                        // Wait max 1 second for each thread
                        if (thread.IsAlive) thread.Join(1000);
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
                    try { _listenerSocket?.Close(); } catch { }
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

        Thread? oldThread = Interlocked.Exchange(ref _listenerThread, newThread);

        newThread.Start();
        oldThread?.Join(TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Starts listening for incoming connections and processes them using the specified protocol.
    /// The listening process can be cancelled using the provided <see cref="CancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the listening process.</param>
    public async Task BeginListeningAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isListening) return;

        _isListening = true;
        _logger.Debug("Starting listener");
        const int maxParallelAccepts = 5;

        // Create a linked token source to combine external cancellation with Internal cancellation
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        await _listenerLock.WaitAsync(linkedToken).ConfigureAwait(false);

        try
        {
            // Bind and Listen
            _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, _port));
            _listenerSocket.Listen(Listener.SocketBacklog);

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
            try
            {
                _listenerSocket.Close();
            }
            catch { }

            _listenerLock.Release();
        }
    }
}
