using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Notio.Logging.Internal;

/// <summary>
/// Provides pooling for StringBuilder instances to reduce allocations.
/// </summary>
internal static class StringBuilderPool
{
    // Use array pool for efficient reuse
    private static readonly ArrayPool<StringBuilder> s_pool = ArrayPool<StringBuilder>.Create(20, 50);

    /// <summary>
    /// Rents a StringBuilder from the pool with the specified capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Rent(int capacity = 256)
    {
        StringBuilder builder = s_pool.Rent(1)[0];
        if (builder == null)
        {
            builder = new StringBuilder(capacity);
        }
        else
        {
            builder.Clear();
            if (builder.Capacity < capacity)
                builder.EnsureCapacity(capacity);
        }

        return builder;
    }

    /// <summary>
    /// Returns a StringBuilder to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(StringBuilder builder)
    {
        if (builder == null) return;

        // Clear the builder to avoid leaking sensitive data
        builder.Clear();

        // Return to pool
        s_pool.Return([builder], clearArray: false);
    }
}
