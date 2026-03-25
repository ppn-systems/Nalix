// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace Nalix.Network.Listeners.Udp;

public abstract partial class UdpListenerBase
{
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    private static string EllipseLeft(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLen)
        {
            return value;
        }
        else
        {
            return maxLen <= 3 ? new string('.', maxLen) : $"...{MemoryExtensions.AsSpan(value, value.Length - (maxLen - 3))}";
        }
    }
}
