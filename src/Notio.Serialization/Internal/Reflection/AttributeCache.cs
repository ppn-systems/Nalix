using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Serialization.Internal.Reflection;

/// <summary>
/// A thread-safe cache of attributes belonging to a given key (MemberInfo or Type).
///
/// The Retrieve method is the most useful one in this class as it
/// calls the retrieval process if the type is not contained
/// in the cache.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AttributeCache"/> class.
/// </remarks>
internal class AttributeCache
{
    private readonly Lazy<ConcurrentDictionary<Tuple<object, Type>, IEnumerable<object>>> _data =
        new(() => new ConcurrentDictionary<Tuple<object, Type>, IEnumerable<object>>(), true);

    /// <summary>
    /// Gets the default cache.
    /// </summary>
    /// <value>
    /// The default cache.
    /// </value>
    internal static Lazy<AttributeCache> DefaultCache { get; } = new Lazy<AttributeCache>(() => new AttributeCache());

    /// <summary>
    /// Gets one attribute of a specific type from a member.
    /// </summary>
    /// <typeparam name="T">The attribute type.</typeparam>
    /// <param name="member">The member.</param>
    /// <param name="inherit"><c>true</c> to inspect the ancestors of element; otherwise, <c>false</c>.</param>
    /// <returns>An attribute stored for the specified type.</returns>
    internal T? RetrieveOne<T>(MemberInfo member, bool inherit = false)
        where T : Attribute
    {
        if (member == null)
            return default;

        var key = new Tuple<object, Type>(member, typeof(T));

        var attr = Retrieve(key, k => member.GetCustomAttributes(typeof(T), inherit));

        return attr?.SingleOrDefault() as T;
    }

    private IEnumerable<object> Retrieve(Tuple<object, Type> key, Func<Tuple<object, Type>, IEnumerable<object>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return _data.Value.GetOrAdd(key, k => factory.Invoke(k));
    }
}
