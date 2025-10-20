// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Serialization;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    [System.ThreadStatic]
    private static System.Collections.Generic.HashSet<System.Type>? t_visitedTypes;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<bool>> s_isRefCache;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<int>> s_sizeOfFnCache;

    static TypeMetadata()
    {
        _ = typeof(IFixedSizeSerializable);

        s_isRefCache = new();
        s_sizeOfFnCache = new();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool IsReferenceOrContainsReferences(System.Type type)
    {
        System.Func<bool> fn = s_isRefCache.GetOrAdd(type, static t =>
        {
            System.Reflection.MethodInfo method = typeof(System.Runtime.CompilerServices.RuntimeHelpers)
                .GetMethod(nameof(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences))!
                .MakeGenericMethod(t);

            System.Linq.Expressions.Expression<System.Func<bool>> call =
                System.Linq.Expressions.Expression.Lambda<System.Func<bool>>(
                    System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return fn();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int UnsafeSizeOf(System.Type type)
    {
        System.Func<int> del = s_sizeOfFnCache.GetOrAdd(type, static t =>
        {
            System.Reflection.MethodInfo method = typeof(System.Runtime.CompilerServices.Unsafe)
                .GetMethod("SizeOf")!
                .MakeGenericMethod(t);

            System.Linq.Expressions.Expression<System.Func<int>> call =
                System.Linq.Expressions.Expression.Lambda<System.Func<int>>(
                    System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return del();
    }
}
