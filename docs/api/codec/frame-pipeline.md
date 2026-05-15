# Frame Pipeline
 
This page covers the high-level transform orchestration in `Nalix.Codec.Transforms`.
 
## Source mapping
 
- `src/Nalix.Codec/Transforms/FramePipeline.cs`
 
## Main types
 
- `FramePipeline`
 
## FramePipeline
 
`FramePipeline` unifies the execution of cryptographic and compression transforms for inbound and outbound frames. It ensures that transforms are applied in the correct transport order while optimizing for performance and memory usage.
 
### Transport Order
 
- **Inbound**: Decrypt first $\rightarrow$ Decompress.
- **Outbound**: Compress first $\rightarrow$ Encrypt.
 
### Key Methods
 
| Method | Description |
| --- | --- |
| `ProcessInbound(...)` | Applies inbound transforms (decryption, then decompression) to a buffer lease. |
| `ProcessOutbound(...)` | Applies outbound transforms (compression, then encryption) to a buffer lease. |
 
### Performance Optimizations
 
- **Direct Mutation**: Uses `ref IBufferLease` to mutate the lease reference directly, avoiding unnecessary allocations when possible.
- **Fused Outbound Path**: When both compression and encryption are enabled, `FramePipeline` uses a specialized "fused" path that rents a single large buffer to perform both operations, reducing pool pressure.
- **Aggressive Inlining**: Key methods are marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.
 
### Usage
 
The `FramePipeline` is typically called by transport listeners (like `TcpListener` or `UdpListener`) or session managers.
 
```csharp
// Example inbound processing
FramePipeline.ProcessInbound(ref lease, secret, algorithm, out uint? seq);
 
// Example outbound processing
FramePipeline.ProcessOutbound(
    ref lease, 
    enableCompress: true, 
    minSizeToCompress: 128, 
    enableEncrypt: true, 
    secret: encryptionKey, 
    seq: nextSeq, 
    algorithm: CipherSuiteType.Chacha20Poly1305);
```
 
### Error Handling
 
- Throws if a packet is marked as encrypted but no cipher or key is provided.
- Automatically disposes intermediate buffers if an exception occurs during the fused outbound path.
- Transparently handles the `ENCRYPTED` and `COMPRESSED` flags in the packet header.
 
## Related APIs
 
- [Frame Model](./packets/frame-model.md)
- [AEAD and Envelope](../security/aead-and-envelope.md)
- [LZ4](./lz4.md)
- [BufferLease](../environment/memory/buffer-lease.md)
- [Sequencing](../environment/sequencing.md)
