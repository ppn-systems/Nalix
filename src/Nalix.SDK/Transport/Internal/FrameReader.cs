// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Abstractions;
using Nalix.Codec.DataFrames.Chunks;
using Nalix.Codec.Memory;
using Nalix.Codec.Options;
using Nalix.Codec.Transforms;
using Nalix.Environment.Configuration;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Optimized frame recipient that handles reassembly of fragmented packets,
/// decryption, and decompression of raw network data.
/// </summary>
internal sealed class FrameReader : IDisposable
{
    private static readonly FragmentOptions s_fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();
    private static readonly SocketException s_frameSizeExceeded = new((int)SocketError.MessageSize);

    private readonly TransportOptions _options;
    private readonly Func<Socket> _getSocket;
    private readonly Action<Exception> _onError;
    private readonly Action<IBufferLease> _onMessage;

    private readonly FragmentAssembler _fragmentAssembler = new()
    {
        MaxStreamBytes = s_fragmentOptions.MaxReassemblyBytes,
        StreamTimeoutMs = s_fragmentOptions.ReassemblyTimeoutMs
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameReader"/> class.
    /// </summary>
    /// <param name="getSocket">A delegate that returns the active socket used for receiving data.</param>
    /// <param name="options">The transport options used to validate inbound frame sizes.</param>
    /// <param name="onMessage">The callback invoked when a fully processed frame is ready.</param>
    /// <param name="onError">The callback invoked when receive processing fails.</param>
    public FrameReader(
        Func<Socket> getSocket,
        TransportOptions options,
        Action<IBufferLease> onMessage,
        Action<Exception> onError)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
        _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
    }

    /// <summary>
    /// Starts the receive loop and continues processing frames until cancellation is requested or a fatal socket error occurs.
    /// </summary>
    /// <param name="token">A cancellation token used to stop the receive loop.</param>
    public async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            Socket s = _getSocket();
            byte[] headerBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(TcpSession.HeaderSize);
            Memory<byte> headerMemory = new(headerBuffer, 0, TcpSession.HeaderSize);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);

                        ushort totalLen = BinaryPrimitives.ReadUInt16LittleEndian(headerMemory.Span);
                        if ((uint)totalLen < TcpSession.HeaderSize || totalLen > _options.BufferSize)
                        {
                            throw s_frameSizeExceeded;
                        }

                        int payloadLen = totalLen - TcpSession.HeaderSize;
                        byte[]? rented = BufferLease.ByteArrayPool.Rent(totalLen);
                        try
                        {
                            BinaryPrimitives.WriteUInt16LittleEndian(rented.AsSpan(0, TcpSession.HeaderSize), totalLen);

                            if (payloadLen > 0)
                            {
                                await RECEIVE_EXACTLY_ASYNC(s, rented.AsMemory(TcpSession.HeaderSize, payloadLen), token).ConfigureAwait(false);
                            }

                            // Ownership of 'rented' is taken by BufferLease.
                            BufferLease lease = BufferLease.TakeOwnership(rented, TcpSession.HeaderSize, payloadLen);
                            rented = null; // Transferred

                            if (FragmentAssembler.IsFragmentedFrame(lease.Span, out FragmentHeader header))
                            {
                                this.PROCESS_FRAGMENTED_FRAME(lease, header);
                                continue;
                            }

                            this.PROCESS_NORMAL_FRAME(lease);
                        }
                        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
                        {
                            if (rented != null)
                            {
                                BufferLease.ByteArrayPool.Return(rented);
                            }

                            throw;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _onError(ex);
                        break;
                    }
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(headerBuffer);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (Abstractions.Exceptions.ExceptionClassifier.IsNonFatal(ex))
        {
            _onError(ex);
        }
        finally
        {
            _fragmentAssembler.Dispose();
        }
    }

    private void PROCESS_FRAGMENTED_FRAME(BufferLease chunkLease, FragmentHeader header)
    {
        try
        {
            ReadOnlySpan<byte> chunkBody = chunkLease.Span[FragmentHeader.WireSize..];
            FragmentAssemblyResult? assembled = _fragmentAssembler.Add(header, chunkBody, out bool streamEvicted);

            if (assembled is not null)
            {
                this.PROCESS_NORMAL_FRAME(assembled.Value.Lease);
            }
        }
        finally
        {
            chunkLease.Dispose();
        }
    }

    private void PROCESS_NORMAL_FRAME(IBufferLease lease)
    {
        IBufferLease original = lease;
        try
        {
            FramePipeline.ProcessInbound(ref lease, _options.Secret.AsSpan(), _options.Algorithm);

            _onMessage(lease);
        }
        finally
        {
            if (!ReferenceEquals(lease, original))
            {
                lease.Dispose();
            }

            original.Dispose();
        }
    }

    private static async Task RECEIVE_EXACTLY_ASYNC(Socket s, Memory<byte> dst, CancellationToken token)
    {
        int read = 0;
        while (read < dst.Length)
        {
            int n = await s.ReceiveAsync(dst[read..], SocketFlags.None, token).ConfigureAwait(false);
            if (n == 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            read += n;
        }
    }

    /// <summary>
    /// Releases resources used by the fragment assembler.
    /// </summary>
    public void Dispose() => _fragmentAssembler.Dispose();
}
