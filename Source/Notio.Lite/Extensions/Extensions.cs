using Notio.Lite.Formatters;
using Notio.Lite.Mappers;
using Notio.Lite.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Notio.Lite.Extensions;

/// <summary>
/// Extension methods.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Iterates over the public, instance, readable properties of the source and
    /// tries to write a compatible value to a public, instance, writable property in the destination.
    /// </summary>
    /// <typeparam name="T">The type of the source.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="target">The target.</param>
    /// <param name="ignoreProperties">The ignore properties.</param>
    /// <returns>
    /// Number of properties that was copied successful.
    /// </returns>
    public static int CopyPropertiesTo<T>(this T source, object target, params string[]? ignoreProperties)
        where T : class =>
        ObjectMapper.Copy(source, target, GetCopyableProperties(target), ignoreProperties);

    /// <summary>
    /// Copies the properties to new instance of T.
    /// </summary>
    /// <typeparam name="T">The new object type.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="ignoreProperties">The ignore properties.</param>
    /// <returns>
    /// The specified type with properties copied.
    /// </returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static T CopyPropertiesToNew<T>(this object source, string[]? ignoreProperties = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var target = Activator.CreateInstance<T>();
        ObjectMapper.Copy(source, target, GetCopyableProperties(target), ignoreProperties);

        return target;
    }

    /// <summary>
    /// Gets the copyable properties.
    ///
    /// If there is no properties with the attribute <c>AttributeCache</c> returns all the properties.
    /// </summary>
    /// <param name="this">The object.</param>
    /// <returns>
    /// Array of properties.
    /// </returns>
    /// <exception cref="ArgumentNullException">model.</exception>
    /// <seealso cref="AttributeCache"/>
    public static IEnumerable<string> GetCopyableProperties(this object @this)
    {
        ArgumentNullException.ThrowIfNull(@this);

        var collection = PropertyTypeCache.DefaultCache.Value
            .RetrieveAllProperties(@this.GetType(), true);

        var properties = collection
            .Select(x => new
            {
                x.Name,
                HasAttribute = AttributeCache.DefaultCache.Value.RetrieveOne<CopyableAttribute>(x) != null,
            })
            .Where(x => x.HasAttribute)
            .Select(x => x.Name);

        return properties.Any()
            ? properties
            : collection.Select(x => x.Name);
    }

    internal static void CreateTarget(
        this object source,
        Type targetType,
        bool includeNonPublic,
        ref object? target)
    {
        switch (source)
        {
            // do nothing. Simply skip creation
            case string _:
                break;

            // When using arrays, there is no default constructor, attempt to build a compatible array
            case IList sourceObjectList when targetType.IsArray:
                var elementType = targetType.GetElementType();

                if (elementType != null)
                    target = Array.CreateInstance(elementType, sourceObjectList.Count);
                break;

            default:
                var constructors = ConstructorTypeCache.DefaultCache.Value
                    .RetrieveAllConstructors(targetType, includeNonPublic);

                // Try to check if empty constructor is available
                if (constructors.Any(x => x.Item2.Length == 0))
                {
                    target = Activator.CreateInstance(targetType, includeNonPublic);
                }
                else
                {
                    var firstCtor = constructors
                        .OrderBy(x => x.Item2.Length)
                        .FirstOrDefault();

                    target = Activator.CreateInstance(targetType,
                        firstCtor?.Item2.Select(arg => arg.GetType().GetDefault()).ToArray());
                }

                break;
        }
    }

    internal static string GetNameWithCase(this string name, JsonSerializerCase jsonSerializerCase) =>
        jsonSerializerCase switch
        {
            JsonSerializerCase.PascalCase => char.ToUpperInvariant(name[0]) + name[1..],
            JsonSerializerCase.CamelCase => char.ToLowerInvariant(name[0]) + name[1..],
            JsonSerializerCase.None => name,
            _ => throw new ArgumentOutOfRangeException(nameof(jsonSerializerCase), jsonSerializerCase, null)
        };
}