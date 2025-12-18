// Copyright (c) 2025 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Common.Security;

/// <summary>
/// Behavior when a per-connection queue is full.
/// </summary>
public enum DropPolicy
{
    /// <summary>
    /// Drop the incoming (newest) packet.
    /// </summary>
    DROP_NEWEST = 0,

    /// <summary>
    /// Drop the oldest packet in the queue to make room for the new one.
    /// </summary>
    DROP_OLDEST = 1,

    /// <summary>
    /// BLOCK the producer until there is room (backpressure).
    /// WARNING: may stall the receiving loop if abused.
    /// </summary>
    BLOCK = 2,

    /// <summary>
    /// COALESCE duplicate packets (by key) and keep only the latest.
    /// </summary>
    COALESCE = 3
}