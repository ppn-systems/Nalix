// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Common.Security;

/// <summary>
/// Implements a sliding window for sequence number validation to prevent replay attacks.
/// Thread-safe using atomic operations.
/// </summary>
public sealed class SlidingWindow
{
    private readonly int _windowSize;
    private readonly int _arraySize;
    private long _maxSeen;
    private readonly long[] _bitmap;
    private readonly Lock _lock = new();

    public SlidingWindow(int windowSize = 1024)
    {
        _windowSize = windowSize;
        _arraySize = windowSize / 64;
        _bitmap = new long[_arraySize];
    }

    /// <summary>
    /// Attempts to check and mark a sequence number.
    /// </summary>
    /// <param name="seq">The 32-bit sequence number to check.</param>
    /// <returns>True if the sequence number is new and within the window; false if replayed or too old.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCheck(uint seq)
    {
        lock (_lock)
        {
            long s = seq;
            long currentMax = _maxSeen;

            if (s > currentMax)
            {
                // Advance window
                long shift = s - currentMax;
                if (shift >= _windowSize)
                {
                    Array.Clear(_bitmap);
                }
                else
                {
                    this.SHIFT_BITMAP(shift);
                }
                _maxSeen = s;
                this.MARK_BIT(0);
                return true;
            }

            long diff = currentMax - s;
            if (diff >= _windowSize)
            {
                return false; // Too old
            }

            if (this.IS_BIT_SET(diff))
            {
                return false; // Replayed
            }

            this.MARK_BIT(diff);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SHIFT_BITMAP(long shift)
    {
        int wholeWords = (int)(shift / 64);
        int bits = (int)(shift % 64);

        if (wholeWords > 0)
        {
            for (int i = _arraySize - 1; i >= wholeWords; i--)
            {
                _bitmap[i] = _bitmap[i - wholeWords];
            }
            for (int i = 0; i < wholeWords; i++)
            {
                _bitmap[i] = 0;
            }
        }

        if (bits > 0)
        {
            for (int i = _arraySize - 1; i >= 0; i--)
            {
                _bitmap[i] <<= bits;
                if (i > 0)
                {
                    _bitmap[i] |= (long)((ulong)_bitmap[i - 1] >> (64 - bits));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IS_BIT_SET(long diff)
    {
        int word = (int)(diff / 64);
        int bit = (int)(diff % 64);
        return (_bitmap[word] & (1L << bit)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MARK_BIT(long diff)
    {
        int word = (int)(diff / 64);
        int bit = (int)(diff % 64);
        _bitmap[word] |= 1L << bit;
    }
}
