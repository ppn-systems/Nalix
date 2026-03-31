// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Framework.Tests")]
[assembly: InternalsVisibleTo("Nalix.Framework.Benchmarks")]
#endif

namespace Nalix.Framework.Serialization.Internal.Reflection;

internal static partial class FieldCache<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
{
    private static readonly FieldSchema[] s_metadata;
    private static readonly SerializeLayout s_layout;
    private static readonly HashSet<string> s_ignoredProperties;
    private static readonly Dictionary<Type, Dictionary<string, int>> s_explicitOrdersByDeclaringType;

    static FieldCache()
    {
        s_layout = GET_SERIALIZE_LAYOUT(typeof(T));
        s_ignoredProperties = BUILD_IGNORED_PROPERTIES(typeof(T));
        s_explicitOrdersByDeclaringType = s_layout == SerializeLayout.Explicit
            ? BUILD_EXPLICIT_ORDER_MAPS(typeof(T))
            : [];
        s_metadata = DISCOVER_FIELDS(typeof(T), s_layout, s_ignoredProperties, s_explicitOrdersByDeclaringType);

        s_getters = new Delegate[s_metadata.Length];
        s_setters = new Delegate[s_metadata.Length];

        for (int i = 0; i < s_metadata.Length; i++)
        {
            FieldInfo field = s_metadata[i].FieldInfo;

            s_getters[i] = CreateGetter(field);
            s_setters[i] = CreateSetter(field);
        }

        ENSURE_EXPLICIT_LAYOUT_IS_VALID();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static FieldSchema[] DISCOVER_FIELDS(
        Type type,
        SerializeLayout layout,
        HashSet<string> ignoredProperties,
        Dictionary<Type, Dictionary<string, int>> explicitOrdersByDeclaringType)
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

                int? explicitOrder = layout == SerializeLayout.Explicit
                    ? GET_EXPLICIT_ORDER(field, explicitOrdersByDeclaringType)
                    : null;
                if (layout == SerializeLayout.Explicit && explicitOrder is null)
                {
                    continue;
                }

                included.Add(new FieldSchema(
                    layout == SerializeLayout.Explicit ? explicitOrder!.Value : sequentialOrder++,
                    field.Name,
                    field.FieldType.IsValueType,
                    field.FieldType,
                    field));
            }
        }

        if (layout == SerializeLayout.Explicit)
        {
            included.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        }

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
    private static Dictionary<Type, Dictionary<string, int>> BUILD_EXPLICIT_ORDER_MAPS(Type type)
    {
        Dictionary<Type, Dictionary<string, int>> result = [];

        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            PropertyInfo[] properties = current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Dictionary<string, int> propertyOrders = new(StringComparer.Ordinal);

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                SerializeOrderAttribute? order = property.GetCustomAttribute<SerializeOrderAttribute>();
                if (order is null)
                {
                    continue;
                }

                propertyOrders[property.Name] = order.Order;
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
            int end = field.Name.IndexOf('>');
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
        => type.GetCustomAttribute<SerializePackableAttribute>()?.SerializeLayout ?? SerializeLayout.Sequential;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int? GET_EXPLICIT_ORDER(FieldInfo field, Dictionary<Type, Dictionary<string, int>> explicitOrdersByDeclaringType)
    {
        Type? type = field.DeclaringType;
        if (type is null || !explicitOrdersByDeclaringType.TryGetValue(type, out Dictionary<string, int>? propertyOrders))
        {
            return null;
        }

        if (field.Name.Length > 2 && field.Name[0] == '<')
        {
            int end = field.Name.IndexOf('>');
            if (end > 1 && propertyOrders.TryGetValue(field.Name[1..end], out int backingFieldOrder))
            {
                return backingFieldOrder;
            }
        }

        return propertyOrders.TryGetValue(field.Name, out int order)
            ? order
            : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ENSURE_EXPLICIT_LAYOUT_IS_VALID()
    {
        if (s_layout is not SerializeLayout.Explicit)
        {
            return;
        }

        ENSURE_NO_DUPLICATE_ORDERS();
        ENSURE_NO_NEGATIVE_ORDERS();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static void ENSURE_NO_DUPLICATE_ORDERS()
    {
        for (int i = 1; i < s_metadata.Length; i++)
        {
            if (s_metadata[i - 1].Order == s_metadata[i].Order)
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
