// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.DataFrames;
using Nalix.Codec.Extensions;
using Xunit;

namespace Nalix.Codec.Tests.DataFrames;

/// <summary>
/// Verifies source-generated packet manifest registration without reflection scanning.
/// </summary>
public sealed class PacketRegistryGeneratedManifestTests
{
    [Fact]
    public void RegisterGeneratedPacketsCreatesCatalogAndDeserializesByMagic()
    {
        uint magic = PacketRegistryFactory.Compute(typeof(GeneratedManifestPacket));
        PacketRegistry registry = new PacketRegistryFactory()
            .RegisterGeneratedPackets(
            [
                new KeyValuePair<uint, PacketDeserializer>(
                    magic,
                    static raw => GeneratedManifestPacket.Deserialize(raw))
            ],
            [
                new KeyValuePair<uint, string>(magic, typeof(GeneratedManifestPacket).FullName!)
            ])
            .CreateCatalog();

        byte[] raw = new byte[PacketConstants.HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(raw, magic);

        Assert.Equal(1, registry.DeserializerCount);
        Assert.True(registry.IsKnownMagic(magic));
        Assert.True(registry.IsRegistered<GeneratedManifestPacket>());

        IPacket packet = registry.Deserialize(raw);
        GeneratedManifestPacket result = Assert.IsType<GeneratedManifestPacket>(packet);
        Assert.Equal(magic, result.Header.MagicNumber);
    }

    [Fact]
    public void RegisterGeneratedPacketsDetectsDuplicateMagicCollision()
    {
        uint magic = PacketRegistryFactory.Compute(typeof(GeneratedManifestPacket));
        PacketRegistryFactory factory = new PacketRegistryFactory()
            .RegisterGeneratedPackets(
            [
                new KeyValuePair<uint, PacketDeserializer>(magic, static raw => GeneratedManifestPacket.Deserialize(raw)),
                new KeyValuePair<uint, PacketDeserializer>(magic, static raw => GeneratedManifestPacket.Deserialize(raw))
            ],
            [
                new KeyValuePair<uint, string>(magic, typeof(GeneratedManifestPacket).FullName!)
            ]);

        InternalErrorException ex = Assert.Throws<InternalErrorException>(factory.CreateCatalog);

        Assert.Contains("Hash collision", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"0x{magic:X8}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterGeneratedPacketsRejectsNullDeserializerEnumerable()
    {
        PacketRegistryFactory factory = new();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => factory.RegisterGeneratedPackets(null!));

        Assert.Equal("deserializers", ex.ParamName);
    }

    private sealed class GeneratedManifestPacket : IPacket, IPacketDeserializer<GeneratedManifestPacket>
    {
        public int Length => PacketConstants.HeaderSize;

        public PacketHeader Header { get; set; }

        public byte[] Serialize()
        {
            byte[] buffer = new byte[PacketConstants.HeaderSize];
            _ = Serialize(buffer);
            return buffer;
        }

        public int Serialize(Span<byte> buffer)
        {
            if (buffer.Length < PacketConstants.HeaderSize)
            {
                throw new ArgumentException("buffer too small", nameof(buffer));
            }

            BinaryPrimitives.WriteUInt32LittleEndian(buffer, Header.MagicNumber);
            return PacketConstants.HeaderSize;
        }

        public static GeneratedManifestPacket Deserialize(ReadOnlySpan<byte> raw)
        {
            return new GeneratedManifestPacket
            {
                Header = raw.AsHeaderRef()
            };
        }
    }
}
