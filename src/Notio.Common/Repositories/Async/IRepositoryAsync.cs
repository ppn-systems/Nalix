using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Notio.Common.Repositories.Async;

/// <summary>
/// Generic repository interface for asynchronous CRUD operations and querying entities.
/// </summary>
/// <typeparam name="T">The type of entity.</typeparam>
public interface IRepositoryAsync<T> where T : class
{
    /// <summary>
    /// Retrieves all entities with optional pagination.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based index).</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning a list of entities.</returns>
    Task<IEnumerable<T>> GetAllAsync(
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the total number of entities asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning the total number of entities.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity satisfies a given condition asynchronously.
    /// </summary>
    /// <param name="predicate">The condition to check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning true if any entity matches the condition; otherwise, false.</returns>
    Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks whether an entity with the specified ID exists in the database.
    /// </summary>
    /// <param name="id">The ID of the entity to check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning <c>true</c> if an entity with the specified ID exists;
    /// otherwise, <c>false</c>.
    /// </returns>
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the first entity that matches the specified condition, or null if no match is found.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A task representing the asynchronous operation, returning the first matching entity, or <c>null</c> if no match is found.
    /// </returns>
    Task<T> GetFirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by its ID asynchronously.
    /// </summary>
    /// <param name="id">The entity ID.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning the entity if found; otherwise, null.</returns>
    Task<T> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities matching a specified condition with optional pagination asynchronously.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <param name="pageNumber">The page number (1-based index).</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning a list of matching entities.</returns>
    Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities with optional filtering, ordering, and pagination asynchronously.
    /// </summary>
    /// <param name="filter">Optional filter condition.</param>
    /// <param name="orderBy">Optional ordering function.</param>
    /// <param name="includeProperties">Comma-separated related properties to include.</param>
    /// <param name="pageNumber">The page number (1-based index).</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning a list of entities.</returns>
    Task<IEnumerable<T>> GetAsync(
        Expression<Func<T, bool>> filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
        string includeProperties = "",
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity asynchronously.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple entities asynchronously.
    /// </summary>
    /// <param name="entities">The list of entities to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity asynchronously.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(T entity);

    /// <summary>
    /// Updates multiple entities asynchronously.
    /// </summary>
    /// <param name="entities">The list of entities to update.</param>
    void UpdateRange(IEnumerable<T> entities);

    /// <summary>
    /// Deletes an entity by its ID asynchronously.
    /// </summary>
    /// <param name="id">The ID of the entity to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity asynchronously.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    void Delete(T entity);

    /// <summary>
    /// Deletes multiple entities asynchronously.
    /// </summary>
    /// <param name="entities">The list of entities to delete.</param>
    void DeleteRange(IEnumerable<T> entities);

    /// <summary>
    /// Detaches the specified entity from the database context, stopping it from being tracked.
    /// </summary>
    /// <param name="entity">The entity to detach.</param>
    void Detach(T entity);

    /// <summary>
    /// Saves all changes made in the context asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that represents an asynchronous operation returning the number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
