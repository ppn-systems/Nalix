// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.


// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Abstractions.Networking.Protocols;

namespace Nalix.Abstractions.Networking.Packets;

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
