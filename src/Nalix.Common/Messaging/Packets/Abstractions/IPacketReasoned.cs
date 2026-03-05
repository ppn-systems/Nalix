// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Messaging.Protocols;

namespace Nalix.Common.Messaging.Packets.Abstractions;

/// <summary>
/// Defines a contract for packets that carry a reason code,
/// typically used in control scenarios such as disconnect, error, or nack.
/// </summary>
public interface IPacketReasoned
{
    /// <summary>
    /// Gets the reason code that explains the purpose or error for this packet.
    /// </summary>
    ProtocolReason Reason { get; }
}
