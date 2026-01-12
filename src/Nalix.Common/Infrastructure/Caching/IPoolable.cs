// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Infrastructure.Caching;

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
