// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using Nalix.Network.Internal.WebSockets;
using Xunit;

namespace Nalix.Network.Tests;

public class WebSocketUpgradeParserTests
{
    [Fact]
    public void Parse_ExtractsForwardedClientIpHeaders()
    {
        byte[] request = Encoding.ASCII.GetBytes(
            "GET /ws/ HTTP/1.1\r\n" +
            "Host: example.com\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Version: 13\r\n" +
            "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
            "CF-Connecting-IP: 203.0.113.10\r\n" +
            "X-Real-IP: 203.0.113.11\r\n" +
            "X-Forwarded-For: 203.0.113.12, 10.0.0.1\r\n" +
            "\r\n");

        WebSocketUpgradeResult result = WebSocketUpgradeParser.Parse(request);

        Assert.True(result.IsValid);
        Assert.Equal("203.0.113.10", Encoding.ASCII.GetString(result.CfConnectingIp));
        Assert.Equal("203.0.113.11", Encoding.ASCII.GetString(result.XRealIp));
        Assert.Equal("203.0.113.12, 10.0.0.1", Encoding.ASCII.GetString(result.XForwardedFor));
    }
}
