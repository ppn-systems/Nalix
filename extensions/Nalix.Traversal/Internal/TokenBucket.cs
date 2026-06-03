// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Environment.Time;

namespace Nalix.Traversal.Internal;

/// <summary>
/// A high-performance, byte-level Token Bucket rate limiter.
/// Designed for limiting bandwidth of P2P Reflector sessions.
/// </summary>
internal sealed class TokenBucket
{
    private readonly long _capacity;
    private readonly long _fillRate;
    private readonly Lock _lock = new();

    private long _tokens;
    private long _lastRefillTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucket"/> class.
    /// </summary>
    /// <param name="capacity">The maximum burst capacity (in bytes).</param>
    /// <param name="fillRate">The refill rate (in bytes per second).</param>
    public TokenBucket(long capacity, long fillRate)
    {
        _capacity = capacity;
        _fillRate = fillRate;
        _tokens = capacity; // Start full
        _lastRefillTicks = Clock.MonoTicksNow();
    }

    /// <summary>
    /// Attempts to consume the specified number of tokens (bytes).
    /// </summary>
    /// <param name="amount">The number of tokens to consume.</param>
    /// <returns><c>true</c> if the tokens were successfully consumed; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryConsume(int amount)
    {
        if (amount > _capacity)
        {
            return false;
        }

        lock (_lock)
        {
            long now = Clock.MonoTicksNow();
            long deltaTicks = now - _lastRefillTicks;

            if (deltaTicks > 0)
            {
                // Calculate tokens generated since the last refill.
                // Using double to prevent integer overflow and truncation issues before division.
                long generated = (long)((double)deltaTicks * _fillRate / Stopwatch.Frequency);

                if (generated > 0)
                {
                    _tokens += generated;
                    if (_tokens > _capacity)
                    {
                        _tokens = _capacity;
                    }
                    _lastRefillTicks = now;
                }
            }

            if (_tokens >= amount)
            {
                _tokens -= amount;
                return true;
            }

            return false;
        }
    }
}
