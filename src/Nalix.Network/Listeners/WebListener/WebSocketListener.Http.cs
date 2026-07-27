// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Text;
using System.Net.Sockets;
using System.Text;
using Nalix.Network.Internal.WebSockets;

#pragma warning disable IDE0079
#pragma warning disable CA1031

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
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

    private void SEND_STATIC_RESPONSE(WebSocketUpgradeContext state, SocketAsyncEventArgs args, ReadOnlySpan<byte> response)
    {
        try
        {
            _ = state.Socket?.Send(response, SocketFlags.None);
        }
        catch { }

        try
        {
            state.Socket?.Shutdown(SocketShutdown.Send);
        }
        catch { }

        this.ReleaseWsUpgradeContext(state, args, success: true);
    }

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

        try
        {
            state.Socket?.Shutdown(SocketShutdown.Send);
        }
        catch { }

        this.ReleaseWsUpgradeContext(state, args, success: true);
    }
}
