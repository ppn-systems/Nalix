// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Nalix.Codec.DataFrames.Chunks;

/// <summary>
/// Thread-safe generator for <see cref="FragmentHeader.StreamId"/>.
/// Automatically wraps around to 1 after 65,535. StreamId = 0 is reserved/invalid.
/// </summary>
public static class FragmentStreamId
{
    private static uint s_counter;

    /// <summary>
    /// Allocates the next StreamId in a thread-safe way. Never returns 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Next()
    {
        uint raw = Interlocked.Increment(ref s_counter);
        ushort id = (ushort)(raw & 0xFFFFu);

        // StreamId = 0 is invalid — skip over it
        return id == 0 ? (ushort)1 : id;
    }
}

