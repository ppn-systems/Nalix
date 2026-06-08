using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Networking.Sessions;
using Nalix.Abstractions.Primitives;
using Nalix.Runtime.Dispatching;
using Nalix.Runtime.Routing;
using Nalix.Runtime.Extensions;
using Nalix.Runtime.Handlers;
using Xunit;

namespace Nalix.Runtime.Tests;

[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class RuntimeDispatchAndHandlersTests
{


    static RuntimeDispatchAndHandlersTests()
    {
        string tempFolderName = "NalixTests_" + Guid.NewGuid().ToString("N");
        if (System.IO.Path.IsPathRooted(tempFolderName))
        {
            throw new InvalidOperationException("Temporary test folder name must be relative.");
        }

        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), tempFolderName);
        Nalix.Environment.IO.Directories.SetBasePathOverride(tempPath);

        string configDir = Nalix.Environment.IO.Directories.ConfigurationDirectory;
        System.IO.Directory.CreateDirectory(configDir);

        string certPath = System.IO.Path.Combine(configDir, "certificate.private");
        System.IO.File.WriteAllText(certPath, "0000000000000000000000000000000000000000000000000000000000000000");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void PacketDispatchOptionsWithDispatchLoopCount_WhenOutOfRange_ThrowsArgumentOutOfRangeException(int value)
    {
        PacketDispatchOptions<TestPacket> options = new();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => { options.WithDispatchLoopCount(value); });
    }

    [Fact]
    public void PacketDispatchOptionsWithDispatchLoopCount_WhenValueIsNullOrValid_SetsProperty()
    {
        PacketDispatchOptions<TestPacket> options = new();

        _ = options.WithDispatchLoopCount(null);
        Assert.Equal(0, options.Drain.Count);

        _ = options.WithDispatchLoopCount(8);
        Assert.Equal(8, options.Drain.Count);
    }

    [Fact]
    public void PacketDispatchOptionsWithMiddleware_WhenMiddlewareIsNull_ThrowsArgumentNullException()
    {
        PacketDispatchOptions<TestPacket> options = new();

        _ = Assert.Throws<ArgumentNullException>(() => { options.WithMiddleware(null!); });
    }





#if DEBUG
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
#endif

    [Fact]
    public async Task ConnectionExtensionsSendAsync_WhenSenderIsNull_ThrowsArgumentNullException()
    {
        IPacketSender? sender = null;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await sender!.SendAsync(
                controlType: ControlType.PING,
                reason: ProtocolReason.NONE,
                action: ProtocolAdvice.NONE,
                options: default));
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





    private sealed class FakeSessionService : ISessionService
    {
        public System.Threading.Tasks.ValueTask SaveSessionAsync(IConnection connection, System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.ValueTask.CompletedTask;

        public System.Threading.Tasks.ValueTask<SessionEntry?> ConsumeAsync(ulong sessionToken, System.Threading.CancellationToken cancellationToken = default)
            => new((SessionEntry?)null);

        public void Dispose() { }
    }

    private sealed class TestPacket : IPacket
    {
        public int Length => 0;
        public PacketHeader Header { get; set; }
        public byte[] Serialize() => [];
        public int Serialize(Span<byte> buffer) => 0;
    }
}















