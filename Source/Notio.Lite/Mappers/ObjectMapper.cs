using Notio.Lite.Extensions;
using Notio.Lite.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Lite.Mappers;

/// <summary>
/// Represents an AutoMapper-like object to map from one object type
/// to another using defined properties map or using the default behaviour
/// to copy same named properties from one object to another.
///
/// The extension methods like CopyPropertiesTo use the default behaviour.
/// </summary>
/// <example>
/// The following code explains how to map an object's properties into an instance of type T.
/// <code>
/// using Notio.Mappers;
///
/// class Example
/// {
///     class Person
///     {
///         public string Name { get; set; }
///         public int Age { get; set; }
///     }
///
///     static void Main()
///     {
///         var obj = new { Name = "John", Age = 42 };
///
///         var person = Runtime.ObjectMapper.Map&lt;Person&gt;(obj);
///     }
/// }
/// </code>
///
/// The following code explains how to explicitly map certain properties.
/// <code>
/// using Notio.Mappers;
///
/// class Example
/// {
///     class User
///     {
///         public string Name { get; set; }
///         public Role Role { get; set; }
///     }
///
///     public class Role
///     {
///         public string Name { get; set; }
///     }
///
///     class UserDto
///     {
///         public string Name { get; set; }
///         public string Role { get; set; }
///     }
///
///     static void Main()
///     {
///         // create a User object
///         var person =
///             new User { Name = "Phillip", Role = new Role { Name = "Admin" } };
///
///         // create an Object Mapper
///         var mapper = new ObjectMapper();
///
///         // map the User's Role.Name to UserDto's Role
///         mapper.CreateMap&lt;User, UserDto&gt;()
///             .MapProperty(d => d.Role, x => x.Role.Name);
///
///         // apply the previous map and retrieve a UserDto object
///         var destination = mapper.Map&lt;UserDto&gt;(person);
///     }
/// }
/// </code>
/// </example>
public partial class ObjectMapper
{
    private static readonly Lazy<ObjectMapper> LazyInstance = new(() => new ObjectMapper());

    /// <summary>
    /// Gets the current.
    /// </summary>
    /// <value>
    /// The current.
    /// </value>
    public static ObjectMapper Current => LazyInstance.Value;

    /// <summary>
    /// Copies the specified source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="target">The target.</param>
    /// <param name="propertiesToCopy">The properties to copy.</param>
    /// <param name="ignoreProperties">The ignore properties.</param>
    /// <returns>
    /// Copied properties count.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// target.
    /// </exception>
    public static int Copy(
        object source,
        object target,
        IEnumerable<string>? propertiesToCopy = null,
        params string[]? ignoreProperties)
    {
        ArgumentNullException.ThrowIfNull(source);

        ArgumentNullException.ThrowIfNull(target);

        return CopyInternal(
            target,
            GetSourceMap(source),
            propertiesToCopy,
            ignoreProperties);
    }

    private static int CopyInternal(
        object target,
        Dictionary<string, Tuple<Type, object>> sourceProperties,
        IEnumerable<string>? propertiesToCopy,
        IEnumerable<string>? ignoreProperties)
    {
        // Filter properties
        var requiredProperties = propertiesToCopy?
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.ToLowerInvariant());

        var ignoredProperties = ignoreProperties?
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.ToLowerInvariant());

        var properties = PropertyTypeCache.DefaultCache.Value
            .RetrieveFilteredProperties(target.GetType(), true, x => x.CanWrite);

        return properties
            .Select(x => x.Name)
            .Distinct()
            .ToDictionary(x => x.ToLowerInvariant(), x => properties.First(y => y.Name == x))
            .Where(x => sourceProperties.ContainsKey(x.Key))
            .When(() => requiredProperties != null, q => q.Where(y => requiredProperties!.Contains(y.Key)))
            .When(() => ignoredProperties != null, q => q.Where(y => !ignoredProperties!.Contains(y.Key)))
            .ToDictionary(x => x.Value, x => sourceProperties[x.Key])
            .Sum(x => TrySetValue(x.Key, x.Value, target) ? 1 : 0);
    }

    private static bool TrySetValue(PropertyInfo propertyInfo, Tuple<Type, object> property, object target)
    {
        try
        {
            var (type, value) = property;

            if (type.IsEnum)
            {
                propertyInfo.SetValue(target,
                    Enum.ToObject(propertyInfo.PropertyType, value));

                return true;
            }

            if (type.IsValueType || propertyInfo.PropertyType != type)
                return propertyInfo.TrySetBasicType(value, target);

            if (propertyInfo.PropertyType.IsArray)
            {
                if (value is IEnumerable<object> enumerableValue)
                {
                    propertyInfo.TrySetArray(enumerableValue, target);
                    return true;
                }
                return false;
            }

            propertyInfo.SetValue(target, GetValue(value, propertyInfo.PropertyType));

            return true;
        }
        catch
        {
            // swallow
        }

        return false;
    }

    private static object? GetValue(object source, Type targetType)
    {
        if (source == null)
            return null;

        object? target = null;

        source.CreateTarget(targetType, false, ref target);

        switch (source)
        {
            case string _:
                target = source;
                break;

            case IList sourceList when target is IList targetList:
                var addMethod = targetType.GetMethods()
                    .FirstOrDefault(
                        m => m.Name == Lite.Formatters.Json.AddMethodName && m.IsPublic && m.GetParameters().Length == 1);

                if (addMethod == null) return target;

                var elementType = targetList.GetType().GetElementType();
                var isItemValueType = elementType != null && elementType.IsValueType;

                foreach (var item in sourceList)
                {
                    try
                    {
                        targetList.Add(isItemValueType
                            ? item
                            : item.CopyPropertiesToNew<object>());
                    }
                    catch
                    {
                        // ignored
                    }
                }

                break;

            default:
                source.CopyPropertiesTo(target!);
                break;
        }

        return target;
    }

    private static Dictionary<string, Tuple<Type, object>> GetSourceMap(object source)
    {
        // select distinct properties because they can be duplicated by inheritance
        var sourceProperties = PropertyTypeCache.DefaultCache.Value
            .RetrieveFilteredProperties(source.GetType(), true, x => x.CanRead)
            .ToArray();

        return sourceProperties
            .Select(x => x.Name)
            .Distinct()
            .ToDictionary(x => x.ToLowerInvariant(), x => Tuple
            .Create(sourceProperties
            .First(y => y.Name == x).PropertyType, (object?)sourceProperties
            .First(y => y.Name == x).GetValue(source)!));
    }
}