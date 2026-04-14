// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Nalix.Chat.Application.Commands;
using Nalix.Chat.Application.Results;
using Nalix.Chat.Domain.Messages;
using Nalix.Chat.Domain.Policies;
using Nalix.Chat.Domain.Rooms;
using Nalix.Chat.Domain.Rules;
using Nalix.Chat.Domain.Services;
using Nalix.Chat.Domain.Users;

namespace Nalix.Chat.Application.Services;

/// <summary>
/// In-memory chat room service that enforces business rules.
/// </summary>
public sealed class ChatRoomService : IChatRoomService
{
    private readonly ConcurrentDictionary<string, ChatRoom> _rooms = new(StringComparer.Ordinal);
    private readonly RoomCapacityRule _roomCapacityRule;
    private readonly MessageModerationPolicy _moderationPolicy;
    private readonly ILogger<ChatRoomService>? _logger;
    private long _nextServerMessageId;

    /// <summary>
    /// Initializes a chat room service.
    /// </summary>
    public ChatRoomService(
        RoomCapacityRule roomCapacityRule,
        MessageModerationPolicy moderationPolicy,
        ILogger<ChatRoomService>? logger = null)
    {
        _roomCapacityRule = roomCapacityRule ?? throw new ArgumentNullException(nameof(roomCapacityRule));
        _moderationPolicy = moderationPolicy ?? throw new ArgumentNullException(nameof(moderationPolicy));
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<JoinRoomOutcome> JoinRoomAsync(
        string roomId,
        string participantId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(displayName))
        {
            return ValueTask.FromResult(new JoinRoomOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.ValidationFailed,
                Message: "Room id, participant id, and display name are required.",
                Room: null,
                Participant: null));
        }

        string normalizedRoomId = roomId.Trim();
        string normalizedParticipantId = participantId.Trim();
        string normalizedDisplayName = displayName.Trim();

        ChatRoom room = _rooms.GetOrAdd(
            normalizedRoomId,
            static key => new ChatRoom(key, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        if (!_roomCapacityRule.TryValidate(room.ParticipantCount, out string? capacityReason))
        {
            return ValueTask.FromResult(new JoinRoomOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.RoomCapacityReached,
                Message: capacityReason ?? "Room capacity reached.",
                Room: room,
                Participant: null));
        }

        ChatParticipant participant = new(
            normalizedParticipantId,
            normalizedDisplayName,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        if (!room.TryAddParticipant(participant, out ChatParticipant? existing))
        {
            ChatParticipant effective = existing ?? participant;
            return ValueTask.FromResult(new JoinRoomOutcome(
                Succeeded: true,
                ErrorCode: ChatDomainErrorCode.AlreadyInRoom,
                Message: "Participant is already in the room.",
                Room: room,
                Participant: effective));
        }

        _logger?.LogInformation("Participant {ParticipantId} joined room {RoomId}.", normalizedParticipantId, normalizedRoomId);

        return ValueTask.FromResult(new JoinRoomOutcome(
            Succeeded: true,
            ErrorCode: ChatDomainErrorCode.None,
            Message: "Joined room successfully.",
            Room: room,
            Participant: participant));
    }

    /// <inheritdoc/>
    public ValueTask<SendMessageOutcome> SendMessageAsync(
        string roomId,
        string participantId,
        long clientMessageId,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(participantId))
        {
            return ValueTask.FromResult(new SendMessageOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.ValidationFailed,
                Message: "Room id and participant id are required.",
                ChatMessage: null));
        }

        string normalizedRoomId = roomId.Trim();
        string normalizedParticipantId = participantId.Trim();

        if (!_rooms.TryGetValue(normalizedRoomId, out ChatRoom? room))
        {
            return ValueTask.FromResult(new SendMessageOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.RoomNotFound,
                Message: "Room not found.",
                ChatMessage: null));
        }

        if (!room.TryGetParticipant(normalizedParticipantId, out ChatParticipant? participant) || participant is null)
        {
            return ValueTask.FromResult(new SendMessageOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.NotRoomMember,
                Message: "Participant is not a member of this room.",
                ChatMessage: null));
        }

        if (!_moderationPolicy.TryValidate(content, out string? moderationReason))
        {
            return ValueTask.FromResult(new SendMessageOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.MessageRejected,
                Message: moderationReason ?? "Message failed moderation.",
                ChatMessage: null));
        }

        long serverMessageId = Interlocked.Increment(ref _nextServerMessageId);
        long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ChatMessage message = new(
            serverMessageId,
            clientMessageId,
            normalizedRoomId,
            normalizedParticipantId,
            participant.DisplayName,
            content,
            nowUnixMs);

        return ValueTask.FromResult(new SendMessageOutcome(
            Succeeded: true,
            ErrorCode: ChatDomainErrorCode.None,
            Message: "Message accepted.",
            ChatMessage: message));
    }

    /// <inheritdoc/>
    public ValueTask<LeaveRoomOutcome> LeaveRoomAsync(
        string roomId,
        string participantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(participantId))
        {
            return ValueTask.FromResult(new LeaveRoomOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.ValidationFailed,
                Message: "Room id and participant id are required.",
                Participant: null));
        }

        string normalizedRoomId = roomId.Trim();
        string normalizedParticipantId = participantId.Trim();

        if (!_rooms.TryGetValue(normalizedRoomId, out ChatRoom? room))
        {
            return ValueTask.FromResult(new LeaveRoomOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.RoomNotFound,
                Message: "Room not found.",
                Participant: null));
        }

        if (!room.TryRemoveParticipant(normalizedParticipantId, out ChatParticipant? participant) || participant is null)
        {
            return ValueTask.FromResult(new LeaveRoomOutcome(
                Succeeded: false,
                ErrorCode: ChatDomainErrorCode.NotRoomMember,
                Message: "Participant is not in the room.",
                Participant: null));
        }

        if (room.ParticipantCount == 0)
        {
            _ = _rooms.TryRemove(normalizedRoomId, out _);
        }

        _logger?.LogInformation("Participant {ParticipantId} left room {RoomId}.", normalizedParticipantId, normalizedRoomId);

        return ValueTask.FromResult(new LeaveRoomOutcome(
            Succeeded: true,
            ErrorCode: ChatDomainErrorCode.None,
            Message: "Participant removed from room.",
            Participant: participant));
    }

    /// <inheritdoc/>
    public bool IsParticipantInRoom(string roomId, string participantId)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(participantId);

        return _rooms.TryGetValue(roomId, out ChatRoom? room) && room.IsParticipantMember(participantId);
    }

    /// <inheritdoc/>
    public bool TryGetParticipant(string roomId, string participantId, out ChatParticipant? participant)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(participantId);

        participant = null;

        if (!_rooms.TryGetValue(roomId, out ChatRoom? room))
        {
            return false;
        }

        return room.TryGetParticipant(participantId, out participant);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ChatParticipant> GetParticipants(string roomId)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        if (!_rooms.TryGetValue(roomId, out ChatRoom? room))
        {
            return Array.Empty<ChatParticipant>();
        }

        return room.SnapshotParticipants();
    }

    /// <summary>
    /// Orchestrates a join-room use-case command.
    /// </summary>
    public async ValueTask<JoinRoomResult> ExecuteJoinRoomAsync(
        JoinRoomCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        JoinRoomOutcome outcome = await this.JoinRoomAsync(
            command.RoomId,
            command.ParticipantId,
            command.DisplayName,
            cancellationToken).ConfigureAwait(false);

        return new JoinRoomResult(
            Succeeded: outcome.Succeeded,
            ErrorCode: outcome.ErrorCode,
            Message: outcome.Message,
            RoomId: outcome.Room?.RoomId ?? command.RoomId,
            ParticipantId: outcome.Participant?.ParticipantId ?? command.ParticipantId,
            DisplayName: outcome.Participant?.DisplayName ?? command.DisplayName,
            ClientRequestId: command.ClientRequestId);
    }

    /// <summary>
    /// Orchestrates a send-message use-case command.
    /// </summary>
    public async ValueTask<SendMessageResult> ExecuteSendMessageAsync(
        SendMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        SendMessageOutcome outcome = await this.SendMessageAsync(
            command.RoomId,
            command.ParticipantId,
            command.ClientMessageId,
            command.Content,
            cancellationToken).ConfigureAwait(false);

        ChatMessage? message = outcome.ChatMessage;

        return new SendMessageResult(
            Succeeded: outcome.Succeeded,
            ErrorCode: outcome.ErrorCode,
            Message: outcome.Message,
            RoomId: message?.RoomId ?? command.RoomId,
            SenderId: message?.SenderId ?? command.ParticipantId,
            SenderDisplayName: message?.SenderDisplayName ?? string.Empty,
            ClientMessageId: command.ClientMessageId,
            ServerMessageId: message?.ServerMessageId ?? 0,
            SentAtUnixMs: message?.SentAtUnixMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Content: message?.Content ?? command.Content);
    }
}
