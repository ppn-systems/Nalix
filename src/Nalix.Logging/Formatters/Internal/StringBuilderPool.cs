namespace Nalix.Logging.Formatters.Internal;

/// <summary>
/// Provides pooling for StringBuilder instances to reduce allocations.
/// </summary>
internal static class StringBuilderPool
{
    // Use array pool for efficient reuse
    private static readonly System.Buffers.ArrayPool<System.Text.StringBuilder> _spool;

    static StringBuilderPool() => _spool = System.Buffers.ArrayPool<System.Text.StringBuilder>.Create(20, 50);

    /// <summary>
    /// Rents a StringBuilder from the pool with the specified capacity.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Text.StringBuilder Rent(System.Int32 capacity = 256)
    {
        System.Text.StringBuilder builder = _spool.Rent(1)[0];
        if (builder == null)
        {
            builder = new System.Text.StringBuilder(capacity);
        }
        else
        {
            _ = builder.Clear();
            if (builder.Capacity < capacity)
            {
                _ = builder.EnsureCapacity(capacity);
            }
        }

        return builder;
    }

    /// <summary>
    /// Returns a StringBuilder to the pool.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Return(System.Text.StringBuilder builder)
    {
        if (builder == null)
        {
            return;
        }

        // Clear the builder to avoid leaking sensitive data
        _ = builder.Clear();

        // Return to pool
        _spool.Return([builder], clearArray: false);
    }
}
