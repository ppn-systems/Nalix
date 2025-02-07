using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Serialization.Internal.Mappers;

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
internal class ObjectMap<TSource, TDestination> : IObjectMap
{
    internal ObjectMap(IEnumerable<PropertyInfo> intersect)
    {
        SourceType = typeof(TSource);
        DestinationType = typeof(TDestination);

        var sourceProperties = SourceType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => intersect.Any(i => i.Name == p.Name))
            .ToDictionary(p => p.Name, p => p);

        var destinationProperties = DestinationType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => sourceProperties.ContainsKey(p.Name))
            .ToDictionary(p => p.Name, p => (Destination: p, Source: sourceProperties[p.Name]));

        Map = destinationProperties.Values
            .GroupBy(p => p.Destination)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Source).ToList());
    }

    /// <inheritdoc/>
    public Dictionary<PropertyInfo, List<PropertyInfo>> Map { get; }

    /// <inheritdoc/>
    public Type SourceType { get; }

    /// <inheritdoc/>
    public Type DestinationType { get; }
}