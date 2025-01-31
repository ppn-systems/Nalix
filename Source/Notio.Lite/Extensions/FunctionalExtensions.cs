using System;
using System.Collections.Generic;

namespace Notio.Lite.Extensions;

/// <summary>
/// Functional programming extension methods.
/// </summary>
public static class FunctionalExtensions
{
    /// <summary>
    /// Whens the specified condition.
    /// </summary>
    /// <typeparam name="T">The type of IEnumerable.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="fn">The function.</param>
    /// <returns>
    /// The IEnumerable.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// this
    /// or
    /// condition
    /// or
    /// fn.
    /// </exception>
    public static IEnumerable<T> When<T>(
        this IEnumerable<T> list,
        Func<bool> condition,
        Func<IEnumerable<T>, IEnumerable<T>> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(condition);

        return condition() ? fn(list) : list;
    }
}