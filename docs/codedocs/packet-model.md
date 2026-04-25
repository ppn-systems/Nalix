---
title: "Packet Model"
description: "Learn how Nalix defines packets, computes packet identity, and builds immutable packet registries for server and client code."
---

The packet model is the foundation of Nalix. It defines how a type becomes a wire-level packet, how that packet gets a stable identity, and how both the server and the client deserialize it without hand-written switch statements.

## What This Concept Is

`FrameBase` and `PacketBase<TSelf>` in [FrameBase.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/FrameBase.cs) and [PacketBase.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketBase.cs) are the base abstractions for concrete packet contracts. `PacketRegistryFactory`, `PacketRegistry`, and `PacketRegister` in [src/Nalix.Framework/DataFrames](/workspace/home/nalix/src/Nalix.Framework/DataFrames) build the lookup table that turns raw bytes back into typed packets.

This exists so you can define packet contracts once and reuse them everywhere. The same concrete packet types drive server dispatch in `Nalix.Runtime` and client deserialization in `Nalix.SDK`.

## How It Relates To Other Concepts

- The [Hosting Bootstrap](/workspace/home/codedocs-template/content/docs/hosting-bootstrap.mdx) concept uses `AddPacket(...)` and packet namespace scanning to create the registry automatically.
- The [Dispatch Pipeline](/workspace/home/codedocs-template/content/docs/dispatch-pipeline.mdx) concept depends on the registry to resolve packet types before handlers run.
- The [Transport Sessions](/workspace/home/codedocs-template/content/docs/transport-sessions.mdx) concept reuses the same registry on the client for typed subscriptions and request matching.

## How It Works Internally

`FrameBase` defines the header contract every packet exposes: `MagicNumber`, `OpCode`, `Flags`, `Priority`, `SequenceId`, `Length`, and serialization methods. `PacketBase<TSelf>` builds on that in three important ways:

1. It computes a per-type magic number once through `PacketRegistryFactory.Compute(typeof(TSelf))`.
2. It delegates actual field serialization to `LiteSerializer`.
3. It optionally uses object pooling depending on `PacketOptions.EnablePooling`.

The constructor of `PacketBase<TSelf>` assigns `MagicNumber` automatically, which is why most user-defined packets only need to set `OpCode`. The static `Deserialize(ReadOnlySpan<byte>)` method validates the header, checks the magic number, creates or rents a packet instance, and feeds the payload through `LiteSerializer.Deserialize(...)`.

`PacketRegistryFactory` turns packet discovery into a startup step. It can:

- register one packet explicitly with `RegisterPacket<TPacket>()`
- scan all packet types in an assembly with `RegisterAllPackets(...)`
- include assemblies for namespace-based scanning with `IncludeAssembly(...)`
- scan namespaces through `IncludeNamespace(...)` or `IncludeNamespaceRecursive(...)`

When `CreateCatalog()` runs, it freezes the resulting deserializer lookup into a `PacketRegistry`. That registry then exposes `Deserialize(...)`, `TryDeserialize(...)`, `IsKnownMagic(...)`, and `IsRegistered<TPacket>()`.

```mermaid
flowchart TD
  A[PacketBase<TSelf>] --> B[Auto Magic Number]
  A --> C[LiteSerializer]
  D[PacketRegistryFactory] --> E[Scan Types / Assemblies / Namespaces]
  E --> F[CreateCatalog()]
  F --> G[PacketRegistry]
  G --> H[Server Dispatch]
  G --> I[SDK Sessions]
```

## Basic Usage

Use `PacketBase<TSelf>` for shared contracts in a class library referenced by both server and client projects.

```csharp
using Nalix.Framework.DataFrames;
using Nalix.Common.Serialization;

[SerializePackable]
public sealed class LoginRequest : PacketBase<LoginRequest>
{
    public const ushort Code = 0x2001;

    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public LoginRequest() => OpCode = Code;
}

[SerializePackable]
public sealed class LoginResponse : PacketBase<LoginResponse>
{
    public const ushort Code = 0x2002;

    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public LoginResponse() => OpCode = Code;
}
```

Build a registry explicitly when you are outside the hosting builder:

```csharp
using Nalix.Framework.DataFrames;

PacketRegistryFactory factory = new();
factory.RegisterPacket<LoginRequest>()
       .RegisterPacket<LoginResponse>();

PacketRegistry catalog = factory.CreateCatalog();
```

## Advanced Usage

Use explicit serialization order when binary compatibility matters across versions or multiple deployments.

```csharp
using Nalix.Framework.DataFrames;
using Nalix.Common.Serialization;

[SerializePackable(SerializeLayout.Explicit)]
public sealed class MatchState : PacketBase<MatchState>
{
    public const ushort Code = 0x3001;

    [SerializeOrder(0)]
    public int Round { get; set; }

    [SerializeOrder(1)]
    public int ScoreA { get; set; }

    [SerializeOrder(2)]
    public int ScoreB { get; set; }

    public MatchState() => OpCode = Code;
}

PacketRegistry registry = new PacketRegistry(factory =>
{
    factory.IncludeCurrentDomain()
           .IncludeNamespaceRecursive("MyApp.Contracts");
});
```

That pattern matches the source behavior in [PacketRegistry.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketRegistry.cs), where the `PacketRegistry(Action<PacketRegistryFactory>)` constructor is just a convenience wrapper around `CreateCatalog()`.

<Callout type="warn">Do not let server and client packet assemblies drift. `PacketBase<TSelf>.Deserialize(...)` validates the magic number before deserializing, so copying a packet class into two repos and changing only one side will fail fast at runtime. Keep contracts in one shared assembly and reference it from both ends.</Callout>

<Accordions>
<Accordion title="Auto layout vs explicit layout">
`[SerializePackable]` with the default auto layout is faster to author and works well when both server and client are upgraded together. The source code in `PacketBase<TSelf>` and `LiteSerializer` does not require you to micromanage field order for ordinary in-process teams. The trade-off is binary evolution: if you reorder fields casually and deploy server and client on different schedules, the wire contract can change in ways that are hard to spot in code review. Use explicit layout with `[SerializeOrder(n)]` when the packet shape must stay stable across independent deployments or long-lived clients.
</Accordion>
<Accordion title="Pooled packets vs simpler lifecycle">
Server-side pooling improves hot-path behavior because `PacketBase<TSelf>.Create()` can rent packet instances from `ObjectPoolManager` when `PacketOptions.EnablePooling` is true. That reduces allocation pressure during sustained traffic and matches the rest of Nalix's pooling strategy. The cost is ownership discipline: pooled packets need to be disposed or returned correctly, and handler code should not retain packet references after processing unless it fully understands the lifecycle. The SDK disables packet pooling by default in `Nalix.SDK/Bootstrap.cs`, which is a deliberate trade-off toward simpler client code rather than maximum throughput.
</Accordion>
</Accordions>

## Source Files To Read

- [FrameBase.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/FrameBase.cs)
- [PacketBase.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketBase.cs)
- [PacketRegistry.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketRegistry.cs)
- [PacketRegistryFactory.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketRegistryFactory.cs)
- [LiteSerializer.cs](/workspace/home/nalix/src/Nalix.Framework/Serialization/LiteSerializer.cs)
