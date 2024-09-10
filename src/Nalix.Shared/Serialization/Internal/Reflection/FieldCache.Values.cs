namespace Nalix.Shared.Serialization.Internal.Reflection;

internal static partial class FieldCache<T>
{
    #region Generic Value Operations - Zero Boxing

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TField GetValue<TField>(T obj, int fieldIndex)
    {
        var metadata = _metadata[fieldIndex];

        if (metadata.FieldType != typeof(TField))
        {
            throw new System.InvalidOperationException(
                $"Field '{metadata.Name}' is of type '{metadata.FieldType}', not '{typeof(TField)}'");
        }

        return (TField)metadata.FieldInfo.GetValue(obj)!;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static TField GetValue<TField>(T obj, string fieldName)
    {
        if (!_fieldIndex.TryGetValue(fieldName, out var index))
        {
            throw new System.ArgumentException($"Field '{fieldName}' not found in {typeof(T).Name}");
        }

        return GetValue<TField>(obj, index);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static void SetValue<TField>(T obj, int fieldIndex, TField value)
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
    public static void SetValue<TField>(T obj, string fieldName, TField value)
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
    public static object GetValueBoxed(T obj, int fieldIndex)
    {
        return _metadata[fieldIndex].FieldInfo.GetValue(obj)!;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static object GetValueBoxed(T obj, string fieldName)
    {
        if (!_fieldIndex.TryGetValue(fieldName, out var index))
        {
            throw new System.ArgumentException($"Field '{fieldName}' not found in {typeof(T).Name}");
        }

        return GetValueBoxed(obj, index);
    }

    #endregion Boxed Value Operations - Fallback
}