namespace Nalix.Common.Repositories.Async;

/// <summary>
/// Generic repository interface for asynchronous CRUD operations and querying entities.
/// </summary>
/// <typeparam name="T">The type of entity.</typeparam>
public interface IRepositoryAsync<T> : IRepositoryBase<T> where T : class
{
    /// <summary>
    /// Retrieves all entities with optional pagination.
    /// </summary>
    /// <param name="pageNumber">The page Number (1-based index).</param>
    /// <param name="pageSize">The Number of records per page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning a list of entities.</returns>
    System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<T>> GetAllAsync(
        System.Int32 pageNumber = 1,
        System.Int32 pageSize = 10,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the total Number of entities asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning the total Number of entities.</returns>
    System.Threading.Tasks.Task<System.Int32> CountAsync(
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity satisfies a given condition asynchronously.
    /// </summary>
    /// <param name="predicate">The condition to check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning true if any entity matches the condition; otherwise, false.</returns>
    System.Threading.Tasks.Task<System.Boolean> AnyAsync(
        System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> predicate,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks whether an entity with the specified Number exists in the database.
    /// </summary>
    /// <param name="id">The Number of the entity to check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning <c>true</c> if an entity with the specified Number exists;
    /// otherwise, <c>false</c>.
    /// </returns>
    System.Threading.Tasks.Task<System.Boolean> ExistsAsync(
        System.Int32 id,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the first entity that matches the specified condition, or null if no match is found.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning the first matching entity, or <c>null</c> if no match is found.
    /// </returns>
    System.Threading.Tasks.Task<T> GetFirstOrDefaultAsync(
        System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> predicate,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by its Number asynchronously.
    /// </summary>
    /// <param name="id">The entity Number.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning the entity if found; otherwise, null.</returns>
    System.Threading.Tasks.Task<T> GetByIdAsync(
        System.Int32 id,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities matching a specified condition with optional pagination asynchronously.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <param name="pageNumber">The page Number (1-based index).</param>
    /// <param name="pageSize">The Number of records per page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning a list of matching entities.</returns>
    System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<T>> FindAsync(
        System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> predicate,
        System.Int32 pageNumber = 1,
        System.Int32 pageSize = 10,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities with optional filtering, ordering, and pagination asynchronously.
    /// </summary>
    /// <param name="filter">Optional filter condition.</param>
    /// <param name="orderBy">Optional ordering function.</param>
    /// <param name="includeProperties">Comma-separated related properties to include.</param>
    /// <param name="pageNumber">The page Number (1-based index).</param>
    /// <param name="pageSize">The Number of records per page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning a list of entities.</returns>
    System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<T>> GetAsync(
        System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> filter = null,
        System.Func<System.Linq.IQueryable<T>, System.Linq.IOrderedQueryable<T>> orderBy = null,
        System.String includeProperties = "", System.Int32 pageNumber = 1, System.Int32 pageSize = 10,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity asynchronously.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    System.Threading.Tasks.Task AddAsync(
        T entity,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple entities asynchronously.
    /// </summary>
    /// <param name="entities">The list of entities to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    System.Threading.Tasks.Task AddRangeAsync(
        System.Collections.Generic.IEnumerable<T> entities,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its Number asynchronously.
    /// </summary>
    /// <param name="id">The Number of the entity to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    System.Threading.Tasks.Task DeleteAsync(
        System.Int32 id,
        System.Threading.CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes made in the context asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning the Number of state entries written to the database.</returns>
    System.Threading.Tasks.Task<System.Int32> SaveChangesAsync(
        System.Threading.CancellationToken cancellationToken = default);
}
