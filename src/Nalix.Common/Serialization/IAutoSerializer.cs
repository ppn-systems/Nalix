namespace Nalix.Common.Serialization;

/// <summary>
/// Defines a contract for auto-serializing and deserializing objects of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the object to serialize/deserialize.</typeparam>
public interface IAutoSerializer<T> where T : new()
{
    /// <summary>
    /// Calculates the total number of bytes required to serialize an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="obj">The object to measure.</param>
    /// <returns>Total byte size required for serialization.</returns>
    int GetSize(in T obj);

    /// <summary>
    /// Serializes the object into the provided <see cref="System.Span{Byte}"/>.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="span">The destination span to write bytes into.</param>
    void Serialize(in T obj, System.Span<byte> span);

    /// <summary>
    /// Deserializes an object of type <typeparamref name="T"/> from the provided <see cref="System.ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <param name="span">The source span to read bytes from.</param>
    /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
    T Deserialize(System.ReadOnlySpan<byte> span);

    /// <summary>
    /// Serializes the object into the provided <see cref="System.Memory{Byte}"/>.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="memory">The destination memory buffer to write bytes into.</param>
    void Serialize(in T obj, System.Memory<byte> memory);

    /// <summary>
    /// Deserializes an object of type <typeparamref name="T"/> from the provided <see cref="System.ReadOnlyMemory{Byte}"/>.
    /// </summary>
    /// <param name="memory">The source memory buffer to read bytes from.</param>
    /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
    T Deserialize(System.ReadOnlyMemory<byte> memory);

    /// <summary>
    /// Serializes the object to a new byte array.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A byte array containing the serialized data.</returns>
    byte[] SerializeToArray(in T obj);

    /// <summary>
    /// Deserializes an object from a byte array.
    /// </summary>
    /// <param name="data">The byte array containing serialized data.</param>
    /// <returns>A deserialized object of type <typeparamref name="T"/>.</returns>
    T DeserializeFromArray(byte[] data);
}
