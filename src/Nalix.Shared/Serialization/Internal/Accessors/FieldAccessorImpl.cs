using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Internal.Reflection;

namespace Nalix.Shared.Serialization.Internal.Accessors;

/// <summary>
/// Strongly-typed field accessor implementation eliminates boxing
/// và leverage FieldCache cho optimal performance.
/// </summary>
/// <typeparam name="T">Object type chứa field.</typeparam>
/// <typeparam name="TField">Field type.</typeparam>
internal sealed class FieldAccessorImpl<T, TField>(int index) : FieldAccessor<T>
{
    #region Fields

    private readonly int _index = index;
    private readonly IFormatter<TField> _formatter = FormatterProvider.Get<TField>();

    #endregion Fields

    #region Serialization Implementation

    /// <summary>
    /// Serializes field sử dụng FieldCache cho zero-boxing access.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref DataWriter writer, T obj)
    {
        System.ArgumentNullException.ThrowIfNull(obj);

#if DEBUG
        var field = FieldCache<T>.GetField(_index);
        string fieldName = field.Name ?? $"Field#{_index}";
        System.Console.WriteLine($"[DEBUG] Serializing field '{fieldName}' ({field.FieldType.Name}) in {typeof(T).Name}");
#endif

        try
        {
            TField value = FieldCache<T>.GetValue<TField>(obj, _index);
            _formatter.Serialize(ref writer, value);

#if DEBUG
            System.Console.WriteLine($"[DEBUG] ✔ Serialized field '{fieldName}'");
#endif
        }
        catch (System.Exception ex)
        {
            throw new System.InvalidOperationException(
                $"Failed to serialize field '{fieldName}' of type '{typeof(TField).Name}' in '{typeof(T).Name}'.", ex);
        }
    }

    /// <summary>
    /// Deserializes field sử dụng FieldCache cho zero-boxing access.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref DataReader reader, T obj)
    {
        System.ArgumentNullException.ThrowIfNull(obj);

        try
        {
            TField value = _formatter.Deserialize(ref reader);
            // Zero-boxing field assignment thông qua FieldCache
            FieldCache<T>.SetValue(obj, _index, value);
        }
        catch (System.Exception ex)
        {
            throw new System.InvalidOperationException($"Failed to deserialize field at index {_index}", ex);
        }
    }

    #endregion Serialization Implementation
}
