// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Nalix.Abstractions;

/// <summary>
/// Represents a thread-safe key-value map backed by <see cref="ConcurrentDictionary{TKey, TValue}"/>,
/// integrated with an object pool to reduce allocations and GC pressure in high-concurrency scenarios.
/// </summary>
/// <typeparam name="TKey">The type of keys in the map.</typeparam>
/// <typeparam name="TValue">The type of values in the map.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "<Pending>")]
public interface IObjectMap<TKey, TValue> : IDictionary<TKey, TValue>, IPoolable
{
    /// <summary>
    /// Returns the current instance to the shared object pool.
    /// </summary>
    /// <remarks>
    /// After calling this method, the instance must not be used.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "<Pending>")]
    void Return();
}
