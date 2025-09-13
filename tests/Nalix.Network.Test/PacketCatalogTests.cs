using Nalix.Common.Caching;
using Nalix.Common.Packets;
using Nalix.Common.Packets.Abstractions;
using Nalix.Common.Packets.Enums;
using Nalix.Common.Packets.Models;
using Nalix.Common.Protocols;
using Nalix.Shared.Messaging.Catalog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Xunit;

namespace Nalix.Network.Tests;

/// <summary>
/// Unit tests for <see cref="PacketCatalog"/> behaviors.
/// </summary>
public sealed class PacketCatalogTests
{
    private sealed class DummyPacket : IPacket, IPoolable
    {
        UInt16 IPacket.Length => throw new NotImplementedException();

        UInt32 IPacket.MagicNumber => throw new NotImplementedException();

        UInt16 IPacket.OpCode => throw new NotImplementedException();

        PacketFlags IPacket.Flags => throw new NotImplementedException();

        PacketPriority IPacket.Priority => throw new NotImplementedException();

        ProtocolType IPacket.Transport => throw new NotImplementedException();

        void IPoolable.ResetForPool() => throw new NotImplementedException();
        Byte[] IPacket.Serialize() => throw new NotImplementedException();
        Int32 IPacket.Serialize(Span<Byte> buffer) => throw new NotImplementedException();
    }

    /// <summary>
    /// Verify: TryDeserialize returns false when buffer shorter than header (4 bytes).
    /// </summary>
    [Fact]
    public void TryDeserialize_ShouldReturnFalse_ForShortBuffer()
    {
        // Arrange
        var transformers = new Dictionary<Type, PacketTransformer>().ToFrozenDictionary();
        var deserializers = new Dictionary<UInt32, PacketDeserializer>().ToFrozenDictionary();
        var catalog = new PacketCatalog(transformers, deserializers);
        Span<Byte> raw = stackalloc Byte[PacketConstants.HeaderSize - 1];

        // Act
        var ok = catalog.TryDeserialize(raw, out var pkt);

        // Assert
        Assert.False(ok);
        Assert.Null(pkt);
    }

    /// <summary>
    /// Verify: TryDeserialize uses registered deserializer for known magic number.
    /// </summary>
    [Fact]
    public void TryDeserialize_ShouldUseRegisteredDeserializer()
    {
        // Arrange
        const UInt32 Magic = 0x11223344;

        // Replace this line:
        // PacketDeserializer deser = static ReadOnlySpan<Byte> _ => new DummyPacket();

        // With this line:
        static IPacket deser(ReadOnlySpan<Byte> _) => new DummyPacket();
        var des = new Dictionary<UInt32, PacketDeserializer> { [Magic] = deser };
        var catalog = new PacketCatalog(
            new Dictionary<Type, PacketTransformer>().ToFrozenDictionary(),
            des.ToFrozenDictionary());

        // Build raw: [magic (LE)] + some payload
        Span<Byte> raw = stackalloc Byte[PacketConstants.HeaderSize + 2];
        _ = BitConverter.TryWriteBytes(raw, Magic);

        // Act
        var ok = catalog.TryDeserialize(raw, out var pkt);

        // Assert
        Assert.True(ok);
        _ = Assert.IsType<DummyPacket>(pkt);
    }

    /// <summary>
    /// Verify: TryGetDeserializer returns registered delegate; otherwise false.
    /// </summary>
    [Fact]
    public void TryGetDeserializer_ShouldReturnDelegate_IfRegistered()
    {
        // Arrange
        const UInt32 Magic = 0xAABBCCDD;
        static IPacket deser(ReadOnlySpan<Byte> _) => new DummyPacket();

        var catalog = new PacketCatalog(
            new Dictionary<Type, PacketTransformer>().ToFrozenDictionary(),
            new Dictionary<UInt32, PacketDeserializer> { [Magic] = deser }.ToFrozenDictionary());

        // Act
        var found = catalog.TryGetDeserializer(Magic, out var got);

        // Assert
        Assert.True(found);
        Assert.Equal(deser, got);

        // Negative
        Assert.False(catalog.TryGetDeserializer(0xDEADBEEF, out _));
    }
}
