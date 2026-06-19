// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions.Validation;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Memory.Internal.Buffers;
using Nalix.Framework.Options;
using Xunit;

namespace Nalix.Framework.Tests.Memory;

[Collection("ReturnValidation")]
public sealed class ReturnValidationTests
{
    #region ReturnValidation.Disabled

    [Fact]
    public void ReturnValidation_Disabled_RentReturn_Works()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.Disabled);

        byte[] arr = bucket.Rent();
        Assert.NotNull(arr);
        Assert.Equal(256, arr.Length);

        bucket.Return(arr);

        // Should be able to rent again
        byte[] arr2 = bucket.Rent();
        Assert.NotNull(arr2);
        bucket.Return(arr2);
    }

    [Fact]
    public void ReturnValidation_Disabled_DoubleReturn_DoesNotThrow()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.Disabled);

        byte[] arr = bucket.Rent();
        bucket.Return(arr);

        // Double return should not throw (no tracking)
        Exception? ex = Record.Exception(() => bucket.Return(arr));
        Assert.Null(ex);
    }

    [Fact]
    public void ReturnValidation_Disabled_NoRentedAddressDictionaryCreated()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.Disabled);

        // Verify _rentedAddresses is null via reflection
        var field = typeof(SlabBucket).GetField("_rentedAddresses",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(field);
        Assert.Null(field.GetValue(bucket));
    }

    [Fact]
    public void ReturnValidation_Disabled_ConcurrentRentReturn_NoCorruption()
    {
        using SlabBucket bucket = new(256, 64, returnValidation: ReturnValidation.Disabled);

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
                        arr.AsSpan().Fill((byte)(Thread.CurrentThread.ManagedThreadId & 0xFF));
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

    #endregion

    #region ReturnValidation.SilentDrop

    [Fact]
    public void ReturnValidation_SilentDrop_RentReturn_Works()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.SilentDrop);

        byte[] arr = bucket.Rent();
        Assert.NotNull(arr);
        Assert.Equal(256, arr.Length);

        bucket.Return(arr);

        byte[] arr2 = bucket.Rent();
        Assert.NotNull(arr2);
        bucket.Return(arr2);
    }

    [Fact]
    public void ReturnValidation_SilentDrop_DoubleReturn_IsIgnored()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.SilentDrop);

        byte[] arr = bucket.Rent();

        // Rent state: 1 rented, 3 free
        Assert.Equal(3, bucket.GetPoolInfo().FreeBuffers);

        bucket.Return(arr);

        // After first return: 0 rented, 4 free
        Assert.Equal(4, bucket.GetPoolInfo().FreeBuffers);

        // Double return should be silently ignored
        Exception? ex = Record.Exception(() => bucket.Return(arr));
        Assert.Null(ex);

        // FreeBuffers should not exceed TotalBuffers
        BufferPoolState info = bucket.GetPoolInfo();
        Assert.Equal(4, info.TotalBuffers);
        Assert.Equal(4, info.FreeBuffers);
    }

    [Fact]
    public void ReturnValidation_SilentDrop_RentedAddressDictionaryCreated()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.SilentDrop);

        var field = typeof(SlabBucket).GetField("_rentedAddresses",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(field);
        Assert.NotNull(field.GetValue(bucket));
    }

    [Fact]
    public void ReturnValidation_SilentDrop_ConcurrentRentReturn_NoCorruption()
    {
        using SlabBucket bucket = new(256, 64, returnValidation: ReturnValidation.SilentDrop);

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
                        arr.AsSpan().Fill((byte)(Thread.CurrentThread.ManagedThreadId & 0xFF));
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

    #endregion

    #region ReturnValidation.ThrowOnError

    [Fact]
    public void ReturnValidation_ThrowOnError_RentReturn_Works()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.ThrowOnError);

        byte[] arr = bucket.Rent();
        Assert.NotNull(arr);
        Assert.Equal(256, arr.Length);

        bucket.Return(arr);

        byte[] arr2 = bucket.Rent();
        Assert.NotNull(arr2);
        bucket.Return(arr2);
    }

    [Fact]
    public void ReturnValidation_ThrowOnError_DoubleReturn_ThrowsInvalidOperationException()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.ThrowOnError);

        byte[] arr = bucket.Rent();
        bucket.Return(arr);

        // Double return should throw
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => bucket.Return(arr));
        Assert.Contains("double-return", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReturnValidation_ThrowOnError_ReturnWrongArray_DoesNotCorruptPool()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.ThrowOnError);

        byte[] arr = bucket.Rent();
        bucket.Return(arr);

        // Create a fake array of the right size but wrong address
        // (simulated by a separate allocation)
        byte[] fakeArray = new byte[256];

        // Return of a non-owned array should be silently dropped (GC generation < 2 check)
        // or by IsOwnedAddress check
        Exception? ex = Record.Exception(() => bucket.Return(fakeArray));
        Assert.Null(ex); // Should not throw — just silently dropped

        // Pool should be fine
        BufferPoolState info = bucket.GetPoolInfo();
        Assert.Equal(4, info.TotalBuffers);
        Assert.Equal(4, info.FreeBuffers);
    }

    [Fact]
    public void ReturnValidation_ThrowOnError_RentedAddressDictionaryCreated()
    {
        using SlabBucket bucket = new(256, 4, returnValidation: ReturnValidation.ThrowOnError);

        var field = typeof(SlabBucket).GetField("_rentedAddresses",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(field);
        Assert.NotNull(field.GetValue(bucket));
    }

    [Fact]
    public void ReturnValidation_ThrowOnError_ConcurrentRentReturn_NoUnhandledExceptions()
    {
        using SlabBucket bucket = new(256, 64, returnValidation: ReturnValidation.ThrowOnError);

        const int threads = 8;
        const int opsPerThread = 200;
        int errors = 0;

        Parallel.For(0, threads, _ =>
        {
            var rented = new List<byte[]>();
            for (int i = 0; i < opsPerThread; i++)
            {
                try
                {
                    byte[] arr = bucket.Rent();
                    if (arr.Length < 256)
                    {
                        Interlocked.Increment(ref errors);
                    }
                    rented.Add(arr);

                    // Periodically return
                    if (rented.Count > 2)
                    {
                        bucket.Return(rented[0]);
                        rented.RemoveAt(0);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            }

            // Return remaining
            foreach (byte[] arr in rented)
            {
                try { bucket.Return(arr); }
                catch { Interlocked.Increment(ref errors); }
            }
        });

        Assert.Equal(0, errors);
    }

    #endregion

    #region No Duplicate Buffers Under Concurrent Access

    [Fact]
    public void ReturnValidation_ThrowOnError_ConcurrentRent_NoDuplicateBuffers()
    {
        using SlabBucket bucket = new(256, 32, returnValidation: ReturnValidation.ThrowOnError);

        const int threads = 8;
        const int buffersPerThread = 4;

        var allRented = new System.Collections.Concurrent.ConcurrentBag<byte[]>();
        var barrier = new Barrier(threads);
        var tasks = new Task[threads];

        for (int t = 0; t < threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < buffersPerThread; i++)
                {
                    byte[] arr = bucket.Rent();
                    allRented.Add(arr);
                }
            });
        }

        Task.WaitAll(tasks);

        // Verify no duplicates
        var seen = new HashSet<IntPtr>();
        foreach (byte[] arr in allRented)
        {
            unsafe
            {
                fixed (byte* p = arr)
                {
                    IntPtr addr = (IntPtr)p;
                    Assert.True(seen.Add(addr), $"Duplicate buffer detected at address 0x{addr:X}");
                }
            }
        }

        // Return all
        foreach (byte[] arr in allRented)
        {
            bucket.Return(arr);
        }
    }

    #endregion

    #region BufferOptions ValidationMode Default

    [Fact]
    public void BufferOptions_DebugDefault_IsThrow()
    {
        BufferOptions options = new();
        Assert.Equal(ReturnValidation.ThrowOnError, options.ReturnValidation);
    }

    [Fact]
    public void BufferOptions_CanBeSetToDisabled()
    {
        BufferOptions options = new() { ReturnValidation = ReturnValidation.Disabled };
        Assert.Equal(ReturnValidation.Disabled, options.ReturnValidation);
    }

    #endregion

    #region BufferPoolManager Integration

    [Fact]
    public void BufferPoolManager_WithValidationModeDisabled_RentReturnWorks()
    {
        var options = MemoryTestSupport.CreateBufferOptions(enableMemoryTrimming: false);
        options.ReturnValidation = ReturnValidation.Disabled;

        using BufferPoolManager manager = new(options);

        byte[] arr = manager.Rent(256);
        Assert.NotNull(arr);
        Assert.True(arr.Length >= 256);

        manager.Return(arr);
    }

    [Fact]
    public void BufferPoolManager_WithValidationModeThrowOnError_RentReturnWorks()
    {
        var options = MemoryTestSupport.CreateBufferOptions(enableMemoryTrimming: false);
        options.ReturnValidation = ReturnValidation.ThrowOnError;

        using BufferPoolManager manager = new(options);

        byte[] arr = manager.Rent(256);
        Assert.NotNull(arr);
        Assert.True(arr.Length >= 256);

        manager.Return(arr);
    }

    #endregion
}

#endif

