using Notio.Lite.Reflection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Notio.Lite.Extensions;

/// <summary>
/// Provides functionality to access <see cref="IPropertyProxy"/> objects
/// associated with types. Getters and setters are stored as delegates compiled
/// from constructed lambda expressions for fast access.
/// </summary>
public static class PropertyProxyExtensions
{
    private static readonly Lock SyncLock = new();
    private static readonly Dictionary<Type, Dictionary<string, IPropertyProxy>> ProxyCache = new(32);

    /// <summary>
    /// Gets the property proxies associated with a given type.
    /// </summary>
    /// <param name="t">The type to retrieve property proxies from.</param>
    /// <returns>A dictionary with property names as keys and <see cref="IPropertyProxy"/> objects as values.</returns>
    public static Dictionary<string, IPropertyProxy> PropertyProxies(
        [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] this Type t)
    {
        ArgumentNullException.ThrowIfNull(t);

        // Manually get properties without triggering dynamic member analysis
        var properties = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        lock (SyncLock)
        {
            if (ProxyCache.TryGetValue(t, out Dictionary<string, IPropertyProxy>? value))
                return value;

            var result = new Dictionary<string, IPropertyProxy>(properties.Length, StringComparer.InvariantCultureIgnoreCase);
            foreach (var propertyInfo in properties)
            {
                result[propertyInfo.Name] = new PropertyInfoProxy(t, propertyInfo);
            }

            ProxyCache[t] = result;
            return result;
        }
    }

    /// <summary>
    /// Gets the property proxy given the property name.
    /// </summary>
    /// <typeparam name="T">The type of instance to extract proxies from.</typeparam>
    /// <param name="obj">The instance to extract proxies from.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns>The associated <see cref="IPropertyProxy"/></returns>
    public static IPropertyProxy? PropertyProxy<T>(this T obj, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        var proxies = (obj?.GetType() ?? typeof(T)).PropertyProxies();

        return proxies?.ContainsKey(propertyName) == true ? proxies[propertyName] : null;
    }

    /// <summary>
    /// Reads the property value.
    /// </summary>
    /// <typeparam name="T">The type to get property proxies from.</typeparam>
    /// <param name="obj">The instance.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns>
    /// The value obtained from the associated <see cref="IPropertyProxy" />
    /// </returns>
    /// <exception cref="ArgumentNullException">obj.</exception>
    public static object? ReadProperty<T>(this T obj, string propertyName)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var proxy = obj.PropertyProxy(propertyName);
        return proxy?.GetValue(obj);
    }

    /// <summary>
    /// Writes the property value using the property proxy.
    /// </summary>
    /// <typeparam name="T">The type to get property proxies from.</typeparam>
    /// <param name="obj">The instance.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <param name="value">The value.</param>
    public static void WriteProperty<T>(this T obj, string propertyName, object value)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var proxy = obj.PropertyProxy(propertyName);
        proxy?.SetValue(obj, value);
    }

    private static string PropertyName<T, TV>(this Expression<Func<T, TV>> propertyExpression)
    {
        var memberExpression = propertyExpression.Body as MemberExpression
                               ?? (propertyExpression.Body as UnaryExpression)?.Operand as MemberExpression;

        return memberExpression == null
            ? throw new ArgumentException("Invalid property expression", nameof(propertyExpression))
            : memberExpression.Member.Name;
    }
}