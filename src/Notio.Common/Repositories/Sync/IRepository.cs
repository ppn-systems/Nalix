using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Notio.Common.Repositories.Sync;

/// <summary>
/// Generic repository interface for synchronous CRUD operations and querying entities.
/// </summary>
/// <typeparam name="T">The type of entity.</typeparam>
public interface IRepository<T> : IRepositoryBase<T> where T : class
{
    /// <summary>
    /// Retrieves all entities with optional pagination.
    /// </summary>
    /// <param name="pageNumber">The page Number (1-based index).</param>
    /// <param name="pageSize">The Number of records per page.</param>
    /// <returns>A list of entities.</returns>
    IEnumerable<T> GetAll(int pageNumber = 1, int pageSize = 10);

    /// <summary>
    /// Counts the total Number of entities.
    /// </summary>
    /// <returns>Total Number of entities.</returns>
    int Count();

    /// <summary>
    /// Checks if any entity satisfies a given condition.
    /// </summary>
    /// <param name="predicate">The condition to check.</param>
    /// <returns>True if any entity matches the condition; otherwise, false.</returns>
    bool Any(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Checks whether an entity with the specified Number exists in the database.
    /// </summary>
    /// <param name="id">The Number of the entity to check.</param>
    /// <returns><c>true</c> if an entity with the specified Number exists; otherwise, <c>false</c>.</returns>
    bool Exists(int id);

    /// <summary>
    /// Retrieves an entity by its Number.
    /// </summary>
    /// <param name="id">The entity Number.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    T GetById(int id);

    /// <summary>
    /// Retrieves the first entity that matches the specified condition, or null if no match is found.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <returns>The first matching entity, or null if no match is found.</returns>
    T GetFirstOrDefault(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Retrieves entities matching a specified condition with optional pagination.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <param name="pageNumber">The page Number (1-based index).</param>
    /// <param name="pageSize">The Number of records per page.</param>
    /// <returns>A list of matching entities.</returns>
    IEnumerable<T> Find(Expression<Func<T, bool>> predicate, int pageNumber = 1, int pageSize = 10);

    /// <summary>
    /// Retrieves entities with optional filtering, ordering, and pagination.
    /// </summary>
    /// <param name="filter">Optional filter condition.</param>
    /// <param name="orderBy">Optional ordering function.</param>
    /// <param name="includeProperties">Comma-separated related properties to include.</param>
    /// <param name="pageNumber">The page Number (1-based index).</param>
    /// <param name="pageSize">The Number of records per page.</param>
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
    /// Deletes an entity by its Number.
    /// </summary>
    /// <param name="id">The Number of the entity to delete.</param>
    void Delete(int id);

    /// <summary>
    /// Saves all changes made in the context.
    /// </summary>
    /// <returns>The Number of state entries written to the database.</returns>
    int SaveChanges();
}
