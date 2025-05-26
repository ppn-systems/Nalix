using Nalix.Common.Serialization;
using Nalix.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Types;

internal static partial class TypeMetadata
{
    // Frozen collections cho read performance cực khủng
    private static readonly ConcurrentDictionary<Type, SerializeLayout> _layoutCache = new();

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _cacheProperty = new();

    // Thread-local pools để tránh contention
    [ThreadStatic]
    private static List<PropertyInfo> t_propsList;

    [ThreadStatic]
    private static List<(int, PropertyInfo)> t_orderedList;

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static PropertyInfo[] ComputeSerializableProperties(
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(PropertyAccess)] Type type)
    {
        Console.WriteLine($"\n=== Scanning {type.Name} ===");

        // Kiểm tra SerializePackable attribute
        var attr = type.GetCustomAttribute<SerializePackableAttribute>();
        Console.WriteLine($"SerializePackable attribute: {(attr != null ? "Yes" : "No")}");

        ReadOnlySpan<PropertyInfo> allProps = type.GetProperties(Flags);
        Console.WriteLine($"\nFound properties: {allProps.Length}");

        // In ra tất cả properties tìm thấy
        foreach (var prop in allProps)
        {
            Console.WriteLine($"\nProperty: {prop.Name}");
            Console.WriteLine($"- Type: {prop.PropertyType}");
            Console.WriteLine($"- CanRead: {prop.CanRead}");
            Console.WriteLine($"- CanWrite: {prop.CanWrite}");
            Console.WriteLine($"- SerializeIgnore: {prop.GetCustomAttribute<SerializeIgnoreAttribute>() != null}");
        }

        SerializeLayout layout = GetLayout(type);
        Console.WriteLine($"\nLayout: {layout}");

        var result = layout switch
        {
            SerializeLayout.Sequential => ProcessSequentialOptimized(allProps),
            SerializeLayout.Explicit => ProcessExplicitOptimized(allProps),
            _ => []
        };

        Console.WriteLine($"\nSelected for serialization: {result.Length}");
        foreach (var prop in result)
        {
            Console.WriteLine($"- {prop.Name}");
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyInfo[] ProcessSequentialOptimized(ReadOnlySpan<PropertyInfo> allProps)
    {
        // Reuse thread-local list để tránh allocation
        var props = t_propsList ??= [];
        props.Clear();

        if (props.Capacity < allProps.Length)
            props.Capacity = allProps.Length;

        // Unrolled loop cho common cases
        var i = 0;
        var length = allProps.Length;

        // Process 4 properties at a time để help với branch prediction
        for (; i <= length - 4; i += 4)
        {
            ref readonly var prop0 = ref allProps[i];
            ref readonly var prop1 = ref allProps[i + 1];
            ref readonly var prop2 = ref allProps[i + 2];
            ref readonly var prop3 = ref allProps[i + 3];
        }

        // Handle remaining properties
        for (; i < length; i++)
        {
            ref readonly var prop = ref allProps[i];

            props.Add(prop);
        }

        return [.. props];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PropertyInfo[] ProcessExplicitOptimized(ReadOnlySpan<PropertyInfo> allProps)
    {
        // Reuse thread-local list
        var ordered = t_orderedList ??= [];
        ordered.Clear();

        if (ordered.Capacity < allProps.Length)
            ordered.Capacity = allProps.Length;

        // Single pass với early validation
        foreach (ref readonly var prop in allProps)
        {
            // Cached attribute lookup
            var orderAttr = GetOrderAttributeFast(prop);
            if (orderAttr is not null)
            {
                ordered.Add((orderAttr.Order, prop));
            }
        }

        // In-place sort với custom comparer
        ordered.Sort(static (a, b) => a.Item1.CompareTo(b.Item1));

        // Fast extraction
        var result = new PropertyInfo[ordered.Count];
        var span = result.AsSpan();

        for (var i = 0; i < ordered.Count; i++)
        {
            span[i] = ordered[i].Item2;
        }

        return result;
    }

    // Cached attribute accessors để tránh reflection overhead
    private static readonly ConcurrentDictionary<PropertyInfo, SerializeIgnoreAttribute> _ignoreAttrCache = new();

    private static readonly ConcurrentDictionary<PropertyInfo, SerializeOrderAttribute> _orderAttrCache = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SerializeIgnoreAttribute GetIgnoreAttributeFast(PropertyInfo prop)
        => _ignoreAttrCache.GetOrAdd(prop, static p => p.GetCustomAttribute<SerializeIgnoreAttribute>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SerializeOrderAttribute GetOrderAttributeFast(PropertyInfo prop)
        => _orderAttrCache.GetOrAdd(prop, static p => p.GetCustomAttribute<SerializeOrderAttribute>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SerializeLayout GetLayout(System.Type type)
        => _layoutCache.GetOrAdd(type, static t =>
            t.GetCustomAttribute<SerializePackableAttribute>()?.SerializeLayout ?? SerializeLayout.Sequential);
}
