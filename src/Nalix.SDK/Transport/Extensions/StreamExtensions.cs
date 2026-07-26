// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Packets;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Extensions;

/// <summary>
/// Provides streaming extension methods for <see cref="TransportSession"/> allowing packet multiplexing.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Sends a request and returns an asynchronous stream of responses.
    /// The stream completes automatically when a response with <see cref="IPacketStreamable.IsEndOfStream"/> is received.
    /// </summary>
    /// <typeparam name="TResponse">The expected type of the response chunks.</typeparam>
    /// <param name="client">The connected client session.</param>
    /// <param name="request">The request packet initiating the stream.</param>
    /// <param name="options">Options for the request (e.g., encryption).</param>
    /// <param name="ct">The cancellation token to cancel the stream.</param>
    /// <returns>An asynchronous enumerable stream of responses.</returns>
    /// <exception cref="ArgumentNullException">Thrown if client or request is null.</exception>
    /// <exception cref="NetworkException">Thrown if the client is not connected.</exception>
    public static async IAsyncEnumerable<TResponse> StreamAsync<TResponse>(
        this TransportSession client,
        IPacket request,
        RequestOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
        where TResponse : class, IPacket, IPacketStaticOpcode, IPacketStreamable
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        if (!client.IsConnected)
        {
            throw new NetworkException($"[SDK.StreamAsync<{typeof(TResponse).Name}>] Client is not connected.");
        }

        options ??= RequestOptions.Default;

        ushort expectedSeqId = request.Header.SequenceId;

        // Use Unbounded channel to prevent blocking the network reader thread.
        // If the consumer is too slow, memory will grow, which is the standard behavior for downloading streams.
        Channel<TResponse> channel = Channel.CreateUnbounded<TResponse>(new UnboundedChannelOptions
        {
            SingleWriter = false, // Network reader threads might be multiple depending on transport implementation
            SingleReader = true   // Only one consumer iterates the foreach loop
        });

        void OnMessageReceived(TResponse chunk)
        {
            if (chunk.Header.SequenceId != expectedSeqId)
            {
                DisposePacket(chunk);
                return;
            }

            // Write the chunk to the channel
            if (!channel.Writer.TryWrite(chunk))
            {
                DisposePacket(chunk);
                return;
            }

            // If this is the final chunk, complete the channel
            if (chunk.IsEndOfStream)
            {
                _ = channel.Writer.TryComplete();
            }
        }

        void OnDisconnected(object? sender, Exception ex) => channel.Writer.TryComplete(ex ?? new NetworkException("Client disconnected unexpectedly during stream."));

        IDisposable msgSub = client.On<TResponse>(OnMessageReceived, disposeAfter: false);
        client.OnDisconnected += OnDisconnected;

        try
        {
            // Send the request
            await client.SendAsync(request, encrypt: options.Encrypt, ct: ct).ConfigureAwait(false);

            // Yield the stream elements as they arrive
            await foreach (TResponse item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            msgSub.Dispose();
            client.OnDisconnected -= OnDisconnected;

            // Ensure the channel is completed if we exit early (e.g. cancellation)
            _ = channel.Writer.TryComplete();
        }
    }

    private static void DisposePacket(IPacket packet)
    {
        if (packet is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
