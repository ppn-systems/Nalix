// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Defines how the server handles a connection when it sends more packets
/// than the configured rate-limit or overflow threshold allows.
/// </summary>
public enum NetworkOverflowPolicy
{
    /// <summary>
    /// Silently drops the excessive packet while keeping older queued packets for processing.
    /// This is the safe default behavior.
    /// </summary>
    DropPacket = 0,

    /// <summary>
    /// Immediately disconnects the connection. This is suitable for penalizing connections
    /// that show signs of intentional spam or DDoS-like behavior beyond the configured threshold.
    /// </summary>
    Disconnect = 1
}
