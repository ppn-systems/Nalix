// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Connection.Protocols;

/// <summary>
/// Standard reason codes for control packets (DISCONNECT, ERROR, NACK).
/// Grouped by ranges to ease logging, analytics, and client decisioning.
/// Inspired by WebSocket close codes, MQTT v5 reason codes, gRPC, HTTP, and QUIC.
/// </summary>
public enum ReasonCode : System.UInt16
{
    #region 0–49: General

    /// <summary>
    /// No reason specified (default).
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Unknown or unspecified error.
    /// </summary>
    UNKNOWN = 1,

    /// <summary>
    /// Operation cancelled by caller or system.
    /// </summary>
    CANCELLED = 2,

    /// <summary>
    /// Feature not implemented on this endpoint/version.
    /// </summary>
    NOT_IMPLEMENTED = 3,

    /// <summary>
    /// Temporary condition; try again later.
    /// </summary>
    TEMPORARY_FAILURE = 4,

    /// <summary>
    /// Deprecated or removed feature.
    /// </summary>
    DEPRECATED = 5,

    #endregion

    #region 100–149: Transport / Network

    /// <summary>
    /// No response within expected timeframe.
    /// </summary>
    TIMEOUT = 100,

    /// <summary>
    /// Peer closed the connection.
    /// </summary>
    REMOTE_CLOSED = 101,

    /// <summary>
    /// Local endpoint closed the connection.
    /// </summary>
    LOCAL_CLOSED = 102,

    /// <summary>
    /// Generic network/transport error.
    /// </summary>
    NETWORK_ERROR = 103,

    /// <summary>
    /// Connection refused by remote host/port.
    /// </summary>
    CONNECTION_REFUSED = 104,

    /// <summary>
    /// Connection reset by peer.
    /// </summary>
    CONNECTION_RESET = 105,

    /// <summary>
    /// DNS resolution failed.
    /// </summary>
    DNS_FAILURE = 106,

    /// <summary>
    /// MTU/fragmentation constraints violated.
    /// </summary>
    MTU_VIOLATION = 107,

    /// <summary>
    /// Transport congestion detected.
    /// </summary>
    CONGESTION = 108,

    /// <summary>
    /// Keepalive/ping failed.
    /// </summary>
    KEEPALIVE_FAILED = 109,

    #endregion

    #region 150–199: Protocol / Framing

    /// <summary>
    /// Protocol violation or unsupported version.
    /// </summary>
    PROTOCOL_ERROR = 150,

    /// <summary>
    /// Protocol version is not supported.
    /// </summary>
    VERSION_UNSUPPORTED = 151,

    /// <summary>
    /// Frame/message size exceeds allowed limit.
    /// </summary>
    FRAME_TOO_LARGE = 152,

    /// <summary>
    /// Message payload exceeds server policy.
    /// </summary>
    MESSAGE_TOO_LARGE = 153,

    /// <summary>
    /// Unexpected message type or order.
    /// </summary>
    UNEXPECTED_MESSAGE = 154,

    /// <summary>
    /// Missing required field or header.
    /// </summary>
    MISSING_REQUIRED_FIELD = 155,

    /// <summary>
    /// Duplicate message or idempotency conflict.
    /// </summary>
    DUPLICATE_MESSAGE = 156,

    /// <summary>
    /// State machine violation.
    /// </summary>
    STATE_VIOLATION = 157,

    #endregion

    #region 200–259: Security / AuthN / AuthZ

    /// <summary>
    /// Authentication required or failed.
    /// </summary>
    UNAUTHENTICATED = 200,

    /// <summary>
    /// Authenticated but lacking permission.
    /// </summary>
    UNAUTHORIZED = 201,

    /// <summary>
    /// Access explicitly forbidden by policy.
    /// </summary>
    FORBIDDEN = 202,

    /// <summary>
    /// Account is locked due to policy.
    /// </summary>
    ACCOUNT_LOCKED = 203,

    /// <summary>
    /// Account is suspended.
    /// </summary>
    ACCOUNT_SUSPENDED = 204,

    /// <summary>
    /// Client/user is banned.
    /// </summary>
    BANNED = 205,

    /// <summary>
    /// Source IP or ASN is blocked.
    /// </summary>
    IP_BLOCKED = 206,

    /// <summary>
    /// Too many requests (rate limited).
    /// </summary>
    RATE_LIMITED = 207,

    /// <summary>
    /// Token expired.
    /// </summary>
    TOKEN_EXPIRED = 208,

    /// <summary>
    /// Token revoked or invalid.
    /// </summary>
    TOKEN_REVOKED = 209,

    /// <summary>
    /// Device or factor not trusted.
    /// </summary>
    DEVICE_UNTRUSTED = 210,

    #endregion

    #region 260–299: Crypto / Integrity

    /// <summary>
    /// TLS/DTLS handshake failed.
    /// </summary>
    TLS_HANDSHAKE_FAILED = 260,

    /// <summary>
    /// TLS required but absent.
    /// </summary>
    TLS_REQUIRED = 261,

    /// <summary>
    /// Certificate invalid or untrusted.
    /// </summary>
    TLS_CERT_INVALID = 262,

    /// <summary>
    /// Message signature invalid.
    /// </summary>
    SIGNATURE_INVALID = 263,

    /// <summary>
    /// Message checksum/hash failed.
    /// </summary>
    CHECKSUM_FAILED = 264,

    /// <summary>
    /// Decryption failed.
    /// </summary>
    DECRYPTION_FAILED = 265,

    /// <summary>
    /// Replay attack detected.
    /// </summary>
    REPLAY_DETECTED = 266,

    /// <summary>
    /// Nonce or IV invalid/reused.
    /// </summary>
    NONCE_INVALID = 267,

    #endregion

    #region 300–349: Service / Infrastructure

    /// <summary>
    /// Server is shutting down intentionally.
    /// </summary>
    SERVER_SHUTDOWN = 300,

    /// <summary>
    /// Service temporarily unavailable.
    /// </summary>
    SERVICE_UNAVAILABLE = 301,

    /// <summary>
    /// Planned or emergency maintenance.
    /// </summary>
    MAINTENANCE = 302,

    /// <summary>
    /// Server overloaded (backpressure)
    /// .</summary>
    OVERLOADED = 303,

    /// <summary>
    /// Dependency (DB/cache/queue) failure.
    /// </summary>
    DEPENDENCY_FAILURE = 304,

    /// <summary>
    /// Database not reachable/failed.
    /// </summary>
    DATABASE_UNAVAILABLE = 305,

    /// <summary>
    /// Cache tier not reachable/failed.
    /// </summary>
    CACHE_UNAVAILABLE = 306,

    /// <summary>
    /// Queue/broker not reachable/failed.
    /// </summary>
    QUEUE_UNAVAILABLE = 307,

    #endregion

    #region 350–399: Application / Semantics

    /// <summary>
    /// Input validation failed.
    /// </summary>
    VALIDATION_FAILED = 350,

    /// <summary>
    /// Resource not found.
    /// </summary>
    NOT_FOUND = 351,

    /// <summary>
    /// Resource already exists.
    /// </summary>
    ALREADY_EXISTS = 352,

    /// <summary>
    /// State precondition failed (ETag/If-Match).
    /// </summary>
    PRECONDITION_FAILED = 353,

    /// <summary>
    /// Application state conflict.
    /// </summary>
    STATE_CONFLICT = 354,

    /// <summary>
    /// Unsupported media/content type.
    /// </summary>
    UNSUPPORTED_MEDIA_TYPE = 355,

    /// <summary>
    /// Serialization/formatting failed.
    /// </summary>
    SERIALIZATION_FAILED = 356,

    #endregion

    #region 400–449: Flow Control / QoS

    /// <summary>
    /// Server throttled the client.
    /// </summary>
    THROTTLED = 400,

    /// <summary>
    /// Client cannot keep up with delivery rate.
    /// </summary>
    SLOW_CONSUMER = 401,

    /// <summary>
    /// Credit/permit exhausted (credit-based flow control).
    /// </summary>
    CREDIT_EXHAUSTED = 402,

    /// <summary>
    /// Flow window exceeded.
    /// </summary>
    WINDOW_EXCEEDED = 403,

    #endregion

    #region 450–499: Resource / Quota

    /// <summary>
    /// Generic resource limit hit.
    /// </summary>
    RESOURCE_LIMIT = 450,

    /// <summary>
    /// Out of memory.
    /// </summary>
    MEMORY_EXHAUSTED = 451,

    /// <summary>
    /// Per-user/tenant connection cap reached.
    /// </summary>
    CONNECTION_LIMIT = 452,

    /// <summary>
    /// File descriptor/handle limit reached.
    /// </summary>
    FD_LIMIT = 453,

    /// <summary>
    /// Disk is full or quota exceeded.
    /// </summary>
    DISK_FULL = 454,

    /// <summary>
    /// CPU limit or budget exceeded.
    /// </summary>
    CPU_LIMIT = 455,

    #endregion

    #region 500–549: Client / Local Decisions

    /// <summary>
    /// User requested to quit.
    /// </summary>
    CLIENT_QUIT = 500,

    /// <summary>
    /// Local application aborted the operation.
    /// </summary>
    ABORTED = 501,

    /// <summary>
    /// Idle timeout on client side.
    /// </summary>
    IDLE_TIMEOUT = 502,

    /// <summary>
    /// Local configuration forbids the action.
    /// </summary>
    LOCAL_POLICY = 503,

    #endregion

    #region 550–599: Session / Time / Clock

    /// <summary>
    /// Session is not found.
    /// </summary>
    SESSION_NOT_FOUND = 550,

    /// <summary>
    /// Session expired/invalidated.
    /// </summary>
    SESSION_EXPIRED = 551,

    /// <summary>
    /// Duplicate session detected.
    /// </summary>
    DUPLICATE_SESSION = 552,

    /// <summary>
    /// Key rotation required before proceeding.
    /// </summary>
    KEY_ROTATION_REQUIRED = 553,

    /// <summary>
    /// Significant time skew detected.
    /// </summary>
    TIME_SKEW = 554,

    #endregion

    #region 600–649: Consistency / Coordination

    /// <summary>
    /// Leader changed; retry after backoff.
    /// </summary>
    LEADER_CHANGE = 600,

    /// <summary>
    /// Not the leader; use redirect/migrate.
    /// </summary>
    NOT_LEADER = 601,

    /// <summary>
    /// Consensus/quorum not reached.
    /// </summary>
    CONSENSUS_UNAVAILABLE = 602,

    /// <summary>
    /// Read was served from a stale replica.
    /// </summary>
    STALE_READ = 603,

    #endregion

    #region 650–699: Routing / Placement

    /// <summary>
    /// Resource moved; follow redirect.
    /// </summary>
    REDIRECT = 650,

    /// <summary>
    /// Shard/partition migrated; reconnect.
    /// </summary>
    MIGRATE = 651,

    /// <summary>
    /// Region/zone temporarily unavailable.
    /// </summary>
    REGION_UNAVAILABLE = 652,

    #endregion

    #region 700–749: Compliance / Policy

    /// <summary>
    /// Blocked due to legal or regulatory reasons.
    /// </summary>
    LEGAL_BLOCK = 700,

    /// <summary>
    /// Content violates policy.
    /// </summary>
    CONTENT_VIOLATION = 701,

    /// <summary>
    /// Age/parental restrictions apply.
    /// </summary>
    AGE_RESTRICTED = 702,

    #endregion

    #region 900–999: Reserved / Vendor Specific

    /// <summary>
    /// Reserved for site/vendor-specific mapping.
    /// </summary>
    RESERVED_900 = 900,

    /// <summary>
    /// Reserved for site/vendor-specific mapping.
    /// </summary>
    RESERVED_901 = 901,

    #endregion
}