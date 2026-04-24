# Compression Options

`CompressionOptions` controls whether outbound packet frames may be compressed and
how large a serialized payload must be before compression is attempted.

## Source Mapping

- `src/Nalix.Framework/Options/CompressionOptions.cs`
- `src/Nalix.Framework/DataFrames/Transforms/FramePipeline.cs`
- `src/Nalix.Runtime/Dispatching/PacketSender.cs`
- `src/Nalix.Network.Hosting/Bootstrap.cs`

## Defaults and Validation

| Property | Default | Validation | Runtime effect |
|---|---:|---|---|
| `Enabled` | `true` | None. | Allows outbound compression when the frame size threshold is met. |
| `MinSizeToCompress` | `1024` | `0..int.MaxValue` | Minimum payload size, in bytes, before outbound compression is attempted. |

`Validate()` uses data-annotation validation through
`Validator.ValidateObject(..., validateAllProperties: true)`.

## Hosting Initialization

`Bootstrap.Initialize()` loads `CompressionOptions` during server startup so the
configuration template is materialized into `server.ini`:

```csharp
_ = ConfigurationManager.Instance.Get<CompressionOptions>();
```

## Outbound Runtime Flow

`PacketSender` reads `CompressionOptions` as a static configuration instance and
passes its values into `FramePipeline.ProcessOutbound(...)` for each send.

```mermaid
flowchart LR
    A["PacketSender.SendAsync"] --> B["Serialize packet into BufferLease"]
    B --> C["FramePipeline.ProcessOutbound"]
    C --> D{"Enabled && payload >= MinSizeToCompress"}
    D -->|Yes| E["PacketCompression.CompressFrame"]
    D -->|No| F["Keep serialized frame"]
    E --> G{"Encrypt requested?"}
    F --> G
    G -->|Yes| H["PacketCipher.EncryptFrame"]
    G -->|No| I["Transport.SendAsync"]
    H --> I
```

The exact source condition is:

```csharp
bool doCompress = enableCompress &&
    (current.Length - FrameTransformer.Offset) >= minSizeToCompress;
```

`FrameTransformer.Offset` is subtracted before comparing with the threshold, so the
threshold applies to the payload portion rather than the full frame length.

## Transform Ordering

| Direction | Source order | Notes |
|---|---|---|
| Outbound | Compress, then encrypt | Compression is attempted before encryption so encrypted data is not compressed. |
| Inbound | Decrypt, then decompress | The pipeline re-reads frame flags after decryption before checking compression. |

`CompressionOptions` only controls the outbound compression decision. Inbound
`FramePipeline.ProcessInbound(...)` relies on packet flags; if a received frame is
marked `COMPRESSED`, the pipeline attempts decompression regardless of local
`CompressionOptions.Enabled`.

## Buffer Ownership

`FramePipeline.ProcessOutbound(...)` mutates the current lease by `ref`. When a
transform produces a replacement lease, it disposes the previous lease and assigns
the new one back to `current`. `PacketSender` then disposes any replacement lease
after `Transport.SendAsync(...)`, and always disposes the original raw lease in its
outer `finally` block.

## Operational Guidance

The default `MinSizeToCompress = 1024` avoids spending CPU on small payloads that
may not benefit from compression after headers and framing overhead. Lower the
threshold only when payload characteristics show a measurable bandwidth win.

## Related APIs

- [Packet Sender](../../runtime/routing/packet-sender.md)
- [Network Options](./options.md)
