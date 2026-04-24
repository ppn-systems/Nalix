// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
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
        Func<bool> fn = s_isRefCache.GetOrAdd(type, static t =>
        {
            MethodInfo method = s_isReferenceOrContainsReferencesMethod.MakeGenericMethod(t);
            return method.CreateDelegate<Func<bool>>();
        });

        return fn();
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int UnsafeSizeOf(Type type)
    {
        // Same pattern as above, but for size queries.
        Func<int> del = s_sizeOfFnCache.GetOrAdd(type, static t =>
        {
            MethodInfo method = s_unsafeSizeOfMethod.MakeGenericMethod(t);
            return method.CreateDelegate<Func<int>>();
        });

        return del();
    }
}
