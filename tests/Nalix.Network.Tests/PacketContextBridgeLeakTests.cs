#if DEBUG
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.ProtocolFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Network.Connections;
using Nalix.Abstractions.Primitives;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Routing;
using Xunit;

namespace Nalix.Network.Tests;

/// <summary>
/// Regression tests verifying that the source-generated dispatch path
/// properly returns bridge PacketContext objects to the pool.
///
/// Bug: The generated invoker created via PacketContextBridge.Create
/// was never returned to the pool, causing PacketContext&lt;TConcrete&gt;,
/// the concrete packet, and any associated BufferLease to leak monotonically.
/// </summary>
[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class PacketContextBridgeLeakTests
{
    private static readonly IOpCodeExtractor s_testOpCodeExtractor = new TestOpCodeExtractor();
    private static readonly ObjectPoolManager s_pool = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

    private sealed class TestOpCodeExtractor : IOpCodeExtractor
    {
        public ushort Extract(ReadOnlySpan<byte> payload) =>
            payload.Length >= 2 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[0..]) : (ushort)0;
    }

    #region Helpers

    private static Control CreateControlPacket(ushort opCode)
    {
        Control packet = new();
        packet.Initialize(ControlType.NONE, sequenceId: 1);
        var h = packet.Header;
        h.OpCode = opCode;
        packet.Header = h;
        return packet;
    }

    private static long GetBridgeContextOutstanding()
    {
        var info = s_pool.GetTypeInfo<PacketContext<Control>>();
        return (long)info["Outstanding"];
    }

    #endregion Helpers

    #region Sync Void Handler

    [Fact]
    public async Task BridgeContext_SyncVoidHandler_ReturnedToPool_AfterDispatch()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<SyncVoidController>();

        options.TryResolveHandler(0x2000, out var descriptor).Should().BeTrue();

        long beforeOutstanding = GetBridgeContextOutstanding();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x2000);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(100);

        long afterOutstanding = GetBridgeContextOutstanding();

        (afterOutstanding - beforeOutstanding).Should().BeLessThanOrEqualTo(1,
            "bridge PacketContext<Control> must be returned to the pool after sync dispatch");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class SyncVoidController
    {
        [PacketOpcode(0x2000)]
        public static void Handle(IPacketContext<Control> context)
        {
        }
    }

    #endregion

    #region Async Task Handler

    [Fact]
    public async Task BridgeContext_AsyncTaskHandler_ReturnedToPool_AfterDispatch()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<AsyncTaskController>();

        options.TryResolveHandler(0x2001, out var descriptor).Should().BeTrue();

        long beforeOutstanding = GetBridgeContextOutstanding();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x2001);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(100);

        long afterOutstanding = GetBridgeContextOutstanding();

        (afterOutstanding - beforeOutstanding).Should().BeLessThanOrEqualTo(1,
            "bridge PacketContext<Control> must be returned to the pool after async Task dispatch");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class AsyncTaskController
    {
        [PacketOpcode(0x2001)]
        public static async Task Handle(IPacketContext<Control> context)
        {
            await Task.Yield();
        }
    }

    #endregion

    #region Async ValueTask Handler

    [Fact]
    public async Task BridgeContext_AsyncValueTaskHandler_ReturnedToPool_AfterDispatch()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<AsyncValueTaskController>();

        options.TryResolveHandler(0x2002, out var descriptor).Should().BeTrue();

        long beforeOutstanding = GetBridgeContextOutstanding();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x2002);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(100);

        long afterOutstanding = GetBridgeContextOutstanding();

        (afterOutstanding - beforeOutstanding).Should().BeLessThanOrEqualTo(1,
            "bridge PacketContext<Control> must be returned to the pool after async ValueTask dispatch");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class AsyncValueTaskController
    {
        [PacketOpcode(0x2002)]
        public static async ValueTask Handle(IPacketContext<Control> context)
        {
            await Task.Yield();
        }
    }

    #endregion

    #region ValueTask<T> Handler (returns response)

    [Fact]
    public async Task BridgeContext_ValueTaskOfTHandler_ReturnedToPool_AfterDispatch()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<ValueTaskOfTController>();

        options.TryResolveHandler(0x2003, out var descriptor).Should().BeTrue();

        long beforeOutstanding = GetBridgeContextOutstanding();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x2003);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(100);

        long afterOutstanding = GetBridgeContextOutstanding();

        (afterOutstanding - beforeOutstanding).Should().BeLessThanOrEqualTo(1,
            "bridge PacketContext<Control> must be returned to the pool after ValueTask<T> dispatch");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class ValueTaskOfTController
    {
        [PacketOpcode(0x2003)]
        public static ValueTask<Control> Handle(IPacketContext<Control> context)
        {
            Control response = new();
            response.Initialize(ControlType.PONG, sequenceId: context.Packet.Header.SequenceId);
            return ValueTask.FromResult(response);
        }
    }

    #endregion

    #region Exception Path

    [Fact]
    public async Task BridgeContext_ExceptionInHandler_ReturnedToPool_AfterException()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<ThrowingController>();

        options.TryResolveHandler(0x2004, out var descriptor).Should().BeTrue();

        long beforeOutstanding = GetBridgeContextOutstanding();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x2004);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(100);

        long afterOutstanding = GetBridgeContextOutstanding();

        (afterOutstanding - beforeOutstanding).Should().BeLessThanOrEqualTo(1,
            "bridge PacketContext<Control> must be returned to the pool even when handler throws");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class ThrowingController
    {
        [PacketOpcode(0x2004)]
        public static void Handle(IPacketContext<Control> context)
        {
            throw new InvalidOperationException("Simulated handler failure");
        }
    }

    #endregion

    #region Concrete PacketContext (non-interface)

    [Fact]
    public async Task BridgeContext_ConcretePacketContextHandler_ReturnedToPool_AfterDispatch()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<ConcretePacketContextController>();

        options.TryResolveHandler(0x2005, out var descriptor).Should().BeTrue();

        long beforeOutstanding = GetBridgeContextOutstanding();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x2005);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(100);

        long afterOutstanding = GetBridgeContextOutstanding();

        (afterOutstanding - beforeOutstanding).Should().BeLessThanOrEqualTo(1,
            "bridge PacketContext<Control> must be returned to the pool for concrete PacketContext handler");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class ConcretePacketContextController
    {
        [PacketOpcode(0x2005)]
        public static async ValueTask Handle(PacketContext<Control> context)
        {
            await Task.Yield();
        }
    }

    #endregion

    #region Task<T> Handler (sync return)

    [Fact]
    public async Task BridgeContext_TaskOfTHandler_ReturnedToPool_AfterDispatch()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<TaskOfTController>();

        options.TryResolveHandler(0x2006, out var descriptor).Should().BeTrue();

        long beforeOutstanding = GetBridgeContextOutstanding();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x2006);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(100);

        long afterOutstanding = GetBridgeContextOutstanding();

        (afterOutstanding - beforeOutstanding).Should().BeLessThanOrEqualTo(1,
            "bridge PacketContext<Control> must be returned to the pool after Task<T> dispatch");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class TaskOfTController
    {
        [PacketOpcode(0x2006)]
        public static Task<Control> Handle(IPacketContext<Control> context)
        {
            Control response = new();
            response.Initialize(ControlType.PONG, sequenceId: context.Packet.Header.SequenceId);
            return Task.FromResult(response);
        }
    }

    #endregion

    #region ConnectedSocketScope

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

    #endregion
}
#endif
