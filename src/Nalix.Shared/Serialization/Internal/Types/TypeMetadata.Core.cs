// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Serialization;


#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<System.Boolean>> s_isRefCache;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<System.Int32>> s_sizeOfFnCache;

    static TypeMetadata()
    {
        System.Type _ = typeof(IFixedSizeSerializable);

        s_isRefCache = new();
        s_sizeOfFnCache = new();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Boolean IsReferenceOrContainsReferences(System.Type type)
    {
        System.Func<System.Boolean> fn = s_isRefCache.GetOrAdd(type, static t =>
        {
            System.Reflection.MethodInfo method = typeof(System.Runtime.CompilerServices.RuntimeHelpers)
                .GetMethod(nameof(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences))!
                .MakeGenericMethod(t);

            System.Linq.Expressions.Expression<System.Func<System.Boolean>> call =
                System.Linq.Expressions.Expression.Lambda<System.Func<System.Boolean>>(
                    System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return fn();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Int32 UnsafeSizeOf(System.Type type)
    {
        System.Func<System.Int32> del = s_sizeOfFnCache.GetOrAdd(type, static t =>
        {
            System.Reflection.MethodInfo method = typeof(System.Runtime.CompilerServices.Unsafe)
                .GetMethod("SizeOf")!
                .MakeGenericMethod(t);

            System.Linq.Expressions.Expression<System.Func<System.Int32>> call =
                System.Linq.Expressions.Expression.Lambda<System.Func<System.Int32>>(
                    System.Linq.Expressions.Expression.Call(null, method));

            return call.Compile();
        });

        return del();
    }
}
