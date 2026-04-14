// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Domain.Users;

/// <summary>
/// Represents a participant in a room.
/// </summary>
public sealed class ChatParticipant
{
    /// <summary>
    /// Initializes a participant.
    /// </summary>
    public ChatParticipant(string participantId, string displayName, long joinedAtUnixMs)
    {
        this.ParticipantId = participantId;
        this.DisplayName = displayName;
        this.JoinedAtUnixMs = joinedAtUnixMs;
    }

    /// <summary>
    /// Gets participant unique id.
    /// </summary>
    public string ParticipantId { get; }

    /// <summary>
    /// Gets participant display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets join time in unix milliseconds.
    /// </summary>
    public long JoinedAtUnixMs { get; }
}
