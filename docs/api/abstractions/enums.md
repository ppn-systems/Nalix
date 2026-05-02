# Common Enumerations

This page provides a comprehensive reference for all enumerations defined in `Nalix.Abstractions`. These constants ensure binary and semantic compatibility across the networking, security, and serialization layers.

---

## Networking & Packets

### ProtocolOpCode

Defines the reserved OpCodes for Nalix system and protocol-level internal packets. Values in the range `0x0000-0x00FF` are reserved for system use.

| Member | Value | Description |
| :--- | :--- | :--- |
| `HANDSHAKE` | `0x0000` | The default handshake protocol packet for key exchange and transcript verification. |
| `SYSTEM_CONTROL` | `0x0001` | Used for system-level control packets like PING, PONG, ERROR, DISCONNECT. |
| `SESSION_SIGNAL` | `0x0002` | Unified packet flow for session management (resume, ack, reject). |

### PacketPriority

Specifies the relative priority level of a network packet. Higher values generally indicate a greater urgency for delivery.

| Member | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0x00` | Standard priority level for most packets. |
| `LOW` | `0x01` | Lower-than-normal priority. |
| `MEDIUM` | `0x02` | Moderate priority level, between LOW and HIGH. |
| `HIGH` | `0x03` | Higher-than-normal priority. |
| `URGENT` | `0x04` | Highest priority level for urgent packets. |

### PacketFlags

Defines bitwise flags that describe the state or properties of a network packet.

| Member | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0x00` | No flags are set (uncompressed, unencrypted, not fragmented). |
| `COMPRESSED` | `1 << 1` | The packet payload has been compressed to reduce its size. |
| `ENCRYPTED` | `1 << 2` | The packet payload has been encrypted for secure transmission. |
| `FRAGMENTED` | `1 << 3` | The packet is a fragment of a larger message. |
| `RELIABLE` | `1 << 4` | The packet is sent over a reliable transport protocol (typically TCP). |
| `UNRELIABLE` | `1 << 5` | The packet is sent over an unreliable transport protocol (typically UDP). |
| `ACKNOWLEDGED` | `1 << 6` | The packet has been acknowledged by the receiver. |
| `SYSTEM` | `1 << 7` | The packet is a system-level message (ping, handshake, etc.). |

### PacketHeaderOffset

Represents the positions of fields in the serialization order.

| Member | Offset | Type | Description |
| :--- | :--- | :--- | :--- |
| `MagicNumber` | `0` | `int` | Unique identifier for the packet format or protocol. |
| `OpCode` | `4` | `ushort` | Operation code specifying the command or type of the packet. |
| `Flags` | `6` | `byte` | State or processing options for the packet. |
| `Priority` | `7` | `byte` | Relative processing priority of the packet. |
| `SequenceId` | `8` | `ushort` | Used for packet sequence correlation. |
| `Region` | `10` | - | End offset of the packet header fields. |
| `MaxValue` | `255` | - | The maximum numeric value reserved for the enum. |

### PacketContextState

Defines the lifecycle states of a `PacketContext` instance.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Pooled` | `0` | Available in the object pool. |
| `InUse` | `1` | Actively in use; not available for pooling. |
| `Returned` | `2` | Completed processing and returned for reuse. |

---

## Protocol Control & Signals

### ControlType

Identifies the kind of control message used by the protocol layer.

| Member | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0x00` | No control message specified. |
| `PING` | `0x01` | Check connection liveness. |
| `PONG` | `0x02` | Response to a ping. |
| `ACK` | `0x03` | Confirm receipt. |
| `DISCONNECT` | `0x04` | Termination request. |
| `ERROR` | `0x05` | Failure description. |
| `HEARTBEAT` | `0x07` | Keep connection active. |
| `NACK` | `0x08` | Negative acknowledgement. |
| `RESUME` | `0x09` | Resume interrupted session. |
| `SHUTDOWN` | `0x0A` | Request graceful shutdown. |
| `REDIRECT` | `0x0B` | Redirect to another endpoint. |
| `THROTTLE` | `0x0C` | Reduce transmission rate. |
| `NOTICE` | `0x0D` | Maintenance notice. |
| `TIMEOUT` | `0x10` | Operation timed out. |
| `FAIL` | `0x11` | Generic failure. |
| `TIMESYNCREQUEST` | `0x12` | Request server time. |
| `TIMESYNCRESPONSE` | `0x13` | Provide server time. |
| `CIPHER_UPDATE` | `0x14` | Request cipher suite change. |
| `CIPHER_UPDATE_ACK` | `0x15` | Acknowledge cipher change. |
| `RESERVED1` | `0xFE` | Reserved for future extension. |
| `RESERVED2` | `0xFF` | Reserved for future extension. |

### ControlFlags

Additional context flags for protocol control messages.

| Member | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0x00` | No flags set. |
| `IS_TRANSIENT` | `1 << 0` | Condition is transient and safe to retry. |
| `IS_AUTHRELATED` | `1 << 1` | Error is related to authN/authZ. |
| `HAS_REDIRECT` | `1 << 2` | Redirect fields are present. |
| `SLOW_DOWN` | `1 << 3` | Suggestion to reduce sending rate. |

### ProtocolAdvice

High-level actions suggested for a given control reason.

| Member | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0` | No specific action. |
| `RETRY` | `1` | Retry immediately. |
| `BACKOFF_RETRY` | `2` | Retry with exponential backoff. |
| `DO_NOT_RETRY` | `3` | Stop automatic retries. |
| `REAUTHENTICATE` | `4` | Refresh credentials. |
| `SLOW_DOWN` | `5` | Reduce sending rate. |
| `RECONNECT` | `6` | Switch transport or route. |
| `FIX_AND_RETRY` | `7` | Fix the issue and retry. |

### ProtocolReason

Standard reason codes for protocol control messages.

| Member | Value | Domain | Description |
| :--- | :--- | :--- | :--- |
| `NONE` | `0` | General | No reason specified. |
| `UNKNOWN` | `1` | General | Unspecified error. |
| `CANCELLED` | `2` | General | Operation cancelled. |
| `NOT_IMPLEMENTED` | `3` | General | Feature not implemented. |
| `TEMPORARY_FAILURE` | `4` | General | Temporary condition; retry later. |
| `DEPRECATED` | `5` | General | Deprecated feature. |
| `REQUEST_INVALID` | `6` | General | Malformed request. |
| `INTERNAL_ERROR` | `7` | General | Internal server error. |
| `TIMEOUT` | `100` | Transport | Timeout waiting for response. |
| `REMOTE_CLOSED` | `101` | Transport | Peer closed connection. |
| `LOCAL_CLOSED` | `102` | Transport | Local closed connection. |
| `NETWORK_ERROR` | `103` | Transport | Generic transport failure. |
| `CONNECTION_REFUSED` | `104` | Transport | Peer refused connection. |
| `CONNECTION_RESET` | `105` | Transport | Peer reset connection. |
| `DNS_FAILURE` | `106` | Transport | DNS resolution failed. |
| `MTU_VIOLATION` | `107` | Transport | MTU constraints violated. |
| `CONGESTION` | `108` | Transport | Congestion detected. |
| `KEEPALIVE_FAILED` | `109` | Transport | Ping failed. |
| `PROTOCOL_ERROR` | `150` | Framing | Protocol/Framing violation. |
| `VERSION_UNSUPPORTED` | `151` | Framing | Version not supported. |
| `FRAME_TOO_LARGE` | `152` | Framing | Frame limit exceeded. |
| `MESSAGE_TOO_LARGE` | `153` | Framing | Payload limit exceeded. |
| `UNEXPECTED_MESSAGE` | `154` | Framing | Out-of-order/type message. |
| `MISSING_REQUIRED_FIELD` | `155` | Framing | Field missing. |
| `DUPLICATE_MESSAGE` | `156` | Framing | Duplicate message. |
| `STATE_VIOLATION` | `157` | Framing | State machine violation. |
| `CRYPTO_UNSUPPORTED` | `158` | Framing | Unsupported crypto params. |
| `COMPRESSION_UNSUPPORTED` | `159` | Framing | Unsupported compression. |
| `OPERATION_UNSUPPORTED` | `160` | Framing | Operation not supported. |
| `MALFORMED_PACKET` | `161` | Framing | Malformed packet. |
| `UNAUTHENTICATED` | `200` | Security | AuthN required/failed. |
| `UNAUTHORIZED` | `201` | Security | AuthZ lacking permission. |
| `FORBIDDEN` | `202` | Security | Explicitly forbidden. |
| `ACCOUNT_LOCKED` | `203` | Security | Account locked. |
| `ACCOUNT_SUSPENDED` | `204` | Security | Account suspended. |
| `BANNED` | `205` | Security | Client/user banned. |
| `IP_BLOCKED` | `206` | Security | Source IP blocked. |
| `RATE_LIMITED` | `207` | Security | Too many requests. |
| `TOKEN_EXPIRED` | `208` | Security | Token expired. |
| `TOKEN_REVOKED` | `209` | Security | Token revoked. |
| `DEVICE_UNTRUSTED` | `210` | Security | Device/factor untrusted. |
| `TLS_HANDSHAKE_FAILED` | `260` | Crypto | TLS handshake failed. |
| `TLS_REQUIRED` | `261` | Crypto | TLS required. |
| `TLS_CERT_INVALID` | `262` | Crypto | Cert invalid. |
| `SIGNATURE_INVALID` | `263` | Crypto | Signature invalid. |
| `CHECKSUM_FAILED` | `264` | Crypto | Checksum failed. |
| `DECRYPTION_FAILED` | `265` | Crypto | Decryption failed. |
| `REPLAY_DETECTED` | `266` | Crypto | Replay attack detected. |
| `NONCE_INVALID` | `267` | Crypto | Nonce invalid/reused. |
| `SERVER_SHUTDOWN` | `300` | Infra | Intentional shutdown. |
| `SERVICE_UNAVAILABLE` | `301` | Infra | Temporarily unavailable. |
| `MAINTENANCE` | `302` | Infra | Maintenance ongoing. |
| `OVERLOADED` | `303` | Infra | Server overloaded. |
| `DEPENDENCY_FAILURE` | `304` | Infra | Dependency failure. |
| `DATABASE_UNAVAILABLE` | `305` | Infra | DB unreachable. |
| `CACHE_UNAVAILABLE` | `306` | Infra | Cache unreachable. |
| `QUEUE_UNAVAILABLE` | `307` | Infra | Queue unreachable. |
| `VALIDATION_FAILED` | `350` | App | Input validation failed. |
| `NOT_FOUND` | `351` | App | Resource not found. |
| `ALREADY_EXISTS` | `352` | App | Resource exists. |
| `PRECONDITION_FAILED` | `353` | App | Precondition failed. |
| `STATE_CONFLICT` | `354` | App | State conflict. |
| `UNSUPPORTED_MEDIA_TYPE` | `355` | App | Unsupported media type. |
| `SERIALIZATION_FAILED` | `356` | App | Formatting failed. |
| `UNSUPPORTED_PACKET` | `357` | App | Unsupported packet type. |
| `TRANSFORM_FAILED` | `358` | App | Transformation failed. |
| `THROTTLED` | `400` | QoS | Client throttled. |
| `SLOW_CONSUMER` | `401` | QoS | Client too slow. |
| `CREDIT_EXHAUSTED` | `402` | QoS | Permit exhausted. |
| `WINDOW_EXCEEDED` | `403` | QoS | Flow window exceeded. |
| `RESOURCE_LIMIT` | `450` | Resource | Limit hit. |
| `MEMORY_EXHAUSTED` | `451` | Resource | Out of memory. |
| `CONNECTION_LIMIT` | `452` | Resource | Connection cap reached. |
| `FD_LIMIT` | `453` | Resource | Handle limit reached. |
| `DISK_FULL` | `454` | Resource | Disk full. |
| `CPU_LIMIT` | `455` | Resource | CPU budget exceeded. |
| `CLIENT_QUIT` | `500` | Client | User quit. |
| `ABORTED` | `501` | Client | Local abort. |
| `IDLE_TIMEOUT` | `502` | Client | Idle timeout. |
| `LOCAL_POLICY` | `503` | Client | Local policy violation. |
| `COMPRESSION_FAILED` | `504` | Client | Compression error. |
| `SESSION_NOT_FOUND` | `550` | Session | Session not found. |
| `SESSION_EXPIRED` | `551` | Session | Session expired. |
| `DUPLICATE_SESSION` | `552` | Session | Duplicate session. |
| `KEY_ROTATION_REQUIRED` | `553` | Session | Key rotation required. |
| `TIME_SKEW` | `554` | Session | Time skew detected. |
| `LEADER_CHANGE` | `600` | Consistency | Leader changed. |
| `NOT_LEADER` | `601` | Consistency | Not the leader. |
| `CONSENSUS_UNAVAILABLE` | `602` | Consistency | Quorum not reached. |
| `STALE_READ` | `603` | Consistency | Stale read. |
| `REDIRECT` | `650` | Routing | Resource moved. |
| `MIGRATE` | `651` | Routing | Shard migrated. |
| `REGION_UNAVAILABLE` | `652` | Routing | Region unavailable. |
| `LEGAL_BLOCK` | `700` | Compliance | Legal block. |
| `CONTENT_VIOLATION` | `701` | Compliance | Policy violation. |
| `AGE_RESTRICTED` | `702` | Compliance | Age restricted. |
| `INVALID_USERNAME` | `703` | Compliance | Invalid format. |
| `WEAK_PASSWORD` | `704` | Compliance | Weak password. |
| `RESERVED_900` | `900` | Vendor | Vendor reserved. |
| `RESERVED_901` | `901` | Vendor | Vendor reserved. |

---

## Security & Identity

### CipherSuiteType

Defines the supported symmetric and AEAD cipher suites.

| Member | Value | Category | Description |
| :--- | :--- | :--- | :--- |
| `Salsa20` | `3` | Symmetric | Fast stream cipher by DJB. |
| `Chacha20` | `4` | Symmetric | Standardized stream cipher (RFC 8439). |
| `Salsa20Poly1305` | `7` | AEAD | Salsa20 + Poly1305 MAC. |
| `Chacha20Poly1305` | `8` | AEAD | Chacha20 + Poly1305 MAC (RFC 8439). |

### PermissionLevel

Coarse-grained authority levels used for access control.

| Member | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0` | No authority. |
| `GUEST` | `25` | Anonymous access. |
| `READ_ONLY` | `50` | Read-only access. |
| `USER` | `100` | Standard user. |
| `SUPERVISOR` | `175` | Elevated scope privileges. |
| `TENANT_ADMINISTRATOR` | `200` | Tenant-level admin. |
| `SYSTEM_ADMINISTRATOR` | `225` | System-wide admin. |
| `OWNER` | `255` | Highest authority. |

### SnowflakeType

Categorizes a snowflake identifier by entity type.

| Member | Value | Category | Description |
| :--- | :--- | :--- | :--- |
| `Unknown` | `0` | Core | Generic purpose. |
| `Configuration` | `1` | Core | Configuration versions. |
| `Log` | `2` | Core | Logging/Audit trails. |
| `System` | `3` | Core | System infrastructure. |
| `Account` | `10` | User | User accounts. |
| `Session` | `11` | User | Active sessions. |
| `Message` | `20` | Messaging | Messages/Chats. |
| `Notification` | `21` | Messaging | System notifications. |
| `Email` | `22` | Messaging | Email entities. |
| `Sms` | `23` | Messaging | SMS verification. |
| `Order` | `30` | Business | Orders. |
| `Inventory` | `31` | Business | Inventory items. |
| `Transaction` | `32` | Business | Financial transactions. |
| `Invoice` | `33` | Business | Invoices. |
| `SupportTicket` | `34` | Business | Support tickets. |
| `MaxValue` | `255` | - | Enum upper bound. |

---

## Serialization & Concurrency

### SerializeLayout

Describes how fields are ordered when a type is serialized.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Auto` | `0` | Automatic optimized packing. |
| `Sequential` | `1` | Order by declaration. |
| `Explicit` | `2` | Order by metadata attributes. |

### DropPolicy

Behavior when a per-connection queue is full.

| Member | Value | Description |
| :--- | :--- | :--- |
| `DropNewest` | `0` | Drop incoming packet. |
| `DropOldest` | `1` | Drop oldest in queue. |
| `Block` | `2` | Block the producer (backpressure). |
| `Coalesce` | `3` | Keep only latest unique packet. |

### MiddlewareStage

Defines the execution stages for middleware.

| Member | Value | Description |
| :--- | :--- | :--- |
| `Inbound` | `0` | Pre-handler processing. |
| `Outbound` | `1` | Post-handler processing. |
| `Both` | `2` | Inbound and outbound stages. |

### WorkerPriority

Specifies the relative dispatch priority for queued workers.

| Member | Value | Description |
| :--- | :--- | :--- |
| `LOW` | `0` | Background/cleanup tasks. |
| `NORMAL` | `1` | Normal traffic. |
| `HIGH` | `2` | Latency-sensitive work. |
| `URGENT` | `3` | Run immediately when queued. |

---

## Wire-Level Coverage (Source-Anchored)

This page is an enum reference first. Enums alone are not a full wire protocol specification for cross-language clients.

### Confirmed From Source

| Topic | Current Status | Source Anchors |
| :--- | :--- | :--- |
| Packet header binary layout | **Defined**: fixed 10 bytes, contiguous offsets, no padding (`MagicNumber[0..3]`, `OpCode[4..5]`, `Flags[6]`, `Priority[7]`, `SequenceId[8..9]`). | `src/Nalix.Abstractions/Networking/Packets/PacketHeaderOffset.cs`, `src/Nalix.Abstractions/Networking/Packets/PacketConstants.cs`, `src/Nalix.Codec/Extensions/HeaderExtensions.cs`, `src/Nalix.Codec/DataFrames/FrameBase.cs` |
| Header endianness | **Defined**: header read helpers are little-endian (`ReadHeaderLE` / `WriteHeaderLE` using `MemoryMarshal`). | `src/Nalix.Codec/Extensions/HeaderExtensions.cs` |
| TCP framing | **Defined**: stream is length-prefixed with `UInt16 LE` where prefix value is total frame size (including the 2-byte prefix). | `src/Nalix.SDK/Transport/Internal/FrameSender.cs`, `src/Nalix.SDK/Transport/Internal/FrameReader.cs`, `src/Nalix.Network/Internal/Transport/SocketConnection.Send.cs`, `src/Nalix.Network/Internal/Transport/SocketConnection.cs` |
| UDP framing | **Defined**: outbound datagram is `[SessionToken(8 bytes) | Payload]`; token is `Snowflake` in little-endian layout. | `src/Nalix.SDK/Transport/UdpSession.cs`, `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs`, `src/Nalix.Framework/Identifiers/Snowflake.Serialization.cs` |
| Fragment chunk format | **Defined**: per-chunk payload starts with 8-byte `FragmentHeader` (`Magic=0xF0`, `StreamId u16 LE`, `ChunkIndex u16 LE`, `TotalChunks u16 LE`, `Flags`). | `src/Nalix.Codec/DataFrames/Chunks/FragmentHeader.cs`, `src/Nalix.Codec/DataFrames/Chunks/FragmentAssembler.cs`, `src/Nalix.SDK/Transport/Internal/FrameSender.cs`, `src/Nalix.Network/Internal/Transport/SocketConnection.Send.cs` |
| Handshake packet structure | **Defined**: fixed-size `Handshake` frame with stage/reason/token/pubkey/nonce/proof/transcript fields. | `src/Nalix.Codec/DataFrames/SignalFrames/Handshake.cs`, `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`, `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs`, `src/Nalix.Codec/Security/HandshakeX25519.cs` |
| Crypto envelope | **Defined**: envelope header is 12 bytes (`NALX`, version, suite, flags, nonceLen, seq LE); AEAD payload is `header||nonce||ciphertext||tag(16)`. | `src/Nalix.Codec/Security/Internal/EnvelopeHeader.cs`, `src/Nalix.Codec/Security/Internal/EnvelopeFormat.cs`, `src/Nalix.Codec/Security/Engine/AeadEngine.cs`, `src/Nalix.Codec/Security/EnvelopeCipher.cs` |
| Session resume packet and proof | **Defined**: `SessionResume` is fixed size (`52` bytes total frame) and proof is HMAC-Keccak256 over 8-byte token. | `src/Nalix.Codec/DataFrames/SignalFrames/SessionResume.cs`, `src/Nalix.Runtime/Handlers/SessionHandlers.cs` |

### Known Gaps For Cross-Language Clients

The following items are still missing as a single canonical, RFC-style wire contract:

- A normative `Protocol Wire Specification` document that defines all packet diagrams and serialization rules in one place.
- Canonical hex/byte examples for control paths such as `PING`, `HANDSHAKE`, and `CIPHER_UPDATE`.
- Explicit statement about big-endian runtime support policy. (Current code paths are LE-centric; some generic writer helpers use native-endian writes.)
- A single formal statement of handshake negotiation policy. (Current runtime sets `Chacha20Poly1305` after handshake and does not currently allow practical live suite switching.)
- A normative statement for fragmented-packet signaling semantics (`FragmentHeader` magic is authoritative in code paths; `PacketFlags.FRAGMENTED` is not the current detection gate).
- Zero-RTT/resumption-ticket format beyond current `SessionToken + proof` flow.

### Suggested Next Documentation Page

Create `Protocol Wire Specification` and keep it source-anchored to the files above. Recommended sections:

- Packet header layout (byte diagram + field table).
- Transport framing (TCP stream framing vs UDP datagram framing).
- Fragmentation framing and reassembly.
- Handshake sequence diagram with packet-by-packet payload fields.
- Crypto parameters (nonce, AAD, key derivation, envelope formats).
- Example packets (hex dump + decoded fields).

Related existing references:

- [Frame Model](../codec/packets/frame-model.md)
- [Fragmentation](../codec/packets/fragmentation.md)
- [Handshake Protocol (X25519)](../security/handshake.md)
- [AEAD and Envelope](../security/aead-and-envelope.md)
- [Session Resumption Protocol](../security/session-resume.md)
