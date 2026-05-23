// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Diagnostics;

/// <summary>
/// Defines the core telemetry target categories registered in the system diagnostics report registry.
/// </summary>
public enum CoreTelemetryTarget : byte
{
    /// <summary>
    /// No target specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Task manager and background execution telemetry.
    /// </summary>
    Tasks = 1,

    /// <summary>
    /// Buffer pool allocations and metrics.
    /// </summary>
    Buffers = 2,

    /// <summary>
    /// Generic object pool metrics.
    /// </summary>
    ObjectPools = 3,

    /// <summary>
    /// Active network connection hub and traffic stats.
    /// </summary>
    Connections = 4,

    /// <summary>
    /// Connection guard, IP banning, and blacklist metrics.
    /// </summary>
    ConnectionGuard = 5,

    /// <summary>
    /// Packet dispatch channel and routing telemetry.
    /// </summary>
    PacketDispatch = 6,

    /// <summary>
    /// Concurrency gate for per-opcode throttling.
    /// </summary>
    ConcurrencyGate = 7,

    /// <summary>
    /// Policy-based rate limiter metrics.
    /// </summary>
    PolicyRateLimiter = 8,

    /// <summary>
    /// Token bucket rate limiter metrics.
    /// </summary>
    TokenBucketLimiter = 9,

    /// <summary>
    /// Session management and persistence metrics.
    /// </summary>
    Sessions = 10,
}
