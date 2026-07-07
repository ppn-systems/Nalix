# Packet Schema Generator
 
The Packet Schema Generator produces binary schema metadata for all packet types, facilitating validation and cross-language compatibility.
 
## Source Mapping
 
- `analyzers/Nalix.Analyzers.Generators/PacketSchemaGenerator.cs`
 
## Overview
 
Nalix packets need a way to describe their layout so that external tools, diagnostic dashboards, or client-side code generators can understand the binary format. This generator extracts the structural information of each packet and embeds it into the assembly.
 
## How it works
 
1. **Structural Analysis**: Inspects each packet type to determine the name, type, and byte offset of every serializable field.
2. **Metadata Generation**: Creates a `PacketSchema` object for each packet, containing a collection of `FieldDescriptor` entries.
3. **Registry Integration**: Registers these schemas with the `PacketRegistry` so they can be queried at runtime.
 
## Features
 
- **Automatic Offsets**: Calculates exact byte offsets based on the chosen `SerializeLayout` (Sequential or Explicit).
- **Type Information**: Records the binary primitive types or complex nested types for each field.
- **Diagnostic Support**: Powers the `GenerateReport()` functionality used in debugging and logging.
- **Schema Export**: Provides the raw data needed to export schemas to JSON or other IDL formats.
 
## Usage Example
 
While you rarely interact with the generator directly, you can access the generated schema at runtime:
 
```csharp
if (PacketRegistry.TryGetSchema(opcode, out var schema))
{
    foreach (var field in schema.Fields)
    {
        Console.WriteLine($"{field.Name} at offset {field.Offset} ({field.Type})");
    }
}
```
 
## Related APIs
 
- [Packet Registry](../../codec/packets/packet-registry.md)
- [Binary Specification](../../../concepts/internals/binary-spec.md)
- [Serialization Basics](../../codec/serialization/serialization-basics.md)
- [Analyzers](../index.md)
