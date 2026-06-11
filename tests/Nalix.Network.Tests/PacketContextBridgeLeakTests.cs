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

    #region Response Packet Disposal - Sync

    [Fact]
    public async Task ResponsePacket_SyncHandler_DisposedAfterSend()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<SyncResponseController>();

        options.TryResolveHandler(0x3000, out var descriptor).Should().BeTrue();

        s_pool.GetTypeInfo<Control>(); // ensure pool is initialized

        for (int i = 0; i < 200; i++)
        {
            Control packet = CreateControlPacket(0x3000);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(200);

        var info = s_pool.GetTypeInfo<Control>();
        long outstanding = (long)info["Outstanding"];

        // Response packets created by the handler must be returned to the pool.
        // Allow small tolerance for pool warmup but assert no monotonic growth.
        outstanding.Should().BeLessThanOrEqualTo(2,
            "handler-returned Control response packets must be disposed after send");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class SyncResponseController
    {
        [PacketOpcode(0x3000)]
        public static Control Handle(IPacketContext<Control> context)
        {
            Control response = new();
            response.Initialize(ControlType.PONG, sequenceId: context.Packet.Header.SequenceId);
            return response;
        }
    }

    #endregion

    #region Response Packet Disposal - Async ValueTask<T>

    [Fact]
    public async Task ResponsePacket_AsyncValueTaskHandler_DisposedAfterSend()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<AsyncValueTaskResponseController>();

        options.TryResolveHandler(0x3001, out var descriptor).Should().BeTrue();

        for (int i = 0; i < 200; i++)
        {
            Control packet = CreateControlPacket(0x3001);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(200);

        var info = s_pool.GetTypeInfo<Control>();
        long outstanding = (long)info["Outstanding"];

        outstanding.Should().BeLessThanOrEqualTo(2,
            "async handler-returned Control response packets must be disposed after send");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class AsyncValueTaskResponseController
    {
        [PacketOpcode(0x3001)]
        public static async ValueTask<Control> Handle(IPacketContext<Control> context)
        {
            await Task.Yield();
            Control response = new();
            response.Initialize(ControlType.PONG, sequenceId: context.Packet.Header.SequenceId);
            return response;
        }
    }

    #endregion

    #region Same Packet Return (no double-dispose)

    [Fact]
    public async Task ResponsePacket_HandlerReturnsRequestPacket_NoDoubleDispose()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<SamePacketReturnController>();

        options.TryResolveHandler(0x3002, out var descriptor).Should().BeTrue();

        // Dispatch many times — if double-dispose occurred, the packet pool
        // would log negative-outstanding errors or throw.
        for (int i = 0; i < 200; i++)
        {
            Control packet = CreateControlPacket(0x3002);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(200);

        // If we get here without exceptions, the atomic _isRented guard
        // prevented double-return. Verify pool is healthy.
        var info = s_pool.GetTypeInfo<Control>();
        string status = (string)info["Status"];
        status.Should().NotBe("Unhealthy",
            "returning the same request packet must not corrupt the pool");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class SamePacketReturnController
    {
        [PacketOpcode(0x3002)]
        public static Control Handle(IPacketContext<Control> context)
        {
            // Return the request packet itself as the response.
            // Dispatcher must detect ReferenceEquals and skip dispose,
            // letting the base PacketContext handle cleanup.
            return context.Packet;
        }
    }

    #endregion

    #region Response Packet + Exception (handler throws after partial work)

    [Fact]
    public async Task ResponsePacket_HandlerThrows_RequestContextStillCleanedUp()
    {
        using ConnectedSocketScope scope = await ConnectedSocketScope.CreateAsync();
        using Connection connection = new(scope.ServerSocket, s_testOpCodeExtractor);

        PacketDispatchOptions<IPacket> options = new();
        _ = options.WithHandler<ThrowingResponseController>();

        options.TryResolveHandler(0x3003, out var descriptor).Should().BeTrue();

        for (int i = 0; i < 100; i++)
        {
            Control packet = CreateControlPacket(0x3003);
            await options.ExecuteResolvedHandlerAsync(descriptor, packet, connection);
        }

        await Task.Delay(200);

        // Bridge context and request context must still be cleaned up on exception.
        var ctxInfo = s_pool.GetTypeInfo<PacketContext<Control>>();
        long ctxOutstanding = (long)ctxInfo["Outstanding"];
        ctxOutstanding.Should().BeLessThanOrEqualTo(1,
            "PacketContext<Control> must be returned even when handler throws");
    }

    [PacketController]
    [Nalix.Abstractions.Injection.Injectable]
    internal sealed class ThrowingResponseController
    {
        [PacketOpcode(0x3003)]
        public static Control Handle(IPacketContext<Control> context)
        {
            throw new InvalidOperationException("Handler fails before returning response");
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
