using Nalix.Serialization.Buffers;
using Nalix.Serialization.Formatters;
using Nalix.Serialization.Internal.Types;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Nalix.Serialization;

public static class Serializer
{
    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes All =
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All;

    /// <summary>
    /// Serialize đối tượng thành mảng byte.
    /// </summary>
    public static byte[] Serialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(in T value)
    {
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            byte[] array = System.GC.AllocateUninitializedArray<byte>(
                System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), value);
            return array;
        }

        IFormatter<T> formatter = FormatterProvider.GetComplex<T>();
        TypeMetadata.TryGetFixedOrUnmanagedSize<T>(out int size);
        DataWriter writer = new(size);

        try
        {
            formatter.Serialize(ref writer, value);
            return writer.ToArray().ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>
    /// Deserialize từ mảng byte thành đối tượng.
    /// </summary>
    public static int Deserialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        System.ReadOnlySpan<byte> buffer, ref T value)
    {
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            if (buffer.Length < TypeMetadata.SizeOf<T>())
            {
                throw new SerializationException($"{TypeMetadata.SizeOf<T>()}{buffer.Length}");
            }
            value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(buffer));
            return System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        }

        DataReader reader = new(buffer);
        IFormatter<T> formatter = FormatterProvider.GetComplex<T>();

        value = formatter.Deserialize(ref reader);
        return reader.BytesRead;
    }
}
