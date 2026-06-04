// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable CA1000 // Do not declare static members on generic types

using Nalix.Abstractions.Networking.Packets;

namespace Nalix.Codec.DataFrames;

/// <summary>
/// Provides a runtime-accessible schema for generated packet metadata.
/// Populated by the source generator at startup via module initializers.
/// </summary>
/// <typeparam name="TSelf">The concrete packet type.</typeparam>
[System.Diagnostics.DebuggerStepThrough]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class PacketSchema<TSelf> where TSelf : PacketBase<TSelf>, IPacketStaticOpcode, new()
{
    /// <summary>
    /// Gets or sets the minimum static size of the packet header and fixed-size fields.
    /// </summary>
    public static int StaticSize { get; set; }
}
