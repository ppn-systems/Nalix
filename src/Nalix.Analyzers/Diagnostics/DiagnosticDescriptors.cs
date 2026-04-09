// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.CodeAnalysis;

namespace Nalix.Analyzers.Diagnostics;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor DuplicateControllerOpcode = new(
        id: "NALIX001",
        title: "Packet controller contains duplicate PacketOpcode",
        messageFormat: "Handler method '{0}' uses PacketOpcode 0x{1:X4}, which is already used by another handler in the same [PacketController]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix controller dispatch relies on unique opcodes per controller.");

    public static readonly DiagnosticDescriptor ControllerMethodRequiresOpcode = new(
        id: "NALIX002",
        title: "Packet controller handler should declare PacketOpcode",
        messageFormat: "Handler method '{0}' in a [PacketController] matches Nalix handler patterns and should be annotated with [PacketOpcode(...)]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet controller methods are only discovered when PacketOpcode is present.");

    public static readonly DiagnosticDescriptor InvalidControllerHandlerSignature = new(
        id: "NALIX003",
        title: "Packet controller handler has unsupported signature",
        messageFormat: "Handler method '{0}' has unsupported Nalix signature. Supported forms: (TPacket, IConnection), (TPacket, IConnection, CancellationToken), (PacketContext<T>), (PacketContext<T>, CancellationToken).",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet handlers must follow one of the signatures supported by PacketHandlerCompiler.");

    public static readonly DiagnosticDescriptor PacketContextTypeMismatch = new(
        id: "NALIX004",
        title: "PacketContext<T> does not match dispatch packet type",
        messageFormat: "Handler method '{0}' uses PacketContext<{1}>, but the dispatcher packet type is '{2}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix dispatch expects PacketContext<T> to use the same TPacket as PacketDispatchOptions<TPacket>.");

    public static readonly DiagnosticDescriptor HandlerPacketTypeMismatch = new(
        id: "NALIX005",
        title: "Registered controller handler packet type does not match dispatcher type",
        messageFormat: "Controller '{0}' contains handler '{1}' that expects packet type '{2}', which is not assignable to dispatcher packet type '{3}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix dispatch registration should use controllers whose handler packet types are compatible with PacketDispatchOptions<TPacket>.");

    public static readonly DiagnosticDescriptor MiddlewareTypeMismatch = new(
        id: "NALIX006",
        title: "Registered middleware type does not match dispatcher packet type",
        messageFormat: "Middleware type '{0}' is not assignable to IPacketMiddleware<{1}> for this PacketDispatchOptions<{1}>",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet middleware should be registered against a compatible PacketDispatchOptions<TPacket>.");

    public static readonly DiagnosticDescriptor BufferMiddlewareShouldNotUseStageAttribute = new(
        id: "NALIX007",
        title: "Network buffer middleware ignores MiddlewareStageAttribute",
        messageFormat: "Buffer middleware type '{0}' implements INetworkBufferMiddleware, but MiddlewareStageAttribute has no effect in NetworkBufferMiddlewarePipeline",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix network buffer middleware ordering uses MiddlewareOrderAttribute only.");

    public static readonly DiagnosticDescriptor ControllerMissingPacketControllerAttribute = new(
        id: "NALIX008",
        title: "Registered handler controller is missing PacketController",
        messageFormat: "Controller type '{0}' is registered with WithHandler, but it is missing [PacketController]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix requires PacketControllerAttribute on controller types registered through PacketDispatchOptions.WithHandler.");

    public static readonly DiagnosticDescriptor PacketRegistryPacketMissingDeserializer = new(
        id: "NALIX009",
        title: "Registered packet type is missing static Deserialize",
        messageFormat: "Packet type '{0}' is registered with PacketRegistryFactory.RegisterPacket, but it does not implement IPacketDeserializer<{0}>",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet registry only binds packet types that expose the required static Deserialize(ReadOnlySpan<byte>) member.");

    public static readonly DiagnosticDescriptor PacketBaseSelfTypeMismatch = new(
        id: "NALIX010",
        title: "PacketBase<TSelf> should use the containing packet type",
        messageFormat: "Packet type '{0}' inherits from PacketBase<{1}> but should inherit from PacketBase<{0}>",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet types should use themselves as the TSelf argument when inheriting from PacketBase<TSelf>.");

    public static readonly DiagnosticDescriptor PacketDeserializerSelfTypeMismatch = new(
        id: "NALIX011",
        title: "IPacketDeserializer<T> should use the containing packet type",
        messageFormat: "Packet type '{0}' implements IPacketDeserializer<{1}> but should implement IPacketDeserializer<{0}>",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet deserializer contracts should target the containing packet type for consistent registry binding.");

    public static readonly DiagnosticDescriptor PacketBaseMissingDeserializeMethod = new(
        id: "NALIX012",
        title: "PacketBase packet should expose static Deserialize",
        messageFormat: "Packet type '{0}' inherits from PacketBase<{0}> but does not expose 'public static new {0} Deserialize(ReadOnlySpan<byte>)'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet types built on PacketBase<TSelf> should expose a static Deserialize(ReadOnlySpan<byte>) helper for registry scanning and discoverability.");

    public static readonly DiagnosticDescriptor ExplicitSerializationMemberMissingOrder = new(
        id: "NALIX013",
        title: "Explicit serialization member should declare SerializeOrder",
        messageFormat: "Member '{0}' belongs to a [SerializePackable(SerializeLayout.Explicit)] type and should declare [SerializeOrder(...)] or [SerializeIgnore]",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Explicit serialization layouts should assign a stable SerializeOrder to every serialized member.");

    public static readonly DiagnosticDescriptor DuplicateSerializeOrder = new(
        id: "NALIX014",
        title: "SerializeOrder value is duplicated",
        messageFormat: "Member '{0}' uses SerializeOrder {1}, which is already used by another member in type '{2}'",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "SerializeOrder values should be unique within a serializable type.");

    public static readonly DiagnosticDescriptor SerializeIgnoreConflictsWithOrder = new(
        id: "NALIX015",
        title: "SerializeIgnore conflicts with SerializeOrder",
        messageFormat: "Member '{0}' declares both [SerializeIgnore] and [SerializeOrder(...)]",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A member cannot be both ignored and explicitly ordered for serialization.");

    public static readonly DiagnosticDescriptor SerializeDynamicSizeOnFixedMember = new(
        id: "NALIX016",
        title: "SerializeDynamicSize is unnecessary on fixed-size member",
        messageFormat: "Member '{0}' uses [SerializeDynamicSize] but type '{1}' is fixed-size",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "SerializeDynamicSize should be used only for variable-size serialization members such as strings, arrays, or variable-size collections.");

    public static readonly DiagnosticDescriptor PacketDeserializeSignatureInvalid = new(
        id: "NALIX017",
        title: "Packet Deserialize signature is invalid",
        messageFormat: "Packet type '{0}' exposes Deserialize, but it should be 'public static new {0} Deserialize(ReadOnlySpan<byte>)'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet types should expose a public static Deserialize(ReadOnlySpan<byte>) helper with the packet type as return value.");

    public static readonly DiagnosticDescriptor PacketRegistryPacketMustBeConcrete = new(
        id: "NALIX018",
        title: "Registered packet type must be concrete",
        messageFormat: "Packet type '{0}' is registered with PacketRegistryFactory.RegisterPacket, but it is not a concrete non-generic type",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet registry should register only concrete, non-abstract, non-generic packet types.");

    public static readonly DiagnosticDescriptor BufferMiddlewareRegistrationTypeMismatch = new(
        id: "NALIX019",
        title: "Registered buffer middleware does not implement INetworkBufferMiddleware",
        messageFormat: "Type '{0}' is passed to WithBufferMiddleware, but it is not assignable to INetworkBufferMiddleware",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix buffer middleware registration should pass a type implementing INetworkBufferMiddleware.");

    public static readonly DiagnosticDescriptor ResetForPoolShouldCallBase = new(
        id: "NALIX020",
        title: "Packet ResetForPool should call base.ResetForPool",
        messageFormat: "Packet type '{0}' overrides ResetForPool but does not call base.ResetForPool()",
        category: "Lifecycle",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packets derived from PacketBase<TSelf> should call base.ResetForPool() to restore header fields and default metadata.");

    public static readonly DiagnosticDescriptor NegativeSerializeOrder = new(
        id: "NALIX021",
        title: "SerializeOrder should not be negative",
        messageFormat: "Member '{0}' uses negative SerializeOrder {1}",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix explicit serialization layout should not use negative SerializeOrder values.");

    public static readonly DiagnosticDescriptor PacketMemberOverlapsHeaderRegion = new(
        id: "NALIX022",
        title: "Packet member SerializeOrder overlaps packet header region",
        messageFormat: "Member '{0}' uses SerializeOrder {1}, which overlaps the reserved packet header region ending at {2}",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Packet payload members on PacketBase-derived types should start at or after PacketHeaderOffset.Region.");

    public static readonly DiagnosticDescriptor UnsupportedConfigurationPropertyType = new(
        id: "NALIX023",
        title: "Configuration property type is not supported",
        messageFormat: "Property '{0}' on ConfigurationLoader type '{1}' uses unsupported configuration type '{2}'. Supported types: primitives, string, DateTime, TimeSpan, Guid, and enums. Add [ConfiguredIgnore] to skip it.",
        category: "Configuration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix ConfigurationLoader binds only supported scalar, Guid, TimeSpan, DateTime, and enum property types. Unsupported properties should be marked with ConfiguredIgnoreAttribute.");

    public static readonly DiagnosticDescriptor ConfigurationPropertyNotBindable = new(
        id: "NALIX024",
        title: "Configuration property is not bindable",
        messageFormat: "Property '{0}' on ConfigurationLoader type '{1}' will not be bound because it {2}. Make the setter public or add [ConfiguredIgnore] to document the intent.",
        category: "Configuration",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix ConfigurationLoader binds only public instance properties with public setters. Non-bindable properties should either expose a public setter or be marked with ConfiguredIgnoreAttribute.");

    public static readonly DiagnosticDescriptor MetadataProviderClearsOpcode = new(
        id: "NALIX025",
        title: "Packet metadata provider clears Opcode",
        messageFormat: "Metadata provider '{0}' assigns builder.Opcode = null inside Populate(...), which will make PacketMetadataBuilder.Build() fail",
        category: "Routing",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix packet metadata providers should not clear builder.Opcode because packet metadata requires a non-null opcode.");

    public static readonly DiagnosticDescriptor MetadataProviderOverwritesOpcodeWithoutGuard = new(
        id: "NALIX026",
        title: "Packet metadata provider overwrites Opcode without guard",
        messageFormat: "Metadata provider '{0}' assigns builder.Opcode in Populate(...) without checking the current value first",
        category: "Routing",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix packet metadata providers usually should augment metadata instead of unconditionally replacing an existing PacketOpcodeAttribute.");

    public static readonly DiagnosticDescriptor RequestOptionsRetryCountNegative = new(
        id: "NALIX027",
        title: "RequestOptions RetryCount should not be negative",
        messageFormat: "RequestOptions RetryCount is set to negative value {0}",
        category: "SDK",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix RequestOptions retry count must be zero or greater.");

    public static readonly DiagnosticDescriptor RequestOptionsTimeoutNegative = new(
        id: "NALIX028",
        title: "RequestOptions TimeoutMs should not be negative",
        messageFormat: "RequestOptions TimeoutMs is set to negative value {0}",
        category: "SDK",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix RequestOptions timeout must be zero or greater. Use 0 to wait indefinitely.");

    public static readonly DiagnosticDescriptor RequestEncryptRequiresTcpSession = new(
        id: "NALIX029",
        title: "Encrypted RequestAsync requires TcpSession",
        messageFormat: "RequestAsync uses RequestOptions.Encrypt=true, but client type '{0}' is not assignable to TcpSession",
        category: "SDK",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix encrypted RequestAsync overload requires the client to be a TcpSession.");

    public static readonly DiagnosticDescriptor PacketMiddlewareMissingOrder = new(
        id: "NALIX030",
        title: "Packet middleware should declare MiddlewareOrder",
        messageFormat: "Packet middleware type '{0}' does not declare [MiddlewareOrder(...)]",
        category: "Middleware",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix packet middleware ordering is more predictable when each middleware declares MiddlewareOrderAttribute explicitly.");

    public static readonly DiagnosticDescriptor BufferMiddlewareMissingOrder = new(
        id: "NALIX031",
        title: "Buffer middleware should declare MiddlewareOrder",
        messageFormat: "Buffer middleware type '{0}' does not declare [MiddlewareOrder(...)]",
        category: "Middleware",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix network buffer middleware ordering is determined solely by MiddlewareOrderAttribute.");

    public static readonly DiagnosticDescriptor InboundMiddlewareAlwaysExecuteIgnored = new(
        id: "NALIX032",
        title: "Inbound middleware ignores AlwaysExecute",
        messageFormat: "Middleware type '{0}' sets MiddlewareStage(Inbound, AlwaysExecute = true), but AlwaysExecute only affects outbound execution",
        category: "Middleware",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix AlwaysExecute only changes outbound middleware behavior. It has no effect on inbound-only middleware.");

    public static readonly DiagnosticDescriptor MiddlewareRegistrationDuplicateOrder = new(
        id: "NALIX033",
        title: "Registered middleware shares MiddlewareOrder with another middleware in the same chain",
        messageFormat: "Middleware type '{0}' uses MiddlewareOrder {1}, which is already used by '{2}' in the same registration chain",
        category: "Middleware",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix middleware registration chains are easier to reason about when MiddlewareOrder values are unique within the same builder chain.");

    public static readonly DiagnosticDescriptor SerializeHeaderConflictsWithOrder = new(
        id: "NALIX034",
        title: "SerializeHeader conflicts with SerializeOrder",
        messageFormat: "Member '{0}' declares both [SerializeHeader(...)] and [SerializeOrder(...)]",
        category: "Serialization",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix serialization members should not declare both SerializeHeader and SerializeOrder. SerializeHeader is a specialized form of ordering for header fields.");

    public static readonly DiagnosticDescriptor ReservedOpCodeRange = new(
        id: "NALIX035",
        title: "PacketOpcode is in reserved range",
        messageFormat: "Handler method '{0}' uses OpCode 0x{1:X4}, which is in the reserved system range (0x0000 - 0x00FF)",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "OpCodes between 0x0000 and 0x00FF are reserved for internal Nalix system packets and should not be used for application handlers.");

    public static readonly DiagnosticDescriptor GlobalDuplicateOpcode = new(
        id: "NALIX036",
        title: "PacketOpcode is already used in a different controller",
        messageFormat: "Handler method '{0}' uses OpCode 0x{1:X4}, which is already defined by '{2}' in controller '{3}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nalix dispatch requires unique opcodes across all registered controllers in a compilation unit.");

    public static readonly DiagnosticDescriptor AllocationInHotPath = new(
        id: "NALIX037",
        title: "Potential allocation in hot path",
        messageFormat: "Method '{0}' is a high-frequency Nalix hot path. Allocating '{1}' via 'new' may impact performance; consider using ObjectPoolManager or recycling instances.",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Nalix middleware and handlers are executed frequently. Avoiding allocations in these paths is critical for low-latency networking.");

    public static readonly DiagnosticDescriptor OpCodeDocMismatch = new(
        id: "NALIX038",
        title: "OpCode in documentation does not match attribute",
        messageFormat: "Documentation for '{0}' mentions OpCode 0x{1:X4}, but the attribute value is 0x{2:X4}",
        category: "Documentation",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "XML documentation summaries should be synchronized with PacketOpcode values for clarity and automated protocol documentation.");

    public static readonly DiagnosticDescriptor PotentialBufferLeaseLeak = new(
        id: "NALIX039",
        title: "Potential IBufferLease leak",
        messageFormat: "Local variable or parameter '{0}' of type IBufferLease might be leaked; ensure it is disposed of exactly once on all code paths",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IBufferLease represents a pooled resource that must be returned to the pool exactly once via Dispose() or an explicit return call.");
}
