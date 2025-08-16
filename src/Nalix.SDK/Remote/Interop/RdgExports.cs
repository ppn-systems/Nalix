// Copyright (c) 2025 PPN Corporation. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nalix.SDK.Remote.Interop;

/// <summary>Unmanaged-callable exports for <see cref="RdgClient"/> (UDP).</summary>
[System.Security.SuppressUnmanagedCodeSecurity]
[SkipLocalsInit]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[DebuggerDisplay("Exported RdgClient count = {_map.Count}")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RdgExports
{
    private static Int64 _next = 1;
    private static readonly ConcurrentDictionary<Int64, RdgClient> _map = new();

    /// <inheritdoc/>
    [StackTraceHidden]
    [SuppressGCTransition]
    [UnmanagedCallersOnly(EntryPoint = "rdg_ver", CallConvs = new[] { typeof(CallConvCdecl) })]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UInt32 Ver() => 0x00010000; // 1.0.0

    /// <inheritdoc/>
    [UnmanagedCallersOnly(EntryPoint = "rdg_new", CallConvs = new[] { typeof(CallConvCdecl) })]
    [DebuggerNonUserCode]
    public static Int64 New([In] IntPtr ipUtf8, Int32 port)
    {
        String ip = Marshal.PtrToStringUTF8(ipUtf8)!;
        var cli = new RdgClient(ip, port);
        Int64 h = System.Threading.Interlocked.Increment(ref _next);
        _map[h] = cli;
        return h;
    }

    /// <inheritdoc/>
    [UnmanagedCallersOnly(EntryPoint = "rdg_free", CallConvs = new[] { typeof(CallConvCdecl) })]
    [StackTraceHidden, DebuggerNonUserCode]
    public static void Free(Int64 h)
    {
        if (_map.TryRemove(h, out var c))
        {
            c.Dispose();
        }
    }

    /// <inheritdoc/>
    [UnmanagedCallersOnly(EntryPoint = "rdg_start", CallConvs = new[] { typeof(CallConvCdecl) })]
    [StackTraceHidden, DebuggerNonUserCode]
    public static Int32 Start(Int64 h)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        try { c.Start(); return 0; }
        catch { return -2; }
    }

    /// <inheritdoc/>
    [UnmanagedCallersOnly(EntryPoint = "rdg_stop", CallConvs = new[] { typeof(CallConvCdecl) })]
    [StackTraceHidden, DebuggerNonUserCode]
    public static void Stop(Int64 h)
    {
        if (_map.TryGetValue(h, out var c))
        {
            c.Stop();
        }
    }

    /// <inheritdoc/>
    [StackTraceHidden]
    [SuppressGCTransition] // tiny and non-blocking
    [UnmanagedCallersOnly(EntryPoint = "rdg_isrunning", CallConvs = new[] { typeof(CallConvCdecl) })]
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Int32 IsRunning(Int64 h)
        => !_map.TryGetValue(h, out var c) ? -1 : (c.IsReceiving ? 1 : 0);

    /// <inheritdoc/>
    [UnmanagedCallersOnly(EntryPoint = "rdg_send", CallConvs = new[] { typeof(CallConvCdecl) })]
    [StackTraceHidden, DebuggerNonUserCode]
    public static Int32 Send(Int64 h, [In] IntPtr data, Int32 len)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        if (data == IntPtr.Zero || len < 0)
        {
            return -3;
        }

        try
        {
            unsafe
            {
                var span = new ReadOnlySpan<Byte>((void*)data, len);
                var arr = System.Buffers.ArrayPool<Byte>.Shared.Rent(len);
                try
                {
                    span.CopyTo(arr);
                    var mem = new ReadOnlyMemory<Byte>(arr, 0, len);
                    _ = c.SendAsync(mem).GetAwaiter().GetResult();
                }
                finally { System.Buffers.ArrayPool<Byte>.Shared.Return(arr); }
            }
            return 0;
        }
        catch { return -2; }
    }

    /// <inheritdoc/>
    [UnmanagedCallersOnly(EntryPoint = "rdg_recv", CallConvs = new[] { typeof(CallConvCdecl) })]
    [StackTraceHidden, DebuggerNonUserCode]
    public static Int32 Recv(Int64 h, [In] IntPtr outBuf, Int32 outCap, Int32 timeoutMs)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        if (outBuf == IntPtr.Zero || outCap <= 0)
        {
            return -3;
        }

        try
        {
            if (!c.TryRecv(timeoutMs, out var pkt) || pkt is null)
            {
                return 0; // timeout
            }

            Int32 n = Math.Min(outCap, pkt.Length);
            Marshal.Copy(pkt, 0, outBuf, n);
            return n;
        }
        catch { return -2; }
    }

    /// <inheritdoc/>
    [UnmanagedCallersOnly(EntryPoint = "rdg_setcb", CallConvs = new[] { typeof(CallConvCdecl) })]
    [RequiresUnreferencedCode("Converts unmanaged function pointer to managed delegate; ensure RdgClient.rdg_cb is preserved when trimming/AOT.")]
    [StackTraceHidden, DebuggerNonUserCode]
    public static Int32 SetCb(Int64 h, [In] IntPtr fnPtr, IntPtr user)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        try
        {
            var del = Marshal.GetDelegateForFunctionPointer<RdgClient.rdg_cb>(fnPtr);
            c.SetCb(del, user);
            return 0;
        }
        catch { return -2; }
    }
}
