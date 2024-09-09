using Nalix.Shared.Serialization.Buffers;
using Nalix.Shared.Serialization.Internal.Types;
using System;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Provides formatting for unmanaged types.
/// </summary>
/// <remarks>
/// Unmanaged types include:
/// <list type="bullet">
/// <item>
/// <description>
/// sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, decimal, or bool
/// </description>
/// </item>
/// <item><description>Any enum type</description></item>
/// <item><description>Any pointer type</description></item>
/// <item><description>Any user-defined struct type that contains fields of unmanaged types only</description></item>
/// </list>
/// Reference: <see href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types"/>.
/// </remarks>
public sealed partial class UnmanagedFormatter<T> : IFormatter<T> where T : unmanaged
{
    /// <summary>
    /// Writes an unmanaged value to the buffer without alignment requirements.
    /// </summary>
    /// <param name="writer">The <see cref="DataWriter"/> to write to.</param>
    /// <param name="value">The unmanaged value to write.</param>
    public unsafe void Serialize(ref DataWriter writer, T value)
    {
        System.Int32 size = TypeMetadata.SizeOf<T>();

        // Pin the span to get a pointer and write unaligned
        ref System.Byte dest = ref writer.GetFreeBufferReference();
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref dest, value);

        writer.Advance(size);
    }

    /// <summary>
    /// Reads an unmanaged value from the buffer without alignment requirements.
    /// </summary>
    /// <param name="reader">The <see cref="DataReader"/> to read from.</param>
    /// <returns>The unmanaged value read from the buffer.</returns>
    public unsafe T Deserialize(ref DataReader reader)
    {
        T value;
        System.Int32 size = TypeMetadata.SizeOf<T>();
        System.ReadOnlySpan<System.Byte> span = reader.GetSpan(size);

#if DEBUG
        if (reader.BytesRemaining < size)
            throw new InvalidOperationException($"Buffer underrun while deserializing {typeof(T)}. Needed {size} bytes.");
#endif

        fixed (System.Byte* ptr = span)
        {
            value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(ptr);
        }

        reader.Advance(size);
        return value;
    }
}
