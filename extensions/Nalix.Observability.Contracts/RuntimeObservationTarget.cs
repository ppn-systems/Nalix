// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Observability.Contracts;

/// <summary>
/// Identifies the runtime subsystem to observe.
/// </summary>
public enum RuntimeObservationTarget : byte
{
    /// <summary>
    /// No runtime observation target has been assigned.
    /// </summary>
    NONE = 0x00,

    /// <summary>
    /// Packet dispatch metrics and queue state.
    /// </summary>
    DISPATCH = 0x01,

    /// <summary>
    /// Runtime task scheduling and execution metrics.
    /// </summary>
    TASKS = 0x02,

    /// <summary>
    /// Buffer pool usage and allocation metrics.
    /// </summary>
    BUFFERS = 0x03,

    /// <summary>
    /// Active connection metrics.
    /// </summary>
    CONNECTIONS = 0x04,

    /// <summary>
    /// Registered dependency and runtime instance metrics.
    /// </summary>
    INSTANCES = 0x05,

    /// <summary>
    /// Object pool usage and lifecycle metrics.
    /// </summary>
    OBJECT_POOLS = 0x06,

    /// <summary>
    /// Connection guard throttling and protection metrics.
    /// </summary>
    CONNECTION_GUARD = 0x07,

    /// <summary>
    /// Concurrency gate limits and usage metrics.
    /// </summary>
    CONCURRENCY_GATE = 0x08,

    /// <summary>
    /// Policy rate limiter counters and state.
    /// </summary>
    POLICY_RATE_LIMITER = 0x09,

    /// <summary>
    /// Token bucket limiter counters and state.
    /// </summary>
    TOKEN_BUCKET_LIMITER = 0x0A,

    /// <summary>
    /// Active session metrics.
    /// </summary>
    SESSIONS = 0x0B,

    /// <summary>
    /// Listener transport metrics.
    /// </summary>
    LISTENER = 0x0C,

    /// <summary>
    /// Protocol transport metrics.
    /// </summary>
    PROTOCOL = 0x0D
}
