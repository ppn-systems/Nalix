// Copyright (c) 2025 PPN Corporation. All rights reserved.

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
    public static void ZeroMemory(System.Byte[] buffer)
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
    public static void ZeroMemory(System.Span<System.Byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        for (System.Int32 i = 0; i < buffer.Length; i++)
        {
            System.Threading.Volatile.Write(ref buffer[i], 0);
        }
    }

    /// <summary>
    /// Overwrites the contents represented by the provided
    /// <paramref name="segment"/> with zeros. This method does not throw if
    /// the underlying array is <c>null</c>.
    /// </summary>
    /// <param name="segment">
    /// The <see cref="System.ArraySegment{T}"/> referencing memory containing
    /// sensitive data that must be cleared.
    /// </param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void ZeroMemory(System.ArraySegment<System.Byte> segment)
    {
        if (segment.Array is null)
        {
            return;
        }

        ZeroMemory(System.MemoryExtensions.AsSpan(segment));

        System.GC.KeepAlive(segment.Array);
    }
}
