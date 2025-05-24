using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Nalix.Serialization;

public static class Serializer
{
    /// <summary>
    /// Serialize đối tượng thành mảng byte.
    /// </summary>
    public static byte[] Serialize<T>(in T value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            var array = System.GC.AllocateUninitializedArray<byte>(Unsafe.SizeOf<T>());
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), value);
            return array;
        }
    }

    /// <summary>
    /// Deserialize từ mảng byte thành đối tượng.
    /// </summary>
    public static int Deserialize<[
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> buffer, ref T value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            if (buffer.Length < Unsafe.SizeOf<T>())
            {
                throw new SerializationException($"{Unsafe.SizeOf<T>()}{buffer.Length}");
            }
            value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(buffer));
            return Unsafe.SizeOf<T>();
        }

        var reader = new BinaryReader(buffer);
        try
        {
            reader.ReadValue(ref value);
            return reader.Consumed;
        }
        finally
        {
            reader.Dispose();
            state.Reset();
        }
    }
}
