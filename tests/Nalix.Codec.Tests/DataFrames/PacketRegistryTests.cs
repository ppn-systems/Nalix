// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.Codec.ProtocolFrames;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Objects;

namespace Nalix.Codec.Tests.DataFrames;

/// <summary>
/// Verifies packet registry round-trips and lookup behavior using the public registry pipeline.
/// </summary>
public sealed class PacketRegistryTests : IDisposable
{
    public PacketRegistryTests()
    {
        _ = InstanceManager.Instance.GetOrCreateInstance<ObjectPoolManager>();


        if (!PacketRegistry.IsBuilt)
            PacketRegistry.Build();
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
            reasonCode: ProtocolReason.NONE);

        byte[] bytes = original.Serialize();
        IPacket packet = PacketRegistry.Deserialize(bytes);

        Assert.NotNull(packet);

        Control result = Assert.IsType<Control>(packet);
        Assert.Equal(original.Header.OpCode, result.Header.OpCode);
        Assert.Equal(original.Header.MagicNumber, result.Header.MagicNumber);
        Assert.Equal(original.Header.SequenceId, result.Header.SequenceId);
        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.Reason, result.Reason);
        Assert.Equal(original.Header.Flags, result.Header.Flags);
        Assert.Equal(original.Header.Priority, result.Header.Priority);
    }

    [Fact]
    public void ControlMagicNumberIsConsistentAcrossInstances()
    {
        Control a = new();
        Control b = new();
        Assert.Equal(a.Header.MagicNumber, b.Header.MagicNumber);
    }

    [Fact]
    public void ControlAfterResetForPoolMagicNumberPreserved()
    {
        Control packet = new();
        uint magicBefore = packet.Header.MagicNumber;

        packet.ResetForPool();

        Assert.Equal(magicBefore, packet.Header.MagicNumber);
    }

    [Fact]
    public void ControlAfterResetForPoolCanBeReinitializedAndRoundTripped()
    {
        Control packet = new();
        packet.Initialize(0x0002, ControlType.PONG, sequenceId: 99);
        packet.ResetForPool();

        packet.Initialize(0x0003, ControlType.PING, sequenceId: 7);
        byte[] bytes = packet.Serialize();

        IPacket result = PacketRegistry.Deserialize(bytes);

        Control control = Assert.IsType<Control>(result);
        Assert.Equal(0x0003, control.Header.OpCode);
        Assert.Equal(7u, control.Header.SequenceId);
        Assert.Equal(ControlType.PING, control.Type);
    }


    [Fact]
    public void ComputedMagicMatchesInstanceMagicAndSerializedHeader()
    {
        Control control = new();
        Directive directive = new();

        uint regControl = PacketRegistry.Compute(typeof(Control));
        uint regDirective = PacketRegistry.Compute(typeof(Directive));

        Assert.Equal(regControl, control.Header.MagicNumber);
        Assert.Equal(regDirective, directive.Header.MagicNumber);

        byte[] bytes = control.Serialize();
        uint magicInBytes = System.Buffers.Binary.BinaryPrimitives
                                           .ReadUInt32LittleEndian(bytes);
        Assert.Equal(regControl, magicInBytes);
    }


    [Fact]
    public void DirectiveSerializeThenDeserializePreservesAllFields()
    {
        Directive original = new();
        original.Initialize(
            opCode: 0x0020,
            type: ControlType.NOTICE,
            reason: ProtocolReason.NONE,
            action: ProtocolAdvice.RETRY,
            sequenceId: 123,
            flags: PacketFlags.SYSTEM | PacketFlags.RELIABLE,
            controlFlags: ControlFlags.NONE,
            arg0: 0xDEAD,
            arg1: 0xBEEF,
            arg2: 0xFF);

        byte[] bytes = original.Serialize();
        IPacket packet = PacketRegistry.Deserialize(bytes);

        Directive result = Assert.IsType<Directive>(packet);

        Assert.Equal(original.Header.OpCode, result.Header.OpCode);
        Assert.Equal(original.Header.MagicNumber, result.Header.MagicNumber);
        Assert.Equal(original.Header.SequenceId, result.Header.SequenceId);
        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.Reason, result.Reason);
        Assert.Equal(original.Action, result.Action);
        Assert.Equal(original.Control, result.Control);
        Assert.Equal(original.Arg0, result.Arg0);
        Assert.Equal(original.Arg1, result.Arg1);
        Assert.Equal(original.Arg2, result.Arg2);
        Assert.Equal(original.Header.Priority, result.Header.Priority);
        Assert.Equal(original.Header.Flags, result.Header.Flags);
    }

    [Fact]
    public void DeserializeWhenBufferIsTooShortThrowsArgumentException()
    {
        byte[] tooShort = new byte[3];

        ArgumentException ex = Assert.Throws<ArgumentException>(() => PacketRegistry.Deserialize(tooShort));
        Assert.StartsWith("Raw packet data is too short to contain a valid header", ex.Message);
    }

    [Fact]
    public void DeserializeWhenMagicNumberIsUnknownThrowsInvalidOperationException()
    {
        byte[] buf = new byte[PacketConstants.HeaderSize];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, 0xDEADBEEF);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => PacketRegistry.Deserialize(buf));
        Assert.StartsWith("Cannot deserialize packet: Magic", ex.Message);
    }

    [Fact]
    public void DeserializeWhenBufferIsEmptyThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => PacketRegistry.Deserialize([]));
        Assert.StartsWith("Raw packet data is too short to contain a valid header", ex.Message);
    }

    [Fact]
    public void DeserializeIntoExistingReferenceReturnsResolvedPacket()
    {
        Control original = new();
        original.Initialize(0x0020, ControlType.PING, sequenceId: 7);

        byte[] bytes = original.Serialize();
        Control destination = new();

        IPacket packet = PacketRegistry.Deserialize(bytes);
        Control result = Assert.IsType<Control>(packet);

        Assert.Equal(original.Header.OpCode, result.Header.OpCode);
        Assert.Equal(original.Header.MagicNumber, result.Header.MagicNumber);
        Assert.Equal(original.Header.SequenceId, result.Header.SequenceId);
        Assert.Equal(original.Type, result.Type);
    }

    [Fact]
    public void TryDeserializeIntoExistingReferenceReturnsFalseForUnknownMagic()
    {
        byte[] buf = new byte[PacketConstants.HeaderSize];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, 0xDEADBEEF);

        Control destination = new();
        bool ok = PacketRegistry.TryDeserialize(buf, out IPacket? packet);

        Assert.False(ok);
    }

    [Fact]
    public void AllRegisteredPacketsHaveUniqueMagicNumbers()
    {
        uint controlMagic = new Control().Header.MagicNumber;
        uint directiveMagic = new Directive().Header.MagicNumber;

        Assert.NotEqual(controlMagic, directiveMagic);
    }

    [Fact]
    public void DifferentPacketTypesProduceDifferentMagicNumbers()
    {
        uint a = PacketRegistry.Compute(typeof(Control));
        uint c = PacketRegistry.Compute(typeof(Directive));

        Assert.NotEqual(a, c);
    }
}

















