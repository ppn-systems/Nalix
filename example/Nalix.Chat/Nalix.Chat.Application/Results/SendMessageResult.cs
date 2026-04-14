// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Domain.Services;

namespace Nalix.Chat.Application.Results;

/// <summary>
/// Send-message use-case result.
/// </summary>
public sealed record SendMessageResult(
    bool Succeeded,
    ChatDomainErrorCode ErrorCode,
    string Message,
    string RoomId,
    string SenderId,
    string SenderDisplayName,
    long ClientMessageId,
    long ServerMessageId,
    long SentAtUnixMs,
    string Content);
