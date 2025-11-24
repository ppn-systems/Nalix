// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Transport;

/// <summary>
/// Represents the lifecycle state of a <see cref="TcpSessionBase"/>.
/// </summary>
public enum TcpSessionState : byte
{
    /// <summary>
    /// Not connected. Initial state and state after a clean disconnect.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// A connect attempt is in progress.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Socket is open and operational.
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Connection was lost; an automatic reconnect attempt is in progress.
    /// </summary>
    Reconnecting = 3,

    /// <summary>
    /// The session has been disposed and cannot be reused.
    /// </summary>
    Disposed = 4
}
