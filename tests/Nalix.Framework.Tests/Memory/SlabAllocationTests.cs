// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG

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
/// Tests for the standalone slab-based memory allocation subsystem.
/// </summary>
[Trait("Category", "Memory")]
public sealed class SlabAllocationTests
{
    #region MemorySlab Tests

    [Theory]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    public void MemorySlab_Init_AllocatesCorrectBackingSize(int size)
    {
        MemorySlab slab = new(size);

        Assert.Equal(size, slab.TotalBytes);
        Assert.True(slab.SlabId > 0);
        Assert.True(slab.IsIdle);
    }

    [Fact]
    public void MemorySlab_GetArray_ReturnsCorrectArray()
    {
        const int size = 512;
        MemorySlab slab = new(size);

        byte[] arr = slab.GetArray();
        Assert.NotNull(arr);
        Assert.Equal(size, arr.Length);
    }

    [Fact]
    public void MemorySlab_ActiveStateTracking_UpdatesCorrectly()
    {
        MemorySlab slab = new(128);

        Assert.True(slab.IsIdle);
        
        slab.MarkActive();
        Assert.False(slab.IsIdle);

        slab.MarkIdle();
        Assert.True(slab.IsIdle);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MemorySlab_InvalidParams_Throws(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemorySlab(size));
    }

    #endregion MemorySlab Tests

    #region SlabBucket Tests

    [Fact]
    public void SlabBucket_RentAndReturn_RoundTrip()
    {
        using SlabBucket bucket = new(256, 4);

        byte[] arr = bucket.Rent();
        Assert.NotNull(arr);
        Assert.True(arr.Length >= 256);

        // Write some data to confirm usability
        arr.AsSpan().Fill(42);

        bucket.Return(arr);

        // Should be able to rent it back
        byte[] arr2 = bucket.Rent();
        Assert.NotNull(arr2);
        Assert.True(arr2.Length >= 256);
        bucket.Return(arr2);
    }

    [Fact]
    public void SlabBucket_ExhaustAndGrow_AllocatesNewSlabs()
    {
        using SlabBucket bucket = new(128, 2);

        // Rent more buffers than initially allocated to trigger growth
        List<byte[]> rented = new(10);
        for (int i = 0; i < 10; i++)
        {
            rented.Add(bucket.Rent());
        }

        // All should be valid
        foreach (byte[] arr in rented)
        {
            Assert.NotNull(arr);
            Assert.True(arr.Length >= 128);
        }

        // Return all
        foreach (byte[] arr in rented)
        {
            bucket.Return(arr);
        }

        Assert.True(bucket.GetPoolInfo().TotalBuffers >= 10);
    }

    [Fact]
    public void SlabBucket_TryRent_ReturnsFalseWhenEmpty()
    {
        // Create with 0 initial capacity
        using SlabBucket bucket = new(256, 0);

        // TryRent should return false (Rent would allocate, TryRent should not)
        bool result = bucket.TryRent(out byte[]? array);
        Assert.False(result);
        Assert.Null(array);
    }

    [Fact]
    public void SlabBucket_ReturnWrongSize_SilentlyDropped()
    {
        using SlabBucket bucket256 = new(256, 4);
        using SlabBucket bucket512 = new(512, 4);

        byte[] arr512 = bucket512.Rent();

        // Returning a 512-byte array to a 256-byte bucket should be silently dropped
        bucket256.Return(arr512);

        // The 256 bucket should still have its own segments
        byte[] arr256 = bucket256.Rent();
        Assert.True(arr256.Length >= 256);
        bucket256.Return(arr256);

        bucket512.Return(arr512);
    }

    [Fact]
    public void SlabBucket_IncreaseCapacity_AddsBuffers()
    {
        using SlabBucket bucket = new(256, 4);
        int initialTotal = bucket.GetPoolInfo().TotalBuffers;

        bucket.IncreaseCapacity(8);

        Assert.True(bucket.GetPoolInfo().TotalBuffers >= initialTotal + 8);
    }

    [Fact]
    public void SlabBucket_DecreaseCapacity_RemovesFreeBuffers()
    {
        using SlabBucket bucket = new(256, 16);
        bucket.IncreaseCapacity(8); // Ensure we are above initial capacity
        int totalBeforeShrink = bucket.GetPoolInfo().TotalBuffers;

        bucket.DecreaseCapacity(4);

        Assert.True(bucket.GetPoolInfo().TotalBuffers < totalBeforeShrink);
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
                    byte[] arr = bucket.Rent();
                    if (arr.Length < 256)
                    {
                        Interlocked.Increment(ref errors);
                    }
                    else
                    {
                        // Write to the buffer to detect sharing corruption
                        arr.AsSpan().Fill((byte)(System.Environment.CurrentManagedThreadId & 0xFF));
                    }

                    bucket.Return(arr);
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

        Assert.True(mgr.TryRent(256, out byte[]? arr256));
        Assert.Equal(256, arr256.Length);
        Assert.True(mgr.TryReturn(arr256));

        Assert.True(mgr.TryRent(512, out byte[]? arr512));
        Assert.Equal(512, arr512.Length);
        Assert.True(mgr.TryReturn(arr512));
    }

    [Fact]
    public void SlabPoolManager_BestFitLookup_FindsSmallestSuitableBucket()
    {
        using SlabPoolManager mgr = new();
        mgr.CreateBucket(256, 4);
        mgr.CreateBucket(512, 4);
        mgr.CreateBucket(1024, 4);

        // Requesting 300 bytes should get a 512-byte array (best fit)
        Assert.True(mgr.TryRent(300, out byte[]? arr));
        Assert.Equal(512, arr.Length);
        Assert.True(mgr.TryReturn(arr));

        // Requesting 1 byte should get a 256-byte array
        Assert.True(mgr.TryRent(1, out arr));
        Assert.Equal(256, arr.Length);
        Assert.True(mgr.TryReturn(arr));
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

    #endregion SlabPoolManager Tests

    #region Integration Tests

    [Fact]
    public void BufferLease_Rent_UsesStandaloneSlabWithOffset0()
    {
        BufferLease lease = BufferLease.Rent(256);

        Assert.True(lease.Capacity >= 256);
        
#if DEBUG
        Assert.Equal(0, lease.AsSegment().Offset);
#endif

        // Write and read through the lease Span
        byte[] testData = [1, 2, 3, 4, 5];
        testData.CopyTo(lease.SpanFull);
        lease.CommitLength(5);

        Assert.Equal(5, lease.Length);
        Assert.Equal(testData, lease.Span.ToArray());

        lease.Dispose();
    }

    [Fact]
    public void BufferPoolManager_RentByteArray_ReturnsOffset0()
    {
        using BufferPoolManager manager = new(new BufferConfig
        {
            TotalBuffers = 100,
            FallbackToArrayPool = true
        });

        byte[] arr = manager.Rent(256);
        Assert.NotNull(arr);
        Assert.True(arr.Length >= 256);

        // Standalone arrays always allow writing from 0
        arr.AsSpan(0, 256).Fill(0xCC);

        manager.Return(arr);
    }

    #endregion Integration Tests
}

#endif
