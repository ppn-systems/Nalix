# Compression Options

`CompressionOptions` controls global compression enablement and minimum payload threshold for compression.

## Audit Summary

- Source mapping was previously incorrect and has been fixed.
- This page needed the same audit/missing/rationale structure as the rest of the API set.

## Missing Content Identified

- Explicit rationale for threshold-based compression behavior.
- Uniform section ordering with other options pages.

## Improvement Rationale

Consistency improves readability and keeps cross-package option docs comparable.

## Source Mapping

- `src/Nalix.Framework/Options/CompressionOptions.cs`

## Properties

- `Enabled`
- `MinSizeToCompress`

## Why This Matters

Compression should only run on payload sizes where it is likely to produce net benefit.

## Related APIs

- [Packet Sender](../../runtime/routing/packet-sender.md)
- [Network Options](./options.md)
