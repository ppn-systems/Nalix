// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Protocols;
using Nalix.Common.Primitives;

namespace Nalix.Common.Networking.Sessions;

/// <summary>
/// Describes the outcome of a session resume attempt.
/// </summary>
public readonly struct SessionResumeResult(
    bool success,
    ProtocolReason reason,
    UInt56 sessionToken,
    SessionSnapshot? snapshot,
    bool tokenRotated = false)
{
    /// <summary>
    /// Gets whether the resume succeeded.
    /// </summary>
    public bool Success { get; } = success;

    /// <summary>
    /// Gets the protocol reason that describes the result.
    /// </summary>
    public ProtocolReason Reason { get; } = reason;

    /// <summary>
    /// Gets the token that the client should continue using.
    /// </summary>
    public UInt56 SessionToken { get; } = sessionToken;

    /// <summary>
    /// Gets the restored snapshot when the resume succeeds.
    /// </summary>
    public SessionSnapshot? Snapshot { get; } = snapshot;

    /// <summary>
    /// Gets whether a replacement token was issued.
    /// </summary>
    public bool TokenRotated { get; } = tokenRotated;
}
