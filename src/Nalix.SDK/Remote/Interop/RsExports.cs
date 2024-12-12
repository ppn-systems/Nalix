// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.SDK.Remote.Interop;

/// <summary>
/// Provides unmanaged-callable exports for creating and controlling <see cref="RsClient"/> instances.
/// </summary>
/// <remarks>
/// All functions are exposed via <see cref="System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute"/> and can be called directly
/// from native code without a P/Invoke layer.  
/// Each managed <see cref="RsClient"/> is tracked in a handle map keyed by <c>long</c>.
/// </remarks>
[System.Security.SuppressUnmanagedCodeSecurity]
[System.Runtime.CompilerServices.SkipLocalsInit]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
[System.Diagnostics.DebuggerDisplay("Exported RsClient count = {_map.Count}")]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class RsExports
{
    private static System.Int64 _next = 1;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Int64, RsClient> _map = new();

    /// <summary>
    /// Gets the version of the native API.
    /// </summary>
    /// <returns>32-bit version encoded as <c>0xMMmmpppp</c> (Major.Minor.Patch).</returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.UnmanagedCallersOnly(
        EntryPoint = "rs_ver",
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.UInt32 Ver() => 0x00010000; // 1.0.0

    /// <summary>
    /// Creates a new <see cref="RsClient"/> instance.
    /// </summary>
    /// <param name="ipUtf8">Pointer to a UTF-8 string representing the remote IP address.</param>
    /// <param name="port">TCP port to connect to.</param>
    /// <returns>A unique handle (greater than 0) for the client.</returns>
    [System.Runtime.InteropServices.UnmanagedCallersOnly(
        EntryPoint = "rs_new",
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static System.Int64 New(
        [System.Runtime.InteropServices.In] System.IntPtr ipUtf8, System.Int32 port)
    {
        System.String ip = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(ipUtf8)!;
        var cli = new RsClient(ip, port);
        System.Int64 h = System.Threading.Interlocked.Increment(ref _next);
        _map[h] = cli;
        return h;
    }

    /// <summary>
    /// Frees a previously created <see cref="RsClient"/>.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "rs_free")]
    public static void Free(System.Int64 h)
    {
        if (_map.TryRemove(h, out var c))
        {
            c.Dispose();
        }
    }

    /// <summary>
    /// Connects synchronously to the server.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    /// <param name="timeoutMs">Timeout in milliseconds; defaults to 20,000 ms if &lt;= 0.</param>
    /// <returns>
    /// 0 if success; -1 if handle not found; -2 if an exception occurs.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "rs_conn")]
    public static System.Int32 Conn(System.Int64 h, System.Int32 timeoutMs)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        try { c.Conn(timeoutMs <= 0 ? 20000 : timeoutMs); return 0; }
        catch { return -2; }
    }

    /// <summary>
    /// Initiates an asynchronous connection to the server.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    /// <param name="timeoutMs">Timeout in milliseconds; defaults to 30,000 ms if &lt;= 0.</param>
    /// <returns>
    /// 0 if accepted; -1 if handle not found; -2 if an exception occurs.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "rs_conna")]
    public static System.Int32 Conna(System.Int64 h, System.Int32 timeoutMs)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        try { _ = c.Conna(timeoutMs <= 0 ? 30000 : timeoutMs); return 0; }
        catch { return -2; }
    }

    /// <summary>
    /// Disconnects the client.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    [System.Runtime.InteropServices.UnmanagedCallersOnly(
        EntryPoint = "rs_disc",
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void Disc(System.Int64 h)
    {
        if (_map.TryGetValue(h, out var c))
        {
            c.Disc();
        }
    }

    /// <summary>
    /// Checks whether the client is currently connected.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    /// <returns>
    /// 1 if connected; 0 if not connected; -1 if handle not found.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.InteropServices.SuppressGCTransition]
    [System.Runtime.InteropServices.UnmanagedCallersOnly(
        EntryPoint = "rs_isconn",
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining |
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static System.Int32 IsConn(System.Int64 h) => !_map.TryGetValue(h, out var c) ? -1 : c.IsConn ? 1 : 0;

    /// <summary>
    /// Sends a packet to the server.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    /// <param name="data">Pointer to the packet data.</param>
    /// <param name="len">Length of the packet.</param>
    /// <returns>
    /// 0 if success; -1 if handle not found; -2 if exception; -3 if invalid pointer or length.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "rs_send")]
    public static System.Int32 Send(
        System.Int64 h,
        [System.Runtime.InteropServices.In] System.IntPtr data,
        System.Int32 len)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        if (data == System.IntPtr.Zero || len < 0)
        {
            return -3;
        }

        try
        {
            unsafe
            {
                var span = new System.ReadOnlySpan<System.Byte>((void*)data, len);
                var arr = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(len);
                try
                {
                    span.CopyTo(arr);
                    var mem = new System.ReadOnlyMemory<System.Byte>(arr, 0, len);
                    _ = c.Send(mem).GetAwaiter().GetResult();
                }
                finally { System.Buffers.ArrayPool<System.Byte>.Shared.Return(arr); }
            }
            return 0;
        }
        catch { return -2; }
    }

    /// <summary>
    /// Attempts to receive a packet from the server.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    /// <param name="outBuf">Pointer to the output buffer to copy data into.</param>
    /// <param name="outCap">Capacity of the output buffer.</param>
    /// <param name="timeoutMs">Timeout in milliseconds. Pass &lt;= 0 to wait indefinitely.</param>
    /// <returns>
    /// &gt;0 = number of bytes written;  
    /// 0 = timeout (no data);  
    /// -1 = handle not found;  
    /// -2 = exception;  
    /// -3 = invalid buffer.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "rs_recv")]
    public static System.Int32 Recv(
        System.Int64 h,
        [System.Runtime.InteropServices.In] System.IntPtr outBuf,
        System.Int32 outCap,
        System.Int32 timeoutMs)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        if (outBuf == System.IntPtr.Zero || outCap <= 0)
        {
            return -3;
        }

        try
        {
            if (!c.TryRecv(timeoutMs, out var pkt) || pkt is null)
            {
                return 0; // timeout
            }

            System.Int32 n = System.Math.Min(outCap, pkt.Length);
            System.Runtime.InteropServices.Marshal.Copy(pkt, 0, outBuf, n);
            return n;
        }
        catch { return -2; }
    }

    /// <summary>
    /// Sets a native callback to be invoked on packet arrival.
    /// </summary>
    /// <param name="h">Handle of the client.</param>
    /// <param name="fnPtr">Pointer to the callback function.</param>
    /// <param name="user">User-defined pointer passed to callback.</param>
    /// <returns>
    /// 0 if success; -1 if handle not found; -2 if exception.
    /// </returns>
    [System.Diagnostics.StackTraceHidden]
    [System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "rs_setcb")]
    public static System.Int32 SetCb(
        System.Int64 h,
        [System.Runtime.InteropServices.In] System.IntPtr fnPtr,
        [System.Runtime.InteropServices.In] System.IntPtr user)
    {
        if (!_map.TryGetValue(h, out var c))
        {
            return -1;
        }

        try
        {
            var del = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<RsClient.rs_cb>(fnPtr);
            c.SetCb(del, user);
            return 0;
        }
        catch { return -2; }
    }
}
