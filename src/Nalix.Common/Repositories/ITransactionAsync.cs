// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Repositories;

/// <summary>
/// Manages database transactions to ensure data consistency.
/// </summary>
public interface ITransactionAsync
{
    /// <summary>
    /// Begins a new database transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task BeginTransactionAsync(
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current database transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task CommitTransactionAsync(
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current database transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    System.Threading.Tasks.Task RollbackTransactionAsync(
        System.Threading.CancellationToken cancellationToken = default);
}
