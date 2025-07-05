namespace Nalix.Common.Connection.Protocols;

/// <summary>
/// Contains standard protocol error messages used across the application.
/// </summary>
public static class ProtocolErrorTexts
{
    /// <summary>No error or status code provided.</summary>
    public const System.String None = "No error or status code provided.";

    /// <summary>Operation completed successfully.</summary>
    public const System.String Success = "Operation completed successfully.";

    /// <summary>An unknown error occurred.</summary>
    public const System.String UnknownError = "An unknown error occurred.";

    /// <summary>You are not authorized to perform this action.</summary>
    public const System.String Unauthorized = "You are not authorized to perform this action.";

    /// <summary>Access to this resource is forbidden.</summary>
    public const System.String Forbidden = "Access to this resource is forbidden.";

    /// <summary>You do not have sufficient permission.</summary>
    public const System.String PermissionDenied = "You do not have sufficient permission.";

    /// <summary>You must authenticate before proceeding.</summary>
    public const System.String AuthenticationFailure = "You must authenticate before proceeding.";

    /// <summary>The specified command was not found.</summary>
    public const System.String CommandNotFound = "The specified command was not found.";

    /// <summary>The command is invalid or malformed.</summary>
    public const System.String InvalidCommand = "The command is invalid or malformed.";

    /// <summary>The request is invalid.</summary>
    public const System.String BadRequest = "The request is invalid.";

    /// <summary>The request conflicts with existing data.</summary>
    public const System.String Conflict = "The request conflicts with existing data.";

    /// <summary>The provided data is invalid.</summary>
    public const System.String InvalidData = "The provided data is invalid.";

    /// <summary>Missing required information.</summary>
    public const System.String MissingInformation = "Missing required information.";

    /// <summary>The payload format is invalid.</summary>
    public const System.String InvalidPayload = "The payload format is invalid.";

    /// <summary>The payload size exceeds the allowed limit.</summary>
    public const System.String PayloadTooLarge = "The payload size exceeds the allowed limit.";

    /// <summary>Packet checksum does not match.</summary>
    public const System.String ChecksumMismatch = "Packet checksum does not match.";

    /// <summary>The request timed out.</summary>
    public const System.String RequestTimeout = "The request timed out.";

    /// <summary>The packet has expired.</summary>
    public const System.String PacketExpired = "The packet has expired.";

    /// <summary>Encrypted packet is required, but the packet is not encrypted.</summary>
    public const System.String PacketEncryption = "Encrypted packet is required, but the packet is not encrypted.";

    /// <summary>The packet type is invalid.</summary>
    public const System.String PacketType = "The packet type is invalid.";

    /// <summary>You are being rate-limited. Please try again later.</summary>
    public const System.String RateLimited = "You are being rate-limited. Please try again later.";

    /// <summary>An internal server error occurred.</summary>
    public const System.String ServerError = "An internal server error occurred.";

    /// <summary>The service is currently unavailable.</summary>
    public const System.String ServiceUnavailable = "The service is currently unavailable.";

    /// <summary>Conflict detected due to concurrent modification.</summary>
    public const System.String ConcurrencyError = "Conflict detected due to concurrent modification.";

    /// <summary>Unable to connect to the server. Please try again later.</summary>
    public const System.String ConnectionFailure = "Unable to connect to the server. Please try again later.";

    /// <summary>The connection was refused by the server.</summary>
    public const System.String ConnectionRefused = "The connection was refused by the server.";

    /// <summary>Session expired or invalid. Please log in again.</summary>
    public const System.String SessionError = "Session expired or invalid. Please log in again.";

    /// <summary>Failed to decrypt the packet.</summary>
    public const System.String DecryptionFailure = "Failed to decrypt the packet.";

    /// <summary>Failed to compress/decompress the packet.</summary>
    public const System.String CompressionFailure = "Failed to compress/decompress the packet.";

    /// <summary>Data tampering detected.</summary>
    public const System.String DataTampering = "Data tampering detected.";

    /// <summary>The requested resource was not found.</summary>
    public const System.String NotFound = "The requested resource was not found.";
}