# Auto Generation
 
`Nalix.Analyzers.Generators` provides Roslyn Source Generators that automate the creation of high-performance networking and serialization code at compile time. This ensures type safety, AOT compatibility, and zero-allocation performance without manual boilerplate.
 
## Overview
 
Source generation is a core pillar of the Nalix architecture. By moving logic from runtime reflection to compile-time code generation, Nalix achieves several critical goals:
 
- **Maximum Performance**: Generated code is static C# that can be fully optimized by the JIT or AOT compiler.
- **Zero-Allocation**: Generated formatters and registries are designed to work with the Nalix pooling system.
- **AOT Compatibility**: Since no reflection-emit is used, Nalix runs perfectly on platforms with strict AOT requirements.
- **Developer Experience**: Common patterns like packet registration and serialization formatting are handled automatically.
 
## Generators
 
The following generators are included in the `Nalix.Analyzers.Generators` project:
 
- [**Serialization Generator**](./serialization-generator.md): Creates optimized binary formatters for packets and data structures.
- [**Packet Registry Generator**](./packet-registry-generator.md): Automates packet discovery and provides O(1) dispatch tables.
- [**Packet Schema Generator**](./packet-schema-generator.md): Generates binary schema metadata for diagnostics and cross-language support.
- [**Configuration Generator**](./configuration-generator.md): Provides AOT-safe binding for `.ini` configuration files.
 
## Related APIs
 
- [Analyzers Overview](../index.md)
- [Diagnostic Codes](../diagnostic-codes.md)
- [Serialization Basics](../../codec/serialization/serialization-basics.md)
- [Packet Registry](../../codec/packets/packet-registry.md)
