// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Internal.Reflection;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Accessors;

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
    [System.Diagnostics.DebuggerStepThrough]
    public abstract void Serialize(ref DataWriter writer, T obj);

    /// <summary>
    /// Deserializes một field vào object sử dụng field cache optimization.
    /// </summary>
    /// <param name="reader">Binary reader chứa serialized data.</param>
    /// <param name="obj">Object để populate data.</param>
    [System.Diagnostics.DebuggerStepThrough]
    public abstract void Deserialize(ref DataReader reader, T obj);

    /// <summary>
    /// Factory method tạo strongly typed field accessor.
    /// Sử dụng reflection nhưng cached cho performance.
    /// </summary>
    /// <param name="schema">Field schema từ FieldCache.</param>
    /// <param name="index">Field index cho fast access.</param>
    /// <returns>Optimized field accessor instance.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldAccessor<T> Create(FieldSchema schema, System.Int32 index)
    {
        System.ArgumentNullException.ThrowIfNull(schema.FieldInfo);

        try
        {
            // TODO: Cache reflection calls cho production performance
            System.Reflection.MethodInfo method = typeof(FieldAccessor<T>)
                .GetMethod(nameof(CreateTyped),
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic)
                ?? throw new System.InvalidOperationException("CreateTyped method not found");

            return (FieldAccessor<T>)method.MakeGenericMethod(schema.FieldType).Invoke(null, [index])!;
        }
        catch (System.Exception ex)
        {
            throw new System.InvalidOperationException($"Failed to create accessor for field {schema.Name}", ex);
        }
    }

    /// <summary>
    /// Generic helper method tạo FieldAccessorImpl.
    /// Private để enforce factory pattern usage.
    /// </summary>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static FieldAccessorImpl<T, TField> CreateTyped<TField>(System.Int32 index) => new(index);
}