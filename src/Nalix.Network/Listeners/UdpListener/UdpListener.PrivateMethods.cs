// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String EllipseLeft(System.String value, System.Int32 maxLen)
    {
        return System.String.IsNullOrEmpty(value) || value.Length <= maxLen
            ? value
            : maxLen <= 3 ? new System.String('.', maxLen) : $"...{System.MemoryExtensions.AsSpan(value, value.Length - (maxLen - 3))}";
    }
}