// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Nalix.Network.Internal.WebSockets;

namespace Nalix.Network.Listeners.Web;

public abstract partial class WebSocketListenerBase
{
    /// <summary>
    /// Resolves the logical client endpoint from trusted HTTP forwarded headers.
    /// </summary>
    private bool TryResolveForwardedEndpoint(Socket socket, WebSocketUpgradeResult result, out IPEndPoint? endpoint, out bool reject)
    {
        endpoint = null;
        reject = false;

        if (!_forwardedConfig.Enabled || !TryGetForwardedAddress(result, out IPAddress forwardedAddress))
        {
            return false;
        }

        // Forwarded IP headers are client-controlled unless the physical peer is a trusted proxy.
        if (socket.RemoteEndPoint is not IPEndPoint physicalIp || !_limiter.IsTrustedProxy(physicalIp))
        {
            reject = _forwardedConfig.RequireTrustedProxy;
            return true;
        }

        endpoint = new IPEndPoint(forwardedAddress, physicalIp.Port);
        return true;
    }

    private static bool TryGetForwardedAddress(WebSocketUpgradeResult result, out IPAddress address)
    {
        // Header precedence follows common reverse-proxy deployments: Cloudflare, direct proxy, then chain.
        if (TryParseHeaderAddress(result.CfConnectingIp, out address))
        {
            return true;
        }

        if (TryParseHeaderAddress(result.XRealIp, out address))
        {
            return true;
        }

        return TryParseHeaderAddress(GetFirstForwardedFor(result.XForwardedFor), out address);
    }

    private static ReadOnlySpan<byte> GetFirstForwardedFor(ReadOnlySpan<byte> value)
    {
        int comma = value.IndexOf((byte)',');
        return comma < 0 ? value : value[..comma];
    }

    private static bool TryParseHeaderAddress(ReadOnlySpan<byte> value, out IPAddress address)
    {
        address = IPAddress.None;
        value = TrimAsciiSpace(value);
        return !value.IsEmpty && IPAddress.TryParse(Encoding.ASCII.GetString(value), out address!);
    }
}
