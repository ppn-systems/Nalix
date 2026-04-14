// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Chat.Application.Services;
using Nalix.Chat.Contracts.Events;
using Nalix.Chat.Contracts.Packets;
using Nalix.Chat.Domain.Policies;
using Nalix.Chat.Domain.Rules;
using Nalix.Chat.Domain.Services;
using Nalix.Chat.Infrastructure.Handlers;
using Nalix.Chat.Infrastructure.Protocols;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.SignalFrames;
using Nalix.Framework.Injection;
using Nalix.Logging;
using Nalix.Logging.Options;
using Nalix.Logging.Sinks;
using Nalix.Network.Connections;
using Nalix.Network.Hosting;
using Nalix.Network.Options;

namespace Nalix.Chat.Infrastructure.Hosting;

/// <summary>
/// Bootstraps and runs the Nalix chat server.
/// </summary>
public sealed class ChatServerHost
{
    private const ushort DefaultPort = 57216;

    private readonly ILogger _logger;
    private readonly IChatRoomService _chatRoomService;
    private readonly SessionResumeHandler _sessionResumeHandler;
    private readonly ConnectionHub _connectionHub;

    private ChatServerHost(
        ILogger logger,
        IChatRoomService chatRoomService,
        SessionResumeHandler sessionResumeHandler,
        ConnectionHub connectionHub)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatRoomService = chatRoomService ?? throw new ArgumentNullException(nameof(chatRoomService));
        _sessionResumeHandler = sessionResumeHandler ?? throw new ArgumentNullException(nameof(sessionResumeHandler));
        _connectionHub = connectionHub ?? throw new ArgumentNullException(nameof(connectionHub));
    }

    /// <summary>
    /// Creates a host with default dependencies and options.
    /// </summary>
    public static ChatServerHost CreateDefault()
    {
        ConfigurationManager.Instance.Get<NLogixOptions>()
                    .MinLevel = LogLevel.Trace;

        ILogger logger = new NLogix(cfg => cfg.RegisterTarget(new BatchConsoleLogTarget(t => t.EnableColors = true)));

        RoomCapacityRule capacityRule = new(maxParticipants: 512);
        MessageModerationPolicy moderationPolicy = new(maxLength: 1024);

        IChatRoomService chatRoomService = new ChatRoomService(capacityRule, moderationPolicy);
        SessionResumeHandler sessionResumeHandler = new();
        ConnectionHub hub = new(logger: logger);

        return new ChatServerHost(logger, chatRoomService, sessionResumeHandler, hub);
    }

    /// <summary>
    /// Builds a configured network application instance.
    /// </summary>
    public NetworkApplication Build()
    {
        InstanceManager.Instance.Register(_sessionResumeHandler);
        InstanceManager.Instance.Register<IChatRoomService>(_chatRoomService);

        return NetworkApplication.CreateBuilder()
            .ConfigureLogging(_logger)
            .ConfigureConnectionHub(_connectionHub)
            .Configure<NetworkSocketOptions>(options =>
            {
                options.Port = DefaultPort;
                options.Backlog = 2048;
            })
            .AddPacket<JoinRoomRequest>()
            .AddPacket<JoinRoomResponse>()
            .AddPacket<ChatMessageRequest>()
            .AddPacket<ChatMessageAck>()
            .AddPacket<ChatMessageBroadcast>()
            .AddPacket<Handshake>()
            .AddPacket<SessionResume>()
            .AddPacket<Control>()
            .AddHandler<JoinRoomHandler>()
            .AddHandler<ChatMessageHandler>()
            .AddTcp<ChatPacketProtocol>()
            .Build();
    }

    /// <summary>
    /// Runs the host until cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using NetworkApplication host = this.Build();

        _logger.LogInformation("Nalix.Chat server is running on tcp://127.0.0.1:{Port}", DefaultPort);
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
