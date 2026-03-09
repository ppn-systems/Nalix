// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets.Abstractions;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.Injection;
using Nalix.Shared.Frames.Controls;
using Nalix.Shared.Memory.Pooling;
using Nalix.Shared.Registry;
using Xunit;

namespace Nalix.Shared.Tests.Registry;

/// <summary>
/// <para>
/// Integration tests for <see cref="IPacketCatalog"/> covering the full round-trip:
/// <c>Serialize → TryDeserialize</c> via <see cref="PacketRegistry"/>.
/// </para>
/// <para>
/// Setup:
///   Each test class instance gets a fresh <see cref="PacketRegistry"/> built from
///   <see cref="PacketRegistryFactory"/> (same path as production).
///   <see cref="ObjectPoolManager"/> is registered in <see cref="InstanceManager"/>
///   so that <see cref="PacketBase{TSelf}.Deserialize"/> can pull pooled instances.
/// </para>
/// </summary>
public sealed class PacketCatalogTests : System.IDisposable
{
    #region Setup

    private readonly IPacketCatalog _catalog;

    public PacketCatalogTests()
    {
        // Register ObjectPoolManager so PacketBase<TSelf>.Deserialize can use the pool.
        InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

        // Build catalog the same way production code does.
        _catalog = new PacketRegistry(factory =>
        {
            factory.RegisterPacket<Control>();
            factory.RegisterPacket<Handshake>();
            factory.RegisterPacket<Directive>();
        });
    }

    public void Dispose()
    {
        // Nothing to tear down — InstanceManager is process-scoped.
        // ObjectPoolManager cleans up pooled instances automatically.
    }

    #endregion Setup

    [Fact]
    public void Debug_RegistryBuildDirect()
    {
        // Build registry thủ công, không qua PacketRegistry constructor
        var factory = new PacketRegistryFactory();
        factory.RegisterPacket<Control>();

        var registry = factory.CreateCatalog();

        System.UInt32 key = PacketRegistryFactory.Compute(typeof(Control));

        System.Boolean hasKey = registry.TryGetDeserializer(key, out _);

        Assert.True(hasKey, $"Key 0x{key:X8} không có trong registry sau khi build");
    }

    [Fact]
    public void Debug_TryDeserialize_Control()
    {
        var original = new Control();
        original.Initialize(0x0001, ControlType.PING, sequenceId: 42);

        System.Byte[] bytes = original.Serialize();

        // Xem bytes có đủ dài không
        Assert.True(bytes.Length >= Nalix.Common.Networking.Packets.PacketConstants.HeaderSize,
            $"Bytes quá ngắn: {bytes.Length}");

        // Xem MagicNumber trong bytes
        System.UInt32 magicInBytes = System.Buffers.Binary.BinaryPrimitives
                                           .ReadUInt32LittleEndian(bytes);

        // Xem registry có key này không — gọi TryGetDeserializer trực tiếp
        System.Boolean hasKey = _catalog.TryGetDeserializer(magicInBytes, out var des);

        Assert.True(hasKey,
            $"Registry không có key=0x{magicInBytes:X8}. " +
            $"AutoMagic=0x{PacketRegistryFactory.Compute(typeof(Control)):X8}");

        // Cuối cùng mới gọi TryDeserialize
        System.Boolean found = _catalog.TryDeserialize(bytes, out IPacket packet);
        Assert.True(found, $"TryDeserialize fail dù key tồn tại");
    }

    // -------------------------------------------------------------------------
    // Control (fixed-size packet)
    // -------------------------------------------------------------------------

    [Fact]
    public void Control_SerializeThenDeserialize_ReturnsSamePacket()
    {
        // Arrange
        var original = new Control();
        original.Initialize(
            opCode: 0x0001,
            type: ControlType.PING,
            sequenceId: 42,
            reasonCode: ProtocolReason.NONE,
            transport: ProtocolType.TCP);

        // Act — serialize to byte[]
        System.Byte[] bytes = original.Serialize();

        // Assert catalog can identify and deserialize it
        System.Boolean found = _catalog.TryDeserialize(bytes, out IPacket packet);

        Assert.True(found);
        Assert.NotNull(packet);

        var result = Assert.IsType<Control>(packet);
        Assert.Equal(original.OpCode, result.OpCode);
        Assert.Equal(original.MagicNumber, result.MagicNumber);
        Assert.Equal(original.SequenceId, result.SequenceId);
        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.Reason, result.Reason);
        Assert.Equal(original.Protocol, result.Protocol);
        Assert.Equal(original.Priority, result.Priority);
    }

    [Fact]
    public void Control_MagicNumber_IsConsistentAcrossInstances()
    {
        // AutoMagic must be deterministic — same type always yields same hash.
        var a = new Control();
        var b = new Control();
        Assert.Equal(a.MagicNumber, b.MagicNumber);
    }

    [Fact]
    public void Control_AfterResetForPool_MagicNumberPreserved()
    {
        var packet = new Control();
        System.UInt32 magicBefore = packet.MagicNumber;

        packet.ResetForPool();

        Assert.Equal(magicBefore, packet.MagicNumber);
    }

    [Fact]
    public void Control_AfterResetForPool_CanRoundTripAgain()
    {
        // Simulate: get from pool → use → return → get again → use.
        var packet = new Control();
        packet.Initialize(0x0002, ControlType.PONG, sequenceId: 99);
        packet.ResetForPool();

        // Re-initialize after reset
        packet.Initialize(0x0003, ControlType.PING, sequenceId: 7);
        System.Byte[] bytes = packet.Serialize();

        System.Boolean found = _catalog.TryDeserialize(bytes, out IPacket result);

        Assert.True(found);
        var control = Assert.IsType<Control>(result);
        Assert.Equal(0x0003, control.OpCode);
        Assert.Equal(7u, control.SequenceId);
        Assert.Equal(ControlType.PING, control.Type);
    }

    // -------------------------------------------------------------------------
    // Handshake (dynamic-size packet — has byte[] Data)
    // -------------------------------------------------------------------------

    [Fact]
    public void Handshake_SerializeThenDeserialize_ReturnsSamePayload()
    {
        // Arrange
        System.Byte[] payload = [0x01, 0x02, 0x03, 0xDE, 0xAD, 0xBE, 0xEF];
        var original = new Handshake(opCode: 0x0010, data: payload, transport: ProtocolType.TCP);

        // Act
        System.Byte[] bytes = original.Serialize();
        System.Boolean found = _catalog.TryDeserialize(bytes, out IPacket packet);

        // Assert
        Assert.True(found);
        var result = Assert.IsType<Handshake>(packet);
        Assert.Equal(original.OpCode, result.OpCode);
        Assert.Equal(original.MagicNumber, result.MagicNumber);
        Assert.Equal(original.Protocol, result.Protocol);
        Assert.Equal(payload, result.Data);
    }

    [Fact]
    public void Debug_MagicNumbers()
    {
        var control = new Control();
        var handshake = new Handshake();
        var directive = new Directive();

        System.UInt32 regControl = PacketRegistryFactory.Compute(typeof(Control));
        System.UInt32 regHandshake = PacketRegistryFactory.Compute(typeof(Handshake));
        System.UInt32 regDirective = PacketRegistryFactory.Compute(typeof(Directive));

        // Xem instance magic vs registry key có khớp không
        Assert.Equal(regControl, control.MagicNumber);
        Assert.Equal(regHandshake, handshake.MagicNumber);
        Assert.Equal(regDirective, directive.MagicNumber);

        // Xem bytes[0..4] sau serialize có đúng MagicNumber không
        System.Byte[] bytes = control.Serialize();
        System.UInt32 magicInBytes = System.Buffers.Binary.BinaryPrimitives
                                           .ReadUInt32LittleEndian(bytes);
        Assert.Equal(regControl, magicInBytes);
    }

    [Fact]
    public void Handshake_EmptyPayload_RoundTripsCorrectly()
    {
        var original = new Handshake(opCode: 0x0011, data: [], transport: ProtocolType.UDP);
        System.Byte[] bytes = original.Serialize();

        System.Boolean found = _catalog.TryDeserialize(bytes, out IPacket packet);

        Assert.True(found);
        var result = Assert.IsType<Handshake>(packet);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void Handshake_Length_ReflectsActualDataSize()
    {
        System.Byte[] payload = new System.Byte[64];
        var packet = new Handshake(opCode: 0x0012, data: payload);

        // HeaderSize + 64 bytes of data
        System.Int32 expected = Nalix.Common.Networking.Packets.PacketConstants.HeaderSize + payload.Length;
        Assert.Equal((System.UInt16)expected, packet.Length);
    }

    [Fact]
    public void Debug_Handshake_HeaderSize()
    {
        // Packet rỗng hoàn toàn — data = []
        var empty = new Handshake(opCode: 0, data: []);
        System.Byte[] bytes = empty.Serialize();

        // Bytes length của packet rỗng = header thuần
        // So sánh với PacketConstants.HeaderSize
        System.Int32 expected = Nalix.Common.Networking.Packets.PacketConstants.HeaderSize;
        System.Int32 actual = bytes.Length;

        Assert.True(
            actual == expected,
            $"Header mismatch. Expected={expected}, Actual={actual}\nHex={System.Convert.ToHexString(bytes)}");
    }

    [Fact]
    public void Debug_Handshake_SerializedFields()
    {
        // Dùng reflection để xem FieldCache đang serialize những field nào
        System.Type fieldCacheType = System.Type.GetType(
            "Nalix.Shared.Serialization.Internal.Reflection.FieldCache`1, Nalix.Shared")!
            .MakeGenericType(typeof(Handshake));

        System.Reflection.FieldInfo metadataField = fieldCacheType
            .GetField("_metadata",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)!;

        System.Array metadata = (System.Array)metadataField.GetValue(null)!;

        System.Text.StringBuilder sb = new();
        foreach (var item in metadata)
        {
            System.Type itemType = item.GetType();
            var name = itemType.GetProperty("Name")?.GetValue(item);
            var order = itemType.GetProperty("Order")?.GetValue(item);
            var type = itemType.GetProperty("FieldType")?.GetValue(item);
            sb.AppendLine($"Order={order} Name={name} Type={type}");
        }

        Assert.Fail(sb.ToString()); // intentional — chỉ để xem output
    }

    // -------------------------------------------------------------------------
    // Directive (fixed-size, multiple fields)
    // -------------------------------------------------------------------------

    [Fact]
    public void Directive_SerializeThenDeserialize_AllFieldsPreserved()
    {
        // Arrange
        var original = new Directive();
        original.Initialize(
            opCode: 0x0020,
            type: ControlType.ACK,
            reason: ProtocolReason.NONE,
            action: ProtocolAdvice.RETRY,
            sequenceId: 123,
            flags: ControlFlags.NONE,
            arg0: 0xDEAD,
            arg1: 0xBEEF,
            arg2: 0xFF);

        // Act
        System.Byte[] bytes = original.Serialize();
        System.Boolean found = _catalog.TryDeserialize(bytes, out IPacket packet);

        // Assert
        Assert.True(found);
        var result = Assert.IsType<Directive>(packet);

        Assert.Equal(original.OpCode, result.OpCode);
        Assert.Equal(original.MagicNumber, result.MagicNumber);
        Assert.Equal(original.SequenceId, result.SequenceId);
        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.Reason, result.Reason);
        Assert.Equal(original.Action, result.Action);
        Assert.Equal(original.Control, result.Control);
        Assert.Equal(original.Arg0, result.Arg0);
        Assert.Equal(original.Arg1, result.Arg1);
        Assert.Equal(original.Arg2, result.Arg2);
        Assert.Equal(original.Priority, result.Priority);
        Assert.Equal(original.Protocol, result.Protocol);
    }

    // -------------------------------------------------------------------------
    // Catalog negative cases
    // -------------------------------------------------------------------------

    [Fact]
    public void TryDeserialize_BufferTooShort_ReturnsFalse()
    {
        // Buffer smaller than HeaderSize must return false, not throw.
        System.Byte[] tooShort = new System.Byte[3];
        System.Boolean found = _catalog.TryDeserialize(tooShort, out IPacket packet);

        Assert.False(found);
        Assert.Null(packet);
    }

    [Fact]
    public void TryDeserialize_UnknownMagicNumber_ReturnsFalse()
    {
        // Build a buffer with a MagicNumber that is not registered.
        System.Byte[] buf = new System.Byte[Nalix.Common.Networking.Packets.PacketConstants.HeaderSize];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, 0xDEADBEEF);

        System.Boolean found = _catalog.TryDeserialize(buf, out IPacket packet);

        Assert.False(found);
        Assert.Null(packet);
    }

    [Fact]
    public void TryDeserialize_EmptyBuffer_ReturnsFalse()
    {
        System.Boolean found = _catalog.TryDeserialize([], out IPacket packet);

        Assert.False(found);
        Assert.Null(packet);
    }

    // -------------------------------------------------------------------------
    // MagicNumber uniqueness
    // -------------------------------------------------------------------------

    [Fact]
    public void AllRegisteredPackets_HaveUniqueMagicNumbers()
    {
        // Each concrete packet type must hash to a different UInt32.
        System.UInt32 controlMagic = new Control().MagicNumber;
        System.UInt32 handshakeMagic = new Handshake().MagicNumber;
        System.UInt32 directiveMagic = new Directive().MagicNumber;

        Assert.NotEqual(controlMagic, handshakeMagic);
        Assert.NotEqual(controlMagic, directiveMagic);
        Assert.NotEqual(handshakeMagic, directiveMagic);
    }

    [Fact]
    public void DifferentPacketTypes_ProduceDifferentMagicNumbers()
    {
        // PacketRegistryFactory.Compute must be injective for these types.
        System.UInt32 a = PacketRegistryFactory.Compute(typeof(Control));
        System.UInt32 b = PacketRegistryFactory.Compute(typeof(Handshake));
        System.UInt32 c = PacketRegistryFactory.Compute(typeof(Directive));

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(b, c);
    }
}