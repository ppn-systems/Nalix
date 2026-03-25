// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Memory.Internal;

/// <summary>
/// Provides helper methods for securely clearing sensitive data from memory.
/// This type is intended for security-critical paths such as credential handling,
/// key derivation, or symmetric encryption where sensitive information must be removed
/// from memory immediately after usage.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
internal static class MemorySecurity
{
    /// <summary>
    /// Overwrites the provided <paramref name="buffer"/> with zeros.
    /// This method does not throw if the buffer is <c>null</c> or empty.
    /// </summary>
    /// <param name="buffer">
    /// The byte array containing sensitive information that should be cleared.
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void ZeroMemory(byte[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        ZeroMemory(System.MemoryExtensions.AsSpan(buffer));

        System.GC.KeepAlive(buffer);
    }

    /// <summary>
    /// Overwrites the provided <paramref name="buffer"/> span with zeros.
    /// </summary>
    /// <param name="buffer">
    /// The span representing sensitive byte data that should be cleared.
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static void ZeroMemory(System.Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        // Use a ref-based loop to reduce bounds checks and make elimination harder.
        ref byte r0 = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer);

        for (int i = 0; i < buffer.Length; i++)
        {
            // Volatile.Write acts as a barrier against certain optimizations.
            System.Threading.Volatile.Write(
                ref System.Runtime.CompilerServices.Unsafe.Add(ref r0, i),
                0);
        }
    }
}
