using Microsoft.EntityFrameworkCore;
using Notio.Database.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Notio.Database.Extensions;

public static partial class DbContextExtensions
{
    /// <summary>
    /// Get all active (not deleted) entities.
    /// </summary>
    public static IQueryable<T> Active<T>(this DbSet<T> dbSet)
        where T : BaseEntity
        => dbSet.Where(e => !e.IsDeleted);

    /// <summary>
    /// Get an entity by ID asynchronously.
    /// </summary>
    public static async Task<T> FindByIdAsync<T>(this DbSet<T> dbSet, int id)
        where T : class
        => await dbSet.FindAsync(id);

    /// <summary>
    /// Soft delete an entity.
    /// </summary>
    public static async Task SoftDeleteAsync<T>(this NotioContext context, T entity) where T : BaseEntity
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Add a list of entities in bulk.
    /// </summary>
    public static async Task AddRangeAsync<T>(this DbSet<T> dbSet,
        IEnumerable<T> entities, NotioContext context) where T : class
    {
        await dbSet.AddRangeAsync(entities);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Check if an entity exists by ID.
    /// </summary>
    public static async Task<bool> ExistsAsync<T>(this DbSet<T> dbSet, int id) where T : BaseEntity
        => await dbSet.AnyAsync(e => EF.Property<int>(e, "Id") == id);

    /// <summary>
    /// Count all active entities.
    /// </summary>
    public static async Task<int> CountActiveAsync<T>(this DbSet<T> dbSet) where T : BaseEntity
        => await dbSet.CountAsync(e => !e.IsDeleted);

    /// <summary>
    /// Get the latest N records from a table.
    /// </summary>
    public static async Task<List<T>> GetLatestAsync<T>(this DbSet<T> dbSet, int count) where T : BaseEntity
        => await dbSet.OrderByDescending(e => e.CreatedAt).Take(count).ToListAsync();
}