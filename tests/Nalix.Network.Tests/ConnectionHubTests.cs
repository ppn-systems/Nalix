using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions.Identity;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Primitives;
using Nalix.Environment.Configuration;
using Nalix.Network.Connections;
using Nalix.Network.Options;
using Nalix.Hosting.Internal;
using Nalix.Runtime.Sessions;
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
    public async Task ListConnections_ByEndpoint_UsesAddressIndex()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope1 = await ConnectedSocketScope.CreateAsync();
        using ConnectedSocketScope scope2 = await ConnectedSocketScope.CreateAsync();
        using Connection connection1 = new(scope1.ServerSocket);
        using Connection connection2 = new(scope2.ServerSocket);

        hub.RegisterConnection(connection1);
        hub.RegisterConnection(connection2);

        hub.ListConnections(connection1.NetworkEndpoint)
           .Should()
           .Contain(connection1)
           .And.Contain(connection2);

        hub.UnregisterConnection(connection1);

        hub.ListConnections(connection1.NetworkEndpoint)
           .Should()
           .NotContain(connection1)
           .And.Contain(connection2);
    }

    [Fact]
    public async Task ConnectionTerminator_CloseByEndpoint_ClosesMatchingAddress()
    {
        using ConnectionHub hub = new();
        using ConnectedSocketScope scope1 = await ConnectedSocketScope.CreateAsync();
        using ConnectedSocketScope scope2 = await ConnectedSocketScope.CreateAsync();
        using Connection connection1 = new(scope1.ServerSocket);
        using Connection connection2 = new(scope2.ServerSocket);

        hub.RegisterConnection(connection1);
        hub.RegisterConnection(connection2);

        ConnectionTerminator terminator = new(hub);

        terminator.CloseEndpoint(connection1.NetworkEndpoint).Should().Be(2);

        connection1.IsDisposed.Should().BeTrue();
        connection2.IsDisposed.Should().BeTrue();
        hub.Count.Should().Be(0);
    }

    [Fact]
    public void GetShardIndex_MixesSnowflakeUlongBeforePowerOfTwoMasking()
    {
        ConnectionHubOptions options = ConfigurationManager.Instance.Get<ConnectionHubOptions>();
        int previousShardCount = options.ShardCount;

        try
        {
            options.ShardCount = 16;
            using ConnectionHub hub = new();
            MethodInfo method = typeof(ConnectionHub).GetMethod("GetShardIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(nameof(ConnectionHub), "GetShardIndex");

            int[] indexes = Enumerable.Range(0, 256)
                                      .Select(sequence => ComposeSnowflakeId((uint)sequence))
                                      .Select(id => (int)method.Invoke(hub, [id])!)
                                      .ToArray();

            indexes.Distinct().Count().Should().BeGreaterThan(8);
        }
        finally
        {
            options.ShardCount = previousShardCount;
        }
    }

    [Fact]
    public async Task UnregisterConnection_WhenSessionStoreFails_ReclaimsSessionSnapshot()
    {
        Nalix.Environment.Configuration.ConfigurationManager.Instance.Get<Nalix.Runtime.Options.SessionStoreOptions>().MinAttributesForPersistence = 0;

        using FailingSessionStore failingStore = new();
        using SessionService sessionService = new(store: failingStore);
        using ConnectionHub hub = new();
        using SessionPersistenceObserver observer = new(hub, sessionService);
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
        
        // Wait for background persistence to finish throwing and reclaiming
        bool reclaimed = false;
        for (int i = 0; i < 50; i++)
        {
            if (attempted.Snapshot.Secret == Bytes32.Zero)
            {
                reclaimed = true;
                break;
            }
            await Task.Delay(10);
        }

        reclaimed.Should().BeTrue("Session secret should be zeroed after store failure.");
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

    private static ulong ComposeSnowflakeId(uint sequence)
        => ((ulong)(byte)SnowflakeType.Session << 56)
         | ((ulong)0x12345678u << 24)
         | ((ulong)(sequence & 0x3FFFu) << 10)
         | 1UL;

    private sealed class FailingSessionStore : ISessionStore, IDisposable
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

        public ValueTask StoreAsync(SessionEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _storeAttempt.TrySetResult(entry);
            throw new InvalidOperationException("Simulated session-store failure.");
        }

        public ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<SessionEntry?>(null);
        }

        public void Dispose()
        {
        }
    }
}














