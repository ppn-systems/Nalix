// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Logging.Internal.Pooling;

/// <summary>
/// Provides object pooling for StringBuilder instances to reduce allocations.
/// </summary>
/// <remarks>
/// This class implements a high-performance pool for StringBuilder objects,
/// reducing garbage collection pressure in high-throughput logging scenarios.
/// </remarks>
internal static class StringBuilderPool
{
    #region Constants

    private const System.Int32 DefaultCapacity = 512;
    private const System.Int32 MaximumRetainedCapacity = 4096;

    #endregion Constants

    #region Fields

    [System.ThreadStatic]
    private static System.Text.StringBuilder? t_cachedInstance;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Rents a StringBuilder from the pool.
    /// </summary>
    /// <returns>A StringBuilder instance.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Text.StringBuilder Rent()
    {
        var sb = t_cachedInstance;
        if (sb != null)
        {
            t_cachedInstance = null;
            return sb;
        }

        return new System.Text.StringBuilder(DefaultCapacity);
    }

    /// <summary>
    /// Returns a StringBuilder to the pool.
    /// </summary>
    /// <param name="sb">The StringBuilder to return.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void Return(System.Text.StringBuilder sb)
    {
        if (sb == null)
        {
            return;
        }

        // Only pool if not too large
        if (sb.Capacity <= MaximumRetainedCapacity)
        {
            _ = sb.Clear();
            t_cachedInstance = sb;
        }
    }

    /// <summary>
    /// Rents a StringBuilder, executes an action, and returns it to the pool.
    /// </summary>
    /// <param name="action">The action to execute with the StringBuilder.</param>
    /// <returns>The resulting string.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.String Rent(System.Action<System.Text.StringBuilder> action)
    {
        System.ArgumentNullException.ThrowIfNull(action);

        var sb = Rent();
        try
        {
            action(sb);
            return sb.ToString();
        }
        finally
        {
            Return(sb);
        }
    }

    #endregion Public Methods
}
