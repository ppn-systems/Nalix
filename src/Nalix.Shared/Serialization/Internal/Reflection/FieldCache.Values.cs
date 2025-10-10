// Copyright (c) 2025 PPN Corporation. All rights reserved.

#if DEBUG
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nalix.Shared.Serialization.Benchmarks")]
#endif

namespace Nalix.Shared.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    #region Generic Value Operations - Zero Boxing

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TField GetValue<TField>(T obj, System.Int32 fieldIndex)
    {
        var metadata = _metadata[fieldIndex];

        return metadata.FieldType != typeof(TField)
            ? throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' is of type '{metadata.FieldType}', not '{typeof(TField)}'")
            : (TField)metadata.FieldInfo.GetValue(obj)!;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TField GetValue<TField>(T obj, System.String fieldName)
    {
        return !_fieldIndex.TryGetValue(fieldName, out var index)
            ? throw new System.ArgumentException($"Field '{fieldName}' not found in {typeof(T).Name}")
            : GetValue<TField>(obj, index);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TField>(T obj, System.Int32 fieldIndex, TField value)
    {
        var metadata = _metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' is of type '{metadata.FieldType}', not '{typeof(TField)}'");
        }

        metadata.FieldInfo.SetValue(obj, value);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TField>(T obj, System.String fieldName, TField value)
    {
        if (!_fieldIndex.TryGetValue(fieldName, out var index))
        {
            throw new System.ArgumentException($"Field '{fieldName}' not found in {typeof(T).Name}");
        }

        SetValue(obj, index, value);
    }

    #endregion Generic Value Operations - Zero Boxing

    #region Boxed Value Operations - Fallback

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Object GetValueBoxed(T obj, System.Int32 fieldIndex) => _metadata[fieldIndex].FieldInfo.GetValue(obj)!;

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static System.Object GetValueBoxed(T obj, System.String fieldName)
    {
        return !_fieldIndex.TryGetValue(fieldName, out var index)
            ? throw new System.ArgumentException($"Field '{fieldName}' not found in {typeof(T).Name}")
            : GetValueBoxed(obj, index);
    }

    #endregion Boxed Value Operations - Fallback
}