// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

/// <summary>
/// Provides caching mechanisms for retrieving field metadata efficiently.
/// </summary>
/// <typeparam name="T">The type whose fields are being cached.</typeparam>
internal static partial class FieldCache<T>
{
    /// <summary>
    /// Retrieves all cached field metadata as a span.
    /// </summary>
    /// <returns>A <see cref="System.ReadOnlySpan{T}"/> containing metadata for all fields.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.ReadOnlySpan<FieldSchema> GetFields()
        => System.MemoryExtensions.AsSpan(_metadata);

    /// <summary>
    /// Gets the number of fields cached for the specified type.
    /// </summary>
    /// <returns>The count of cached fields.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Int32 GetFieldCount() => _metadata.Length;

    /// <summary>
    /// Retrieves metadata for a field at the given index.
    /// </summary>
    /// <param name="index">The index of the field.</param>
    /// <returns>The metadata for the specified field.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldSchema GetField(System.Int32 index) => _metadata[index];

    /// <summary>
    /// Retrieves metadata for a field by its name.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The metadata for the specified field.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// Thrown if the field name does not exist in the cache.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldSchema GetField(System.String fieldName)
    {
        return _fieldIndex.TryGetValue(fieldName, out System.Int32 index)
            ? _metadata[index]
            : throw new System.ArgumentException($"Field '{fieldName}' not found in {typeof(T).Name}");
    }

    /// <summary>
    /// Checks whether a field with the given name exists in the cache.
    /// </summary>
    /// <param name="fieldName">The name of the field to check.</param>
    /// <returns><c>true</c> if the field exists; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Boolean HasField(System.String fieldName) => _fieldIndex.ContainsKey(fieldName);

    /// <summary>
    /// Gets the type of the specified field by its name.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The <see cref="System.Type"/> of the field.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// Thrown if the field name does not exist in the cache.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Type GetFieldType(System.String fieldName) => GetField(fieldName).FieldType;
}
