#if DEBUG
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Nalix.Abstractions;
using Nalix.Abstractions.Networking;
using Nalix.Network.Internal.Pooling;
using Xunit;

namespace Nalix.Network.Tests;

/// <summary>
/// Regression tests for <see cref="PooledConnectEventContext"/> pool reset behavior.
///
/// Bug: ResetForPool() did not clear LocalOwner, causing returned contexts
/// in the global pool to retain a reference to the disposed Connection
/// (via IPooledConnectContextPool), preventing GC until the context was
/// rented again.
/// </summary>
[SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "xUnit tests intentionally follow the test synchronization context.")]
public sealed class PooledConnectEventContextTests
{
    [Fact]
    public void ResetForPool_ClearsLocalOwner()
    {
        // Arrange
        PooledConnectEventContext context = new();

        // Simulate a connection acquiring and initializing the context
        var fakeOwner = new FakeConnectContextPool();
        context.LocalOwner = fakeOwner;
        context.Initialize(
            static (_, _) => { },
            sender: new object(),
            args: new FakeConnectEventArgs(),
            releasePendingPacketOnCompletion: false);

        // Act
        context.ResetForPool();

        // Assert: LocalOwner must be cleared to prevent retaining Connection reference
        _ = context.LocalOwner.Should().BeNull(
            "ResetForPool must clear LocalOwner to allow GC of the owning Connection");

        // Other fields must also be cleared
        _ = context.Sender.Should().BeNull();
        _ = context.Callback.Should().BeNull();
        _ = context.Args.Should().BeNull();
    }

    [Fact]
    public void ResetForPool_ClearsAllFields_AfterFullLifecycle()
    {
        // Arrange
        PooledConnectEventContext context = new();

        var fakeOwner = new FakeConnectContextPool();
        context.LocalOwner = fakeOwner;
        context.ReleasePendingPacketOnCompletion = true;

        context.Initialize(
            static (_, _) => { },
            sender: new object(),
            args: new FakeConnectEventArgs(),
            releasePendingPacketOnCompletion: true);

        // Act
        context.ResetForPool();

        // Assert: all references cleared
        _ = context.LocalOwner.Should().BeNull();
        _ = context.Sender.Should().BeNull();
        _ = context.Callback.Should().BeNull();
        _ = context.Args.Should().BeNull();
        _ = context.ReleasePendingPacketOnCompletion.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ReturnsToOwnerPool_WhenLocalOwnerSet()
    {
        // Arrange
        PooledConnectEventContext context = new();
        var fakeOwner = new FakeConnectContextPool();
        context.LocalOwner = fakeOwner;
        context.Initialize(
            static (_, _) => { },
            sender: new object(),
            args: new FakeConnectEventArgs());

        // Act
        context.Dispose();

        // Assert: returned to owner's pool, not global pool
        _ = fakeOwner.ReturnContextCalled.Should().BeTrue();
    }

    #region Fakes

    private sealed class FakeConnectContextPool : IPooledConnectContextPool
    {
        public bool ReturnContextCalled { get; private set; }

        public PooledConnectEventContext AcquireContext() => new();

        public void ReturnContext(PooledConnectEventContext context)
        {
            ReturnContextCalled = true;
            context.ResetForPool();
        }

        public void ReleasePendingPacket() { }
    }

    private sealed class FakeConnectEventArgs : IConnectionEventArgs
    {
        public IConnection Connection => null!;
        public INetworkEndpoint? NetworkEndpoint => null;
        public IBufferLease? Lease => null;
        public void Dispose() { }
    }

    #endregion
}
#endif
