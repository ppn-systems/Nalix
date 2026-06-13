# Packet Handler Generator

The `PacketHandlerGenerator` is a Roslyn incremental source generator that produces zero-allocation dispatch compilers for classes annotated with `[PacketController]`. It replaces the runtime reflection-based handler compilation model with compile-time code generation.

## Source Mapping

- `analyzers/Nalix.Analyzers.Generators/PacketHandlerGenerator.cs`

## What It Does

At compile time, the generator:

1. Scans all classes annotated with `[PacketController]` across the compilation.
2. For each controller, inspects methods annotated with `[PacketOpcode]`.
3. Emits an `IPacketHandlerCompiler` implementation that registers each handler via `IPacketHandlerBuilder<TPacket>.RegisterHandler(...)`.
4. Emits a `[ModuleInitializer]` that registers the compiler with `PacketHandlerRegistry.Register(...)` at assembly load time.

## Generated Output

For a controller like:

```csharp
[PacketController("SampleHandlers")]
public sealed class SampleHandlers
{
    [PacketOpcode(0x1001)]
    public ValueTask<LoginResponse> HandleLogin(IPacketContext<LoginRequest> context)
        => ValueTask.FromResult(new LoginResponse());
}
```

The generator produces (conceptually):

```csharp
[ModuleInitializer]
internal static void __RegisterSampleHandlers()
{
    PacketHandlerRegistry.Register(
        typeof(SampleHandlers),
        new SampleHandlers_Compiler());
}

internal sealed class SampleHandlers_Compiler : IPacketHandlerCompiler
{
    public void InitializeDependencies() { }

    public void Build<TPacket>(IPacketHandlerBuilder<TPacket> builder, Func<object> factory)
        where TPacket : IPacket
    {
        var instance = (SampleHandlers)factory();
        builder.RegisterHandler(
            opCode: 0x1001,
            metadata: new PacketMetadata(/* attributes */),
            methodName: "HandleLogin",
            instance: instance,
            returnType: typeof(ValueTask<LoginResponse>),
            expectedPacketType: typeof(LoginRequest),
            invoker: (inst, ctx) => /* compiled delegate */);
    }
}
```

## Runtime Integration

At runtime, `PacketDispatchOptions.WithHandler<TController>()` calls `PacketHandlerRegistry.TryBuildHandlers(...)`, which finds the generated compiler and invokes `Build(...)` to register all handlers.

## Supported Handler Signatures

The generator supports the following method signature patterns:

| Style | Signature |
| --- | --- |
| Context | `(IPacketContext<T> context)` |
| Context + Token | `(IPacketContext<T> context, CancellationToken ct)` |
| Legacy | `(T packet, IConnection connection)` |
| Legacy + Token | `(T packet, IConnection connection, CancellationToken ct)` |
| Raw Memory | `(ReadOnlyMemory<byte> raw, IConnection connection)` |

Return types: `void`, `Task`, `ValueTask`, `T`, `Task<T>`, `ValueTask<T>`.

## Diagnostic

If a handler method has an unsupported signature, the generator emits `NALIX003`.

## Related APIs

- [Auto Generation Overview](./index.md)
- [Diagnostic Codes](../diagnostic-codes.md)
- [Packet Attributes](../../abstractions/packet-attributes.md)
