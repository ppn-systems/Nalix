// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;

namespace Nalix.Codec.Serialization.Formatters.Primitives;

/// <summary>
/// High-performance zero-allocation formatter for the unmanaged <see cref="PacketHeader"/> struct.
/// Writes and reads the full 10-byte header as a single unmanaged block.
/// </summary>
public sealed class PacketHeaderFormatter : IFormatter<PacketHeader>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, in PacketHeader value) => writer.WriteUnmanaged(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketHeader Deserialize(ref DataReader reader) => reader.ReadUnmanaged<PacketHeader>();
}
