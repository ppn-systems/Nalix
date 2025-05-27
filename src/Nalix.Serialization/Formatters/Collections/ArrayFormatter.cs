using Nalix.Common.Exceptions;
using Nalix.Serialization.Buffers;
using Nalix.Serialization.Internal.Types;

namespace Nalix.Serialization.Formatters.Collections;

/// <summary>
/// Provides serialization and deserialization for arrays of unmanaged types.
/// </summary>
/// <typeparam name="T">The unmanaged type of the array elements.</typeparam>
public sealed class ArrayFormatter<T> : IFormatter<T[]> where T : unmanaged
{
    /// <summary>
    /// Serializes an array of unmanaged values into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The array to serialize.</param>
    public unsafe void Serialize(ref DataWriter writer, T[] value)
    {
        if (value == null)
        {
            // Convention: -1 indicates a null array
            FormatterProvider
                .Get<ushort>()
                .Serialize(ref writer, SerializationLimits.Null);

            return;
        }

        FormatterProvider
            .Get<ushort>()
            .Serialize(ref writer, unchecked((ushort)value.Length));

        if (value.Length == 0) return;

        int totalBytes = value.Length * TypeMetadata.SizeOf<T>();

        writer.Expand(totalBytes);
        var span = writer.GetSpan(totalBytes);

        // Copy block memory
        fixed (T* src = value)
        fixed (byte* dst = span)
        {
            System.Buffer.MemoryCopy(src, dst, totalBytes, totalBytes);
        }

        writer.Advance(totalBytes);
    }

    /// <summary>
    /// Deserializes an array of unmanaged values from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized array of unmanaged values, or null if applicable.</returns>
    public unsafe T[] Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider
            .Get<ushort>()
            .Deserialize(ref reader);

        if (length == 0) return [];
        if (length == SerializationLimits.Null) return null;
        if (length > SerializationLimits.MaxArray)
            throw new SerializationException("Array length out of range");

        int total = length * TypeMetadata.SizeOf<T>();
        System.ReadOnlySpan<byte> span = reader.GetSpan(total);
        T[] result = new T[length];

        fixed (byte* src = span)
        fixed (T* dst = result)
        {
            System.Buffer.MemoryCopy(src, dst, total, total);
        }

        reader.Advance(total);
        return result;
    }
}
