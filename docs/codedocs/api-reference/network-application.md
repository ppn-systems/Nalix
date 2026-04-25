---
title: "NetworkApplication"
description: "Reference for the runnable Nalix host entry point and its lifecycle methods."
---

`NetworkApplication` is the top-level server runtime in `Nalix.Network.Hosting`. It is defined in [NetworkApplication.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplication.cs) and is the object returned by `INetworkApplicationBuilder.Build()`.

## Import Path

```csharp
using Nalix.Network.Hosting;
```

## Source

- [src/Nalix.Network.Hosting/NetworkApplication.cs](/workspace/home/nalix/src/Nalix.Network.Hosting/NetworkApplication.cs)

## Signature

```csharp
public sealed class NetworkApplication : IActivatableAsync
{
    public static NetworkApplicationBuilder CreateBuilder();
    public Task RunAsync(CancellationToken cancellationToken = default);
    public Task ActivateAsync(CancellationToken cancellationToken = default);
    public Task DeactivateAsync(CancellationToken cancellationToken = default);
    public void Dispose();
}
```

## What It Does

`NetworkApplication` owns startup and shutdown order. On activation it prepares shared services, creates the packet dispatch runtime, starts listeners, and then activates hosted services. On deactivation it tears those pieces down in reverse order.

## Methods

### `CreateBuilder`

```csharp
public static NetworkApplicationBuilder CreateBuilder();
```

Returns a new builder backed by an internal `HostingBuilderContext`.

Example:

```csharp
NetworkApplicationBuilder builder = NetworkApplication.CreateBuilder();
```

### `RunAsync`

```csharp
public Task RunAsync(CancellationToken cancellationToken = default);
```

Activates the application, waits until the token is cancelled, then deactivates it.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `cancellationToken` | `CancellationToken` | `default` | Stops the host and triggers shutdown when cancelled. |

Example:

```csharp
using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await app.RunAsync(cts.Token);
```

### `ActivateAsync`

```csharp
public Task ActivateAsync(CancellationToken cancellationToken = default);
```

Starts the dispatcher, listeners, and hosted services if the application has not already been started.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `cancellationToken` | `CancellationToken` | `default` | Cancels the startup process. |

Example:

```csharp
await app.ActivateAsync();
// run other work here
await app.DeactivateAsync();
```

### `DeactivateAsync`

```csharp
public Task DeactivateAsync(CancellationToken cancellationToken = default);
```

Stops listeners first, disposes protocols, stops hosted services, and then deactivates the packet dispatcher.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `cancellationToken` | `CancellationToken` | `default` | Passed to hosted services during shutdown. |

Example:

```csharp
await app.DeactivateAsync();
```

### `Dispose`

```csharp
public void Dispose();
```

Calls `DeactivateAsync(CancellationToken.None)` and suppresses finalization.

## Lifecycle Pattern

```csharp
using NetworkApplication app = NetworkApplication.CreateBuilder()
    .AddPacket<MyPacket>()
    .AddHandler<MyHandlers>()
    .AddTcp<MyProtocol>()
    .Build();

await app.RunAsync();
```

## Related Types

- [Network Builder](/docs/api-reference/network-builder)
- [Protocol and Network](/docs/api-reference/protocol-and-network)
- [Dispatch Runtime](/docs/api-reference/dispatch-runtime)
