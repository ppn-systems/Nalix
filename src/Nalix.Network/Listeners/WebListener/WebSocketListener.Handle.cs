// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Nalix.Abstractions.Diagnostics;
using Nalix.Abstractions.Exceptions;
using Nalix.Environment.Memory;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;
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
    private static readonly byte[] s_handshakeResponsePrefix = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "u8.ToArray();
    private static readonly byte[] s_handshakeSubProtocolPrefix = "\r\nSec-WebSocket-Protocol: "u8.ToArray();
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

        this.BeginWebSocketHandshake(socket, null, null, 0);

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

#pragma warning disable CA2000
        if (!_wsReceiveArgsPool.TryPop(out SocketAsyncEventArgs? args))
        {
            args = new SocketAsyncEventArgs();
            args.Completed += this.OnWebSocketReadCompleted;
        }
#pragma warning restore CA2000

        args.UserToken = state;
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
        string requestPath = Encoding.UTF8.GetString(result.Path);
        if (!requestPath.StartsWith(_path, StringComparison.OrdinalIgnoreCase))
        {
            this.Metrics.RECORD_ERROR();
            this.ReleaseWsUpgradeContext(state, args, success: false);
            return;
        }

        string? origin = result.Origin.IsEmpty ? null : Encoding.UTF8.GetString(result.Origin);
        if (!_config.IsOriginAllowed(origin))
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
            string configuredSubProtocol = _config.SubProtocol;
            bool hasConfiguredSubProtocol = !string.IsNullOrWhiteSpace(configuredSubProtocol);
            bool hasSubProtocol = hasConfiguredSubProtocol && ContainsRequestedSubProtocol(result.SubProtocol, configuredSubProtocol);

            if (hasConfiguredSubProtocol && !result.SubProtocol.IsEmpty && !hasSubProtocol)
            {
                this.Metrics.RECORD_ERROR();
                this.ReleaseWsUpgradeContext(state, args, success: false);
                return;
            }

            byte[]? subProtocolBytes = hasSubProtocol ? Encoding.UTF8.GetBytes(configuredSubProtocol) : null;
            int responseLength = s_handshakeResponsePrefix.Length + acceptKeyLen;

            if (hasSubProtocol)
            {
                responseLength += s_handshakeSubProtocolPrefix.Length + subProtocolBytes!.Length;
            }
            responseLength += s_handshakeResponseSuffix.Length;

            byte[] responseBuffer = BufferLease.ByteArrayPool.Rent(responseLength);
            int offset = 0;

            Buffer.BlockCopy(s_handshakeResponsePrefix, 0, responseBuffer, offset, s_handshakeResponsePrefix.Length);
            offset += s_handshakeResponsePrefix.Length;

            acceptKey.CopyTo(new Span<byte>(responseBuffer, offset, acceptKeyLen));
            offset += acceptKeyLen;

            if (hasSubProtocol)
            {
                byte[] selectedSubProtocol = subProtocolBytes!;
                Buffer.BlockCopy(s_handshakeSubProtocolPrefix, 0, responseBuffer, offset, s_handshakeSubProtocolPrefix.Length);
                offset += s_handshakeSubProtocolPrefix.Length;

                Buffer.BlockCopy(selectedSubProtocol, 0, responseBuffer, offset, selectedSubProtocol.Length);
                offset += selectedSubProtocol.Length;
            }

            Buffer.BlockCopy(s_handshakeResponseSuffix, 0, responseBuffer, offset, s_handshakeResponseSuffix.Length);

            // Send sync for now since it's small and kernel buffer can take it immediately
            int sent = state.Socket!.Send(responseBuffer, 0, responseLength, SocketFlags.None);
            BufferLease.ByteArrayPool.Return(responseBuffer);

            if (sent != responseLength)
            {
                this.Metrics.RECORD_ERROR();
                this.ReleaseWsUpgradeContext(state, args, success: false);
                return;
            }

            // Capture socket and endpoint before releasing state back to the pool
            Socket socket = state.Socket!;
            EndPoint realEndPoint = state.RealEndPoint ?? socket.RemoteEndPoint!;

            if (state.RealEndPoint is null && this.TryResolveForwardedEndpoint(socket, result, out IPEndPoint? forwardedEndpoint, out bool rejectForwarded))
            {
                if (rejectForwarded)
                {
                    if (socket.RemoteEndPoint is IPEndPoint physicalIp)
                    {
                        _limiter.Release(physicalIp);
                    }

                    this.Metrics.RECORD_LIMITER_REJECTION();
                    this.ReleaseWsUpgradeContext(state, args, success: false);
                    return;
                }

                if (forwardedEndpoint is not null && socket.RemoteEndPoint is IPEndPoint physicalEndPoint)
                {
                    if (!_limiter.TryAccept(forwardedEndpoint))
                    {
                        _limiter.Release(physicalEndPoint);
                        this.Metrics.RECORD_LIMITER_REJECTION();
                        this.ReleaseWsUpgradeContext(state, args, success: false);
                        return;
                    }

                    _limiter.Release(physicalEndPoint);
                    realEndPoint = forwardedEndpoint;
                }
            }

            // Create a NetworkStream and wrap it in a WebSocket
            NetworkStream stream = new(socket, ownsSocket: false);

            WebSocket webSocket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions
            {
                IsServer = true,
                SubProtocol = hasSubProtocol ? configuredSubProtocol : null,
                KeepAliveInterval = TimeSpan.FromSeconds(_config.KeepAliveIntervalSeconds)
            });

            WebSocketConnection? connection = new(webSocket, _protocol.OpCodeExtractor, realEndPoint);
            try
            {
                connection.ConnectionClosed += this.HandleConnectionClose;
                connection.ConnectionClosed += _limiter.OnConnectionClosed;
                connection.MessageProcessed += _protocol.PostProcessMessage;
                connection.MessageProcessing += _protocol.FrameProcessor.ProcessFrame;

                if (base._config.EnableTimeout)
                {
                    _timing.Register(connection);
                }

                // Dispatch
                this.DISPATCH_CONNECTION(connection);
                connection = null;

                // Successfully dispatched, release context (keeps socket open)
                this.ReleaseWsUpgradeContext(state, args, success: true);
            }
            finally
            {
                connection?.Dispose();
            }

            return;
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

    private static bool ContainsRequestedSubProtocol(ReadOnlySpan<byte> requested, string configured)
    {
        ReadOnlySpan<char> expected = configured.AsSpan().Trim();
        int start = 0;

        while (start < requested.Length)
        {
            int comma = requested[start..].IndexOf((byte)',');
            int end = comma < 0 ? requested.Length : start + comma;
            ReadOnlySpan<byte> token = TrimAsciiSpace(requested[start..end]);

            if (MatchesAscii(token, expected))
            {
                return true;
            }

            if (comma < 0)
            {
                break;
            }

            start = end + 1;
        }

        return false;
    }

    private static ReadOnlySpan<byte> TrimAsciiSpace(ReadOnlySpan<byte> value)
    {
        int start = 0;
        while (start < value.Length && value[start] <= 32)
        {
            start++;
        }

        int end = value.Length - 1;
        while (end >= start && value[end] <= 32)
        {
            end--;
        }

        return value[start..(end + 1)];
    }

    private static bool MatchesAscii(ReadOnlySpan<byte> value, ReadOnlySpan<char> expected)
    {
        if (value.Length != expected.Length)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    private void DetachWsUpgradeContext(WebSocketUpgradeContext state)
    {
        if (state.RemovedFromList)
        {
            return;
        }

        state.RemovedFromList = true;

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
            BufferLease.ByteArrayPool.Return(buf);
            state.Buffer = [];
        }

        args.UserToken = null;
        _wsReceiveArgsPool.Push(args);

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

                if (now - current.HandshakeStartTimeTicks <= timeoutTicks)
                {
                    break;
                }

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
