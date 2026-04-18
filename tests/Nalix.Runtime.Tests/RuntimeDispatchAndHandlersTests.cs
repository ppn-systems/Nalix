using Nalix.Common.Networking.Protocols;
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Network.Routing;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Extensions;
using Nalix.Runtime.Handlers;
using Xunit;

namespace Nalix.Runtime.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class RuntimeDispatchAndHandlersTests
{
    private static readonly FieldInfo s_providersField =
        typeof(PacketMetadataProviders).GetField("s_providers", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("PacketMetadataProviders.s_providers field not found.");

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void PacketDispatchOptionsWithDispatchLoopCount_WhenOutOfRange_ThrowsArgumentOutOfRangeException(int value)
    {
        PacketDispatchOptions<TestPacket> options = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => options.WithDispatchLoopCount(value));
    }

    [Fact]
    public void PacketDispatchOptionsWithDispatchLoopCount_WhenValueIsNullOrValid_SetsProperty()
    {
        PacketDispatchOptions<TestPacket> options = new();

        _ = options.WithDispatchLoopCount(null);
        Assert.Null(options.DispatchLoopCount);

        _ = options.WithDispatchLoopCount(8);
        Assert.Equal(8, options.DispatchLoopCount);
    }

    [Fact]
    public void PacketDispatchOptionsWithMiddleware_WhenMiddlewareIsNull_ThrowsArgumentNullException()
    {
        PacketDispatchOptions<TestPacket> options = new();

        _ = Assert.Throws<ArgumentNullException>(() => options.WithMiddleware(null!));
    }

    [Fact]
    public void PacketDispatchOptionsWithHandler_WhenControllerMissingPacketControllerAttribute_ThrowsInternalErrorException()
    {
        PacketDispatchOptions<TestPacket> options = new();

        InternalErrorException ex = Assert.Throws<InternalErrorException>(() => options.WithHandler<MissingControllerAttributeController>());

        Assert.Contains("missing the [PacketController] attribute", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PacketDispatchOptionsWithHandler_WhenControllerAttributeMissingAndInstanceIsNull_ThrowsInternalErrorException()
    {
        PacketDispatchOptions<TestPacket> options = new();
        MissingControllerAttributeController? instance = null;

        _ = Assert.Throws<InternalErrorException>(() => options.WithHandler(instance!));
    }

    [Fact]
    public void PacketMetadataProvidersRegister_WhenCalled_AppendsProviderInOrder()
    {
        IList providers = (IList)s_providersField.GetValue(null)!;
        int originalCount = providers.Count;
        try
        {
            TestMetadataProvider a = new();
            TestMetadataProvider b = new();

            PacketMetadataProviders.Register(a);
            PacketMetadataProviders.Register(b);

            Assert.Equal(originalCount + 2, providers.Count);
            Assert.Same(a, providers[originalCount]);
            Assert.Same(b, providers[originalCount + 1]);
        }
        finally
        {
            while (providers.Count > originalCount)
            {
                providers.RemoveAt(providers.Count - 1);
            }
        }
    }

    [Fact]
    public void PacketContextDefaultsAndReturn_WhenCalledMultipleTimes_RemainsSafe()
    {
        PacketContext<TestPacket> context = new();

        Assert.False(context.IsReliable);
        Assert.False(context.SkipOutbound);

        context.Return();
        context.Return();
        context.ResetForPool();
    }

    [Fact]
    public async Task ConnectionExtensionsSendAsync_WhenConnectionIsNull_ThrowsArgumentNullException()
    {
        IConnection? connection = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.SendAsync(
                controlType: ControlType.PING,
                reason: ProtocolReason.NONE,
                action: ProtocolAdvice.NONE,
                options: default));
    }

    [Fact]
    public async Task HandshakeHandlersHandleAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await HandshakeHandlers.HandleAsync(null!).AsTask());
    }

    [Fact]
    public async Task SessionHandlersHandleAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await SessionHandlers.HandleAsync(null!).AsTask());
    }

    [Fact]
    public async Task SystemControlHandlersHandleAsync_WhenContextIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await SystemControlHandlers.HandleAsync(null!).AsTask());
    }

    private sealed class MissingControllerAttributeController
    {
        [PacketOpcode(1)]
        public static ValueTask HandleAsync(IPacketContext<TestPacket> _) => ValueTask.CompletedTask;
    }

    private sealed class TestMetadataProvider : IPacketMetadataProvider
    {
        public void Populate(MethodInfo method, PacketMetadataBuilder builder)
        {
        }
    }

    private sealed class TestPacket : IPacket
    {
        public int Length => 0;
        public uint MagicNumber { get; set; }
        public ushort OpCode { get; set; }
        public PacketFlags Flags { get; set; }
        public PacketPriority Priority { get; set; }
        public ushort SequenceId { get; } = 1;
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
    }
}

