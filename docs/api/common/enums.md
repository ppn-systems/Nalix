# Common Enumerations

This page provides a comprehensive reference for all enumerations defined in `Nalix.Common`. These constants ensure binary and semantic compatibility across the networking, security, and serialization layers.

## Networking & Packets

### PacketPriority
Specifies the relative priority level of a network packet.

| Name | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0x00` | Standard priority level for most packets. |
| `LOW` | `0x01` | Lower-than-normal; may be delayed in favor of higher priority. |
| `MEDIUM` | `0x02` | Moderate priority between LOW and HIGH. |
| `HIGH` | `0x03` | Higher-than-normal; processed before NONE and MEDIUM. |
| `URGENT` | `0x04` | Highest priority; processed as soon as possible. |

### PacketFlags
Bitwise flags describing properties of a network packet.

| Name | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0x00` | No flags set (uncompressed, unencrypted). |
| `COMPRESSED` | `0x02` | Payload is compressed and requires decompression. |
| `ENCRYPTED` | `0x04` | Payload is encrypted and requires decryption. |
| `FRAGMENTED` | `0x08` | Packet is a fragment of a larger message. |
| `RELIABLE` | `0x10` | Sent over a reliable transport (e.g., TCP). |
| `UNRELIABLE` | `0x20` | Sent over an unreliable transport (e.g., UDP). |
| `ACKNOWLEDGED`| `0x40` | The packet has been acknowledged by the receiver. |
| `SYSTEM` | `0x80` | System-level message (ping, handshake, etc.). |

### ProtocolOpCode (Reserved)
Reserved OpCodes for system and protocol-level internal packets (Range: `0x0000-0x00FF`).

| Name | Value | Description |
| :--- | :--- | :--- |
| `HANDSHAKE` | `0x0000` | Key exchange and transcript verification. |
| `SYSTEM_CONTROL`| `0x0001` | Control packets (PING, PONG, ERROR, DISCONNECT). |
| `SESSION_SIGNAL`| `0x0002` | Session management (resume, ack, reject). |

### ProtocolType
Identifies the transport protocol associated with a packet.

| Name | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0x00` | No transport protocol specified. |
| `TCP` | `0x06` | Transmission Control Protocol. |
| `UDP` | `0x11` | User Datagram Protocol. |

### PacketHeaderOffset
Defines the byte offsets of fields within the serialized packet header.

| Name | Offset | Type | Description |
| :--- | :--- | :--- | :--- |
| `MagicNumber` | `0` | `uint` | Unique identifier for packet format. |
| `OpCode` | `4` | `ushort` | Operation / Command code. |
| `Flags` | `6` | `byte` | Bitwise packet flags. |
| `Priority` | `7` | `byte` | Processing priority. |
| `Transport` | `8` | `byte` | ProtocolType. |
| `SequenceId` | `9` | `ushort` | Sequence number for correlation. |
| `Region` | `11` | - | End of header; start of payload. |

### PacketContextState
Describes the current lifecycle state of a packet within a processing pipeline.

| Name | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0` | Freshly created or uninitialized. |
| `PENDING` | `1` | Waiting for processing. |
| `PROCESSING` | `2` | Actively handled by a middleware or controller. |
| `COMPLETED` | `3` | Successfully processed. |
| `FAILED` | `4` | Terminal failure state. |

## Protocol Control & Signals

### ControlType
Identifies the kind of control message used by the protocol layer.

| Name | Value | Description |
| :--- | :--- | :--- |
| `PING` | `0x01` | Liveness check. |
| `PONG` | `0x02` | Liveness response. |
| `ACK` | `0x03` | Receipt confirmation. |
| `DISCONNECT`| `0x04` | Connection termination. |
| `ERROR` | `0x05` | Failure notification. |
| `HEARTBEAT` | `0x07` | Keep-alive signal. |
| `NACK` | `0x08` | Negative acknowledgement. |
| `RESUME` | `0x09` | Session resumption request. |
| `REDIRECT` | `0x0B` | Endpoint redirection. |
| `THROTTLE` | `0x0C` | Rate reduction request. |
| `TIMESYNC` | `0x12/0x13` | Clock synchronization request/response. |
| `CIPHER_UPD` | `0x14/0x15` | Cipher suite rotation request/ack. |

### ControlFlags
Additional context flags for protocol control messages.

| Name | Value | Description |
| :--- | :--- | :--- |
| `IS_TRANSIENT` | `0x01` | Condition is temporary and safe to retry. |
| `IS_AUTHRELATED`| `0x02` | Error is related to authN/authZ. |
| `HAS_REDIRECT` | `0x04` | Redirect metadata is present. |
| `SLOW_DOWN` | `0x08` | Suggestion to reduce sending rate. |

### ProtocolReason
Standard reason codes for protocol control messages across various error domains.

| Range | Domain | Description |
| :--- | :--- | :--- |
| `0-49` | General | NONE, UNKNOWN, CANCELLED, INTERNAL_ERROR. |
| `100-149` | Transport | TIMEOUT, REMOTE_CLOSED, CONGESTION. |
| `150-199` | Framing | PROTOCOL_ERROR, FRAME_TOO_LARGE, MALFORMED. |
| `200-259` | Auth | UNAUTHENTICATED, UNAUTHORIZED, BANNED. |
| `260-299` | Crypto | TLS_FAILED, CHECKSUM_FAILED, REPLAY_DETECTED. |
| `300-349` | Infra | SERVER_SHUTDOWN, MAINTENANCE, OVERLOADED. |
| `350-399` | App | VALIDATION_FAILED, NOT_FOUND, STATE_CONFLICT. |
| `400-449` | QoS | THROTTLED, WINDOW_EXCEEDED. |

### ProtocolAdvice
High-level suggested actions for the client based on a failure.

| Name | Value | Description |
| :--- | :--- | :--- |
| `RETRY` | `1` | Retry immediately. |
| `BACKOFF_RETRY` | `2` | Retry with exponential backoff. |
| `DO_NOT_RETRY` | `3` | Stop automatic retries. |
| `REAUTHENTICATE`| `4` | Refresh credentials/tokens. |
| `SLOW_DOWN` | `5` | Reduce transmission rate. |

## Security & Identity

### PermissionLevel
Coarse-grained authority levels. Higher values indicate broader authority.

| Name | Value | Description |
| :--- | :--- | :--- |
| `NONE` | `0` | No authority. |
| `GUEST` | `25` | Anonymous/Guest access. |
| `READ_ONLY` | `50` | Read-only access. |
| `USER` | `100` | Standard user. |
| `SUPERVISOR` | `175` | Scope-limited elevated privileges. |
| `TENANT_ADMIN` | `200` | Organization-level administrator. |
| `SYSTEM_ADMIN` | `225` | System-wide administrator. |
| `OWNER` | `255` | Highest authority (root). |

### CipherSuiteType
Supported encryption and AEAD suites.

| Name | Value | Description |
| :--- | :--- | :--- |
| `Chacha20` | `4` | Secure stream cipher (RFC 8439). |
| `Chacha20Poly1305` | `8` | Modern AEAD suite (RFC 8439). |

### SnowflakeType
Categorizes a snowflake identifier.

| Name | Value | Description |
| :--- | :--- | :--- |
| `Account` | `10` | User account IDs. |
| `Session` | `11` | Session IDs. |
| `Message` | `20` | Messaging/Chat IDs. |
| `Log` | `2` | Audit trail IDs. |

## Serialization & Concurrency

### SerializeLayout
Describes how fields are ordered when a type is serialized.

| Name | Value | Description |
| :--- | :--- | :--- |
| `Auto` | `0` | Automatic packing for minimum padding. |
| `Sequential` | `1` | Order fields by declaration. |
| `Explicit` | `2` | Order fields by metadata attributes. |

### DropPolicy
Behavior when a processing queue is full.

| Name | Value | Description |
| :--- | :--- | :--- |
| `DropNewest` | `0` | Reject incoming packet. |
| `DropOldest` | `1` | Evict oldest packet to make room. |
| `Block` | `2` | Apply backpressure to producer. |
| `Coalesce` | `3` | Merge duplicate packets by key. |

### MiddlewareStage
Defines execution stages for pipeline middleware.

| Name | Value | Description |
| :--- | :--- | :--- |
| `Inbound` | `0` | Executes before the handler. |
| `Outbound` | `1` | Executes after the handler. |
| `Both` | `2` | Executes in both stages. |

### WorkerPriority
Thread or task priority within the concurrent execution system.

| Name | Value | Description |
| :--- | :--- | :--- |
| `Idle` | `0` | Executed only when system is quiet. |
| `Background` | `1` | Lower priority tasks. |
| `Normal` | `2` | Default execution priority. |
| `RealTime` | `3` | High priority, latency-sensitive tasks. |
