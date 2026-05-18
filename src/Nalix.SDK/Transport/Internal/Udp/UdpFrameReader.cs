// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Codec.Transforms;
using Nalix.Environment.Memory;
using Nalix.Environment.Sequencing;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal.Udp;

/// <summary>
/// Handles receiving and processing UDP datagrams.
/// Responsible for: buffer management, inbound pipeline (decrypt + decompress), 
/// and dispatching to <see cref="UdpSession"/>.
/// </summary>
internal sealed class UdpFrameReader : IDisposable
{
    private readonly Func<Socket> _getSocket;
    private readonly SequenceCounter _sequence;
    private readonly TransportOptions _options;
    private readonly Action<Exception> _onError;
    private readonly Action<IBufferLease> _onMessageReceived;
    private readonly Func<ReadOnlyMemory<byte>, Task>? _onMessageAsync;

    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpFrameReader"/> class.
    /// </summary>
    /// <param name="getSocket">Delegate to get current socket.</param>
    /// <param name="options">Transport options.</param>
    /// <param name="onMessageReceived">Sync callback for <see cref="UdpSession.OnMessageReceived"/>.</param>
    /// <param name="onMessageAsync">Async callback for <see cref="UdpSession.OnMessageAsync"/>.</param>
    /// <param name="onError">Error callback.</param>
    public UdpFrameReader(
        Func<Socket> getSocket,
        TransportOptions options,
        Action<IBufferLease> onMessageReceived,
        Func<ReadOnlyMemory<byte>, Task>? onMessageAsync,
        Action<Exception> onError)
    {
        _sequence = new SequenceCounter();
        _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onMessageReceived = onMessageReceived ?? throw new ArgumentNullException(nameof(onMessageReceived));
        _onMessageAsync = onMessageAsync;
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
    }

    /// <summary>
    /// Main receive loop for UDP datagrams.
    /// </summary>
    public async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        Socket? socket = _getSocket();
        if (socket == null)
        {
            return;
        }

        int bufferSize = _options.MaxUdpDatagramSize;
        byte[]? rawBuffer = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                rawBuffer = BufferLease.ByteArrayPool.Rent(bufferSize);

                try
                {
                    int received = await socket.ReceiveAsync(rawBuffer, SocketFlags.None, cancellationToken)
                                               .ConfigureAwait(false);

                    if (received <= 0)
                    {
                        continue;
                    }

                    await this.ProcessDatagramAsync(rawBuffer, received, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _onError(ex);
                    }
                    break;
                }
                finally
                {
                    if (rawBuffer != null)
                    {
                        BufferLease.ByteArrayPool.Return(rawBuffer);
                        rawBuffer = null;
                    }
                }
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            _onError(ex);
        }
    }

    private async Task ProcessDatagramAsync(byte[] rawBuffer, int received, CancellationToken ct)
    {
        IBufferLease datagram = BufferLease.TakeOwnership(rawBuffer, 0, received);
        IBufferLease original = datagram;

        try
        {
            // Inbound pipeline: Decrypt -> Decompress
            FramePipeline.ProcessInbound(ref datagram, _options.Secret.AsSpan(), _options.Algorithm, out uint? seq);

            if (!_sequence.IsValid(seq, window: 64))
            {
                return; // Duplicate datagram, already processed
            }

            // Dispatch
            await this.DispatchMessageAsync(datagram, ct).ConfigureAwait(false);

            if (seq.HasValue)
            {
                _sequence.UpdateTo(seq.Value);
            }
        }
        finally
        {
            if (!ReferenceEquals(datagram, original))
            {
                datagram.Dispose();
            }
            original.Dispose();
        }
    }

    private async Task DispatchMessageAsync(IBufferLease lease, CancellationToken _)
    {
        // Sync handler (hot path)
        _onMessageReceived?.Invoke(lease);

        if (_onMessageAsync is not null)
        {
            lease.Retain();

            try
            {
                await _onMessageAsync(lease.Memory).ConfigureAwait(false);
            }
            catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
            {
                _onError(ex);
            }
            finally
            {
                lease.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }
    }
}
