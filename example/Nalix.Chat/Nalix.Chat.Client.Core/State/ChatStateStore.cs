// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Nalix.Chat.Contracts.Events;

namespace Nalix.Chat.Client.Core.State;

/// <summary>
/// Stores room and message state in a UI-agnostic structure.
/// </summary>
public sealed class ChatStateStore
{
    private readonly ConcurrentDictionary<string, RoomBuffer> _rooms = new(StringComparer.Ordinal);

    /// <summary>
    /// Appends a broadcast message to room state.
    /// </summary>
    public void AppendMessage(ChatMessageBroadcast broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        RoomBuffer buffer = _rooms.GetOrAdd(broadcast.RoomId, static _ => new RoomBuffer());
        buffer.Append(broadcast);
    }

    /// <summary>
    /// Returns a room message snapshot.
    /// </summary>
    public IReadOnlyList<ChatMessageBroadcast> GetMessages(string roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        if (!_rooms.TryGetValue(roomId, out RoomBuffer? buffer))
        {
            return Array.Empty<ChatMessageBroadcast>();
        }

        return buffer.Snapshot();
    }

    private sealed class RoomBuffer
    {
        private readonly Lock _gate = new();
        private readonly List<ChatMessageBroadcast> _messages = [];

        public void Append(ChatMessageBroadcast message)
        {
            lock (_gate)
            {
                _messages.Add(message);
            }
        }

        public IReadOnlyList<ChatMessageBroadcast> Snapshot()
        {
            lock (_gate)
            {
                if (_messages.Count == 0)
                {
                    return Array.Empty<ChatMessageBroadcast>();
                }

                ChatMessageBroadcast[] snapshot = new ChatMessageBroadcast[_messages.Count];
                _messages.CopyTo(snapshot, 0);
                return snapshot;
            }
        }
    }
}
