// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Diagnostics;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.Controls;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Nalix.Logging;
using Xunit;

namespace Nalix.Framework.Tests.Registry;

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "<Pending>")]
    private readonly IPacketRegistry _catalog;

    public PacketCatalogTests()
    {
        // Register ObjectPoolManager so PacketBase<TSelf>.Deserialize can use the pool.
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

        // Build catalog the same way production code does.
        _catalog = new PacketRegistry(factory =>
        {
            _ = factory.RegisterPacket<Control>();
            _ = factory.RegisterPacket<Handshake>();
            _ = factory.RegisterPacket<Directive>();
        });
    }

    public void Dispose()
    {
        // Nothing to tear down — InstanceManager is process-scoped.
        // ObjectPoolManager cleans up pooled instances automatically.
    }

    #endregion Setup

    [Fact]
    public void DebugRegistryBuildDirect()
    {
        // Build registry thủ công, không qua PacketRegistry constructor
        PacketRegistryFactory factory = new();

        PacketRegistry registry = factory.CreateCatalog();

        uint key = PacketRegistryFactory.Compute(typeof(Control));

        bool hasKey = registry.TryGetDeserializer(key, out _);

        Assert.True(hasKey, $"Key 0x{key:X8} không có trong registry sau khi build, Auto 0x{new Control().MagicNumber:X8}");
    }



    // -------------------------------------------------------------------------
    // Control (fixed-size packet)
    // -------------------------------------------------------------------------

    [Fact]
    public void ControlSerializeThenDeserializeReturnsSamePacket()
    {
        InstanceManager.Instance.Register<ILogger>(NLogix.Host.Instance);
        // Arrange
        Control original = new();
        original.Initialize(
            opCode: 0x0001,
            type: ControlType.PING,
            sequenceId: 42,
            reasonCode: ProtocolReason.NONE,
            transport: ProtocolType.TCP);

        // Act — serialize to byte[]
        byte[] bytes = original.Serialize();

        // Assert catalog can identify and deserialize it
        bool found = _catalog.TryDeserialize(bytes, out IPacket packet);

        Assert.True(found);
        Assert.NotNull(packet);

        Control result = Assert.IsType<Control>(packet);
        Assert.Equal(original.OpCode, result.OpCode);
        Assert.Equal(original.MagicNumber, result.MagicNumber);
        Assert.Equal(original.SequenceId, result.SequenceId);
        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.Reason, result.Reason);
        Assert.Equal(original.Protocol, result.Protocol);
        Assert.Equal(original.Priority, result.Priority);
    }

    [Fact]
    public void ControlMagicNumberIsConsistentAcrossInstances()
    {
        // AutoMagic must be deterministic — same type always yields same hash.
        Control a = new();
        Control b = new();
        Assert.Equal(a.MagicNumber, b.MagicNumber);
    }

    [Fact]
    public void ControlAfterResetForPoolMagicNumberPreserved()
    {
        Control packet = new();
        uint magicBefore = packet.MagicNumber;

        packet.ResetForPool();

        Assert.Equal(magicBefore, packet.MagicNumber);
    }

    [Fact]
    public void ControlAfterResetForPoolCanRoundTripAgain()
    {
        // Simulate: get from pool → use → return → get again → use.
        Control packet = new();
        packet.Initialize(0x0002, ControlType.PONG, sequenceId: 99);
        packet.ResetForPool();

        // Re-initialize after reset
        packet.Initialize(0x0003, ControlType.PING, sequenceId: 7);
        byte[] bytes = packet.Serialize();

        bool found = _catalog.TryDeserialize(bytes, out IPacket result);

        Assert.True(found);
        Control control = Assert.IsType<Control>(result);
        Assert.Equal(0x0003, control.OpCode);
        Assert.Equal(7u, control.SequenceId);
        Assert.Equal(ControlType.PING, control.Type);
    }

    // -------------------------------------------------------------------------
    // Handshake (dynamic-size packet — has byte[] Data)
    // -------------------------------------------------------------------------

    [Fact]
    public void HandshakeSerializeThenDeserializeReturnsSamePayload()
    {
        // Arrange
        byte[] payload = [0x01, 0x02, 0x03, 0xDE, 0xAD, 0xBE, 0xEF];
        Handshake original = new(opCode: 0x0010, data: payload, transport: ProtocolType.TCP);

        // Act
        byte[] bytes = original.Serialize();
        bool found = _catalog.TryDeserialize(bytes, out IPacket packet);

        // Assert
        Assert.True(found);
        Handshake result = Assert.IsType<Handshake>(packet);
        Assert.Equal(original.OpCode, result.OpCode);
        Assert.Equal(original.MagicNumber, result.MagicNumber);
        Assert.Equal(original.Protocol, result.Protocol);
        Assert.Equal(payload, result.Data);
    }

    [Fact]
    public void DebugMagicNumbers()
    {
        Control control = new();
        Handshake handshake = new();
        Directive directive = new();

        uint regControl = PacketRegistryFactory.Compute(typeof(Control));
        uint regHandshake = PacketRegistryFactory.Compute(typeof(Handshake));
        uint regDirective = PacketRegistryFactory.Compute(typeof(Directive));

        // Xem instance magic vs registry key có khớp không
        Assert.Equal(regControl, control.MagicNumber);
        Assert.Equal(regHandshake, handshake.MagicNumber);
        Assert.Equal(regDirective, directive.MagicNumber);

        // Xem bytes[0..4] sau serialize có đúng MagicNumber không
        byte[] bytes = control.Serialize();
        uint magicInBytes = System.Buffers.Binary.BinaryPrimitives
                                           .ReadUInt32LittleEndian(bytes);
        Assert.Equal(regControl, magicInBytes);
    }

    [Fact]
    public void HandshakeEmptyPayloadRoundTripsCorrectly()
    {
        Handshake original = new(opCode: 0x0011, data: [], transport: ProtocolType.UDP);
        byte[] bytes = original.Serialize();

        bool found = _catalog.TryDeserialize(bytes, out IPacket packet);

        Assert.True(found);
        Handshake result = Assert.IsType<Handshake>(packet);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    // -------------------------------------------------------------------------
    // Directive (fixed-size, multiple fields)
    // -------------------------------------------------------------------------

    [Fact]
    public void DirectiveSerializeThenDeserializeAllFieldsPreserved()
    {
        // Arrange
        Directive original = new();
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
        byte[] bytes = original.Serialize();
        bool found = _catalog.TryDeserialize(bytes, out IPacket packet);

        // Assert
        Assert.True(found);
        Directive result = Assert.IsType<Directive>(packet);

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
    public void TryDeserializeBufferTooShortReturnsFalse()
    {
        // Buffer smaller than HeaderSize must return false, not throw.
        byte[] tooShort = new byte[3];
        bool found = _catalog.TryDeserialize(tooShort, out IPacket packet);

        Assert.False(found);
        Assert.Null(packet);
    }

    [Fact]
    public void TryDeserializeUnknownMagicNumberReturnsFalse()
    {
        // Build a buffer with a MagicNumber that is not registered.
        byte[] buf = new byte[PacketConstants.HeaderSize];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, 0xDEADBEEF);

        bool found = _catalog.TryDeserialize(buf, out IPacket packet);

        Assert.False(found);
        Assert.Null(packet);
    }

    [Fact]
    public void TryDeserializeEmptyBufferReturnsFalse()
    {
        bool found = _catalog.TryDeserialize([], out IPacket packet);

        Assert.False(found);
        Assert.Null(packet);
    }

    // -------------------------------------------------------------------------
    // MagicNumber uniqueness
    // -------------------------------------------------------------------------

    [Fact]
    public void AllRegisteredPacketsHaveUniqueMagicNumbers()
    {
        // Each concrete packet type must hash to a different UInt32.
        uint controlMagic = new Control().MagicNumber;
        uint handshakeMagic = new Handshake().MagicNumber;
        uint directiveMagic = new Directive().MagicNumber;

        Assert.NotEqual(controlMagic, handshakeMagic);
        Assert.NotEqual(controlMagic, directiveMagic);
        Assert.NotEqual(handshakeMagic, directiveMagic);
    }

    [Fact]
    public void DifferentPacketTypesProduceDifferentMagicNumbers()
    {
        // PacketRegistryFactory.Compute must be injective for these types.
        uint a = PacketRegistryFactory.Compute(typeof(Control));
        uint b = PacketRegistryFactory.Compute(typeof(Handshake));
        uint c = PacketRegistryFactory.Compute(typeof(Directive));

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(b, c);
    }
}
