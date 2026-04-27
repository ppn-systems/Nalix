// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions;

/// <summary>
/// Defines a contract for objects that can be reused through an object pool.
/// </summary>
/// <remarks>
/// Implementations should reset any internal state in <see cref="ResetForPool"/>
/// to ensure the object is ready for reuse without unintended side effects.
/// </remarks>
public interface IPoolable
{
    /// <summary>
    /// Resets the state of the current <see cref="IPoolable"/> instance
    /// before it is returned to the pool.
    /// </summary>
    /// <remarks>
    /// This method is called automatically by the pool manager to clear or reinitialize
    /// the object's state, ensuring it behaves as a fresh instance when rented again.
    /// </remarks>
    void ResetForPool();
}

/// <summary>
/// Defines a contract for objects that need to be notified when they are rented from a pool.
/// </summary>
public interface IPoolRentable : IPoolable
{
    /// <summary>
    /// Notifies the object that it has been rented from the pool.
    /// </summary>
    /// <remarks>
    /// This method is called automatically by the pool manager immediately after an object
    /// is retrieved from the pool or newly created.
    /// </remarks>
    void OnRent();
}
