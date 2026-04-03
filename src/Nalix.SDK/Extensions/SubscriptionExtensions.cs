// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Abstractions;
using Nalix.SDK.Transport;

namespace Nalix.SDK.Extensions;

/// <summary>
/// Provides extension methods for subscribing to <see cref="TransportSession"/> events with automatic unsubscription.
/// </summary>
public static class SubscriptionExtensions
{
    /// <summary>
    /// Subscribes to <see cref="TransportSession.OnMessageReceived"/> and <see cref="TransportSession.OnDisconnected"/>.
    /// </summary>
    /// <param name="this">The transport session to subscribe to.</param>
    /// <param name="onMessageReceived">The action to invoke when a packet is received.</param>
    /// <param name="onDisconnected">The action to invoke when the session disconnects.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscriptions when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="this"/> is null.</exception>
    public static IDisposable SubscribeTemp(this TransportSession @this, EventHandler<IBufferLease> onMessageReceived, EventHandler<Exception> onDisconnected)
    {
        ArgumentNullException.ThrowIfNull(@this);

        @this.OnMessageReceived += onMessageReceived;
        @this.OnDisconnected += onDisconnected;

        return new Unsubscriber(@this, onMessageReceived, onDisconnected);
    }

    /// <summary>
    /// Handles unsubscription from <see cref="TransportSession"/> events.
    /// </summary>
    /// <param name="c">The transport session being tracked.</param>
    /// <param name="p">The message-received handler to remove.</param>
    /// <param name="d">The disconnected handler to remove.</param>
    private sealed class Unsubscriber(TransportSession c, EventHandler<IBufferLease> p, EventHandler<Exception> d) : IDisposable
    {
        private readonly TransportSession _client = c;
        private readonly EventHandler<IBufferLease> _messageReceived = p;
        private readonly EventHandler<Exception> _disconnect = d;

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.OnDisconnected -= _disconnect;
            _client.OnMessageReceived -= _messageReceived;
        }
    }
}
