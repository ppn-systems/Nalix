using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of reference types.
/// </summary>
/// <typeparam name="T">The reference type of the array elements.</typeparam>
public sealed class ReferenceArrayFormatter<T> : IFormatter<T[]> where T : class
{
    /// <summary>
    /// Serializes an array of reference type objects into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The array of reference type objects to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, T[] value)
    {
        if (value == null)
        {
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        FormatterProvider.Get<System.UInt16>()
                         .Serialize(ref writer, (System.UInt16)value.Length);

        if (value.Length == 0) return;

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        for (System.UInt16 i = 0; i < value.Length; i++)
        {
            formatter.Serialize(ref writer, value[i]);
        }
    }

    /// <summary>
    /// Deserializes an array of reference type objects from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized array of reference type objects, or null if the serialized data represents a null array.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public T[] Deserialize(ref DataReader reader)
    {
        System.UInt16 length = FormatterProvider.Get<System.UInt16>()
                                                .Deserialize(ref reader);

        if (length == SerializerBounds.Null) return null!;
        if (length == 0) return [];

        IFormatter<T> formatter = FormatterProvider.Get<T>();
        T[] array = new T[length];
        for (System.UInt16 i = 0; i < length; i++)
        {
            array[i] = formatter.Deserialize(ref reader);
        }

        return array;
    }
}
