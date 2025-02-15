// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    #region Generic Value Operations - Zero Boxing

    /// <summary>
    /// Creates a compiled getter delegate for a field.
    /// Signature:  Func&lt;T, TField&gt;
    /// </summary>
    private static System.Delegate CreateGetter(System.Reflection.FieldInfo field)
    {
        // Parameter: T obj
        // Body: obj. FieldName
        // Lambda: (T obj) => obj.FieldName

        System.Type x00 = typeof(System.Func<,>).MakeGenericType(typeof(T), field.FieldType);
        System.Linq.Expressions.ParameterExpression x01 = System.Linq.Expressions.Expression.Parameter(typeof(T), "obj");
        System.Linq.Expressions.MemberExpression x02 = System.Linq.Expressions.Expression.Field(x01, field);
        System.Linq.Expressions.LambdaExpression x03 = System.Linq.Expressions.Expression.Lambda(x00, x02, x01);

        return x03.Compile();
    }

    /// <summary>
    /// Creates a compiled setter delegate for a field.
    /// Signature: Action&lt;T, TField&gt;
    /// </summary>
    private static System.Delegate CreateSetter(System.Reflection.FieldInfo field)
    {
        // Parameters: T obj, TField value
        // Body: obj.FieldName = value
        // Lambda:  (T obj, TField value) => obj.FieldName = value

        System.Type x00 = typeof(System.Action<,>).MakeGenericType(typeof(T), field.FieldType);
        System.Linq.Expressions.ParameterExpression x01 = System.Linq.Expressions.Expression.Parameter(typeof(T), "obj");
        System.Linq.Expressions.ParameterExpression x02 = System.Linq.Expressions.Expression.Parameter(field.FieldType, "value");
        System.Linq.Expressions.MemberExpression x03 = System.Linq.Expressions.Expression.Field(x01, field);
        System.Linq.Expressions.BinaryExpression x04 = System.Linq.Expressions.Expression.Assign(x03, x02);
        System.Linq.Expressions.LambdaExpression x05 = System.Linq.Expressions.Expression.Lambda(x00, x04, x01, x02);

        return x05.Compile();
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static TField GetValue<TField>(T obj, System.Int32 fieldIndex)
    {
        FieldSchema metadata = _metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' is of type '{metadata.FieldType}', not '{typeof(TField)}'");
        }

        // Cast and invoke compiled delegate - NO BOXING! 
        System.Func<T, TField> getter = (System.Func<T, TField>)_getters[fieldIndex];
        return getter(obj);
    }

    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void SetValue<TField>(T obj, System.Int32 fieldIndex, TField value)
    {
        FieldSchema metadata = _metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' is of type '{metadata.FieldType}', not '{typeof(TField)}'");
        }

        // Cast and invoke compiled delegate - NO BOXING!
        System.Action<T, TField> setter = (System.Action<T, TField>)_setters[fieldIndex];
        setter(obj, value);
    }

    #endregion Generic Value Operations - Zero Boxing
}