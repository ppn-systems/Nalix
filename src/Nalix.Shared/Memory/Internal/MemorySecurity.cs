// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Provides helper methods for securely clearing sensitive data from memory.
/// This type is intended for security-critical paths such as credential handling,
/// key derivation, or symmetric encryption where sensitive information must be removed
/// from memory immediately after usage.
/// </summary>
[DebuggerNonUserCode]
internal static class MemorySecurity
{
    /// <summary>
    /// Overwrites the provided <paramref name="buffer"/> with zeros.
    /// This method does not throw if the buffer is <c>null</c> or empty.
    /// </summary>
    /// <param name="buffer">
    /// The byte array containing sensitive information that should be cleared.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ZeroMemory(byte[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        ZeroMemory(MemoryExtensions.AsSpan(buffer));

        GC.KeepAlive(buffer);
    }

    /// <summary>
    /// Overwrites the provided <paramref name="buffer"/> span with zeros.
    /// </summary>
    /// <param name="buffer">
    /// The span representing sensitive byte data that should be cleared.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining |
        MethodImplOptions.AggressiveOptimization)]
    public static void ZeroMemory(Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        // Use a ref-based loop to reduce bounds checks and make elimination harder.
        ref byte r0 = ref MemoryMarshal.GetReference(buffer);

        for (int i = 0; i < buffer.Length; i++)
        {
            // Volatile.Write acts as a barrier against certain optimizations.
            Volatile.Write(
                ref Unsafe.Add(ref r0, i),
                0);
        }
    }
}
