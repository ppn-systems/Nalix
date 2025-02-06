using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Notio.Lite.Reflection;

/// <summary>
/// Represents a generic interface to store getters and setters for high speed access to properties.
/// </summary>
public interface IPropertyProxy
{
    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    Type PropertyType { get; }

    /// <summary>
    /// Gets the associated reflection property info.
    /// </summary>
    PropertyInfo Property { get; }

    /// <summary>
    /// Gets the type owning this property proxy.
    /// </summary>
    Type EnclosingType { get; }

    /// <summary>
    /// Gets the property value via a stored delegate.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The property value.</returns>
    object? GetValue(object instance);

    /// <summary>
    /// Sets the property value via a stored delegate.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="value">The value.</param>
    void SetValue(object instance, object? value);
}

/// <summary>
/// The concrete and hidden implementation of the <see cref="IPropertyProxy"/> implementation.
/// </summary>
/// <seealso cref="IPropertyProxy" />
/// <remarks>
/// Initializes a new instance of the <see cref="PropertyInfoProxy"/> class.
/// </remarks>
/// <param name="declaringType">Type of the declaring.</param>
/// <param name="propertyInfo">The property information.</param>
internal sealed class PropertyInfoProxy(Type declaringType, PropertyInfo propertyInfo) : IPropertyProxy
{
    private readonly Func<object, object>? _getter = CreateLambdaGetter(declaringType, propertyInfo);
    private readonly Action<object, object?>? _setter = CreateLambdaSetter(declaringType, propertyInfo);

    /// <inheritdoc />
    public PropertyInfo Property { get; } = propertyInfo;

    /// <inheritdoc />
    public Type EnclosingType { get; } = declaringType;

    /// <inheritdoc />
    public string Name => Property.Name;

    /// <inheritdoc />
    public Type PropertyType => Property.PropertyType;

    /// <inheritdoc />
    public object? GetValue(object instance) => _getter?.Invoke(instance);

    /// <inheritdoc />
    public void SetValue(object instance, object? value) => _setter?.Invoke(instance, value);

    private static Func<object, object>? CreateLambdaGetter(Type instanceType, PropertyInfo propertyInfo)
    {
        if (!propertyInfo.CanRead)
            return null;

        var instanceParameter = Expression.Parameter(typeof(object), "instance");
        var typedInstance = Expression.Convert(instanceParameter, instanceType);
        var property = Expression.Property(typedInstance, propertyInfo);
        var convert = Expression.Convert(property, typeof(object));
        var dynamicGetter = (Func<object, object>)Expression.Lambda(convert, instanceParameter).Compile();

        return dynamicGetter;
    }

    private static Action<object, object?>? CreateLambdaSetter(Type instanceType, PropertyInfo propertyInfo)
    {
        if (!propertyInfo.CanWrite)
            return null;

        var instanceParameter = Expression.Parameter(typeof(object), "instance");
        var valueParameter = Expression.Parameter(typeof(object), "value");

        var typedInstance = Expression.Convert(instanceParameter, instanceType);
        var property = Expression.Property(typedInstance, propertyInfo);
        var propertyValue = Expression.Convert(valueParameter, propertyInfo.PropertyType);

        var body = Expression.Assign(property, propertyValue);
        var dynamicSetter = Expression.Lambda<Action<object, object?>>(body, instanceParameter, valueParameter).Compile();

        return dynamicSetter;
    }
}