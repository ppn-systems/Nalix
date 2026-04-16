using System;
using System.Net.Sockets;
using Nalix.Framework.Memory.Buffers;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

public sealed partial class MemoryTests
{
    [Fact]
    public void RetainWhenLeaseIsDisposedThrowsObjectDisposedException()
    {
        BufferLease lease = BufferLease.CopyFrom([1, 2, 3]);
        lease.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => lease.Retain());
    }

    [Fact]
    public void ReleaseOwnershipWhenCalledTwiceReturnsFalseOnSecondCall()
    {
        using BufferLease lease = BufferLease.CopyFrom([9, 8, 7, 6]);

        bool first = lease.ReleaseOwnership(out byte[]? detached, out int start, out int length);
        bool second = lease.ReleaseOwnership(out _, out _, out _);

        Assert.True(first);
        Assert.NotNull(detached);
        Assert.Equal(0, start);
        Assert.Equal(4, length);
        Assert.False(second);
    }

    [Fact]
    public void CommitLengthWhenSetToCapacityUsesFullSlice()
    {
        using BufferLease lease = BufferLease.Rent(32);
        lease.CommitLength(lease.Capacity);

        Assert.Equal(lease.Capacity, lease.Length);
        Assert.Equal(lease.Capacity, lease.Span.Length);
    }

    [Fact]
    public void FromRentedWhenBufferIsNullThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => BufferLease.FromRented(null!, 0));
    }

    [Fact]
    public void ReturnWithNullArrayDoesNotThrow()
    {
        using BufferPoolManager manager = new(MemoryTestSupport.CreateBufferConfig(enableMemoryTrimming: false));

        Exception? ex = Record.Exception(() => manager.Return((byte[]?)null));

        Assert.Null(ex);
    }

    [Fact]
    public void GetAllocationForSizeWhenOutsideBoundsReturnsBoundaryAllocation()
    {
        using BufferPoolManager manager = new(MemoryTestSupport.CreateBufferConfig(enableMemoryTrimming: false));

        double low = manager.GetAllocationForSize(manager.MinBufferSize - 1);
        double exactMin = manager.GetAllocationForSize(manager.MinBufferSize);
        double high = manager.GetAllocationForSize(manager.MaxBufferSize + 1024);

        Assert.Equal(exactMin, low, 10);
        Assert.Equal(0.25, high, 3);
    }

    [Fact]
    public void ReturnFromSaeaWhenNoBufferAttachedIsSafeAndClearsSegment()
    {
        using BufferPoolManager manager = new(MemoryTestSupport.CreateBufferConfig(enableMemoryTrimming: false));
        using SocketAsyncEventArgs saea = new();
        saea.SetBuffer(null, 0, 0);

        Exception? ex = Record.Exception(() => manager.ReturnFromSaea(saea));

        Assert.Null(ex);
        Assert.Null(saea.Buffer);
        Assert.Equal(0, saea.Count);
    }

    [Fact]
    public void DisposeCanBeCalledTwiceWithoutThrowing()
    {
        BufferPoolManager manager = new(MemoryTestSupport.CreateBufferConfig(enableMemoryTrimming: false));

        Exception? ex = Record.Exception(() =>
        {
            manager.Dispose();
            manager.Dispose();
        });

        Assert.Null(ex);
    }
}
