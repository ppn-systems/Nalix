using Notio.Serialization.Internal.Mappers;
using Notio.Serialization.Internal.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Notio.Serialization.Internal.Extensions;

/// <summary>
/// Extension methods.
/// </summary>
internal static class Extensions
{
    /// <summary>
    /// Converts an array of bytes to a base-64 encoded string.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A <see cref="string" /> converted from an array of bytes.</returns>
    public static string ToBase64(this byte[] bytes) => Convert.ToBase64String(bytes);

    /// <summary>
    /// Converts a set of hexadecimal characters (uppercase or lowercase)
    /// to a byte array. String length must be a multiple of 2 and
    /// any prefix (such as 0x) has to be avoided for this to work properly.
    /// </summary>
    /// <param name="this">The hexadecimal.</param>
    /// <returns>
    /// A byte array containing the results of encoding the specified set of characters.
    /// </returns>
    /// <exception cref="ArgumentNullException">hex.</exception>
    public static byte[] ConvertHexadecimalToBytes(this string @this)
    {
        if (string.IsNullOrWhiteSpace(@this))
            throw new ArgumentNullException(nameof(@this));

        return Enumerable
            .Range(0, @this.Length / 2)
            .Select(x => Convert.ToByte(@this.Substring(x * 2, 2), 16))
            .ToArray();
    }

    /// <summary>
    /// Whens the specified condition.
    /// </summary>
    /// <typeparam name="T">The type of IEnumerable.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="condition">The condition.</param>
    /// <param name="fn">The function.</param>
    /// <returns>
    /// The IEnumerable.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// this
    /// or
    /// condition
    /// or
    /// fn.
    /// </exception>
    internal static IEnumerable<T> When<T>(
        this IEnumerable<T> list,
        Func<bool> condition,
        Func<IEnumerable<T>, IEnumerable<T>> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(condition);

        return condition() ? fn(list) : list;
    }

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
    internal static int CopyPropertiesTo<T>(this T source, object target, params string[]? ignoreProperties)
        where T : class =>
        ObjectMapper.Copy(source, target, target.GetCopyableProperties(), ignoreProperties);

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
    internal static T CopyPropertiesToNew<T>(this object source, string[]? ignoreProperties = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        var target = Activator.CreateInstance<T>();
        ObjectMapper.Copy(source, target, target.GetCopyableProperties(), ignoreProperties);

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
    private static IEnumerable<string> GetCopyableProperties(this object @this)
    {
        ArgumentNullException.ThrowIfNull(@this);

        var collection = PropertyTypeCache.DefaultCache.Value
            .RetrieveAllProperties(@this.GetType(), true);

        var propertyInfos = collection as PropertyInfo[] ?? collection.ToArray();
        var properties = propertyInfos
            .Select(x => new
            {
                x.Name,
                HasAttribute = AttributeCache.DefaultCache.Value.RetrieveOne<CopyableAttribute>(x) != null,
            })
            .Where(x => x.HasAttribute)
            .Select(x => x.Name);

        IEnumerable<string> copyableProperties = properties as string[] ?? properties.ToArray();
        return copyableProperties.Any()
            ? copyableProperties
            : propertyInfos.Select(x => x.Name);
    }

    /// <summary>
    /// Gets the value if exists or default.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="dict">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <returns>
    /// The value of the provided key or default.
    /// </returns>
    /// <exception cref="ArgumentNullException">dict.</exception>
    internal static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(dict);

        return dict.TryGetValue(key, out TValue? value) ? value : defaultValue;
    }

    /// <summary>
    /// Adds a key/value pair to the Dictionary if the key does not already exist.
    /// If the value is null, the key will not be updated.
    /// Based on <c>ConcurrentDictionary.GetOrAdd</c> method.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="dict">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="valueFactory">The value factory.</param>
    /// <returns>
    /// The value for the key.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// dict
    /// or
    /// valueFactory.
    /// </exception>
    internal static TValue? GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, Func<TKey, TValue> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(dict);
        ArgumentNullException.ThrowIfNull(valueFactory);

        if (dict.TryGetValue(key, out TValue? value)) return value;

        var newValue = valueFactory(key);
        if (Equals(newValue, null)) return default;
        value = newValue;
        dict[key] = value;

        return value;
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
                IEnumerable<Tuple<ConstructorInfo,ParameterInfo[]>> enumerable = constructors as Tuple<ConstructorInfo, ParameterInfo[]>[] ?? constructors.ToArray();
                if (enumerable.Any(x => x.Item2.Length == 0))
                {
                    target = Activator.CreateInstance(targetType, includeNonPublic);
                }
                else
                {
                    var firstCtor = enumerable
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
