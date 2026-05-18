// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Network.Internal.Pooling;

/// <summary>
/// Provides pooling operations for <see cref="PooledConnectEventContext"/> instances.
/// </summary>
/// <remarks>
/// This abstraction is used internally to minimize allocations during
/// connection event processing by reusing context instances.
/// </remarks>
internal interface IPooledConnectContextPool
{
    /// <summary>
    /// Releases any packet currently retained by the pool.
    /// </summary>
    /// <remarks>
    /// This method is intended for cleanup scenarios where a pending packet
    /// must be explicitly returned or discarded before the pool is reused.
    /// </remarks>
    void ReleasePendingPacket();

    /// <summary>
    /// Acquires a reusable <see cref="PooledConnectEventContext"/> instance from the pool.
    /// </summary>
    /// <returns>
    /// A pooled <see cref="PooledConnectEventContext"/> instance.
    /// </returns>
    PooledConnectEventContext AcquireContext();

    /// <summary>
    /// Returns a <see cref="PooledConnectEventContext"/> instance back to the pool.
    /// </summary>
    /// <param name="context">
    /// The context instance to return.
    /// </param>
    void ReturnContext(PooledConnectEventContext context);
}
