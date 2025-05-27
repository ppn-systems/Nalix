using Nalix.Common.Serialization;
using System;
using System.Linq;

namespace Nalix.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    #region Validation - Fail Fast Strategy

    private static void ValidateExplicitLayout()
    {
        if (_layout is not SerializeLayout.Explicit) return;

        ValidateNoDuplicateOrders();
        ValidateNoNegativeOrders();
    }

    private static void ValidateNoDuplicateOrders()
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

    private static void ValidateNoNegativeOrders()
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

    #endregion Validation - Fail Fast Strategy

    #region Exception Helpers - Không inline để optimize hot paths

    private static void ThrowFieldTypeMismatch(string fieldName, Type actualType, Type expectedType)
    {
        throw new InvalidOperationException(
            $"Field '{fieldName}' is of type '{actualType}', not '{expectedType}'");
    }

    private static void ThrowFieldNotFound(string fieldName)
    {
        throw new ArgumentException($"Field '{fieldName}' not found in {typeof(T).Name}");
    }

    #endregion Exception Helpers - Không inline để optimize hot paths
}
