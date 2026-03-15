// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Chat.Contracts.Enums;

/// <summary>
/// Defines transport-safe chat error codes.
/// </summary>
public enum ChatErrorCode : byte
{
    /// <summary>
    /// No error occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// Input validation failed.
    /// </summary>
    ValidationFailed = 1,

    /// <summary>
    /// Authentication is missing or invalid.
    /// </summary>
    Unauthenticated = 2,

    /// <summary>
    /// The participant is not in the target room.
    /// </summary>
    NotRoomMember = 3,

    /// <summary>
    /// The room does not exist.
    /// </summary>
    RoomNotFound = 4,

    /// <summary>
    /// The room has reached capacity.
    /// </summary>
    RoomCapacityReached = 5,

    /// <summary>
    /// The message was blocked by moderation policy.
    /// </summary>
    MessageRejected = 6,

    /// <summary>
    /// The service dependency is unavailable.
    /// </summary>
    ServiceUnavailable = 7,

    /// <summary>
    /// Internal infrastructure failure.
    /// </summary>
    InfrastructureFailure = 8
}
