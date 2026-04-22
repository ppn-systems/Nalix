
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

/// <summary>
/// Covers configuration and buffer-lease APIs in the Memory folder.
/// </summary>
[Trait("Category", "Memory")]
public sealed partial class MemoryTests
{
    [Theory]
    [InlineData("1024,0.40;256,0.10;512,0.20", new[] { 256, 512, 1024 }, new[] { 0.10, 0.20, 0.40 })]
    [InlineData("2048,1.0", new[] { 2048 }, new[] { 1.0 })]
    public void ParseBufferAllocations_ValidInput_ReturnsSortedAllocations(
        string value,
        int[] expectedSizes,
        double[] expectedRatios)
    {
        (int size, double ratio)[] allocations = BufferConfig.ParseBufferAllocations(value);

        Assert.Equal(expectedSizes, allocations.Select(static x => x.size).ToArray());
        Assert.Equal(expectedRatios, allocations.Select(static x => x.ratio).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("256,-1")]
    [InlineData("256,0.5;512,0.7")]
    public void ParseBufferAllocations_InvalidInput_ThrowsArgumentException(string value)
        => Assert.Throws<ArgumentException>(() => BufferConfig.ParseBufferAllocations(value));

    [Fact]
    public void Validate_ValidBufferConfig_CompletesSuccessfully()
    {
        BufferConfig config = new()
        {
            TotalBuffers = 64,
            BufferAllocations = "256,0.50;512,0.50",
            ExpandThresholdPercent = 0.20,
            ShrinkThresholdPercent = 0.60,
            AdaptiveGrowthFactor = 2.0,
            MinimumIncrease = 4,
            MaxBufferIncreaseLimit = 16,
            MaxMemoryPercentage = 0.25,
            MaxMemoryBytes = 0
        };

        Exception? exception = Record.Exception(config.Validate);

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0.20, 0.60, "256,1.0", 32, 5.0, 4, 8)]
    [InlineData(0.70, 0.60, "256,1.0", 32, 2.0, 4, 16)]
    [InlineData(0.20, 0.60, "256,0.60;256,0.20", 32, 2.0, 4, 16)]
    public void Validate_InvalidBufferConfig_ThrowsValidationException(
        double expandThreshold,
        double shrinkThreshold,
        string allocations,
        int totalBuffers,
        double growthFactor,
        int minimumIncrease,
        int maxIncreaseLimit)
    {
        BufferConfig config = new()
        {
            ExpandThresholdPercent = expandThreshold,
            ShrinkThresholdPercent = shrinkThreshold,
            BufferAllocations = allocations,
            TotalBuffers = totalBuffers,
            AdaptiveGrowthFactor = growthFactor,
            MinimumIncrease = minimumIncrease,
            MaxBufferIncreaseLimit = maxIncreaseLimit
        };

        _ = Assert.Throws<ValidationException>(config.Validate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CopyFrom_StateUnderTest_ExposesLeaseProperties(bool zeroOnDispose)
    {
        byte[] source = [1, 2, 3, 4];

        using BufferLease lease = BufferLease.CopyFrom(source, zeroOnDispose);

        Assert.Equal(source.Length, lease.Length);
        Assert.True(lease.Capacity >= source.Length);
        Assert.True(lease.RawCapacity >= lease.Capacity);
        Assert.Equal(zeroOnDispose, lease.ZeroOnDispose);
        Assert.Equal(source, lease.Memory.ToArray());
        Assert.Equal(source, lease.Span.ToArray());
        Assert.True(lease.SpanFull.Length >= source.Length);
    }

    [Fact]
    public void ReleaseOwnership_StateUnderTest_ReturnsExpectedOutcome()
    {
        using BufferLease sharedLease = BufferLease.CopyFrom([9, 8, 7]);
        using BufferLease ownedLease = BufferLease.TakeOwnership([1, 2, 3, 4], 1, 2);
        sharedLease.Retain();

        bool sharedReleased = sharedLease.ReleaseOwnership(out byte[]? sharedBuffer, out int sharedStart, out int sharedLength);
        sharedLease.Dispose();
        bool ownedReleased = ownedLease.ReleaseOwnership(out byte[]? ownedBuffer, out int ownedStart, out int ownedLength);

        Assert.False(sharedReleased);
        Assert.Null(sharedBuffer);
        Assert.Equal(0, sharedStart);
        Assert.Equal(0, sharedLength);

        Assert.True(ownedReleased);
        Assert.NotNull(ownedBuffer);
        Assert.Equal(1, ownedStart);
        Assert.Equal(2, ownedLength);
        Assert.Equal(0, ownedLease.Length);
        Assert.Equal(0, ownedLease.Capacity);
    }

    [Fact]
    public void CommitLength_LengthExceedsCapacity_ThrowsArgumentOutOfRangeException()
    {
        using BufferLease lease = BufferLease.Rent(8);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => lease.CommitLength(lease.Capacity + 1));
    }

    [Fact]
    public void TakeOwnership_SliceExceedsBufferBounds_ThrowsArgumentOutOfRangeException()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BufferLease.TakeOwnership([1, 2, 3, 4], 2, 3));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FromRented_StateUnderTest_PreservesPayload(bool zeroOnDispose)
    {
        byte[] wholeBuffer = BufferLease.ByteArrayPool.Rent(4);
        byte[] sliceBuffer = BufferLease.ByteArrayPool.Rent(4);
        byte[] source = [10, 20, 30, 40];
        source.CopyTo(wholeBuffer, 0);
        source.CopyTo(sliceBuffer, 0);

        using BufferLease wholeLease = BufferLease.FromRented(wholeBuffer, 3, zeroOnDispose);
        using BufferLease sliceLease = BufferLease.TakeOwnership(sliceBuffer, 1, 2, zeroOnDispose);

        Assert.Equal([10, 20, 30], wholeLease.Memory.ToArray());
        Assert.Equal([20, 30], sliceLease.Memory.ToArray());
        Assert.Equal(zeroOnDispose, wholeLease.ZeroOnDispose);
        Assert.Equal(zeroOnDispose, sliceLease.ZeroOnDispose);
    }

#if DEBUG
    [Fact]
    public void AsSegment_LeaseContainsData_ReturnsMatchingSegment()
    {
        byte[] buffer = BufferLease.ByteArrayPool.Rent(4);
        byte[] source = [4, 5, 6, 7];
        source.CopyTo(buffer, 0);
        using BufferLease lease = BufferLease.TakeOwnership(buffer, 1, 2);

        ArraySegment<byte> segment = lease.AsSegment();

        Assert.Equal(1, segment.Offset);
        Assert.Equal(2, segment.Count);
        Assert.Equal([5, 6], segment.ToArray());
    }
#endif
}
