# 🛰️ Nalix AI Skill — Protocol Design & Versioning

This skill provides guidance on designing efficient, extensible, and secure network protocols using the Nalix framework.

---

## 🏗️ Designing a Packet

When creating a new packet, follow these design principles:

### 1. Choice of Layout
- **`SerializeLayout.Explicit`**: Best for high-performance fixed-size packets. Provides full control over field offsets.
- **`SerializeLayout.Sequential`**: Best for simple data objects where performance is less critical than ease of maintenance.

### 2. Member Ordering
- **Place fixed-size primitives first:** Put `int`, `long`, `Guid` at the beginning of the payload.
- **Place dynamic members last:** Put `string`, `byte[]`, or `List<T>` at the end to simplify length calculations and parsing.

### 3. Data Types
- Prefer `int` or `short` over `string` for status codes or enums.
- Use `Bytes32` for hashes instead of `string` or `byte[]` to avoid allocations.

---

## 📜 Versioning & Compatibility

As the protocol evolves, maintaining compatibility between client and server is critical.

### Backward Compatibility (Old client, New server)
- **Adding fields:** Append new fields to the end of the packet. Use `SerializeOrder` values higher than existing ones.
- **Optional fields:** Mark new fields as optional or provide default values in the `Deserialize` logic.

### Forward Compatibility (New client, Old server)
- The old server will ignore extra trailing bytes in the packet if using `LiteSerializer`.
- Ensure new fields don't change the interpretation of old fields.

---

## 🔐 Security in Protocol Design

- **Never trust client-provided lengths:** Always validate length prefixes for arrays and strings against reasonable maximums.
- **Obfuscate Opcodes:** Use non-sequential or random-looking Opcodes to make it harder for attackers to map out the protocol.
- **Mandatory Encryption:** Mark sensitive packets with `[PacketEncryption(true)]`.

---

## ⚡ Performance Optimization

- **Bit-Packing:** For boolean flags or small ranges, pack multiple values into a single `byte` or `int` using bitmasks.
- **Avoid Nested Objects:** Deeply nested objects increase serialization complexity. Flatten the data structure where possible.
- **Pre-calculate Length:** If the packet length is constant, hardcode it in the `Length` property to avoid runtime calculations.

---

## 🛡️ Common Pitfalls

- **Payload Overlap:** Forgetting that the first bytes are reserved for the header (NALIX022).
- **String Bloat:** Using `string` for identifiers that could be `int` or `Snowflake`.
- **Large Arrays:** Sending large arrays (e.g., 10,000 items) in a single packet can cause latency spikes and memory pressure. Use paging or multiple packets.
