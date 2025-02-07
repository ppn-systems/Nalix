using Notio.Serialization.Reflection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Notio.Serialization.Extensions;

/// <summary>
/// Provides functionality to access <see cref="IPropertyProxy"/> objects
/// associated with types. Getters and setters are stored as delegates compiled
/// from constructed lambda expressions for fast access.
/// </summary>
internal static class PropertyProxyExtensions
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, IPropertyProxy>> ProxyCache = new();

    /// <summary>
    /// Gets the property proxies associated with a given type.
    /// </summary>
    internal static Dictionary<string, IPropertyProxy> PropertyProxies(
        [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] this Type t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return ProxyCache.GetOrAdd(t, static type =>
        {
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var result = new Dictionary<string, IPropertyProxy>(properties.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var propertyInfo in properties)
                result[propertyInfo.Name] = new PropertyInfoProxy(type, propertyInfo);

            return result;
        });
    }

    /// <summary>
    /// Reads the property value.
    /// </summary>
    internal static object? ReadProperty<T>(this T obj, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(propertyName);

        return obj.GetType().PropertyProxies().TryGetValue(propertyName, out IPropertyProxy? proxy)
            ? proxy.GetValue(obj) : null;
    }

    /// <summary>
    /// Writes the property value using the property proxy.
    /// </summary>
    internal static void WriteProperty<T>(this T obj, string propertyName, object value)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(propertyName);

        if (obj.GetType().PropertyProxies().TryGetValue(propertyName, out IPropertyProxy? proxy))
            proxy.SetValue(obj, value);
    }
}