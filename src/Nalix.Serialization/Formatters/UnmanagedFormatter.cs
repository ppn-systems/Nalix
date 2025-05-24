using Nalix.Serialization.Internal.Types;

namespace Nalix.Serialization.Formatters;

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
    /// <param name="writer">The <see cref="SerializationWriter"/> to write to.</param>
    /// <param name="value">The unmanaged value to write.</param>
    public void Serialize(ref SerializationWriter writer, T value)
    {
        int size = TypeMetadata.GetSizeOf<T>();
        System.Span<byte> span = writer.GetSpan(size);
        System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref span[0], value);
        writer.Advance(size);
    }

    /// <summary>
    /// Reads an unmanaged value from the buffer without alignment requirements.
    /// </summary>
    /// <param name="writer">The <see cref="SerializationReader"/> to read from.</param>
    /// <returns>The unmanaged value read from the buffer.</returns>
    public T Deserialize(ref SerializationReader writer)
    {
        int size = TypeMetadata.GetSizeOf<T>();
        System.Span<byte> span = writer.GetSpan(size);
        T value = System.Runtime.CompilerServices.Unsafe.ReadUnaligned<T>(ref span[0]);
        writer.Advance(size);

        return value;
    }
}
