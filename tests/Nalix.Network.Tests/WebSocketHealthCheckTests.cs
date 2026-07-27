// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Environment.Configuration;
using Nalix.Framework.Injection;
using Nalix.Hosting;
using Nalix.Network.Connections;
using Nalix.Network.Options;
using Nalix.Network.Protocols;
using Xunit;

namespace Nalix.Network.Tests;

[Collection("NetworkConfigTests")]
public sealed class WebSocketHealthCheckTests : IDisposable
{
    private readonly string _certificatePath = Path.Combine(Path.GetTempPath(), $"nalix-ws-health-{Guid.NewGuid():N}.private");

    public WebSocketHealthCheckTests()
    {
        File.WriteAllText(_certificatePath, "0000000000000000000000000000000000000000000000000000000000000001");
    }

    private static ushort GetFreePort()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return (ushort)((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class HealthTestProtocol : Protocol
    {
        private sealed class HealthFrameProcessor : IFrameProcessor
        {
            public void ProcessFrame(object? sender, IConnectionEventArgs args) { }
        }

        private sealed class StubOpCodeExtractor : IOpCodeExtractor
        {
            public ushort Extract(ReadOnlySpan<byte> payload) => 0;
        }

        public override IFrameProcessor FrameProcessor { get; } = new HealthFrameProcessor();
        public override IOpCodeExtractor OpCodeExtractor { get; } = new StubOpCodeExtractor();

        public HealthTestProtocol()
        {
            this.SetConnectionAcceptance(true);
        }

        public override void ProcessMessage(object? sender, IConnectionEventArgs args) { }
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/health")]
    public async Task WebSocketListener_GetHealthzEndpoint_Returns200OKHealthy(string path)
    {
        ushort port = GetFreePort();
        ConfigurationManager.Instance.Get<NetworkWebSocketOptions>().Host = "127.0.0.1";

        ConnectionHub hub = new();
        var builder = NetworkApplication.CreateBuilder();
        builder.UseSecureConnections(_certificatePath);
        builder.UseConnectionHub(hub);
        builder.MapWebSocket<HealthTestProtocol>()
               .OnPort(port)
               .WithPath("/ws/")
               .WithFactory(_ => new HealthTestProtocol());

        using var app = builder.Build();
        await app.ActivateAsync();
        await Task.Delay(500);

        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(IPAddress.Loopback, port);

            string requestLine = $"GET {path} HTTP/1.1\r\nHost: 127.0.0.1\r\nUser-Agent: kube-probe/1.28\r\n\r\n";
            await socket.SendAsync(Encoding.ASCII.GetBytes(requestLine), SocketFlags.None);

            byte[] buffer = new byte[512];
            int read = await socket.ReceiveAsync(buffer, SocketFlags.None);
            string response = Encoding.ASCII.GetString(buffer, 0, read);

            Assert.Contains("HTTP/1.1 200 OK", response);
            Assert.Contains("Healthy", response);
            Assert.Equal(0, hub.Count); // Health probes do not register as active WebSocket connections
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task WebSocketListener_GetVersionEndpoint_ReturnsCustomConfiguredVersion()
    {
        ushort port = GetFreePort();
        var wsOpt = ConfigurationManager.Instance.Get<NetworkWebSocketOptions>();
        wsOpt.Host = "127.0.0.1";
        wsOpt.ServerVersion = "2.5.0-release";

        ConnectionHub hub = new();
        var builder = NetworkApplication.CreateBuilder();
        builder.UseSecureConnections(_certificatePath);
        builder.UseConnectionHub(hub);
        builder.MapWebSocket<HealthTestProtocol>()
               .OnPort(port)
               .WithPath("/ws/")
               .WithFactory(_ => new HealthTestProtocol());

        using var app = builder.Build();
        await app.ActivateAsync();
        await Task.Delay(500);

        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(IPAddress.Loopback, port);

            string requestLine = "GET /version HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n";
            await socket.SendAsync(Encoding.ASCII.GetBytes(requestLine), SocketFlags.None);

            byte[] buffer = new byte[512];
            int read = await socket.ReceiveAsync(buffer, SocketFlags.None);
            string response = Encoding.ASCII.GetString(buffer, 0, read);

            Assert.Contains("HTTP/1.1 200 OK", response);
            Assert.Contains("2.5.0-release", response);
            Assert.Contains("application/json", response);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    [Fact]
    public async Task WebSocketListener_GetMetricsEndpoint_ReturnsPrometheusFormattedMetrics()
    {
        ushort port = GetFreePort();
        ConfigurationManager.Instance.Get<NetworkWebSocketOptions>().Host = "127.0.0.1";

        ConnectionHub hub = new();
        var builder = NetworkApplication.CreateBuilder();
        builder.UseSecureConnections(_certificatePath);
        builder.UseConnectionHub(hub);
        builder.MapWebSocket<HealthTestProtocol>()
               .OnPort(port)
               .WithPath("/ws/")
               .WithFactory(_ => new HealthTestProtocol());

        using var app = builder.Build();
        await app.ActivateAsync();
        await Task.Delay(500);

        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(IPAddress.Loopback, port);

            string requestLine = "GET /metrics HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n";
            await socket.SendAsync(Encoding.ASCII.GetBytes(requestLine), SocketFlags.None);

            byte[] buffer = new byte[1024];
            int read = await socket.ReceiveAsync(buffer, SocketFlags.None);
            string response = Encoding.ASCII.GetString(buffer, 0, read);

            Assert.Contains("HTTP/1.1 200 OK", response);
            Assert.Contains("nalix_active_connections", response);
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_certificatePath)) File.Delete(_certificatePath);
        }
        catch { }
        InstanceManager.Instance.Clear(dispose: false);
    }
}
