namespace Nalix.Common.Package.Enums;

/// <summary>
/// Defines the categories of packets used in communication.
/// </summary>
public enum PacketCategory : byte
{
    /// <summary>
    /// No specific category assigned to the packet.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that the packet is a request.
    /// </summary>
    Request = 1,

    /// <summary>
    /// Indicates that the packet is a response.
    /// </summary>
    Response = 2,

    /// <summary>
    /// Indicates that the packet is an acknowledgment.
    /// </summary>
    Acknowledgment = 3,

    /// <summary>
    /// Indicates that the packet contains an error or exception.
    /// </summary>
    Error = 4
}
