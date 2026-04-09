// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Nalix.SDK.Options;

namespace Nalix.SDK.Transport.Internal;

/// <summary>
/// Optimized frame recipient that handles reassembly of fragmented packets, 
/// decryption, and decompression of raw network data.
/// </summary>
internal sealed class FRAME_READER : IDisposable
{
    private static readonly FragmentOptions s_fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();

    private readonly TransportOptions _options;
    private readonly Func<Socket> _getSocket;
    private readonly Action<Exception> _onError;
    private readonly Action<BufferLease> _onMessage;

    private readonly FragmentAssembler _fragmentAssembler = new()
    {
        MaxStreamBytes = s_fragmentOptions.MaxReassemblyBytes,
        StreamTimeoutMs = s_fragmentOptions.ReassemblyTimeoutMs
    };

    public FRAME_READER(
        Func<Socket> getSocket,
        TransportOptions options,
        Action<BufferLease> onMessage,
        Action<Exception> onError)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
    }

    public async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            Socket s = _getSocket();
            while (!token.IsCancellationRequested)
            {
                byte[] headerBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(TcpSession.HeaderSize);
                try
                {
                    Memory<byte> headerMemory = new(headerBuffer, 0, TcpSession.HeaderSize);
                    await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);

                    ushort totalLen = BinaryPrimitives.ReadUInt16LittleEndian(headerMemory.Span);
                    if (totalLen < TcpSession.HeaderSize || totalLen > _options.MaxPacketSize)
                    {
                        throw new SocketException((int)SocketError.ProtocolNotSupported);
                    }

                    int payloadLen = totalLen - TcpSession.HeaderSize;
                    byte[] rented = BufferLease.ByteArrayPool.Rent(totalLen);
                    try
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(rented.AsSpan(0, TcpSession.HeaderSize), totalLen);

                        if (payloadLen > 0)
                        {
                            await RECEIVE_EXACTLY_ASYNC(s, rented.AsMemory(TcpSession.HeaderSize, payloadLen), token).ConfigureAwait(false);
                        }

                        // Ownership of 'rented' is taken by BufferLease.
                        BufferLease lease = BufferLease.TakeOwnership(rented, TcpSession.HeaderSize, payloadLen);

                        if (FragmentAssembler.IsFragmentedFrame(lease.Span, out FragmentHeader header))
                        {
                            this.PROCESS_FRAGMENTED_FRAME(lease, header);
                            continue;
                        }

                        this.PROCESS_NORMAL_FRAME(lease);
                    }
                    catch { BufferLease.ByteArrayPool.Return(rented); throw; }
                }
                finally { System.Buffers.ArrayPool<byte>.Shared.Return(headerBuffer); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { _onError(ex); }
        finally { _fragmentAssembler.Dispose(); }
    }

    private void PROCESS_FRAGMENTED_FRAME(BufferLease chunkLease, FragmentHeader header)
    {
        try
        {
            ReadOnlySpan<byte> chunkBody = chunkLease.Span[FragmentHeader.WireSize..];
            FragmentAssemblyResult? assembled = _fragmentAssembler.Add(header, chunkBody, out bool streamEvicted);
            chunkLease.Dispose();

            if (assembled is not null)
            {
                this.PROCESS_NORMAL_FRAME(assembled.Value.Lease);
            }
        }
        catch (Exception)
        {
            chunkLease.Dispose();
        }
    }

    private void PROCESS_NORMAL_FRAME(BufferLease lease)
    {
        try
        {
            BufferLease transformed = PacketFrameTransforms.TransformInbound(lease, _options.Secret);
            try
            {
                _onMessage(transformed);
            }
            finally
            {
                transformed.Dispose();
            }
        }
        catch (Exception)
        {
            lease.Dispose();
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

    public void Dispose() => _fragmentAssembler.Dispose();
}
