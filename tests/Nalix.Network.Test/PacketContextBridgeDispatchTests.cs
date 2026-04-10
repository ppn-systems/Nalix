using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Network.Connections;
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;
using Xunit;

namespace Nalix.Network.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class PacketContextBridgeDispatchTests
{
    [Fact]
    public async Task ExecuteResolvedHandlerAsync_BridgesTypedInterfaceContext_WhenDispatcherUsesIPacket()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        InterfaceContextController.Reset();
        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<InterfaceContextController>();

        bool resolved = options.TryResolveHandler(0x1000, out var descriptor);
        resolved.Should().BeTrue();

        Control packet = CreateControlPacket(opCode: 0x1000);

        await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);

        Control observed = await InterfaceContextController.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observed.Should().BeSameAs(packet);
    }

    [Fact]
    public async Task ExecuteResolvedHandlerAsync_BridgesTypedConcreteContext_WhenDispatcherUsesIPacket()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket);

        ConcreteContextController controller = new();
        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler(controller);

        bool resolved = options.TryResolveHandler(0x1001, out var descriptor);
        resolved.Should().BeTrue();

        Control packet = CreateControlPacket(opCode: 0x1001);

        await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);

        Control observed = await controller.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        observed.Should().BeSameAs(packet);
        controller.ObservedContextType.Should().Be<PacketContext<Control>>();
    }

    private static Control CreateControlPacket(ushort opCode)
    {
        Control packet = new();
        packet.Initialize(opCode, ControlType.NONE, sequenceId: 1);
        return packet;
    }

    [PacketController]
    private sealed class InterfaceContextController
    {
        public static TaskCompletionSource<Control> Observed { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static void Reset() => Observed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        [PacketOpcode(0x1000)]
        public static async Task Handle(IPacketContext<Control> context)
        {
            await Task.Yield();
            _ = Observed.TrySetResult(context.Packet);
        }
    }

    [PacketController]
    private sealed class ConcreteContextController
    {
        public TaskCompletionSource<Control> Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Type? ObservedContextType { get; private set; }

        [PacketOpcode(0x1001)]
        public async Task Handle(PacketContext<Control> context)
        {
            await Task.Yield();
            ObservedContextType = context.GetType();
            _ = Observed.TrySetResult(context.Packet);
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
