// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Abstractions;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.Memory.Internal.Buffers;

/// <summary>
/// Manages a pool of standalone pinned byte arrays for a single buffer size class.
/// Each buffer is an independent pinned array allocated on the Pinned Object Heap.
/// </summary>
[DebuggerNonUserCode]
[DebuggerDisplay("SIZE={_segmentSize}, Total={_totalBuffers}, Free={_freeRing.Count}")]
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class SlabBucket : IDisposable
{
    #region Fields

    private static int s_nextBucketId;
    private readonly int _bucketId;

    private readonly int _segmentSize;
    private readonly int _initialCapacity;
    private readonly int _cacheDepth;
    private readonly Lock _slabLock;
    private readonly SlabBucketRing _freeRing;
    private readonly ReturnValidation _returnValidation;
    private readonly ConcurrentDictionary<IntPtr, byte>? _rentedAddresses;

    [ThreadStatic]
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    private static ThreadLocalCache?[]? t_bucketCaches;

    private volatile IntPtr[] _sortedPinnedAddresses = Array.Empty<IntPtr>();

    private int _totalBuffers;
    private int _misses;
    private int _expands;
    private int _shrinks;
    private int _hits;
    private int _rentedCount;
    private long _totalBytesRented;
    private bool _disposed;
    private int _isOptimizing;
    private int _pendingShrinkCount;

    /// <summary>
    /// Occurs when the bucket needs to resize (expand or shrink).
    /// </summary>
    public event Action<SlabBucket, BufferPoolResizeDirection>? ResizeOccurred;

    private sealed class ThreadLocalCache
    {
        public readonly byte[]?[] Cache;
        public int Count;
        public int LocalHits;
        public long LocalBytesRented;

        public ThreadLocalCache(int depth) => Cache = new byte[depth][];
    }

    #endregion Fields

    #region Properties

    /// <summary>Gets the buffer size this bucket manages.</summary>
    public int SegmentSize => _segmentSize;

    /// <summary>Gets the total number of managed buffers (free + in use).</summary>
    public int TotalBuffers => Volatile.Read(ref _totalBuffers);

    /// <summary>Gets the approximate number of free buffers available.</summary>
    public int FreeBuffers => Math.Max(0, Volatile.Read(ref _totalBuffers) - this.GetTotalRented());

    #endregion Properties

    #region Constructor

    /// <summary>
    /// Initializes a new <see cref="SlabBucket"/> for standalone arrays of the given size.
    /// </summary>
    public SlabBucket(int segmentSize, int initialCapacity, int cacheDepth = 8,
                      ReturnValidation returnValidation = ReturnValidation.Disabled)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentSize);
        ArgumentOutOfRangeException.ThrowIfNegative(cacheDepth);

        _segmentSize = segmentSize;
        _initialCapacity = initialCapacity;
        _cacheDepth = cacheDepth;
        _returnValidation = returnValidation;
        _slabLock = new();

        _bucketId = Interlocked.Increment(ref s_nextBucketId) - 1;

        int ringCapacity = initialCapacity <= 0
            ? 4
            : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)initialCapacity);

        _freeRing = new SlabBucketRing(ringCapacity);

        // Only allocate the rented-address dictionary when validation is enabled.
        _rentedAddresses = returnValidation != ReturnValidation.Disabled
            ? new ConcurrentDictionary<IntPtr, byte>(concurrencyLevel: 128, capacity: 1024)
            : null;

        if (initialCapacity > 0)
        {
            this.AllocateAndEnqueue(initialCapacity);
        }
    }

    #endregion Constructor

    #region Public API

    /// <summary>
    /// Attempts to acquire a buffer from the thread-local cache or the shared ring.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRent([NotNullWhen(true)] out byte[]? array)
    {
        ThreadLocalCache cache = this.GetThreadLocalCache();
        if (_cacheDepth > 0 && cache.Count > 0)
        {
            int idx = --cache.Count;
            byte[]? cached = cache.Cache[idx];
            cache.Cache[idx] = null;

            this.TrackRentedAddress(cached);

            _ = Interlocked.Increment(ref _rentedCount);
            cache.LocalHits++;
            cache.LocalBytesRented += _segmentSize;
            if (cache.LocalHits >= 256)
            {
                _ = Interlocked.Add(ref _hits, cache.LocalHits);
                _ = Interlocked.Add(ref _totalBytesRented, cache.LocalBytesRented);
                cache.LocalHits = 0;
                cache.LocalBytesRented = 0;
            }

            array = cached!;
            return true;
        }

        if (_freeRing.TryDequeue(out array))
        {
            this.TrackRentedAddress(array);

            _ = Interlocked.Increment(ref _rentedCount);
            cache.LocalHits++;
            cache.LocalBytesRented += _segmentSize;
            if (cache.LocalHits >= 256)
            {
                _ = Interlocked.Add(ref _hits, cache.LocalHits);
                _ = Interlocked.Add(ref _totalBytesRented, cache.LocalBytesRented);
                cache.LocalHits = 0;
                cache.LocalBytesRented = 0;
            }

            return true;
        }

        array = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Rent()
    {
        if (this.TryRent(out byte[]? array))
        {
            return array;
        }

        _ = Interlocked.Increment(ref _misses);

        // Notify manager that we need to grow.
        this.ResizeOccurred?.Invoke(this, BufferPoolResizeDirection.Increase);

        if (this.TryRent(out array))
        {
            return array;
        }

        // Emergency fallback: if manager rejected growth, allocate one anyway 
        // to prevent consumer failure, but this should be rare.
        // We use a small retry loop because another thread might steal the array we just enqueued.
        for (int i = 0; i < 3; i++)
        {
            this.AllocateAndEnqueue(1);

            if (this.TryRent(out array))
            {
                return array;
            }
        }

        this.THROW_ALLOCATION_FAILED();
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(byte[] array)
    {
        if (array is null || array.Length != _segmentSize)
        {
            return;
        }

        // Ownership check to prevent memory poisoning with non-pinned arrays.
        // POH objects are always in Gen 2 (or higher in future runtimes).
        if (GC.GetGeneration(array) < 2)
        {
            return;
        }

        IntPtr addr;
        unsafe
        {
            fixed (byte* p = array)
            {
                addr = (IntPtr)p;
            }
        }

        if (!this.IsOwnedAddress(addr))
        {
            return;
        }

        // Rented-address validation (only when tracking is enabled).
        if (!this.TryUntrackRentedAddress(addr))
        {
            return;
        }

        ThreadLocalCache cache = this.GetThreadLocalCache();
        _ = Interlocked.Decrement(ref _rentedCount);

        // Deferred shrink: if we have pending shrinks, drop this buffer instead of caching/returning it.
        if (Volatile.Read(ref _pendingShrinkCount) > 0 && this.TRY_DEFERRED_SHRINK(addr))
        {
            return;
        }

        if (_cacheDepth <= 0)
        {
            _ = _freeRing.TryEnqueue(array);
            return;
        }

        if (cache.Count < _cacheDepth)
        {
            cache.Cache[cache.Count++] = array;
            return;
        }

        this.DrainCacheToRing(cache);
        cache.Cache[cache.Count++] = array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TRY_DEFERRED_SHRINK(IntPtr addr)
    {
        int pending = Volatile.Read(ref _pendingShrinkCount);
        while (pending > 0)
        {
            if (Interlocked.CompareExchange(ref _pendingShrinkCount, pending - 1, pending) == pending)
            {
                this.RemovePinnedAddress(addr);
                _ = Interlocked.Decrement(ref _totalBuffers);
                _ = Interlocked.Increment(ref _shrinks);
                return true;
            }
            pending = Volatile.Read(ref _pendingShrinkCount);
        }
        return false;
    }

    /// <summary>Increases capacity by adding more standalone arrays.</summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void IncreaseCapacity(int additionalCapacity)
    {
        if (additionalCapacity <= 0 || !this.TryBeginOptimize())
        {
            return;
        }

        try
        {
            // Cancel any pending shrinks if we are expanding
            _ = Interlocked.Exchange(ref _pendingShrinkCount, 0);

            this.AllocateAndEnqueue(additionalCapacity);
            _ = Interlocked.Increment(ref _expands);
        }
        finally
        {
            this.EndOptimize();
        }
    }

    /// <summary>Decreases capacity by dropping free buffers and releasing slabs.</summary>
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DecreaseCapacity(int capacityToRemove)
    {
        if (capacityToRemove <= 0 || !this.TryBeginOptimize())
        {
            return;
        }

        try
        {
            int currentTotal = Volatile.Read(ref _totalBuffers);
            int canRemove = Math.Min(capacityToRemove, currentTotal - _initialCapacity);
            if (canRemove <= 0)
            {
                return;
            }

            int removed = 0;
            int ringCount = _freeRing.Count;
            int immediateTarget = Math.Min(canRemove, ringCount);

            for (int i = 0; i < immediateTarget; i++)
            {
                if (_freeRing.TryDequeue(out byte[]? arr))
                {
                    removed++;
                    IntPtr addr;
                    unsafe
                    {
                        fixed (byte* p = arr)
                        {
                            addr = (IntPtr)p;
                        }
                    }
                    this.RemovePinnedAddress(addr);
                }
                else
                {
                    break;
                }
            }

            if (removed > 0)
            {
                _ = Interlocked.Add(ref _totalBuffers, -removed);
                _ = Interlocked.Increment(ref _shrinks);
            }

            // Queue remaining for deferred shrink as buffers are returned
            int remaining = canRemove - removed;
            if (remaining > 0)
            {
                _ = Interlocked.Add(ref _pendingShrinkCount, remaining);
            }
        }
        finally
        {
            this.EndOptimize();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferPoolState GetPoolInfo() => this.CreateSnapshot();

    #endregion Public API

    #region Private Helpers

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void THROW_ALLOCATION_FAILED() => throw new InvalidOperationException("SlabBucket: failed to allocate standalone buffer.");

    /// <summary>
    /// Tracks a buffer address as rented when validation is enabled.
    /// No-op when <see cref="ReturnValidation.Disabled"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TrackRentedAddress(byte[]? array)
    {
        if (_returnValidation == ReturnValidation.Disabled || array is null)
        {
            return;
        }

        IntPtr addr;
        unsafe
        {
            fixed (byte* p = array)
            {
                addr = (IntPtr)p;
            }
        }
        _ = _rentedAddresses!.TryAdd(addr, 0);
    }

    /// <summary>
    /// Attempts to untrack a rented buffer address. Returns true if the return is valid.
    /// </summary>
    /// <returns>
    /// <c>true</c> if validation passed (or validation is disabled);
    /// <c>false</c> if the return is invalid (double-return detected).
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryUntrackRentedAddress(IntPtr addr)
    {
        if (_returnValidation == ReturnValidation.Disabled)
        {
            return true;
        }

        // Validation enabled: the dictionary must exist.
        Debug.Assert(_rentedAddresses is not null);

        if (!_rentedAddresses.TryRemove(addr, out _))
        {
            // Address was not tracked as rented — this is a double-return or unowned buffer.
            if (_returnValidation == ReturnValidation.ThrowOnError)
            {
                throw new InvalidOperationException(
                    $"SlabBucket (size={_segmentSize}): double-return or untracked return detected for address 0x{addr:X}.");
            }

            // SilentDrop: silently drop.
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ThreadLocalCache GetThreadLocalCache()
    {
        ThreadLocalCache?[]? caches = t_bucketCaches;
        if (caches == null)
        {
            caches = new ThreadLocalCache[32];
            t_bucketCaches = caches;
        }

        int id = _bucketId;
        if (id >= caches.Length)
        {
            Array.Resize(ref caches, Math.Max(id + 1, caches.Length * 2));
            t_bucketCaches = caches;
        }

        ThreadLocalCache? cache = caches[id];
        if (cache == null)
        {
            cache = new ThreadLocalCache(_cacheDepth);
            caches[id] = cache;
        }

        return cache;
    }

    private int GetTotalRented() => Volatile.Read(ref _rentedCount);

    private int GetTotalHits() => Volatile.Read(ref _hits);

    public long GetTotalBytesRented() => Volatile.Read(ref _totalBytesRented);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsOwnedAddress(IntPtr addr)
    {
        IntPtr[] array = _sortedPinnedAddresses;
        int low = 0;
        int high = array.Length - 1;
        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            IntPtr midVal = array[mid];
            if (midVal == addr)
            {
                return true;
            }
            if ((ulong)midVal < (ulong)addr)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return false;
    }

    private void RemovePinnedAddress(IntPtr addr)
    {
        lock (_slabLock)
        {
            IntPtr[] current = _sortedPinnedAddresses;
            int idx = Array.IndexOf(current, addr);
            if (idx >= 0)
            {
                IntPtr[] next = new IntPtr[current.Length - 1];
                Array.Copy(current, 0, next, 0, idx);
                Array.Copy(current, idx + 1, next, idx, current.Length - idx - 1);
                _sortedPinnedAddresses = next;
            }
        }
    }

    /// <summary>
    /// Allocates individual pinned arrays and enqueues them.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AllocateAndEnqueue(int count)
    {
        if (count <= 0)
        {
            return;
        }

        _freeRing.EnsureCapacity(_freeRing.Count + count);

        int enqueued = 0;
        lock (_slabLock)
        {
            IntPtr[] newAddrs = new IntPtr[_sortedPinnedAddresses.Length + count];
            Array.Copy(_sortedPinnedAddresses, newAddrs, _sortedPinnedAddresses.Length);
            int origLength = _sortedPinnedAddresses.Length;

            for (int i = 0; i < count; i++)
            {
                byte[] array = GC.AllocateArray<byte>(_segmentSize, pinned: true);
                Debug.Assert(GC.GetGeneration(array) == 2); // Verify it lives on POH

                IntPtr addr;
                unsafe
                {
                    fixed (byte* p = array)
                    {
                        addr = (IntPtr)p;
                    }
                }
                newAddrs[origLength + i] = addr;

                if (_freeRing.TryEnqueue(array))
                {
                    enqueued++;
                }
            }

            Array.Sort(newAddrs);
            _sortedPinnedAddresses = newAddrs;
        }

        if (enqueued > 0)
        {
            _ = Interlocked.Add(ref _totalBuffers, enqueued);
        }
    }

    /// <summary>Drains the current thread's cache to the shared ring.</summary>
    private void DrainCacheToRing(ThreadLocalCache cache)
    {
        int toMove = Math.Max(1, cache.Count / 2);

        // Ensure the ring can hold the new buffers to prevent loss.
        _freeRing.EnsureCapacity(_freeRing.Count + toMove);

        for (int i = 0; i < toMove; i++)
        {
            byte[]? arr = cache.Cache[i];
            if (arr != null)
            {
                IntPtr addr;
                unsafe
                {
                    fixed (byte* p = arr)
                    {
                        addr = (IntPtr)p;
                    }
                }

                // Also check for deferred shrink here
                if (Volatile.Read(ref _pendingShrinkCount) > 0 && this.TRY_DEFERRED_SHRINK(addr))
                {
                    cache.Cache[i] = null;
                    continue;
                }

                if (_freeRing.TryEnqueue(arr))
                {
                    cache.Cache[i] = null;
                }
            }
        }

        // Shift remaining
        int remaining = cache.Count - toMove;
        Array.Copy(cache.Cache, toMove, cache.Cache, 0, remaining);
        Array.Clear(cache.Cache, remaining, toMove);
        cache.Count = remaining;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BufferPoolState CreateSnapshot()
    {
        int total = Volatile.Read(ref _totalBuffers);
        int rented = this.GetTotalRented();

        return new BufferPoolState
        {
            FreeBuffers = Math.Max(0, total - rented),
            TotalBuffers = total,
            InitialCapacity = _initialCapacity,
            Expands = Volatile.Read(ref _expands),
            Shrinks = Volatile.Read(ref _shrinks),
            BufferSize = _segmentSize,
            Misses = Volatile.Read(ref _misses),
            Hits = this.GetTotalHits()
        };
    }

    private bool TryBeginOptimize() => Interlocked.CompareExchange(ref _isOptimizing, 1, 0) == 0;
    private void EndOptimize() => Volatile.Write(ref _isOptimizing, 0);

    #endregion Private Helpers

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_slabLock)
        {
            if (_disposed)
            {
                return;
            }

            _ = _freeRing.DrainAll();
            _sortedPinnedAddresses = Array.Empty<IntPtr>();
            _disposed = true;
        }
    }

    #endregion IDisposable

    #region Inner: Ring Buffer

    internal sealed class SlabBucketRing
    {
        private byte[]?[] _slots;
        private int _head;
        private int _tail;
        private int _count;
        private SpinLock _lock;

        public int Count => Volatile.Read(ref _count);

        public SlabBucketRing(int capacity)
        {
            _slots = new byte[capacity][];
            _lock = new SpinLock(false);
        }

        public bool TryEnqueue(byte[] buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == _slots.Length)
                {
                    return false;
                }

                _slots[_tail] = buffer;
                _tail = (_tail + 1) & (_slots.Length - 1);
                _count++;
                return true;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }

        public bool TryDequeue([NotNullWhen(true)] out byte[]? buffer)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == 0) { buffer = null; return false; }
                buffer = _slots[_head]!;
                _slots[_head] = null;
                _head = (_head + 1) & (_slots.Length - 1);
                _count--;
                return true;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }

        public void EnsureCapacity(int targetCapacity)
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (targetCapacity <= _slots.Length)
                {
                    return;
                }

                uint newSize = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)targetCapacity);
                byte[][] newSlots = new byte[newSize][];
                for (int i = 0; i < _count; ++i)
                {
                    newSlots[i] = _slots![(_head + i) & (_slots.Length - 1)]!;
                }

                _slots = newSlots; _head = 0; _tail = _count;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }

        public byte[][] DrainAll()
        {
            bool taken = false;
            try
            {
                _lock.Enter(ref taken);
                if (_count == 0)
                {
                    return Array.Empty<byte[]>();
                }

                byte[][] result = new byte[_count][];
                for (int i = 0; i < _count; ++i)
                {
                    int index = (_head + i) & (_slots.Length - 1);
                    result[i] = _slots[index]!;
                    _slots[index] = null;
                }
                _head = _tail = _count = 0;
                return result;
            }
            finally
            {
                if (taken)
                {
                    _lock.Exit();
                }
            }
        }
    }

    #endregion Inner: Ring Buffer
}
