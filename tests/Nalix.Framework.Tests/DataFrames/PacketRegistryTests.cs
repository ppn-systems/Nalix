// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Networking.Protocols;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;
using Xunit;

namespace Nalix.Framework.Tests.DataFrames;

/// <summary>
/// Verifies packet registry round-trips and lookup behavior using the public registry pipeline.
/// </summary>
public sealed class PacketRegistryTests : IDisposable
{
    private readonly IPacketRegistry _catalog;

    public PacketRegistryTests()
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();

        _catalog = new PacketRegistry(factory =>
        {
            _ = factory.RegisterPacket<Control>();
            _ = factory.RegisterPacket<Handshake>();
            _ = factory.RegisterPacket<Directive>();
        });
    }

    public void Dispose()
    {
    }

    [Fact]
    public void ControlSerializeThenDeserializePreservesPublicState()
    {
        Control original = new();
        original.Initialize(
            opCode: 0x0001,
            type: ControlType.PING,
            sequenceId: 42,
            reasonCode: ProtocolReason.NONE,
            transport: ProtocolType.TCP);

        byte[] bytes = original.Serialize();
        IPacket packet = _catalog.Deserialize(bytes);

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
    public void ControlAfterResetForPoolCanBeReinitializedAndRoundTripped()
    {
        Control packet = new();
        packet.Initialize(0x0002, ControlType.PONG, sequenceId: 99);
        packet.ResetForPool();

        packet.Initialize(0x0003, ControlType.PING, sequenceId: 7);
        byte[] bytes = packet.Serialize();

        IPacket result = _catalog.Deserialize(bytes);

        Control control = Assert.IsType<Control>(result);
        Assert.Equal(0x0003, control.OpCode);
        Assert.Equal(7u, control.SequenceId);
        Assert.Equal(ControlType.PING, control.Type);
    }

    [Fact]
    public void HandshakeSerializeThenDeserializePreservesPayload()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0xDE, 0xAD, 0xBE, 0xEF];
        Handshake original = new(opCode: 0x0010, data: payload, transport: ProtocolType.TCP);

        byte[] bytes = original.Serialize();
        IPacket packet = _catalog.Deserialize(bytes);

        Handshake result = Assert.IsType<Handshake>(packet);
        Assert.Equal(original.OpCode, result.OpCode);
        Assert.Equal(original.MagicNumber, result.MagicNumber);
        Assert.Equal(original.Protocol, result.Protocol);
        Assert.Equal(payload, result.Data);
    }

    [Fact]
    public void ComputedMagicMatchesInstanceMagicAndSerializedHeader()
    {
        Control control = new();
        Handshake handshake = new();
        Directive directive = new();

        uint regControl = PacketRegistryFactory.Compute(typeof(Control));
        uint regHandshake = PacketRegistryFactory.Compute(typeof(Handshake));
        uint regDirective = PacketRegistryFactory.Compute(typeof(Directive));

        Assert.Equal(regControl, control.MagicNumber);
        Assert.Equal(regHandshake, handshake.MagicNumber);
        Assert.Equal(regDirective, directive.MagicNumber);

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

        IPacket packet = _catalog.Deserialize(bytes);

        Handshake result = Assert.IsType<Handshake>(packet);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void DirectiveSerializeThenDeserializePreservesAllFields()
    {
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

        byte[] bytes = original.Serialize();
        IPacket packet = _catalog.Deserialize(bytes);

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

    [Fact]
    public void DeserializeWhenBufferIsTooShortThrowsArgumentException()
    {
        byte[] tooShort = new byte[3];

        ArgumentException ex = Assert.Throws<ArgumentException>(() => _catalog.Deserialize(tooShort));
        Assert.StartsWith("Raw packet data is too short to contain a valid header", ex.Message);
    }

    [Fact]
    public void DeserializeWhenMagicNumberIsUnknownThrowsInvalidOperationException()
    {
        byte[] buf = new byte[PacketConstants.HeaderSize];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, 0xDEADBEEF);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => _catalog.Deserialize(buf));
        Assert.StartsWith("Cannot deserialize packet: Magic", ex.Message);
    }

    [Fact]
    public void DeserializeWhenBufferIsEmptyThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => _catalog.Deserialize([]));
        Assert.StartsWith("Raw packet data is too short to contain a valid header", ex.Message);
    }

    [Fact]
    public void AllRegisteredPacketsHaveUniqueMagicNumbers()
    {
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
        uint a = PacketRegistryFactory.Compute(typeof(Control));
        uint b = PacketRegistryFactory.Compute(typeof(Handshake));
        uint c = PacketRegistryFactory.Compute(typeof(Directive));

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(b, c);
    }
}
