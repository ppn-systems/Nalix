using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Serialization.Internal.Reflection;

internal sealed partial class FieldAccessor<T, TField>
{
    private static Action<T, TField> CreateSetter(PropertyInfo property)
    {
        var objParam = Expression.Parameter(typeof(T), "obj");
        var valueParam = Expression.Parameter(typeof(TField), "value");

        var body = Expression.Assign(
            Expression.Property(Expression.Convert(objParam, property.DeclaringType!), property),
            Expression.Convert(valueParam, property.PropertyType));

        return Expression.Lambda<Action<T, TField>>(body, objParam, valueParam).Compile();
    }

    private static Action<T, TField> CreateSetter(FieldInfo field)
    {
        var objParam = Expression.Parameter(typeof(T), "obj");
        var valueParam = Expression.Parameter(typeof(TField), "value");

        var body = Expression.Assign(
            Expression.Field(Expression.Convert(objParam, field.DeclaringType!), field),
            Expression.Convert(valueParam, field.FieldType));

        return Expression.Lambda<Action<T, TField>>(body, objParam, valueParam).Compile();
    }
}
