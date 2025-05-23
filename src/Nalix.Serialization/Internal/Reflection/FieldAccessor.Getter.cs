using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Serialization.Internal.Reflection;

internal sealed partial class FieldAccessor<T>
{
    private static Func<T, object> CreateGetter(PropertyInfo property)
    {
        var param = Expression.Parameter(typeof(T), "obj");
        var propertyAccess = Expression.Property(Expression.Convert(param, property.DeclaringType!), property);
        var convert = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<T, object>>(convert, param).Compile();
    }

    private static Func<T, object> CreateGetter(FieldInfo field)
    {
        var param = Expression.Parameter(typeof(T), "obj");
        var fieldAccess = Expression.Field(Expression.Convert(param, field.DeclaringType!), field);
        var convert = Expression.Convert(fieldAccess, typeof(object));
        return Expression.Lambda<Func<T, object>>(convert, param).Compile();
    }
}
