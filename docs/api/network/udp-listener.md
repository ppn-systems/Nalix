# UDP Listener (Low-Level Transport)

`UdpListenerBase` orchestrates high-performance Datagram reception. Unlike TCP, UDP is connectionless. Therefore, the Nalix UDP implementation acts as a **stateless router and authenticator** that securely maps incoming datagrams to stateful TCP `Connection` objects.

!!! danger "Security Prerequisite"
    Nalix strictly enforces that UDP communication cannot exist without an active TCP connection. All UDP packets must leverage the `SessionToken` issued by the successful TCP handshake.

---

## 1. Zero-Allocation Receive Loop

The UDP Listener avoids heap allocations completely on its hot path by coupling `SocketAsyncEventArgs` with internal pooling (`BufferLease` and `SpinLock`).

```mermaid
flowchart TD
    subgraph Transport[UDP Socket Layer]
        Raw[Raw UDP Socket]
        Loop[SAEA Receive Loop]
    end

    subgraph Extraction[Header Extraction]
        SizeCheck[Size Validator]
        FlagCheck[PacketFlags Mask Verification]
        Token[Extract 7-byte Token]
    end

    subgraph Security[Security Authentication Gate]
        HubLookup[IConnectionHub Dictionary Lookup]
        IPPinning[End-Point Strict Pinning Check]
        SlidingWindow[SlidingWindow Sequence Validator]
    end

    subgraph Handoff[Processing Handoff]
        Slice[Zero-Allocation Memory Slice]
        AppHandoff[Decryption & Application Stack]
    end

    Raw -->|Incoming Datagram| Loop
    Loop --> SizeCheck
    
    SizeCheck -->|Valid Size > 10 bytes| FlagCheck
    SizeCheck -.->|Invalid Size| Drop[Silent Discard]
    
    FlagCheck -->|Valid UNRELIABLE flag| Token
    FlagCheck -.->|Malformation| Drop
    
    Token --> HubLookup
    HubLookup -->|Found Target| IPPinning
    HubLookup -.->|Token Expired/Invalid| Drop
    
    IPPinning -->|IP matched TCP Socket| SlidingWindow
    IPPinning -.->|IP Spoofed| Drop
    
    SlidingWindow -->|Sequence Unique & Recent| Slice
    SlidingWindow -.->|Packet Replayed / Stale| Drop
    
    Slice --> AppHandoff

```

## 2. Low-Level Security Pipeline

UDP is vulnerable to spoofing and reflection attacks. Nalix hardens the listener via a 3-stage validation gate:

### Stage 1: Protocol & Token Validation
- **Minimum Size Guard**: Any datagram smaller than 10 bytes is instantly dropped.
- **Flag Verification**: The listener validates `payload[6]` (the `PacketFlags` byte) to ensure it carries the `PacketFlags.UNRELIABLE` mask identifying a UDP frame.
- **Session Lookup**: The first 7 bytes (`SessionToken`) are read natively via `ReadOnlySpan<byte>` and cross-referenced with the `IConnectionHub`. Extremely fast mapping occurs without object allocation.

### Stage 2: IP Pinning (SEC-30)
Even if an attacker steals a `SessionToken`, the listener verifies that the source `IPEndPoint` of the UDP datagram stringently matches the `Connection.NetworkEndpoint` bound to the TCP socket. Spoofed IPs are instantly dropped down to the networking stack.

### Stage 3: Sliding Replay Window (SEC-27)
Due to UDP's nature, an attacker could capture a valid packet and replay it iteratively. 
- Nalix utilizes an internal lock-free (via `SpinLock`) `SlidingWindow` instance attached to the connection.
- It parses the 16-bit `SequenceId` at offset 8.
- If the packet sequence is historical or already observed within the window, the datagram is rejected.
- **Performance**: Executing the bit-shift operations inside a low-level struct SpinLock ensures the validation cost rests under 10 nanoseconds per datagram.

## 3. Buffer Lifecycle Handoff

Once authenticated, the UDP payload must be processed:
1. Nalix extracts a `BufferLease` (rented from the `ByteArrayPool`) containing the raw datagram.
2. The 7-byte `SessionToken` prefix is mathematically sliced off using `Memory<byte>` operations without array copies.
3. The remaining payload is routed to the application's `ProcessFrame` pipeline using the resolved `Connection` object as the context.

## 4. Public API Surface

| Method | Description |
|---|---|
| `Activate()` | Initializes the `Socket` and launches the continuous SAEA asynchronous receive loops. |
| `Deactivate()` | Cancels active receive tasks safely. |
| `IsAuthenticated` | **(Abstract)** The final application logic hook used by derived classes for custom datagram acceptance strategy. |

!!! tip "Performance Tuning"
    For immense traffic scenarios, ensure that `NetworkSocketOptions.BufferSize` is appropriately sized (typically between `2MB` and `10MB`) to accommodate OS-level network queuing preventing datagram drop under load spikes.
