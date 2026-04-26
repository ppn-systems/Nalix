---
title: "Packet Framework"
description: "Reference for FrameBase, PacketBase, PacketRegistry, PacketRegistryFactory, PacketRegister, and LiteSerializer."
---

These types are the shared packet and serialization infrastructure used by both server and client code.

## Import Paths

```csharp
using Nalix.Framework.DataFrames;
using Nalix.Framework.Serialization;
```

## Source

- [DataFrames/FrameBase.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/FrameBase.cs)
- [DataFrames/PacketBase.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketBase.cs)
- [DataFrames/PacketRegistry.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketRegistry.cs)
- [DataFrames/PacketRegistryFactory.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketRegistryFactory.cs)
- [DataFrames/PacketRegister.cs](/workspace/home/nalix/src/Nalix.Framework/DataFrames/PacketRegister.cs)
- [Serialization/LiteSerializer.cs](/workspace/home/nalix/src/Nalix.Framework/Serialization/LiteSerializer.cs)

## `FrameBase`

```csharp
public abstract class FrameBase : IPacket
{
    public abstract int Length { get; }
    public uint MagicNumber { get; set; }
    public ushort OpCode { get; set; }
    public PacketFlags Flags { get; set; }
    public PacketPriority Priority { get; set; }
    public ushort SequenceId { get; set; }
    public abstract void ResetForPool();
    public abstract byte[] Serialize();
    public abstract int Serialize(Span<byte> buffer);
}
```

This is the common header contract for all packets.

## `PacketBase<TSelf>`

```csharp
public abstract class PacketBase<TSelf> : FrameBase, IPoolable, IPoolRentable, IReportable, IPacketDeserializer<TSelf>, IDisposable
    where TSelf : PacketBase<TSelf>, new()
{
    public override int Length { get; }
    public override byte[] Serialize();
    public override int Serialize(Span<byte> buffer);
    public static TSelf Create();
    public static TSelf Deserialize(ReadOnlySpan<byte> buffer);
    public override void ResetForPool();
    public void OnRent();
    public void Dispose();
    public string GenerateReport();
    public IDictionary<string, object> GetReportData();
    public override string ToString();
}
```

### Example

```csharp
[SerializePackable]
public sealed class ChatMessage : PacketBase<ChatMessage>
{
    public const ushort Code = 0x4001;
    public string Text { get; set; } = string.Empty;

    public ChatMessage() => OpCode = Code;
}
```

## `PacketRegistry`

```csharp
public sealed class PacketRegistry : IPacketRegistry
{
    public PacketRegistry(FrozenDictionary<uint, PacketDeserializer> deserializers);
    public PacketRegistry(Action<PacketRegistryFactory> configure);
    public int DeserializerCount { get; }
    public static PacketRegistry LoadFromNamespace(string packetNamespace, bool recursive = true);
    public static PacketRegistry LoadFromAssemblyPath(string assemblyPath, bool requirePacketAttribute = false);
    public static PacketRegistry LoadFromNamespace(string assemblyPath, string packetNamespace, bool recursive = true);
    public bool IsKnownMagic(uint magic);
    public bool IsRegistered<TPacket>() where TPacket : IPacket;
    public IPacket Deserialize(ReadOnlySpan<byte> raw);
    public bool TryDeserialize(ReadOnlySpan<byte> raw, out IPacket? packet);
}
```

## `PacketRegistryFactory`

```csharp
public sealed class PacketRegistryFactory
{
    public PacketRegistryFactory();
    public PacketRegistryFactory RegisterPacket<TPacket>() where TPacket : IPacket;
    public PacketRegistryFactory RegisterAllPackets(Assembly? asm, bool requireAttribute = false);
    public PacketRegistryFactory RegisterPacketAssembly(string assemblyPath, bool requireAttribute = false);
    public PacketRegistryFactory IncludeAssembly(Assembly? asm);
    public PacketRegistryFactory IncludeAssembly(string assemblyPath);
    public PacketRegistryFactory IncludeCurrentDomain();
    public PacketRegistryFactory RegisterCurrentDomainPackets(bool requireAttribute = false);
    public PacketRegistryFactory IncludeNamespace(string ns);
    public PacketRegistryFactory IncludeNamespaceRecursive(string rootNs);
    public PacketRegistry CreateCatalog();
    public static uint Compute(Type type);
}
```

### Example

```csharp
PacketRegistryFactory factory = new();
factory.IncludeCurrentDomain()
       .IncludeNamespaceRecursive("MyApp.Contracts");

PacketRegistry catalog = factory.CreateCatalog();
```

## `PacketRegister`

```csharp
public static class PacketRegister
{
    public static PacketRegistry CreateCatalogFromCurrentDomain(bool requirePacketAttribute = false);
    public static PacketRegistry CreateCatalogFromAssemblyPath(string assemblyPath, bool requirePacketAttribute = false);
    public static PacketRegistry CreateCatalogFromAssemblies(IEnumerable<Assembly> assemblies, bool requirePacketAttribute = false);
    public static PacketRegistry CreateCatalogFromAssemblyPaths(IEnumerable<string> assemblyPaths, bool requirePacketAttribute = false);
}
```

Use it when you want convenience helpers rather than fluent factory composition.

## `LiteSerializer`

```csharp
public static class LiteSerializer
{
    public static void Register<T>(IFormatter<T> formatter);
    public static byte[] Serialize<T>(in T value);
    public static int Serialize<T>(in T value, byte[] buffer);
    public static int Serialize<T>(in T value, Span<byte> buffer);
    // Deserialization overloads are also exposed for concrete serializer flows.
}
```

`LiteSerializer` is the low-level serializer behind `PacketBase<TSelf>`. Most application code does not call it directly unless it needs custom formatter registration or serialization outside packet types.

## Related Types

- [Dispatch Runtime](/docs/api-reference/dispatch-runtime)
- [SDK Sessions](/docs/api-reference/sdk-sessions)
