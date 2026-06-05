// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Codec.Pooling;

namespace Nalix.Tunneling.Handlers;

/// <summary>
/// Handles the TunnelConnect packet from the Consumer.
/// </summary>
[PacketController("Nalix.Tunneling")]
public sealed class ConsumerHandler
{
    private readonly ILogger _logger;
    private readonly TunnelOptions _options;
    private readonly TunnelRegistry _tunnelRegistry;
    private readonly ProviderRegistry _providerRegistry;
    private readonly TunnelSessionRegistry _sessionRegistry;

    public ConsumerHandler(ProviderRegistry providerRegistry, TunnelRegistry tunnelRegistry, TunnelSessionRegistry sessionRegistry, TunnelOptions options, ILogger logger)
    {
        _logger = logger;
        _options = options;
        _tunnelRegistry = tunnelRegistry;
        _sessionRegistry = sessionRegistry;
        _providerRegistry = providerRegistry;
    }

    [PacketOpcode(ProtocolOpCode.TUNNEL_CONNECT)]
    public async ValueTask HandleConnectAsync(IPacketContext<TunnelConnect> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ushort channelId = context.Packet.ChannelId;
        IConnection consumerConnection = context.Connection;

        // 1. Find the Provider for this channel
        if (!_providerRegistry.TryGetProvider(channelId, out IConnection? providerControlConnection, out IPacketSender? providerSender))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("[Tunneling.ConsumerHandler] consumer={ConnectionId} channel={ChannelId} error=no-provider-registered", consumerConnection.ID, channelId);
            }

            await SendAsync(context, false, 1).ConfigureAwait(false); // 1 = ChannelNotFound
            return;
        }

        // 2. Register a pending tunnel to get a token
        (Task<IConnection>? providerTask, Abstractions.Primitives.Bytes32 token) = _tunnelRegistry.Register();

        // 3. Request the Provider to open a new data connection
        using (PacketScope<TunnelRequest> requestScope = PacketFactory<TunnelRequest>.Acquire())
        {
            TunnelRequest request = requestScope.Value;
            request.Token = token;

            // Forward the request to the provider
            await providerSender.SendAsync(request).ConfigureAwait(false);
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(
                "[Tunneling.ConsumerHandler] requested-data-connection provider={ProviderId} consumer={ConsumerId} channel={ChannelId}",
                providerControlConnection.ID, consumerConnection.ID, channelId);
        }

        // 4. Wait for the Provider to establish the new data connection
        IConnection? providerDataConnection;
        try
        {
            // Timeout to prevent hanging tasks if provider ignores the request
            providerDataConnection = await providerTask.WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "[Tunneling.ConsumerHandler] data-connection-timeout provider={ProviderId} channel={ChannelId}", providerControlConnection.ID, channelId);
            }

            await SendAsync(context, false, 2).ConfigureAwait(false); // 2 = ProviderTimeout
            return;
        }

        // 5. Send success ACK to consumer
        await SendAsync(context, true, 0).ConfigureAwait(false);

        // 6. Create TunnelSession and start piping
        TunnelSession session = new(consumerConnection, _options, _sessionRegistry, _logger);
        try
        {
            _sessionRegistry.Register(consumerConnection.ID.ToUInt64(), session);
            session.StartTunnel(providerDataConnection);
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(ex, "[Tunneling.ConsumerHandler] tunnel-session-start-error consumer={ConsumerId}", consumerConnection.ID);
            }

            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask SendAsync(IPacketContext<TunnelConnect> context, bool success, byte reason)
    {
        using PacketScope<TunnelConnectAck> scope = PacketFactory<TunnelConnectAck>.Acquire();
        TunnelConnectAck ack = scope.Value;
        ack.Success = success;
        ack.Reason = reason;

        await context.Sender.SendAsync(ack).ConfigureAwait(false);
    }
}

