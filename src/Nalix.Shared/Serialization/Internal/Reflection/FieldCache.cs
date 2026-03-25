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
    private static readonly Dictionary<System.String, System.Int32> _fieldIndex;

    private static readonly Dictionary<System.String, PropertyInfo> _propertyCache;

    #endregion Static Fields

    #region Compiled Delegates Cache

    // Store as object delegates, will be cast at runtime
    private static readonly System.Delegate[] _getters;
    private static readonly System.Delegate[] _setters;

    #endregion

    #region Static Constructor

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2091:Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    static FieldCache()
    {
        _layout = GetSerializeLayout();

        _propertyCache = new Dictionary<System.String, PropertyInfo>(
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

        for (System.Int32 i = 0; i < _metadata.Length; i++)
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
        while (type != null && type != typeof(System.Object))
        {
            fields.AddRange(type.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly));
            type = type.BaseType!;
        }

        List<FieldSchema> includedFields = new(fields.Count);
        System.Int32 sequentialOrder = 0;

        foreach (FieldInfo field in fields)
        {
            if (ShouldIgnoreField(field))
            {
                continue;
            }

            System.Int32 order;

            if (_layout is SerializeLayout.Explicit)
            {
                var explicitOrder = GetExplicitOrder(field);
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
    private static Dictionary<System.String, System.Int32> BuildFieldIndex()
    {
        // Performance: StringComparer.Ordinal nhanh hơn default
        Dictionary<System.String, System.Int32> index = new(
            _metadata.Length, System.StringComparer.Ordinal);

        for (System.Int32 i = 0; i < _metadata.Length; i++)
        {
            index[_metadata[i].Name] = i;
        }

        return index;
    }

    // Updated method to move the DynamicallyAccessedMembersAttribute to the parameter
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    private static System.Int32? GetExplicitOrder(FieldInfo field)
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
                SerializeOrderAttribute? attr = CustomAttributeExtensions.GetCustomAttribute<SerializeOrderAttribute>(property);
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
    private static System.Boolean IsBackingFieldFor(
        FieldInfo field,
        PropertyInfo property) => field.Name == $"<{property.Name}>k__BackingField";

    #endregion Field Discovery

    #region Domain Rules - Business Logic

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static System.Boolean ShouldIgnoreField(FieldInfo field)
    {
        // Rule 1: Skip backing fields nếu property có SerializeIgnoreAttribute
        if (field.Name.StartsWith('<') && field.Name.Contains(">k__BackingField"))
        {
            var propertyName = field.Name[1..field.Name.IndexOf('>')];
            if (_propertyCache.TryGetValue(propertyName, out PropertyInfo? property))
            {
                // Nếu property bị ignore thì skip backing field
                if (CustomAttributeExtensions.GetCustomAttribute<SerializeIgnoreAttribute>(property) is not null)
                {
                    return true;
                }
            }
        }

        // Rule 2: Skip fields có SerializeIgnoreAttribute
        return CustomAttributeExtensions.GetCustomAttribute<SerializeIgnoreAttribute>(field) is not null;
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
        IEnumerable<System.Linq.IGrouping<System.Int32, FieldSchema>> orderGroups = System.Linq.Enumerable.Where(
            System.Linq.Enumerable.GroupBy(_metadata, f => f.Order),
            g => System.Linq.Enumerable.Count(g) > 1
        );

        if (System.Linq.Enumerable.Any(orderGroups))
        {
            System.String duplicates = System.String.Join(", ",
                System.Linq.Enumerable.Select(orderGroups, g =>
                    $"Order {g.Key}: [{System.String.Join(", ",
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
            var negativeFields = System.String.Join(", ",
                System.Linq.Enumerable.Select(negativeOrders, f => $"{f.Name}({f.Order})"));

            throw new System.InvalidOperationException(
                $"Negative serialize orders not allowed in type '{typeof(T).Name}': {negativeFields}");
        }
    }

    #endregion Ensure - Fail Fast Strategy
}
