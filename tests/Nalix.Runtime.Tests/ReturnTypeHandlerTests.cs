#if DEBUG
using System;
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Internal.Results;
using Nalix.Runtime.Internal.Results.Packet;
using Nalix.Runtime.Internal.Results.Void;
using Xunit;
using NSubstitute;

namespace Nalix.Runtime.Tests;

public sealed class ReturnTypeHandlerTests
{
    [Fact]
    public async Task VoidReturnHandler_HandleAsync_DoesNothingButCompletes()
    {
        VoidReturnHandler<TestPacket> handler = new();
        PacketContext<TestPacket> context = new();

        await handler.HandleAsync(null, context);
        // Success if no exception
    }

    [Fact]
    public async Task PacketReturnHandler_Reliable_CallsSenderSendAsync()
    {
        PacketReturnHandler<TestPacket> handler = new();
        IPacket resultPacket = Substitute.For<IPacket>();
        resultPacket.Header.Returns(new PacketHeader { Flags = PacketFlags.RELIABLE });
        
        IPacketSender sender = Substitute.For<IPacketSender>();
        PacketContext<TestPacket> context = new()
        {
            Sender = sender
        };

        await handler.HandleAsync(resultPacket, context);

        await sender.Received(1).SendAsync(resultPacket);
    }

    [Fact]
    public async Task PacketReturnHandler_Reliable_DisposesPacketAfterSend()
    {
        PacketReturnHandler<TestPacket> handler = new();
        DisposablePacket resultPacket = new()
        {
            Header = new PacketHeader { Flags = PacketFlags.RELIABLE }
        };

        IPacketSender sender = Substitute.For<IPacketSender>();
        sender.SendAsync(resultPacket).Returns(_ =>
        {
            Assert.False(resultPacket.IsDisposed);
            return ValueTask.CompletedTask;
        });

        PacketContext<TestPacket> context = new()
        {
            Sender = sender
        };

        await handler.HandleAsync(resultPacket, context);

        Assert.True(resultPacket.IsDisposed);
    }

    [Fact]
    public void ReturnTypeHandlerFactory_GetHandler_ReturnsCorrectHandlerForVoid()
    {
        IReturnHandler<TestPacket> handler = ReturnTypeHandlerFactory<TestPacket>.ResolveHandler(typeof(void));
        Assert.IsType<VoidReturnHandler<TestPacket>>(handler);
    }

    [Fact]
    public void ReturnTypeHandlerFactory_GetHandler_ReturnsCorrectHandlerForTask()
    {
        IReturnHandler<TestPacket> handler = ReturnTypeHandlerFactory<TestPacket>.ResolveHandler(typeof(Task));
        Assert.IsType<Nalix.Runtime.Internal.Results.Task.TaskVoidReturnHandler<TestPacket>>(handler);
    }

    [Fact]
    public async Task TaskReturnHandler_UnwrappedPacketResult_CallsSenderSendAsync()
    {
        IReturnHandler<TestPacket> handler = ReturnTypeHandlerFactory<TestPacket>.ResolveHandler(typeof(Task<TestPacket>));
        TestPacket resultPacket = new() { Header = new PacketHeader { Flags = PacketFlags.RELIABLE } };
        IPacketSender sender = Substitute.For<IPacketSender>();
        PacketContext<TestPacket> context = new()
        {
            Sender = sender
        };

        await handler.HandleAsync(resultPacket, context);

        await sender.Received(1).SendAsync(resultPacket);
    }

    [Fact]
    public async Task ValueTaskReturnHandler_UnwrappedPacketResult_CallsSenderSendAsync()
    {
        IReturnHandler<TestPacket> handler = ReturnTypeHandlerFactory<TestPacket>.ResolveHandler(typeof(ValueTask<TestPacket>));
        TestPacket resultPacket = new() { Header = new PacketHeader { Flags = PacketFlags.RELIABLE } };
        IPacketSender sender = Substitute.For<IPacketSender>();
        PacketContext<TestPacket> context = new()
        {
            Sender = sender
        };

        await handler.HandleAsync(resultPacket, context);

        await sender.Received(1).SendAsync(resultPacket);
    }

    private sealed class TestPacket : IPacket
    {
        public int Length => 0;
        public PacketHeader Header { get; set; }
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
    }

    private sealed class DisposablePacket : IPacket, IDisposable
    {
        public int Length => 0;
        public PacketHeader Header { get; set; }
        public bool IsDisposed { get; private set; }
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
        public void Dispose() => this.IsDisposed = true;
    }
}
#endif











