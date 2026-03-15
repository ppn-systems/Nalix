// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Nalix.Chat.Domain.Users;

namespace Nalix.Chat.Domain.Rooms;

/// <summary>
/// Aggregate root for a chat room.
/// </summary>
public sealed class ChatRoom
{
    private readonly ConcurrentDictionary<string, ChatParticipant> _participants = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a room aggregate.
    /// </summary>
    public ChatRoom(string roomId, long createdAtUnixMs)
    {
        this.RoomId = roomId;
        this.CreatedAtUnixMs = createdAtUnixMs;
    }

    /// <summary>
    /// Gets canonical room id.
    /// </summary>
    public string RoomId { get; }

    /// <summary>
    /// Gets room creation time in unix milliseconds.
    /// </summary>
    public long CreatedAtUnixMs { get; }

    /// <summary>
    /// Gets current participant count.
    /// </summary>
    public int ParticipantCount => _participants.Count;

    /// <summary>
    /// Attempts to add a participant.
    /// </summary>
    public bool TryAddParticipant(ChatParticipant participant, out ChatParticipant? existing)
    {
        ArgumentNullException.ThrowIfNull(participant);

        if (_participants.TryAdd(participant.ParticipantId, participant))
        {
            existing = null;
            return true;
        }

        _ = _participants.TryGetValue(participant.ParticipantId, out existing);
        return false;
    }

    /// <summary>
    /// Attempts to remove a participant.
    /// </summary>
    public bool TryRemoveParticipant(string participantId, out ChatParticipant? participant)
    {
        ArgumentNullException.ThrowIfNull(participantId);
        return _participants.TryRemove(participantId, out participant);
    }

    /// <summary>
    /// Returns true when participant is in room.
    /// </summary>
    public bool IsParticipantMember(string participantId)
    {
        ArgumentNullException.ThrowIfNull(participantId);
        return _participants.ContainsKey(participantId);
    }

    /// <summary>
    /// Tries to read participant info.
    /// </summary>
    public bool TryGetParticipant(string participantId, out ChatParticipant? participant)
    {
        ArgumentNullException.ThrowIfNull(participantId);
        return _participants.TryGetValue(participantId, out participant);
    }

    /// <summary>
    /// Captures a point-in-time participant snapshot.
    /// </summary>
    public IReadOnlyCollection<ChatParticipant> SnapshotParticipants()
    {
        if (_participants.IsEmpty)
        {
            return Array.Empty<ChatParticipant>();
        }

        ChatParticipant[] snapshot = new ChatParticipant[_participants.Count];
        int index = 0;

        foreach (KeyValuePair<string, ChatParticipant> pair in _participants)
        {
            snapshot[index++] = pair.Value;
        }

        if (index == snapshot.Length)
        {
            return snapshot;
        }

        ChatParticipant[] trimmed = new ChatParticipant[index];
        Array.Copy(snapshot, trimmed, index);
        return trimmed;
    }
}
