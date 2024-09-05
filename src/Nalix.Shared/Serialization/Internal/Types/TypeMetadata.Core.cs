// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Serialization;


#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<System.Boolean>> _isRefCache;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Func<System.Int32>> _sizeOfFnCache;

    static TypeMetadata()
    {
        System.Type _ = typeof(IFixedSizeSerializable);

        _isRefCache = new();
        _sizeOfFnCache = new();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Boolean IsReferenceOrContainsReferences(System.Type type)
    {
        System.Func<System.Boolean> fn = _isRefCache.GetOrAdd(type, static t =>
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
        System.Func<System.Int32> del = _sizeOfFnCache.GetOrAdd(type, static t =>
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
