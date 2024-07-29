// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Repositories;

/// <summary>
/// Manages database transactions synchronously to ensure data consistency.
/// </summary>
public interface ITransaction
{
    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    void BeginTransaction();

    /// <summary>
    /// Commits the current database transaction.
    /// </summary>
    void CommitTransaction();

    /// <summary>
    /// Rolls back the current database transaction.
    /// </summary>
    void RollbackTransaction();
}
