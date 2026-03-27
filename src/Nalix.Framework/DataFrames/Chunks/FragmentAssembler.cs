// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Nalix.Framework.Memory.Buffers;

namespace Nalix.Framework.DataFrames.Chunks;

/// <summary>
/// Reassembly engine: collects each received chunk and returns a complete <see cref="BufferLease"/>
/// when the last chunk arrives.
///
/// <para>
/// <b>Shared contract</b> — this instance is embedded into <c>FRAME_READER</c> (SDK)
/// and <c>FramedSocketConnection</c> (Server). Each connection holds its own instance.
/// </para>
///
/// <para><b>Thread safety:</b> designed for a single-threaded receive loop.
/// No locking required as TCP ensures order and only a single goroutine reads from the socket.</para>
///
/// <para><b>Ownership after reassembly:</b>
/// <see cref="TryAdd"/> returns a <see cref="BufferLease"/> newly rented from the pool.
/// <b>The caller must Dispose the lease</b> after handling.</para>
///
/// <para><b>Chunks must arrive in order</b> (TCP guarantees this) and not be duplicated.
/// If you need out-of-order support, expand <c>StreamState</c> with a bitmap per slot.</para>
/// </summary>
public sealed class FragmentAssembler : IDisposable
{
    #region Constants

    /// <summary>
    /// Eviction check interval in number of processed chunks. Call <see cref="EvictExpired"/> every N chunks.
    /// </summary>
    public const int EvictInterval = 64;

    #endregion Constants

    #region Inner state per stream

    private sealed class StreamState : IDisposable
    {
        internal byte[] AccumBuffer;     // Buffer for accumulating payload
        internal int WrittenBytes;       // Number of bytes written
        internal int ReceivedCount;      // Number of chunks received
        internal ushort TotalChunks;     // Total number of chunks expected
        internal long LastActivityMs;    // Timestamp (ms), for eviction

        internal StreamState(ushort totalChunks, int estimatedBytes)
        {
            TotalChunks = totalChunks;
            AccumBuffer = BufferLease.ByteArrayPool.Rent(estimatedBytes);
            WrittenBytes = 0;
            ReceivedCount = 0;
            LastActivityMs = Environment.TickCount64;
        }

        public void Dispose()
        {
            byte[]? buf = Interlocked.Exchange(ref AccumBuffer!, null!);
            if (buf is not null)
            {
                BufferLease.ByteArrayPool.Return(buf);
            }
        }
    }

    #endregion Inner state per stream

    #region Fields

    private bool _disposed;

    // Use regular Dictionary since this is for a single-threaded per-connection receive loop.
    // Avoid ConcurrentDictionary overhead, which is unnecessary here.
    private readonly Dictionary<ushort, StreamState> _streams = [];
    private List<ushort>? _toEvict;

    #endregion Fields

    #region Configuration properties

    /// <summary>
    /// Streams currently being reassembled, indexed by StreamId.
    /// </summary>
    public int OpenStreamCount => _streams.Count;

    /// <summary>
    /// The maximum time (ms) a stream can exist without receiving a new chunk before being evicted.
    /// Default: 30,000 ms (30 seconds).
    /// </summary>
    public long StreamTimeoutMs { get; init; } = 30_000;

    /// <summary>
    /// The maximum size (in bytes) of a stream being reassembled.
    /// Streams exceeding this threshold are immediately discarded. Default: 16 MB.
    /// </summary>
    public int MaxStreamBytes { get; init; } = 16 * 1024 * 1024;

    #endregion Configuration properties

    #region APIs

    /// <summary>
    /// Adds a chunk to the assembler.
    ///
    /// <para>
    /// Returns <see langword="true"/> and sets <paramref name="assembled"/> when
    /// this is the last chunk and all previous chunks have arrived (stream is complete).
    /// </para>
    /// <para>
    /// Returns <see langword="false"/> in all other cases:
    /// waiting for more chunks, header error, or timeout eviction.
    /// </para>
    /// </summary>
    /// <param name="header">Header of the received chunk.</param>
    /// <param name="chunkBody">
    /// The chunk body — without the 7-byte <see cref="FragmentHeader"/>.
    /// Data is <b>copied</b> immediately, so the caller may release the span after calling.
    /// </param>
    /// <param name="assembled">
    /// [out] Complete buffer lease when stream is done. <b>Caller must Dispose.</b>
    /// </param>
    /// <param name="streamEvicted"></param>
    /// <returns><see langword="true"/> if the stream is complete.</returns>
    public bool TryAdd(in FragmentHeader header, ReadOnlySpan<byte> chunkBody, out BufferLease? assembled, out bool streamEvicted)
    {
        assembled = null;
        streamEvicted = false;

        if (_disposed)
        {
            return false;
        }

        // ── Validate ──────────────────────────────────────────────────────
        if (header.StreamId == 0
         || header.TotalChunks == 0
         || header.ChunkIndex >= header.TotalChunks)
        {
            return false; // Malformed — skip, do not evict other streams
        }

        long now = Environment.TickCount64;

        // ── Retrieve or create StreamState ────────────────────────────────
        if (!_streams.TryGetValue(header.StreamId, out StreamState? state))
        {
            if (header.ChunkIndex != 0)
            {
                return false;
            }

            // Estimate buffer size: firstChunk * totalChunks, capped at MaxStreamBytes
            int estimate = (int)Math.Min((long)chunkBody.Length * header.TotalChunks, this.MaxStreamBytes);

            state = new StreamState(header.TotalChunks, Math.Max(estimate, 256));
            _streams[header.StreamId] = state;
        }

        // ── Check timeout ────────────────────────────────────────────────
        if (now - state.LastActivityMs > this.StreamTimeoutMs)
        {
            streamEvicted = true;
            this.EVICT(header.StreamId, state);
            return false;
        }

        state.LastActivityMs = now;

        // ── Check TotalChunks consistency ────────────────────────────────
        // Prevent forgery: TotalChunks must be the same across all chunks of a stream
        if (state.TotalChunks != header.TotalChunks)
        {
            streamEvicted = true;
            this.EVICT(header.StreamId, state);
            return false;
        }

        // ── Check overflow ───────────────────────────────────────────────
        if (state.ReceivedCount >= state.TotalChunks)
        {
            return false; // Duplicate or stale
        }

        if (state.WrittenBytes + chunkBody.Length > this.MaxStreamBytes)
        {
            streamEvicted = true;
            this.EVICT(header.StreamId, state);
            return false;
        }

        // ── Grow buffer if needed ────────────────────────────────────────
        if (state.WrittenBytes + chunkBody.Length > state.AccumBuffer.Length)
        {
            int newCap = Math.Max(
                state.AccumBuffer.Length * 2,
                state.WrittenBytes + chunkBody.Length);

            byte[] newBuf = BufferLease.ByteArrayPool.Rent(newCap);
            state.AccumBuffer.AsSpan(0, state.WrittenBytes).CopyTo(newBuf);
            BufferLease.ByteArrayPool.Return(state.AccumBuffer);
            state.AccumBuffer = newBuf;
        }

        // ── Append chunk body ────────────────────────────────────────────
        // TCP guarantees in-order delivery so end-appending is correct
        chunkBody.CopyTo(state.AccumBuffer.AsSpan(state.WrittenBytes));
        state.WrittenBytes += chunkBody.Length;
        state.ReceivedCount += 1;

        // ── Check if stream is complete ──────────────────────────────────
        if (state.ReceivedCount < state.TotalChunks)
        {
            return false; // Still waiting for more chunks
        }

        // ── Stream done: hand over buffer to caller (zero-copy) ──────────
        int finalLen = state.WrittenBytes;
        byte[] finalBuf = state.AccumBuffer;

        // Separate ownership from state BEFORE remove,
        // so Dispose for state does not return buffer to pool again
        state.AccumBuffer = null!;
        _ = _streams.Remove(header.StreamId);
        // No need to call state.Dispose() because AccumBuffer is now null

        assembled = BufferLease.FromRented(finalBuf, finalLen);
        return true;
    }

    /// <summary>
    /// Evicts all streams that have not received a chunk within <see cref="StreamTimeoutMs"/>.
    /// Call periodically from the receive loop (e.g. every N packets).
    /// </summary>
    /// <returns>Number of streams evicted.</returns>
    public int EvictExpired()
    {
        if (_streams.Count == 0)
        {
            return 0;
        }

        long now = Environment.TickCount64;
        int evicted = 0;

        foreach ((ushort streamId, StreamState? state) in _streams)
        {
            if (now - state.LastActivityMs > this.StreamTimeoutMs)
            {
                _toEvict ??= [];
                _toEvict.Add(streamId);
            }
        }

        if (_toEvict is { Count: > 0 })
        {
            foreach (ushort id in _toEvict)
            {
                if (_streams.Remove(id, out StreamState? state))
                {
                    state.Dispose();
                    evicted++;
                }
            }
            _toEvict.Clear();
        }

        return evicted;
    }

    /// <summary>
    /// Cancels and disposes all streams in progress.
    /// Call when connection is closed to avoid memory leaks.
    /// </summary>
    public void Clear()
    {
        foreach (KeyValuePair<ushort, StreamState> kv in _streams)
        {
            kv.Value.Dispose();
        }

        _streams.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        this.Clear();
    }

    /// <summary>
    /// Quick check to determine if the payload is a fragmented chunk using the magic byte.
    /// </summary>
    public static bool IsFragmentedFrame(ReadOnlySpan<byte> payload, [NotNullWhen(true)] out FragmentHeader header)
    {
        header = default;
        if (payload.Length < FragmentHeader.WireSize)
        {
            return false;
        }

        if (payload[0] != FragmentHeader.Magic)
        {
            return false;
        }

        // Second validation: try to read header and check logical consistency
        try
        {
            header = FragmentHeader.ReadFrom(payload);

            // Additional safety checks
            if (header.StreamId == 0)
            {
                return false;
            }

            if (header.TotalChunks == 0 || header.ChunkIndex >= header.TotalChunks)
            {
                return false;
            }

            return true;
        }
        catch (InvalidDataException)
        {
            // If parsing fails -> treat as normal packet
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            // If parsing fails -> treat as normal packet
            return false;
        }
        catch (IndexOutOfRangeException)
        {
            // If parsing fails → treat as normal packet
            return false;
        }
    }

    #endregion APIs

    #region Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EVICT(ushort streamId, StreamState state)
    {
        _ = _streams.Remove(streamId);
        state.Dispose();
    }

    #endregion Private methods
}
