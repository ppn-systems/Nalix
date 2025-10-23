// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Repositories;

/// <summary>
/// Generic repository interface for synchronous CRUD operations and querying entities.
/// </summary>
/// <typeparam name="T">The type of entity.</typeparam>
public interface IRepositoryBase<T> where T : class
{
    /// <summary>
    /// Updates an existing entity asynchronously.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(T entity);

    /// <summary>
    /// Updates multiple entities asynchronously.
    /// </summary>
    /// <param name="entities">The list of entities to update.</param>
    void UpdateRange(System.Collections.Generic.IEnumerable<T> entities);

    /// <summary>
    /// Deletes an entity asynchronously.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    void Delete(T entity);

    /// <summary>
    /// Deletes multiple entities asynchronously.
    /// </summary>
    /// <param name="entities">The list of entities to delete.</param>
    void DeleteRange(System.Collections.Generic.IEnumerable<T> entities);

    /// <summary>
    /// Detaches the specified entity from the database context, stopping it from being tracked.
    /// </summary>
    /// <param name="entity">The entity to detach.</param>
    void Detach(T entity);
}
