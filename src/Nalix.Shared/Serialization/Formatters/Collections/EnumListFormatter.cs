// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Formatters.Primitives;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for <see cref="System.Collections.Generic.List{T}"/>
/// where T is an enum type.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
[System.Diagnostics.DebuggerStepThrough]
public sealed class EnumListFormatter<T> : IFormatter<System.Collections.Generic.List<T>>
    where T : struct, System.Enum
{
    private static readonly EnumFormatter<T> _enumFormatter = new();

    /// <summary>
    /// Serializes a list of enum values into the provided writer using their underlying primitive type.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The list of enum values to serialize.</param>
    /// <exception cref="SerializationException">
    /// Thrown if the underlying type of the enum is not supported by the <see cref="EnumFormatter{T}"/>.
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
            _enumFormatter.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes a list of enum values from the provided reader using their underlying primitive type.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized list of enum values, or null if the serialized data represents a null list.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the list length is out of range or if the underlying type of the enum is not supported by the <see cref="EnumFormatter{T}"/>.
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
            throw new SerializationException("Enum list length out of range.");
        }

        System.Collections.Generic.List<T> result = new(count);
        for (System.UInt16 i = 0; i < count; i++)
        {
            result.Add(_enumFormatter.Deserialize(ref reader));
        }

        return result;
    }
}
