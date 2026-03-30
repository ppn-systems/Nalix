// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.Configuration;
using Nalix.Framework.DataFrames;
using Nalix.Framework.DataFrames.Chunks;
using Nalix.Framework.Extensions;
using Nalix.Framework.Injection;
using Nalix.Framework.Memory.Buffers;
using Nalix.Framework.Options;
using Nalix.SDK.Configuration;

namespace Nalix.SDK.Transport.Internal;

/// <inheritdoc/>
/// <remarks>
/// Ownership contract:
/// - FRAME_READER creates a <see cref="BufferLease"/> using <see cref="BufferLease.TakeOwnership"/>
///   and passes it to <paramref name="onMessage"/> (usually <c>HandleReceiveMessage</c>).
/// - The handler is the sole owner and must dispose the lease (typically in a finally block).
/// - FRAME_READER never touches the lease after handing it over.
/// </remarks>
internal sealed class FRAME_READER(
    Func<Socket> getSocket,
    TransportOptions options,
    Action<BufferLease> onMessage,
    Action<Exception> onError,
    Action<int> reportBytesReceived,
    ILogger? logger = null) : IDisposable
{
    #region Fields

    private static readonly FragmentOptions s_fragmentOptions = ConfigurationManager.Instance.Get<FragmentOptions>();

    private readonly TransportOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly Func<Socket> _getSocket = getSocket ?? throw new ArgumentNullException(nameof(getSocket));
    private readonly ILogger? _logger = logger ?? InstanceManager.Instance.GetExistingInstance<ILogger>();
    private readonly Action<Exception> _onError = onError ?? throw new ArgumentNullException(nameof(onError));
    private readonly Action<BufferLease> _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
    private readonly Action<int> _reportBytesReceived = reportBytesReceived ?? throw new ArgumentNullException(nameof(reportBytesReceived));

    /// <summary>
    /// Reassembly engine for fragmented packets.
    /// Full payload is only processed (decrypted/decompressed) after all chunks are received.
    /// </summary>
    private readonly FragmentAssembler _fragmentAssembler = new()
    {
        MaxStreamBytes = s_fragmentOptions.MaxReassemblyBytes,
        StreamTimeoutMs = s_fragmentOptions.ReassemblyTimeoutMs
    };

    #endregion Fields

    #region API

    /// <summary>
    /// Main receive loop that supports both normal packets and fragmented packets.
    /// </summary>
    public async Task ReceiveLoopAsync(CancellationToken token)
    {
        Socket s;
        try
        {
            s = _getSocket();
            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace(
                    "[SDK.FRAME_READER] receive-loop starting; endpoint={Endpoint}",
                    FORMAT_ENDPOINT(s)
                );
            }
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(
                    ex,
                    "[SDK.FRAME_READER] receive-start-error"
                );
            }
            _onError(ex);
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1) Read 2-byte little-endian length header
                byte[] headerBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(TcpSession.HeaderSize);
                try
                {
                    Memory<byte> headerMemory = new(headerBuffer, 0, TcpSession.HeaderSize);
                    await RECEIVE_EXACTLY_ASYNC(s, headerMemory, token).ConfigureAwait(false);

                    ushort totalLen = BinaryPrimitives.ReadUInt16LittleEndian(headerMemory.Span);

                    if (totalLen < TcpSession.HeaderSize || totalLen > _options.MaxPacketSize)
                    {
                        if (_logger?.IsEnabled(LogLevel.Warning) == true)
                        {
                            _logger.LogWarning(
                                "[SDK.FRAME_READER] invalid-packet-size totalLen={TotalLen} max={MaxPacketSize} endpoint={Endpoint}",
                                totalLen,
                                _options.MaxPacketSize,
                                FORMAT_ENDPOINT(s)
                            );
                        }

                        throw new SocketException((int)SocketError.ProtocolNotSupported);
                    }

                    int payloadLen = totalLen - TcpSession.HeaderSize;

                    // 2) Rent buffer and read full payload
                    byte[] rented = BufferLease.ByteArrayPool.Rent(totalLen);
                    try
                    {
                        // Write length header back into the buffer
                        BinaryPrimitives.WriteUInt16LittleEndian(MemoryExtensions
                                        .AsSpan(rented, 0, TcpSession.HeaderSize), totalLen);

                        if (payloadLen > 0)
                        {
                            await RECEIVE_EXACTLY_ASYNC(s, MemoryExtensions.AsMemory(rented, TcpSession.HeaderSize, payloadLen), token)
                                                                           .ConfigureAwait(false);
                        }

                        // Report received bytes (best-effort)
                        try { _reportBytesReceived(totalLen); } catch { }

                        // Create lease pointing to payload only (skip 2-byte header)
                        BufferLease lease = BufferLease.TakeOwnership(rented, TcpSession.HeaderSize, payloadLen);

                        // 3) Check if this is a fragmented chunk
                        if (FragmentAssembler.IsFragmentedFrame(lease.Span, out FragmentHeader header))
                        {
                            this.PROCESS_FRAGMENTED_FRAME(lease, header);

                            // Do not call _onMessage yet
                            continue;
                        }

                        // Normal (non-fragmented) frame → process decrypt/decompress
                        this.PROCESS_NORMAL_FRAME(lease);
                    }
                    catch (Exception)
                    {
                        BufferLease.ByteArrayPool.Return(rented);
                        throw;
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(headerBuffer);
                }
            }

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("[SDK.FRAME_READER] receive-loop ending normally");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("[SDK.FRAME_READER] receive-loop cancelled");
            }
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(
                    ex,
                    "[SDK.FRAME_READER] receive-loop faulted"
                );
            }
            try { _onError(ex); } catch { }
        }
        finally
        {
            _fragmentAssembler.Dispose();   // Clean up any pending streams
        }
    }

    #endregion API

    #region Private Methods

    #region Fragment Handling

    /// <summary>
    /// Adds a chunk to the assembler. If this completes a stream, the full payload is processed.
    /// </summary>
    private void PROCESS_FRAGMENTED_FRAME(BufferLease chunkLease, FragmentHeader header)
    {
        try
        {
            ReadOnlySpan<byte> chunkBody = chunkLease.Span[FragmentHeader.WireSize..];

            // Add chunk. If complete, assembled lease contains the full original payload.
            BufferLease? assembled = _fragmentAssembler.Add(header, chunkBody, out bool streamEvicted);

            // Always dispose the chunk lease immediately (data has been copied by assembler)
            chunkLease.Dispose();

            if (streamEvicted)
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning(
                        "[SDK.FRAME_READER] Fragment stream evicted stream={StreamId} — timeout or overflow",
                        header.StreamId
                    );
                }
            }

            if (assembled is not null)
            {
                this.PROCESS_NORMAL_FRAME(assembled);   // Now decrypt and decompress the full payload
            }
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Warning) == true)
            {
                _logger.LogWarning(
                    "[SDK.FRAME_READER] Failed to process fragmented chunk ex={ExceptionMessage}",
                    ex.Message
                );
            }
            chunkLease.Dispose();
        }
    }

    /// <summary>
    /// Processes a complete frame (either normal packet or fully reassembled fragmented payload).
    /// Decryption and decompression are performed here.
    /// </summary>
    private void PROCESS_NORMAL_FRAME(BufferLease lease)
    {
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            _logger.LogDebug(
                "[SDK.FRAME_READER] header-read length={Length}",
                lease.Length
            );
        }

        try
        {
            PacketFlags flags = lease.Span.ReadFlagsLE();

            // Decrypt if needed
            if (flags.HasFlag(PacketFlags.ENCRYPTED))
            {
                BufferLease decryptedLease = BufferLease.Rent(FrameTransformer.GetPlaintextLength(lease.Span));

                FrameTransformer.Decrypt(lease, decryptedLease, _options.Secret);
                decryptedLease.Span.WriteFlagsLE(decryptedLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.ENCRYPTED));
                lease.Dispose();
                lease = decryptedLease;
                flags = lease.Span.ReadFlagsLE();
            }

            // Decompress if needed
            if (flags.HasFlag(PacketFlags.COMPRESSED))
            {
                BufferLease decompressedLease = BufferLease.Rent(FrameTransformer.GetDecompressedLength(lease.Span));

                FrameTransformer.Decompress(lease, decompressedLease);
                decompressedLease.Span.WriteFlagsLE(decompressedLease.Span.ReadFlagsLE().RemoveFlag(PacketFlags.COMPRESSED));
                lease.Dispose();
                lease = decompressedLease;
            }

            // Deliver to upper layer (HandleReceiveMessage)
            _onMessage(lease);
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(
                    ex,
                    "[SDK.FRAME_READER] Failed to process normal frame"
                );
            }

            lease.Dispose();
        }
    }

    #endregion

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

    [DebuggerStepThrough]
    private static string FORMAT_ENDPOINT(Socket? s)
    {
        if (s is null)
        {
            return "<null-socket>";
        }

        try { return s.RemoteEndPoint?.ToString() ?? "<unknown>"; }
        catch (ObjectDisposedException) { return "<disposed>"; }
        catch { return "<unknown>"; }
    }

    public void Dispose() => _fragmentAssembler.Dispose();

    #endregion Private Methods
}
