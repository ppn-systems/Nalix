using System;
using System.Collections.Generic;

namespace Notio.Lite.Extensions;

/// <summary>
/// Extension methods.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Gets the value if exists or default.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="dict">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>
    /// The value of the provided key or default.
    /// </returns>
    /// <exception cref="ArgumentNullException">dict.</exception>
    public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(dict);

        return dict.TryGetValue(key, out TValue? value) ? value : defaultValue;
    }

    /// <summary>
    /// Adds a key/value pair to the Dictionary if the key does not already exist.
    /// If the value is null, the key will not be updated.
    /// Based on <c>ConcurrentDictionary.GetOrAdd</c> method.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="dict">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="valueFactory">The value factory.</param>
    /// <returns>
    /// The value for the key.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// dict
    /// or
    /// valueFactory.
    /// </exception>
    public static TValue? GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(valueFactory);

        if (!dict.TryGetValue(key, out TValue? value))
        {
            var newValue = valueFactory(key);
            if (Equals(newValue, default)) return default;
            value = newValue;
            dict[key] = value;
        }

        return value;
    }
}