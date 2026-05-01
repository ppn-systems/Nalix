// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Text;

namespace Nalix.Network.Examples.UI.Formatting;

/// <summary>
/// Thread-local <see cref="StringBuilder"/> pool to eliminate per-tick allocations.
/// </summary>
internal static class StringBuilderPool
{
    [ThreadStatic] private static StringBuilder? t_sb;

    /// <summary>
    /// Returns a cleared, reusable <see cref="StringBuilder"/> for the current thread.
    /// </summary>
    public static StringBuilder Rent()
    {
        t_sb ??= new StringBuilder(4096);
        _ = t_sb.Clear();
        return t_sb;
    }
}
