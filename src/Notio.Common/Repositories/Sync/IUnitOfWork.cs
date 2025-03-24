using System;

namespace Notio.Common.Repositories.Sync;

/// <summary>
/// Defines the Unit of Work pattern to manage database transactions and ensure data consistency.
/// </summary>
public interface IUnitOfWorkSync : IRepositoryProvider, ITransaction, IDisposable
{
    /// <summary>
    /// Saves all changes made in the current transaction.
    /// </summary>
    /// <returns>The number of affected database records.</returns>
    int SaveChanges();
}
