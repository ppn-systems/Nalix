// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Transport;
using Nalix.Common.Shared;
using Nalix.Framework.Configuration;
using Nalix.Framework.Injection;
using Nalix.SDK.Configuration;
using Nalix.SDK.Transport.Extensions;
using Nalix.SDK.Transport.Internal;
using Nalix.Shared.Memory.Buffers;
using System.Threading.Tasks;

namespace Nalix.SDK.Transport;

/// <summary>
/// Shared base class for TCP-style client sessions.
/// Contains common socket lifecycle, cleanup, send/receive glue, and event wiring.
/// Derived classes implement receive scheduling and framing construction.
/// </summary>
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public abstract class BaseTcpSession : IClientConnection
{
    #region Fields

    internal readonly System.Threading.Lock _sync = new();

    internal FRAME_SENDER? _sender;
    internal FRAME_READER? _receiver;

    internal System.Net.Sockets.Socket? _socket;
    internal Task? _receiveTask;
    internal System.Threading.CancellationTokenSource? _loopCts;

    internal System.Int32 _disposed = 0;

    /// <inheritdoc/>
    internal static readonly ILogger? Logging;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the transport options used by this client.
    /// </summary>
    public TransportOptions Options;

    ITransportOptions IClientConnection.Options => this.Options;

    #endregion Properties

    #region Events

    /// <inheritdoc/>
    public event System.EventHandler? OnConnected;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception>? OnError;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64>? OnBytesSent;

    /// <inheritdoc/>
    public event System.EventHandler<System.Int64>? OnBytesReceived;

    /// <inheritdoc/>
    public event System.EventHandler<IBufferLease>? OnMessageReceived;

    /// <inheritdoc/>
    public event System.EventHandler<System.Exception>? OnDisconnected;

    /// <inheritdoc/>
    public System.Func<BaseTcpSession, System.ReadOnlyMemory<System.Byte>, System.Threading.Tasks.Task>? OnMessageReceivedAsync;

    #endregion

    #region Construction

    static BaseTcpSession() => Logging = InstanceManager.Instance.GetExistingInstance<ILogger>();

    /// <summary>
    /// Constructs base session and loads TransportOptions from configuration.
    /// Derived classes are responsible for buffer configuration if needed.
    /// </summary>
    protected BaseTcpSession()
    {
        this.Options = ConfigurationManager.Instance.Get<TransportOptions>();
        this.Options.Validate();
    }

    #endregion Construction

    #region Public API - Send/Disconnect/Dispose/IsConnected

    /// <inheritdoc/>
    public abstract Task ConnectAsync(System.String? host = null, System.UInt16? port = null, System.Threading.CancellationToken ct = default);

    /// <inheritdoc/>
    public virtual System.Boolean IsConnected => _socket?.Connected == true && System.Threading.Volatile.Read(ref _disposed) == 0;

    /// <inheritdoc/>
    public virtual Task<System.Boolean> SendAsync(System.ReadOnlyMemory<System.Byte> payload, System.Threading.CancellationToken ct = default)
    {
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(BaseTcpSession));
        var sender = System.Threading.Volatile.Read(ref _sender);
        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(payload, ct);
    }

    /// <inheritdoc/>
    public virtual Task<System.Boolean> SendAsync([System.Diagnostics.CodeAnalysis.NotNull] IPacket packet, System.Threading.CancellationToken ct = default)
    {
        System.ArgumentNullException.ThrowIfNull(packet);
        System.ObjectDisposedException.ThrowIf(System.Threading.Volatile.Read(ref _disposed) == 1, nameof(BaseTcpSession));
        var sender = System.Threading.Volatile.Read(ref _sender);
        return sender is null ? throw new System.InvalidOperationException("Client not connected.") : sender.SendAsync(packet, ct);
    }

    /// <summary>
    /// Kiểm soát gửi với option compress/encrypt.
    /// Compress trước, sau đó encrypt – đúng thứ tự.
    /// </summary>
    /// <param name="packet">Gói tin.</param>
    /// <param name="compress">Nén payload.</param>
    /// <param name="encrypt">Mã hóa payload.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>
    /// <c>true</c> nếu gửi thành công; <c>false</c> nếu lỗi socket.
    /// </returns>
    public async System.Threading.Tasks.Task<System.Boolean> SendAsync(
        IPacket packet,
        System.Boolean compress,
        System.Boolean encrypt,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // --- Encode payload ---
        System.ReadOnlyMemory<System.Byte> payload = packet.Serialize();

        BufferLease lease = BufferLease.CopyFrom(payload.Span);
        try
        {
            if (compress)
            {
                BufferLease? compressed = lease.CompressPayload();
                lease.Dispose();
                if (compressed == null)
                {
                    throw new System.Exception("Compression failed");
                }

                lease = compressed;
            }

            if (encrypt)
            {
                BufferLease? encrypted = lease.EncryptPayload(this);
                lease.Dispose();
                if (encrypted == null)
                {
                    throw new System.Exception("Encryption failed");
                }

                lease = encrypted;
            }

            return await SendAsync(lease.Memory, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lease.Dispose();
        }
    }

    /// <inheritdoc/>
    public virtual Task DisconnectAsync()
    {
        if (System.Threading.Volatile.Read(ref _disposed) == 1)
        {
            return Task.CompletedTask;
        }

        CLEANUP_CONNECTION();
        Logging?.Info($"[SDK.{this.GetType().Name}] Disconnected (requested).");
        try { OnDisconnected?.Invoke(this, null!); } catch { }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        CLEANUP_CONNECTION();

        Logging?.Info($"[SDK.{this.GetType().Name}] Disposed.");
        System.GC.SuppressFinalize(this);
    }

    #endregion

    #region Protected - Helpers (shared)

    /// <inheritdoc/>
    protected void RaiseConnected()
    {
        try { OnConnected?.Invoke(this, System.EventArgs.Empty); } catch { }
    }

    /// <inheritdoc/>
    protected void RaiseDisconnected(System.Exception? ex)
    {
        try { OnDisconnected?.Invoke(this, ex!); } catch { }
    }

    /// <inheritdoc/>
    protected void RaiseError(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
    }

    /// <inheritdoc/>
    protected void RaiseBytesSent(System.Int64 bytes)
    {
        try { OnBytesSent?.Invoke(this, bytes); } catch { }
    }

    /// <inheritdoc/>
    protected void RaiseBytesReceived(System.Int64 bytes)
    {
        try { OnBytesReceived?.Invoke(this, bytes); } catch { }
    }


    /// <summary>
    /// Protected helper for subclasses to set up frame helpers (_sender/_receiver).
    /// Subclasses should instantiate _sender/_receiver and bind desired error/byte-report callbacks.
    /// </summary>
    protected abstract void CreateFrameHelpers();

    /// <summary>
    /// Start/stage receive worker - scheduling differs per subclass (TaskManager vs Task.Run).
    /// </summary>
    protected abstract void StartReceiveWorker(System.Threading.CancellationToken loopToken);

    /// <summary>
    /// Common cleanup logic: cancel background loops, dispose sender, close socket, null out reader.
    /// Subclasses may override to perform additional cleanup (e.g., cancel TaskManager handles).
    /// </summary>
    protected virtual void CLEANUP_CONNECTION()
    {
        lock (_sync)
        {
            if (_loopCts is not null)
            {
                CANCEL_AND_DISPOSE_LOCKED(ref _loopCts);
            }

            try
            {
                var prevSender = System.Threading.Interlocked.Exchange(ref _sender, null);
                prevSender?.Dispose();
            }
            catch { /* swallow */ }

            try { _socket?.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { _socket?.Close(); _socket?.Dispose(); } catch { }
            _socket = null!;
            _receiver = null!;
        }

        // Do not dispose _receiveTask; it will finish naturally once loopToken is cancelled.
        _receiveTask = null!;
    }

    /// <summary>
    /// Cancel and dispose a CancellationTokenSource under lock.
    /// </summary>
    protected static void CANCEL_AND_DISPOSE_LOCKED(ref System.Threading.CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try { cts.Cancel(); } catch { }
        try { cts.Dispose(); } catch { }
        cts = null!;
    }

    /// <summary>
    /// Throw if socket is not connected; used by FRAME helpers.
    /// </summary>
    protected System.Net.Sockets.Socket GET_CONNECTED_SOCKET_OR_THROW()
    {
        var s = _socket;
        return s?.Connected == true
            ? s
            : throw new System.InvalidOperationException("Client not connected.");
    }

    /// <summary>
    /// Report byte counts to subscribers. Subclasses can override to include counters.
    /// </summary>
    protected virtual void REPORT_BYTES_SENT(System.Int32 count)
    {
        try { OnBytesSent?.Invoke(this, count); } catch { }
    }

    /// <summary>
    /// Report byte counts to subscribers. Subclasses can override to include counters.
    /// </summary>
    protected virtual void REPORT_BYTES_RECEIVED(System.Int32 count)
    {
        try { OnBytesReceived?.Invoke(this, count); } catch { }
    }

    /// <summary>
    /// Default receive message delivery for synchronous subscribers.
    /// Derived classes that need async handler support should override.
    /// </summary>
    protected virtual void HANDLE_RECEIVE_MESSAGE(BufferLease lease)
    {
        var handlers = OnMessageReceived?.GetInvocationList();
        var asyncHandler = OnMessageReceivedAsync;

        System.ReadOnlyMemory<System.Byte> asyncData = default;

        if (asyncHandler is not null)
        {
            // copy ONCE only if needed
            asyncData = lease.Span.ToArray();
        }

        try
        {
            if (handlers?.Length > 0)
            {
                foreach (var d in handlers)
                {
                    BufferLease copy = BufferLease.CopyFrom(lease.Span);

                    try
                    {
                        ((System.EventHandler<IBufferLease>)d).Invoke(this, copy);
                    }
                    catch (System.Exception ex)
                    {
                        Logging?.Error($"[SDK.{nameof(TcpSession)}] sync handler faulted: {ex.Message}", ex);
                    }
                    finally
                    {
                        // ✅ ALWAYS dispose here → no leak
                        try { copy.Dispose(); } catch { }
                    }
                }
            }
        }
        finally
        {
            try { lease.Dispose(); } catch { }
        }

        if (asyncHandler is not null)
        {
            _ = RUN_ASYNC_HANDLER(asyncHandler, asyncData);
        }
    }

    /// <summary>
    /// Default send error handler: notify subscribers and tear down connection.
    /// Subclasses can override to implement reconnect semantics.
    /// </summary>
    protected virtual void HANDLE_SEND_ERROR(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        CLEANUP_CONNECTION();
    }

    /// <summary>
    /// Default receive error handler: notify subscribers and tear down connection.
    /// Subclasses can override to implement reconnect semantics.
    /// </summary>
    protected virtual void HANDLE_RECEIVE_ERROR(System.Exception ex)
    {
        try { OnError?.Invoke(this, ex); } catch { }
        CLEANUP_CONNECTION();
    }
    private async Task RUN_ASYNC_HANDLER(
        System.Func<BaseTcpSession, System.ReadOnlyMemory<System.Byte>, Task> handler,
        System.ReadOnlyMemory<System.Byte> data)
    {
        try
        {
            await handler(this, data).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            Logging?.Error($"[SDK.{nameof(BaseTcpSession)}] async handler faulted: {ex.Message}", ex);
        }
    }

    #endregion
}