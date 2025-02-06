using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Lite.Mappers;

/// <summary>
/// Interface object map.
/// </summary>
public interface IObjectMap
{
    /// <summary>
    /// Gets or sets the map.
    /// </summary>
    Dictionary<PropertyInfo, List<PropertyInfo>> Map { get; }

    /// <summary>
    /// Gets or sets the type of the source.
    /// </summary>
    Type SourceType { get; }

    /// <summary>
    /// Gets or sets the type of the destination.
    /// </summary>
    Type DestinationType { get; }
}

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