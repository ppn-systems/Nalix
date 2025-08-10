namespace Nalix.Common.Serialization;

/// <summary>
/// Defines a method that returns the number of bytes
/// required to serialize an object.
/// </summary>
public interface ISerializableSize
{
    /// <summary>
    /// Gets the size in bytes required to serialize this instance.
    /// </summary>
    /// <returns>
    /// The size in bytes when serialized.
    /// </returns>
    System.UInt16 GetSize();
}
