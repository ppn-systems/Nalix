// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Nalix.Common.Abstractions;
using Nalix.Common.Networking.Transport;

namespace Nalix.Framework.Extensions;

/// <summary>
/// Provides extension methods for subscribing to <see cref="IClientConnection"/> events with automatic unsubscription.
/// </summary>
public static class SubscriptionExtensions
{
    /// <summary>
    /// Subscribes to the <see cref="IClientConnection.OnMessageReceived"/> and <see cref="IClientConnection.OnDisconnected"/> events.
    /// Returns an <see cref="IDisposable"/> that unsubscribes when disposed.
    /// </summary>
    /// <param name="this">The reliable client to subscribe to.</param>
    /// <param name="onMessageReceived">The action to invoke when a packet is received.</param>
    /// <param name="onDisconnected">The action to invoke when the client is disconnected.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handlers when disposed.</returns>
    public static IDisposable SubscribeTemp(
        this IClientConnection @this,
        EventHandler<IBufferLease> onMessageReceived,
        EventHandler<Exception> onDisconnected)
    {
        @this.OnMessageReceived += onMessageReceived;
        @this.OnDisconnected += onDisconnected;

        return new Unsubscriber(@this, onMessageReceived, onDisconnected);
    }

    /// <summary>
    /// Handles unsubscription from <see cref="IClientConnection"/> events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Unsubscriber"/> class.
    /// </remarks>
    /// <param name="c">The reliable client.</param>
    /// <param name="p">The packet received handler.</param>
    /// <param name="d">The disconnected handler.</param>
    private sealed class Unsubscriber(IClientConnection c, EventHandler<IBufferLease> p, EventHandler<Exception> d) : IDisposable
    {
        private readonly IClientConnection _client = c;
        private readonly EventHandler<IBufferLease> _messageReceived = p;
        private readonly EventHandler<Exception> _disconnect = d;

        public void Dispose()
        {
            _client.OnDisconnected -= _disconnect;
            _client.OnMessageReceived -= _messageReceived;
        }
    }
}
