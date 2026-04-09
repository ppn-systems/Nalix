# 📦 Nalix AI Skill — Packet Registry & Auto-Discovery

This skill explains how Nalix discovers, registers, and manages packet types across the entire ecosystem.

---

## 🏗️ The Registry System

The `PacketRegistry` is a high-speed lookup table used to resolve metadata and deserialization logic for every opcode.

### `PacketRegistryFactory`
- **Auto-Discovery:** Scans assemblies for types marked with `[Packet]` or inheriting from `PacketBase<T>`.
- **Validation:** Checks for duplicate opcodes (NALIX001) and reserved ranges (NALIX035) during the build phase.
- **Bootstrapping:** Generates a "Frozen" registry at startup for O(1) runtime lookups.

---

## 📜 Opcode Management

- **Unique IDs:** Every packet MUST have a unique `ushort` Opcode.
- **Reserved Range:** `0x0000 - 0x00FF` are internal Nalix system packets.
- **Custom Range:** `0x0100 - 0xFFFF` are available for application packets.

---

## ⚡ Performance Optimization

- **Static Function Pointers:** Instead of using slow reflection or standard delegates, the registry often stores `delegate* managed` (unsafe function pointers) for the `Deserialize` method.
- **Perfect Hashing:** For small sets of opcodes, the registry can use perfect hashing to achieve zero-collision lookups.
- **Pre-allocation:** The registry pre-calculates the size and layout of every packet to speed up memory renting from the `SlabPoolManager`.

---

## 🛠️ Advanced Registration

### Manual Registration
If auto-discovery is disabled, you can manually register packets:
```csharp
registry.Register<MyPacket>(0x1234);
```

### Metadata Augmentation
Use `IPacketMetadataProvider` to add extra information to packets globally (e.g., custom flags or priority levels).

---

## 🛡️ Common Pitfalls

- **Assembly Scanning Slowness:** Scanning too many assemblies at startup can increase boot time. Use `AddPacket Namespace` or `AddPacket<TMarker>` to limit the scope.
- **Deserialization Signature:** Every packet MUST have `public static T Deserialize(ReadOnlySpan<byte>)`. Forgetting this will cause a registration error (NALIX011).
- **Opcode Overlap:** Using the same opcode for two different packets in different assemblies will still cause a collision at runtime.
