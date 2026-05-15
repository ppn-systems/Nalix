# Sequencing
 
This page covers the monotonic sequence number generator in `Nalix.Environment.Sequencing`.
 
## Source mapping
 
- `src/Nalix.Environment/Sequencing/SequenceCounter.cs`
 
## Main types
 
- `SequenceCounter`
 
## SequenceCounter
 
`SequenceCounter` provides a thread-safe, monotonically increasing sequence number generator. It is critical for security when using stream ciphers (such as ChaCha20 or Salsa20) to prevent nonce reuse attacks.
 
### Key Features
 
- **Thread-Safe**: Uses `Interlocked` operations for atomic increments.
- **Monotonic**: Guaranteed to never repeat values during the lifetime of the instance.
- **Security-First**: Designed to be used separately for each communication direction (send and receive).
 
### Public Members
 
| Member | Description |
| --- | --- |
| `Next()` | Returns the next sequence number and increments the counter atomically. |
| `Current()` | Returns the current sequence number without incrementing it. |
| `IsValid(receivedSeq, window)` | Validates whether a received sequence number is valid (helps prevent replay attacks). |
| `UpdateTo(receivedSeq)` | Updates the internal counter to the received sequence number if it is higher. |
| `ResumeFrom(lastKnownSeq, safetyGap)` | Resumes the counter from a previously saved value with an optional safety gap. |
| `Reset(newValue)` | Resets the counter to a new value (use only during key rotation). |
 
### Usage Example
 
```csharp
using Nalix.Environment.Sequencing;
 
// Initialize a counter (starts at 0)
var counter = new SequenceCounter();
 
// Get the next sequence number for an outbound packet
uint seq = counter.Next(); // returns 1
 
// Validate a received sequence number
bool isValid = counter.IsValid(receivedSeq, window: 32);
if (isValid)
{
    // Update counter after successful processing
    counter.UpdateTo(receivedSeq);
}
```
 
### Security Considerations
 
- **Directional Isolation**: Always use separate `SequenceCounter` instances for sending and receiving. Sharing a counter across directions will lead to nonce reuse, compromising the encryption.
- **Key Rotation**: When resetting a counter, you **must** perform a full cryptographic key rotation. Reusing a key with a reset counter allows for replay attacks and plaintext recovery.
- **Replay Protection**: The `IsValid` method combined with `UpdateTo` provides a basic mechanism to reject old or duplicate packets.
 
## Related APIs
 
- [AEAD and Envelope](../../security/aead-and-envelope.md)
- [Handshake Protocol](../../security/handshake.md)
- [Frame Model](../../codec/packets/frame-model.md)
