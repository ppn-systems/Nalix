// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Networking.Packets;
using Nalix.SDK.Transport.Extensions;

#if DEBUG
[assembly: InternalsVisibleTo("Nalix.SDK.Tests")]
#endif

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Internal helper that encapsulates the recurring boilerplate shared by all
/// "subscribe -> await matching packet -> timeout -> unsubscribe" operations.
/// </summary>
internal static class PacketAwaiter
{
    /// <summary>
    /// Subscribes for a matching packet, invokes <paramref name="sendAsync"/>,
    /// and waits until the first packet of type <typeparamref name="TPkt"/> that
    /// satisfies <paramref name="predicate"/> arrives — or the operation times out / is canceled.
    /// </summary>
    /// <typeparam name="TPkt"></typeparam>
    /// <param name="client"></param>
    /// <param name="predicate"></param>
    /// <param name="timeoutMs"></param>
    /// <param name="sendAsync"></param>
    /// <param name="ct"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="TimeoutException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task<TPkt> AwaitAsync<TPkt>(
        TransportSession client, Func<TPkt, bool> predicate,
        int timeoutMs, Func<CancellationToken, Task> sendAsync, CancellationToken ct)
        where TPkt : class, IPacket
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(sendAsync);

        if (timeoutMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "timeoutMs must be >= 0 (0 = infinite)");
        }

        TaskCompletionSource<TPkt> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (timeoutMs > 0)
        {
            linkedCts.CancelAfter(timeoutMs);
        }

        using CancellationTokenRegistration registration = linkedCts.Token.Register(() =>
        {
            try { _ = tcs.TrySetCanceled(linkedCts.Token); } catch { }
        });

        using IDisposable subscription = client.OnOnce<TPkt>(
            predicate: packet =>
            {
                try
                {
                    return predicate(packet);
                }
                catch (Exception ex)
                {
                    _ = tcs.TrySetException(ex);
                    return false;
                }
            },
            handler: packet =>
            {
                _ = tcs.TrySetResult(packet);
            },
            disposeAfter: false);

        IDisposable disconnectSub = client.SubscribeTemp<TPkt>(
            onMessage: _ => { },
            onDisconnected: ex =>
            {
                Exception error = new Common.Exceptions.NetworkException(
                    $"Disconnected while waiting for {typeof(TPkt).Name}.",
                    ex ?? new InvalidOperationException("The TCP session was disconnected."));

                try { _ = tcs.TrySetException(error); } catch { }
            });

        using CompositeSubscription composite = client.Subscribe(subscription, disconnectSub);

        try
        {
            await sendAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (Exception sendEx)
        {
            if (sendEx is InvalidOperationException)
            {
                Exception wrapped = new Common.Exceptions.NetworkException(
                    $"Disconnected while sending {typeof(TPkt).Name}.", sendEx);

                try { _ = tcs.TrySetException(wrapped); } catch { }
                throw wrapped;
            }

            try { _ = tcs.TrySetException(sendEx); } catch { }
            throw;
        }

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"No {typeof(TPkt).Name} received within {timeoutMs} ms.");
        }
    }
}
