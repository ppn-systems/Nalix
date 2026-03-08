// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Application.Commands;

/// <summary>
/// Command for sending a room message.
/// </summary>
public sealed record SendMessageCommand(
    string RoomId,
    string ParticipantId,
    long ClientMessageId,
    string Content);
