using System;

namespace Nalix.Common.Serialization;

/// <summary>
/// Defines an interface for objects that can be serialized and deserialized using <see cref="Span{T}"/> of bytes.
/// This interface provides methods for serializing data into a byte span, deserializing data from a byte span,
/// and determining the size required to store the object's data.
/// </summary>
public interface ISerializable
{
    /// <summary>
    /// Gets the number of bytes required to store the serialized form of this object.
    /// </summary>
    /// <returns>The size, in bytes, needed to serialize the object's data.</returns>
    int GetSize();

    /// <summary>
    /// Serializes the object's data into the provided <see cref="Span{T}"/> of bytes.
    /// </summary>
    /// <param name="destination">The <see cref="Span{T}"/> of bytes where the serialized data will be written.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written to the <paramref name="destination"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="destination"/> span is too small to hold the serialized data.</exception>
    void Serialize(Span<byte> destination, out int bytesWritten);

    /// <summary>
    /// Deserializes the object's data from the provided <see cref="ReadOnlySpan{T}"/> of bytes.
    /// </summary>
    /// <param name="source">The <see cref="ReadOnlySpan{T}"/> of bytes containing the data to deserialize.</param>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="source"/> span contains invalid or insufficient data.</exception>
    void Deserialize(ReadOnlySpan<byte> source);
}
