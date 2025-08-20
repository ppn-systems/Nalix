// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Repositories.Abstractions;

namespace Nalix.Common.Repositories.Sync;

/// <summary>
/// Generic repository interface for synchronous CRUD operations and querying entities.
/// </summary>
/// <typeparam name="T">The type of entity.</typeparam>
public interface IRepository<T> : IRepositoryBase<T> where T : class
{
    /// <summary>
    /// Retrieves all entities with optional pagination.
    /// </summary>
    /// <param name="pageNumber">The page TransportProtocol (1-based index).</param>
    /// <param name="pageSize">The TransportProtocol of records per page.</param>
    /// <returns>A list of entities.</returns>
    System.Collections.Generic.IEnumerable<T> GetAll(
        System.Int32 pageNumber = 1, System.Int32 pageSize = 10);

    /// <summary>
    /// Counts the total TransportProtocol of entities.
    /// </summary>
    /// <returns>Total TransportProtocol of entities.</returns>
    System.Int32 Count();

    /// <summary>
    /// Checks if any entity satisfies a given condition.
    /// </summary>
    /// <param name="predicate">The condition to check.</param>
    /// <returns>True if any entity matches the condition; otherwise, false.</returns>
    System.Boolean Any(
        System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> predicate);

    /// <summary>
    /// Checks whether an entity with the specified TransportProtocol exists in the database.
    /// </summary>
    /// <param name="id">The TransportProtocol of the entity to check.</param>
    /// <returns><c>true</c> if an entity with the specified TransportProtocol exists; otherwise, <c>false</c>.</returns>
    System.Boolean Exists(System.Int32 id);

    /// <summary>
    /// Retrieves an entity by its TransportProtocol.
    /// </summary>
    /// <param name="id">The entity TransportProtocol.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    T GetById(System.Int32 id);

    /// <summary>
    /// Retrieves the first entity that matches the specified condition, or null if no match is found.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <returns>The first matching entity, or null if no match is found.</returns>
    T GetFirstOrDefault(System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> predicate);

    /// <summary>
    /// Retrieves entities matching a specified condition with optional pagination.
    /// </summary>
    /// <param name="predicate">The filter condition.</param>
    /// <param name="pageNumber">The page TransportProtocol (1-based index).</param>
    /// <param name="pageSize">The TransportProtocol of records per page.</param>
    /// <returns>A list of matching entities.</returns>
    System.Collections.Generic.IEnumerable<T> Find(
        System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> predicate,
        System.Int32 pageNumber = 1, System.Int32 pageSize = 10);

    /// <summary>
    /// Retrieves entities with optional filtering, ordering, and pagination.
    /// </summary>
    /// <param name="filter">Optional filter condition.</param>
    /// <param name="orderBy">Optional ordering function.</param>
    /// <param name="includeProperties">Comma-separated related properties to include.</param>
    /// <param name="pageNumber">The page TransportProtocol (1-based index).</param>
    /// <param name="pageSize">The TransportProtocol of records per page.</param>
    /// <returns>A list of entities.</returns>
    System.Collections.Generic.IEnumerable<T> Get(
        System.Linq.Expressions.Expression<System.Func<T, System.Boolean>> filter = null,
        System.Func<System.Linq.IQueryable<T>, System.Linq.IOrderedQueryable<T>> orderBy = null,
        System.String includeProperties = "", System.Int32 pageNumber = 1, System.Int32 pageSize = 10);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    void Add(T entity);

    /// <summary>
    /// Adds multiple entities to the database.
    /// </summary>
    /// <param name="entities">The list of entities to add.</param>
    void AddRange(System.Collections.Generic.IEnumerable<T> entities);

    /// <summary>
    /// Deletes an entity by its TransportProtocol.
    /// </summary>
    /// <param name="id">The TransportProtocol of the entity to delete.</param>
    void Delete(System.Int32 id);

    /// <summary>
    /// Saves all changes made in the context.
    /// </summary>
    /// <returns>The TransportProtocol of state entries written to the database.</returns>
    System.Int32 SaveChanges();
}
