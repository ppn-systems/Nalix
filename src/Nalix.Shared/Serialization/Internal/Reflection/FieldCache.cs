// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Reflection;
using Nalix.Common.Serialization;

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

internal static partial class FieldCache<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties)] T>
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
    private static readonly System.Delegate[] _getters;

    private static readonly System.Delegate[] _setters;

    #endregion Compiled Delegates Cache

    #region Static Constructor

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2091:Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    static FieldCache()
    {
        _layout = GetSerializeLayout();

        _propertyCache = new Dictionary<string, PropertyInfo>(
            capacity: 32,
            comparer: System.StringComparer.Ordinal
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
        _getters = new System.Delegate[_metadata.Length];
        _setters = new System.Delegate[_metadata.Length];

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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static FieldSchema[] DiscoverFields<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] TField>()
    {
        System.Type type = typeof(TField);

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
            System.Diagnostics.Debug.WriteLine($"[FieldCache<{typeof(TField).Name}>] WARNING: Type {typeof(TField).Name} has no serializable fields.");
            throw new System.InvalidOperationException($"Type {typeof(TField).Name} has no serializable fields.");
        }

        return _layout is SerializeLayout.Explicit
            ? [.. System.Linq.Enumerable.OrderBy(includedFields, f => f.Order)]
            : [.. includedFields];
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Dictionary<string, int> BuildFieldIndex()
    {
        // Performance: StringComparer.Ordinal nhanh hơn default
        Dictionary<string, int> index = new(
            _metadata.Length, System.StringComparer.Ordinal);

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
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    private static int? GetExplicitOrder(FieldInfo field)
    {
        System.Type? type = field.DeclaringType;
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsBackingFieldFor(
        FieldInfo field,
        PropertyInfo property) => field.Name == $"<{property.Name}>k__BackingField";

    #endregion Field Discovery

    #region Domain Rules - Business Logic

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SerializeLayout GetSerializeLayout()
    {
        SerializePackableAttribute? packableAttr = CustomAttributeExtensions
            .GetCustomAttribute<SerializePackableAttribute>(typeof(T));

        return packableAttr?.SerializeLayout ?? SerializeLayout.Sequential;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static SerializeLayout GetLayout() => _layout;

    #endregion Layout Detection

    #region Ensure - Fail Fast Strategy

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void EnsureExplicitLayoutIsValid()
    {
        if (_layout is not SerializeLayout.Explicit)
        {
            return;
        }

        EnsureNoDuplicateOrders();
        EnsureNoNegativeOrders();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void EnsureNoDuplicateOrders()
    {
        IEnumerable<System.Linq.IGrouping<int, FieldSchema>> orderGroups = System.Linq.Enumerable.Where(
            System.Linq.Enumerable.GroupBy(_metadata, f => f.Order),
            g => System.Linq.Enumerable.Count(g) > 1
        );

        if (System.Linq.Enumerable.Any(orderGroups))
        {
            string duplicates = string.Join(", ",
                System.Linq.Enumerable.Select(orderGroups, g =>
                    $"Order {g.Key}: [{string.Join(", ",
                        System.Linq.Enumerable.Select(g, f => f.Name))}]"));

            throw new System.InvalidOperationException(
                $"Duplicate serialize orders in type '{typeof(T).Name}': {duplicates}");
        }
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void EnsureNoNegativeOrders()
    {
        IEnumerable<FieldSchema> negativeOrders = System.Linq.Enumerable
            .Where(_metadata, f => f.Order < 0);

        if (System.Linq.Enumerable.Any(negativeOrders))
        {
            string negativeFields = string.Join(", ",
                System.Linq.Enumerable.Select(negativeOrders, f => $"{f.Name}({f.Order})"));

            throw new System.InvalidOperationException(
                $"Negative serialize orders not allowed in type '{typeof(T).Name}': {negativeFields}");
        }
    }

    #endregion Ensure - Fail Fast Strategy
}
