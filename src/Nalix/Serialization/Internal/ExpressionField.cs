using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Nalix.Serialization.Internal;

internal static class ExpressionField
{
    /// <summary>
    /// Tạo getter delegate cho field sử dụng Expression Trees
    /// </summary>
    /// <typeparam name="T">Type chứa field</typeparam>
    /// <param name="field">FieldInfo của field cần tạo getter</param>
    /// <returns>Func delegate để get giá trị field</returns>
    public static Func<T, object> CreateFieldGetter<T>(FieldInfo field)
    {
        if (field is null)
            throw new ArgumentNullException(nameof(field));

        try
        {
            if (typeof(T).IsValueType)
            {
                // Đối với struct, sử dụng reflection trực tiếp vì Expression Trees phức tạp hơn
                return (instance) =>
                {
                    object boxed = instance;
                    return field.GetValue(boxed);
                };
            }
            else
            {
                // Đối với reference types, sử dụng Expression Trees
                var instanceParam = Expression.Parameter(typeof(T), "instance");
                var fieldAccess = Expression.Field(instanceParam, field);
                var convertedField = Expression.Convert(fieldAccess, typeof(object));

                var lambda = Expression.Lambda<Func<T, object>>(convertedField, instanceParam);
                return lambda.Compile();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot create getter for field {field.Name} on type {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Tạo setter delegate cho field sử dụng Expression Trees
    /// </summary>
    /// <typeparam name="T">Type chứa field</typeparam>
    /// <param name="field">FieldInfo của field cần tạo setter</param>
    /// <returns>Action delegate để set giá trị field</returns>
    public static Action<T, object> CreateFieldSetter<T>(FieldInfo field)
    {
        if (field is null)
            throw new ArgumentNullException(nameof(field));

        try
        {
            if (typeof(T).IsValueType)
            {
                // Đối với struct, chúng ta không thể tạo setter theo cách thông thường
                // vì struct pass by value. Return null để báo hiệu dùng special approach
                return null;
            }
            else
            {
                // Đối với reference types
                return CreateReferenceTypeFieldSetter<T>(field);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot create setter for field {field.Name} on type {typeof(T).Name}", ex);
        }
    }

    private static Action<T, object> CreateReferenceTypeFieldSetter<T>(FieldInfo field)
    {
        // Tạo parameters
        var instanceParam = Expression.Parameter(typeof(T), "instance");
        var valueParam = Expression.Parameter(typeof(object), "value");

        // Convert value về đúng type của field
        var convertedValue = Expression.Convert(valueParam, field.FieldType);

        // Assign field
        var fieldAccess = Expression.Field(instanceParam, field);
        var assignment = Expression.Assign(fieldAccess, convertedValue);

        // Compile
        var lambda = Expression.Lambda<Action<T, object>>(assignment, instanceParam, valueParam);
        return lambda.Compile();
    }

    private static Action<T, object> CreateValueTypeFieldSetter<T>(FieldInfo field)
    {
        // Đối với value types, ta cần sử dụng reflection trực tiếp
        // vì Expression Trees không handle được ref parameters tốt cho structs
        return (instance, value) =>
        {
            object boxed = instance;
            field.SetValue(boxed, value);
            // Note: Điều này sẽ không update instance gốc vì struct được pass by value
            // Cần modify BinarySerializer để handle điều này
        };
    }

    /// <summary>
    /// Tạo setter đặc biệt cho value types sử dụng ref parameter
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="field">Field info</param>
    /// <returns>Action với ref parameter</returns>
    public static Action<object, object> CreateValueTypeFieldSetterByRef<T>(FieldInfo field)
    {
        return (instanceBoxed, value) =>
        {
            field.SetValue(instanceBoxed, value);
        };
    }
}
