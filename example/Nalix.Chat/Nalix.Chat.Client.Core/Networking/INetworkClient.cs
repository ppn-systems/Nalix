// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Contracts.Events;
using Nalix.Chat.Contracts.Packets;
using Nalix.Common.Security;

namespace Nalix.Chat.Client.Core.Networking;

/// <summary>
/// Abstraction for the chat network manager.
/// </summary>
public interface INetworkClient : IAsyncDisposable
{
    /// <summary>
    /// Gets current connection state.
    /// </summary>
    ConnectionState ConnectionState { get; }

    /// <summary>
    /// Gets current active cipher suite.
    /// </summary>
    CipherSuiteType ActiveCipher { get; }

    /// <summary>
    /// Gets the number of successful cipher rotations.
    /// </summary>
    int CipherRotationCounter { get; }

    /// <summary>
    /// Gets active session identifier text.
    /// </summary>
    string SessionIdentifier { get; }

    /// <summary>
    /// Raised when connection state changes.
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Raised when a chat broadcast is received.
    /// </summary>
    event EventHandler<ChatMessageBroadcast>? MessageReceived;

    /// <summary>
    /// Raised when a transport or protocol error occurs.
    /// </summary>
    event EventHandler<Exception>? Error;

    /// <summary>
    /// Connects to server and establishes secure channel.
    /// </summary>
    ValueTask ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the current session.
    /// </summary>
    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends room join request.
    /// </summary>
    ValueTask<JoinRoomResponse> JoinRoomAsync(string roomId, string participantId, string displayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends chat message request.
    /// </summary>
    ValueTask<ChatMessageAck> SendMessageAsync(string roomId, string participantId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures round trip latency.
    /// </summary>
    ValueTask<double> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs time synchronization and returns current drift estimate.
    /// </summary>
    ValueTask<double> SyncClockAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates cipher suite mid-session.
    /// </summary>
    ValueTask RotateCipherAsync(CancellationToken cancellationToken = default);
}
