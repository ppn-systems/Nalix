using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Serialization.Internal.Reflection;

/// <summary>
/// A thread-safe cache of constructors belonging to a given type.
/// </summary>
internal class ConstructorTypeCache : TypeCache<Tuple<ConstructorInfo, ParameterInfo[]>>
{
    private static readonly BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.Instance;
    private static readonly BindingFlags NonPublicBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// Gets the default cache.
    /// </summary>
    /// <value>
    /// The default cache.
    /// </value>
    internal static Lazy<ConstructorTypeCache> DefaultCache { get; } =
        new Lazy<ConstructorTypeCache>(() => new ConstructorTypeCache());

    /// <summary>
    /// Retrieves all constructors order by the number of parameters ascending.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="includeNonPublic">if set to <c>true</c> [include non public].</param>
    /// <returns>
    /// A collection with all the constructors in the given type.
    /// </returns>
    internal IEnumerable<Tuple<ConstructorInfo, ParameterInfo[]>> RetrieveAllConstructors(Type type, bool includeNonPublic = false)
        => Retrieve(type, GetConstructors(includeNonPublic));

    private static Func<Type, IEnumerable<Tuple<ConstructorInfo, ParameterInfo[]>>> GetConstructors(bool includeNonPublic)
            => t =>
            {
                var bindingFlags = includeNonPublic ? NonPublicBindingFlags : DefaultBindingFlags;
                return t.GetConstructors(bindingFlags)
                    .Select(x => Tuple.Create(x, x.GetParameters()))
                    .OrderBy(x => x.Item2.Length);
            };
}