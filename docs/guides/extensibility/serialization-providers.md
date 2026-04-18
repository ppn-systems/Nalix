# Custom Serialization Provider

This guide shows how to register a custom serialization provider in `Nalix.Framework` using `IFormatter<T>` and `LiteSerializer.Register(...)`.

## When to use a custom formatter

Use a custom formatter when:

- you need strict wire compatibility with an existing protocol
- you want custom field ordering or compact encoding
- automatic formatter generation does not match your model semantics
- your model is immutable and needs explicit reconstruction logic

## Step 1. Implement `IFormatter<T>`

```csharp
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Serialization;

public sealed class UserProfileFormatter : IFormatter<UserProfile>
{
    public void Serialize(ref DataWriter writer, UserProfile value)
    {
        writer.WriteInt32(value.Id);
        writer.WriteString(value.Name);
        writer.WriteUInt16(value.Level);
    }

    public UserProfile Deserialize(ref DataReader reader)
    {
        return new UserProfile(
            id: reader.ReadInt32(),
            name: reader.ReadString(),
            level: reader.ReadUInt16());
    }
}
```

## Step 2. Register at startup

Register once during application startup, before hot-path serialization begins.

```csharp
using Nalix.Framework.Serialization;

LiteSerializer.Register(new UserProfileFormatter());
```

After registration, Nalix serialization calls for `UserProfile` go through your formatter:

```csharp
byte[] payload = LiteSerializer.Serialize(profile);
UserProfile restored = LiteSerializer.Deserialize<UserProfile>(payload, out int bytesRead);
```

## Integration notes

- Registration is global for the current process (`FormatterProvider`-backed).
- Register early to avoid mixed behavior in long-lived services.
- Keep `Serialize`/`Deserialize` symmetric and deterministic.
- Prefer allocation-light logic inside formatter methods.

## Validation checklist

- round-trip test: `model -> bytes -> model`
- malformed input handling in `Deserialize`
- versioning strategy if fields evolve
- cross-service compatibility if multiple apps share the same wire format

## Related pages

- [Serialization Basics](../../api/framework/serialization/serialization-basics.md)
- [Serialization Concept](../../concepts/fundamentals/packet-system.md)
- [Packet Lifecycle](../../concepts/fundamentals/packet-lifecycle.md)
