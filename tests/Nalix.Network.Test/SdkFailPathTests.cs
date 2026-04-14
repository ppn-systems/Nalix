using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Common.Security;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;
using Nalix.SDK.Transport.Extensions;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class SdkFailPathTests
{
    [Fact]
    public async Task UdpSession_SendWithoutSessionToken_ThrowsNetworkException()
    {
        EnsureInfrastructure();

        using UdpSession session = new(
            new TransportOptions
            {
                Address = "127.0.0.1",
                Port = 65535
            },
            new PacketRegistryFactory().CreateCatalog());

        await session.Invoking(s => s.SendAsync(new Control()))
            .Should().ThrowAsync<NetworkException>()
            .WithMessage("*SessionToken must be set*");
    }

    [Fact]
    public async Task TcpSession_ReceiveMalformedFrame_DisconnectsClient()
    {
        EnsureInfrastructure();

        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using TcpSession session = new(
            new TransportOptions
            {
                Address = "127.0.0.1",
            Port = (ushort)((IPEndPoint)scope.ListenerSocket.LocalEndPoint!).Port
            },
            new PacketRegistryFactory().CreateCatalog());

        await session.ConnectAsync("127.0.0.1", (ushort)((IPEndPoint)scope.ListenerSocket.LocalEndPoint!).Port);

        byte[] malformedFrame = new byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(malformedFrame, ushort.MaxValue);
        TaskCompletionSource<Exception> errorObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void ErrorHandler(object? _, Exception ex) => errorObserved.TrySetResult(ex);
        session.OnError += ErrorHandler;

        await Task.Delay(100);
        await scope.ServerSocket.SendAsync(malformedFrame);

        _ = await errorObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForStateAsync(() => !session.IsConnected, TimeSpan.FromSeconds(5));

        session.OnError -= ErrorHandler;
        session.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task RequestAsync_RejectsNegativeTimeout()
    {
        EnsureInfrastructure();

        using TcpSession session = new(
            new TransportOptions
            {
                Address = "127.0.0.1",
                Port = 1
            },
            new PacketRegistryFactory().CreateCatalog());

        await session.Invoking(s => s.RequestAsync<Control>(new Control(), RequestOptions.Default.WithTimeout(-1)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static void EnsureInfrastructure()
    {
        _ = InstanceManager.Instance.WithLogging(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
    }

    private static async Task WaitForStateAsync(Func<bool> predicate, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The operation has timed out.");
            }

            await Task.Delay(25);
        }
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
            Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            int port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            Task<Socket> acceptTask = Task.Run(() => listener.Accept());

            Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
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
