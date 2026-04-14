// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking;

namespace Nalix.Chat.Infrastructure.Handlers;

/// <summary>
/// Stores and restores chat identity data across resumed connections.
/// </summary>
public sealed class SessionResumeHandler
{
    /// <summary>
    /// Binds identity details to a live connection.
    /// </summary>
    public void BindIdentity(IConnection connection, string participantId, string roomId, string displayName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(participantId);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(displayName);

        connection.Attributes[ChatConnectionAttributeKeys.ParticipantId] = participantId;
        connection.Attributes[ChatConnectionAttributeKeys.RoomId] = roomId;
        connection.Attributes[ChatConnectionAttributeKeys.DisplayName] = displayName;
    }

    /// <summary>
    /// Attempts to read identity details from a live connection.
    /// </summary>
    public bool TryGetIdentity(IConnection connection, out ChatSessionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(connection);

        string? participantId = ReadString(connection, ChatConnectionAttributeKeys.ParticipantId);
        string? roomId = ReadString(connection, ChatConnectionAttributeKeys.RoomId);
        string? displayName = ReadString(connection, ChatConnectionAttributeKeys.DisplayName);

        if (string.IsNullOrWhiteSpace(participantId) ||
            string.IsNullOrWhiteSpace(roomId) ||
            string.IsNullOrWhiteSpace(displayName))
        {
            identity = default;
            return false;
        }

        identity = new ChatSessionIdentity(participantId, roomId, displayName);
        return true;
    }

    private static string? ReadString(IConnection connection, string key)
    {
        if (!connection.Attributes.TryGetValue(key, out object? raw) || raw is not string text)
        {
            return null;
        }

        return text;
    }
}

/// <summary>
/// Represents restored chat identity attributes.
/// </summary>
public readonly record struct ChatSessionIdentity(string ParticipantId, string RoomId, string DisplayName);
