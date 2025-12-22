// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

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
    /// Gets the number of fields cached for the specified type.
    /// </summary>
    /// <returns>The count of cached fields.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int GetFieldCount() => _metadata.Length;

    /// <summary>
    /// Retrieves all cached field metadata as a span.
    /// </summary>
    /// <returns>A <see cref="System.ReadOnlySpan{T}"/> containing metadata for all fields.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.ReadOnlySpan<FieldSchema> GetFields()
        => System.MemoryExtensions.AsSpan(_metadata);

    /// <summary>
    /// Retrieves metadata for a field at the given index.
    /// </summary>
    /// <param name="index">The index of the field.</param>
    /// <returns>The metadata for the specified field.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldSchema GetField(int index) => _metadata[index];

    /// <summary>
    /// Retrieves metadata for a field by its name.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The metadata for the specified field.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// Thrown if the field name does not exist in the cache.
    /// </exception>
    /// <exception cref="System.ArgumentException"></exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static FieldSchema GetField(string fieldName)
    {
        System.ArgumentNullException.ThrowIfNull(fieldName);

        if (fieldName.Length is 0)
        {
            throw new System.ArgumentException("Field name cannot be empty.", nameof(fieldName));
        }
        else if (_fieldIndex.TryGetValue(fieldName, out int index))
        {
            return _metadata[index];
        }
        else
        {
            throw new System.Collections.Generic.KeyNotFoundException($"Field '{fieldName}' not found in type {typeof(T).FullName}");
        }
    }

    /// <summary>
    /// Checks whether a field with the given name exists in the cache.
    /// </summary>
    /// <param name="fieldName">The name of the field to check.</param>
    /// <returns><c>true</c> if the field exists; otherwise, <c>false</c>.</returns>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool HasField(string fieldName) => _fieldIndex.ContainsKey(fieldName);

    /// <summary>
    /// Gets the type of the specified field by its name.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The <see cref="System.Type"/> of the field.</returns>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// Thrown if the field name does not exist in the cache.
    /// </exception>
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Type GetFieldType(string fieldName) => GetField(fieldName).FieldType;
}
