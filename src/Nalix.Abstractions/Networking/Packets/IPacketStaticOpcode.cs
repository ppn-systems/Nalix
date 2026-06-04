// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Forces an implementing packet to define a static operation code.
/// Used via generic constraints to avoid runtime reflection and abstract class conflicts.
/// </summary>
public interface IPacketStaticOpcode
{
    /// <summary>
    /// Gets the unique operation code that identifies this packet format.
    /// </summary>
    static abstract ushort StaticOpCode { get; }
}
