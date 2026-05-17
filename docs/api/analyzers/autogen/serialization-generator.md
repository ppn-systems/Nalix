# Serialization Generator
 
The Serialization Formatter Generator automates the creation of high-performance `IFormatter<T>` and `IFillableFormatter<T>` implementations at compile time.
 
## Source Mapping
 
- `src/Nalix.Analyzers.Generators/SerializeFormatterGenerator.cs`
 
## Overview
 
Traditional serialization often relies on reflection or runtime IL emission, which can be slow and problematic for Ahead-of-Time (AOT) compilation. Nalix uses this source generator to produce static C# code that handles serialization directly, ensuring maximum performance and full compatibility with AOT environments like Unity (IL2CPP) or NativeAOT.
 
## How it works
 
1. **Attribute Trigger**: The generator looks for classes or structs marked with the `[GenerateFormatter]` attribute.
2. **Member Discovery**: It analyzes the type and its inheritance hierarchy to find serializable members.
3. **Attribute-Driven Ordering**: It respects attributes like `[SerializeOrder]`. 
4. **Inheritance & Headers**: Since `PacketBase` (via `FrameBase`) defines the `Header` with order 0, it is naturally serialized/deserialized first.
5. **Code Generation**: It generates a partial class containing optimized `Serialize`, `Deserialize`, and `Fill` methods.
6. **Registration**: It uses a `[ModuleInitializer]` to automatically register the generated formatter with the `LiteSerializer` at startup.
 
## Features
 
- **Zero Reflection**: Uses direct property and field access for maximum speed.
- **Pooling Integration**: For reference types, the generated code uses the `Create()` pooling pattern to avoid GC allocations during deserialization.
- **Fused Filling**: Implements `IFillableFormatter<T>` which allows updating an existing instance (crucial for zero-allocation hot paths).
- **AOT Optimized**: Generated code uses attributes like `[SkipLocalsInit]` and `AggressiveInlining` to assist the JIT/AOT compiler.
 
## Real-world Generated Example
 
### 1. Source Packet Definition
 
When you define a packet class like the one below, the generator detects the `[GenerateFormatter]` attribute and the inheritance from `PacketBase<T>`:
 
```csharp
[GenerateFormatter]
[SerializePackable(SerializeLayout.Sequential)]
internal sealed partial class DynamicHintStringPacket : PacketBase<DynamicHintStringPacket>
{
    [SerializeDynamicSize(64)]
    public string Message { get; set; } = string.Empty;
}
```
 
### 2. Generated Formatter Code
 
The following code is what the generator produces. Notice how the **Header** (defined in the base class with order 0) is handled first automatically:
 
```csharp
[StackTraceHidden]
[DebuggerStepThrough]
[SkipLocalsInit]
internal sealed class DynamicHintStringPacketFormatter : 
    IFillableFormatter<DynamicHintStringPacket>, 
    IFormatter<DynamicHintStringPacket>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Serialize(ref DataWriter writer, in DynamicHintStringPacket value)
    {
        ArgumentNullException.ThrowIfNull(value, "value");
        
        // Header from base class (Order 0) is serialized first
        (ref writer).WriteUnmanaged(value.Header);
        
        IFormatter<string> formatter = FormatterProvider.Get<string>();
        string message = value.Message;
        formatter.Serialize(ref writer, in message);
    }
 
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public DynamicHintStringPacket Deserialize(ref DataReader reader)
    {
        // Zero-allocation: Rents an instance from the packet pool
        DynamicHintStringPacket instance = PacketBase<DynamicHintStringPacket>.Create();
        this.Fill(ref reader, instance);
        return instance;
    }
 
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Fill(ref DataReader reader, DynamicHintStringPacket value)
    {
        ArgumentNullException.ThrowIfNull(value, "value");
        
        // Header from base class (Order 0) is deserialized first
        value.Header = (ref reader).ReadUnmanaged<PacketHeader>();
        
        // Complex type (string) is handled via FormatterProvider
        value.Message = FormatterProvider.Get<string>().Deserialize(ref reader);
    }
 
    // Explicit interface implementation for generic usage
    void IFormatter<DynamicHintStringPacket>.Serialize(ref DataWriter writer, in DynamicHintStringPacket value)
    {
        this.Serialize(ref writer, in value);
    }
}
```
 
## Related APIs
 
- [Serialization Basics](../../codec/serialization/serialization-basics.md)
- [Packet Serialization](../../codec/serialization/packet-serialization.md)
- [Analyzers Overview](../index.md)
- [Packet Registry](../../codec/packets/packet-registry.md)
