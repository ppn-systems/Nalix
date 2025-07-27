namespace Nalix.Common.Security.Types;

/// <summary>
/// Defines the types of security guards that can be applied to protect resources or operations.
/// </summary>
public enum GuardType : System.Byte
{
    /// <summary>
    /// No security guard is applied.
    /// </summary>
    None = 0x00,

    /// <summary>
    /// Requires the user to be authenticated.
    /// </summary>
    Authenticated = 0xA7,

    /// <summary>
    /// Requires a valid session to be present.
    /// </summary>
    SessionValid = 0x1C,

    /// <summary>
    /// Enforces rate limiting to prevent abuse.
    /// </summary>
    RateLimited = 0xF3,

    /// <summary>
    /// Requires the request to originate from a trusted IP address.
    /// </summary>
    IpTrusted = 0x8B,

    /// <summary>
    /// Prevents replay attacks by ensuring requests are unique.
    /// </summary>
    NoReplay = 0xD4,

    /// <summary>
    /// Requires specific permissions to access the resource or perform the operation.
    /// </summary>
    PermissionRequired = 0x2E,

    /// <summary>
    /// Verifies the integrity of the payload through a valid signature.
    /// </summary>
    PayloadSignatureVerified = 0x9A,

    /// <summary>
    /// Requires a completed handshake process before access is granted.
    /// </summary>
    HandshakeComplete = 0x65,

    /// <summary>
    /// Restricts access to only encrypted communication channels.
    /// </summary>
    EncryptedChannelOnly = 0xC1
}