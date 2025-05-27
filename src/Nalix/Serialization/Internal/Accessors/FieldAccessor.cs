using Nalix.Serialization.Buffers;
using Nalix.Serialization.Internal.Reflection;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Accessors;

/// <summary>
/// Abstract base class cho field serialization, theo Strategy Pattern.
/// Generic design cho reusability across different object types.
/// </summary>
/// <typeparam name="T">Type của object chứa field.</typeparam>
internal abstract class FieldAccessor<T>
{
    /// <summary>
    /// Serializes một field của object sử dụng field cache optimization.
    /// </summary>
    /// <param name="writer">Binary writer cho serialization.</param>
    /// <param name="obj">Object chứa field cần serialize.</param>
    public abstract void Serialize(ref DataWriter writer, T obj);

    /// <summary>
    /// Deserializes một field vào object sử dụng field cache optimization.
    /// </summary>
    /// <param name="reader">Binary reader chứa serialized data.</param>
    /// <param name="obj">Object để populate data.</param>
    public abstract void Deserialize(ref DataReader reader, T obj);

    /// <summary>
    /// Factory method tạo strongly typed field accessor.
    /// Sử dụng reflection nhưng cached cho performance.
    /// </summary>
    /// <param name="schema">Field schema từ FieldCache.</param>
    /// <param name="index">Field index cho fast access.</param>
    /// <returns>Optimized field accessor instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldAccessor<T> Create(FieldSchema schema, int index)
    {
        ArgumentNullException.ThrowIfNull(schema.FieldInfo);

        try
        {
            // TODO: Cache reflection calls cho production performance
            var method = typeof(FieldAccessor<T>)
                .GetMethod(nameof(CreateTyped), BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("CreateTyped method not found");

            var genericMethod = method.MakeGenericMethod(schema.FieldType);
            var result = genericMethod.Invoke(null, [index]);

            return (FieldAccessor<T>)result!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create accessor for field {schema.Name}", ex);
        }
    }

    /// <summary>
    /// Generic helper method tạo FieldAccessorImpl.
    /// Private để enforce factory pattern usage.
    /// </summary>
    private static FieldAccessorImpl<T, TField> CreateTyped<TField>(int index) => new(index);
}
