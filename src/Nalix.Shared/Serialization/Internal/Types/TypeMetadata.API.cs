// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

/// <summary>
/// Provides metadata operations for serialization types.
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
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T>() => Unsafe.SizeOf<T>();

    /// <summary>
    /// Determines whether the specified type is unmanaged.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>True if the type is unmanaged; otherwise, false.</returns>
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public static bool IsUnmanaged<[DynamicallyAccessedMembers(PropertyAccess)] T>() => Cache<T>.IsUnmanaged;

    /// <summary>
    /// Determines whether the specified type is unmanaged by examining its structure and fields.
    /// </summary>
    /// <param name="type">The type to check for unmanaged status.</param>
    /// <returns>True if the type is unmanaged; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is null.</exception>
    [Pure]
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.NoInlining)]
    public static bool IsUnmanaged(Type type)
    {
        t_visitedTypes ??= [];

        if (!t_visitedTypes.Add(type))
        {
            return false;  // Circular reference detected
        }

        try
        {
            return type.IsValueType &&
                   Marshal.SizeOf(type) > 0 &&
                   Enumerable.All(type.GetFields(
                   BindingFlags.Instance |
                   BindingFlags.NonPublic |
                   BindingFlags.Public), f => IsUnmanaged(f.FieldType));
        }
        catch
        {
            return false;
        }
        finally
        {
            _ = t_visitedTypes.Remove(type);
        }
    }

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
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public static bool IsReferenceOrNullable<[DynamicallyAccessedMembers(PropertyAccess)] T>()
        => Cache<T>.IsReference || Cache<T>.IsNullable;

    /// <summary>
    /// Attempts to retrieve the fixed or unmanaged size of a type.
    /// </summary>
    /// <typeparam name="T">The type to evaluate.</typeparam>
    /// <param name="size">The fixed or unmanaged size of the type.</param>
    /// <returns>The corresponding <see cref="TypeKind"/> value.</returns>
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    /// Determines whether a given type is an anonymous type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is anonymous; otherwise, false.</returns>
    [Pure]
    [MethodImpl(
        MethodImplOptions.AggressiveInlining)]
    public static bool IsAnonymous(Type type)
    {
        // Anonymous types typically have no namespace
        bool hasNoNamespace = type.Namespace == null;

        // Anonymous types are usually sealed (cannot be inherited)
        bool isSealed = type.IsSealed;

        // Anonymous type names usually start with compiler-generated prefixes
        bool nameIndicatesAnonymous =
            type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal) ||
            type.Name.StartsWith("<>__AnonType", StringComparison.Ordinal) ||
            type.Name.StartsWith("VB$AnonymousType_", StringComparison.Ordinal); // For VB.NET

        // Anonymous types are marked with CompilerGeneratedAttribute
        bool isCompilerGenerated = type.IsDefined(
            typeof(CompilerGeneratedAttribute), inherit: false);

        return hasNoNamespace && isSealed && nameIndicatesAnonymous && isCompilerGenerated;
    }
}
