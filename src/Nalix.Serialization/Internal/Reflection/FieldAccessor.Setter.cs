using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Serialization.Internal.Reflection;

internal sealed partial class FieldAccessor<T>
{
    private static Action<T, object> CreateSetter(PropertyInfo property)
    {
        var objParam = Expression.Parameter(typeof(T), "obj");
        var valueParam = Expression.Parameter(typeof(object), "value");

        var body = Expression.Assign(
            Expression.Property(Expression.Convert(objParam, property.DeclaringType!), property),
            Expression.Convert(valueParam, property.PropertyType));

        return Expression.Lambda<Action<T, object>>(body, objParam, valueParam).Compile();
    }

    private static Action<T, object> CreateSetter(FieldInfo field)
    {
        var objParam = Expression.Parameter(typeof(T), "obj");
        var valueParam = Expression.Parameter(typeof(object), "value");

        var body = Expression.Assign(
            Expression.Field(Expression.Convert(objParam, field.DeclaringType!), field),
            Expression.Convert(valueParam, field.FieldType));

        return Expression.Lambda<Action<T, object>>(body, objParam, valueParam).Compile();
    }
}
