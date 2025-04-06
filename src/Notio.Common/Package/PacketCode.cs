using Notio.Common.Attributes;

namespace Notio.Common.Package;

/// <summary>
/// Represents standard error codes used in packet communication between clients and servers.
/// </summary>
public enum PacketCode : ushort
{
    // ===== Success =====
    /// <summary>
    /// Indicates that the operation was successful.
    /// </summary>
    [PacketCodeMessage("Operation completed successfully.")]
    Success = 0,


    // ===== General Errors =====
    /// <summary>
    /// Indicates an unknown error occurred.
    /// </summary>
    [PacketCodeMessage("An unknown error occurred.")]
    UnknownError = 1,


    // ===== Authorization Errors =====
    /// <summary>Client is not authenticated.</summary>
    [PacketCodeMessage("You are not authorized to perform this action.")]
    Unauthorized = 1001,

    /// <summary>Client is authenticated but lacks permission.</summary>
    [PacketCodeMessage("Access to this resource is forbidden.")]
    Forbidden = 1002,

    /// <summary>Client’s authority level is insufficient.</summary>
    [PacketCodeMessage("You do not have sufficient permission.")]
    PermissionDenied = 1003,


    // ===== Command Errors =====
    /// <summary>The command does not exist.</summary>
    [PacketCodeMessage("The specified command was not found.")]
    CommandNotFound = 2001,

    /// <summary>The command format is incorrect.</summary>
    [PacketCodeMessage("The command is invalid or malformed.")]
    InvalidCommand = 2002,

    /// <summary>The command was rejected due to bad data.</summary>
    [PacketCodeMessage("The request is invalid.")]
    BadRequest = 2003,

    /// <summary>There is a conflict with the current state (e.g. duplicate).</summary>
    [PacketCodeMessage("The request conflicts with existing data.")]
    Conflict = 2004,


    // ===== Payload / Format Errors =====
    /// <summary>Payload structure is invalid or unreadable.</summary>
    [PacketCodeMessage("The payload format is invalid.")]
    InvalidPayload = 3001,

    /// <summary>Payload size exceeds allowed limit.</summary>
    [PacketCodeMessage("The payload size exceeds the allowed limit.")]
    PayloadTooLarge = 3002,

    /// <summary>Checksum failed for the received packet.</summary>
    [PacketCodeMessage("Packet checksum does not match.")]
    ChecksumMismatch = 3003,


    // ===== Timeout / Expired =====
    /// <summary>The client or server took too long to respond.</summary>
    [PacketCodeMessage("The request timed out.")]
    RequestTimeout = 4001,

    /// <summary>The packet is no longer valid due to expiration.</summary>
    [PacketCodeMessage("The packet has expired.")]
    PacketExpired = 4002,

    /// <summary>Packet encryption required but not provided.</summary>
    [PacketCodeMessage("Encrypted packet is required, but the packet is not encrypted.")]
    PacketEncryption = 4003,


    // ===== Rate limit =====
    /// <summary>The client has sent too many requests in a short time.</summary>
    [PacketCodeMessage("You are being rate-limited. Please try again later.")]
    RateLimited = 4501,


    // ===== Server Errors =====
    /// <summary>An unexpected server error occurred.</summary>
    [PacketCodeMessage("An internal server error occurred.")]
    ServerError = 5001,

    /// <summary>The server is temporarily unable to handle the request.</summary>
    [PacketCodeMessage("The service is currently unavailable.")]
    ServiceUnavailable = 5002,
}
