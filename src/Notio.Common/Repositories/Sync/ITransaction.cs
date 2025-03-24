namespace Notio.Common.Repositories.Sync;

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
