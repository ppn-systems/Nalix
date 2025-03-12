using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Notio.Common.Data;

/// <summary>
/// Generic repository interface for synchronous CRUD operations and querying entities.
/// </summary>
/// <typeparam name="T">The type of entity.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Retrieves all entities with optional pagination.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based index).</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <returns>A list of entities.</returns>
    IEnumerable<T> GetAll(int pageNumber = 1, int pageSize = 10);

    /// <summary>
    /// Counts the total number of entities.
    /// </summary>
    /// <returns>Total number of entities.</returns>
    int Count();

    /// <summary>
    /// Checks if any entity satisfies a given condition.
    /// </summary>
    /// <param name="predicate">The condition to check.</param>
    /// <returns>True if any entity matches the condition; otherwise, false.</returns>
    bool Any(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Retrieves an entity by its ID.
    /// </summary>
    /// <param name="id">The entity ID.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    T GetById(int id);

    /// <summary>
    /// Retrieves entities matching a specified condition with optional pagination.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <param name="pageNumber">The page number (1-based index).</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <returns>A list of matching entities.</returns>
    IEnumerable<T> Find(Expression<Func<T, bool>> predicate, int pageNumber = 1, int pageSize = 10);

    /// <summary>
    /// Retrieves entities with optional filtering, ordering, and pagination.
    /// </summary>
    /// <param name="filter">Optional filter condition.</param>
    /// <param name="orderBy">Optional ordering function.</param>
    /// <param name="includeProperties">Comma-separated related properties to include.</param>
    /// <param name="pageNumber">The page number (1-based index).</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <returns>A list of entities.</returns>
    IEnumerable<T> Get(
        Expression<Func<T, bool>> filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
        string includeProperties = "",
        int pageNumber = 1,
        int pageSize = 10);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    void Add(T entity);

    /// <summary>
    /// Adds multiple entities to the database.
    /// </summary>
    /// <param name="entities">The list of entities to add.</param>
    void AddRange(IEnumerable<T> entities);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(T entity);

    /// <summary>
    /// Updates multiple entities in the database.
    /// </summary>
    /// <param name="entities">The list of entities to update.</param>
    void UpdateRange(IEnumerable<T> entities);

    /// <summary>
    /// Deletes an entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to delete.</param>
    void Delete(int id);

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    void Delete(T entity);

    /// <summary>
    /// Deletes multiple entities from the database.
    /// </summary>
    /// <param name="entities">The list of entities to delete.</param>
    void DeleteRange(IEnumerable<T> entities);

    /// <summary>
    /// Saves all changes made in the context.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    int SaveChanges();
}
