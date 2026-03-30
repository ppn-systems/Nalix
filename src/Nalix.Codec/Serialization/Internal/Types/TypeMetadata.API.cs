// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Codec.Tests")]
[assembly: InternalsVisibleTo("Nalix.Codec.Benchmarks")]
#endif

namespace Nalix.Codec.Serialization.Internal.Types;

/// <summary>
/// Provides type-shape queries used by the serializer and formatter registry.
/// The helpers here answer questions like "is this unmanaged?" and "how large is it?"
/// without forcing every caller to repeat reflection work.
/// </summary>
[DebuggerNonUserCode]
[SkipLocalsInit]
[ExcludeFromCodeCoverage]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static partial class TypeMetadata
{
    /// <summary>
    /// Retrieves the size, in bytes, of the specified unmanaged type.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to evaluate.</typeparam>
    /// <returns>The size of the type in bytes.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T>() => Unsafe.SizeOf<T>();

    /// <summary>
    /// Determines whether the specified type is unmanaged.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is unmanaged; otherwise, false.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnmanaged<[DynamicallyAccessedMembers(PropertyAccess)] T>() => Cache<T>.IsUnmanaged;

    /// <summary>
    /// Determines whether the specified type is unmanaged by examining its structure and fields.
    /// </summary>
    /// <param name="type">The type to check for unmanaged status.</param>
    /// <returns>True if the type is unmanaged; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is null.</exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnmanaged(Type type)
        => type is not null &&
           type.IsValueType &&
           !IsReferenceOrContainsReferences(type);

    /// <summary>
    /// Determines whether the specified type is nullable.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is nullable; otherwise, false.</returns>
    [Pure]
    public static bool IsNullable<[DynamicallyAccessedMembers(PropertyAccess)] T>() => Cache<T>.IsNullable;

    /// <summary>
    /// Determines whether the type is a reference type or nullable.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is a reference type or nullable; otherwise, false.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceOrNullable<[DynamicallyAccessedMembers(PropertyAccess)] T>() => Cache<T>.IsReference || Cache<T>.IsNullable;

    /// <summary>
    /// Attempts to retrieve the fixed or unmanaged size of a type.
    /// </summary>
    /// <typeparam name="T">The type to evaluate.</typeparam>
    /// <param name="size">The fixed or unmanaged size of the type.</param>
    /// <returns>The corresponding <see cref="TypeKind"/> value.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeKind TryGetFixedOrUnmanagedSize<[DynamicallyAccessedMembers(PropertyAccess)] T>(out int size)
    {
        if (Cache<T>.IsUnmanagedSZArray)
        {
            size = Cache<T>.UnmanagedSZArrayElementSize;
            return TypeKind.UnmanagedSZArray;
        }
        else if (Cache<T>.IsFixedSizeSerializable)
        {
            size = Cache<T>.SerializableFixedSize;
            return TypeKind.FixedSizeSerializable;
        }

        size = 0;
        return TypeKind.None;
    }

    #region Private Methods

    public static void RecursiveWarmupFields(Type type, HashSet<Type>? visited = null, bool warmCurrentType = false)
    {
        // Walk the transitive closure of field types so formatter registration can be
        // preheated without revisiting the same type over and over.
        visited ??= [];
        if (!visited.Add(type))
        {
            return;
        }

        // The formatter constructor that calls into this method is already building the
        // root formatter. Re-entering FormatterProvider.Get<T>() for that same root type
        // would instantiate the formatter again and recurse forever for value types such
        // as Snowflake. Only dependency types should be proactively warmed here.
        if (warmCurrentType && !type.IsPrimitive && !type.IsEnum && type != typeof(string))
        {
            _ = typeof(FormatterProvider).GetMethod("Get")!.MakeGenericMethod(type).Invoke(null, null);
        }

        if (type.IsPrimitive || type.IsEnum || type == typeof(string))
        {
            return;
        }

        if (type.IsArray)
        {
            // Arrays depend on their element type, so recurse into that instead of the wrapper.
            RecursiveWarmupFields(type.GetElementType()!, visited, warmCurrentType: true);
            return;
        }
        if (type.IsGenericType)
        {
            Type def = type.GetGenericTypeDefinition();
            foreach (Type ga in type.GetGenericArguments())
            {
                // Generic arguments may themselves need formatter warmup.
                RecursiveWarmupFields(ga, visited, warmCurrentType: true);
            }

            // Standard collections (Dictionary, List, etc.) have specialized formatters
            // and should NOT have their internal fields (like Dictionary.Entry) scanned.
            if (def == typeof(List<>) ||
                def == typeof(Dictionary<,>) ||
                def == typeof(HashSet<>) ||
                def == typeof(Queue<>) ||
                def == typeof(Stack<>))
            {
                return;
            }
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (field.FieldType == type)
            {
                // Self-referential fields are legal, but we skip them to avoid infinite recursion.
                continue;
            }

            // Recurse into nested field types so the full graph is warmed, not just the root type.
            RecursiveWarmupFields(field.FieldType, visited, warmCurrentType: true);
        }
    }

    #endregion Private Method
}
