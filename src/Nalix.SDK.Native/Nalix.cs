// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Exceptions;
using Nalix.SDK.Native.Wrappers;
using Nalix.SDK.Options;
using Nalix.SDK.Transport;

[assembly: CLSCompliant(false)]

namespace Nalix.SDK.Native;

/// <summary>
/// Provides the main native C ABI interface for the Nalix SDK.
/// 
/// This class exposes high-performance TCP networking functionality through 
/// a clean, stable C-style API that can be consumed by Java (JNI/JNA), C/C++, 
/// Rust, Python, Go, and other languages via FFI.
/// </summary>
[DebuggerNonUserCode]
[DebuggerStepThrough]
public static unsafe partial class Nalix
{
    #region TcpSession Management

    /// <summary>
    /// Creates a new TcpSession instance.
    /// </summary>
    /// <param name="optionsPtr">Pointer to a <see cref="TransportOptions"/> structure in native memory.</param>
    /// <returns>
    /// A valid session handle on success, or <see cref="IntPtr.Zero"/> if creation failed.
    /// </returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Create,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static IntPtr TcpCreate(IntPtr optionsPtr)
    {
        try
        {
            TransportOptions? options = Marshal.PtrToStructure<TransportOptions>(optionsPtr);

            options ??= new TransportOptions();

            TcpSession session = new(options);

            GCHandle gcHandle = GCHandle.Alloc(null, GCHandleType.Normal);
            IntPtr handle = GCHandle.ToIntPtr(gcHandle);

            NativeTcpSession wrapper = new(session, handle);
            gcHandle.Target = wrapper;

            return handle;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Connects the TcpSession to the specified remote host.
    /// </summary>
    /// <param name="handle">The session handle returned by <see cref="TcpCreate"/>.</param>
    /// <param name="host">Pointer to a null-terminated UTF-8 string containing the hostname or IP address.</param>
    /// <param name="port">The remote port number.</param>
    /// <returns><see cref="ErrorCode.Success"/> if successful, otherwise an error code.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Connect,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpConnect(IntPtr handle, byte* host, ushort port)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);

        if (wrapper == null)
        {
            return ErrorCode.InvalidHandle;
        }

        try
        {
            string? hostStr = Marshal.PtrToStringUTF8((IntPtr)host);

            if (host == null)
            {
                return ErrorCode.InvalidArgument;
            }

            wrapper.UnderlyingSession.ConnectAsync(hostStr, port).Wait();
            return ErrorCode.Success;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return ErrorCode.ConnectionFailed;
        }
    }

    /// <summary>
    /// Sends raw binary data over the TCP connection.
    /// </summary>
    /// <param name="handle">The session handle returned by <see cref="TcpCreate"/>.</param>
    /// <param name="data">Pointer to the buffer containing data to send.</param>
    /// <param name="length">The number of bytes to send.</param>
    /// <param name="encrypt">1 = enable encryption, 0 = disable encryption.</param>
    /// <returns><see cref="ErrorCode.Success"/> if the data was sent successfully.</returns>
    [SkipLocalsInit]
    [SuppressGCTransition]
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Send,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpSend(IntPtr handle, byte* data, int length, byte encrypt)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return ErrorCode.InvalidHandle;
        }

        try
        {
            ReadOnlySpan<byte> span = new(data, length);
            wrapper.UnderlyingSession.Send(span, encrypt != 0);

            return ErrorCode.Success;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return ErrorCode.SendFailed;
        }
    }

    /// <summary>
    /// Gracefully disconnects from the server.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <returns><see cref="ErrorCode.Success"/>.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Disconnect,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpDisconnect(IntPtr handle)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        wrapper?.UnderlyingSession.DisconnectAsync().Wait();
        return ErrorCode.Success;
    }

    /// <summary>
    /// Releases all resources used by the session.
    /// </summary>
    /// <param name="handle">The session handle to free.</param>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Free,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void TcpFree(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        GCHandle gcHandle = GCHandle.FromIntPtr(handle);
        if (gcHandle.IsAllocated)
        {
            (gcHandle.Target as IDisposable)?.Dispose();
            gcHandle.Free();
        }
    }

    #endregion

    #region Callbacks

    /// <summary>
    /// Registers callback for successful connection event.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="callback">Function pointer to be called when connected.</param>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Events.OnConnected,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void TcpOnConnected(IntPtr handle, delegate* unmanaged<IntPtr, void> callback)
    {
        if (GET_WRAPPER(handle) is { } w)
        {
            w.OnConnectedCallback = callback;
        }
    }

    /// <summary>
    /// Registers callback for incoming messages.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="callback">Function pointer: (handle, dataPtr, dataLength).</param>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Events.OnMessage,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void TcpOnMessage(IntPtr handle, delegate* unmanaged<IntPtr, byte*, int, void> callback)
    {
        if (GET_WRAPPER(handle) is { } w)
        {
            w.OnMessageCallback = callback;
        }
    }

    /// <summary>
    /// Registers callback for error events.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="callback">Function pointer: (handle, errorMessagePtr, messageLength).</param>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Events.OnError,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void TcpOnError(IntPtr handle, delegate* unmanaged<IntPtr, byte*, int, void> callback)
    {
        if (GET_WRAPPER(handle) is { } w)
        {
            w.OnErrorCallback = callback;
        }
    }

    /// <summary>
    /// Registers callback for disconnection events.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="callback">Function pointer to be called when disconnected.</param>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Events.OnDisconnected,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static void TcpOnDisconnected(IntPtr handle, delegate* unmanaged<IntPtr, void> callback)
    {
        if (GET_WRAPPER(handle) is { } w)
        {
            w.OnDisconnectedCallback = callback;
        }
    }

    #endregion Callbacks
}
