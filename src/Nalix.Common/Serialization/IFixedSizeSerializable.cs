namespace Nalix.Common.Serialization;

/// <summary>
/// Defines a contract for types that can be serialized with a fixed size.
/// Implementing types must provide a static property indicating the fixed size in bytes.
/// </summary>
public interface IFixedSizeSerializable
{
    /// <summary>
    /// Gets the fixed size in bytes required to serialize an instance of the implementing type.
    /// This property must be implemented as a static abstract member.
    /// </summary>
    static abstract System.Int32 Size { get; }
}
