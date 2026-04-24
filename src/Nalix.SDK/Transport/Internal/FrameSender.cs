// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Common.Abstractions;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Optimized frame sender that serializes outbound frames using a SemaphoreSlim.
/// This implementation provides lower latency than channel-based senders by eliminating
/// the intermediate task and TaskCompletionSource overhead.
/// </summary>
internal sealed class FrameSender : IDisposable
{
    #region Fields

    private readonly TransportOptions _options;
    private readonly FragmentOptions _fragmentOptions;
    private readonly Func<Socket> _getSocket;
    private readonly Action<Exception> _onError;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _disposed;

    #endregion Fields

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameSender"/> class.
    /// </summary>
    /// <param name="getSocket">A delegate that returns the active socket used for sending data.</param>
    /// <param name="options">The transport options that control queue capacity and frame behavior.</param>
    /// <param name="onError">The callback invoked when send processing encounters an error.</param>
    public FrameSender(Func<Socket> getSocket, TransportOptions options, Action<Exception> onError)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();
        _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
    }

    /// <summary>
    /// Queues a payload for sending after applying outbound compression and encryption transforms.
    /// </summary>
    /// <param name="payload">The payload to frame and send.</param>
    /// <param name="encrypt">An optional encryption override. When <see langword="null"/>, the sender uses the configured default.</param>
    /// <param name="ct">A cancellation token used to abort queueing or sending.</param>
    /// <returns><see langword="true"/> when the frame is sent successfully; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> SendAsync(ReadOnlyMemory<byte> payload, bool? encrypt = null, CancellationToken ct = default)
    {
        using IBufferLease lease = BufferLease.CopyFrom(payload.Span);
        return await this.SendAsync(lease, encrypt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a payload using an existing <see cref="IBufferLease"/>.
    /// The sender takes ownership of the lease and will dispose it after the frame is sent.
    /// </summary>
    /// <param name="lease">The lease containing the payload to send.</param>
    /// <param name="encrypt">An optional encryption override.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/> when the frame is sent successfully; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> SendAsync([Borrowed] IBufferLease lease, bool? encrypt = null, CancellationToken ct = default)
    {
        IBufferLease current = lease;
        try
        {
            Framework.DataFrames.Transforms.FramePipeline.ProcessOutbound(
                ref current,
                _options.CompressionEnabled,
                _options.CompressionThreshold,
                encrypt ?? _options.EncryptionEnabled,
                _options.Secret.AsSpan(),
                _options.Algorithm);

            if (current.Length >= _fragmentOptions.MaxChunkSize)
            {
                return await this.SEND_FRAGMENTED_ASYNC(current.Memory, ct).ConfigureAwait(false);
            }

            int totalLen = TcpSession.HeaderSize + current.Length;
            byte[] frame = BufferLease.ByteArrayPool.Rent(totalLen);
            try
            {
                BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, TcpSession.HeaderSize), (ushort)totalLen);
                current.Memory.Span.CopyTo(frame.AsSpan(TcpSession.HeaderSize, current.Length));

                return await this.SEND_RAW_ASYNC(frame, totalLen, ct).ConfigureAwait(false);
            }
            finally
            {
                BufferLease.ByteArrayPool.Return(frame);
            }
        }
        catch (Exception ex) when (Common.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            _onError?.Invoke(ex);
            return false;
        }
        finally
        {
            if (!ReferenceEquals(current, lease))
            {
                current.Dispose();
            }
        }
    }

    #region Private Methods

    private async Task<bool> SEND_RAW_ASYNC(byte[] frame, int frameLen, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Socket s = _getSocket();
            int sent = 0;
            while (sent < frameLen)
            {
                int n = await s.SendAsync(new ReadOnlyMemory<byte>(frame, sent, frameLen - sent), SocketFlags.None, ct).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }

                sent += n;
            }
            return true;
        }
        finally
        {
            _ = _sendLock.Release();
        }
    }

    private async Task<bool> SEND_FRAGMENTED_ASYNC(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (payload.Length > _fragmentOptions.MaxPayloadSize)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), $"Payload exceeds MaxPayloadSize {_fragmentOptions.MaxPayloadSize}");
        }

        ushort streamId = FragmentStreamId.Next();
        int chunkBodySize = _fragmentOptions.MaxChunkSize;
        int totalChunks = (payload.Length + chunkBodySize - 1) / chunkBodySize;

        byte[] headerSpan = new byte[FragmentHeader.WireSize];

        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * chunkBodySize;
            int chunkLen = Math.Min(chunkBodySize, payload.Length - offset);
            bool isLast = i == totalChunks - 1;

            FragmentHeader fragHeader = new(streamId, (ushort)i, (ushort)totalChunks, isLast);
            fragHeader.WriteTo(headerSpan);

            int totalFrameLen = TcpSession.HeaderSize + FragmentHeader.WireSize + chunkLen;
            byte[] frame = BufferLease.ByteArrayPool.Rent(totalFrameLen);

            try
            {
                BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)totalFrameLen);
                headerSpan.CopyTo(frame.AsSpan(TcpSession.HeaderSize));
                payload.Slice(offset, chunkLen).Span.CopyTo(frame.AsSpan(TcpSession.HeaderSize + FragmentHeader.WireSize));

                bool sent = await this.SEND_RAW_ASYNC(frame, totalFrameLen, ct).ConfigureAwait(false);
                if (!sent)
                {
                    return false;
                }
            }
            finally
            {
                BufferLease.ByteArrayPool.Return(frame);
            }
        }

        return true;
    }

    #endregion Private Methods

    /// <summary>
    /// Releases the semaphore lock and marks the sender as disposed.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _sendLock.Dispose();
    }
}
