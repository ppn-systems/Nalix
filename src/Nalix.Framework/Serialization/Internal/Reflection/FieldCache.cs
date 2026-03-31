// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
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
    private static readonly string[] s_fieldNames;
    private static readonly HashSet<string> s_ignoredProperties;

    static FieldCache()
    {
        s_layout = GET_SERIALIZE_LAYOUT(typeof(T));
        s_ignoredProperties = BUILD_IGNORED_PROPERTIES(typeof(T));
        s_metadata = DISCOVER_FIELDS(typeof(T), s_layout, s_ignoredProperties);
        s_fieldNames = new string[s_metadata.Length];

        Debug.WriteLine($"[FieldCache<{typeof(T).Name}>] Created: s_metadata.Length = {s_metadata.Length}");

        for (int i = 0; i < s_metadata.Length; i++)
        {
            if (s_metadata[i].FieldInfo == null)
            {
                Debug.WriteLine($"[FieldCache<{typeof(T).Name}>] ERROR: FieldSchema[{i}] ({s_metadata[i].Name}) has null FieldInfo!");
            }
        }

        for (int i = 0; i < s_metadata.Length; i++)
        {
            s_fieldNames[i] = s_metadata[i].Name;
        }

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
    private static FieldSchema[] DISCOVER_FIELDS(Type type, SerializeLayout layout, HashSet<string> ignoredProperties)
    {
        List<FieldInfo> allFields = new(16);
        for (Type? t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            allFields.AddRange(t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        Debug.WriteLine($"[FieldCache<{typeof(T).Name}>] Discover fields for {type}: total={allFields.Count}");

        List<FieldSchema> included = new(allFields.Count);
        int sequentialOrder = 0;
        foreach (FieldInfo field in allFields)
        {
            if (field == null)
            {
                Debug.WriteLine("[FieldCache] Ignored null FieldInfo!");
                continue;
            }

            if (SHOULD_IGNORE_FIELD(field, ignoredProperties))
            {
                Debug.WriteLine($"[FieldCache] Ignore field: {field.Name} ({field.FieldType.Name})");
                continue;
            }

            int? explicitOrder = layout == SerializeLayout.Explicit ? GET_EXPLICIT_ORDER(field) : null;
            if (layout == SerializeLayout.Explicit && explicitOrder is null)
            {
                Debug.WriteLine($"[FieldCache] Explicit order null for field: {field.Name}");
                continue;
            }

            FieldSchema schema = new(
                layout == SerializeLayout.Explicit ? explicitOrder!.Value : sequentialOrder++,
                field.Name,
                field.FieldType.IsValueType,
                field.FieldType,
                field
            );
            included.Add(schema);

            Debug.WriteLine($"[FieldCache] Add field: {schema.Name} (Type={schema.FieldType.Name}, IsValueType={schema.IsValueType}, Order={schema.Order})");
        }

        if (layout == SerializeLayout.Explicit)
        {
            included.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        }

        FieldSchema[] arr = [.. included];
        Debug.WriteLine($"[FieldCache<{typeof(T).Name}>] Included fields: {arr.Length}");

#if DEBUG
        for (int i = 0; i < arr.Length; i++)
        {
            FieldSchema f = arr[i];
            Debug.WriteLine($"  [{i}] Name={f.Name}, FieldType={f.FieldType}, IsValueType={f.IsValueType}, FieldInfo-null={f.FieldInfo is null}, Order={f.Order}");
        }
#endif

        return arr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static HashSet<string> BUILD_IGNORED_PROPERTIES(Type type)
    {
        HashSet<string> set = new(StringComparer.Ordinal);
        foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetCustomAttribute<SerializeIgnoreAttribute>() != null)
            {
                _ = set.Add(p.Name);
            }
        }

        return set;
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
    private static int? GET_EXPLICIT_ORDER(FieldInfo field)
    {
        Type? type = field.DeclaringType;
        if (type == null)
        {
            return null;
        }

        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.Name == prop.Name || field.Name == $"<{prop.Name}>k__BackingField")
            {
                return prop.GetCustomAttribute<SerializeOrderAttribute>()?.Order;
            }
        }

        return null;
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
        HashSet<int> orderCounts = [];
        for (int i = 0; i < s_metadata.Length; i++)
        {
            if (!orderCounts.Add(s_metadata[i].Order))
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
