// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Abstractions;

namespace Nalix.Framework.Memory.Internal.PoolTypes;

/// <summary>
/// A fast, lock-free, zero-allocation thread-local cache slot for a specific poolable type.
/// </summary>
/// <typeparam name="T">The poolable type.</typeparam>
internal static class ThreadLocalCache<T> where T : IPoolable
{
    [ThreadStatic]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    private static T? t_value;

    /// <summary>
    /// Tries to pop the cached instance for the current thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? TryPop()
    {
        T? val = t_value;
        if (val != null)
        {
            t_value = default;
            return val;
        }
        return default;
    }

    /// <summary>
    /// Tries to push an instance to the thread-local cache slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPush(T obj)
    {
        if (t_value == null)
        {
            t_value = obj;
            return true;
        }
        return false;
    }
}
