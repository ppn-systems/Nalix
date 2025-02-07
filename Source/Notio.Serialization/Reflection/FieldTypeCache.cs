using System;
using System.Collections.Generic;
using System.Reflection;

namespace Notio.Serialization.Reflection;

/// <summary>
/// A thread-safe cache of fields belonging to a given type
/// The Retrieve method is the most useful one in this class as it
/// calls the retrieval process if the type is not contained
/// in the cache.
/// </summary>
internal class FieldTypeCache : TypeCache<FieldInfo>
{
    /// <summary>
    /// Gets the default cache.
    /// </summary>
    /// <value>
    /// The default cache.
    /// </value>
    public static Lazy<FieldTypeCache> DefaultCache { get; } = new Lazy<FieldTypeCache>(() => new FieldTypeCache());

    /// <summary>
    /// Retrieves all fields.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>
    /// A collection with all the fields in the given type.
    /// </returns>
    public IEnumerable<FieldInfo> RetrieveAllFields(Type type)
        => Retrieve(type, t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
}