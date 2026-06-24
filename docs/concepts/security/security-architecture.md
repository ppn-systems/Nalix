# Security Architecture

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Intermediate
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Architecture](../fundamentals/architecture.md)

This page explains where security decisions happen in Nalix and how the different layers work together to protect your application.

## Source Mapping

- `src/Nalix.Network/Protocols/Protocol.PublicMethods.cs`
- `src/Nalix.Network/Listeners/UdpListener/UdpListener.Receive.cs`
- `src/Nalix.Runtime/Handlers/HandshakeHandlers.cs`
- `src/Nalix.Runtime/Handlers/SessionHandlers.cs`
- `src/Nalix.SDK/Transport/Extensions/HandshakeExtensions.cs`
- `src/Nalix.SDK/Transport/Extensions/ResumeExtensions.cs`

Nalix does not treat security as a single isolated feature. Instead, security is distributed across the transport, connection, metadata, and middleware layers. This design lets you place checks at the cheapest and most appropriate point in the request path.

## Security Layers

```mermaid
flowchart LR
    subgraph Net ["Network Admission"]
        direction TB
        Listener["Transport Listener"]
        Guard["Connection Guard"]
    end

    subgraph Crypt ["State & Integrity"]
        direction TB
        State["Connection State"]
        Frames["Frame Pipeline"]
    end

    subgraph Routing ["Protocol Validation"]
        direction TB
        Proto["Protocol Rules"]
    end

    subgraph Auth ["Request Authorization"]
        direction TB
        Mw["Middleware Pipeline"]
        Meta["Packet Metadata"]
    end

    App["Application Handler"]

    Listener --> Guard
    Guard --> State
    State --> Frames
    Frames --> Proto
    Proto --> Mw
    Meta -. "declares rules" .-> Mw
    Mw --> App
```

### Layer 1: Transport Admission

`ConnectionGuard` operates at the socket level. It can reject connections based on IP address, rate of connection attempts, or other endpoint criteria — before any application resources are allocated.

### Layer 2: Connection State

`Connection` carries the live session context that many security decisions depend on:

- **Permission level** — `PermissionLevel` enum (e.g., `NONE`, `USER`, `TENANT_ADMINISTRATOR`, `SYSTEM_ADMINISTRATOR`)
- **Session identity** — Connection ID and session token
- **Cipher state** — Active encryption algorithm and shared secret
- **Remote endpoint** — Source IP and port

### Layer 3: Protocol Rules

The `Protocol` implementation controls which connections are accepted (`ValidateConnection`) and how frames are processed. Custom protocol implementations can enforce additional admission rules.

### Layer 4: Handler Metadata

Security requirements are declared directly on handler methods using attributes:

```csharp
using System.Threading.Tasks;
using Nalix.Abstractions.Networking.Packets;

[PacketHandler("AccountHandlers")]
public sealed class AccountHandlers
{
    [PacketOpcode(0x2001)]
    [PacketPermission(PermissionLevel.USER)]
    [PacketTimeout(5000)]
    [PacketRateLimit(requestsPerSecond: 10)]
    public ValueTask<AccountResponse> GetProfile(
        IPacketContext<ProfileRequest> context)
    {
        // Only executed if permission, timeout, and rate limit checks pass
    }
}
```

These attributes are resolved once during handler registration and cached as `PacketMetadata`. Middleware reads the cached metadata at request time — no reflection on the hot path.

### Layer 5: Middleware Enforcement

Packet middleware is where request-level enforcement lives. Built-in middleware includes:

| Middleware | Enforces |
| :---: | :---: |
| `PermissionMiddleware` | Rejects packets from connections below the required `PermissionLevel` |
| `TimeoutMiddleware` | Cancels handler execution exceeding the declared timeout |
| `RateLimitMiddleware` | Per-opcode and per-endpoint rate limiting via `PolicyRateLimiter` and `TokenBucketLimiter` |

Low-level transport rules (decryption validation, frame integrity) are enforced by the **`FramePipeline`** and **`Protocol`** layer before packet deserialization occurs.

## Handshake and Cryptography

Nalix includes a built-in X25519 key-agreement handshake flow:

1. Client generates an ephemeral X25519 key pair and sends a `SessionInit` frame with the public key and a nonce
2. Server generates its own ephemeral key pair, computes the shared secret, and sends a `SessionChallenge` frame
3. Client verifies the proof and sends a `SessionProof` frame; server responds with `SessionEstablished`
4. Subsequent traffic is encrypted using the active session cipher state

Handshake state is carried on the `Connection` object. After handshake completion, the connection's `Secret` and cipher state are set, enabling transparent transport encryption/decryption.

### Proof-of-Work Anti-DDoS

When adaptive PoW is enabled (`ConnectionQuotaOptions.EnableAdaptiveMode`), the server dynamically increases the PoW difficulty as the connection rate rises. Clients must solve a Keccak-256 leading-zero-bits puzzle before the handshake proceeds. This is transparent to well-behaved clients under normal load (difficulty = 0), but makes automated connection floods computationally expensive during attacks.

### Session Rekey

Long-lived sessions can rotate their symmetric key mid-session via the Session Rekey protocol. This resets 16-bit sequence counters (preventing overflow after 65,535 frames) and limits the cryptographic exposure window. Both client and server derive the new key using HKDF-Expand with a static label `nalix-session/rekey` from the current secret, without transmitting any keys over the wire. This is initiated via a control packet of type `SESSION_REKEY` and acknowledged with `SESSION_REKEY_ACK`.

## Session Resume

The session resume protocol allows clients to reconnect without repeating the full handshake. It uses the unified `SessionResume` packet with `SessionResumeStage`:

1. Client sends `SessionResume` with `Stage = REQUEST` and a session token
2. Server validates the token against `ISessionStore`
3. If valid, the server restores connection state (permissions, cipher, attributes) and responds with `Stage = RESPONSE`
4. If invalid, the server responds with a `ProtocolReason` indicating the failure

## UDP Authentication

UDP should be treated as an authenticated datagram path, not as a looser copy of TCP.

Requirements for secure UDP traffic:

- Session identity must already be established first
- Each datagram must include the session token prefix (8 bytes)
- The connection secret must be initialized
- `IsAuthenticated(...)` must validate the datagram before processing
- Replay checks should be enabled

## Recommended Security Posture

For most production deployments:

1. Establish a trusted session via TCP handshake
2. Keep identity and permission state on the `Connection` object
3. Declare packet-level rules with handler attributes
4. Enforce rules using the built-in middleware pipeline
5. Treat UDP as an authenticated extension of an already established session
6. Enable `ConnectionGuard` for socket-level admission control
7. Configure rate limiting and concurrency gates for public-facing endpoints

## Recommended Next Pages

- [Handshake Protocol](./handshake-protocol.md) — X25519 handshake details
- [Session Resumption](./session-resumption.md) — Resume protocol reference
- [Permission Levels](../../api/security/permission-level.md) — Permission enum reference
- [AEAD and Envelope](../../api/security/aead-and-envelope.md) — Encryption API
- [UDP Auth Flow](../../guides/networking/udp-security.md) — UDP authentication guide
- [Custom Middleware](../../guides/extensibility/custom-middleware.md) — Building security middleware
