// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Network.Internal.WebSockets;

#pragma warning disable IDE0079
#pragma warning disable CA1031

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
    /// <summary>
    /// DevOps endpoints (/healthz, /version, /metrics, ...) close the socket directly
    /// without ever creating a connection object, so the ConnectionClosed-based limiter
    /// release never fires. Release the accept-time slot here or it leaks permanently
    /// per request until the subnet/IP is banned forever.
    /// <para>
    /// Must release the same key that was used at accept time. Behind a PROXY-protocol
    /// front end (e.g. cloudflared), that key is the real client endpoint captured in
    /// <see cref="WebSocketUpgradeContext.RealEndPoint"/> -- NOT the physical socket's
    /// RemoteEndPoint, which is the proxy's own local address. Releasing the wrong key
    /// leaves the real client's slot permanently leaked.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RELEASE_LIMITER_SLOT(WebSocketUpgradeContext state)
    {
        try
        {
            if (state.RealEndPoint is IPEndPoint realIp)
            {
                _limiter.Release(realIp);
            }
            else if (state.Socket?.RemoteEndPoint is IPEndPoint ip)
            {
                _limiter.Release(ip);
            }
        }
        catch { }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RELEASE_PHYSICAL_SLOT(Socket socket)
    {
        if (socket.RemoteEndPoint is IPEndPoint physicalIp)
        {
            _limiter.Release(physicalIp);
        }
    }

    private byte[] BUILD_VERSION_RESPONSE_BYTES()
    {
        string ver = _wsconfig.ServerVersion ?? "1.0.0";
        string body = $"{{\"name\":\"Nalix\",\"version\":\"{ver}\",\"status\":\"running\"}}";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string header = $"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: application/json\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);

        byte[] full = new byte[headerBytes.Length + bodyBytes.Length];
        headerBytes.CopyTo(full, 0);
        bodyBytes.CopyTo(full, headerBytes.Length);
        return full;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SEND_STATIC_RESPONSE(WebSocketUpgradeContext state, SocketAsyncEventArgs args, ReadOnlySpan<byte> response)
    {
        try
        {
            _ = state.Socket?.Send(response, SocketFlags.None);
        }
        catch { }

        this.RELEASE_LIMITER_SLOT(state);

        try
        {
            state.Socket?.Shutdown(SocketShutdown.Send);
        }
        catch { }

        if (state.Socket != null)
        {
            SafeCloseSocket(state.Socket);
        }

        this.ReleaseWsUpgradeContext(state, args, success: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SEND_METRICS_RESPONSE(WebSocketUpgradeContext state, SocketAsyncEventArgs args)
    {
        try
        {
            if (state.Socket != null)
            {
                int activeConns = _hub.Count;

                Span<byte> bodyBuffer = stackalloc byte[256];
                ReadOnlySpan<byte> prefix = "# HELP nalix_active_connections Number of active connections\n# TYPE nalix_active_connections gauge\nnalix_active_connections "u8;
                ReadOnlySpan<byte> suffix = "\n"u8;

                prefix.CopyTo(bodyBuffer);
                int bodyWritten = prefix.Length;

                if (Utf8Formatter.TryFormat(activeConns, bodyBuffer[bodyWritten..], out int formattedBytes))
                {
                    bodyWritten += formattedBytes;
                }
                suffix.CopyTo(bodyBuffer[bodyWritten..]);
                bodyWritten += suffix.Length;

                ReadOnlySpan<byte> bodySpan = bodyBuffer[..bodyWritten];

                Span<byte> headerBuffer = stackalloc byte[256];
                ReadOnlySpan<byte> headerPrefix = "HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: text/plain; version=0.0.4\r\nContent-Length: "u8;
                headerPrefix.CopyTo(headerBuffer);
                int headerWritten = headerPrefix.Length;

                if (Utf8Formatter.TryFormat(bodySpan.Length, headerBuffer[headerWritten..], out int clWritten))
                {
                    headerWritten += clWritten;
                }
                "\r\nConnection: close\r\n\r\n"u8.CopyTo(headerBuffer[headerWritten..]);
                headerWritten += 23;

                Span<byte> fullBuffer = stackalloc byte[512];
                headerBuffer[..headerWritten].CopyTo(fullBuffer);
                bodySpan.CopyTo(fullBuffer[headerWritten..]);
                int totalLen = headerWritten + bodySpan.Length;

                _ = state.Socket.Send(fullBuffer[..totalLen], SocketFlags.None);
            }
        }
        catch { }

        this.RELEASE_LIMITER_SLOT(state);

        try
        {
            state.Socket?.Shutdown(SocketShutdown.Send);
        }
        catch { }

        if (state.Socket != null)
        {
            SafeCloseSocket(state.Socket);
        }

        this.ReleaseWsUpgradeContext(state, args, success: true);
    }
}
