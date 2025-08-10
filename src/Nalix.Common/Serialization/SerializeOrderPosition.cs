namespace Nalix.Common.Serialization;

/// <summary>
/// Represents the positions of fields in the serialization order.
/// Each value corresponds to a specific position in the serialized data.
/// </summary>
public enum SerializeOrderPosition : System.Byte
{
    /// <summary>
    /// Represents the magic number field, which uniquely identifies the packet format or protocol.
    /// This field comes first in the serialized data.
    /// </summary>
    MagicNumber = 0,

    /// <summary>
    /// Represents the operation code (OpCode) field, specifying the command or type of the packet.
    /// This field comes second in the serialized data.
    /// </summary>
    OpCode = 4,

    /// <summary>
    /// Represents the flags field, which contains state or processing options for the packet.
    /// This field comes third in the serialized data.
    /// </summary>
    Flags = 6,

    /// <summary>
    /// Represents the priority field, indicating the processing priority of the packet.
    /// This field comes fourth in the serialized data.
    /// </summary>
    Priority = 7,

    /// <summary>
    /// Represents the transport protocol field, indicating the transport protocol (e.g., TCP or UDP) used.
    /// This field comes fifth in the serialized data.
    /// </summary>
    Transport = 8
}
