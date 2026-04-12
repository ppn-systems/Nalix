using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Network.Connections;
using Xunit;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class ConnectionHubSessionTests
{
    [Fact]
    public async Task CreateSession_CapturesConnectionState()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);
        
        connection.Secret = new byte[] { 1, 2, 3 };
        connection.Algorithm = Common.Security.CipherSuiteType.Chacha20Poly1305;
        connection.Level = Common.Security.PermissionLevel.OWNER;
        connection.Attributes["test"] = "value";

        SessionEntry session = hub.CreateSession(connection);

        session.Should().NotBeNull();
        session.Snapshot.Secret.Should().Equal(connection.Secret);
        session.Snapshot.Algorithm.Should().Be(connection.Algorithm);
        session.Snapshot.Level.Should().Be(connection.Level);
        session.Snapshot.Attributes.Should().ContainKey("test").WhoseValue.Should().Be("value");
        session.ConnectionId.Should().Be(connection.ID.ToUInt56());
    }

    [Fact]
    public async Task TryResumeSession_RestoresState_AndFailsIfOldConnectionActive()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope1 = await ConnectedSocketScope.CreateAsync();
        using Connection connection1 = new(scope1.ServerSocket);
        
        connection1.Secret = new byte[] { 1, 2, 3 };
        connection1.Attributes["test"] = "value";
        hub.RegisterConnection(connection1);

        SessionEntry sessionInfo = hub.CreateSession(connection1);

        // Resume should fail while connection1 is still in Hub
        using ConnectedSocketScope scope2 = await ConnectedSocketScope.CreateAsync();
        using Connection connection2 = new(scope2.ServerSocket);
        
        hub.TryResumeSession(sessionInfo.Snapshot.SessionToken, connection2, out _).Should().BeFalse();

        // Unregister connection1
        hub.UnregisterConnection(connection1);

        // Resume should now succeed
        hub.TryResumeSession(sessionInfo.Snapshot.SessionToken, connection2, out SessionEntry? resumedSession).Should().BeTrue();
        
        resumedSession.Should().NotBeNull();
        connection2.Secret.Should().Equal(connection1.Secret);
        connection2.Attributes["test"].Should().Be("value");
        connection2.Attributes["nalix.handshake.established"].Should().Be(true);
        resumedSession!.ConnectionId.Should().Be(connection2.ID.ToUInt56());
    }

    private sealed class ConnectedSocketScope : IDisposable
    {
        private ConnectedSocketScope(Socket listenerSocket, Socket clientSocket, Socket serverSocket)
        {
            ListenerSocket = listenerSocket;
            ClientSocket = clientSocket;
            ServerSocket = serverSocket;
        }

        public Socket ListenerSocket { get; }
        public Socket ClientSocket { get; }
        public Socket ServerSocket { get; }

        public static async Task<ConnectedSocketScope> CreateAsync()
        {
            Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            int port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            Task<Socket> acceptTask = Task.Run(() => listener.Accept());

            Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(IPAddress.Loopback, port);

            Socket server = await acceptTask;
            return new ConnectedSocketScope(listener, client, server);
        }

        public void Dispose()
        {
            try { ClientSocket.Dispose(); } catch { }
            try { ServerSocket.Dispose(); } catch { }
            try { ListenerSocket.Dispose(); } catch { }
        }
    }
}
