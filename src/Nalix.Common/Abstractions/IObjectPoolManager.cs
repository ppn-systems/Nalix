// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Common.Abstractions;

/// <summary>
/// Defines a contract for managing object pools with support for
/// allocation, reuse, diagnostics, and lifecycle management.
/// </summary>
/// <remarks>
/// This abstraction provides a centralized, thread-safe mechanism
/// for pooling reusable objects while exposing metrics and control
/// operations for monitoring and tuning performance.
/// </remarks>
public interface IObjectPoolManager
{
    /// <summary>
    /// Gets or sets the default maximum size for newly created pools.
    /// </summary>
    int DefaultMaxPoolSize { get; set; }

    /// <summary>
    /// Gets the total number of pools currently managed.
    /// </summary>
    int PoolCount { get; }

    /// <summary>
    /// Gets the peak number of pools that have existed simultaneously.
    /// </summary>
    int PeakPoolCount { get; }

    /// <summary>
    /// Gets the total number of object retrieval (Get) operations.
    /// </summary>
    long TotalGetOperations { get; }

    /// <summary>
    /// Gets the total number of object return operations.
    /// </summary>
    long TotalReturnOperations { get; }

    /// <summary>
    /// Gets the total number of cache hits (objects served from pool).
    /// </summary>
    long TotalCacheHits { get; }

    /// <summary>
    /// Gets the total number of cache misses (new object allocations).
    /// </summary>
    long TotalCacheMisses { get; }

    /// <summary>
    /// Gets the overall cache hit rate as a percentage (0–100).
    /// </summary>
    double CacheHitRate { get; }

    /// <summary>
    /// Gets the uptime of the pool manager.
    /// </summary>
    TimeSpan Uptime { get; }

    /// <summary>
    /// Gets the number of pools currently considered unhealthy.
    /// </summary>
    int UnhealthyPoolCount { get; }

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

    /// <summary>
    /// Clears all objects from a specific pool.
    /// </summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <returns>The number of objects removed.</returns>
    int ClearPool<T>() where T : IPoolable;

    /// <summary>
    /// Clears all objects from all pools.
    /// </summary>
    /// <returns>Total number of objects removed.</returns>
    int ClearAllPools();

    /// <summary>
    /// Trims all pools by removing a percentage of stored objects.
    /// </summary>
    /// <param name="percentage">Percentage of objects to remove.</param>
    /// <returns>Total number of objects removed.</returns>
    int TrimAllPools(int percentage = 50);

    /// <summary>
    /// Performs a health check across all pools.
    /// </summary>
    /// <returns>The number of unhealthy pools detected.</returns>
    int PerformHealthCheck();

    /// <summary>
    /// Resets runtime metrics for the pool manager.
    /// </summary>
    void ResetMetrics();

    /// <summary>
    /// Resets all statistics including underlying pool statistics.
    /// </summary>
    void ResetStatistics();

    /// <summary>
    /// Retrieves diagnostic information for a specific pool type.
    /// </summary>
    /// <typeparam name="T">The poolable type.</typeparam>
    /// <returns>A dictionary containing diagnostic data.</returns>
    Dictionary<string, object> GetTypeInfo<T>() where T : IPoolable;

    /// <summary>
    /// Schedules periodic trimming and health checks in the background.
    /// </summary>
    /// <param name="interval">Execution interval.</param>
    /// <param name="percentage">Trim percentage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the background operation.</returns>
    Task ScheduleRegularTrimming(
        TimeSpan interval,
        int percentage = 50,
        CancellationToken cancellationToken = default);
}
