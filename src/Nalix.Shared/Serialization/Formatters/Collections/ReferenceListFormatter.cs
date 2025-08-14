// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for <see cref="System.Collections.Generic.List{T}"/>
/// where T is a reference type.
/// </summary>
/// <typeparam name="T">The reference type of list elements.</typeparam>
[System.Diagnostics.DebuggerStepThrough]
public sealed class ReferenceListFormatter<T> : IFormatter<System.Collections.Generic.List<T>> where T : class
{
    private static readonly IFormatter<T> _elementFormatter = FormatterProvider.Get<T>();

    /// <summary>
    /// Serializes a list of reference type elements into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The list of reference type elements to serialize.</param>
    /// <exception cref="SerializationException">
    /// Thrown if the underlying formatter for type <typeparamref name="T"/> encounters an error during serialization.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T> value)
    {
        if (value is null)
        {
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        System.UInt16 count = (System.UInt16)value.Count;
        FormatterProvider.Get<System.UInt16>()
                         .Serialize(ref writer, count);

        for (System.Int32 i = 0; i < count; i++)
        {
            _elementFormatter.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes a list of reference type elements from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized list of reference type elements, or null if the serialized data represents a null list.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the list length is out of range or if the underlying formatter for type <typeparamref name="T"/> encounters an error during deserialization.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> Deserialize(ref DataReader reader)
    {
        System.UInt16 count = FormatterProvider.Get<System.UInt16>()
                                        .Deserialize(ref reader);

        if (count == 0)
        {
            return [];
        }

        if (count == SerializerBounds.Null)
        {
            return null!;
        }

        if (count > SerializerBounds.MaxArray)
        {
            throw new SerializationException($"Reference list length out of range.");
        }

        System.Collections.Generic.List<T> list = new(count);
        for (System.UInt16 i = 0; i < count; i++)
        {
            list.Add(_elementFormatter.Deserialize(ref reader));
        }

        return list;
    }
}