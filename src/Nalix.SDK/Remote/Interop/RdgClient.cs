// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.SDK.Remote.Interop;

/// <summary>
/// Raw UDP datagram client with background receive loop, CQ for polling, and optional native callback.
/// </summary>
/// <remarks>
/// - Sends datagrams to a fixed remote endpoint (IP:port).
/// - Receives datagrams on a background task and exposes them via <see cref="TryRecv"/> or <see cref="rdg_cb"/>.
/// - Designed for interop usage; no generic packet type.
/// </remarks>
[UnsupportedOSPlatform("browser")]
[SkipLocalsInit]
[DebuggerDisplay("Remote={_remote.Address}:{_remote.Port}, IsReceiving={IsReceiving}")]
public sealed class RdgClient(String ip, Int32 port) : IDisposable
{
    private UdpClient _udp;
    private readonly IPEndPoint _remote = new(IPAddress.Parse(ip ?? throw new ArgumentNullException(nameof(ip))),
                                              (port is > 0 and < 65536) ? port : throw new ArgumentOutOfRangeException(nameof(port)));
    private CancellationTokenSource _cts;
    private Task _rx;

    private readonly ConcurrentQueue<Byte[]> _q = new();
    private readonly SemaphoreSlim _ready = new(0);

    /// <summary>Interop callback delegate.</summary>
    /// <param name="user">User pointer set by <see cref="SetCb"/>.</param>
    /// <param name="data">Pointer to unmanaged buffer containing datagram bytes.</param>
    /// <param name="len">Length of <paramref name="data"/>.</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void rdg_cb(IntPtr user, IntPtr data, Int32 len);

    private rdg_cb _cb;
    private IntPtr _user;

    /// <summary>Indicates whether the receiving loop is running.</summary>
    public Boolean IsReceiving { get; private set; }

    [MemberNotNull(nameof(_udp))]
    private void EnsureUdp()
    {
        if (_udp is not null)
        {
            return;
        }

        _udp = new UdpClient(0);
        _udp.Client.DontFragment = true;
        _udp.Client.ReceiveBufferSize = 1 << 16;
        _udp.Client.SendBufferSize = 1 << 16;
        // Optionally "connect" to fix remote so SendAsync(payload) can be used
        _udp.Connect(_remote);
    }

    /// <summary>Registers a native callback invoked on each received datagram.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCb(rdg_cb cb, IntPtr user) { _cb = cb; _user = user; }

    /// <summary>Starts background receive loop.</summary>
    [MemberNotNull(nameof(_cts))]
    [DebuggerStepThrough, DebuggerNonUserCode]
    public void Start()
    {
        if (IsReceiving)
        {
            return;
        }

        EnsureUdp();

        _cts = new CancellationTokenSource();
        _rx = Task.Run(() => RxAsync(_cts.Token));
        IsReceiving = true;
    }

    /// <summary>Stops receive loop (keeps socket open).</summary>
    [DebuggerStepThrough]
    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        IsReceiving = false;
    }

    /// <summary>Sends one UDP datagram to the configured remote endpoint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<Int32> SendAsync(ReadOnlyMemory<Byte> payload, CancellationToken ct = default)
    {
        EnsureUdp();
        // UdpClient has SendAsync(ReadOnlyMemory<byte>, CancellationToken) from .NET 8+ when connected
        _ = await _udp.SendAsync(payload, ct).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Dequeues a datagram from the internal queue.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Boolean TryRecv(Int32 timeoutMs, [NotNullWhen(true)] out Byte[] pkt)
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

    [DebuggerStepThrough]
    private async Task RxAsync(CancellationToken token)
    {
        EnsureUdp();
        try
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult r = await _udp.ReceiveAsync(token).ConfigureAwait(false);
                // r.Buffer is already a fresh array
                var pkt = r.Buffer;

                var cb = _cb;
                if (cb is not null && pkt.Length > 0)
                {
                    IntPtr p = Marshal.AllocHGlobal(pkt.Length);
                    Marshal.Copy(pkt, 0, p, pkt.Length);
                    try { cb(_user, p, pkt.Length); }
                    finally { Marshal.FreeHGlobal(p); }
                }

                _q.Enqueue(pkt);
                _ = _ready.Release();
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        // optional: log
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        _udp?.Dispose();
        _cts?.Dispose();
        while (_q.TryDequeue(out _)) { }
        GC.SuppressFinalize(this);
    }
}
