using Nalix.Common.Serialization;
using System;

namespace Nalix.Serialization;

/// <inheritdoc/>
public abstract class BinSerializer<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields)] T> : IAutoSerializer<T> where T : new()
{
    /// <inheritdoc/>
    public virtual int GetSize(in T obj)
            => BinarySerializer<T>.GetSize(in obj);

    /// <inheritdoc/>
    public virtual void Serialize(in T obj, Span<byte> span)
        => BinarySerializer<T>.Serialize(in obj, span);

    /// <inheritdoc/>
    public virtual void Serialize(in T obj, Memory<byte> memory)
        => BinarySerializer<T>.Serialize(in obj, memory);

    /// <inheritdoc/>
    public virtual byte[] SerializeToArray(in T obj)
        => BinarySerializer<T>.SerializeToArray(in obj);

    /// <inheritdoc/>
    public virtual T Deserialize(ReadOnlySpan<byte> span)
        => BinarySerializer<T>.Deserialize(span);

    /// <inheritdoc/>
    public virtual T Deserialize(ReadOnlyMemory<byte> memory)
        => BinarySerializer<T>.Deserialize(memory);

    /// <inheritdoc/>
    public virtual T DeserializeFromArray(byte[] data)
        => BinarySerializer<T>.DeserializeFromArray(data);
}
