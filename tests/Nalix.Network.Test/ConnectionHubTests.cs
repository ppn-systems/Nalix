using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Sessions;
using Nalix.Common.Primitives;
using Nalix.Network.Connections;
using Nalix.Network.Sessions;
using Xunit;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class ConnectionHubTests
{
    [Fact]
    public async Task RegisterConnection_IncrementsCount_AndAllowsLookup()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        hub.RegisterConnection(connection);

        hub.Count.Should().Be(1);
        hub.GetConnection(connection.ID).Should().BeSameAs(connection);
    }

    [Fact]
    public async Task UnregisterConnection_DecrementsCount_AndRaisesEvent()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        IConnection? observed = null;
        hub.ConnectionUnregistered += c => observed = c;

        hub.RegisterConnection(connection);
        hub.UnregisterConnection(connection);

        hub.Count.Should().Be(0);
        observed.Should().NotBeNull();
        observed!.ID.Should().Be(connection.ID);
        hub.GetConnection(connection.ID).Should().BeNull();
    }

    [Fact]
    public async Task UnregisterConnection_WhenSessionStoreFails_ReclaimsSessionSnapshot()
    {
        using FailingSessionStore failingStore = new();
        using ConnectionHub hub = new(failingStore);
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        connection.Secret = new Bytes32(RandomNumberGenerator.GetBytes(Bytes32.Size));
        connection.Attributes[ConnectionAttributes.HandshakeEstablished] = true;
        connection.Attributes["attr-1"] = 1;
        connection.Attributes["attr-2"] = 2;
        connection.Attributes["attr-3"] = 3;
        connection.Attributes["attr-4"] = 4;
        connection.Attributes["attr-5"] = 5;

        hub.RegisterConnection(connection);
        hub.UnregisterConnection(connection);

        SessionEntry attempted = await failingStore.WaitForStoreAttemptAsync(TimeSpan.FromSeconds(3));
        attempted.Snapshot.Secret.Should().Be(Bytes32.Zero);
        attempted.Snapshot.Attributes.Should().BeNull();
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

    private sealed class FailingSessionStore : SessionStoreBase, IDisposable
    {
        private readonly TaskCompletionSource<SessionEntry> _storeAttempt =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<SessionEntry> WaitForStoreAttemptAsync(TimeSpan timeout)
        {
            using CancellationTokenSource cts = new(timeout);
            Task completed = await Task.WhenAny(_storeAttempt.Task, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));
            if (completed != _storeAttempt.Task)
            {
                throw new TimeoutException("Session store was not invoked in time.");
            }

            return await _storeAttempt.Task;
        }

        public override ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _storeAttempt.TrySetResult(entry);
            throw new InvalidOperationException("Simulated session-store failure.");
        }

        public override ValueTask RemoveAsync(ulong sessionToken, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public override ValueTask<SessionEntry?> RetrieveAsync(ulong sessionToken, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SessionEntry?>(null);
        }

        public override ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SessionEntry?>(null);
        }

        public void Dispose()
        {
        }
    }
}

