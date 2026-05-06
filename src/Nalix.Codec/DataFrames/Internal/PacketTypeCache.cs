// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Serialization;

namespace Nalix.Codec.DataFrames.Internal;

/// <summary>
/// Per-packet static schema cache used by <see cref="PacketBase{TSelf}"/>.
/// </summary>
internal static class PacketTypeCache<TSelf> where TSelf : PacketBase<TSelf>, new()
{
    #region Nested types

    private static readonly SerializeLayout s_layout =
        typeof(TSelf).GetCustomAttribute<SerializePackableAttribute>()?.SerializeLayout ?? SerializeLayout.Auto;

    private static readonly Lazy<PropertyMetadata[]> s_metadata = new(
        static () =>
        [
            .. ENUMERATE_SERIALIZABLE_PROPERTIES().Select(static x => new PropertyMetadata(x.p))
        ],
        isThreadSafe: true);

    private static readonly Func<TSelf, int>[] s_sizeGetters;

    #endregion Nested types

    #region Constructors

    static PacketTypeCache()
    {
        AutoMagic = PacketRegistryFactory.Compute(typeof(TSelf));
        IsFixedSize = typeof(IFixedSizeSerializable).IsAssignableFrom(typeof(TSelf));
        FixedSize = IsFixedSize ? FETCH_FIXED_SIZE() : 0;

        PropertyMetadata[] all = s_metadata.Value;
        All = all;

        if (IsFixedSize)
        {
            StaticSize = FixedSize;
            s_sizeGetters = [];
            return;
        }

        int staticSize = PacketConstants.HeaderSize;
        List<Func<TSelf, int>> getters = new(capacity: all.Length);

        foreach (PropertyMetadata meta in all)
        {
            if (meta.DynamicKind == DynamicWireKind.None && meta.FixedSize != 0)
            {
                staticSize += meta.FixedSize;
                continue;
            }

            getters.Add(BUILD_SIZE_GETTER(meta));
        }

        StaticSize = staticSize;
        s_sizeGetters = [.. getters];
    }

    #endregion Constructors

    #region Properties

    public static uint AutoMagic { get; }

    public static bool IsFixedSize { get; }

    public static int FixedSize { get; }

    public static int StaticSize { get; }

    public static PropertyMetadata[] All { get; }

    public static PropertyMetadata[] Metadata => s_metadata.Value;

    public static int SizeGettersCount => s_sizeGetters.Length;

    #endregion Properties

    #region API

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLength(TSelf instance)
    {
        if (IsFixedSize)
        {
            return FixedSize;
        }

        Func<TSelf, int>[] getters = s_sizeGetters;
        if (getters.Length == 0)
        {
            return StaticSize;
        }

        int size = StaticSize;
        for (int i = 0; i < getters.Length; i++)
        {
            size += getters[i](instance);
        }

        return size;
    }

    #endregion API

    #region Private methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<TSelf, int> BUILD_SIZE_GETTER(PropertyMetadata meta)
        => meta.DynamicKind switch
        {
            DynamicWireKind.String => BUILD_STRING_GETTER(meta),
            DynamicWireKind.ByteArray => BUILD_BYTE_ARRAY_GETTER(meta),
            DynamicWireKind.Packet => BUILD_PACKET_GETTER(meta),
            DynamicWireKind.UnmanagedList => BUILD_UNMANAGED_LIST_GETTER(meta),
            DynamicWireKind.UnmanagedArray => BUILD_UNMANAGED_ARRAY_GETTER(meta),
            DynamicWireKind.GenericCollection => BUILD_GENERIC_COLLECTION_GETTER(meta),
            DynamicWireKind.None or DynamicWireKind.Other or _ => BUILD_FALLBACK_GETTER(meta)
        };

    // List<T> where T : unmanaged → O(1), zero-reflection
    private static Func<TSelf, int> BUILD_UNMANAGED_LIST_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, System.Collections.ICollection?> getter =
            BUILD_TYPED_GETTER<System.Collections.ICollection?>(meta);

        int elementSize = meta.ElementSize;
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            System.Collections.ICollection? list = getter(instance);
            return list is null
                ? nullWireSize
                : checked(sizeof(int) + (list.Count * elementSize));
        };
    }

    private static Func<TSelf, int> BUILD_GENERIC_COLLECTION_GETTER(PropertyMetadata meta)
    {
        MethodInfo buildGeneric = typeof(PacketTypeCache<TSelf>)
            .GetMethod(nameof(BUILD_GENERIC_COLLECTION_GETTER),
                       BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InternalErrorException(
                $"Missing method: {nameof(BUILD_GENERIC_COLLECTION_GETTER)}");

        MethodInfo closed = buildGeneric.MakeGenericMethod(meta.DeclaredType);
        return (Func<TSelf, int>)closed.Invoke(null, [meta])!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<TSelf, int> BUILD_GENERIC_COLLECTION_GETTER<TCollection>(PropertyMetadata meta)
    {
        Func<TSelf, TCollection> getter = BUILD_TYPED_GETTER<TCollection>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            TCollection value = getter(instance);
            return value is null ? nullWireSize : WireSizeProvider.GetSize(value);
        };
    }

    private static Func<TSelf, int> BUILD_STRING_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, string?> getter = BUILD_TYPED_GETTER<string?>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            string? value = getter(instance);
            return value is null
                ? nullWireSize
                : sizeof(int) + Encoding.UTF8.GetByteCount(value);
        };
    }

    private static Func<TSelf, int> BUILD_BYTE_ARRAY_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, byte[]?> getter = BUILD_TYPED_GETTER<byte[]?>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            byte[]? value = getter(instance);
            return value is null ? nullWireSize : sizeof(int) + value.Length;
        };
    }

    private static Func<TSelf, int> BUILD_PACKET_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, IPacket?> getter = BUILD_TYPED_GETTER<IPacket?>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            IPacket? packet = getter(instance);
            if (packet is null)
            {
                return nullWireSize;
            }

            return ReferenceEquals(packet, instance)
                ? 0
                : sizeof(byte) + packet.Length;
        };
    }

    private static Func<TSelf, int> BUILD_UNMANAGED_ARRAY_GETTER(PropertyMetadata meta)
    {
        Func<TSelf, Array?> getter = BUILD_TYPED_GETTER<Array?>(meta);
        int elementSize = meta.ElementSize;
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            Array? value = getter(instance);
            return value is null ? nullWireSize : sizeof(int) + (value.Length * elementSize);
        };
    }

    private static Func<TSelf, int> BUILD_FALLBACK_GETTER(PropertyMetadata meta)
    {
        MethodInfo buildGeneric = typeof(PacketTypeCache<TSelf>)
            .GetMethod(nameof(BuildFallbackGetterCore), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InternalErrorException($"Missing method: {nameof(BuildFallbackGetterCore)}");
        MethodInfo closed = buildGeneric.MakeGenericMethod(meta.DeclaredType);
        return (Func<TSelf, int>)closed.Invoke(null, [meta])!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<TSelf, int> BuildFallbackGetterCore<TValue>(PropertyMetadata meta)
    {
        Func<TSelf, TValue> getter = BUILD_TYPED_GETTER<TValue>(meta);
        int nullWireSize = meta.NullWireSize;

        return instance =>
        {
            TValue value = getter(instance);
            return value is null ? nullWireSize : WireSizeProvider.GetSize(value);
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Func<TSelf, TValue> BUILD_TYPED_GETTER<TValue>(PropertyMetadata meta)
    {
        MethodInfo? getMethod = meta.Property.GetMethod;
        if (getMethod is null)
        {
            return static _ => default!;
        }

        try
        {
            return getMethod.CreateDelegate<Func<TSelf, TValue>>();
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            return instance => (TValue)meta.GetValue(instance)!;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerable<(PropertyInfo p, SerializeOrderAttribute? order)> ENUMERATE_SERIALIZABLE_PROPERTIES()
    {
        IEnumerable<(PropertyInfo p, SerializeOrderAttribute? order, SerializeIgnoreAttribute? ignore, SerializeHeaderAttribute? header)> candidates =
            typeof(TSelf)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static p => (
                    p,
                    order: p.GetCustomAttribute<SerializeOrderAttribute>(),
                    ignore: p.GetCustomAttribute<SerializeIgnoreAttribute>(),
                    header: p.GetCustomAttribute<SerializeHeaderAttribute>()));

        IEnumerable<(PropertyInfo p, SerializeOrderAttribute? order)> selected = candidates
            .Where(static x => x.ignore is null && x.header is null)
            .Select(static x => (x.p, x.order));

        return s_layout == SerializeLayout.Explicit
            ? selected
                .Where(static x => x.order is not null)
                .OrderBy(static x => x.order!.Order)
            : selected;
    }

    private static int FETCH_FIXED_SIZE()
        => typeof(TSelf).GetProperty(nameof(IFixedSizeSerializable.Size), BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is int size ? size : 0;

    #endregion Private methods
}
