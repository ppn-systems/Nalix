# Bool Refactor Tracking

- Fixed: `ConnectionHub.RegisterConnection` and `ConnectionHub.UnregisterConnection`
  Files:
  - `E:\Cs\Nalix\src\Nalix.Common\Networking\IConnection.Hub.cs`
  - `E:\Cs\Nalix\src\Nalix.Network\Connections\Connection.Hub.cs`
  Notes:
  - Converted from `bool` return values to exception-based semantics.
  - Do not re-scan these methods for pending bool-failure cleanup.

- Fixed: `LZ4Decoder.Decode` overloads
  Files:
  - `E:\Cs\Nalix\src\Nalix.Framework\LZ4\Engine\LZ4Decoder.cs`
  Notes:
  - Removed silent `false` failure flow for invalid headers and decode corruption.
  - Lease ownership is disposed on all failing paths.
  - Do not re-scan these methods for pending bool-failure cleanup.

- Fixed: `LZ4Codec` / `LZ4Encoder`
  Files:
  - `E:\Cs\Nalix\src\Nalix.Framework\LZ4\LZ4Codec.cs`
  - `E:\Cs\Nalix\src\Nalix.Framework\LZ4\Engine\LZ4Encoder.cs`
  Notes:
  - Removed public `bool`-based encode/decode failure flow for LZ4 operations.
  - Span-based APIs now throw on invalid buffers or unexpected codec failures.
  - Lease-based APIs transfer ownership explicitly and dispose on all failing paths.
  - Do not re-scan these methods for pending bool-failure cleanup.
