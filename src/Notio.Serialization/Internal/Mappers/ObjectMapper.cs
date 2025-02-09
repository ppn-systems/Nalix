using Notio.Serialization.Internal.Extensions;
using Notio.Serialization.Internal.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Serialization.Internal.Mappers;

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
internal partial class ObjectMapper
{
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
    internal static int Copy(
        object source, object target,
        IEnumerable<string>? propertiesToCopy = null,
        params string[]? ignoreProperties)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        return CopyInternal(target, GetSourceMap(source), propertiesToCopy, ignoreProperties);
    }

    private static int CopyInternal(
        object target,
        Dictionary<string, (Type Type, object Value)> sourceProperties,
        IEnumerable<string>? propertiesToCopy,
        IEnumerable<string>? ignoreProperties)
    {
        var propertyDict = PropertyTypeCache.DefaultCache.Value
            .RetrieveFilteredProperties(target.GetType(), true, x => x.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        // Loại bỏ bộ lọc nếu null
        var requiredSet = propertiesToCopy?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignoreSet = ignoreProperties?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return propertyDict
            .Where(p => sourceProperties.ContainsKey(p.Key) &&
                        (requiredSet == null || requiredSet.Contains(p.Key)) &&
                        (ignoreSet == null || !ignoreSet.Contains(p.Key)))
            .Sum(p => TrySetValue(p.Value, sourceProperties[p.Key], target) ? 1 : 0);
    }

    private static bool TrySetValue(PropertyInfo propertyInfo, (Type Type, object Value) property, object target)
    {
        try
        {
            var (type, value) = property;

            if (value == null || propertyInfo.PropertyType.IsAssignableFrom(type))
            {
                propertyInfo.SetValue(target, value);
                return true;
            }

            if (propertyInfo.PropertyType.IsEnum && Enum.IsDefined(propertyInfo.PropertyType, value))
            {
                propertyInfo.SetValue(target, Enum.ToObject(propertyInfo.PropertyType, value));
                return true;
            }

            if (type.IsValueType || propertyInfo.PropertyType != type)
                return propertyInfo.TrySetBasicType(value, target);

            if (propertyInfo.PropertyType.IsArray && value is IEnumerable<object> enumerableValue)
            {
                propertyInfo.TrySetArray(enumerableValue, target);
                return true;
            }

            propertyInfo.SetValue(target, GetValue(value, propertyInfo.PropertyType));
            return true;
        }
        catch
        {
            return false;
        }
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
                return source;

            case IList sourceList when target is IList targetList:
                var addMethod = targetType.GetMethods()
                    .FirstOrDefault(m => m.Name == Json.AddMethodName && m.IsPublic && m.GetParameters().Length == 1);

                if (addMethod == null) return target;

                foreach (var item in sourceList)
                {
                    try
                    {
                        targetList.Add(item.CopyPropertiesToNew<object>());
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }
                return target;

            default:
                source.CopyPropertiesTo(target!);
                return target;
        }
    }

    private static Dictionary<string, (Type Type, object Value)> GetSourceMap(object source)
    {
        return PropertyTypeCache.DefaultCache.Value
            .RetrieveFilteredProperties(source.GetType(), true, x => x.CanRead)
            .ToDictionary(p => p.Name, p => ((Type, object))(p.PropertyType, p.GetValue(source)!), StringComparer.OrdinalIgnoreCase);
    }
}