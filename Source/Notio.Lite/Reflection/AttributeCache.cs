using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Lite.Reflection;

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
/// <param name="propertyCache">The property cache object.</param>
public class AttributeCache(PropertyTypeCache? propertyCache = null)
{
    private readonly Lazy<ConcurrentDictionary<Tuple<object, Type>, IEnumerable<object>>> _data =
        new(() => new ConcurrentDictionary<Tuple<object, Type>, IEnumerable<object>>(), true);

    /// <summary>
    /// Gets the default cache.
    /// </summary>
    /// <value>
    /// The default cache.
    /// </value>
    public static Lazy<AttributeCache> DefaultCache { get; } = new Lazy<AttributeCache>(() => new AttributeCache());

    /// <summary>
    /// A PropertyTypeCache object for caching properties and their attributes.
    /// </summary>
    public PropertyTypeCache PropertyTypeCache { get; } = propertyCache ?? PropertyTypeCache.DefaultCache.Value;

    /// <summary>
    /// Gets specific attributes from a member constrained to an attribute.
    /// </summary>
    /// <typeparam name="T">The type of the attribute to be retrieved.</typeparam>
    /// <param name="member">The member.</param>
    /// <param name="inherit"><c>true</c> to inspect the ancestors of element; otherwise, <c>false</c>.</param>
    /// <returns>An array of the attributes stored for the specified type.</returns>
    public IEnumerable<object> Retrieve<T>(MemberInfo member, bool inherit = false)
        where T : Attribute
    {
        ArgumentNullException.ThrowIfNull(member);

        return Retrieve(new Tuple<object, Type>(member, typeof(T)), t => member.GetCustomAttributes<T>(inherit));
    }

    /// <summary>
    /// Gets all attributes of a specific type from a member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="type">The attribute type.</param>
    /// <param name="inherit"><c>true</c> to inspect the ancestors of element; otherwise, <c>false</c>.</param>
    /// <returns>An array of the attributes stored for the specified type.</returns>
    public IEnumerable<object> Retrieve(MemberInfo member, Type type, bool inherit = false)
    {
        ArgumentNullException.ThrowIfNull(member);

        ArgumentNullException.ThrowIfNull(type);

        return Retrieve(
            new Tuple<object, Type>(member, type),
            t => member.GetCustomAttributes(type, inherit));
    }

    /// <summary>
    /// Gets one attribute of a specific type from a member.
    /// </summary>
    /// <typeparam name="T">The attribute type.</typeparam>
    /// <param name="member">The member.</param>
    /// <param name="inherit"><c>true</c> to inspect the ancestors of element; otherwise, <c>false</c>.</param>
    /// <returns>An attribute stored for the specified type.</returns>
    public T? RetrieveOne<T>(MemberInfo member, bool inherit = false)
        where T : Attribute
    {
        if (member == null)
            return default;

        var attr = Retrieve(
            new Tuple<object, Type>(member, typeof(T)),
            t => member.GetCustomAttributes(typeof(T), inherit));

        return ConvertToAttribute<T>(attr);
    }

    private static T? ConvertToAttribute<T>(IEnumerable<object> attr)
        where T : Attribute
    {
        if (attr?.Any() != true)
            return default;

        return attr.Count() == 1
            ? (T)Convert.ChangeType(attr.First(), typeof(T))
            : throw new AmbiguousMatchException("Multiple custom attributes of the same type found.");
    }

    private IEnumerable<object> Retrieve(Tuple<object, Type> key, Func<Tuple<object, Type>, IEnumerable<object>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return _data.Value.GetOrAdd(key, k => factory.Invoke(k).Where(item => item != null));
    }
}