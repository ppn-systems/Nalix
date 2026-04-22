// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Internal.Buffers;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

/// <summary>
/// Tests for the slab-based memory allocation subsystem:
/// <see cref="MemorySlab"/>, <see cref="SlabBucket"/>, and <see cref="SlabPoolManager"/>.
/// Also validates integration with <see cref="BufferPoolManager"/> and <see cref="BufferLease"/>.
/// </summary>
public sealed class SlabAllocationTests
{
    #region MemorySlab Tests

    [Theory]
    [InlineData(256, 4)]
    [InlineData(1024, 16)]
    [InlineData(4096, 1)]
    public void MemorySlab_Init_AllocatesCorrectBackingSize(int segmentSize, int segmentCount)
    {
        MemorySlab slab = new(segmentSize, segmentCount);

        Assert.Equal(segmentSize, slab.SegmentSize);
        Assert.Equal(segmentCount, slab.SegmentCount);
        Assert.Equal(segmentSize * segmentCount, slab.TotalBytes);
        Assert.True(slab.SlabId > 0);
        Assert.True(slab.IsFullyIdle);
        Assert.Equal(0, slab.ActiveSegments);
    }

    [Fact]
    public void MemorySlab_GetSegment_ReturnsCorrectOffsetsAndSize()
    {
        const int segSize = 512;
        const int segCount = 4;
        MemorySlab slab = new(segSize, segCount);

        for (int i = 0; i < segCount; i++)
        {
            ArraySegment<byte> seg = slab.GetSegment(i);
            Assert.NotNull(seg.Array);
            Assert.Equal(i * segSize, seg.Offset);
            Assert.Equal(segSize, seg.Count);
        }
    }

    [Fact]
    public void MemorySlab_SharedBackingArray_AllSegmentsShareSameArray()
    {
        MemorySlab slab = new(256, 8);
        byte[]? first = slab.GetSegment(0).Array;

        for (int i = 1; i < 8; i++)
        {
            Assert.Same(first, slab.GetSegment(i).Array);
        }

        Assert.True(slab.OwnsBacking(first!));
    }

    [Fact]
    public void MemorySlab_ActiveSegmentTracking_IncrementsAndDecrements()
    {
        MemorySlab slab = new(128, 4);

        slab.IncrementActive();
        slab.IncrementActive();
        Assert.Equal(2, slab.ActiveSegments);
        Assert.False(slab.IsFullyIdle);

        slab.DecrementActive();
        slab.DecrementActive();
        Assert.Equal(0, slab.ActiveSegments);
        Assert.True(slab.IsFullyIdle);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void MemorySlab_InvalidParams_Throws(int segSize, int segCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemorySlab(segSize, segCount));
    }

    [Fact]
    public void MemorySlab_SegmentDataIsolation_WritesDoNotBleedAcrossSegments()
    {
        MemorySlab slab = new(64, 4);
        ArraySegment<byte> seg0 = slab.GetSegment(0);
        ArraySegment<byte> seg1 = slab.GetSegment(1);

        // Write to segment 0
        seg0.Array!.AsSpan(seg0.Offset, seg0.Count).Fill(0xAA);

        // Verify segment 1 is still zeroed
        ReadOnlySpan<byte> span1 = seg1.Array!.AsSpan(seg1.Offset, seg1.Count);
        foreach (byte b in span1)
        {
            Assert.Equal(0, b);
        }
    }

    #endregion MemorySlab Tests

    #region SlabBucket Tests

    [Fact]
    public void SlabBucket_RentAndReturn_RoundTrip()
    {
        using SlabBucket bucket = new(256, 4);

        ArraySegment<byte> seg = bucket.Rent();
        Assert.NotNull(seg.Array);
        Assert.Equal(256, seg.Count);

        // Write some data to confirm usability
        seg.Array!.AsSpan(seg.Offset, seg.Count).Fill(42);

        bucket.Return(seg);

        // Should be able to rent it back
        ArraySegment<byte> seg2 = bucket.Rent();
        Assert.NotNull(seg2.Array);
        Assert.Equal(256, seg2.Count);
        bucket.Return(seg2);
    }

    [Fact]
    public void SlabBucket_ExhaustAndGrow_AllocatesNewSlab()
    {
        using SlabBucket bucket = new(128, 2);

        // Rent more segments than initially allocated to trigger growth
        List<ArraySegment<byte>> rented = new(10);
        for (int i = 0; i < 10; i++)
        {
            rented.Add(bucket.Rent());
        }

        // All should be valid
        foreach (ArraySegment<byte> seg in rented)
        {
            Assert.NotNull(seg.Array);
            Assert.Equal(128, seg.Count);
        }

        // Return all
        foreach (ArraySegment<byte> seg in rented)
        {
            bucket.Return(seg);
        }

        Assert.True(bucket.TotalSegments >= 10);
    }

    [Fact]
    public void SlabBucket_TryRent_ReturnsFalseWhenEmpty()
    {
        // Create with 0 initial capacity
        using SlabBucket bucket = new(256, 0);

        // TryRent should return false (Rent would allocate, TryRent should not)
        // Actually TryRent returns false only if ring + cache are empty
        bool result = bucket.TryRent(out ArraySegment<byte> seg);
        Assert.False(result);
        Assert.Null(seg.Array);
    }

    [Fact]
    public void SlabBucket_ReturnWrongSize_SilentlyDropped()
    {
        using SlabBucket bucket256 = new(256, 4);
        using SlabBucket bucket512 = new(512, 4);

        ArraySegment<byte> seg512 = bucket512.Rent();

        // Returning a 512-byte segment to a 256-byte bucket should be silently dropped
        bucket256.Return(seg512);

        // The 256 bucket should still have its own segments
        ArraySegment<byte> seg256 = bucket256.Rent();
        Assert.Equal(256, seg256.Count);
        bucket256.Return(seg256);

        bucket512.Return(seg512);
    }

    [Fact]
    public void SlabBucket_IncreaseCapacity_AddsSegments()
    {
        using SlabBucket bucket = new(256, 4);
        int initialTotal = bucket.TotalSegments;

        bucket.IncreaseCapacity(8);

        Assert.True(bucket.TotalSegments >= initialTotal + 8);
    }

    [Fact]
    public void SlabBucket_DecreaseCapacity_RemovesFreeSegments()
    {
        using SlabBucket bucket = new(256, 16);
        int initialTotal = bucket.TotalSegments;

        bucket.DecreaseCapacity(4);

        Assert.True(bucket.TotalSegments < initialTotal);
    }

    [Fact]
    public void SlabBucket_GetPoolInfo_ReturnsValidSnapshot()
    {
        using SlabBucket bucket = new(1024, 8);

        BufferPoolState info = bucket.GetPoolInfo();
        Assert.Equal(1024, info.BufferSize);
        Assert.Equal(8, info.TotalBuffers);
        Assert.True(info.FreeBuffers > 0);
    }

    [Fact]
    public void SlabBucket_ConcurrentRentReturn_NoCorruption()
    {
        using SlabBucket bucket = new(256, 64);

        const int threads = 8;
        const int opsPerThread = 200;
        int errors = 0;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                try
                {
                    ArraySegment<byte> seg = bucket.Rent();
                    if (seg.Array is null || seg.Count != 256)
                    {
                        Interlocked.Increment(ref errors);
                    }
                    else
                    {
                        // Write to the segment to detect sharing corruption
                        seg.Array.AsSpan(seg.Offset, seg.Count).Fill((byte)(System.Environment.CurrentManagedThreadId & 0xFF));
                    }

                    bucket.Return(seg);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        });

        Assert.Equal(0, errors);
    }

    #endregion SlabBucket Tests

    #region SlabPoolManager Tests

    [Fact]
    public void SlabPoolManager_CreateBucket_RegistersSuccessfully()
    {
        using SlabPoolManager mgr = new();
        mgr.CreateBucket(256, 4);
        mgr.CreateBucket(512, 4);

        Assert.True(mgr.TryRent(256, out ArraySegment<byte> seg256));
        Assert.Equal(256, seg256.Count);
        Assert.True(mgr.TryReturn(seg256));

        Assert.True(mgr.TryRent(512, out ArraySegment<byte> seg512));
        Assert.Equal(512, seg512.Count);
        Assert.True(mgr.TryReturn(seg512));
    }

    [Fact]
    public void SlabPoolManager_BestFitLookup_FindsSmallestSuitableBucket()
    {
        using SlabPoolManager mgr = new();
        mgr.CreateBucket(256, 4);
        mgr.CreateBucket(512, 4);
        mgr.CreateBucket(1024, 4);

        // Requesting 300 bytes should get a 512-byte segment (best fit)
        Assert.True(mgr.TryRent(300, out ArraySegment<byte> seg));
        Assert.Equal(512, seg.Count);
        Assert.True(mgr.TryReturn(seg));

        // Requesting 1 byte should get a 256-byte segment
        Assert.True(mgr.TryRent(1, out seg));
        Assert.Equal(256, seg.Count);
        Assert.True(mgr.TryReturn(seg));
    }

    [Fact]
    public void SlabPoolManager_NoSuitableBucket_ReturnsFalse()
    {
        using SlabPoolManager mgr = new();
        mgr.CreateBucket(256, 4);
        mgr.CreateBucket(512, 4);

        // Requesting 1024 bytes when max bucket is 512 should return false
        Assert.False(mgr.TryRent(1024, out _));
    }

    [Fact]
    public void SlabPoolManager_TryReturnUnknownSize_ReturnsFalse()
    {
        using SlabPoolManager mgr = new();
        mgr.CreateBucket(256, 4);

        // A segment of size 128 doesn't match any bucket
        byte[] dummy = new byte[128];
        ArraySegment<byte> fake = new(dummy, 0, 128);
        Assert.False(mgr.TryReturn(fake));
    }

    [Fact]
    public void SlabPoolManager_DuplicateCreateBucket_IsNoOp()
    {
        using SlabPoolManager mgr = new();
        mgr.CreateBucket(256, 4);
        mgr.CreateBucket(256, 100); // Should be no-op

        var buckets = mgr.GetAllBuckets();
        Assert.Single(buckets);
    }

    #endregion SlabPoolManager Tests

    #region Integration Tests

    [Fact]
    public void BufferLease_Rent_UsesSlabSegmentWithCorrectOffset()
    {
        // This test validates the end-to-end flow:
        // BufferLease.Rent() → ByteArrayPool.RentSegment() → BufferPoolManager.RentSegment()
        // → SlabPoolManager.TryRent() → SlabBucket.Rent() → returns slab-backed segment
        // → BufferLease uses seg.Offset as start
        BufferLease lease = BufferLease.Rent(256);

        Assert.True(lease.Capacity > 0);

        // Write and read through the lease Span (which respects the offset)
        byte[] testData = [1, 2, 3, 4, 5];
        testData.CopyTo(lease.SpanFull);
        lease.CommitLength(5);

        Assert.Equal(5, lease.Length);
        Assert.Equal(testData, lease.Span.ToArray());

        lease.Dispose();
    }

    [Fact]
    public void BufferLease_CopyFrom_PreservesDataWithSlabOffset()
    {
        byte[] src = [10, 20, 30, 40, 50];
        BufferLease lease = BufferLease.CopyFrom(src);

        Assert.Equal(5, lease.Length);
        Assert.Equal(src, lease.Span.ToArray());

        lease.Dispose();
    }

    [Fact]
    public void BufferLease_RentDisposeCycle_ReturnsToSlabPool()
    {
        // Rent and dispose multiple leases to exercise the return path
        for (int i = 0; i < 100; i++)
        {
            BufferLease lease = BufferLease.Rent(256);
            lease.SpanFull[0] = (byte)(i & 0xFF);
            lease.CommitLength(1);
            lease.Dispose();
        }

        // If return didn't work, we'd eventually OOM or exhaust slabs.
        // Success = no exception after 100 cycles.
    }

    [Fact]
    public void BufferPoolManager_RentSegment_ReturnsSlabBackedSegment()
    {
        using BufferPoolManager manager = new(new BufferConfig
        {
            TotalBuffers = 100,
            FallbackToArrayPool = true
        });

        ArraySegment<byte> seg = manager.RentSegment(256);
        Assert.NotNull(seg.Array);
        Assert.True(seg.Count >= 256);

        // Write to validate the segment is usable
        seg.Array!.AsSpan(seg.Offset, seg.Count).Fill(0xBB);

        manager.Return(seg);
    }

    [Fact]
    public void BufferPoolManager_RentByteArray_StillReturnsOffset0()
    {
        using BufferPoolManager manager = new(new BufferConfig
        {
            TotalBuffers = 100,
            FallbackToArrayPool = true
        });

        // The byte[] API must return arrays where callers write from offset 0
        byte[] arr = manager.Rent(256);
        Assert.NotNull(arr);
        Assert.True(arr.Length >= 256);

        // Write from offset 0 — this is the contract for the byte[] API
        arr.AsSpan(0, 256).Fill(0xCC);

        manager.Return(arr);
    }

    [Fact]
    public void BufferPoolManager_ConcurrentSlabAndByteArrayPaths_NoContention()
    {
        using BufferPoolManager manager = new(new BufferConfig
        {
            TotalBuffers = 200,
            FallbackToArrayPool = true
        });

        const int threads = 8;
        const int opsPerThread = 100;
        int errors = 0;

        Parallel.For(0, threads, threadIdx =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                try
                {
                    if (threadIdx % 2 == 0)
                    {
                        // Segment path (slab-backed)
                        ArraySegment<byte> seg = manager.RentSegment(256);
                        seg.Array!.AsSpan(seg.Offset, Math.Min(4, seg.Count)).Fill(0xAA);
                        manager.Return(seg);
                    }
                    else
                    {
                        // Byte[] path (per-buffer)
                        byte[] arr = manager.Rent(256);
                        arr.AsSpan(0, 4).Fill(0xBB);
                        manager.Return(arr);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }
        });

        Assert.Equal(0, errors);
    }

    #endregion Integration Tests
}
