using Nalix.Common.Exceptions;
using Nalix.Serialization.Buffers;
using Nalix.Serialization.Formatters;
using Nalix.Serialization.Internal.Types;

namespace Nalix.Serialization;

/// <summary>
/// Provides serialization and deserialization methods for objects.
/// </summary>
public static class Serializer
{
    private const System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes All =
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All;

    /// <summary>
    /// Serializes an object into a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A byte array representing the serialized object.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if serialization encounters an error.
    /// </exception>
    public static byte[] Serialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(in T value)
    {
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            byte[] array = System.GC.AllocateUninitializedArray<byte>(TypeMetadata.SizeOf<T>());
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
            writer.Clear();
        }
    }

    /// <summary>
    /// Deserializes an object from a byte array.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="buffer">The byte array containing serialized data.</param>
    /// <param name="value">The reference to the object where deserialized data will be stored.</param>
    /// <returns>The number of bytes read during deserialization.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if deserialization encounters an error or if there is insufficient data in the buffer.
    /// </exception>
    public static int Deserialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        System.ReadOnlySpan<byte> buffer, ref T value)
    {
        if (!TypeMetadata.IsReferenceOrNullable<T>())
        {
            if (buffer.Length < TypeMetadata.SizeOf<T>())
            {
                throw new SerializationException($"Expected {TypeMetadata.SizeOf<T>()} bytes, found {buffer.Length} bytes.");
            }
            value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(
                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(buffer));

            return System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        }

        DataReader reader = new(buffer);
        IFormatter<T> formatter = FormatterProvider.GetComplex<T>();

        value = formatter.Deserialize(ref reader);
        return reader.BytesRead;
    }
}
