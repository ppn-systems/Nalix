// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Client.Core.Networking;

/// <summary>
/// Represents network connectivity state for the chat client.
/// </summary>
public enum ConnectionState : byte
{
    /// <summary>
    /// Session is offline.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Session is establishing a connection.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Session is online.
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Session is reconnecting after a transient failure.
    /// </summary>
    Reconnecting = 3,

    /// <summary>
    /// Session entered a faulted terminal state.
    /// </summary>
    Faulted = 4
}
