using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Framework.Memory.Objects;
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

        connection.Secret = new Abstractions.Primitives.Bytes32(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        connection.Algorithm = Abstractions.Security.CipherSuiteType.Chacha20Poly1305;
        connection.Level = Abstractions.Security.PermissionLevel.OWNER;
        connection.Attributes["test"] = "value";

        SessionEntry session = hub.SessionStore.CreateSession(connection);

        _ = session.Should().NotBeNull();
        _ = session.Snapshot.Secret.Should().Be(connection.Secret);
        _ = session.Snapshot.Algorithm.Should().Be(connection.Algorithm);
        _ = session.Snapshot.Level.Should().Be(connection.Level);
        _ = session.Snapshot.Attributes.Should().ContainKey("test").WhoseValue.Should().Be("value");
        _ = session.ConnectionId.Should().Be(connection.ID.ToUInt64());
    }

    [Fact]
    public async Task TryResumeSession_RestoresState_AndFailsIfOldConnectionActive()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope1 = await ConnectedSocketScope.CreateAsync();
        using Connection connection1 = new(scope1.ServerSocket);

        connection1.Secret = new Abstractions.Primitives.Bytes32(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        connection1.Attributes["test"] = "value";
        hub.RegisterConnection(connection1);

        SessionEntry sessionInfo = hub.SessionStore.CreateSession(connection1);

        await hub.SessionStore.StoreAsync(sessionInfo);

        // Resume should fail while connection1 is still in Hub
        using ConnectedSocketScope scope2 = await ConnectedSocketScope.CreateAsync();
        using Connection connection2 = new(scope2.ServerSocket);

        // Simulator of TryResumeSession logic:
        SessionEntry? s = await hub.SessionStore.RetrieveAsync(sessionInfo.Snapshot.SessionToken);
        _ = s.Should().NotBeNull();

        // Security validation (hub.GetConnection)
        _ = hub.GetConnection(s.ConnectionId).Should().NotBeNull();

        // Unregister connection1 (this will dispose it and wipe its secret)
        var expectedSecret = connection1.Secret;
        hub.UnregisterConnection(connection1);

        // Resume should now succeed
        SessionEntry? ss = await hub.SessionStore.RetrieveAsync(sessionInfo.Snapshot.SessionToken);
        _ = ss.Should().NotBeNull();
        _ = hub.GetConnection(ss.ConnectionId).Should().BeNull();

        // Manual state restoration
        ApplySession(connection2, ss);

        _ = connection2.Secret.Should().Be(expectedSecret);
        _ = connection2.Attributes["test"].Should().Be("value");
        _ = connection2.Attributes["nalix.handshake.established"].Should().Be(true);
    }

    [Fact]
    public async Task TryResumeSession_PersistsState_AfterModifications()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope1 = await ConnectedSocketScope.CreateAsync();
        using Connection connection1 = new(scope1.ServerSocket);

        hub.RegisterConnection(connection1);

        // 1. Create session (Initial state)
        SessionEntry sessionInfo = hub.SessionStore.CreateSession(connection1);
        await hub.SessionStore.StoreAsync(sessionInfo);

        // 2. Modify state AFTER session creation
        connection1.Attributes["user_id"] = 12345;
        connection1.Level = Abstractions.Security.PermissionLevel.SYSTEM_ADMINISTRATOR;

        // 3. Update session before unregistering (required now because Unregister doesn't auto-update)
        SyncSession(connection1, sessionInfo);
        await hub.SessionStore.StoreAsync(sessionInfo);

        // 4. Unregister connection
        hub.UnregisterConnection(connection1);

        // 5. Resume session with new connection
        using ConnectedSocketScope scope2 = await ConnectedSocketScope.CreateAsync();
        using Connection connection2 = new(scope2.ServerSocket);
        SessionEntry? s = await hub.SessionStore.RetrieveAsync(sessionInfo.Snapshot.SessionToken);
        _ = s.Should().NotBeNull();

        ApplySession(connection2, s);

        // 6. Verify that modifications were persisted
        _ = connection2.Attributes.Should().ContainKey("user_id").WhoseValue.Should().Be(12345);
        _ = connection2.Level.Should().Be(Abstractions.Security.PermissionLevel.SYSTEM_ADMINISTRATOR);
    }

    [Fact]
    public async Task UpdateSession_ManuallySyncsState()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        hub.RegisterConnection(connection);
        SessionEntry sessionInfo = hub.SessionStore.CreateSession(connection);
        await hub.SessionStore.StoreAsync(sessionInfo);

        // Modify state
        connection.Attributes["custom"] = "data";

        // Manual sync
        SyncSession(connection, sessionInfo);
        await hub.SessionStore.StoreAsync(sessionInfo);

        // Verify snapshot from Hub store
        SessionEntry? updated = await hub.SessionStore.RetrieveAsync(sessionInfo.Snapshot.SessionToken);
        _ = updated.Should().NotBeNull();
        _ = updated.Snapshot.Attributes.Should().ContainKey("custom").WhoseValue.Should().Be("data");
    }

    [Fact]
    public async Task StoreAsync_WhenTokenIsOverwritten_ReturnsPreviousEntry()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        connection.Secret = new Abstractions.Primitives.Bytes32(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        connection.Attributes["marker"] = "old";

        SessionEntry original = hub.SessionStore.CreateSession(connection);
        await hub.SessionStore.StoreAsync(original);

        SessionSnapshot replacementSnapshot = new()
        {
            SessionToken = original.Snapshot.SessionToken,
            CreatedAtUnixMilliseconds = original.Snapshot.CreatedAtUnixMilliseconds,
            ExpiresAtUnixMilliseconds = original.Snapshot.ExpiresAtUnixMilliseconds,
            Secret = new Abstractions.Primitives.Bytes32(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            Algorithm = original.Snapshot.Algorithm,
            Level = original.Snapshot.Level,
            Attributes = ObjectMap<string, object>.Rent()
        };
        replacementSnapshot.Attributes!["marker"] = "new";

        SessionEntry replacement = new(replacementSnapshot, original.ConnectionId);
        await hub.SessionStore.StoreAsync(replacement);

        _ = original.Snapshot.Secret.Should().Be(Abstractions.Primitives.Bytes32.Zero);
        _ = original.Snapshot.Attributes.Should().BeNull();

        SessionEntry? stored = await hub.SessionStore.RetrieveAsync(original.Snapshot.SessionToken);
        _ = stored.Should().BeSameAs(replacement);

        await hub.SessionStore.RemoveAsync(original.Snapshot.SessionToken);
    }

    private static void SyncSession(IConnection connection, SessionEntry session)
    {
        SessionSnapshot old = session.Snapshot;

        ObjectMap<string, object> attrs = ObjectMap<string, object>.Rent();
        foreach (KeyValuePair<string, object> attr in connection.Attributes)
        {
            if (attr.Key == "nalix.handshake.state")
            {
                continue;
            }

            attrs[attr.Key] = attr.Value;
        }

        session.Snapshot = new SessionSnapshot
        {
            SessionToken = old.SessionToken,
            CreatedAtUnixMilliseconds = old.CreatedAtUnixMilliseconds,
            ExpiresAtUnixMilliseconds = old.ExpiresAtUnixMilliseconds,
            Secret = connection.Secret,
            Algorithm = connection.Algorithm,
            Level = connection.Level,
            Attributes = attrs
        };

        old.Return();
    }

    private static void ApplySession(IConnection connection, SessionEntry session)
    {
        SessionSnapshot snapshot = session.Snapshot;
        connection.Secret = snapshot.Secret;
        connection.Algorithm = snapshot.Algorithm;
        connection.Level = snapshot.Level;

        if (snapshot.Attributes != null)
        {
            foreach (KeyValuePair<string, object> attr in snapshot.Attributes)
            {
                connection.Attributes[attr.Key] = attr.Value;
            }
        }

        connection.Attributes["nalix.handshake.established"] = true;
        connection.Attributes["nalix.session.token"] = snapshot.SessionToken;
    }

    private sealed class ConnectedSocketScope : IDisposable
    {
        private ConnectedSocketScope(Socket listenerSocket, Socket clientSocket, Socket serverSocket)
        {
            this.ListenerSocket = listenerSocket;
            this.ClientSocket = clientSocket;
            this.ServerSocket = serverSocket;
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
            try
            {
                this.ClientSocket.Dispose();
            }
            catch (SocketException ex)
            {
                Trace.WriteLine($"Ignoring SocketException while disposing ClientSocket: {ex}");
            }
            catch (ObjectDisposedException ex)
            {
                Trace.WriteLine($"Ignoring ObjectDisposedException while disposing ClientSocket: {ex}");
            }

            try
            {
                this.ServerSocket.Dispose();
            }
            catch (SocketException ex)
            {
                Trace.WriteLine($"Ignoring SocketException while disposing ServerSocket: {ex}");
            }
            catch (ObjectDisposedException ex)
            {
                Trace.WriteLine($"Ignoring ObjectDisposedException while disposing ServerSocket: {ex}");
            }

            try
            {
                this.ListenerSocket.Dispose();
            }
            catch (SocketException ex)
            {
                Trace.WriteLine($"Ignoring SocketException while disposing ListenerSocket: {ex}");
            }
            catch (ObjectDisposedException ex)
            {
                Trace.WriteLine($"Ignoring ObjectDisposedException while disposing ListenerSocket: {ex}");
            }
        }
    }
}














