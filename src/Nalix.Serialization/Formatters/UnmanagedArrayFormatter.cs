using Nalix.Serialization.Internal.Types;

namespace Nalix.Serialization.Formatters;

/// <summary>
/// Formatter cho mảng các kiểu unmanaged.
/// </summary>
public sealed class UnmanagedArrayFormatter<T> : IFormatter<T[]> where T : unmanaged
{
    public void Serialize(ref SerializationWriter writer, T[] value)
    {
        if (value == null)
        {
            writer.Write(-1); // Quy ước: -1 nghĩa là null array
            return;
        }
        writer.Write(value.Length); // Ghi length trước

        if (value.Length == 0) return;

        int totalBytes = value.Length * TypeMetadata.GetSizeOf<T>();
        var span = writer.GetSpan(totalBytes);

        // Copy block memory
        unsafe
        {
            fixed (T* src = value)
            fixed (byte* dst = span)
            {
                System.Buffer.MemoryCopy(src, dst, totalBytes, totalBytes);
            }
        }
        writer.Advance(totalBytes);
    }

    public T[] Deserialize(ref SerializationReader reader)
    {
        int length = reader.Read<int>();
        if (length == -1) return null;
        if (length == 0) return System.Array.Empty<T>();

        int totalBytes = length * TypeMetadata.GetSizeOf<T>();
        var span = reader.GetSpan(totalBytes);
        T[] result = new T[length];

        unsafe
        {
            fixed (byte* src = span)
            fixed (T* dst = result)
            {
                System.Buffer.MemoryCopy(src, dst, totalBytes, totalBytes);
            }
        }
        reader.Advance(totalBytes);
        return result;
    }
}
