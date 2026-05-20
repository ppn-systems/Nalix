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

    [ThreadStatic]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
    private static object? t_owner;

    /// <summary>
    /// Tries to pop the cached instance for the current thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? TryPop(object owner)
    {
        T? val = t_value;
        if (val != null && ReferenceEquals(t_owner, owner))
        {
            t_value = default;
            t_owner = null;
            return val;
        }
        return default;
    }

    /// <summary>
    /// Tries to push an instance to the thread-local cache slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPush(object owner, T obj)
    {
        if (t_value == null)
        {
            t_value = obj;
            t_owner = owner;
            return true;
        }
        return false;
    }
}
