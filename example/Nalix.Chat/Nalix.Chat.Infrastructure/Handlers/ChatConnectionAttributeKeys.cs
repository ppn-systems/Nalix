// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Infrastructure.Handlers;

/// <summary>
/// Well-known connection attribute keys used by the chat sample.
/// </summary>
public static class ChatConnectionAttributeKeys
{
    /// <summary>
    /// Stores participant id.
    /// </summary>
    public const string ParticipantId = "nalix.chat.participant-id";

    /// <summary>
    /// Stores room id.
    /// </summary>
    public const string RoomId = "nalix.chat.room-id";

    /// <summary>
    /// Stores display name.
    /// </summary>
    public const string DisplayName = "nalix.chat.display-name";

    /// <summary>
    /// Stores cipher rotation counter per connection.
    /// </summary>
    public const string CipherRotationCounter = "nalix.chat.cipher-rotation-counter";
}
