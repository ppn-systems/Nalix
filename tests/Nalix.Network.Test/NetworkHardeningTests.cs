using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Network.Connections;
using Nalix.Network.RateLimiting;
using Xunit;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best-effort socket cleanup in test helper mirrors existing network tests.")]
public sealed class NetworkHardeningTests
{
    [Fact]
    public async Task ConnectionDispose_DirectCall_RaisesCloseEventOnce()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        Connection connection = new(scope.ServerSocket);
        Int32 closeCount = 0;

        connection.OnCloseEvent += (_, _) => closeCount++;

        connection.Dispose();
        connection.Dispose();

        closeCount.Should().Be(1);
        connection.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void DatagramGuard_WhenIPv4TrackingCapReached_RejectsNewSourcesButAllowsExistingSource()
    {
        using DatagramGuard guard = new(maxPacketsPerSecond: 10, maxTrackedIPv4Windows: 2, maxTrackedIPv6Windows: 2);
        IPEndPoint first = new(IPAddress.Parse("10.0.0.1"), 12345);
        IPEndPoint second = new(IPAddress.Parse("10.0.0.2"), 12345);
        IPEndPoint third = new(IPAddress.Parse("10.0.0.3"), 12345);

        guard.TryAccept(first).Should().BeTrue();
        guard.TryAccept(second).Should().BeTrue();
        guard.TryAccept(third).Should().BeFalse();
        guard.TryAccept(first).Should().BeTrue();
    }

    [Fact]
    public void DatagramGuard_EnforcesPerSourcePacketsPerSecond()
    {
        using DatagramGuard guard = new(maxPacketsPerSecond: 2, maxTrackedIPv4Windows: 4, maxTrackedIPv6Windows: 4);
        IPEndPoint endpoint = new(IPAddress.Parse("10.0.1.1"), 12345);

        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeTrue();
        guard.TryAccept(endpoint).Should().BeFalse();
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
