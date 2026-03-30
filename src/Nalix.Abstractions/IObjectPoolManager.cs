// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions;

/// <summary>
/// Defines a contract for managing object pools with support for
/// allocation, reuse, diagnostics, and lifecycle management.
/// </summary>
/// <remarks>
/// This abstraction provides a centralized, thread-safe mechanism
/// for pooling reusable objects while exposing metrics and control
/// operations for monitoring and tuning performance.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "<Pending>")]
public interface IObjectPoolManager
{
    /// <summary>
    /// Retrieves an instance of <typeparamref name="T"/> from the pool,
    /// or creates a new one if none are available.
    /// </summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/>.</returns>
    T Get<T>() where T : IPoolable, new();

    /// <summary>
    /// Returns an object instance back to its pool.
    /// </summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <param name="obj">The object to return.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="obj"/> is null.
    /// </exception>
    void Return<T>(T obj) where T : IPoolable;

    /// <summary>
    /// Preallocates a number of instances and adds them to the pool.
    /// </summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <param name="count">Number of instances to allocate.</param>
    /// <returns>The number of objects actually allocated.</returns>
    int Prealloc<T>(int count) where T : IPoolable, new();

    /// <summary>
    /// Sets the maximum capacity of a pool for a specific type.
    /// </summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <param name="maxCapacity">Maximum number of retained objects.</param>
    /// <returns>
    /// True if the capacity was set successfully; otherwise false.
    /// </returns>
    bool SetMaxCapacity<T>(int maxCapacity) where T : IPoolable;
}
