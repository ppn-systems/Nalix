# Packet Handler Attributes — Command, Concurrency, Security, Rate-Limit, Timeout, Encryption

This library provides annotation attributes for packet handler/controller methods in .NET server backends.
You use these attributes to declaratively control **dispatch, security, rate limiting, concurrency, timeout, and encryption** for packet handlers.

---

## Overview Table

| Attribute                         |  Used On  | Purpose / Controls                                         |
|-----------------------------------|-----------|------------------------------------------------------------|
| `PacketControllerAttribute`       | class     | Declares a controller (group of handlers) and its metadata |
| `PacketOpcodeAttribute`           | method    | Sets command/packet OpCode                                 |
| `PacketPermissionAttribute`       | method    | Min permission required to execute                         |
| `PacketConcurrencyLimitAttribute` | method    | Limits concurrent executions, with queue option            |
| `PacketRateLimitAttribute`        | method    | Limits request rate, supports burst window                 |
| `PacketEncryptionAttribute`       | method    | Requires encryption, algorithm selection                   |
| `PacketTimeoutAttribute`          | method    | Sets max processing time (ms); triggers fail timeout       |

---

## 1. `PacketControllerAttribute`

Marks a class as a *packet controller* and defines its metadata.

```csharp
[PacketController(name: "GameControl", isActive: true, version: "2.0")]
public class GameProtocolController { ... }
```

- **name:** Friendly name for debug/logging
- **isActive:** If false, handler is ignored
- **version:** Version for migration/support

**Requirements for handlers:**

- Methods: public, 2–3 parameters
  - Param 1: implements `IPacket`
  - Param 2: implements `IConnection`
  - Param 3 (optional): `CancellationToken`
- Return type: `void`, `Task`, `ValueTask`, generic `Task<T>`, `ValueTask<T>`
- Throws on duplicates or bad signatures during scanning

---

## 2. `PacketOpcodeAttribute`

Assigns a unique OpCode to a handler method.

```csharp
[PacketOpcode(0x1802)]
public Task HandleLogin(LoginPacket packet, IConnection conn) { ... }
```

- **OpCode:** `ushort` value (matches wire/protocol commands)

---

## 3. `PacketPermissionAttribute`

Sets minimum permission required for handler.

```csharp
[PacketPermission(PermissionLevel.ADMIN)]
public Task HandleDelete(DeletePacket packet, IConnection conn) { ... }
```

- Enforces security policy: Only clients with level ≥ specified can invoke.

---

## 4. `PacketConcurrencyLimitAttribute`

Limits concurrent execution per handler; supports queue if overflow.

```csharp
[PacketConcurrencyLimit(4, queue: true, queueMax: 32)]
public Task HandleExpensiveTask(ExpensivePacket p, IConnection c) { ... }
```

- **Max:** maximum concurrent executions allowed
- **Queue:** true enables waiting in a queue (instead of hard-reject)
- **QueueMax:** cap queue length (if 0, reject when full)

---

## 5. `PacketRateLimitAttribute`

Limits how many requests per second can be processed (with burst support).

```csharp
[PacketRateLimit(8, burst: 1.5)]
public Task HandlePing(PingPacket p, IConnection c) { ... }
```

- **RequestsPerSecond:** max rate allowed
- **Burst:** burst multiplier (defaults to 1)

---

## 6. `PacketEncryptionAttribute`

Requires packets to be encrypted when sent/received; allows algorithm selection.

```csharp
[PacketEncryption(isEncrypted: true, algorithmType: CipherSuiteType.SALSA20_POLY1305)]
public Task HandleSecret(SecretPacket p, IConnection c) { ... }
```

- **IsEncrypted:** if true, encryption must be applied
- **AlgorithmType:** select cipher suite (ChaCha, Salsa, ...)

---

## 7. `PacketTimeoutAttribute`

Sets per-handler max processing time (in milliseconds).

```csharp
[PacketTimeout(2000)] // 2 seconds
public Task HandleLongTask(LongPacket p, IConnection c) { ... }
```

- Throws timeout/fail response if exceeded

---

## Full Example Controller

```csharp
[PacketController("ExampleCtrl")]
public class ExampleCtrl
{
    [PacketOpcode(0x1001)]
    [PacketPermission(PermissionLevel.USER)]
    [PacketConcurrencyLimit(2, queue: true, queueMax: 8)]
    [PacketRateLimit(5, burst: 2)]
    [PacketEncryption(isEncrypted: true)]
    [PacketTimeout(8000)]
    public async Task HandleChat(ChatPacket packet, IConnection conn, CancellationToken ct)
    {
        // Handler implementation...
    }
}
```

---

## Best Practices

- Always annotate with `PacketOpcode` for dispatcher to work
- Use permission/concurrency/rate/timeout for robust production command protection
- Encryption required for business-critical/PII
- Timeout required for potentially long-running tasks (defensive fail)

---

## License

Licensed under the Apache License, Version 2.0.  
Copyright (c) 2025 PPN Corporation.

---

## See Also

- [PacketDispatchChannel documentation](./PacketDispatchChannel.md)
- [MiddlewarePipeline](../Middleware/README.md)
