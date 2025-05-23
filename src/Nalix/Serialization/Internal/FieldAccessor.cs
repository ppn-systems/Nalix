using System;
using System.Collections.Generic;
using System.Reflection;

namespace Nalix.Serialization.Internal;

internal static class FieldAccessor<T>
{
    private static readonly Dictionary<FieldInfo, Func<T, object>> _getters = [];
    private static readonly Dictionary<FieldInfo, Action<T, object>> _setters = [];
    private static readonly Dictionary<FieldInfo, Action<object, object>> _valueTypeSetters = [];

    public static Func<T, object> GetGetter(FieldInfo field) => _getters[field];

    public static Action<T, object> GetSetter(FieldInfo field) => _setters[field];

    public static Action<object, object> GetValueTypeSetter(FieldInfo field) => _valueTypeSetters[field];

    public static void InitializeFieldAccessors(IEnumerable<FieldInfo> fields)
    {
        foreach (var field in fields)
        {
            Func<T, object> getter = ExpressionField.CreateFieldGetter<T>(field)
                ?? throw new InvalidOperationException($"Cannot create getter for {field.Name}");

            Action<T, object> setter = ExpressionField.CreateFieldSetter<T>(field);

            _getters[field] = getter;

            if (typeof(T).IsValueType)
            {
                _valueTypeSetters[field] = ExpressionField.CreateValueTypeFieldSetterByRef<T>(field);
            }
            else
            {
                if (setter is null)
                    throw new InvalidOperationException($"Cannot create setter for {field.Name}");
                _setters[field] = setter;
            }
        }
    }
}
