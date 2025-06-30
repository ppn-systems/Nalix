using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for lists of elements.
/// </summary>
/// <typeparam name="T">The type of the elements in the list.</typeparam>
public sealed class ListFormatter<T> : IFormatter<System.Collections.Generic.List<T>>
{
    /// <summary>
    /// Serializes a list of elements into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The list of elements to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, System.Collections.Generic.List<T> value)
    {
        if (value == null)
        {
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        FormatterProvider.Get<System.UInt16>()
                         .Serialize(ref writer, (System.UInt16)value.Count);

        if (value.Count == 0) return;

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        for (System.UInt16 i = 0; i < value.Count; i++)
        {
            formatter.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes a list of elements from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized list of elements, or null if the serialized data represents a null list.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public System.Collections.Generic.List<T> Deserialize(ref DataReader reader)
    {
        System.UInt16 length = FormatterProvider.Get<System.UInt16>()
                                                .Deserialize(ref reader);

        if (length == SerializerBounds.Null) return [];
        if (length == 0) return [];

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        System.Collections.Generic.List<T> list = new(length);
        for (System.UInt16 i = 0; i < length; i++)
        {
            list.Add(formatter.Deserialize(ref reader));
        }

        return list;
    }
}
