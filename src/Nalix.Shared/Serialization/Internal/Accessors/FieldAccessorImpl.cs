// Copyright (c) 2025 PPN Corporation. All rights reserved.

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
internal sealed class FieldAccessorImpl<T, TField>(System.Int32 index) : FieldAccessor<T>
{
    #region Fields

    private readonly System.Int32 _index = index;
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

        TField value = FieldCache<T>.GetValue<TField>(obj, _index);
        _formatter.Serialize(ref writer, value);
    }

    /// <summary>
    /// Deserializes field sử dụng FieldCache cho zero-boxing access.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref DataReader reader, T obj)
    {
        System.ArgumentNullException.ThrowIfNull(obj);

        TField value = _formatter.Deserialize(ref reader);
        FieldCache<T>.SetValue(obj, _index, value);
    }

    #endregion Serialization Implementation
}