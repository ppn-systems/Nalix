using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Nalix.Abstractions;
using Nalix.Environment.Memory;
using Nalix.Network.Connections;
using Nalix.Network.Listeners.Web;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.Network.Protocols;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Network.Options;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Nalix.Integration.Tests;

public class WebSocketTransportTests : IDisposable
{
    /// <summary>
    /// A real protocol implementation for testing, inheriting from Nalix.Network.Protocols.Protocol
    /// </summary>
    private sealed class IntegrationTestProtocol : Protocol
    {
        public int ProcessedCount;

        public IntegrationTestProtocol()
        {
            this.SetConnectionAcceptance(true);
        }

        public override void ProcessMessage(object? sender, IConnectEventArgs args)
        {
            Interlocked.Increment(ref ProcessedCount);
            // Echo the payload back exactly as received
            if (args.Lease != null && args.Lease.Length > 0)
            {
                args.Connection.TCP.SendAsync(args.Lease.Memory).AsTask().Wait();
            }
        }
    }

    /// <summary>
    /// A concrete implementation of WebSocketListenerBase to expose the server.
    /// </summary>
    private sealed class TestWebSocketListener : WebSocketListenerBase
    {
        public TestWebSocketListener(ushort port, string path, IProtocol protocol, IConnectionHub hub)
            : base(port, path, protocol, hub) { }

        public override void ProcessFrame(object? sender, IConnectEventArgs args)
        {
            this.Protocol.ProcessMessage(sender, args);
        }
    }

    private static ushort GetFreePort()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return (ushort)((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    [Fact]
    public async Task WebSocketSession_ConnectAndEcho_Success()
    {
        // 1. Setup Server using real ConnectionHub and Protocol
        ushort port = GetFreePort();
        
        // Configure Host to 127.0.0.1 so it doesn't require admin privileges on Windows
        ConfigurationManager.Instance.Get<NetworkWebSocketOptions>().Host = "127.0.0.1";
        
        var protocol = new IntegrationTestProtocol();
        var hub = new ConnectionHub(); // Real hub

        using var server = new TestWebSocketListener(port, "/ws/", protocol, hub);
        server.Activate();

        // Wait a little for server to start
        await Task.Delay(1000);

        // 2. Setup Client
        var options = new TransportOptions
        {
            Address = "127.0.0.1",
            Port = port,
            EncryptionEnabled = false,
            CompressionEnabled = false
        };

        using var client = new WebSocketSession(options);
        
        var messageReceivedTcs = new TaskCompletionSource<string>();

        client.OnMessageReceived += (s, lease) =>
        {
            if (lease.Length > 10)
            {
                string msg = System.Text.Encoding.UTF8.GetString(lease.Span[10..]);
                messageReceivedTcs.TrySetResult(msg);
            }
        };

        // 3. Connect
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        // 4. Send with a valid 10-byte packet header prefix
        string testMessage = "Hello WebSocket Nalix!";
        byte[] rawText = System.Text.Encoding.UTF8.GetBytes(testMessage);
        
        byte[] payload = new byte[10 + rawText.Length];
        // OpCode = 0x1234
        payload[4] = 0x34;
        payload[5] = 0x12;
        rawText.CopyTo(payload.AsSpan(10));

        await client.SendAsync(payload);

        // 5. Receive
        var completedTask = await Task.WhenAny(messageReceivedTcs.Task, Task.Delay(5000));
        Assert.Equal(messageReceivedTcs.Task, completedTask); // Should not timeout

        string received = await messageReceivedTcs.Task;
        Assert.Equal(testMessage, received);
        Assert.Equal(1, protocol.ProcessedCount);

        // 6. Cleanup
        await client.DisconnectAsync();
        server.Deactivate();
    }

    [Fact]
    public async Task WebSocketHosting_ConnectAndEcho_Success()
    {
        // 1. Setup Server using NetworkApplicationBuilder
        ushort port = GetFreePort();
        
        // Configure Host to 127.0.0.1
        ConfigurationManager.Instance.Get<NetworkWebSocketOptions>().Host = "127.0.0.1";

        var builder = Nalix.Hosting.NetworkApplication.CreateBuilder();
        
        // Bind WebSocket using the fluent API
        builder.BindWebSocket<IntegrationTestProtocol>()
               .OnPort(port)
               .WithPath("/ws/")
               .WithFactory(dispatch => new IntegrationTestProtocol());

        using var app = builder.Build();
        await app.ActivateAsync();

        // Wait a little for server to start
        await Task.Delay(1000);

        try
        {
            // 2. Setup Client
            var options = new TransportOptions
            {
                Address = "127.0.0.1",
                Port = port,
                EncryptionEnabled = false,
                CompressionEnabled = false
            };

            using var client = new WebSocketSession(options);
            
            var messageReceivedTcs = new TaskCompletionSource<string>();

            client.OnMessageReceived += (s, lease) =>
            {
                if (lease.Length > 10)
                {
                    string msg = System.Text.Encoding.UTF8.GetString(lease.Span[10..]);
                    messageReceivedTcs.TrySetResult(msg);
                }
            };

            // 3. Connect
            await client.ConnectAsync();
            Assert.True(client.IsConnected);

            // 4. Send with a valid 10-byte packet header prefix
            string testMessage = "Hello WebSocket Hosting!";
            byte[] rawText = System.Text.Encoding.UTF8.GetBytes(testMessage);
            
            byte[] payload = new byte[10 + rawText.Length];
            // OpCode = 0x1234
            payload[4] = 0x34;
            payload[5] = 0x12;
            rawText.CopyTo(payload.AsSpan(10));

            await client.SendAsync(payload);

            // 5. Receive
            var completedTask = await Task.WhenAny(messageReceivedTcs.Task, Task.Delay(5000));
            Assert.Equal(messageReceivedTcs.Task, completedTask); // Should not timeout

            string received = await messageReceivedTcs.Task;
            Assert.Equal(testMessage, received);

            // 6. Cleanup
            await client.DisconnectAsync();
        }
        finally
        {
            await app.DeactivateAsync();
        }
    }

    public void Dispose()
    {
        Nalix.Framework.Injection.InstanceManager.Instance.Clear(dispose: false);
    }
}
