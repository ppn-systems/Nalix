// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string EllipseLeft(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
        {
            return value;
        }
        else
        {
            return maxLen <= 3 ? new string('.', maxLen) : $"...{System.MemoryExtensions.AsSpan(value, value.Length - (maxLen - 3))}";
        }
    }
}
