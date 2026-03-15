// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Chat.Contracts.Enums;
using Nalix.Chat.Domain.Services;
using Nalix.Common.Networking;
using Nalix.Framework.Injection;

namespace Nalix.Chat.Infrastructure.Handlers;

internal static class ChatHandlerCommon
{
    public static ILogger? Logger => InstanceManager.Instance.GetExistingInstance<ILogger>();

    public static IChatRoomService? ChatRoomService => InstanceManager.Instance.GetExistingInstance<IChatRoomService>();

    public static IConnectionHub? ConnectionHub => InstanceManager.Instance.GetExistingInstance<IConnectionHub>();

    public static SessionResumeHandler? SessionResumeHandler => InstanceManager.Instance.GetExistingInstance<SessionResumeHandler>();

    public static bool IsAuthenticated(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return connection.Attributes.TryGetValue(ConnectionAttributes.HandshakeEstablished, out object? raw)
            && raw is bool established
            && established;
    }

    public static ChatErrorCode ToTransportError(ChatDomainErrorCode errorCode)
    {
        return errorCode switch
        {
            ChatDomainErrorCode.None => ChatErrorCode.None,
            ChatDomainErrorCode.ValidationFailed => ChatErrorCode.ValidationFailed,
            ChatDomainErrorCode.RoomNotFound => ChatErrorCode.RoomNotFound,
            ChatDomainErrorCode.NotRoomMember => ChatErrorCode.NotRoomMember,
            ChatDomainErrorCode.RoomCapacityReached => ChatErrorCode.RoomCapacityReached,
            ChatDomainErrorCode.MessageRejected => ChatErrorCode.MessageRejected,
            ChatDomainErrorCode.AlreadyInRoom => ChatErrorCode.None,
            _ => ChatErrorCode.InfrastructureFailure
        };
    }
}
