using Nalix.Common.Networking.Protocols;

namespace Nalix.SDK.Extensions;

/// <summary>
/// Provides extension methods for converting protocol-related enums to user-friendly string descriptions for UI display.
/// </summary>
public static class ProtocolStringExtensions
{
    /// <summary>
    /// Converts the <see cref="ProtocolAdvice"/> value to a short, user-friendly English description suitable for UI display.
    /// </summary>
    /// <param name="advice">The <see cref="ProtocolAdvice"/> value.</param>
    /// <returns>A concise, user-friendly English string describing the advice.</returns>
    public static string ToDisplayString(this ProtocolAdvice advice)
    {
        return advice switch
        {
            ProtocolAdvice.NONE => "No action required.",
            ProtocolAdvice.RETRY => "Please try again.",
            ProtocolAdvice.BACKOFF_RETRY => "Please wait and try again.",
            ProtocolAdvice.DO_NOT_RETRY => "Cannot retry automatically.",
            ProtocolAdvice.REAUTHENTICATE => "Sign in again required.",
            ProtocolAdvice.SLOW_DOWN => "Please slow down.",
            ProtocolAdvice.RECONNECT => "Please reconnect.",
            ProtocolAdvice.FIX_AND_RETRY => "Fix the issue and try again.",
            _ => "Unknown action.",
        };
    }

    /// <summary>
    /// Converts a <see cref="ProtocolReason"/> value to a short, user-friendly English description.
    /// </summary>
    /// <param name="reason">The <see cref="ProtocolReason"/> value.</param>
    /// <returns>A concise, user-friendly English string describing the reason.</returns>
    public static string ToDisplayString(this ProtocolReason reason)
    {
        return reason switch
        {
            // 0–49: General
            ProtocolReason.NONE => "No reason specified.",
            ProtocolReason.UNKNOWN => "Unknown error.",
            ProtocolReason.CANCELLED => "Operation cancelled.",
            ProtocolReason.NOT_IMPLEMENTED => "Not implemented.",
            ProtocolReason.TEMPORARY_FAILURE => "Temporary failure. Try again.",
            ProtocolReason.DEPRECATED => "Feature deprecated.",
            ProtocolReason.REQUEST_INVALID => "Invalid request.",
            ProtocolReason.INTERNAL_ERROR => "Internal server error.",

            // 100–149: Transport / Network
            ProtocolReason.TIMEOUT => "Request timed out.",
            ProtocolReason.REMOTE_CLOSED => "Connection closed by remote.",
            ProtocolReason.LOCAL_CLOSED => "Connection closed locally.",
            ProtocolReason.NETWORK_ERROR => "Network error.",
            ProtocolReason.CONNECTION_REFUSED => "Connection refused.",
            ProtocolReason.CONNECTION_RESET => "Connection was reset.",
            ProtocolReason.DNS_FAILURE => "DNS lookup failed.",
            ProtocolReason.MTU_VIOLATION => "Packet too large (MTU exceeded).",
            ProtocolReason.CONGESTION => "Network congestion.",
            ProtocolReason.KEEPALIVE_FAILED => "Keepalive failed.",

            // 150–199: Protocol / Framing
            ProtocolReason.PROTOCOL_ERROR => "Protocol error.",
            ProtocolReason.VERSION_UNSUPPORTED => "Unsupported protocol version.",
            ProtocolReason.FRAME_TOO_LARGE => "Frame too large.",
            ProtocolReason.MESSAGE_TOO_LARGE => "Message too large.",
            ProtocolReason.UNEXPECTED_MESSAGE => "Unexpected message.",
            ProtocolReason.MISSING_REQUIRED_FIELD => "Missing required field.",
            ProtocolReason.DUPLICATE_MESSAGE => "Duplicate message.",
            ProtocolReason.STATE_VIOLATION => "Invalid state.",
            ProtocolReason.CRYPTO_UNSUPPORTED => "Unsupported cryptography.",
            ProtocolReason.COMPRESSION_UNSUPPORTED => "Unsupported compression.",
            ProtocolReason.OPERATION_UNSUPPORTED => "Operation not supported.",
            ProtocolReason.MALFORMED_PACKET => "Malformed packet.",

            // 200–259: Security / AuthN / AuthZ
            ProtocolReason.UNAUTHENTICATED => "Authentication required.",
            ProtocolReason.UNAUTHORIZED => "Not authorized.",
            ProtocolReason.FORBIDDEN => "Access forbidden.",
            ProtocolReason.ACCOUNT_LOCKED => "Account locked.",
            ProtocolReason.ACCOUNT_SUSPENDED => "Account suspended.",
            ProtocolReason.BANNED => "User is banned.",
            ProtocolReason.IP_BLOCKED => "IP is blocked.",
            ProtocolReason.RATE_LIMITED => "Too many requests.",
            ProtocolReason.TOKEN_EXPIRED => "Session expired.",
            ProtocolReason.TOKEN_REVOKED => "Session invalid.",
            ProtocolReason.DEVICE_UNTRUSTED => "Untrusted device.",

            // 260–299: Crypto / Integrity
            ProtocolReason.TLS_HANDSHAKE_FAILED => "Secure connection failed.",
            ProtocolReason.TLS_REQUIRED => "Secure connection required.",
            ProtocolReason.TLS_CERT_INVALID => "Invalid certificate.",
            ProtocolReason.SIGNATURE_INVALID => "Invalid signature.",
            ProtocolReason.CHECKSUM_FAILED => "Invalid checksum.",
            ProtocolReason.DECRYPTION_FAILED => "Decryption failed.",
            ProtocolReason.REPLAY_DETECTED => "Replay detected.",
            ProtocolReason.NONCE_INVALID => "Nonce invalid or reused.",

            // 300–349: Service / Infrastructure
            ProtocolReason.SERVER_SHUTDOWN => "Server is shutting down.",
            ProtocolReason.SERVICE_UNAVAILABLE => "Service unavailable.",
            ProtocolReason.MAINTENANCE => "Under maintenance.",
            ProtocolReason.OVERLOADED => "Server overloaded.",
            ProtocolReason.DEPENDENCY_FAILURE => "Dependency failed.",
            ProtocolReason.DATABASE_UNAVAILABLE => "Database unavailable.",
            ProtocolReason.CACHE_UNAVAILABLE => "Cache unavailable.",
            ProtocolReason.QUEUE_UNAVAILABLE => "Queue unavailable.",

            // 350–399: Application / Semantics
            ProtocolReason.VALIDATION_FAILED => "Validation failed.",
            ProtocolReason.NOT_FOUND => "Not found.",
            ProtocolReason.ALREADY_EXISTS => "Already exists.",
            ProtocolReason.PRECONDITION_FAILED => "Precondition failed.",
            ProtocolReason.STATE_CONFLICT => "State conflict.",
            ProtocolReason.UNSUPPORTED_MEDIA_TYPE => "Unsupported content type.",
            ProtocolReason.SERIALIZATION_FAILED => "Serialization failed.",
            ProtocolReason.UNSUPPORTED_PACKET => "Unsupported packet.",
            ProtocolReason.TRANSFORM_FAILED => "Processing failed.",

            // 400–449: Flow Control / QoS
            ProtocolReason.THROTTLED => "Request throttled.",
            ProtocolReason.SLOW_CONSUMER => "Receiving too slowly.",
            ProtocolReason.CREDIT_EXHAUSTED => "Quota exhausted.",
            ProtocolReason.WINDOW_EXCEEDED => "Flow control limit exceeded.",

            // 450–499: Resource / Quota
            ProtocolReason.RESOURCE_LIMIT => "Resource limit reached.",
            ProtocolReason.MEMORY_EXHAUSTED => "Out of memory.",
            ProtocolReason.CONNECTION_LIMIT => "Too many connections.",
            ProtocolReason.FD_LIMIT => "Too many open files.",
            ProtocolReason.DISK_FULL => "Disk full.",
            ProtocolReason.CPU_LIMIT => "CPU limit exceeded.",

            // 500–549: Client / Local Decisions
            ProtocolReason.CLIENT_QUIT => "User requested to quit.",
            ProtocolReason.ABORTED => "Operation aborted.",
            ProtocolReason.IDLE_TIMEOUT => "Session timed out.",
            ProtocolReason.LOCAL_POLICY => "Blocked by local policy.",
            ProtocolReason.COMPRESSION_FAILED => "Compression failed.",

            // 550–599: Session / Time / Clock
            ProtocolReason.SESSION_NOT_FOUND => "Session not found.",
            ProtocolReason.SESSION_EXPIRED => "Session expired.",
            ProtocolReason.DUPLICATE_SESSION => "Duplicate session.",
            ProtocolReason.KEY_ROTATION_REQUIRED => "Key rotation required.",
            ProtocolReason.TIME_SKEW => "Clock error.",

            // 600–649: Consistency / Coordination
            ProtocolReason.LEADER_CHANGE => "Leadership changed. Retry.",
            ProtocolReason.NOT_LEADER => "Not the leader.",
            ProtocolReason.CONSENSUS_UNAVAILABLE => "Consensus unavailable.",
            ProtocolReason.STALE_READ => "Stale read.",

            // 650–699: Routing / Placement
            ProtocolReason.REDIRECT => "Resource moved.",
            ProtocolReason.MIGRATE => "Shard migrated.",
            ProtocolReason.REGION_UNAVAILABLE => "Region unavailable.",

            // 700–749: Compliance / Policy
            ProtocolReason.LEGAL_BLOCK => "Blocked for legal reasons.",
            ProtocolReason.CONTENT_VIOLATION => "Content policy violation.",
            ProtocolReason.AGE_RESTRICTED => "Restricted by age.",
            ProtocolReason.INVALID_USERNAME => "Invalid username.",
            ProtocolReason.WEAK_PASSWORD => "Weak password.",

            // 900–999: Reserved / Vendor Specific
            ProtocolReason.RESERVED_900 => "Reserved code.",
            ProtocolReason.RESERVED_901 => "Reserved code.",

            // Default
            _ => "Unspecified error.",
        };
    }
}
