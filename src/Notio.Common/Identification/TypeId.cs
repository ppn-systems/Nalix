namespace Notio.Common.Identification;

/// <summary>
/// ID type to serve different purposes in the system.
/// </summary>
public enum TypeId
{
    /// <summary>
    /// No specific purpose.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// For system configurations or versions.
    /// </summary>
    System = 1,

    /// <summary>
    /// For user account management.
    /// </summary>
    Account = 2,

    /// <summary>
    /// For session management.
    /// </summary>
    Session = 3,

    /// <summary>
    /// For chat and message management.
    /// </summary>
    Chat = 4,

    /// <summary>
    /// For network communication packets.
    /// </summary>
    Packet = 5,

    /// <summary>
    /// Limit on the number of ID types, do not exceed this value.
    /// </summary>
    Limit = 1 << 5
}
