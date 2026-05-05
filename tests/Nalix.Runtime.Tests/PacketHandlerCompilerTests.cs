#if DEBUG
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Primitives;
using Nalix.Abstractions.Security;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Compilation;
using NSubstitute;
using Xunit;

namespace Nalix.Runtime.Tests;

public sealed class PacketHandlerCompilerTests
{
    #region Helpers

    private static PacketContext<T> CreateContext<T>(T packet, IConnection? connection = null, CancellationToken ct = default)
        where T : IPacket
    {
        PacketContext<T> ctx = new();
        ctx.Initialize(packet, connection ?? CreateConnection(), default, reliable: false, ct);
        return ctx;
    }

    private static IConnection CreateConnection()
    {
        IConnection conn = Substitute.For<IConnection>();
        conn.Level.Returns(PermissionLevel.SYSTEM_ADMINISTRATOR);
        return conn;
    }

    #endregion

    #region Test packet types

    private sealed class TestPacket : IPacket
    {
        public int Length => 0;
        public PacketHeader Header { get; set; }
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
    }

    private sealed class ConcretePacket : IPacket
    {
        public int Length => 0;
        public PacketHeader Header { get; set; }
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
        public int Value { get; set; }
    }

    #endregion

    #region Test controllers

    // --- ContextOnly signature ---
    [PacketController]
    private sealed class ContextOnlyController
    {
        public static int LastOpcode;

        [PacketOpcode(0x0001)]
        public static void HandleStaticVoid(PacketContext<TestPacket> ctx)
        {
            LastOpcode = ctx.Packet.Header.OpCode;
        }

        [PacketOpcode(0x0002)]
        public static int HandleStaticReturn(PacketContext<TestPacket> ctx)
        {
            return 42;
        }

        [PacketOpcode(0x0003)]
        public void HandleInstanceVoid(PacketContext<TestPacket> ctx)
        {
            LastOpcode = ctx.Packet.Header.OpCode;
        }

        [PacketOpcode(0x0004)]
        public string HandleInstanceReturn(PacketContext<TestPacket> ctx)
        {
            return "hello";
        }
    }

    // --- ContextWithToken signature ---
    [PacketController]
    private sealed class ContextWithTokenController
    {
        public static CancellationToken LastToken;

        [PacketOpcode(0x0010)]
        public static void HandleStaticVoid(PacketContext<TestPacket> ctx, CancellationToken ct)
        {
            LastToken = ct;
        }

        [PacketOpcode(0x0011)]
        public static Task<int> HandleStaticTask(PacketContext<TestPacket> ctx, CancellationToken ct)
        {
            LastToken = ct;
            return Task.FromResult(99);
        }

        [PacketOpcode(0x0012)]
        public ValueTask HandleInstanceValueTask(PacketContext<TestPacket> ctx, CancellationToken ct)
        {
            LastToken = ct;
            return ValueTask.CompletedTask;
        }

        [PacketOpcode(0x0013)]
        public ValueTask<string> HandleInstanceValueTaskT(PacketContext<TestPacket> ctx, CancellationToken ct)
        {
            LastToken = ct;
            return ValueTask.FromResult("vt");
        }
    }

    // --- Legacy signatures (TPacket matches) ---
    [PacketController]
    private sealed class LegacyController
    {
        public static TestPacket? LastPacket;
        public static IConnection? LastConn;

        [PacketOpcode(0x0020)]
        public static void HandleNoToken(TestPacket pkt, IConnection conn)
        {
            LastPacket = pkt;
            LastConn = conn;
        }

        [PacketOpcode(0x0021)]
        public static Task HandleNoTokenTask(TestPacket pkt, IConnection conn)
        {
            LastPacket = pkt;
            return Task.CompletedTask;
        }

        [PacketOpcode(0x0022)]
        public void HandleWithToken(TestPacket pkt, IConnection conn, CancellationToken ct)
        {
            LastPacket = pkt;
            LastConn = conn;
        }

        [PacketOpcode(0x0023)]
        public ValueTask<ushort> HandleWithTokenValueTaskT(TestPacket pkt, IConnection conn, CancellationToken ct)
        {
            LastPacket = pkt;
            return ValueTask.FromResult(pkt.Header.OpCode);
        }
    }

    // --- Legacy concrete signatures (TPacket = IPacket, handler uses ConcretePacket) ---
    [PacketController]
    private sealed class ConcreteLegacyController
    {
        public static ConcretePacket? LastPacket;

        [PacketOpcode(0x0030)]
        public static void HandleConcreteNoToken(ConcretePacket pkt, IConnection conn)
        {
            LastPacket = pkt;
        }

        [PacketOpcode(0x0031)]
        public static int HandleConcreteWithToken(ConcretePacket pkt, IConnection conn, CancellationToken ct)
        {
            LastPacket = pkt;
            return pkt.Value;
        }
    }

    // --- Context bridge (handler uses PacketContext<ConcretePacket>, dispatcher uses IPacket) ---
    [PacketController]
    private sealed class ContextBridgeController
    {
        public static ConcretePacket? LastPacket;

        [PacketOpcode(0x0040)]
        public static void HandleBridge(PacketContext<ConcretePacket> ctx)
        {
            LastPacket = ctx.Packet;
        }

        [PacketOpcode(0x0041)]
        public static int HandleBridgeWithToken(PacketContext<ConcretePacket> ctx, CancellationToken ct)
        {
            LastPacket = ctx.Packet;
            return ctx.Packet.Value;
        }
    }

    // --- Duplicate opcode controller ---
    [PacketController]
    private sealed class DuplicateOpcodeController
    {
        [PacketOpcode(0x00FF)]
        public static void First(PacketContext<TestPacket> ctx) { }

        [PacketOpcode(0x00FF)]
        public static void Second(PacketContext<TestPacket> ctx) { }
    }

    // --- Missing attribute controller ---
    private sealed class NoAttributeController
    {
        [PacketOpcode(0x0001)]
        public static void Handle(PacketContext<TestPacket> ctx) { }
    }

    // --- IPacket dispatcher with concrete context handler ---
    [PacketController]
    private sealed class MixedController
    {
        public static object? LastResult;

        [PacketOpcode(0x0050)]
        public static void HandleContextOnly(PacketContext<IPacket> ctx)
        {
            LastResult = ctx.Packet;
        }

        [PacketOpcode(0x0051)]
        public static int HandleLegacyConcrete(ConcretePacket pkt, IConnection conn)
        {
            LastResult = pkt;
            return pkt.Value;
        }
    }

    #endregion

    #region ContextOnly tests

    [Fact]
    public void CompileHandlers_ContextOnly_StaticVoid_InvokesSuccessfully()
    {
        PacketHandler<IPacket>[] handlers = PacketHandlerCompiler<MixedController, IPacket>
            .CompileHandlers(() => new MixedController());

        // Find handler 0x0050
        PacketHandler<IPacket> handler = FindHandler(handlers, 0x0050);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0050 } };
        PacketContext<IPacket> ctx = CreateContext<IPacket>(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Same(pkt, MixedController.LastResult);
    }

    [Fact]
    public void CompileHandlers_ContextOnly_InstanceVoid_InvokesSuccessfully()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0003);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0003 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(0x0003, ContextOnlyController.LastOpcode);
    }

    [Fact]
    public void CompileHandlers_ContextOnly_StaticReturn_ReturnsCorrectValue()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0002);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0002 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(42, result.Result);
    }

    [Fact]
    public void CompileHandlers_ContextOnly_InstanceReturn_ReturnsCorrectValue()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0004);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0004 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal("hello", result.Result);
    }

    #endregion

    #region ContextWithToken tests

    [Fact]
    public void CompileHandlers_ContextWithToken_StaticVoid_PassesToken()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextWithTokenController, TestPacket>
            .CompileHandlers(() => new ContextWithTokenController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0010);
        using CancellationTokenSource cts = new();
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0010 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt, ct: cts.Token);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(cts.Token, ContextWithTokenController.LastToken);
    }

    [Fact]
    public async Task CompileHandlers_ContextWithToken_StaticTask_ReturnsCorrectValue()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextWithTokenController, TestPacket>
            .CompileHandlers(() => new ContextWithTokenController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0011);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0011 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);
        object value = await result;

        Assert.Equal(99, value);
    }

    [Fact]
    public async Task CompileHandlers_ContextWithToken_InstanceValueTaskT_ReturnsCorrectValue()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextWithTokenController, TestPacket>
            .CompileHandlers(() => new ContextWithTokenController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0013);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0013 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);
        object value = await result;

        Assert.Equal("vt", value);
    }

    #endregion

    #region Legacy signature tests (TPacket matches)

    [Fact]
    public void CompileHandlers_LegacyNoToken_StaticVoid_PassesArgsCorrectly()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<LegacyController, TestPacket>
            .CompileHandlers(() => new LegacyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0020);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0020 } };
        IConnection conn = CreateConnection();
        PacketContext<TestPacket> ctx = CreateContext(pkt, conn);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Same(pkt, LegacyController.LastPacket);
        Assert.Same(conn, LegacyController.LastConn);
    }

    [Fact]
    public async Task CompileHandlers_LegacyNoToken_StaticTask_CompletesSuccessfully()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<LegacyController, TestPacket>
            .CompileHandlers(() => new LegacyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0021);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0021 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);
        await result;

        Assert.Same(pkt, LegacyController.LastPacket);
    }

    [Fact]
    public void CompileHandlers_LegacyWithToken_InstanceVoid_PassesArgsCorrectly()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<LegacyController, TestPacket>
            .CompileHandlers(() => new LegacyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0022);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0022 } };
        IConnection conn = CreateConnection();
        using CancellationTokenSource cts = new();
        PacketContext<TestPacket> ctx = CreateContext(pkt, conn, cts.Token);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Same(pkt, LegacyController.LastPacket);
        Assert.Same(conn, LegacyController.LastConn);
    }

    [Fact]
    public async Task CompileHandlers_LegacyWithToken_InstanceValueTaskT_ReturnsCorrectValue()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<LegacyController, TestPacket>
            .CompileHandlers(() => new LegacyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0023);
        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0023 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);
        object value = await result;

        Assert.Equal((ushort)0x0023, (ushort)value);
    }

    #endregion

    #region Legacy concrete signature tests (TPacket = IPacket, handler uses ConcretePacket)

    [Fact]
    public void CompileHandlers_LegacyConcreteNoToken_StaticVoid_CastsPacketCorrectly()
    {
        PacketHandler<IPacket>[] handlers = PacketHandlerCompiler<MixedController, IPacket>
            .CompileHandlers(() => new MixedController());

        PacketHandler<IPacket> handler = FindHandler(handlers, 0x0051);
        ConcretePacket pkt = new() { Header = new PacketHeader { OpCode = 0x0051 }, Value = 77 };
        PacketContext<IPacket> ctx = CreateContext<IPacket>(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(77, result.Result);
    }

    #endregion

    #region Context bridge tests (PacketContext<Concrete> with IPacket dispatcher)

    [Fact]
    public void CompileHandlers_ContextBridge_BridgesContextSuccessfully()
    {
        PacketHandler<IPacket>[] handlers = PacketHandlerCompiler<ContextBridgeController, IPacket>
            .CompileHandlers(() => new ContextBridgeController());

        PacketHandler<IPacket> handler = FindHandler(handlers, 0x0040);
        ConcretePacket pkt = new() { Header = new PacketHeader { OpCode = 0x0040 }, Value = 55 };
        PacketContext<IPacket> ctx = CreateContext<IPacket>(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);

        Assert.True(result.IsCompletedSuccessfully);
        Assert.Same(pkt, ContextBridgeController.LastPacket);
    }

    [Fact]
    public async Task CompileHandlers_ContextBridgeWithToken_ReturnsCorrectValue()
    {
        PacketHandler<IPacket>[] handlers = PacketHandlerCompiler<ContextBridgeController, IPacket>
            .CompileHandlers(() => new ContextBridgeController());

        PacketHandler<IPacket> handler = FindHandler(handlers, 0x0041);
        ConcretePacket pkt = new() { Header = new PacketHeader { OpCode = 0x0041 }, Value = 88 };
        PacketContext<IPacket> ctx = CreateContext<IPacket>(pkt);

        ValueTask<object> result = handler.ExecuteAsync(ctx);
        object value = await result;

        Assert.Equal(88, value);
        Assert.Same(pkt, ContextBridgeController.LastPacket);
    }

    #endregion

    #region Metadata tests

    [Fact]
    public void CompileHandlers_HandlerHasCorrectOpcode()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        Assert.Contains(handlers, h => h.OpCode == 0x0001);
        Assert.Contains(handlers, h => h.OpCode == 0x0002);
        Assert.Contains(handlers, h => h.OpCode == 0x0003);
        Assert.Contains(handlers, h => h.OpCode == 0x0004);
    }

    [Fact]
    public void CompileHandlers_HandlerHasCorrectReturnType()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        PacketHandler<TestPacket> voidHandler = FindHandler(handlers, 0x0001);
        Assert.Equal(typeof(void), voidHandler.ReturnType);

        PacketHandler<TestPacket> intHandler = FindHandler(handlers, 0x0002);
        Assert.Equal(typeof(int), intHandler.ReturnType);

        PacketHandler<TestPacket> stringHandler = FindHandler(handlers, 0x0004);
        Assert.Equal(typeof(string), stringHandler.ReturnType);
    }

    [Fact]
    public void CompileHandlers_HandlerHasReturnHandler()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        foreach (PacketHandler<TestPacket> handler in handlers)
        {
            Assert.NotNull(handler.ReturnHandler);
        }
    }

    [Fact]
    public void CompileHandlers_ControllerInstanceIsReused()
    {
        ContextOnlyController instance = new();
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => instance);

        foreach (PacketHandler<TestPacket> handler in handlers)
        {
            Assert.Same(instance, handler.Instance);
        }
    }

    #endregion

    #region Error cases

    [Fact]
    public void CompileHandlers_MissingControllerAttribute_ThrowsInternalErrorException()
    {
        Assert.Throws<InternalErrorException>(() =>
            PacketHandlerCompiler<NoAttributeController, TestPacket>
                .CompileHandlers(() => new NoAttributeController()));
    }

    #endregion

    #region Cached compilation tests

    [Fact]
    public void CompileHandlers_CalledTwice_ReturnsSameHandlerCount()
    {
        PacketHandler<TestPacket>[] first = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        PacketHandler<TestPacket>[] second = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        Assert.Equal(first.Length, second.Length);
    }

    #endregion

    #region CanExecute tests

    [Fact]
    public void CanExecute_WhenPermissionExceeded_ReturnsFalse()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        PacketHandler<TestPacket> handler = FindHandler(handlers, 0x0001);

        IConnection lowLevelConn = Substitute.For<IConnection>();
        lowLevelConn.Level.Returns(PermissionLevel.GUEST);

        TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0001 } };
        PacketContext<TestPacket> ctx = CreateContext(pkt, lowLevelConn);

        // The default handler has no [PacketPermission] so CanExecute should return true
        Assert.True(handler.CanExecute(ctx));
    }

    #endregion

    #region Concurrent invocations

    [Fact]
    public async Task CompileHandlers_ConcurrentInvocations_DoNotInterfere()
    {
        PacketHandler<TestPacket>[] handlers = PacketHandlerCompiler<ContextOnlyController, TestPacket>
            .CompileHandlers(() => new ContextOnlyController());

        PacketHandler<TestPacket> returnHandler = FindHandler(handlers, 0x0002);

        Task<object>[] tasks = new Task<object>[100];
        for (int i = 0; i < 100; i++)
        {
            TestPacket pkt = new() { Header = new PacketHeader { OpCode = 0x0002 } };
            PacketContext<TestPacket> ctx = CreateContext(pkt);
            tasks[i] = returnHandler.ExecuteAsync(ctx).AsTask();
        }

        object[] results = await Task.WhenAll(tasks);

        foreach (object r in results)
        {
            Assert.Equal(42, r);
        }
    }

    #endregion

    #region Helper

    private static PacketHandler<T> FindHandler<T>(PacketHandler<T>[] handlers, ushort opcode) where T : IPacket
    {
        foreach (PacketHandler<T> h in handlers)
        {
            if (h.OpCode == opcode)
            {
                return h;
            }
        }

        throw new InvalidOperationException($"Handler with opcode 0x{opcode:X4} not found.");
    }

    #endregion
}
#endif
