// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
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

    #region Struct Setter (ref T)

    // Action<T,TField> không support ref T nên cần declare delegate riêng
    private delegate void RefSetter<TVal>(ref T obj, TVal value);

    // Cache riêng cho ref-setters — key là fieldIndex
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int32, System.Object> _refSetterCache = new();

    /// <summary>
    /// Set field value trực tiếp lên struct gốc thông qua ref T.
    /// Chỉ dùng cho value types (struct) — nếu dùng cho class thì dùng overload không có ref.
    /// </summary>
    [System.Diagnostics.StackTraceHidden]
    [System.Diagnostics.DebuggerStepThrough]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TField>(ref T obj, System.Int32 fieldIndex, TField value)
    {
        FieldSchema metadata = _metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' expects type '{metadata.FieldType}', " +
                $"but got '{typeof(TField)}'.");
        }

        RefSetter<TField> setter = GetOrCreateRefSetter<TField>(fieldIndex, metadata);
        setter(ref obj, value);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] // NoInlining vì chỉ chạy 1 lần per field
    private static RefSetter<TField> GetOrCreateRefSetter<TField>(
        System.Int32 fieldIndex,
        FieldSchema metadata)
    {
        return (RefSetter<TField>)_refSetterCache.GetOrAdd(fieldIndex, _ =>
        {
            // (ref T obj, TField value) => obj.<FieldName> = value
            System.Linq.Expressions.ParameterExpression objParam = System.Linq.Expressions.Expression.Parameter(typeof(T).MakeByRefType(), "obj");         // ref T
            System.Linq.Expressions.ParameterExpression valParam = System.Linq.Expressions.Expression.Parameter(typeof(TField), "value");
            System.Linq.Expressions.MemberExpression fieldExpr = System.Linq.Expressions.Expression.Field(objParam, metadata.FieldInfo);
            System.Linq.Expressions.BinaryExpression assignExpr = System.Linq.Expressions.Expression.Assign(fieldExpr, valParam);

            return System.Linq.Expressions.Expression.Lambda<RefSetter<TField>>(assignExpr, objParam, valParam).Compile();
        });
    }

    #endregion Struct Setter (ref T)
}
