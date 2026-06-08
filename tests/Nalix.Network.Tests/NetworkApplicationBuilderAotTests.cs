#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Hosting;
using Nalix.Hosting.Internal;
using Nalix.Hosting.Protocols;
using Nalix.Runtime.Dispatching;
using Xunit;

namespace Nalix.Network.Tests;

public sealed class NetworkApplicationBuilderAotTests
{
    #region ScanHandlers is a no-op

    [Fact]
    public void ScanHandlers_Assembly_ReturnsBuilder_DoesNotThrow()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

#pragma warning disable CS0618
        INetworkApplicationBuilder result = builder.ScanHandlers(typeof(NetworkApplicationBuilderAotTests).Assembly);
#pragma warning restore CS0618

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void ScanHandlers_Generic_ReturnsBuilder_DoesNotThrow()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

#pragma warning disable CS0618
        INetworkApplicationBuilder result = builder.ScanHandlers<NetworkApplicationBuilderAotTests>();
#pragma warning restore CS0618

        result.Should().BeSameAs(builder);
    }

    #endregion

    #region AddHandler registrations

    [Fact]
    public void AddHandler_Generic_RegistersHandlerDescriptor()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

        builder.AddHandler<TestAotController>();

        HostingBuilderContext state = GetState(builder);
        state.Handlers.Should().ContainSingle(h => h.HandlerType == typeof(TestAotController));
    }

    [Fact]
    public void AddHandler_Type_RegistersHandlerDescriptor()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

        builder.AddHandler(typeof(TestAotController));

        HostingBuilderContext state = GetState(builder);
        state.Handlers.Should().ContainSingle(h => h.HandlerType == typeof(TestAotController));
    }

    [Fact]
    public void AddHandler_Factory_RegistersHandlerDescriptor()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        var controller = new TestAotController();

        builder.AddHandler(() => controller);

        HostingBuilderContext state = GetState(builder);
        state.Handlers.Should().ContainSingle(h => h.HandlerType == typeof(TestAotController));
    }

    [Fact]
    public void AddHandler_DuplicateType_DoesNotThrow()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

        builder.AddHandler<TestAotController>();
        builder.AddHandler(typeof(TestAotController));

        HostingBuilderContext state = GetState(builder);
        state.Handlers.Should().HaveCount(2);
        state.Handlers.Should().AllSatisfy(h => h.HandlerType.Should().Be(typeof(TestAotController)));
    }

    #endregion

    #region ScanHandlers does NOT add handlers (no-op)

    [Fact]
    public void ScanHandlers_DoesNotPopulateHandlers()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

#pragma warning disable CS0618
        builder.ScanHandlers<NetworkApplicationBuilderAotTests>();
#pragma warning restore CS0618

        HostingBuilderContext state = GetState(builder);
        state.Handlers.Should().BeEmpty("ScanHandlers is a no-op and does not register handlers");
    }

    #endregion

    #region CreateProtocol generic instantiation

    [Fact]
    public void CreateProtocol_WithDispatchConstructor_PassesDispatch()
    {
        var mockDispatch = new StubPacketDispatch();

        IProtocol protocol = NetworkApplicationBuilder.CreateProtocol<TestProtocolWithDispatch>(mockDispatch);

        protocol.Should().NotBeNull();
        protocol.Should().BeOfType<TestProtocolWithDispatch>();
        ((TestProtocolWithDispatch)protocol).ReceivedDispatch.Should().BeSameAs(mockDispatch);
    }

    [Fact]
    public void CreateProtocol_WithoutDispatchConstructor_UsesParameterlessCtor()
    {
        var mockDispatch = new StubPacketDispatch();

        IProtocol protocol = NetworkApplicationBuilder.CreateProtocol<TestProtocolNoDispatch>(mockDispatch);

        protocol.Should().NotBeNull();
        protocol.Should().BeOfType<TestProtocolNoDispatch>();
        ((TestProtocolNoDispatch)protocol).WasParameterlessCtorCalled.Should().BeTrue();
    }

    [Fact]
    public void CreateProtocol_NullDispatch_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NetworkApplicationBuilder.CreateProtocol<TestProtocolWithDispatch>(null!));
    }

    #endregion

    #region ResolveHandlerRegistrations returns only explicit handlers

    [Fact]
    public void ResolveHandlerRegistrations_ReturnsOnlyExplicitHandlers()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
        builder.AddHandler<TestAotController>();

        HostingBuilderContext state = GetState(builder);
        IEnumerable<HandlerDescriptor> handlers = InvokeResolveHandlerRegistrations(state);

        handlers.Should().HaveCount(1);
        handlers.First().HandlerType.Should().Be(typeof(TestAotController));
    }

    [Fact]
    public void ResolveHandlerRegistrations_EmptyWhenNoHandlersAdded()
    {
        NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();

        HostingBuilderContext state = GetState(builder);
        IEnumerable<HandlerDescriptor> handlers = InvokeResolveHandlerRegistrations(state);

        handlers.Should().BeEmpty();
    }

    #endregion

    #region Helpers and Test Types

    private static HostingBuilderContext GetState(NetworkApplicationBuilder builder)
    {
        FieldInfo? field = typeof(NetworkApplicationBuilder)
            .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
        return (HostingBuilderContext)field!.GetValue(builder)!;
    }

    private static IEnumerable<HandlerDescriptor> InvokeResolveHandlerRegistrations(HostingBuilderContext state)
    {
        MethodInfo? method = typeof(NetworkApplicationBuilder)
            .GetMethod("ResolveHandlerRegistrations", BindingFlags.Static | BindingFlags.NonPublic);
        return (IEnumerable<HandlerDescriptor>)method!.Invoke(null, [state])!;
    }

    [Nalix.Abstractions.Injection.Injectable]
    private sealed class TestProtocolWithDispatch : Protocol
    {
        public IPacketDispatch? ReceivedDispatch { get; }
        public override IFrameProcessor FrameProcessor => null!;
        public override Nalix.Abstractions.Networking.Protocols.IOpCodeExtractor OpCodeExtractor => null!;

        public TestProtocolWithDispatch(IPacketDispatch dispatch)
        {
            ReceivedDispatch = dispatch;
        }

        public override void ProcessMessage(object? sender, IConnectEventArgs args) { }
    }

    [Nalix.Abstractions.Injection.Injectable]
    private sealed class TestProtocolNoDispatch : Protocol
    {
        public bool WasParameterlessCtorCalled { get; }
        public override IFrameProcessor FrameProcessor => null!;
        public override Nalix.Abstractions.Networking.Protocols.IOpCodeExtractor OpCodeExtractor => null!;

        public TestProtocolNoDispatch()
        {
            WasParameterlessCtorCalled = true;
        }

        public override void ProcessMessage(object? sender, IConnectEventArgs args) { }
    }

    private sealed class StubPacketDispatch : IPacketDispatch
    {
        public bool HandlePacket(Nalix.Framework.Memory.Buffers.IBufferLease lease, IConnection connection) => true;
    }

    [PacketController("AotTest")]
    private sealed class TestAotController { }

    #endregion
}
#endif
