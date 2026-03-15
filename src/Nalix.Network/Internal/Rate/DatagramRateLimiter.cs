// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;

namespace Nalix.Network.Internal.Rate;

/// <summary>
/// A high-performance, lock-free fixed-window rate limiter for UDP traffic.
/// Utilizes Interlocked CAS operations on a struct-based window to avoid lock contention
/// on the hot path. Enforces Max Packets-Per-Second per IP to prevent DDoS floods.
/// Intended to be registered as a singleton in the DI container.
/// </summary>
public sealed class DatagramRateLimiter : IDisposable, IWithLogging<DatagramRateLimiter>
{
    // ── Packed into a single 64-bit value for Interlocked.CompareExchange ──
    // High 32 bits: secondOffset (uint)  |  Low 32 bits: count (uint)
    // Enables full CAS atomics across the window state, eliminating the need for locks.
    private sealed class WindowSlot
    {
        // Volatile long: [secondOffset (high 32) | count (low 32)]
        public long Packed;
    }

    // ── Custom comparer to avoid boxing and use direct byte[] hash for uint IP keys ──
    private sealed class IpComparer : IEqualityComparer<uint>
    {
        public static readonly IpComparer Instance = new();
        public bool Equals(uint x, uint y) => x == y;
        public int GetHashCode(uint obj) => (int)obj;
    }

    // ── Use uint (raw IPv4 address bytes) as dictionary keys, avoiding IPAddress allocations ──
    // Using IPAddress as keys incurs heap allocation; raw uint is faster. 
    // IPv6 fallback uses string.GetHashCode(), handled separately below.
    private readonly int _maxPacketsPerSecond;
    private readonly ConcurrentDictionary<uint, WindowSlot> _ipv4Map;
    private readonly ConcurrentDictionary<string, WindowSlot> _ipv6Map;
    private readonly CancellationTokenSource _cts;
    private ILogger? _logger;
    private int _disposed;

    // Bit packing constants
    private const long CountMask = 0x00000000FFFFFFFFL;
    private const int SecondShift = 32;

    /// <summary>
    /// Initializes a new instance of <see cref="DatagramRateLimiter"/>.
    /// </summary>
    /// <param name="maxPacketsPerSecond">The maximum number of accepted packets per second per IP address. Defaults to 1000.</param>
    public DatagramRateLimiter(int maxPacketsPerSecond = 1000)
    {
        _maxPacketsPerSecond = maxPacketsPerSecond;

        // Capacity hint to avoid early resizes, concurrencyLevel = number of CPU cores
        int concurrency = Environment.ProcessorCount;
        _ipv4Map = new ConcurrentDictionary<uint, WindowSlot>(concurrency, capacity: 1024);
        _ipv6Map = new ConcurrentDictionary<string, WindowSlot>(
            concurrency, capacity: 64, StringComparer.Ordinal);

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => this.CleanupLoopAsync(_cts.Token));
    }

    /// <inheritdoc/>
    public DatagramRateLimiter WithLogging(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Checks if a packet from the specified <paramref name="endPoint"/> should be accepted.
    /// Lock-free on the hot path, leveraging a CAS loop.
    /// </summary>
    /// <param name="endPoint">The remote UDP endpoint for incoming packets.</param>
    /// <returns><c>true</c> if the packet can be accepted; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAccept(IPEndPoint endPoint)
    {
        if (Volatile.Read(ref _disposed) != 0 || endPoint is null)
        {
            return false;
        }

        // Use Environment.TickCount64 / 1000 to obtain a "second token".
        // Shifting by 10 (>> 10) is roughly equivalent to division by 1024 ms—this is faster than direct division but has ~2.4% error.
        // Use / 1000 for exact precision if strictly required.
        uint currentSecond = (uint)(Environment.TickCount64 >> 10);

        return endPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? this.TryAcceptIPv4(endPoint.Address, currentSecond)
            : this.TryAcceptIPv6(endPoint.Address, currentSecond);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAcceptIPv4(IPAddress address, uint currentSecond)
    {
        // Get raw uint value from IPv4 to avoid allocation and IPAddress.GetHashCode overhead
#pragma warning disable CS0618
        uint key = (uint)address.Address; // Obsolete API, but fastest path for IPv4
#pragma warning restore CS0618

        WindowSlot slot = _ipv4Map.GetOrAdd(key, static _ => new WindowSlot());
        return this.CasAccept(slot, currentSecond);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAcceptIPv6(IPAddress address, uint currentSecond)
    {
        // For IPv6, use the address as a string key (less common for UDP DDoS scenarios)
        string key = address.ToString();
        WindowSlot slot = _ipv6Map.GetOrAdd(key, static _ => new WindowSlot());
        return this.CasAccept(slot, currentSecond);
    }

    /// <summary>
    /// Lock-free CAS loop for updating the window and checking the rate limit.
    /// </summary>
    /// <param name="slot">Window slot for the IP.</param>
    /// <param name="currentSecond">Current second "token".</param>
    /// <returns><c>true</c> if the packet is accepted; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CasAccept(WindowSlot slot, uint currentSecond)
    {
        while (true)
        {
            long oldPacked = Volatile.Read(ref slot.Packed);
            uint oldSecond = (uint)(oldPacked >> SecondShift);
            uint oldCount = (uint)(oldPacked & CountMask);

            long newPacked;

            if (oldSecond != currentSecond)
            {
                // New window: reset count to 1
                newPacked = ((long)currentSecond << SecondShift) | 1L;
            }
            else
            {
                if (oldCount >= (uint)_maxPacketsPerSecond)
                {
                    return false; // Rate limit exceeded
                }

                newPacked = ((long)currentSecond << SecondShift) | (oldCount + 1L);
            }

            // CAS: only update if no other thread has changed it in the meantime
            if (Interlocked.CompareExchange(ref slot.Packed, newPacked, oldPacked) == oldPacked)
            {
                return true;
            }

            // CAS failed due to race—spin and retry (normally resolves in 1-2 iterations)
            Thread.SpinWait(1);
        }
    }

    /// <summary>
    /// Background cleanup loop to evict stale window slots periodically.
    /// </summary>
    /// <param name="token">Cancellation token for loop termination.</param>
    private async Task CleanupLoopAsync(CancellationToken token)
    {
        TimeSpan cleanupInterval = TimeSpan.FromMinutes(1);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(cleanupInterval, token).ConfigureAwait(false);
                this.EvictStaleWindows();
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException ex)
            {
                _logger?.Warn($"[NW.{nameof(DatagramRateLimiter)}] Cleanup error.", ex);
            }
        }
    }

    /// <summary>
    /// Removes stale window slots for IPs that have not sent recent traffic.
    /// </summary>
    private void EvictStaleWindows()
    {
        uint currentSecond = (uint)(Environment.TickCount64 >> 10);
        const uint staleThreshold = 10; // Inactivity threshold: ~10 seconds
        int removed = 0;

        // Reuse list with capacity hints for efficiency.
        List<uint> toRemoveV4 = new((_ipv4Map.Count / 4) + 1);
        List<string> toRemoveV6 = new((_ipv6Map.Count / 4) + 1);

        foreach ((uint key, WindowSlot? slot) in _ipv4Map)
        {
            uint slotSecond = (uint)(Volatile.Read(ref slot.Packed) >> SecondShift);
            if ((currentSecond - slotSecond) > staleThreshold)
            {
                toRemoveV4.Add(key);
            }
        }

        foreach ((string? key, WindowSlot? slot) in _ipv6Map)
        {
            uint slotSecond = (uint)(Volatile.Read(ref slot.Packed) >> SecondShift);
            if ((currentSecond - slotSecond) > staleThreshold)
            {
                toRemoveV6.Add(key);
            }
        }

        foreach (uint key in toRemoveV4)
        {
            if (_ipv4Map.TryRemove(key, out _))
            {
                removed++;
            }
        }

        foreach (string key in toRemoveV6)
        {
            if (_ipv6Map.TryRemove(key, out _))
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            _logger?.Debug($"[NW.{nameof(DatagramRateLimiter)}] Evicted {removed} idle windows. IPv4={_ipv4Map.Count}, IPv6={_ipv6Map.Count}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _cts.Cancel();
            _cts.Dispose();
            _ipv4Map.Clear();
            _ipv6Map.Clear();
        }
    }
}
