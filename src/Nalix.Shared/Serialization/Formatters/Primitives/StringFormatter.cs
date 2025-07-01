using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Cung cấp serialize/deserialize cho kiểu string với hiệu năng cao (dùng unsafe, length dạng ushort).
/// </summary>
public sealed class StringFormatter : IFormatter<System.String>
{
    private static readonly System.Text.Encoding Utf8 = System.Text.Encoding.UTF8;

    /// <summary>
    /// Serializes a string value into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The string value to serialize.</param>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe void Serialize(ref DataWriter writer, System.String value)
    {
        if (value == null)
        {
            // 65535 biểu diễn null
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, SerializerBounds.Null);
            return;
        }

        if (value.Length == 0)
        {
            FormatterProvider.Get<System.UInt16>()
                             .Serialize(ref writer, 0);
            return;
        }

        // Tính trước số byte sẽ cần khi encode UTF8
        int byteCount = Utf8.GetByteCount(value);
        if (byteCount > SerializerBounds.MaxString)
            throw new SerializationException("The string exceeds the allowed limit.");

        FormatterProvider.Get<System.UInt16>()
                         .Serialize(ref writer, (System.UInt16)byteCount);

        if (byteCount > 0)
        {
            writer.Expand(byteCount);
            ref System.Byte destination = ref writer.GetFreeBufferReference();

            fixed (System.Char* src = value)
            fixed (System.Byte* dest = &destination)
            {
                // Encode trực tiếp vào dest
                System.Int32 bytesWritten = Utf8.GetBytes(src, value.Length, dest, byteCount);

                if (bytesWritten != byteCount)
                    throw new SerializationException("UTF8 encoding error for the string.");
            }

            writer.Advance(byteCount);
        }
    }

    /// <summary>
    /// Deserializes a string value from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized string value.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the string length exceeds the maximum allowed limit.
    /// </exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public unsafe System.String Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider.Get<ushort>().Deserialize(ref reader);
        if (length == 0) return string.Empty;
        if (length == SerializerBounds.Null) return null!;
        if (length > SerializerBounds.MaxString)
            throw new SerializationException("String length out of range");

        System.ReadOnlySpan<System.Byte> dest = reader.GetSpan(length);

        System.String result;
        fixed (System.Byte* src = dest)
        {
            result = Utf8.GetString(src, length);
        }

        reader.Advance(length);
        return result;
    }
}
