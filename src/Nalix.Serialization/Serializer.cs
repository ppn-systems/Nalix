using System;
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            byte[] array = System.GC.AllocateUninitializedArray<byte>(
                System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
            System.Runtime.CompilerServices.Unsafe.WriteUnaligned(
                ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array), value);
            return array;
        }
    }

    /// <summary>
    /// Deserialize từ mảng byte thành đối tượng.
    /// </summary>
    public static int Deserialize<[
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(All)] T>(
        System.ReadOnlySpan<byte> buffer, ref T value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            if (buffer.Length < System.Runtime.CompilerServices.Unsafe.SizeOf<T>())
            {
                throw new SerializationException($"{System.Runtime.CompilerServices.Unsafe.SizeOf<T>()}{buffer.Length}");
            }
            value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(buffer));
            return System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
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
