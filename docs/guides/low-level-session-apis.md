# Low-Level Transport Session APIs (Nalix.SDK)

!!! warning "Advanced Topic"
    This page describes raw buffer event loops, manual memory retention, and bypassing framework protections. Assume extreme caution.

!!! info "Learning Signals"
    - :fontawesome-solid-layer-group: **Level**: Advanced
    - :fontawesome-solid-clock: **Time**: 15 minutes
    - :fontawesome-solid-book: **Prerequisites**: [Client Session Initialization](client-session-connect.md)

The primary `client-session-connect.md` guide focuses on high-level workflows like strongly-typed packet subscriptions and automatic connection wrappers. However, for maximum pipeline control, custom proxy applications, or extreme hot-path optimization, developers can interact with the lowest-level APIs inside a `TransportSession`.

## 1. Raw Message Hooks (Zero-Allocation Paths)

Instead of using `session.On<MyPacket>(...)`, which dynamically deserializes classes using the framework's catalog memory footprint, you can subscribe raw memory handlers.

### Purely Synchronous Parsing (`OnMessageReceived`)
Fires directly in the I/O socket read loop. You are provided with an `IBufferLease`. The framework will automatically dispose of the lease (reclaiming the buffer) after your inline function exits.

```csharp
// WARNING: Do NOT execute heavy CPU loops or blocking await tasks here.
// You block the entire network stream from receiving subsequent frames.
session.OnMessageReceived += (sender, lease) => 
{
    // Inspect underlying memory safely without throwing classes on the Garbage Collector
    ReadOnlySpan<byte> rawData = lease.Span;
    if (rawData[..2].SequenceEqual(MyMagicNumber)) {
        Console.WriteLine("Matched Magic Bytes");
    }
    
    // lease is disposed inherently UNLESS you call lease.Retain() to hold ownership.
    // If you retain it, YOU must manually call `lease.Dispose()` later to prevent memory leaks!
};
```

### Purely Asynchronous Queuing (`OnMessageAsync`)
Provides an asynchronous backpressure pipeline. The framework safely copies the buffer or manages its persistence automatically via `TransportOptions.AsyncQueueCapacity`. 

```csharp
session.OnMessageAsync += async (payload) => 
{
    // payload is a ReadOnlyMemory<byte> safely detached from the hot path
    await Database.StreamChangesAsync(payload);
};
```

## 2. Granular Handshake and Resumption Controls

When orchestrating connections manually using `ConnectAsync()`, it is extremely important to understand the difference between establishing a **brand-new connection** and recovering from a **network disconnect**:

- **Resume (`ResumeSessionAsync`)**: Used **exclusively** when a client was disconnected but still retains the `SessionToken` and symmetric `Secret` in memory. It bypasses asymmetric math completely.
- **Handshake (`HandshakeAsync`)**: Used to derive a brand new set of cryptographic keys. It is only necessary for brand new secure sessions (or if your server explicitly mandates it; for raw plaintext local servers, you might completely skip both).

```csharp
// 1. Establish raw TCP stream
await session.ConnectAsync();

// 2. Check if this is a Reconnection (We still have the old Identity State)
if (!options.SessionToken.IsEmpty && options.Secret.Length > 0)
{
    // Fast path: Tell the server we dropped connection but still have our keys.
    ProtocolReason resumeResult = await session.ResumeSessionAsync();
    
    if (resumeResult == ProtocolReason.NONE)
    {
        Console.WriteLine("Dropped session recovered. No handshake needed!");
        return; // We can securely transmit data immediately.
    }
    
    // If resume failed (e.g. server evicted the session), we usually must 
    // drop the socket, re-ConnectAsync(), and fallback to Handshake.
}

// 3. Brand New Connection (Or Fallback)
// Negotiates entirely new encryption keys dynamically using X25519.
// NOTE: Not necessary if your server purely runs in unencrypted Dev mode without Handshake handlers.
await session.HandshakeAsync();
```

## 3. Raw Buffer Transmission (Bypassing Serialization)

For systems like proxies or relays, you don't want to deserialize a packet just to reserialize it. The Nalix SDK exposes a `SendAsync` overload that accepts `ReadOnlyMemory<byte>` directly. This skips the `IPacketRegistry` and writes bytes directly to the underlying `FrameSender`.

```csharp
byte[] precomputedLoginPacket = ComputeStaticLogin();

// Bypasses struct instantiation, interface boxing, and serialization entirely.
// Memory is grabbed, compressed, encrypted, and fired instantly.
await session.SendAsync(precomputedLoginPacket.AsMemory());
```

> [!TIP]
> This is massively beneficial for high throughput servers blasting the exact same static packet (e.g. ping signals, map state updates) to thousands of clients. Calculate the bytes once, and pass the memory slice to `SendAsync()`.

## 4. Manual Encryption Overrides (Per-Packet)

While `TransportOptions.EncryptionEnabled` strictly governs global cryptographic enforcement across the `TransportSession`, there are scenarios where a highly specific internal frame must bypass the global rules (e.g. sending a `Handshake` packet before encryption keys are actually finalized).

If utilizing a `TcpSession` directly, you can access an intense `encrypt: bool?` override on `SendAsync`:

```csharp
TcpSession session = (TcpSession)mySession;

// Forces this specific HandshakePacket to travel in absolute plaintext
// ignoring `EncryptionEnabled = true` safely to avoid race conditions.
await session.SendAsync(new HandshakePacket(), encrypt: false);
```

> [!CAUTION]
> Manually overriding encryption flags on custom endpoints can severely crash the connection if the server receiving the frame is statically enforcing ciphertext validation via its middleware. Proceed with extreme caution and ensure your server-side configurations expect plaintext bursts.

## Related Information Paths
- [High-Level Client Initialization](client-session-connect.md)
- [Zero Allocation Hot Paths](zero-allocation-hot-path.md)
- [Transport Session Overviews](../api/sdk/transport-session.md)
