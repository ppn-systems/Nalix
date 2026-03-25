// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nalix.Common.Serialization;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

internal static partial class FieldCache<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
{
    #region Static Fields

    private static readonly FieldSchema[] _metadata;
    private static readonly SerializeLayout _layout;
    private static readonly Dictionary<string, int> _fieldIndex;

    private static readonly Dictionary<string, PropertyInfo> _propertyCache;

    #endregion Static Fields

    #region Compiled Delegates Cache

    /// <summary>
    /// Store as object delegates, will be cast at runtime
    /// </summary>
    private static readonly Delegate[] _getters;

    private static readonly Delegate[] _setters;

    #endregion Compiled Delegates Cache

    #region Static Constructor

    [SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage("Trimming",
        "IL2091:Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    static FieldCache()
    {
        _layout = GetSerializeLayout();

        _propertyCache = new Dictionary<string, PropertyInfo>(
            capacity: 32,
            comparer: StringComparer.Ordinal
        );

        foreach (PropertyInfo p in typeof(T).GetProperties(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance))
        {
            _propertyCache[p.Name] = p;
        }

        _metadata = DiscoverFields<T>();
        _fieldIndex = BuildFieldIndex();

        // Create compiled getters/setters for each field
        _getters = new Delegate[_metadata.Length];
        _setters = new Delegate[_metadata.Length];

        for (int i = 0; i < _metadata.Length; i++)
        {
            FieldInfo field = _metadata[i].FieldInfo;
            _getters[i] = CreateGetter(field);
            _setters[i] = CreateSetter(field);
        }

        EnsureExplicitLayoutIsValid();
    }

    #endregion Static Constructor

    #region Field Discovery

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static FieldSchema[] DiscoverFields<
        [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicFields |
        DynamicallyAccessedMemberTypes.NonPublicFields)] TField>()
    {
        Type type = typeof(TField);

        List<FieldInfo> fields = [];
        while (type != null && type != typeof(object))
        {
            fields.AddRange(type.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly));
            type = type.BaseType!;
        }

        List<FieldSchema> includedFields = new(fields.Count);
        int sequentialOrder = 0;

        foreach (FieldInfo field in fields)
        {
            if (ShouldIgnoreField(field))
            {
                continue;
            }

            int order;

            if (_layout is SerializeLayout.Explicit)
            {
                int? explicitOrder = GetExplicitOrder(field);
                if (explicitOrder is null)
                {
                    continue;
                }
                order = explicitOrder.Value;
            }
            else
            {
                // Sequential: auto-increment order
                order = sequentialOrder++;
            }

            FieldSchema metadata = new(
                order,
                field.Name,
                field.FieldType.IsValueType,
                field.FieldType,
                field
            );

            includedFields.Add(metadata);
        }

        if (includedFields.Count == 0)
        {
            // Log warning or throw exception
            Debug.WriteLine($"[FieldCache<{typeof(TField).Name}>] WARNING: Type {typeof(TField).Name} has no serializable fields.");
            throw new InvalidOperationException($"Type {typeof(TField).Name} has no serializable fields.");
        }

        return _layout is SerializeLayout.Explicit
            ? [.. System.Linq.Enumerable.OrderBy(includedFields, f => f.Order)]
            : [.. includedFields];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Dictionary<string, int> BuildFieldIndex()
    {
        // Performance: StringComparer.Ordinal nhanh hơn default
        Dictionary<string, int> index = new(
            _metadata.Length, StringComparer.Ordinal);

        for (int i = 0; i < _metadata.Length; i++)
        {
            index[_metadata[i].Name] = i;
        }

        return index;
    }

    /// <summary>
    /// Updated method to move the DynamicallyAccessedMembersAttribute to the parameter
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [SuppressMessage("Trimming",
        "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    private static int? GetExplicitOrder(FieldInfo field)
    {
        Type? type = field.DeclaringType;
        if (type is null)
        {
            return null;
        }

        foreach (PropertyInfo property in type.GetProperties(
            BindingFlags.Public |
            BindingFlags.Instance))
        {
            if (field.Name == property.Name || IsBackingFieldFor(field, property))
            {
                SerializeOrderAttribute? attr = property.GetCustomAttribute<SerializeOrderAttribute>();
                if (attr is not null)
                {
                    return attr.Order;
                }
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBackingFieldFor(
        FieldInfo field,
        PropertyInfo property) => field.Name == $"<{property.Name}>k__BackingField";

    #endregion Field Discovery

    #region Domain Rules - Business Logic

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ShouldIgnoreField(FieldInfo field)
    {
        // Rule 1: Skip backing fields nếu property có SerializeIgnoreAttribute
        if (field.Name.StartsWith('<') && field.Name.Contains(">k__BackingField"))
        {
            string propertyName = field.Name[1..field.Name.IndexOf('>')];
            if (_propertyCache.TryGetValue(propertyName, out PropertyInfo? property))
            {
                // Nếu property bị ignore thì skip backing field
                if (property.GetCustomAttribute<SerializeIgnoreAttribute>() is not null)
                {
                    return true;
                }
            }
        }

        // Rule 2: Skip fields có SerializeIgnoreAttribute
        return field.GetCustomAttribute<SerializeIgnoreAttribute>() is not null;
    }

    #endregion Domain Rules - Business Logic

    #region Layout Detection

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SerializeLayout GetSerializeLayout()
    {
        SerializePackableAttribute? packableAttr = CustomAttributeExtensions
            .GetCustomAttribute<SerializePackableAttribute>(typeof(T));

        return packableAttr?.SerializeLayout ?? SerializeLayout.Sequential;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SerializeLayout GetLayout() => _layout;

    #endregion Layout Detection

    #region Ensure - Fail Fast Strategy

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureExplicitLayoutIsValid()
    {
        if (_layout is not SerializeLayout.Explicit)
        {
            return;
        }

        EnsureNoDuplicateOrders();
        EnsureNoNegativeOrders();
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureNoDuplicateOrders()
    {
        IEnumerable<IGrouping<int, FieldSchema>> orderGroups = Enumerable.Where(
            Enumerable.GroupBy(_metadata, f => f.Order),
            g => Enumerable.Count(g) > 1
        );

        if (Enumerable.Any(orderGroups))
        {
            string duplicates = string.Join(", ",
                Enumerable.Select(orderGroups, g =>
                    $"Order {g.Key}: [{string.Join(", ",
                        Enumerable.Select(g, f => f.Name))}]"));

            throw new InvalidOperationException(
                $"Duplicate serialize orders in type '{typeof(T).Name}': {duplicates}");
        }
    }

    [StackTraceHidden]
    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureNoNegativeOrders()
    {
        IEnumerable<FieldSchema> negativeOrders = Enumerable
            .Where(_metadata, f => f.Order < 0);

        if (Enumerable.Any(negativeOrders))
        {
            string negativeFields = string.Join(", ",
                Enumerable.Select(negativeOrders, f => $"{f.Name}({f.Order})"));

            throw new InvalidOperationException(
                $"Negative serialize orders not allowed in type '{typeof(T).Name}': {negativeFields}");
        }
    }

    #endregion Ensure - Fail Fast Strategy
}
