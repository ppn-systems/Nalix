// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Infrastructure.Caching;
using Nalix.Common.Infrastructure.Client;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for subscribing to <see cref="IReliableClient"/> events with automatic unsubscription.
/// </summary>
public static class SubscriptionExtensions
{
    /// <summary>
    /// Subscribes to the <see cref="IReliableClient.OnMessageReceived"/> and <see cref="IReliableClient.OnDisconnected"/> events.
    /// Returns an <see cref="System.IDisposable"/> that unsubscribes when disposed.
    /// </summary>
    /// <param name="this">The reliable client to subscribe to.</param>
    /// <param name="onMessageReceived">The action to invoke when a packet is received.</param>
    /// <param name="onDisconnected">The action to invoke when the client is disconnected.</param>
    /// <returns>An <see cref="System.IDisposable"/> that unsubscribes the handlers when disposed.</returns>
    public static System.IDisposable SubscribeTemp(
        this IReliableClient @this,
        System.EventHandler<IBufferLease> onMessageReceived,
        System.EventHandler<System.Exception> onDisconnected)
    {
        @this.OnMessageReceived += onMessageReceived;
        @this.OnDisconnected += onDisconnected;

        return new Unsubscriber(@this, onMessageReceived, onDisconnected);
    }

    /// <summary>
    /// Handles unsubscription from <see cref="IReliableClient"/> events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Unsubscriber"/> class.
    /// </remarks>
    /// <param name="c">The reliable client.</param>
    /// <param name="p">The packet received handler.</param>
    /// <param name="d">The disconnected handler.</param>
    private sealed class Unsubscriber(IReliableClient c, System.EventHandler<IBufferLease> p, System.EventHandler<System.Exception> d) : System.IDisposable
    {
        private readonly IReliableClient _client = c;
        private readonly System.EventHandler<IBufferLease> _messageReceived = p;
        private readonly System.EventHandler<System.Exception> _disconnect = d;

        public void Dispose()
        {
            _client.OnDisconnected -= _disconnect;
            _client.OnMessageReceived -= _messageReceived;
        }
    }
}
