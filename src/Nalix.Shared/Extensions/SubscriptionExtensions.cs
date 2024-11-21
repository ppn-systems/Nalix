// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Client;
using Nalix.Common.Packets.Abstractions;

namespace Nalix.Shared.Extensions;

/// <summary>
/// Provides extension methods for subscribing to <see cref="IReliableClient"/> events with automatic unsubscription.
/// </summary>
public static class SubscriptionExtensions
{
    /// <summary>
    /// Subscribes to the <see cref="IReliableClient.PacketReceived"/> and <see cref="IReliableClient.Disconnected"/> events.
    /// Returns an <see cref="System.IDisposable"/> that unsubscribes when disposed.
    /// </summary>
    /// <param name="client">The reliable client to subscribe to.</param>
    /// <param name="onPacket">The action to invoke when a packet is received.</param>
    /// <param name="onDisconnected">The action to invoke when the client is disconnected.</param>
    /// <returns>An <see cref="System.IDisposable"/> that unsubscribes the handlers when disposed.</returns>
    public static System.IDisposable SubscribeTemp(
        this IReliableClient client,
        System.Action<IPacket> onPacket,
        System.Action<System.Exception> onDisconnected)
    {
        client.PacketReceived += onPacket;
        client.Disconnected += onDisconnected;

        return new Unsubscriber(client, onPacket, onDisconnected);
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
    private sealed class Unsubscriber(IReliableClient c, System.Action<IPacket> p, System.Action<System.Exception> d) : System.IDisposable
    {
        private readonly IReliableClient _client = c;
        private readonly System.Action<IPacket> _packet = p;
        private readonly System.Action<System.Exception> _disconnect = d;

        public void Dispose()
        {
            _client.PacketReceived -= _packet;
            _client.Disconnected -= _disconnect;
        }
    }
}
