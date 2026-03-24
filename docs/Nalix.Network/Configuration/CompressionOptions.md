# CompressionOptions — Control network-level compression triggers

`CompressionOptions` tunes when the transport pipeline compresses packets passing through Nalix.Network. Applying compression only when payloads are large can dramatically reduce bandwidth without extra CPU on tiny packets.

---

## Properties

| Property | Description | Default |
|----------|-------------|---------|
| `Enabled` | Globally toggle compression. Set `false` to disable compression entirely (e.g., for debugging, low-CPU environments, or when encryption already provides enough savings). | `true` |
| `MinSizeToCompress` | Minimum payload size (bytes) that triggers compression. Packets smaller than this stay uncompressed. | `1024` |

> Both settings combine with per-handler/pipeline flags (e.g., `PacketAttribute` metadata) so you can disable compression for specific packet types even when the global flag is on.

---

## Usage

- Load via configuration (`ConfigurationManager.Instance.Get<CompressionOptions>()`).
- Call `CompressionOptions.Validate()` to ensure the minimum size is non-negative.
- To favor low latency, bump `MinSizeToCompress` upward so only genuinely large payloads get compressed.
- Disable compression (`Enabled = false`) when running inside resource-constrained containers or during profiling sessions.

---

## See also

- [TransportOptions](../Nalix.SDK/Configuration/TransportOptions.md) (client-side counterpart)
- [PacketAttributes](../Routing/PacketAttributes.md) (per-packet compression overrides)
