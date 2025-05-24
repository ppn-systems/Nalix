using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Serialization.Internal.Reflection;

internal sealed partial class FieldAccessor<T, TField>
{
    private static Func<T, TField> CreateGetter(PropertyInfo property)
    {
        var param = Expression.Parameter(typeof(T), "obj");
        var propertyAccess = Expression.Property(Expression.Convert(param, property.DeclaringType!), property);
        var convert = Expression.Convert(propertyAccess, typeof(TField));
        return Expression.Lambda<Func<T, TField>>(convert, param).Compile();
    }

    private static Func<T, TField> CreateGetter(FieldInfo field)
    {
        var param = Expression.Parameter(typeof(T), "obj");
        var fieldAccess = Expression.Field(Expression.Convert(param, field.DeclaringType!), field);
        var convert = Expression.Convert(fieldAccess, typeof(TField));
        return Expression.Lambda<Func<T, TField>>(convert, param).Compile();
    }
}
