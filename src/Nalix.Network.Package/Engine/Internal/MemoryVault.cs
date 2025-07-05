using Nalix.Common.Constants;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nalix.Network.Package.Engine.Internal;

/// <summary>
/// Ultra-optimized memory allocator using aggressive unsafe operations and direct memory management
/// </summary>
internal static unsafe class MemoryVault
{
    private static readonly ConcurrentDictionary<nint, TrackedMemory> _trackedMemory = new();
    private static readonly Timer _cleanupTimer;
    private static readonly Lock _lockObject = new();

    private const int CleanupIntervalMs = 15000;  // 15 seconds - more aggressive
    private const int UnusedThresholdMs = 45000;  // 45 seconds - shorter threshold

    static MemoryVault()
    {
        _cleanupTimer = new Timer(static _ => CleanupUnusedMemory(), null, CleanupIntervalMs, CleanupIntervalMs);
    }

    /// <summary>
    /// Allocates memory using the most aggressive unsafe optimizations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static ManagedBuffer Allocate(ReadOnlySpan<byte> payload)
    {
        var length = payload.Length;

        // Ultra-fast empty check
        if (length == 0)
            return new ManagedBuffer(ReadOnlyMemory<byte>.Empty, null);

        // Stack allocation for tiny payloads - direct stackalloc
        if (length <= PacketConstants.StackAllocLimit)
        {
            return AllocateStack(payload);
        }

        // Pinned heap allocation for medium payloads
        if (length <= PacketConstants.HeapAllocLimit)
        {
            return AllocateHeap(payload);
        }

        // Pooled allocation for large payloads with tracking
        return AllocatePooled(payload);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ManagedBuffer AllocateStack(ReadOnlySpan<byte> payload)
    {
        var array = GC.AllocateUninitializedArray<byte>(payload.Length, pinned: false);

        fixed (byte* dest = array)
        fixed (byte* src = payload)
        {
            // Ultra-fast memory copy using CPU intrinsics when possible
            Unsafe.CopyBlockUnaligned(dest, src, (uint)payload.Length);
        }

        return new ManagedBuffer(array, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ManagedBuffer AllocateHeap(ReadOnlySpan<byte> payload)
    {
        // Pinned allocation for better performance
        var array = GC.AllocateUninitializedArray<byte>(payload.Length, pinned: true);

        fixed (byte* dest = array)
        fixed (byte* src = payload)
        {
            // Direct memory copy using vectorized operations when possible
            var remaining = payload.Length;
            var destPtr = dest;
            var srcPtr = src;

            // Copy in 64-byte chunks for better cache utilization
            while (remaining >= 64)
            {
                Unsafe.CopyBlock(destPtr, srcPtr, 64);
                destPtr += 64;
                srcPtr += 64;
                remaining -= 64;
            }

            // Copy remaining bytes
            if (remaining > 0)
                Unsafe.CopyBlockUnaligned(destPtr, srcPtr, (uint)remaining);
        }

        return new ManagedBuffer(array, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ManagedBuffer AllocatePooled(ReadOnlySpan<byte> payload)
    {
        var pooledArray = ArrayPool<byte>.Shared.Rent(payload.Length);
        var actualLength = payload.Length;

        // Ultra-fast unsafe copy
        fixed (byte* dest = pooledArray)
        fixed (byte* src = payload)
        {
            // Use the most aggressive memory copy available
            Buffer.MemoryCopy(src, dest, pooledArray.Length, actualLength);
        }

        // Create weak reference for tracking
        var weakRef = new WeakReference<byte[]>(pooledArray);
        var handle = new PoolHandle((nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(pooledArray.AsSpan())));

        // Track for cleanup
        var entry = new TrackedMemory(pooledArray, Environment.TickCount64, weakRef);
        _trackedMemory.TryAdd(handle.Address, entry);

        return new ManagedBuffer(pooledArray.AsMemory(0, actualLength), handle);
    }

    /// <summary>
    /// Returns pooled memory using direct pointer manipulation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReturnToPool(PoolHandle handle)
    {
        if (handle.Address != 0 && _trackedMemory.TryRemove(handle.Address, out var entry))
        {
            try
            {
                if (entry.WeakRef.TryGetTarget(out var array))
                {
                    // Clear sensitive data before returning to pool
                    array.AsSpan().Clear();
                    ArrayPool<byte>.Shared.Return(array, clearArray: false);
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }

    /// <summary>
    /// Aggressive cleanup using direct memory scanning
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CleanupUnusedMemory()
    {
        var currentTime = Environment.TickCount64;
        var toRemove = new List<nint>();

        // Use lock-free enumeration where possible
        foreach (var kvp in _trackedMemory)
        {
            var entry = kvp.Value;
            var isAlive = entry.WeakRef.TryGetTarget(out _);

            if (!isAlive || (currentTime - entry.LastAccessTime) > UnusedThresholdMs)
            {
                toRemove.Add(kvp.Key);
            }
        }

        // Batch cleanup for better performance
        if (toRemove.Count > 0)
        {
            lock (_lockObject)
            {
                foreach (var address in toRemove)
                {
                    if (_trackedMemory.TryRemove(address, out var entry) &&
                        entry.WeakRef.TryGetTarget(out var array))
                    {
                        try
                        {
                            // Zero out memory before returning
                            array.AsSpan().Clear();
                            ArrayPool<byte>.Shared.Return(array, clearArray: false);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Force cleanup all tracked memory (shutdown scenario)
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Shutdown()
    {
        _cleanupTimer?.Dispose();

        foreach (var kvp in _trackedMemory)
        {
            if (kvp.Value.WeakRef.TryGetTarget(out var array))
            {
                try
                {
                    array.AsSpan().Clear();
                    ArrayPool<byte>.Shared.Return(array, clearArray: false);
                }
                catch { }
            }
        }

        _trackedMemory.Clear();
    }
}