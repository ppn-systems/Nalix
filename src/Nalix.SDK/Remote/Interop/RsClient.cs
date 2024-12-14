// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System;
using System.Buffers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.SDK.Remote.Interop;

/// <summary>
/// Provides a managed wrapper for a TCP client with length-prefixed packet framing,
/// asynchronous send/receive, and optional native callback interoperability.
/// </summary>
/// <remarks>
/// - Each outgoing message is prefixed with a 2-byte big-endian length (including the header).
/// - Incoming packets are delivered via <see cref="TryRecv"/> or a registered <see cref="rs_cb"/> callback.
/// - Designed for interop with unmanaged code through function pointers.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="RsClient"/> class.
/// </remarks>
/// <param name="ip">Remote IP address of the server.</param>
/// <param name="port">Remote TCP port (1–65535).</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="ip"/> is null.</exception>
/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="port"/> is outside the valid range.</exception>
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
[System.Diagnostics.DebuggerDisplay("Remote={Ip}:{Port}, Connected={IsConn}")]
public sealed class RsClient(String ip, Int32 port) : IDisposable
{
    private Task _rx;
    private TcpClient _cli;
    private NetworkStream _st;
    private CancellationTokenSource _cts;
    private readonly SemaphoreSlim _ready = new(0);
    private readonly System.Collections.Concurrent.ConcurrentQueue<Byte[]> _q = new();

    /// <summary>
    /// Gets the remote IP address of the server to connect to.
    /// </summary>
    public String Ip { get; } = ip ?? throw new ArgumentNullException(nameof(ip));

    /// <summary>
    /// Gets the remote TCP port of the server to connect to.
    /// </summary>
    public Int32 Port { get; } = (port is > 0 and < 65536) ? port : throw new ArgumentOutOfRangeException(nameof(port));

    /// <summary>
    /// Gets a value indicating whether the client is currently connected.
    /// </summary>
    public Boolean IsConn => _cli?.Connected == true && _st is not null;

    /// <summary>
    /// Represents the callback delegate for delivering received packets to unmanaged code.
    /// </summary>
    /// <param name="user">User-defined pointer passed during registration.</param>
    /// <param name="data">Pointer to the received packet buffer.</param>
    /// <param name="len">Length of the received packet.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void rs_cb(IntPtr user, IntPtr data, Int32 len);

    private rs_cb _cb;
    private IntPtr _user;

    /// <summary>
    /// Registers a native callback function to be invoked when packets are received.
    /// </summary>
    /// <param name="cb">Callback delegate.</param>
    /// <param name="user">User-defined pointer passed to the callback.</param>
    public void SetCb(rs_cb cb, IntPtr user)
    {
        _cb = cb;
        _user = user;
    }

    /// <summary>
    /// Establishes a synchronous TCP connection to the remote server.
    /// </summary>
    /// <param name="timeoutMs">Connection timeout in milliseconds. Default is 20,000 ms.</param>
    /// <param name="ct">Optional cancellation token.</param>
    [System.Diagnostics.DebuggerStepThrough, System.Diagnostics.DebuggerNonUserCode]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_cli), nameof(_st), nameof(_cts))]
    public void Conn(Int32 timeoutMs = 20000, CancellationToken ct = default)
    {
        Disc();
        _cli = new TcpClient { NoDelay = true };
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);
        _cli.Connect(Ip, Port);
        _st = _cli.GetStream();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        _rx = Task.Run(() => RxAsync(_cts.Token), ct);
    }

    /// <summary>
    /// Establishes an asynchronous TCP connection to the remote server.
    /// </summary>
    /// <param name="timeoutMs">Connection timeout in milliseconds. Default is 30,000 ms.</param>
    /// <param name="ct">Optional cancellation token.</param>
    [System.Diagnostics.DebuggerStepThrough, System.Diagnostics.DebuggerNonUserCode]
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_cli), nameof(_st), nameof(_cts))]
    public async Task Conna(Int32 timeoutMs = 30000, CancellationToken ct = default)
    {
        Disc();
        _cli = new TcpClient { NoDelay = true };
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeoutMs);
        await _cli.ConnectAsync(Ip, Port, linked.Token).ConfigureAwait(false);
        _st = _cli.GetStream();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        _rx = Task.Run(() => RxAsync(_cts.Token), ct);
    }

    /// <summary>
    /// Sends a packet to the server with a length-prefixed header.
    /// </summary>
    /// <param name="body">Packet body to send.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>0 if success; negative value otherwise.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public async Task<Int32> Send(ReadOnlyMemory<Byte> body, CancellationToken ct = default)
    {
        if (_st is null)
        {
            return -4;
        }

        UInt16 total = checked((UInt16)(body.Length + 2));
        Byte[] hdr = new Byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(hdr, total);
        await _st.WriteAsync(hdr, ct).ConfigureAwait(false);
        await _st.WriteAsync(body, ct).ConfigureAwait(false);
        await _st.FlushAsync(ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>
    /// Attempts to dequeue a received packet from the internal queue.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds. Pass 0 or less to wait indefinitely.</param>
    /// <param name="pkt">Output packet if available.</param>
    /// <returns><c>true</c> if a packet was dequeued; otherwise, <c>false</c>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public Boolean TryRecv(
        Int32 timeoutMs,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Byte[] pkt)
    {
        if (timeoutMs <= 0)
        {
            _ready.Wait();
            return _q.TryDequeue(out pkt);
        }
        if (_ready.Wait(timeoutMs))
        {
            return _q.TryDequeue(out pkt);
        }

        pkt = null;
        return false;
    }

    [System.Diagnostics.DebuggerStepThrough]
    private async Task RxAsync(CancellationToken token)
    {
        if (_st is null)
        {
            return;
        }

        var s = _st;
        try
        {
            while (!token.IsCancellationRequested)
            {
                Byte[] hdr = new Byte[2];
                await s.ReadExactlyAsync(hdr, token).ConfigureAwait(false);
                Int32 body = ((hdr[0] << 8) | hdr[1]) - 2;
                if (body <= 0)
                {
                    throw new InvalidOperationException("Bad frame length.");
                }

                Byte[] rent = ArrayPool<Byte>.Shared.Rent(body);
                try
                {
                    await s.ReadExactlyAsync(new Memory<Byte>(rent, 0, body), token).ConfigureAwait(false);
                    var pkt = new Byte[body];
                    Buffer.BlockCopy(rent, 0, pkt, 0, body);

                    var cb = _cb;
                    if (cb is not null)
                    {
                        IntPtr p = Marshal.AllocHGlobal(body);
                        Marshal.Copy(pkt, 0, p, body);
                        try { cb(_user, p, body); }
                        finally { Marshal.FreeHGlobal(p); }
                    }

                    _q.Enqueue(pkt);
                    _ = _ready.Release();
                }
                finally { ArrayPool<Byte>.Shared.Return(rent); }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        // optional: log
    }

    /// <summary>
    /// Disconnects the client and releases resources.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public void Disc() => Dispose();

    /// <inheritdoc/>
    [System.Diagnostics.DebuggerStepThrough]
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        _st?.Dispose();
        _cli?.Close();
        _cts?.Dispose();
        _st = null; _cli = null;
        while (_q.TryDequeue(out _)) { }
        GC.SuppressFinalize(this);
    }
}
