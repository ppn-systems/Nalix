using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Lite.Mappers;

/// <summary>
/// Represents an object map.
/// </summary>
/// <typeparam name="TSource">The type of the source.</typeparam>
/// <typeparam name="TDestination">The type of the destination.</typeparam>
/// <seealso cref="IObjectMap" />
public class ObjectMap<TSource, TDestination> : IObjectMap
{
    internal ObjectMap(IEnumerable<PropertyInfo> intersect)
    {
        SourceType = typeof(TSource);
        DestinationType = typeof(TDestination);
        Map = intersect
            .Where(property => property != null)
            .ToDictionary(
                property => DestinationType.GetProperty(property.Name)!,
                property => new List<PropertyInfo> { SourceType.GetProperty(property.Name)! });
    }

    /// <inheritdoc/>
    public Dictionary<PropertyInfo, List<PropertyInfo>> Map { get; }

    /// <inheritdoc/>
    public Type SourceType { get; }

    /// <inheritdoc/>
    public Type DestinationType { get; }
}