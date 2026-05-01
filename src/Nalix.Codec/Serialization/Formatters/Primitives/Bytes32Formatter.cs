// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Nalix.Abstractions.Primitives;
using Nalix.Codec.Extensions;
using Nalix.Codec.Memory;

namespace Nalix.Codec.Serialization.Formatters.Primitives;

/// <summary>
/// High-performance zero-allocation formatter for the unmanaged Bytes32 struct.
/// </summary>
public sealed class Bytes32Formatter : IFormatter<Bytes32>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Serialize(ref DataWriter writer, Bytes32 value) => writer.WriteUnmanaged(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bytes32 Deserialize(ref DataReader reader) => reader.ReadUnmanaged<Bytes32>();
}
