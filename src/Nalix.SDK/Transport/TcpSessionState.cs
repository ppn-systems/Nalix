// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.SDK.Transport;

/// <summary>
/// Represents the lifecycle state of a <see cref="TcpSessionBase"/>.
/// </summary>
public enum TcpSessionState : System.Byte
{
    /// <summary>Not connected. Initial state and state after a clean disconnect.</summary>
    Disconnected,

    /// <summary>A connect attempt is in progress.</summary>
    Connecting,

    /// <summary>Socket is open and operational.</summary>
    Connected,

    /// <summary>Connection was lost; an automatic reconnect attempt is in progress.</summary>
    Reconnecting,

    /// <summary>The session has been disposed and cannot be reused.</summary>
    Disposed
}
