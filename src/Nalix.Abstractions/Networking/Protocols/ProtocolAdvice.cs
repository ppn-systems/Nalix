// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking.Protocols;

/// <summary>
/// High-level actions suggested to the client for a given control reason.
/// Treat these values as guidance rather than strict commands.
/// </summary>
public enum ProtocolAdvice : byte
{
    /// <summary>
    /// No specific action.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Retry immediately.
    /// </summary>
    RETRY = 1,

    /// <summary>
    /// Retry with exponential backoff and jitter.
    /// </summary>
    BACKOFF_RETRY = 2,

    /// <summary>
    /// Do not retry automatically.
    /// </summary>
    DO_NOT_RETRY = 3,

    /// <summary>
    /// Re-authenticate or refresh credentials.
    /// </summary>
    REAUTHENTICATE = 4,

    /// <summary>
    /// Reduce sending rate.
    /// </summary>
    SLOW_DOWN = 5,

    /// <summary>
    /// Reconnect or switch transport or route.
    /// </summary>
    RECONNECT = 6,

    /// <summary>
    /// Fix the issue and retry.
    /// </summary>
    FIX_AND_RETRY = 7,
}
