// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Domain.Messages;

/// <summary>
/// Represents an accepted message in the domain.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// Initializes a message.
    /// </summary>
    public ChatMessage(
        long serverMessageId,
        long clientMessageId,
        string roomId,
        string senderId,
        string senderDisplayName,
        string content,
        long sentAtUnixMs)
    {
        this.ServerMessageId = serverMessageId;
        this.ClientMessageId = clientMessageId;
        this.RoomId = roomId;
        this.SenderId = senderId;
        this.SenderDisplayName = senderDisplayName;
        this.Content = content;
        this.SentAtUnixMs = sentAtUnixMs;
    }

    /// <summary>
    /// Gets server message id.
    /// </summary>
    public long ServerMessageId { get; }

    /// <summary>
    /// Gets client correlation id.
    /// </summary>
    public long ClientMessageId { get; }

    /// <summary>
    /// Gets room id.
    /// </summary>
    public string RoomId { get; }

    /// <summary>
    /// Gets sender id.
    /// </summary>
    public string SenderId { get; }

    /// <summary>
    /// Gets sender display name.
    /// </summary>
    public string SenderDisplayName { get; }

    /// <summary>
    /// Gets message content.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets server timestamp in unix milliseconds.
    /// </summary>
    public long SentAtUnixMs { get; }
}
