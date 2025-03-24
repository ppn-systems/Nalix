using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Common.Repositories.Async;

/// <summary>
/// Defines the Unit of Work pattern to manage database transactions and ensure data consistency.
/// </summary>
public interface IUnitOfWorkAsync : IRepositoryProviderAsync, ITransactionAsync, IDisposable
{
    /// <summary>
    /// Saves all changes made in the current transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning the number of affected database records.
    /// </returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
