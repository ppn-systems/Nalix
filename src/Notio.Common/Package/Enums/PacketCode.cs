using Notio.Common.Package.Attributes;

namespace Notio.Common.Package.Enums;

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

    /// <summary>Client has not authenticated.</summary>
    [PacketCodeMessage("You must authenticate before proceeding.")]
    AuthenticationFailure = 1004,


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

    /// <summary>The provided data is invalid or malformed.</summary>
    [PacketCodeMessage("The provided data is invalid.")]
    InvalidData = 2005,

    /// <summary>Required information is missing from the request.</summary>
    [PacketCodeMessage("Missing required information.")]
    MissingInformation = 2006,


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

    /// <summary>The packet type is invalid.</summary>
    [PacketCodeMessage("The packet type is invalid.")]
    PacketType = 4004,


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

    /// <summary>Concurrent modification detected.</summary>
    [PacketCodeMessage("Conflict detected due to concurrent modification.")]
    ConcurrencyError = 5003,


    // ===== Connection Errors =====
    /// <summary>The connection to the server was lost.</summary>
    [PacketCodeMessage("Unable to connect to the server. Please try again later.")]
    ConnectionFailure = 6001,

    /// <summary>Connection was refused by the server.</summary>
    [PacketCodeMessage("The connection was refused by the server.")]
    ConnectionRefused = 6002,


    // ===== Session Errors =====
    /// <summary>The session is invalid or expired.</summary>
    [PacketCodeMessage("Session expired or invalid. Please log in again.")]
    SessionError = 7001,


    // ===== Encryption and Compression Errors =====
    /// <summary>Encryption is required but not provided.</summary>
    [PacketCodeMessage("Failed to decrypt the packet.")]
    DecryptionFailure = 8001,

    /// <summary>Compression is required but not provided.</summary>
    [PacketCodeMessage("Failed to compress/decompress the packet.")]
    CompressionFailure = 8002,


    // ===== Resource Errors =====
    /// <summary>Packet integrity check failed.</summary>
    [PacketCodeMessage("Data tampering detected.")]
    DataTampering = 9001,

    /// <summary>The requested resource was not found.</summary>
    [PacketCodeMessage("The requested resource was not found.")]
    NotFound = 9002,
}
