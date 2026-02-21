// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Common.Networking.Packets;

namespace Nalix.Framework.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    private const int MaxNestingDepth = 8;

    [ThreadStatic] private static int s_nestingDepth;

    /// <summary>
    /// Computes the dynamic length of an object for serialization.
    /// Handles strings, byte arrays, IPackets, and unmanaged arrays.
    /// Includes a recursion guard for IPackets.
    /// </summary>
    /// <param name="value">The value to measure.</param>
    /// <returns>The number of bytes on the wire.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetDynamicSize(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        // Handle string with 2-byte length prefix.
        if (value is string s)
        {
            return string.IsNullOrEmpty(s) ? 2 : 2 + Encoding.UTF8.GetByteCount(s);
        }

        // Handle byte array with 4-byte length prefix (LiteSerializer format).
        if (value is byte[] b)
        {
            return b.Length == 0 ? 4 : 4 + b.Length;
        }

        // Handle nested packets with recursion guard.
        if (value is IPacket p)
        {
            try
            {
                return p.Length;
            }
            finally
            {
                s_nestingDepth--;
            }
        }

        // Handle unmanaged arrays with 4-byte length prefix.
        if (value is Array arr)
        {
            Type? elementType = arr.GetType().GetElementType();
            if (elementType != null && IsUnmanaged(elementType))
            {
                return 4 + (arr.Length * GetElementSize(elementType));
            }
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetElementSize(Type type)
    {
        // Enum: resolve to underlying type.
        if (type.IsEnum)
        {
            return GetElementSize(Enum.GetUnderlyingType(type));
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => 1,
            TypeCode.SByte => 1,
            TypeCode.Boolean => 1,
            TypeCode.Char => 2,
            TypeCode.Int16 => 2,
            TypeCode.UInt16 => 2,
            TypeCode.Int32 => 4,
            TypeCode.UInt32 => 4,
            TypeCode.Single => 4,
            TypeCode.Int64 => 8,
            TypeCode.UInt64 => 8,
            TypeCode.Double => 8,
            TypeCode.Decimal => 16,
            TypeCode.DateTime => 8,
            TypeCode.Empty => throw new NotImplementedException(),
            TypeCode.Object => throw new NotImplementedException(),
            TypeCode.DBNull => throw new NotImplementedException(),
            TypeCode.String => throw new NotImplementedException(),
            _ => UnsafeSizeOf(type) // Fallback for other unmanaged structs.
        };
    }
}
