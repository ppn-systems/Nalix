// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Chat.Domain.Messages;
using Nalix.Chat.Domain.Rooms;
using Nalix.Chat.Domain.Users;

namespace Nalix.Chat.Domain.Services;

/// <summary>
/// Defines chat room business operations.
/// </summary>
public interface IChatRoomService
{
    /// <summary>
    /// Attempts to join a room.
    /// </summary>
    ValueTask<JoinRoomOutcome> JoinRoomAsync(
        string roomId,
        string participantId,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to send a message to a room.
    /// </summary>
    ValueTask<SendMessageOutcome> SendMessageAsync(
        string roomId,
        string participantId,
        long clientMessageId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to leave a room.
    /// </summary>
    ValueTask<LeaveRoomOutcome> LeaveRoomAsync(
        string roomId,
        string participantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when participant is in room.
    /// </summary>
    bool IsParticipantInRoom(string roomId, string participantId);

    /// <summary>
    /// Attempts to read participant profile.
    /// </summary>
    bool TryGetParticipant(string roomId, string participantId, out ChatParticipant? participant);

    /// <summary>
    /// Returns room participants snapshot.
    /// </summary>
    IReadOnlyCollection<ChatParticipant> GetParticipants(string roomId);
}

/// <summary>
/// Domain-safe error codes used by room service.
/// </summary>
public enum ChatDomainErrorCode : byte
{
    /// <summary>
    /// No error occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// Validation failed.
    /// </summary>
    ValidationFailed = 1,

    /// <summary>
    /// Room was not found.
    /// </summary>
    RoomNotFound = 2,

    /// <summary>
    /// Participant is not in room.
    /// </summary>
    NotRoomMember = 3,

    /// <summary>
    /// Room reached max capacity.
    /// </summary>
    RoomCapacityReached = 4,

    /// <summary>
    /// Message was rejected by moderation policy.
    /// </summary>
    MessageRejected = 5,

    /// <summary>
    /// Participant is already in room.
    /// </summary>
    AlreadyInRoom = 6
}

/// <summary>
/// Outcome for room join operation.
/// </summary>
public readonly record struct JoinRoomOutcome(
    bool Succeeded,
    ChatDomainErrorCode ErrorCode,
    string Message,
    ChatRoom? Room,
    ChatParticipant? Participant);

/// <summary>
/// Outcome for send message operation.
/// </summary>
public readonly record struct SendMessageOutcome(
    bool Succeeded,
    ChatDomainErrorCode ErrorCode,
    string Message,
    ChatMessage? ChatMessage);

/// <summary>
/// Outcome for leave room operation.
/// </summary>
public readonly record struct LeaveRoomOutcome(
    bool Succeeded,
    ChatDomainErrorCode ErrorCode,
    string Message,
    ChatParticipant? Participant);
