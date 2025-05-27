using Nalix.Common.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nalix.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    #region Static Fields

    private static readonly FieldSchema[] _metadata;
    private static readonly SerializeLayout _layout;
    private static readonly System.Collections.Generic.Dictionary<string, int> _fieldIndex;

    #endregion Static Fields

    #region Static Constructor

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2091:Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    static FieldCache()
    {
        _layout = GetSerializeLayout();
        _metadata = DiscoverFields<T>();
        _fieldIndex = BuildFieldIndex();
        EnsureExplicitLayoutIsValid();
    }

    #endregion Static Constructor

    #region Field Discovery

    private static FieldSchema[] DiscoverFields<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] TField>()
    {
        Type type = typeof(TField);
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance);

        var includedFields = new List<FieldSchema>(fields.Length);
        int sequentialOrder = 0;

        foreach (FieldInfo field in fields)
        {
            if (ShouldIgnoreField(field)) continue;

            int order;

            if (_layout is SerializeLayout.Explicit)
            {
                // Explicit: chỉ include fields có SerializeOrderAttribute
                var explicitOrder = GetExplicitOrder(field);
                if (explicitOrder is null)
                {
                    // ✅ Skip field không có order trong Explicit layout
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

        // Sort theo order nếu là Explicit layout
        return _layout is SerializeLayout.Explicit
            ? [.. includedFields.OrderBy(f => f.Order)]
            : [.. includedFields];
    }

    private static Dictionary<string, int> BuildFieldIndex()
    {
        // Performance: StringComparer.Ordinal nhanh hơn default
        Dictionary<string, int> index = new(_metadata.Length, StringComparer.Ordinal);

        for (int i = 0; i < _metadata.Length; i++)
        {
            index[_metadata[i].Name] = i;
        }

        return index;
    }

    // Updated method to move the DynamicallyAccessedMembersAttribute to the parameter
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming",
        "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. " +
        "The generic parameter of the source method or type does not have matching annotations.", Justification = "<Pending>")]
    private static int? GetExplicitOrder(FieldInfo field)
    {
        PropertyInfo property = typeof(T).GetProperties()
            .FirstOrDefault(p =>
                p.Name.Equals(field.Name, StringComparison.Ordinal) ||
                IsBackingFieldFor(field, p));

        if (property is not null)
        {
            SerializeOrderAttribute orderAttr = property.GetCustomAttribute<SerializeOrderAttribute>();
            if (orderAttr is not null) return orderAttr.Order;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsBackingFieldFor(FieldInfo field, PropertyInfo property)
        => field.Name == $"<{property.Name}>k__BackingField";

    #endregion Field Discovery

    #region Domain Rules - Business Logic

    private static bool ShouldIgnoreField(FieldInfo field)
    {
        // Rule 1: Skip backing fields nếu property có SerializeIgnoreAttribute
        if (field.Name.StartsWith('<') && field.Name.Contains(">k__BackingField"))
        {
            var propertyName = field.Name[1..field.Name.IndexOf('>')];
            var property = typeof(T).GetProperty(propertyName);

            // Nếu property bị ignore thì skip backing field
            if (property?.GetCustomAttribute<SerializeIgnoreAttribute>() is not null)
                return true;
        }

        // Rule 2: Skip fields có SerializeIgnoreAttribute
        if (field.GetCustomAttribute<SerializeIgnoreAttribute>() is not null)
            return true;

        return false;
    }

    #endregion Domain Rules - Business Logic

    #region Layout Detection

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SerializeLayout GetSerializeLayout()
    {
        SerializePackableAttribute packableAttr = typeof(T).GetCustomAttribute<SerializePackableAttribute>();
        return packableAttr?.SerializeLayout ?? SerializeLayout.Sequential;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static SerializeLayout GetLayout() => _layout;

    #endregion Layout Detection

    #region Ensure - Fail Fast Strategy

    private static void EnsureExplicitLayoutIsValid()
    {
        if (_layout is not SerializeLayout.Explicit) return;

        EnsureNoDuplicateOrders();
        EnsureNoNegativeOrders();
    }

    private static void EnsureNoDuplicateOrders()
    {
        var orderGroups = _metadata.GroupBy(f => f.Order).Where(g => g.Count() > 1);

        if (orderGroups.Any())
        {
            System.String duplicates = string.Join(", ",
                orderGroups.Select(g => $"Order {g.Key}: [{string.Join(", ", g.Select(f => f.Name))}]"));

            throw new InvalidOperationException(
                $"Duplicate serialize orders in type '{typeof(T).Name}': {duplicates}");
        }
    }

    private static void EnsureNoNegativeOrders()
    {
        var negativeOrders = _metadata.Where(f => f.Order < 0);
        if (negativeOrders.Any())
        {
            var negativeFields = string.Join(", ",
                negativeOrders.Select(f => $"{f.Name}({f.Order})"));

            throw new InvalidOperationException(
                $"Negative serialize orders not allowed in type '{typeof(T).Name}': {negativeFields}");
        }
    }

    #endregion Ensure - Fail Fast Strategy
}
