// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

#pragma warning disable CA1711

namespace Nalix.Abstractions.Networking.Protocols;

/// <summary>
/// Additional context flags for protocol control messages.
/// </summary>
[System.Flags]
public enum ControlFlags : byte
{
    /// <summary>
    /// No special flags set.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Indicates the condition is transient and safe to retry.
    /// </summary>
    IS_TRANSIENT = 1 << 0,

    /// <summary>
    /// Indicates the error is related to authentication or authorization.
    /// </summary>
    IS_AUTHRELATED = 1 << 1,

    /// <summary>
    /// Indicates redirect fields are present in the payload.
    /// </summary>
    HAS_REDIRECT = 1 << 2,

    /// <summary>
    /// Indicates the client should reduce its sending rate.
    /// </summary>
    SLOW_DOWN = 1 << 3
}
