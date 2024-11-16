namespace Nalix.Common.Caching;

/// <summary>
/// Provides a contract for managing reusable byte buffers of various sizes.
/// </summary>
/// <remarks>
/// Implementations of this interface should handle buffer allocation, pooling,
/// and returning to minimize memory allocations and improve performance.
/// </remarks>
public interface IBufferPool
{
    /// <summary>
    /// Gets the maximum buffer size available in the pool based on its configuration.
    /// </summary>
    System.Int32 MaxBufferSize { get; }

    /// <summary>
    /// Rents a buffer from the pool with the specified size.
    /// </summary>
    /// <param name="size">
    /// The requested size of the buffer in bytes. 
    /// Defaults to <c>256</c> if no value is provided.
    /// </param>
    /// <returns>
    /// A <see cref="System.Byte"/> array representing the rented buffer.
    /// </returns>
    System.Byte[] Rent(System.Int32 size = 256);

    /// <summary>
    /// Returns a previously rented buffer to the pool for reuse.
    /// </summary>
    /// <param name="buffer">
    /// The buffer to return. Must not be <see langword="null"/>.
    /// </param>
    void Return(System.Byte[] buffer);

    /// <summary>
    /// Retrieves memory allocation statistics for a specific buffer size.
    /// </summary>
    /// <param name="size">
    /// The buffer size (in bytes) to check.
    /// </param>
    /// <returns>
    /// A <see cref="System.Double"/> representing the allocation metric for the specified size.
    /// </returns>
    System.Double GetAllocationForSize(System.Int32 size);
}
