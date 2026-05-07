// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Codec.Tests")]
[assembly: InternalsVisibleTo("Nalix.Codec.Benchmarks")]
#endif

namespace Nalix.Codec.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    // These caches keep the reflection-backed helper methods cheap after the first lookup.
    private static readonly MethodInfo s_isReferenceOrContainsReferencesMethod;
    private static readonly MethodInfo s_unsafeSizeOfMethod;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<bool>> s_isRefCache;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<int>> s_sizeOfFnCache;

    static TypeMetadata()
    {
        // Resolve the generic runtime helpers once so later calls can just bind them to T.
        _ = typeof(IFixedSizeSerializable);
        s_isReferenceOrContainsReferencesMethod = typeof(RuntimeHelpers)
            .GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences))!;
        s_unsafeSizeOfMethod = typeof(System.Runtime.CompilerServices.Unsafe).GetMethod(nameof(Unsafe.SizeOf), BindingFlags.Public | BindingFlags.Static)!;

        s_isRefCache = new();
        s_sizeOfFnCache = new();
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsReferenceOrContainsReferences(Type type)
    {
        // Cache the closed generic delegate per type so repeated unmanaged checks do
        // not pay reflection costs more than once.
#if NALIX_AOT
        return IsReferenceOrContainsReferencesFallback(type, []);
#else
        Func<bool> fn = s_isRefCache.GetOrAdd(type, static t =>
        {
            MethodInfo method = s_isReferenceOrContainsReferencesMethod.MakeGenericMethod(t);
            return method.CreateDelegate<Func<bool>>();
        });

        return fn();
#endif
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int UnsafeSizeOf(Type type)
    {
        // Same pattern as above, but for size queries.
#if NALIX_AOT
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte => 1,
            TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => 8,
            TypeCode.Decimal => 16,
            TypeCode.DateTime => 8,
            TypeCode.Object when type == typeof(Guid) => 16,
            TypeCode.Object when type == typeof(TimeSpan) => 8,
            TypeCode.Object when type == typeof(TimeOnly) => 8,
            TypeCode.Object when type == typeof(DateOnly) => 4,
            TypeCode.Object when type == typeof(DateTimeOffset) => 16,
            _ when type.IsEnum => UnsafeSizeOf(Enum.GetUnderlyingType(type)),
            _ => IntPtr.Size
        };
#else
        Func<int> del = s_sizeOfFnCache.GetOrAdd(type, static t =>
        {
            MethodInfo method = s_unsafeSizeOfMethod.MakeGenericMethod(t);
            return method.CreateDelegate<Func<int>>();
        });

        return del();
#endif
    }

#if NALIX_AOT
    private static bool IsReferenceOrContainsReferencesFallback(
        Type type,
        System.Collections.Generic.HashSet<Type> visited)
    {
        if (!type.IsValueType)
        {
            return true;
        }

        if (type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(Guid) ||
            type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(DateTime) ||
            type == typeof(TimeSpan) || type == typeof(DateTimeOffset))
        {
            return false;
        }

        if (!visited.Add(type))
        {
            return false;
        }

        return true;
    }
#endif
}
