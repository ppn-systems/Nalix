namespace Notio.Common.Repositories.Sync;

/// <summary>
/// Provides access to repositories for different entity types.
/// </summary>
public interface IRepositoryProvider
{
    /// <summary>
    /// Retrieves the repository for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The type of entity.</typeparam>
    /// <returns>An instance of the repository for the specified entity.</returns>
    IRepository<T> GetRepository<T>() where T : class;
}
