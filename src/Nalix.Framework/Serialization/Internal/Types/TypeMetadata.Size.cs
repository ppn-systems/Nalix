// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Serialization;

namespace Nalix.Framework.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetNestedSize<T>(T value, out int size) => TryGetNestedSize(typeof(T), value, out size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetNestedSize(Type declaredType, object? value, out int size)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        size = 0;

        Type effectiveType = Nullable.GetUnderlyingType(declaredType) ?? declaredType;

        if (effectiveType == typeof(string))
        {
            size = value is string s ? sizeof(ushort) + (s.Length == 0 ? 0 : Encoding.UTF8.GetByteCount(s)) : sizeof(ushort);
            return true;
        }

        if (declaredType.IsGenericType && declaredType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type underlying = declaredType.GetGenericArguments()[0];
            if (value is null)
            {
                size = 1;
                return true;
            }

            if (!TryGetNestedSize(underlying, value, out int nestedSize))
            {
                return false;
            }

            size = 1 + nestedSize;
            return true;
        }

        if (declaredType.IsArray)
        {
            return TryGetArraySize(declaredType, (Array?)value, out size);
        }

        if (TryGetFixedSize(declaredType, out int fixedSize))
        {
            size = fixedSize;
            return true;
        }

        if (TryGetListSize(declaredType, value, out size))
        {
            return true;
        }

        if (TryGetDictionarySize(declaredType, value, out size))
        {
            return true;
        }

        if (TryGetMemorySize(declaredType, value, out size))
        {
            return true;
        }

        if (!declaredType.IsValueType)
        {
            if (value is null)
            {
                size = 1;
                return true;
            }

            // [FIX] Stack Overflow Protection
            // If the value is a concrete packet instance (e.g. Handshake), do NOT reflect
            // into its properties because PacketBase.Length will call back into us.
            if (value is IPacket packet)
            {
                size = packet.Length;
                return true;
            }

            if (TryGetObjectSize(declaredType, value, out size))
            {
                size += 1;
                return true;
            }

            return false;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetArraySize(Type arrayType, Array? array, out int size)
    {
        size = 0;
        Type elementType = arrayType.GetElementType()
            ?? throw new InvalidOperationException($"Unable to resolve element type for '{arrayType.FullName}'.");

        if (array is null)
        {
            size = sizeof(ushort);
            return true;
        }

        int total = sizeof(ushort);
        int length = array.Length;
        if (length == 0)
        {
            size = total;
            return true;
        }

        if (TryGetFixedSize(elementType, out int elementSize))
        {
            size = total + (length * elementSize);
            return true;
        }

        for (int i = 0; i < length; i++)
        {
            if (!TryGetNestedSize(elementType, array.GetValue(i), out int nestedSize))
            {
                return false;
            }

            total += nestedSize;
        }

        size = total;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetListSize(Type declaredType, object? value, out int size)
    {
        size = 0;
        if (!declaredType.IsGenericType || declaredType.GetGenericTypeDefinition() != typeof(List<>))
        {
            return false;
        }

        if (value is null)
        {
            size = sizeof(ushort);
            return true;
        }

        Type elementType = declaredType.GetGenericArguments()[0];
        IList list = (IList)value;
        size = sizeof(ushort);

        if (TryGetFixedSize(elementType, out int elementSize))
        {
            size += list.Count * elementSize;
            return true;
        }

        bool isNullableValue = elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(Nullable<>);
        if (isNullableValue)
        {
            Type underlying = elementType.GetGenericArguments()[0];
            for (int i = 0; i < list.Count; i++)
            {
                object? item = list[i];
                size += 1;
                if (item is not null)
                {
                    if (!TryGetNestedSize(underlying, item, out int nestedSize))
                    {
                        size = 0;
                        return false;
                    }

                    size += nestedSize;
                }
            }

            return true;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (!TryGetNestedSize(elementType, list[i], out int nestedSize))
            {
                size = 0;
                return false;
            }

            size += nestedSize;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetDictionarySize(Type declaredType, object? value, out int size)
    {
        size = 0;
        if (!declaredType.IsGenericType || declaredType.GetGenericTypeDefinition() != typeof(Dictionary<,>))
        {
            return false;
        }

        size = sizeof(int);
        if (value is null)
        {
            return true;
        }

        Type[] args = declaredType.GetGenericArguments();
        Type keyType = args[0];
        Type valueType = args[1];

        foreach (object? entry in (IEnumerable)value)
        {
            object key = entry!.GetType().GetProperty("Key", BindingFlags.Public | BindingFlags.Instance)!.GetValue(entry)!;
            object? itemValue = entry.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)!.GetValue(entry);
            if (!TryGetNestedSize(keyType, key, out int keySize) ||
                !TryGetNestedSize(valueType, itemValue, out int valueSize))
            {
                size = 0;
                return false;
            }

            size += keySize;
            size += valueSize;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetMemorySize(Type declaredType, object? value, out int size)
    {
        size = 0;
        if (!declaredType.IsGenericType)
        {
            return false;
        }

        Type def = declaredType.GetGenericTypeDefinition();
        if (def != typeof(Memory<>) && def != typeof(ReadOnlyMemory<>))
        {
            return false;
        }

        int length = value is null ? 0 : (int)declaredType.GetProperty("Length", BindingFlags.Public | BindingFlags.Instance)!.GetValue(value)!;
        Type elementType = declaredType.GetGenericArguments()[0];
        size = sizeof(int) + (length * RuntimeUnsafe.SizeOfHelper(elementType));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetFixedSize(Type type, out int size)
    {
        if (typeof(IFixedSizeSerializable).IsAssignableFrom(type))
        {
            PropertyInfo? property = type.GetProperty(nameof(IFixedSizeSerializable.Size), BindingFlags.Public | BindingFlags.Static);
            if (property?.GetValue(null) is int fixedSize)
            {
                size = fixedSize;
                return true;
            }
        }

        if (type.IsEnum)
        {
            type = Enum.GetUnderlyingType(type);
        }

        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(bool))
        {
            size = 1;
            return true;
        }

        if (type == typeof(short) || type == typeof(ushort) || type == typeof(char))
        {
            size = 2;
            return true;
        }

        if (type == typeof(int) || type == typeof(uint) || type == typeof(float) || type == typeof(DateOnly))
        {
            size = 4;
            return true;
        }

        if (type == typeof(long) || type == typeof(ulong) || type == typeof(double) || type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(TimeOnly))
        {
            size = 8;
            return true;
        }

        if (type == typeof(DateTimeOffset))
        {
            size = 10;
            return true;
        }

        if (type == typeof(decimal) || type == typeof(Guid))
        {
            size = 16;
            return true;
        }

        size = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetObjectSize(Type declaredType, object value, out int size)
    {
        size = 0;

        if (declaredType == typeof(string))
        {
            return false;
        }

        PropertyInfo[] properties = declaredType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        if (properties.Length == 0)
        {
            return false;
        }

        bool any = false;
        foreach (PropertyInfo property in properties)
        {
            if (property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            if (property.GetCustomAttribute<SerializeIgnoreAttribute>() is not null)
            {
                continue;
            }

            if (!property.CanRead)
            {
                continue;
            }

            object? propValue = property.GetValue(value);
            if (!TryGetNestedSize(property.PropertyType, propValue, out int nestedSize))
            {
                return false;
            }

            size += nestedSize;
            any = true;
        }

        return any;
    }

    private static class RuntimeUnsafe
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOfHelper(Type type)
            => (int)typeof(System.Runtime.CompilerServices.Unsafe)
                .GetMethod(nameof(Unsafe.SizeOf), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(type)
                .Invoke(null, null)!;
    }
}
