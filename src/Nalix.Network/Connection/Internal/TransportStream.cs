// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Logging.Abstractions;
using Nalix.Common.Packets;
using Nalix.Framework.Time;
using Nalix.Network.Internal.Pooled;
using Nalix.Shared.Injection;
using Nalix.Shared.Memory.Pooling;

namespace Nalix.Network.Connection.Internal;

/// <summary>
/// Manages the socket connection and handles sending/receiving data with caching and logging.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TransportStream"/> class.
/// </remarks>
/// <param name="socket">The socket.</param>
/// <param name="cts">The cancellation token source.</param>
internal class TransportStream(
    System.Net.Sockets.Socket socket, System.Threading.CancellationTokenSource cts) : System.IDisposable
{
    #region Fields

    private readonly TransportCache _cache = new();
    private readonly System.Net.Sockets.Socket _socket = socket;
    private readonly System.Threading.CancellationTokenSource _cts = cts;

    private System.Boolean _disposed;
    private volatile System.Boolean _keepReading;
    private System.Threading.CancellationToken _rxToken;                    // cached linked token
    private System.Threading.CancellationTokenSource? _rxCts;               // linked CTS reused for the whole loop
    private System.Threading.CancellationToken _lastExternalToken;          // remember last external to avoid relinking
    private System.Threading.CancellationTokenRegistration? _rxShutdownReg; // socket shutdown registration on cancel
    private System.Byte[] _buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                            .Rent(256);

    #endregion Fields

    #region Properties

    /// <summary>
    /// Event triggered when the connection is disconnected.
    /// </summary>
    public event System.Action? Disconnected;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public System.Int64 UpTime => this._cache.Uptime;

    /// <summary>
    /// Gets the last ping time in milliseconds.
    /// </summary>
    public System.Int64 LastPingTime => this._cache.LastPingTime;

    #endregion Properties

    #region Constructor

    static TransportStream() =>
        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
        .SetMaxCapacity<PooledSocketAsyncContext>(1024);

    #endregion Constructor

    #region Public Methods

    /// <summary>
    /// Begins receiving data asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void BeginReceive(System.Threading.CancellationToken cancellationToken = default)
    {
        if (this._disposed)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TransportStream)}] BeginReceive called on disposed");
            return;
        }

        _keepReading = true;

        // Reuse last external token if caller passes default but we had one before
        if (!cancellationToken.CanBeCanceled && _lastExternalToken.CanBeCanceled)
        {
            cancellationToken = _lastExternalToken;
        }

        // Link only if needed; otherwise reuse existing _rxToken
        this.EnsureLinkedToken(cancellationToken);

        try
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TransportStream)}] Starting asynchronous read operation");

            System.Net.Sockets.SocketAsyncEventArgs args = new();
            args.SetBuffer(this._buffer, 0, 2);

            args.Completed += (sender, args) =>
            {
                // Capture first, then dispose
                var se = args.SocketError;
                var bt = args.BytesTransferred;
                args.Dispose();

                // Convert the result to Task to keep the API intact
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new(
                    System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

                if (se == System.Net.Sockets.SocketError.Success)
                {
                    tcs.SetResult(bt);
                }
                else
                {
                    tcs.SetException(new System.Net.Sockets.SocketException((System.Int32)se));
                }

                System.Threading.Tasks.Task<System.Int32> receiveTask = tcs.Task;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await this.OnReceiveCompleted(receiveTask, _rxToken)
                                  .ConfigureAwait(false);
                    }
                    catch (System.OperationCanceledException) { }
                    catch (System.ObjectDisposedException) { }
                    catch (System.Net.Sockets.SocketException ex) when
                        (ex.SocketErrorCode is System.Net.Sockets.SocketError.ConnectionReset or
                         System.Net.Sockets.SocketError.OperationAborted)
                    {
                        this.OnDisconnected();
                    }
                    catch (System.Exception ex)
                    {
                        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                                .Error($"[{nameof(TransportStream)}] BeginReceive error: {ex.Message}");
                    }
                }, _rxToken);
            };

            if (!this._socket.ReceiveAsync(args))
            {
                var se = args.SocketError;
                var bt = args.BytesTransferred;
                args.Dispose();

                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new(
                    System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

                if (se == System.Net.Sockets.SocketError.Success)
                {
                    tcs.SetResult(bt);
                }
                else
                {
                    tcs.SetException(new System.Net.Sockets.SocketException((System.Int32)se));
                }

                System.Threading.Tasks.Task<System.Int32> receiveTask = tcs.Task;
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await this.OnReceiveCompleted(receiveTask, _rxToken)
                                  .ConfigureAwait(false);
                    }
                    catch (System.OperationCanceledException) { }
                    catch (System.ObjectDisposedException) { }
                    catch (System.Net.Sockets.SocketException ex) when
                        (ex.SocketErrorCode is System.Net.Sockets.SocketError.ConnectionReset or
                                               System.Net.Sockets.SocketError.OperationAborted)
                    {
                        this.OnDisconnected();
                    }
                }, _rxToken);
            }
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TransportStream)}] BeginReceive error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends data synchronously using a Span.
    /// </summary>
    /// <param name="data">The data to send as a Span.</param>
    /// <returns>true if the data was sent successfully; otherwise, false.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Boolean Send(System.ReadOnlySpan<System.Byte> data)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > System.UInt16.MaxValue - sizeof(System.UInt16))
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + sizeof(System.UInt16));

        if (data.Length <= PacketConstants.StackAllocLimit)
        {
            try
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TransportStream)}] Sending data (stackalloc)");

                System.Span<System.Byte> bufferS = stackalloc System.Byte[totalLength];
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bufferS, totalLength);

                data.CopyTo(bufferS[sizeof(System.UInt16)..]);
                System.Int32 sent = _socket.Send(bufferS);
                if (sent != bufferS.Length)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Error($"[{nameof(TransportStream)}] Partial send: {sent}/{bufferS.Length}");
                    return false;
                }

                // Note: _cache only supports ReadOnlyMemory<byte>, so convert
                this._cache.PushOutgoing(data.ToArray());
                return true;
            }
            catch (System.Exception ex)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TransportStream)}] Send error (stackalloc): {ex}");
                return false;
            }
        }

        System.Byte[] buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                        .Rent(totalLength);

        try
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TransportStream)}] Sending data (pooled)");

            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                  .AsSpan(buffer), totalLength);
            data.CopyTo(System.MemoryExtensions
                .AsSpan(buffer, sizeof(System.UInt16)));


            _ = _socket.Send(buffer, 0, totalLength, System.Net.Sockets.SocketFlags.None);

            // Note: _cache only supports ReadOnlyMemory<byte>, so convert
            this._cache.PushOutgoing(System.MemoryExtensions
                       .AsMemory(buffer, 2, totalLength - 2)
                       .ToArray());

            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TransportStream)}] Send error (pooled): {ex}");
            return false;
        }
        finally
        {
            InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Return(buffer);
        }
    }

    /// <summary>
    /// Sends a data asynchronously.
    /// </summary>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous send operation. The value of the TResult parameter contains true if the data was sent successfully; otherwise, false.</returns>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        System.ReadOnlyMemory<System.Byte> data,
        System.Threading.CancellationToken cancellationToken)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        if (data.Length > System.UInt16.MaxValue - sizeof(System.UInt16))
        {
            throw new System.ArgumentOutOfRangeException(nameof(data), "Packet too large");
        }

        System.UInt16 totalLength = (System.UInt16)(data.Length + sizeof(System.UInt16));
        System.Byte[] buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().Rent(totalLength);

        try
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(System.MemoryExtensions
                                                  .AsSpan(buffer), totalLength);

            data.Span.CopyTo(System.MemoryExtensions
                     .AsSpan(buffer, sizeof(System.UInt16)));

            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TransportStream)}] Sending data async");

            _ = await this._socket.SendAsync(System.MemoryExtensions
                                  .AsMemory(buffer, 0, totalLength), System.Net.Sockets.SocketFlags.None, cancellationToken)
                                  .ConfigureAwait(false);

            this._cache.PushOutgoing(data);

            return true;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error($"[{nameof(TransportStream)}] SendAsync error: {ex.Message}");
            return false;
        }
        finally
        {
            InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                    .Return(buffer);
        }
    }

    /// <summary>
    /// Gets a copy of incoming packets.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.ReadOnlyMemory<System.Byte> PopIncoming()
    {
        if (this._cache.Incoming.TryPop(out System.ReadOnlyMemory<System.Byte> data))
        {
            return data;
        }

        return System.ReadOnlyMemory<System.Byte>.Empty; // Avoid null
    }

    /// <summary>
    /// Injects raw packet bytes into the incoming cache manually.
    /// </summary>
    /// <param name="data">The raw byte data to inject.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void InjectIncoming(System.Byte[] data)
    {
        if (data.Length == 0 || this._disposed)
        {
            return;
        }

        this._cache.LastPingTime = (System.Int64)Clock.UnixTime().TotalMilliseconds;
        this._cache.PushIncoming(new System.ReadOnlyMemory<System.Byte>(data));

        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TransportStream)}] Injected {data.Length} bytes into incoming cache.");
    }

    /// <summary>
    /// Registers a callback to be invoked when a packet is cached.
    /// </summary>
    /// <param name="handler">The callback method to register.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetPacketCached(System.Action handler) => this._cache.PacketCached += handler;

    /// <summary>
    /// Unregisters a previously registered callback from the packet cached event.
    /// </summary>
    /// <param name="handler">The callback method to remove.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void RemovePacketCached(System.Action handler) => this._cache.PacketCached -= handler;

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Handles the completion of data reception.
    /// </summary>
    /// <param name="task">The task representing the read operation.</param>
    /// <param name="_">The cancellation token.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability", "CA2016:Forward the 'CancellationToken' parameter to methods", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    private async System.Threading.Tasks.Task OnReceiveCompleted(
        System.Threading.Tasks.Task<System.Int32> task,
        System.Threading.CancellationToken _)
    {
        if (task.IsCanceled || this._disposed)
        {
            return;
        }

        try
        {
            System.Int32 totalBytesRead = await task;
            if (totalBytesRead == 0)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TransportStream)}] Clients closed");

                this.OnDisconnected();
                return;
            }

            // Ensure full 2-byte header
            if (totalBytesRead < 2)
            {
                while (totalBytesRead < 2)
                {
                    System.Int32 bytesRead;
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<System.Int32>(
                        System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
                    var saea = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                       .Get<PooledSocketAsyncContext>();
                    try
                    {
                        saea.SetBuffer(this._buffer, totalBytesRead, 2 - totalBytesRead);
                        saea.UserToken = tcs;

                        if (!this._socket.ReceiveAsync(saea))
                        {
                            if (saea.SocketError == System.Net.Sockets.SocketError.Success)
                            {
                                tcs.SetResult(saea.BytesTransferred);
                            }
                            else
                            {
                                tcs.SetException(new System.Net.Sockets.SocketException((System.Int32)saea.SocketError));
                            }
                        }

                        bytesRead = await tcs.Task;
                    }
                    finally
                    {
                        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                .Return(saea);
                    }

                    if (bytesRead == 0)
                    {
                        this.OnDisconnected();
                        return;
                    }

                    totalBytesRead += bytesRead;
                }
            }

            // Read size (includes the 2-byte header)
            System.UInt16 size = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(this._buffer);
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TransportStream)}] Packet size: {size} bytes.");

            if (size < 2)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TransportStream)}] Invalid packet size: {size} (must be >= 2).");
                return;
            }

            if (size > InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().MaxBufferSize)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TransportStream)}] Size {size} exceeds max " +
                                               $"{InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>().MaxBufferSize} bytes.");

                return;
            }

            // If need a bigger buffer
            if (size > _buffer.Length)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TransportStream)}] Renting larger buffer");

                InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                        .Return(_buffer);

                _buffer = InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                                  .Rent(size);

                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(_buffer, size);
            }

            // Read remaining bytes until we reach 'size'
            while (totalBytesRead < size)
            {
                System.Int32 bytesRead;
                System.Threading.Tasks.TaskCompletionSource<System.Int32> tcs = new();
                PooledSocketAsyncContext saea = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                                                        .Get<PooledSocketAsyncContext>();

                try
                {
                    saea.SetBuffer(this._buffer, totalBytesRead, size - totalBytesRead);
                    saea.UserToken = tcs;

                    if (!this._socket.ReceiveAsync(saea))
                    {
                        if (saea.SocketError == System.Net.Sockets.SocketError.Success)
                        {
                            tcs.SetResult(saea.BytesTransferred);
                        }
                        else
                        {
                            tcs.SetException(new System.Net.Sockets.SocketException((System.Int32)saea.SocketError));
                        }
                    }

                    bytesRead = await tcs.Task;
                }
                finally
                {
                    InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>()
                                            .Return(saea);
                }

                if (bytesRead == 0)
                {
                    InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                            .Debug($"[{nameof(TransportStream)}] Clients closed during read");

                    this.OnDisconnected();
                    return;
                }

                totalBytesRead += bytesRead;
            }

            if (totalBytesRead == size)
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Debug($"[{nameof(TransportStream)}] Packet received");

                this._cache.LastPingTime = Clock.UnixMillisecondsNow();

                this._cache.PushIncoming(System.MemoryExtensions
                           .AsMemory(this._buffer, 2, size - 2));
            }
            else
            {
                InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                        .Error($"[{nameof(TransportStream)}] Incomplete packet: read {totalBytesRead}/{size} bytes.");
            }
        }
        catch (System.Net.Sockets.SocketException ex) when
              (ex.SocketErrorCode is System.Net.Sockets.SocketError.ConnectionReset or
                                     System.Net.Sockets.SocketError.OperationAborted)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Debug($"[{nameof(TransportStream)}] connection reset/aborted");

            this.OnDisconnected();
        }
        catch (System.ObjectDisposedException)
        {
            _keepReading = false;
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                    .Error(ex);
        }
        finally
        {
            // Only continue when still valid
            if (_keepReading && !_disposed && !(_rxCts?.IsCancellationRequested ?? false))
            {
                // Preserve external token link
                this.BeginReceive(_lastExternalToken);
            }
        }
    }

    /// <summary>
    /// Disposes the managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">If true, releases managed resources; otherwise, only releases unmanaged resources.</param>
    private void Dispose(System.Boolean disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // stop loop & cancel token first
            _keepReading = false;
            try
            {
                _rxCts?.Cancel();
            }
            catch { }

            try
            {
                this._socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
            }
            catch { /* ignore */ }

            try
            {
                this._socket.Close();
            }
            catch { /* ignore */ }

            _rxShutdownReg?.Dispose();
            _rxCts?.Dispose();
            _rxShutdownReg = null;
            _rxCts = null;

            // now it’s safe to return pooled buffer
            InstanceManager.Instance.GetOrCreateInstance<BufferPoolManager>()
                                    .Return(this._buffer);

            this._cache.Dispose();
            this._socket.Dispose();
        }

        this._disposed = true;
        InstanceManager.Instance.GetExistingInstance<ILogger>()?
                                .Debug($"[{nameof(TransportStream)}] disposed");
    }

    // Ensure linked token once; recreate only when necessary
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void EnsureLinkedToken(System.Threading.CancellationToken externalToken)
    {
        // Recreate if: none, already canceled, or external token changed
        if (_rxCts is null
            || _rxCts.IsCancellationRequested
            || !_lastExternalToken.Equals(externalToken))
        {
            _rxShutdownReg?.Dispose();
            _rxCts?.Dispose();

            _rxCts = externalToken.CanBeCanceled
                ? System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken)
                : System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

            _rxToken = _rxCts.Token;
            _lastExternalToken = externalToken;

            // Wake ReceiveAsync on cancel
            _rxShutdownReg = _rxToken.Register(() =>
            {
                try { _socket.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
                try { _socket.Close(); } catch { }
            });
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OnDisconnected()
    {
        // Stop the receive loop exactly once
        if (_keepReading)
        {
            _keepReading = false;
            try { _rxCts?.Cancel(); } catch { /* ignore */ }
        }

        // Notify subscribers (outside world)
        try
        {
            Disconnected?.Invoke();
        }
        catch (System.Exception ex)
        {
            InstanceManager.Instance.GetExistingInstance<ILogger>()?
                .Error($"[{nameof(TransportStream)}] Disconnected handler error: {ex.Message}");
        }
    }


    #endregion Private Methods

    /// <summary>
    /// Disposes the resources used by the <see cref="TransportStream"/> instance.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override System.String ToString()
        => $"TransportStream (Clients = {this._socket.RemoteEndPoint}, " +
           $"Disposed = {this._disposed}, UpTime = {this.UpTime}ms, LastPing = {this.LastPingTime}ms)" +
           $"IncomingCount = {this._cache.Incoming.Count}, OutgoingCount = {this._cache.Outgoing.Count} }}";
}