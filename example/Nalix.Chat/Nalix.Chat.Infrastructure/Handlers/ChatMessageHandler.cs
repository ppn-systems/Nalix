// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Chat.Contracts;
using Nalix.Chat.Contracts.Enums;
using Nalix.Chat.Contracts.Events;
using Nalix.Chat.Contracts.Packets;
using Nalix.Chat.Domain.Services;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;
using Nalix.Framework.Time;
using System.IO;
using System.Net.Sockets;

namespace Nalix.Chat.Infrastructure.Handlers;

/// <summary>
/// Handles chat message send requests.
/// </summary>
[PacketController("ChatMessage")]
public sealed class ChatMessageHandler
{
    /// <summary>
    /// Handles a chat message packet.
    /// </summary>
    [PacketOpcode((ushort)ChatOpCode.ChatMessageRequest)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketEncryption]
    [PacketRateLimit(10, burst: 5)]
    public static async ValueTask HandleAsync(IPacketContext<IPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Packet is not ChatMessageRequest request)
        {
            return;
        }

        IChatRoomService? service = ChatHandlerCommon.ChatRoomService;
        if (service is null)
        {
            await SendAckAsync(context, request, false, ChatErrorCode.ServiceUnavailable, "Chat service is unavailable.", 0, Clock.UnixMillisecondsNow())
                .ConfigureAwait(false);
            return;
        }

        if (!ChatHandlerCommon.IsAuthenticated(context.Connection))
        {
            try
            {
                await SendAckAsync(context, request, false, ChatErrorCode.Unauthenticated, "Handshake is required before sending messages.", 0, Clock.UnixMillisecondsNow())
                    .ConfigureAwait(false);
            }
            finally
            {
                context.Connection.Disconnect("authentication required");
            }

            return;
        }

        SessionResumeHandler? sessionResume = ChatHandlerCommon.SessionResumeHandler;
        if (sessionResume is not null &&
            sessionResume.TryGetIdentity(context.Connection, out ChatSessionIdentity identity) &&
            !string.Equals(identity.ParticipantId, request.ParticipantId, StringComparison.Ordinal))
        {
            await SendAckAsync(context, request, false, ChatErrorCode.Unauthenticated, "Participant identity mismatch.", 0, Clock.UnixMillisecondsNow())
                .ConfigureAwait(false);
            context.Connection.Disconnect("participant identity mismatch");
            return;
        }

        try
        {
            SendMessageOutcome outcome = await service.SendMessageAsync(
                request.RoomId,
                request.ParticipantId,
                request.ClientMessageId,
                request.Message,
                context.CancellationToken).ConfigureAwait(false);

            ChatErrorCode errorCode = ChatHandlerCommon.ToTransportError(outcome.ErrorCode);
            long nowUnixMs = Clock.UnixMillisecondsNow();

            if (!outcome.Succeeded || outcome.ChatMessage is null)
            {
                await SendAckAsync(context, request, false, errorCode, outcome.Message, 0, nowUnixMs)
                    .ConfigureAwait(false);
                return;
            }

            await SendAckAsync(
                context,
                request,
                true,
                ChatErrorCode.None,
                outcome.Message,
                outcome.ChatMessage.ServerMessageId,
                outcome.ChatMessage.SentAtUnixMs).ConfigureAwait(false);

            ChatMessageBroadcast broadcast = new()
            {
                RoomId = outcome.ChatMessage.RoomId,
                ServerMessageId = outcome.ChatMessage.ServerMessageId,
                SenderId = outcome.ChatMessage.SenderId,
                SenderDisplayName = outcome.ChatMessage.SenderDisplayName,
                Message = outcome.ChatMessage.Content,
                ServerTimestampUnixMs = outcome.ChatMessage.SentAtUnixMs
            };

            await BroadcastToRoomAsync(
                broadcast,
                outcome.ChatMessage.RoomId,
                outcome.ChatMessage.SenderId,
                service,
                context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ChatHandlerCommon.Logger?.LogError(ex, "Failed to process ChatMessageRequest from participant {ParticipantId}", request.ParticipantId);

            await SendAckAsync(context, request, false, ChatErrorCode.InfrastructureFailure, "Unable to process message.", 0, Clock.UnixMillisecondsNow())
                .ConfigureAwait(false);
        }
    }

    private static async Task SendAckAsync(
        IPacketContext<IPacket> context,
        ChatMessageRequest request,
        bool accepted,
        ChatErrorCode errorCode,
        string message,
        long serverMessageId,
        long serverTimestampUnixMs)
    {
        ChatMessageAck ack = new()
        {
            ClientMessageId = request.ClientMessageId,
            ServerMessageId = serverMessageId,
            Accepted = accepted,
            ErrorCode = errorCode,
            Message = message,
            ServerTimestampUnixMs = serverTimestampUnixMs
        };

        await context.Sender.SendAsync(ack, context.CancellationToken).ConfigureAwait(false);
    }

    private static async Task BroadcastToRoomAsync(
        ChatMessageBroadcast broadcast,
        string roomId,
        string senderId,
        IChatRoomService chatRoomService,
        CancellationToken cancellationToken)
    {
        IConnectionHub? hub = ChatHandlerCommon.ConnectionHub;
        SessionResumeHandler? sessionResume = ChatHandlerCommon.SessionResumeHandler;

        if (hub is null || sessionResume is null)
        {
            return;
        }

        IReadOnlyCollection<IConnection> connections = hub.ListConnections();
        IReadOnlyCollection<Nalix.Chat.Domain.Users.ChatParticipant> participants = chatRoomService.GetParticipants(roomId);

        if (participants.Count == 0)
        {
            return;
        }

        HashSet<string> participantSet = new(StringComparer.Ordinal);
        foreach (Nalix.Chat.Domain.Users.ChatParticipant participant in participants)
        {
            _ = participantSet.Add(participant.ParticipantId);
        }

        foreach (IConnection target in connections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!sessionResume.TryGetIdentity(target, out ChatSessionIdentity identity))
            {
                continue;
            }

            if (!string.Equals(identity.RoomId, roomId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!participantSet.Contains(identity.ParticipantId))
            {
                continue;
            }

            if (string.Equals(identity.ParticipantId, senderId, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                await target.TCP.SendAsync(broadcast, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (IOException ex)
            {
                ChatHandlerCommon.Logger?.LogError(ex, "Failed to broadcast message to participant {ParticipantId}", identity.ParticipantId);
            }
            catch (SocketException ex)
            {
                ChatHandlerCommon.Logger?.LogError(ex, "Failed to broadcast message to participant {ParticipantId}", identity.ParticipantId);
            }
            catch (ObjectDisposedException ex)
            {
                ChatHandlerCommon.Logger?.LogError(ex, "Failed to broadcast message to participant {ParticipantId}", identity.ParticipantId);
            }
        }
    }
}
