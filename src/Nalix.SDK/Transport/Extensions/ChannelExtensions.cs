// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Channels;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides extension methods for subscribing to transport events via <see cref="System.Threading.Channels.Channel{T}"/>.
/// </summary>
public static class ChannelExtensions
{
    /// <summary>
    /// Subscribes to packets of type <typeparamref name="TEvent"/> and buffers them into a <see cref="ChannelReader{T}"/>.
    /// The subscription is automatically removed when <paramref name="cancellationToken"/> is cancelled or the client disconnects.
    /// </summary>
    /// <typeparam name="TEvent">The packet type to receive.</typeparam>
    /// <param name="client">The transport session to subscribe to.</param>
    /// <param name="boundedCapacity">If set, creates a bounded channel with the specified capacity. If null, creates an unbounded channel.</param>
    /// <param name="fullMode">The behavior to use when writing to a full bounded channel. Default is <see cref="BoundedChannelFullMode.DropOldest"/>.</param>
    /// <param name="cancellationToken">The token used to cancel the subscription and complete the channel.</param>
    /// <returns>A channel reader that asynchronously yields received packets.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="client"/> is null.</exception>
    public static ChannelReader<TEvent> SubscribeChannel<TEvent>(
        this TransportSession client, int? boundedCapacity = null,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.DropOldest,
        CancellationToken cancellationToken = default) where TEvent : class, IPacket, IPacketStaticOpcode
    {
        ArgumentNullException.ThrowIfNull(client);

        Channel<TEvent> channel;

        if (boundedCapacity.HasValue)
        {
            channel = Channel.CreateBounded<TEvent>(new BoundedChannelOptions(boundedCapacity.Value)
            {
                FullMode = fullMode,
                SingleWriter = false,
                SingleReader = true
            }, DisposePacket);
        }
        else
        {
            channel = Channel.CreateUnbounded<TEvent>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });
        }

#pragma warning disable CA2000 // Ownership is transferred to the cancellation token and disconnect handlers
        IDisposable msgSub = client.On<TEvent>(packet =>
        {
            _ = channel.Writer.TryWrite(packet);
        }, disposeAfter: false);
#pragma warning restore CA2000

        void OnDisconnected(object? sender, Exception ex)
        {
            msgSub.Dispose();
            _ = channel.Writer.TryComplete(ex ?? new NetworkException("Client disconnected unexpectedly during channel subscription."));
        }

        client.OnDisconnected += OnDisconnected;

        // Register cancellation to cleanup
        CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            msgSub.Dispose();
            client.OnDisconnected -= OnDisconnected;
            _ = channel.Writer.TryComplete(new OperationCanceledException(cancellationToken));
        });

        // Clean up the registration if the channel completes for another reason (like disconnect)
        // Note: CancellationTokenRegistration.Dispose is safe to call concurrently or multiple times.
        void CleanupRegistration(object? sender, Exception ex)
        {
            registration.Dispose();
            client.OnDisconnected -= CleanupRegistration;
        }

        client.OnDisconnected += CleanupRegistration;

        return channel.Reader;
    }

    private static void DisposePacket(IPacket packet)
    {
        if (packet is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
