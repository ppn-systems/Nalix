// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Codec.Tests")]
[assembly: InternalsVisibleTo("Nalix.Codec.Benchmarks")]
#endif

namespace Nalix.Codec.Serialization.Internal.Reflection;

internal static partial class FieldCache<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
{
    private static readonly FieldSchema[] s_metadata;
    private static readonly SerializeLayout s_layout;
    private static readonly HashSet<string> s_ignoredProperties;
    private static readonly Dictionary<Type, Dictionary<string, (int Order, bool IsHeader)>> s_explicitOrdersByDeclaringType;

    static FieldCache()
    {
        s_layout = GET_SERIALIZE_LAYOUT(typeof(T));
        s_ignoredProperties = BUILD_IGNORED_PROPERTIES(typeof(T));
        s_explicitOrdersByDeclaringType = BUILD_EXPLICIT_ORDER_MAPS(typeof(T));
        s_metadata = DISCOVER_FIELDS(typeof(T), s_layout, s_ignoredProperties, s_explicitOrdersByDeclaringType);

        s_getters = new Delegate[s_metadata.Length];
        s_setters = new Delegate[s_metadata.Length];

        for (int i = 0; i < s_metadata.Length; i++)
        {
            FieldInfo field = s_metadata[i].FieldInfo;

            s_getters[i] = CreateGetter(field);
            s_setters[i] = CreateSetter(field);
        }

        ENSURE_LAYOUT_IS_VALID();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static FieldSchema[] DISCOVER_FIELDS(
        Type type,
        SerializeLayout layout,
        HashSet<string> ignoredProperties,
        Dictionary<Type, Dictionary<string, (int Order, bool IsHeader)>> explicitOrdersByDeclaringType)
    {
        List<FieldSchema> included = new(16);
        int sequentialOrder = 0;

        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            FieldInfo[] declaredFields = current.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            for (int i = 0; i < declaredFields.Length; i++)
            {
                FieldInfo field = declaredFields[i];

                if (SHOULD_IGNORE_FIELD(field, ignoredProperties))
                {
                    continue;
                }

                (int Order, bool IsHeader)? explicitOrder = GET_EXPLICIT_ORDER(field, explicitOrdersByDeclaringType);
                if (layout == SerializeLayout.Explicit && explicitOrder is null)
                {
                    continue;
                }

                bool isHeader = explicitOrder is not null && explicitOrder.Value.IsHeader;
                int order = explicitOrder is not null && (layout != SerializeLayout.Auto || isHeader) ? explicitOrder.Value.Order : sequentialOrder++;

                int size = GET_TYPE_MEMORY_SIZE(field.FieldType);

                included.Add(new FieldSchema(
                    order,
                    isHeader,
                    size,
                    field.Name,
                    field.FieldType.IsValueType,
                    field.FieldType,
                    field));
            }
        }

        included.Sort((a, b) =>
        {
            if (a.IsHeader != b.IsHeader)
            {
                return a.IsHeader ? -1 : 1;
            }

            if (a.IsHeader)
            {
                return a.Order.CompareTo(b.Order);
            }

            if (layout == SerializeLayout.Auto && a.Size != b.Size)
            {
                return b.Size.CompareTo(a.Size); // Descending
            }

            return a.Order.CompareTo(b.Order);
        });

        return [.. included];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static HashSet<string> BUILD_IGNORED_PROPERTIES(Type type)
    {
        HashSet<string> set = new(StringComparer.Ordinal);
        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            PropertyInfo[] properties = current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetCustomAttribute<SerializeIgnoreAttribute>() != null)
                {
                    _ = set.Add(property.Name);
                }
            }
        }

        return set;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Dictionary<Type, Dictionary<string, (int Order, bool IsHeader)>> BUILD_EXPLICIT_ORDER_MAPS(Type type)
    {
        Dictionary<Type, Dictionary<string, (int Order, bool IsHeader)>> result = [];

        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            PropertyInfo[] properties = current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Dictionary<string, (int Order, bool IsHeader)> propertyOrders = new(StringComparer.Ordinal);

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                SerializeOrderAttribute? order = property.GetCustomAttribute<SerializeOrderAttribute>();
                SerializeHeaderAttribute? header = property.GetCustomAttribute<SerializeHeaderAttribute>();

                if (order is not null && header is not null)
                {
                    throw new InvalidOperationException($"Property {property.Name} in {current.Name} cannot have both [SerializeHeader] and [SerializeOrder].");
                }

                if (header is not null)
                {
                    propertyOrders[property.Name] = (header.Order, true);
                    continue;
                }

                if (order is not null)
                {
                    propertyOrders[property.Name] = (order.Order, false);
                    continue;
                }
            }

            result[current] = propertyOrders;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SHOULD_IGNORE_FIELD(FieldInfo field, HashSet<string> ignoredProperties)
    {
        // <PropertyName>k__BackingField
        if (field.Name.Length > 2 && field.Name[0] == '<')
        {
            int end = field.Name.IndexOf('>', StringComparison.Ordinal);
            if (end > 1)
            {
                if (ignoredProperties.Contains(field.Name[1..end]))
                {
                    return true;
                }
            }
        }
        return field.GetCustomAttribute<SerializeIgnoreAttribute>() != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SerializeLayout GET_SERIALIZE_LAYOUT(Type type)
        => type.GetCustomAttribute<SerializePackableAttribute>()?.SerializeLayout ?? SerializeLayout.Auto;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GET_TYPE_MEMORY_SIZE(Type type)
    {
        if (!type.IsValueType)
        {
            return IntPtr.Size;
        }

        if (type.IsEnum)
        {
            return GET_TYPE_MEMORY_SIZE(Enum.GetUnderlyingType(type));
        }

        if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte))
        {
            return 1;
        }

        if (type == typeof(short) || type == typeof(ushort) || type == typeof(char))
        {
            return 2;
        }

        if (type == typeof(int) || type == typeof(uint) || type == typeof(float))
        {
            return 4;
        }

        if (type == typeof(long) || type == typeof(ulong) || type == typeof(double) || type == typeof(DateTime) || type == typeof(TimeSpan))
        {
            return 8;
        }

        if (type == typeof(decimal) || type == typeof(Guid))
        {
            return 16;
        }

        try
        {
            return System.Runtime.InteropServices.Marshal.SizeOf(type);
        }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            return IntPtr.Size;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (int Order, bool IsHeader)? GET_EXPLICIT_ORDER(FieldInfo field, Dictionary<Type, Dictionary<string, (int Order, bool IsHeader)>> explicitOrdersByDeclaringType)
    {
        Type? type = field.DeclaringType;
        if (type is null || !explicitOrdersByDeclaringType.TryGetValue(type, out Dictionary<string, (int Order, bool IsHeader)>? propertyOrders))
        {
            return null;
        }

        if (field.Name.Length > 2 && field.Name[0] == '<')
        {
            int end = field.Name.IndexOf('>', StringComparison.Ordinal);
            if (end > 1 && propertyOrders.TryGetValue(field.Name[1..end], out (int Order, bool IsHeader) backingFieldOrder))
            {
                return backingFieldOrder;
            }
        }

        return propertyOrders.TryGetValue(field.Name, out (int Order, bool IsHeader) order) ? order : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ENSURE_LAYOUT_IS_VALID()
    {
        ENSURE_NO_DUPLICATE_ORDERS();
        ENSURE_NO_NEGATIVE_ORDERS();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ENSURE_NO_DUPLICATE_ORDERS()
    {
        for (int i = 1; i < s_metadata.Length; i++)
        {
            if (s_metadata[i - 1].IsHeader == s_metadata[i].IsHeader && s_metadata[i - 1].Order == s_metadata[i].Order)
            {
                throw new InvalidOperationException($"Duplicate serialize orders in {typeof(T).Name}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ENSURE_NO_NEGATIVE_ORDERS()
    {
        for (int i = 0; i < s_metadata.Length; i++)
        {
            if (s_metadata[i].Order < 0)
            {
                throw new InvalidOperationException($"Negative serialize order in {typeof(T).Name}");
            }
        }
    }
}
