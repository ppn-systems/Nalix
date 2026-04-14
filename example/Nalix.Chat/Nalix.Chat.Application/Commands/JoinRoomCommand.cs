// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Application.Commands;

/// <summary>
/// Command for joining a room.
/// </summary>
public sealed record JoinRoomCommand(
    string RoomId,
    string ParticipantId,
    string DisplayName,
    long ClientRequestId);
