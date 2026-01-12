// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Messaging.Protocols;

/// <summary>
/// High-level client actions suggested by the server for a given reason.
/// Clients should treat these as hints to guide behavior.
/// </summary>
public enum ProtocolAdvice : System.Byte
{
    /// <summary>
    /// No specific action. Log and continue.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// Retry immediately (typically after a quick reconnect).
    /// </summary>
    RETRY = 1,

    /// <summary>
    /// Retry with exponential backoff and jitter.
    /// </summary>
    BACKOFF_RETRY = 2,

    /// <summary>
    /// Do not retry automatically. Requires user or app intervention.
    /// </summary>
    DO_NOT_RETRY = 3,

    /// <summary>
    /// Prompt user to re-authenticate or refresh credentials.
    /// </summary>
    REAUTHENTICATE = 4,

    /// <summary>
    /// Reduce sending rate or adjust flow control credits.
    /// </summary>
    SLOW_DOWN = 5,

    /// <summary>
    /// Reconnect or switch transport/route.
    /// </summary>
    RECONNECT = 6,

    /// <summary>
    /// Fix the issue and retry the operation.
    /// </summary>
    FIX_AND_RETRY = 7,
}
