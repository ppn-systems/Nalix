namespace Nalix.Common.Repositories.Sync;

/// <summary>
/// Defines the Unit of Work pattern to manage database transactions and ensure data consistency.
/// </summary>
public interface IUnitOfWorkSync : IRepositoryProvider, ITransaction, System.IDisposable
{
    /// <summary>
    /// Saves all changes made in the current transaction.
    /// </summary>
    /// <returns>The Number of affected database records.</returns>
    System.Int32 SaveChanges();
}
