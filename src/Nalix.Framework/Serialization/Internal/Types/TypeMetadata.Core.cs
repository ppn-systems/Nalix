// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif
namespace Nalix.Framework.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private static readonly MethodInfo s_isReferenceOrContainsReferencesMethod;
    private static readonly MethodInfo s_unsafeSizeOfMethod;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<bool>> s_isRefCache;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<int>> s_sizeOfFnCache;

    static TypeMetadata()
    {
        _ = typeof(IFixedSizeSerializable);
        s_isReferenceOrContainsReferencesMethod = typeof(RuntimeHelpers)
            .GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences))!;
        s_unsafeSizeOfMethod = typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf), BindingFlags.Public | BindingFlags.Static)!;

        s_isRefCache = new();
        s_sizeOfFnCache = new();
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsReferenceOrContainsReferences(Type type)
    {
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
        Func<int> del = s_sizeOfFnCache.GetOrAdd(type, static t =>
        {
            MethodInfo method = s_unsafeSizeOfMethod.MakeGenericMethod(t);
            return method.CreateDelegate<Func<int>>();
        });

        return del();
    }
}
