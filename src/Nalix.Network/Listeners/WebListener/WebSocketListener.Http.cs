// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Nalix.Network.Internal.WebSockets;

#pragma warning disable IDE0079
#pragma warning disable CA1031

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{

    private static readonly byte[] s_healthzResponseBytes =
        "HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: text/plain\r\nContent-Length: 7\r\nConnection: close\r\n\r\nHealthy"u8.ToArray();

    private static readonly byte[] s_corsPreflightResponseBytes =
        "HTTP/1.1 204 No Content\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, OPTIONS\r\nAccess-Control-Allow-Headers: *\r\nConnection: close\r\n\r\n"u8.ToArray();

    private void SEND_CORS_PREFLIGHT_RESPONSE(WebSocketUpgradeContext state, SocketAsyncEventArgs args)
    {
        try
        {
            if (state.Socket != null && state.Socket.Connected)
            {
                _ = state.Socket.Send(s_corsPreflightResponseBytes, SocketFlags.None);
                try
                {
                    state.Socket.Shutdown(SocketShutdown.Send);
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            this.ReleaseWsUpgradeContext(state, args, success: true);
        }
    }

    private void SEND_HEALTH_CHECK_RESPONSE(WebSocketUpgradeContext state, SocketAsyncEventArgs args)
    {
        try
        {
            if (state.Socket != null && state.Socket.Connected)
            {
                _ = state.Socket.Send(s_healthzResponseBytes, SocketFlags.None);
                try
                {
                    state.Socket.Shutdown(SocketShutdown.Send);
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            this.ReleaseWsUpgradeContext(state, args, success: true);
        }
    }

    private void SEND_VERSION_RESPONSE(WebSocketUpgradeContext state, SocketAsyncEventArgs args)
    {
        try
        {
            if (state.Socket != null && state.Socket.Connected)
            {
                string ver = _config.ServerVersion ?? "1.0.0";
                string body = $"{{\"name\":\"Nalix\",\"version\":\"{ver}\",\"status\":\"running\"}}";
                byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                string header = $"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: application/json\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                byte[] fullResponse = new byte[headerBytes.Length + bodyBytes.Length];
                headerBytes.CopyTo(fullResponse, 0);
                bodyBytes.CopyTo(fullResponse, headerBytes.Length);

                _ = state.Socket.Send(fullResponse, SocketFlags.None);
                try
                {
                    state.Socket.Shutdown(SocketShutdown.Send);
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            this.ReleaseWsUpgradeContext(state, args, success: true);
        }
    }

    private void SEND_METRICS_RESPONSE(WebSocketUpgradeContext state, SocketAsyncEventArgs args)
    {
        try
        {
            if (state.Socket != null && state.Socket.Connected)
            {
                int activeConns = _hub.Count;

                StringBuilder sb = new();
                _ = sb.AppendLine("# HELP nalix_active_connections Number of active connections");
                _ = sb.AppendLine("# TYPE nalix_active_connections gauge");
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"nalix_active_connections {activeConns}");

                byte[] bodyBytes = Encoding.UTF8.GetBytes(sb.ToString());
                string header = $"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: text/plain; version=0.0.4\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                byte[] fullResponse = new byte[headerBytes.Length + bodyBytes.Length];
                headerBytes.CopyTo(fullResponse, 0);
                bodyBytes.CopyTo(fullResponse, headerBytes.Length);

                _ = state.Socket.Send(fullResponse, SocketFlags.None);
                try
                {
                    state.Socket.Shutdown(SocketShutdown.Send);
                }
                catch { }
            }
        }
        catch { }
        finally
        {
            this.ReleaseWsUpgradeContext(state, args, success: true);
        }
    }
}
