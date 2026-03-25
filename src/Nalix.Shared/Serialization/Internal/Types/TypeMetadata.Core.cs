// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    [ThreadStatic]
    private static HashSet<Type>? t_visitedTypes;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<bool>> s_isRefCache;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<int>> s_sizeOfFnCache;

    static TypeMetadata()
    {
        _ = typeof(IFixedSizeSerializable);

        s_isRefCache = new();
        s_sizeOfFnCache = new();
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.NoInlining)]
    private static bool IsReferenceOrContainsReferences(Type type)
    {
        Func<bool> fn = s_isRefCache.GetOrAdd(type, static t =>
        {
            MethodInfo method = typeof(RuntimeHelpers)
                .GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences))!
                .MakeGenericMethod(t);

            System.Linq.Expressions.Expression<Func<bool>> call =
                System.Linq.Expressions.Expression.Lambda<Func<bool>>(
                    System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return fn();
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(
        MethodImplOptions.NoInlining)]
    private static int UnsafeSizeOf(Type type)
    {
        Func<int> del = s_sizeOfFnCache.GetOrAdd(type, static t =>
        {
            MethodInfo method = typeof(Unsafe)
                .GetMethod("SizeOf")!
                .MakeGenericMethod(t);

            System.Linq.Expressions.Expression<Func<int>> call =
                System.Linq.Expressions.Expression.Lambda<Func<int>>(
                    System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return del();
    }
}
