namespace Nalix.Common.Repositories.Async;

/// <summary>
/// Provides access to asynchronous repositories for different entity types.
/// </summary>
public interface IRepositoryProviderAsync
{
    /// <summary>
    /// Retrieves the asynchronous repository for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The type of entity.</typeparam>
    /// <returns>An instance of the asynchronous repository for the specified entity.</returns>
    IRepositoryAsync<T> GetRepositoryAsync<T>() where T : class;
}
