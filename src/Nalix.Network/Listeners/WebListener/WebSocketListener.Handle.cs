// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Memory;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Internal.Pooling;
using Nalix.Network.Internal.Tcp;
using Nalix.Network.Internal.WebSockets;

#pragma warning disable IDE0079
#pragma warning disable CA2213
#pragma warning disable CA1031
#pragma warning disable CA2000

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
    // HTTP/1.1 101 Switching Protocols response
    private static readonly byte[] s_handshakeResponsePrefix = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "u8.ToArray();
    private static readonly byte[] s_handshakeResponseSuffix = "\r\n\r\n"u8.ToArray();

    [DebuggerStepThrough]
    internal override AcceptResult ProcessAcceptedSocket(Socket socket, PooledAcceptContext context)
    {
        // For non-proxy connections, we intercept here
        if (!socket.Connected || socket.Handle.ToInt64() == -1)
        {
            SafeCloseSocket(socket);
            return new AcceptResult(AcceptConnectionResult.InvalidSocket, null);
        }

        if (socket.RemoteEndPoint is not IPEndPoint ip || !this.Limiter.TryAccept(ip))
        {
            this.Metrics.RECORD_LIMITER_REJECTION();
            SafeCloseSocket(socket);
            return new AcceptResult(AcceptConnectionResult.RejectedByLimiter, null);
        }

        if (!this.InitializeOptions(socket))
        {
            SafeCloseSocket(socket);
            this.Metrics.RECORD_ERROR();
            return new AcceptResult(AcceptConnectionResult.Failed, null);
        }

        // Return context to pool since we don't need it for WS handshake
        ObjectPoolManager.Shared.Return(context);

        this.BeginWebSocketHandshake(socket, ip, null, 0);

        return new AcceptResult(AcceptConnectionResult.Pending, null);
    }

    [DebuggerStepThrough]
    internal override AcceptResult ProcessProxyAcceptedSocket(Socket socket, EndPoint? realEndPoint, int headerBytesConsumed, byte[]? receiveBuffer, int bytesReceived)
    {
        // For proxy connections, we intercept here.
        // The socket is already validated and options are initialized by TcpListenerBase.
        // We just need to start the WS handshake.
        this.BeginWebSocketHandshake(socket, realEndPoint, receiveBuffer, bytesReceived);

        return new AcceptResult(AcceptConnectionResult.Pending, null);
    }

    private void BeginWebSocketHandshake(Socket socket, EndPoint? realEndPoint, byte[]? proxyBuffer, int proxyBytesReceived)
    {
        WebSocketUpgradeContext state = ObjectPoolManager.Shared.Get<WebSocketUpgradeContext>();
        state.Socket = socket;
        state.RealEndPoint = realEndPoint;
        state.HandshakeStartTimeTicks = Stopwatch.GetTimestamp();

        int initialOffset = 0;
        if (proxyBuffer != null && proxyBytesReceived > 0)
        {
            state.Buffer = BufferLease.ByteArrayPool.Rent(Math.Max(_config.MaxUpgradeRequestSize, proxyBytesReceived));
            Buffer.BlockCopy(proxyBuffer, 0, state.Buffer, 0, proxyBytesReceived);
            state.BytesReceived = proxyBytesReceived;
            initialOffset = proxyBytesReceived;
            // Return proxy buffer since we copied what we needed
            BufferLease.ByteArrayPool.Return(proxyBuffer);
        }
        else
        {
            state.Buffer = BufferLease.ByteArrayPool.Rent(_config.MaxUpgradeRequestSize);
            state.BytesReceived = 0;
        }

        SocketAsyncEventArgs args = new()
        {
            UserToken = state
        };
        args.Completed += this.OnWebSocketReadCompleted;
        args.SetBuffer(state.Buffer, initialOffset, state.Buffer.Length - initialOffset);

        lock (_wsUpgradeLock)
        {
            this.EnqueueWsUpgradeContext(state);
        }

        bool pending = false;
        try
        {
            pending = socket.ReceiveAsync(args);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            lock (_wsUpgradeLock)
            {
                this.DetachWsUpgradeContext(state);
            }
            this.Metrics.RECORD_ERROR();
            this.ReleaseWsUpgradeContext(state, args, success: false);
            return;
        }

        if (!pending)
        {
            this.OnWebSocketReadCompleted(socket, args);
        }
    }

    private void OnWebSocketReadCompleted(object? sender, SocketAsyncEventArgs args)
    {
        WebSocketUpgradeContext state = (WebSocketUpgradeContext)args.UserToken!;

        if (args.SocketError != SocketError.Success || args.BytesTransferred == 0)
        {
            lock (_wsUpgradeLock)
            {
                this.DetachWsUpgradeContext(state);
            }
            this.Metrics.RECORD_ERROR();
            this.ReleaseWsUpgradeContext(state, args, success: false);
            return;
        }

        state.BytesReceived += args.BytesTransferred;

        WebSocketUpgradeResult result = WebSocketUpgradeParser.Parse(new ReadOnlySpan<byte>(state.Buffer, 0, state.BytesReceived));

        if (!result.IsValid)
        {
            // If invalid, check if we've exceeded the max request size or if it's incomplete
            if (state.BytesReceived >= _config.MaxUpgradeRequestSize)
            {
                lock (_wsUpgradeLock)
                {
                    this.DetachWsUpgradeContext(state);
                }
                this.Metrics.RECORD_ERROR();
                this.ReleaseWsUpgradeContext(state, args, success: false);
                return;
            }

            // Incomplete, read more
            args.SetBuffer(state.BytesReceived, state.Buffer.Length - state.BytesReceived);
            try
            {
                if (!state.Socket!.ReceiveAsync(args))
                {
                    this.OnWebSocketReadCompleted(sender, args);
                }
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                lock (_wsUpgradeLock)
                {
                    this.DetachWsUpgradeContext(state);
                }
                this.Metrics.RECORD_ERROR();
                this.ReleaseWsUpgradeContext(state, args, success: false);
            }
            return;
        }

        // Handshake parsed successfully!
        lock (_wsUpgradeLock)
        {
            this.DetachWsUpgradeContext(state);
        }

        // Validate path
        string requestPath = System.Text.Encoding.UTF8.GetString(result.Path);
        if (!requestPath.StartsWith(_path, StringComparison.OrdinalIgnoreCase))
        {
            this.Metrics.RECORD_ERROR();
            this.ReleaseWsUpgradeContext(state, args, success: false);
            return;
        }

        // Compute Sec-WebSocket-Accept
        Span<byte> acceptKey = stackalloc byte[28]; // Base64(SHA1) = 28 bytes
        int acceptKeyLen = WebSocketUpgradeParser.ComputeAcceptKey(result.SecWebSocketKey, acceptKey);

        if (acceptKeyLen == 0)
        {
            this.Metrics.RECORD_ERROR();
            this.ReleaseWsUpgradeContext(state, args, success: false);
            return;
        }

        // Send 101 Switching Protocols response
        try
        {
            // Allocate exact size for response
            int responseLength = s_handshakeResponsePrefix.Length + acceptKeyLen + s_handshakeResponseSuffix.Length;
            byte[] responseBuffer = BufferLease.ByteArrayPool.Rent(responseLength);

            Buffer.BlockCopy(s_handshakeResponsePrefix, 0, responseBuffer, 0, s_handshakeResponsePrefix.Length);
            acceptKey.CopyTo(new Span<byte>(responseBuffer, s_handshakeResponsePrefix.Length, acceptKeyLen));
            Buffer.BlockCopy(s_handshakeResponseSuffix, 0, responseBuffer, s_handshakeResponsePrefix.Length + acceptKeyLen, s_handshakeResponseSuffix.Length);

            // Send sync for now since it's small and kernel buffer can take it immediately
            int sent = state.Socket!.Send(responseBuffer, 0, responseLength, SocketFlags.None);
            BufferLease.ByteArrayPool.Return(responseBuffer);

            if (sent != responseLength)
            {
                this.Metrics.RECORD_ERROR();
                this.ReleaseWsUpgradeContext(state, args, success: false);
                return;
            }

            // Create Connection
            IConnection? connection = this.InitializeConnection(state.Socket!, state.RealEndPoint, result.BytesConsumed, state.Buffer, state.BytesReceived);

            if (connection != null)
            {
                // We've successfully upgraded the socket to WebSocket!
                // NOTE: InitializeConnection will attach it to TcpListenerBase's receive pipeline.
                // However, TcpListenerBase creates a `Connection` which reads raw TCP stream.
                // WE NEED TO DECORATE THIS WITH A WEBSOCKET CONNECTION LAYER.
                // We'll wrap the underlying connection in a WebSocketConnection so it decodes WS frames.

                // Let's release the context, keep socket open
                this.ReleaseWsUpgradeContext(state, args, success: true);

                // Dispatch
                this.DISPATCH_CONNECTION(connection);
                return;
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            this.Metrics.RECORD_ERROR();
            if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
            {
                DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace, new DiagnosticLog("NW.WebSocketListenerBase:Handle", "handshake-send-error", ex));
            }
        }

        this.ReleaseWsUpgradeContext(state, args, success: false);
    }

    private void DetachWsUpgradeContext(WebSocketUpgradeContext state)
    {
        if (state.Prev != null)
        {
            state.Prev.Next = state.Next;
        }
        else if (_wsUpgradeHead == state)
        {
            _wsUpgradeHead = state.Next;
        }

        if (state.Next != null)
        {
            state.Next.Prev = state.Prev;
        }
        else if (_wsUpgradeTail == state)
        {
            _wsUpgradeTail = state.Prev;
        }

        state.Next = null;
        state.Prev = null;
    }

    private void EnqueueWsUpgradeContext(WebSocketUpgradeContext state)
    {
        state.Next = null;
        state.Prev = _wsUpgradeTail;

        if (_wsUpgradeTail != null)
        {
            _wsUpgradeTail.Next = state;
        }
        else
        {
            _wsUpgradeHead = state;
        }

        _wsUpgradeTail = state;
    }

    private void ReleaseWsUpgradeContext(WebSocketUpgradeContext state, SocketAsyncEventArgs args, bool success)
    {
        if (state.Buffer is { } buf)
        {
            // Only return the buffer if we aren't passing it down to the connection
            if (!success)
            {
                BufferLease.ByteArrayPool.Return(buf);
            }
            state.Buffer = [];
        }

        args.UserToken = null;
        args.Completed -= this.OnWebSocketReadCompleted;
        args.Dispose();

        if (!success && state.Socket != null)
        {
            SafeCloseSocket(state.Socket);
        }

        state.ResetForPool();
        ObjectPoolManager.Shared.Return(state);
    }

    private void SWEEP_WS_HANDSHAKE_TIMEOUTS()
    {
        long now = Stopwatch.GetTimestamp();
        long timeoutTicks = (long)(_config.HandshakeTimeoutMs / 1000.0 * Stopwatch.Frequency);

        lock (_wsUpgradeLock)
        {
            WebSocketUpgradeContext? current = _wsUpgradeHead;
            while (current != null)
            {
                WebSocketUpgradeContext? next = current.Next;

                if (now - current.HandshakeStartTimeTicks > timeoutTicks)
                {
                    this.DetachWsUpgradeContext(current);

                    if (DiagnosticsEvents.Source.IsEnabled(DiagnosticsEvents.Internal.Trace))
                    {
                        DiagnosticsEvents.Write(DiagnosticsEvents.Internal.Trace,
                            new DiagnosticLog("NW.WebSocketListenerBase:Sweep", $"ws-handshake-timeout remote-endpoint={current.Socket?.RemoteEndPoint}"));
                    }

                    // Force close the socket
                    if (current.Socket != null)
                    {
                        SafeCloseSocket(current.Socket);
                    }
                }

                current = next;
            }
        }
    }

    private void CLEANUP_WS_UPGRADES()
    {
        lock (_wsUpgradeLock)
        {
            WebSocketUpgradeContext? current = _wsUpgradeHead;
            while (current != null)
            {
                WebSocketUpgradeContext? next = current.Next;
                if (current.Socket != null)
                {
                    SafeCloseSocket(current.Socket);
                }
                current = next;
            }
            _wsUpgradeHead = null;
            _wsUpgradeTail = null;
        }
    }
}
