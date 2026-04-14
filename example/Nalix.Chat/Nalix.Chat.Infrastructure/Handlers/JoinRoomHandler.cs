// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Chat.Contracts;
using Nalix.Chat.Contracts.Enums;
using Nalix.Chat.Contracts.Packets;
using Nalix.Chat.Domain.Services;
using Nalix.Common.Networking.Packets;
using Nalix.Common.Security;

namespace Nalix.Chat.Infrastructure.Handlers;

/// <summary>
/// Handles room-join requests.
/// </summary>
[PacketController("ChatJoin")]
public sealed class JoinRoomHandler
{
    /// <summary>
    /// Handles a room join packet.
    /// </summary>
    [PacketOpcode((ushort)ChatOpCode.JoinRoomRequest)]
    [PacketPermission(PermissionLevel.NONE)]
    [PacketEncryption]
    public static async ValueTask HandleAsync(IPacketContext<IPacket> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Packet is not JoinRoomRequest request)
        {
            return;
        }

        IChatRoomService? service = ChatHandlerCommon.ChatRoomService;
        if (service is null)
        {
            await context.Sender.SendAsync(new JoinRoomResponse
            {
                ClientRequestId = request.ClientRequestId,
                Succeeded = false,
                ErrorCode = ChatErrorCode.ServiceUnavailable,
                RoomId = request.RoomId,
                DisplayName = request.DisplayName,
                Message = "Chat service is unavailable."
            }, context.CancellationToken).ConfigureAwait(false);
            return;
        }

        if (!ChatHandlerCommon.IsAuthenticated(context.Connection))
        {
            JoinRoomResponse denied = new()
            {
                ClientRequestId = request.ClientRequestId,
                Succeeded = false,
                ErrorCode = ChatErrorCode.Unauthenticated,
                RoomId = request.RoomId,
                DisplayName = request.DisplayName,
                Message = "Handshake is required before joining rooms."
            };

            try
            {
                await context.Sender.SendAsync(denied, context.CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                context.Connection.Disconnect("authentication required");
            }

            return;
        }

        try
        {
            JoinRoomOutcome outcome = await service.JoinRoomAsync(
                request.RoomId,
                request.ParticipantId,
                request.DisplayName,
                context.CancellationToken).ConfigureAwait(false);

            JoinRoomResponse response = new()
            {
                ClientRequestId = request.ClientRequestId,
                Succeeded = outcome.Succeeded,
                ErrorCode = ChatHandlerCommon.ToTransportError(outcome.ErrorCode),
                RoomId = outcome.Room?.RoomId ?? request.RoomId,
                DisplayName = outcome.Participant?.DisplayName ?? request.DisplayName,
                Message = outcome.Message
            };

            await context.Sender.SendAsync(response, context.CancellationToken).ConfigureAwait(false);

            if (!outcome.Succeeded || outcome.Participant is null || outcome.Room is null)
            {
                return;
            }

            SessionResumeHandler? sessionResume = ChatHandlerCommon.SessionResumeHandler;
            sessionResume?.BindIdentity(
                context.Connection,
                outcome.Participant.ParticipantId,
                outcome.Room.RoomId,
                outcome.Participant.DisplayName);
        }
        catch (Exception ex)
        {
            ChatHandlerCommon.Logger?.LogError(ex, "Failed to process JoinRoomRequest for participant {ParticipantId}", request.ParticipantId);

            await context.Sender.SendAsync(new JoinRoomResponse
            {
                ClientRequestId = request.ClientRequestId,
                Succeeded = false,
                ErrorCode = ChatErrorCode.InfrastructureFailure,
                RoomId = request.RoomId,
                DisplayName = request.DisplayName,
                Message = "Unable to process join request."
            }, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
