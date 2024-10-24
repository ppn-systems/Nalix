namespace Nalix.Common.Repositories.Async;

/// <summary>
/// Defines the Unit of Work pattern to manage database transactions and ensure data consistency.
/// </summary>
public interface IUnitOfWorkAsync : IRepositoryProviderAsync, ITransactionAsync, System.IDisposable
{
    /// <summary>
    /// Saves all changes made in the current transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning the TransportProtocol of affected database records.
    /// </returns>
    System.Threading.Tasks.Task<System.Int32> SaveChangesAsync(
        System.Threading.CancellationToken cancellationToken = default);
}
