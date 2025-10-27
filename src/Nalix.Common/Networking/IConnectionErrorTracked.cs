// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Networking;

/// <summary>
/// Defines a contract for tracking errors associated with a connection.
/// </summary>
/// <remarks>
/// Implementations are expected to provide thread-safe error tracking,
/// typically used for connection monitoring, throttling, or banning logic.
/// </remarks>
public interface IConnectionErrorTracked
{
    /// <summary>
    /// Gets or sets the total number of errors encountered by the connection.
    /// </summary>
    /// <remarks>
    /// This value can be used to evaluate connection health and determine
    /// whether the connection should be restricted or banned.
    /// </remarks>
    int ErrorCount { get; }

    /// <summary>
    /// Atomically increments the <see cref="ErrorCount"/>.
    /// </summary>
    /// <remarks>
    /// Implementations should ensure thread safety when updating the error count.
    /// </remarks>
    void IncrementErrorCount();
}
