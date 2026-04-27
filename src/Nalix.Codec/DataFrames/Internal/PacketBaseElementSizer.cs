// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Codec.DataFrames.Internal;

internal static class PacketBaseElementSizer
{
    private static readonly ConcurrentDictionary<Type, Func<int>> s_elementSizeCache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetElementSize(Type type)
    {
        if (type.IsEnum)
        {
            return GetElementSize(Enum.GetUnderlyingType(type));
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Decimal => 16,
            TypeCode.Object => GetUnsafeSizeOf(type),
            TypeCode.DBNull => GetUnsafeSizeOf(type),
            TypeCode.String => GetUnsafeSizeOf(type),
            TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => 2,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => 4,
            TypeCode.Byte or TypeCode.SByte or TypeCode.Boolean or TypeCode.Empty => 1,
            TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double or TypeCode.DateTime => 8,
            _ => 0
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int GetUnsafeSizeOf(Type type)
    {
        return s_elementSizeCache.GetOrAdd(type, static t =>
        {
            MethodInfo method =
                typeof(Unsafe)
                    .GetMethod(nameof(Unsafe.SizeOf), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(t);

            return method.CreateDelegate<Func<int>>();
        })();
    }
}
