# CompressionOptions

`CompressionOptions` controls when runtime compression should apply.

## Source mapping

- `src/Nalix.Network/Options/CompressionOptions.cs`

## What it controls

- whether compression is enabled
- the minimum payload size required before compression is attempted

## Basic usage

```csharp
CompressionOptions options = ConfigurationManager.Instance.Get<CompressionOptions>();
options.Validate();
```

## Related APIs

- [TransportOptions](../../sdk/options/transport-options.md)
- [Network Options](./options.md)
