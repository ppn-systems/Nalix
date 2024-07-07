// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Framework.Cryptography.Padding;

/// <summary>
/// Helpers for zero padding to 16-byte boundaries in AEAD transcripts.
/// </summary>
[System.Diagnostics.DebuggerNonUserCode]
[System.Runtime.CompilerServices.SkipLocalsInit]
public static class AeadPadding
{
    /// <summary>
    /// Computes how many zero bytes are required to reach the next 16-byte boundary.
    /// </summary>
    /// <param name="length">Current length.</param>
    /// <returns>Zero-pad length in [0..15].</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 PadLen16(System.Int32 length) => (16 - (length & 0x0F)) & 0x0F;
}
