// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Codec.Memory;
using Nalix.Codec.Serialization;
using Nalix.Codec.Serialization.Internal.Reflection;
using Nalix.Codec.Serialization.Internal.Types;

namespace Nalix.Codec.DataFrames.Internal;

/// <summary>
/// Computes serialized wire sizes without materializing a temporary byte stream.
/// </summary>
internal static class WireSizeProvider
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetSize<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(T value)
        => WireSizeCache<T>.GetSize(value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int MeasureWithFormatter<T>(IFormatter<T> formatter, T value)
    {
        DataWriter writer = new(256);
        try
        {
            formatter.Serialize(ref writer, value);
            return writer.WrittenCount;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static class WireSizeCache<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
    {
        private static readonly Func<T, int> s_getSize = CreateSizer();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSize(T value) => s_getSize(value);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Func<T, int> CreateSizer()
        {
            Type type = typeof(T);

            if (type == typeof(string))
            {
                return CastSizer<string?>(SizeString);
            }

            if (type == typeof(string[]))
            {
                return CastSizer<string[]?>(SizeStringArray);
            }

            if (type.IsArray)
            {
                Type elementType = type.GetElementType()
                    ?? throw new InvalidOperationException($"Array type '{type.FullName}' has no element type.");

                return CreateArraySizer(elementType);
            }

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                Type[] arguments = type.GetGenericArguments();

                if (definition == typeof(Nullable<>))
                {
                    return CreateSizerFromGeneric(nameof(CreateNullableSizer), arguments);
                }

                if (definition == typeof(List<>))
                {
                    return CreateSizerFromGeneric(nameof(CreateListSizer), arguments);
                }

                if (definition == typeof(Dictionary<,>))
                {
                    return CreateSizerFromGeneric(nameof(CreateDictionarySizer), arguments);
                }

                if (definition == typeof(HashSet<>))
                {
                    return CreateSizerFromGeneric(nameof(CreateHashSetSizer), arguments);
                }

                if (definition == typeof(Queue<>))
                {
                    return CreateSizerFromGeneric(nameof(CreateQueueSizer), arguments);
                }

                if (definition == typeof(Stack<>))
                {
                    return CreateSizerFromGeneric(nameof(CreateStackSizer), arguments);
                }

                if (definition == typeof(Memory<>))
                {
                    return CreateSizerFromGeneric(nameof(CreateMemorySizer), arguments);
                }

                if (definition == typeof(ReadOnlyMemory<>))
                {
                    return CreateSizerFromGeneric(nameof(CreateReadOnlyMemorySizer), arguments);
                }
            }

            if (TypeMetadata.IsUnmanaged<T>())
            {
                int size = TypeMetadata.SizeOf<T>();
                return _ => size;
            }

            if (typeof(IPacket).IsAssignableFrom(type))
            {
                return value => value is null ? sizeof(byte) : sizeof(byte) + ((IPacket)value).Length;
            }

            if (type.IsClass)
            {
                return value => value is null ? sizeof(byte) : sizeof(byte) + ComplexWireSizer<T>.GetSize(value);
            }

            if (type.IsValueType)
            {
                return ComplexWireSizer<T>.GetSize;
            }

            IFormatter<T> formatter = FormatterProvider.Get<T>();
            return value => value is null ? sizeof(int) : MeasureWithFormatter(formatter, value);
        }

        private static Func<T, int> CreateArraySizer(Type elementType)
        {
            if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return CreateSizerFromGeneric(nameof(CreateNullableArraySizer), [elementType.GetGenericArguments()[0]]);
            }

            if (TypeMetadata.IsUnmanaged(elementType) || elementType.IsEnum)
            {
                return CreateSizerFromGeneric(nameof(CreateUnmanagedArraySizer), [elementType]);
            }

            return CreateSizerFromGeneric(nameof(CreateReferenceArraySizer), [elementType]);
        }

        private static Func<T, int> CreateSizerFromGeneric(string methodName, Type[] genericArguments)
        {
            MethodInfo method = typeof(WireSizeCache<T>)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Missing wire-size factory: {methodName}.");

            return (Func<T, int>)method.MakeGenericMethod(genericArguments).Invoke(null, null)!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Func<T, int> CastSizer<TValue>(Func<TValue, int> sizer)
            => (Func<T, int>)(object)sizer;

        private static Func<T, int> CreateNullableSizer<TValue>() where TValue : struct
        {
            static int Size(TValue? value) =>
                value.HasValue ? sizeof(byte) + WireSizeProvider.GetSize(value.Value) : sizeof(byte);

            return CastSizer<TValue?>(Size);
        }

        private static Func<T, int> CreateUnmanagedArraySizer<TElement>()
        {
            int elementSize = PacketBaseElementSizer.GetElementSize(typeof(TElement));

            static int SizeCore(TElement[]? value, int elementSize)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                return checked(sizeof(int) + (value.Length * elementSize));
            }

            return CastSizer<TElement[]?>(value => SizeCore(value, elementSize));
        }

        private static Func<T, int> CreateNullableArraySizer<TElement>() where TElement : struct
        {
            static int Size(TElement?[]? value)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                int size = sizeof(int);
                for (int i = 0; i < value.Length; i++)
                {
                    size += WireSizeProvider.GetSize(value[i]);
                }

                return size;
            }

            return CastSizer<TElement?[]?>(Size);
        }

        private static Func<T, int> CreateReferenceArraySizer<TElement>()
        {
            static int Size(TElement[]? value)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                int size = sizeof(int);
                for (int i = 0; i < value.Length; i++)
                {
                    size += WireSizeProvider.GetSize(value[i]);
                }

                return size;
            }

            return CastSizer<TElement[]?>(Size);
        }

        private static Func<T, int> CreateListSizer<TElement>()
        {
            int elementSize = GetFixedValueSize<TElement>();

            static int SizeCore(List<TElement>? value, int elementSize)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                if (elementSize != 0)
                {
                    return checked(sizeof(int) + (value.Count * elementSize));
                }

                int size = sizeof(int);
                for (int i = 0; i < value.Count; i++)
                {
                    size += WireSizeProvider.GetSize(value[i]);
                }

                return size;
            }

            return CastSizer<List<TElement>?>(value => SizeCore(value, elementSize));
        }

        private static Func<T, int> CreateDictionarySizer<TKey, TValue>() where TKey : notnull
        {
            int keySize = GetFixedValueSize<TKey>();
            int valueSize = GetFixedValueSize<TValue>();

            static int SizeCore(Dictionary<TKey, TValue>? value, int keySize, int valueSize)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                if (keySize != 0 && valueSize != 0)
                {
                    return checked(sizeof(int) + (value.Count * (keySize + valueSize)));
                }

                int size = sizeof(int);
                foreach (KeyValuePair<TKey, TValue> pair in value)
                {
                    size += keySize != 0 ? keySize : WireSizeProvider.GetSize(pair.Key);
                    size += valueSize != 0 ? valueSize : WireSizeProvider.GetSize(pair.Value);
                }

                return size;
            }

            return CastSizer<Dictionary<TKey, TValue>?>(value => SizeCore(value, keySize, valueSize));
        }

        private static Func<T, int> CreateHashSetSizer<TElement>() where TElement : notnull
        {
            int elementSize = GetFixedValueSize<TElement>();

            static int SizeCore(HashSet<TElement>? value, int elementSize)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                if (elementSize != 0)
                {
                    return checked(sizeof(int) + (value.Count * elementSize));
                }

                int size = sizeof(int);
                foreach (TElement element in value)
                {
                    size += WireSizeProvider.GetSize(element);
                }

                return size;
            }

            return CastSizer<HashSet<TElement>?>(value => SizeCore(value, elementSize));
        }

        private static Func<T, int> CreateQueueSizer<TElement>()
        {
            int elementSize = GetFixedValueSize<TElement>();

            static int SizeCore(Queue<TElement>? value, int elementSize)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                if (elementSize != 0)
                {
                    return checked(sizeof(int) + (value.Count * elementSize));
                }

                int size = sizeof(int);
                foreach (TElement element in value)
                {
                    size += WireSizeProvider.GetSize(element);
                }

                return size;
            }

            return CastSizer<Queue<TElement>?>(value => SizeCore(value, elementSize));
        }

        private static Func<T, int> CreateStackSizer<TElement>()
        {
            int elementSize = GetFixedValueSize<TElement>();

            static int SizeCore(Stack<TElement>? value, int elementSize)
            {
                if (value is null)
                {
                    return sizeof(int);
                }

                if (elementSize != 0)
                {
                    return checked(sizeof(int) + (value.Count * elementSize));
                }

                int size = sizeof(int);
                foreach (TElement element in value)
                {
                    size += WireSizeProvider.GetSize(element);
                }

                return size;
            }

            return CastSizer<Stack<TElement>?>(value => SizeCore(value, elementSize));
        }

        private static Func<T, int> CreateMemorySizer<TElement>() where TElement : unmanaged
        {
            int elementSize = Unsafe.SizeOf<TElement>();

            return CastSizer<Memory<TElement>>(value => checked(sizeof(int) + (value.Length * elementSize)));
        }

        private static Func<T, int> CreateReadOnlyMemorySizer<TElement>() where TElement : unmanaged
        {
            int elementSize = Unsafe.SizeOf<TElement>();

            return CastSizer<ReadOnlyMemory<TElement>>(value => checked(sizeof(int) + (value.Length * elementSize)));
        }

        private static int SizeString(string? value)
            => value is null ? sizeof(int) : sizeof(int) + Encoding.UTF8.GetByteCount(value);

        private static int SizeStringArray(string[]? value)
        {
            if (value is null)
            {
                return sizeof(int);
            }

            int size = sizeof(int);
            for (int i = 0; i < value.Length; i++)
            {
                size += SizeString(value[i]);
            }

            return size;
        }

        private static int GetFixedValueSize<TValue>()
            => TypeMetadata.IsUnmanaged<TValue>() || typeof(TValue).IsEnum
                ? PacketBaseElementSizer.GetElementSize(typeof(TValue))
                : 0;
    }

    private static class ComplexWireSizer<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties |
            DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
    {
        private static readonly Func<T, int>[] s_fieldSizers = CreateFieldSizers();

        internal static int GetSize(T value)
        {
            int size = 0;
            for (int i = 0; i < s_fieldSizers.Length; i++)
            {
                size += s_fieldSizers[i](value);
            }

            return size;
        }

        private static Func<T, int>[] CreateFieldSizers()
        {
            FieldSchema[] fields = FieldCache<T>.GetFields();
            Func<T, int>[] sizers = new Func<T, int>[fields.Length];

            for (int i = 0; i < fields.Length; i++)
            {
                sizers[i] = CreateFieldSizer(i, fields[i].FieldType);
            }

            return sizers;
        }

        private static Func<T, int> CreateFieldSizer(int fieldIndex, Type fieldType)
        {
            MethodInfo method = typeof(ComplexWireSizer<T>)
                .GetMethod(nameof(CreateFieldSizerCore), BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Missing method: {nameof(CreateFieldSizerCore)}.");

            return (Func<T, int>)method.MakeGenericMethod(fieldType).Invoke(null, [fieldIndex])!;
        }

        private static Func<T, int> CreateFieldSizerCore<TField>(int fieldIndex)
            => value => WireSizeProvider.GetSize(FieldCache<T>.GetValue<TField>(value, fieldIndex));
    }
}
