namespace Nalix.Common.Connection.Protocols;

/// <summary>
/// Contains standard protocol error messages used across the application.
/// </summary>
public static class ProtocolMessage
{
    /// <summary>No error or status code provided.</summary>
    public const string None = "No error or status code provided.";

    /// <summary>Operation completed successfully.</summary>
    public const string Success = "Operation completed successfully.";

    /// <summary>An unknown error occurred.</summary>
    public const string UnknownError = "An unknown error occurred.";

    /// <summary>You are not authorized to perform this action.</summary>
    public const string Unauthorized = "You are not authorized to perform this action.";

    /// <summary>Access to this resource is forbidden.</summary>
    public const string Forbidden = "Access to this resource is forbidden.";

    /// <summary>You do not have sufficient permission.</summary>
    public const string PermissionDenied = "You do not have sufficient permission.";

    /// <summary>You must authenticate before proceeding.</summary>
    public const string AuthenticationFailure = "You must authenticate before proceeding.";

    /// <summary>The specified command was not found.</summary>
    public const string CommandNotFound = "The specified command was not found.";

    /// <summary>The command is invalid or malformed.</summary>
    public const string InvalidCommand = "The command is invalid or malformed.";

    /// <summary>The request is invalid.</summary>
    public const string BadRequest = "The request is invalid.";

    /// <summary>The request conflicts with existing data.</summary>
    public const string Conflict = "The request conflicts with existing data.";

    /// <summary>The provided data is invalid.</summary>
    public const string InvalidData = "The provided data is invalid.";

    /// <summary>Missing required information.</summary>
    public const string MissingInformation = "Missing required information.";

    /// <summary>The payload format is invalid.</summary>
    public const string InvalidPayload = "The payload format is invalid.";

    /// <summary>The payload size exceeds the allowed limit.</summary>
    public const string PayloadTooLarge = "The payload size exceeds the allowed limit.";

    /// <summary>Packet checksum does not match.</summary>
    public const string ChecksumMismatch = "Packet checksum does not match.";

    /// <summary>The request timed out.</summary>
    public const string RequestTimeout = "The request timed out.";

    /// <summary>The packet has expired.</summary>
    public const string PacketExpired = "The packet has expired.";

    /// <summary>Encrypted packet is required, but the packet is not encrypted.</summary>
    public const string PacketEncryption = "Encrypted packet is required, but the packet is not encrypted.";

    /// <summary>The packet type is invalid.</summary>
    public const string PacketType = "The packet type is invalid.";

    /// <summary>You are being rate-limited. Please try again later.</summary>
    public const string RateLimited = "You are being rate-limited. Please try again later.";

    /// <summary>An internal server error occurred.</summary>
    public const string ServerError = "An internal server error occurred.";

    /// <summary>The service is currently unavailable.</summary>
    public const string ServiceUnavailable = "The service is currently unavailable.";

    /// <summary>Conflict detected due to concurrent modification.</summary>
    public const string ConcurrencyError = "Conflict detected due to concurrent modification.";

    /// <summary>Unable to connect to the server. Please try again later.</summary>
    public const string ConnectionFailure = "Unable to connect to the server. Please try again later.";

    /// <summary>The connection was refused by the server.</summary>
    public const string ConnectionRefused = "The connection was refused by the server.";

    /// <summary>Session expired or invalid. Please log in again.</summary>
    public const string SessionError = "Session expired or invalid. Please log in again.";

    /// <summary>Failed to decrypt the packet.</summary>
    public const string DecryptionFailure = "Failed to decrypt the packet.";

    /// <summary>Failed to compress/decompress the packet.</summary>
    public const string CompressionFailure = "Failed to compress/decompress the packet.";

    /// <summary>Data tampering detected.</summary>
    public const string DataTampering = "Data tampering detected.";

    /// <summary>The requested resource was not found.</summary>
    public const string NotFound = "The requested resource was not found.";
}
