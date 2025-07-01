using Nalix.Common.Serialization;
using Nalix.Common.Serialization.Attributes;

namespace Nalix.Shared.Serialization.Internal.Reflection;

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
        System.Type type = typeof(TField);
        System.Reflection.FieldInfo[] fields = type.GetFields(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        System.Collections.Generic.List<FieldSchema> includedFields = new(fields.Length);
        int sequentialOrder = 0;

        foreach (System.Reflection.FieldInfo field in fields)
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
            ? [.. System.Linq.Enumerable.OrderBy(includedFields, f => f.Order)]
            : [.. includedFields];
    }

    private static System.Collections.Generic.Dictionary<System.String, System.Int32> BuildFieldIndex()
    {
        // Performance: StringComparer.Ordinal nhanh hơn default
        System.Collections.Generic.Dictionary<System.String, System.Int32> index = new(
            _metadata.Length, System.StringComparer.Ordinal);

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
    private static int? GetExplicitOrder(System.Reflection.FieldInfo field)
    {
        System.Reflection.PropertyInfo? property =
            System.Linq.Enumerable.FirstOrDefault(
                typeof(T).GetProperties(),
                p => p.Name.Equals(field.Name, System.StringComparison.Ordinal) ||
                     IsBackingFieldFor(field, p));

        if (property is not null)
        {
            SerializeOrderAttribute? orderAttr = System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<SerializeOrderAttribute>(property);
            if (orderAttr is not null) return orderAttr.Order;
        }

        return null;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool IsBackingFieldFor(
        System.Reflection.FieldInfo field,
        System.Reflection.PropertyInfo property)
        => field.Name == $"<{property.Name}>k__BackingField";

    #endregion Field Discovery

    #region Domain Rules - Business Logic

    private static bool ShouldIgnoreField(System.Reflection.FieldInfo field)
    {
        // Rule 1: Skip backing fields nếu property có SerializeIgnoreAttribute
        if (field.Name.StartsWith('<') && field.Name.Contains(">k__BackingField"))
        {
            var propertyName = field.Name[1..field.Name.IndexOf('>')];
            var property = typeof(T).GetProperty(propertyName);

            // Nếu property bị ignore thì skip backing field
            if (property is not null &&
                System.Reflection.CustomAttributeExtensions
                    .GetCustomAttribute<SerializeIgnoreAttribute>(property) is not null)
            {
                return true;
            }
        }

        // Rule 2: Skip fields có SerializeIgnoreAttribute
        if (System.Reflection.CustomAttributeExtensions
                .GetCustomAttribute<SerializeIgnoreAttribute>(field) is not null)
        {
            return true;
        }

        return false;
    }

    #endregion Domain Rules - Business Logic

    #region Layout Detection

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static SerializeLayout GetSerializeLayout()
    {
        SerializePackableAttribute? packableAttr = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<SerializePackableAttribute>(typeof(T));

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
        var orderGroups = System.Linq.Enumerable.Where(
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

    private static void EnsureNoNegativeOrders()
    {
        System.Collections.Generic.IEnumerable<FieldSchema> negativeOrders = System.Linq.Enumerable
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