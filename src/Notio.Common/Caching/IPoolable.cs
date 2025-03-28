namespace Notio.Common.Caching;

/// <summary>
/// Interface for objects that can be pooled.
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// Resets an <see cref="IPoolable"/> instance before it is returned to the pool.
    /// </summary>
    void ResetForPool();
}
