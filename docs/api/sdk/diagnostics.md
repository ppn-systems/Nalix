# Session Diagnostics

This page covers the diagnostics helpers for `TcpSession`.

## Source mapping

- `src/Nalix.SDK/Extensions/DiagnosticsExtensions.cs`

## Main types

- `DiagnosticsExtensions`
- `TcpSessionDiagnostics`

## Basic usage

```csharp
TcpSessionDiagnostics snapshot = client.GetDiagnostics();

Console.WriteLine(snapshot.ToString());
Console.WriteLine(snapshot.SendBytesPerSecond);
```

## Public methods

- `GetDiagnostics(this TcpSession client)`
- `GetDiagnostics(this IClientConnection client)`

## What the snapshot contains

- `IsConnected`
- `Endpoint`
- `TotalBytesSent`
- `TotalBytesReceived`
- `SendBytesPerSecond`
- `ReceiveBytesPerSecond`
- `CapturedAt`

## Related APIs

- [TCP Session](./tcp-session.md)
- [Session Extensions](./tcp-session-extensions.md)
- [Thread Dispatching](./thread-dispatching.md)
- [Transport Options](./options/transport-options.md)
