// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System.Net.Sockets;
using Nalix.Abstractions.Exceptions;
using Nalix.Environment.Memory;
using Nalix.Tunneling.Internal;

namespace Nalix.Tunneling;

/// <summary>
/// Provides high-performance bi-directional socket piping for tunneling.
/// </summary>
public static class TunnelPipe
{
    /// <summary>
    /// Pipes data bi-directionally between two raw sockets until one or both are closed.
    /// </summary>
    /// <param name="client">The client socket.</param>
    /// <param name="backend">The backend server socket.</param>
    /// <param name="options">The tunnel options, containing bandwidth and buffer settings.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the completed piping operation.</returns>
    public static async Task StartAsync(
        Socket client, Socket backend,
        TunnelOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(options);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        long bandwidth = options.MaxBytesPerSecond;
        int bufferSize = options.BufferSize <= 0 ? 8192 : options.BufferSize;

        Task toBackend = PumpAsync(client, backend, bandwidth, bufferSize, cts.Token);
        Task toClient = PumpAsync(backend, client, bandwidth, bufferSize, cts.Token);

        try
        {
            _ = await Task.WhenAny(toBackend, toClient).ConfigureAwait(false);
        }
        finally
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(toBackend, toClient).ConfigureAwait(false);

            CloseSocketSafe(client);
            CloseSocketSafe(backend);
        }
    }

    private static async Task PumpAsync(
        Socket source,
        Socket destination,
        long maxBytesPerSecond,
        int bufferSize,
        CancellationToken cancellationToken)
    {
        byte[] buffer = BufferLease.ByteArrayPool.Rent(bufferSize);
        TokenBucket? bucket = maxBytesPerSecond > 0
            ? new TokenBucket(maxBytesPerSecond, maxBytesPerSecond)
            : null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (bucket != null)
                {
                    await bucket.ConsumeOrWaitAsync(1, cancellationToken).ConfigureAwait(false);
                }

                int received = await source.ReceiveAsync(
                    buffer.AsMemory(),
                    SocketFlags.None,
                    cancellationToken).ConfigureAwait(false);

                if (received <= 0)
                {
                    break;
                }

                if (bucket != null)
                {
                    // Since we already consumed 1 token above to block, we consume the rest
                    int remainingToConsume = received - 1;
                    if (remainingToConsume > 0)
                    {
                        await bucket.ConsumeOrWaitAsync(remainingToConsume, cancellationToken).ConfigureAwait(false);
                    }
                }

                int sent = await SendAsync(
                    destination,
                    buffer.AsMemory(0, received),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        finally
        {
            BufferLease.ByteArrayPool.Return(buffer);
        }
    }

    private static async ValueTask<int> SendAsync(
        Socket socket,
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int totalSent = 0;

        while (totalSent < buffer.Length)
        {
            int sent = await socket.SendAsync(
                buffer[totalSent..],
                SocketFlags.None,
                cancellationToken).ConfigureAwait(false);

            if (sent <= 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            totalSent += sent;
        }

        return totalSent;
    }

    private static void CloseSocketSafe(Socket socket)
    {
        try
        {
            if (socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
        finally
        {
            try { socket.Close(); }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex)) { }
        }
    }
}
