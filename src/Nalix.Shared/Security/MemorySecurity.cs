// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Shared.Security;

/// <summary>
/// Provides helper methods for securely clearing sensitive data from memory.
/// This type is intended for security-critical paths such as credential handling,
/// key derivation, or symmetric encryption where sensitive information must be removed
/// from memory immediately after usage.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
public static class MemorySecurity
{
    /// <summary>
    /// Overwrites the provided <paramref name="buffer"/> with zeros.
    /// This method does not throw if the buffer is <c>null</c> or empty.
    /// </summary>
    /// <param name="buffer">
    /// The byte array containing sensitive information that should be cleared.
    /// </param>
    public static void ZeroMemory(System.Byte[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        System.MemoryExtensions.AsSpan(buffer).Clear();
    }

    /// <summary>
    /// Overwrites the provided <paramref name="buffer"/> span with zeros.
    /// </summary>
    /// <param name="buffer">
    /// The span representing sensitive byte data that should be cleared.
    /// </param>
    public static void ZeroMemory(System.Span<System.Byte> buffer)
    {
        buffer.Clear();
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
    public static void ZeroMemory(System.ArraySegment<System.Byte> segment)
    {
        if (segment.Array is null)
        {
            return;
        }

        System.MemoryExtensions.AsSpan(segment).Clear();
    }
}
