# 📩 Nalix AI Skill — SDK Request/Response Pattern

This skill explains the internal mechanics of the `SendRequestAsync<TResponse>` pattern, which provides a familiar async-await experience over an inherently asynchronous message-based protocol.

---

## 🏗️ The Request/Response Engine

When you call `SendRequestAsync`, the following sequence occurs:

1.  **Sequence Generation:** A unique `ushort` Sequence ID is assigned to the request.
2.  **TCS Registration:** A `TaskCompletionSource<TResponse>` is created and stored in a dictionary keyed by the Sequence ID.
3.  **Sending:** The packet is sent to the server with the Sequence ID in the header.
4.  **Waiting:** The calling thread awaits the TCS task.
5.  **Matching:** When a response arrives from the server, the SDK looks up the TCS by Sequence ID and completes the task.

---

## 📜 Timeout & Cleanup

To prevent memory leaks and infinite waits, every request has a timeout.

- **`RequestOptions.Timeout`**: Default is typically 10-30 seconds.
- **Cancellation:** If the timeout expires or the user cancels the `CancellationToken`, the TCS is cancelled, and the entry is removed from the dictionary.

---

## ⚡ Performance Optimization

- **Pooled TCS:** For high-frequency requests, the SDK can use `ValueTaskCompletionSource` or a pooled version of TCS to reduce allocations.
- **Fast Lookup:** The Sequence ID dictionary is optimized for high-concurrency access.

---

## 🛠️ Usage Patterns

### Standard Request
```csharp
var profile = await client.SendRequestAsync<UserProfilePacket>(new GetProfileRequest { UserId = 123 });
```

### Custom Options
```csharp
var result = await client.SendRequestAsync<LoginResponse>(loginReq, new RequestOptions { Timeout = TimeSpan.FromSeconds(5) });
```

---

## 🛡️ Common Pitfalls

- **Sequence Wrapping:** `ushort` only has 65,535 values. The SDK must handle Sequence ID reuse correctly (ensuring old requests have timed out before reusing an ID).
- **Type Mismatch:** If the server sends a response packet that doesn't match the expected `TResponse`, the SDK should throw a `ProtocolException`.
- **Large Responses:** For requests that return multiple packets (streaming), do not use `SendRequestAsync`. Use the `Stream` API instead.
