// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Domain.Services;

namespace Nalix.Chat.Application.Results;

/// <summary>
/// Join-room use-case result.
/// </summary>
public sealed record JoinRoomResult(
    bool Succeeded,
    ChatDomainErrorCode ErrorCode,
    string Message,
    string RoomId,
    string ParticipantId,
    string DisplayName,
    long ClientRequestId);
