using System;
using System.Reflection;

namespace Nalix.Serialization.Internal;

internal class ExpressionField
{
    public static Func<T, object> CreateFieldGetter<T>(FieldInfo field)
    {
        var param = System.Linq.Expressions.Expression.Parameter(typeof(T), "obj");
        System.Linq.Expressions.Expression fieldAccess = System.Linq.Expressions.Expression.Field(param, field);
        if (field.FieldType.IsValueType)
            fieldAccess = System.Linq.Expressions.Expression.Convert(fieldAccess, typeof(object));
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, object>>(fieldAccess, param);
        return lambda.Compile();
    }

    public static Action<T, object> CreateFieldSetter<T>(FieldInfo field)
    {
        var paramObj = System.Linq.Expressions.Expression.Parameter(typeof(T), "obj");
        var paramVal = System.Linq.Expressions.Expression.Parameter(typeof(object), "value");
        // Unbox value nếu là value type
        System.Linq.Expressions.Expression valueCast = field.FieldType.IsValueType
            ? System.Linq.Expressions.Expression.Convert(paramVal, field.FieldType)
            : System.Linq.Expressions.Expression.TypeAs(paramVal, field.FieldType);
        var fieldAccess = System.Linq.Expressions.Expression.Field(paramObj, field);
        var assign = System.Linq.Expressions.Expression.Assign(fieldAccess, valueCast);
        var lambda = System.Linq.Expressions.Expression.Lambda<Action<T, object>>(assign, paramObj, paramVal);
        return lambda.Compile();
    }
}
