// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking.Protocols;
using Nalix.Abstractions.Security;
using Nalix.SDK.Native.Results;
using Nalix.SDK.Native.Wrappers;
using Nalix.SDK.Transport.Extensions;

#pragma warning disable IDE0058 // Expression value is never used
#pragma warning disable CA2012 // Use ValueTasks correctly

namespace Nalix.SDK.Native;

public static unsafe partial class Nalix
{
    /// <summary>
    /// Connects then attempts session resume with fallback to full handshake.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="host">Pointer to null-terminated UTF-8 string.</param>
    /// <param name="port">Remote port.</param>
    /// <returns><see cref="ErrorCode.Success"/> on successful connection.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.ConnectWithResume,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpConnectWithResume(IntPtr handle, byte* host, ushort port)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return ErrorCode.InvalidHandle;
        }

        try
        {
            string hostStr = Marshal.PtrToStringUTF8((IntPtr)host) ?? "";
            wrapper.UnderlyingSession.ConnectWithResumeAsync(hostStr, port).GetAwaiter().GetResult();
            return ErrorCode.Success;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return ErrorCode.ConnectionFailed;
        }
    }


    /// <summary>
    /// Performs the X25519 cryptographic handshake with the server.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <returns><see cref="ErrorCode.Success"/> if handshake completed successfully.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Handshake,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpHandshake(IntPtr handle)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return ErrorCode.InvalidHandle;
        }

        try
        {
            wrapper.UnderlyingSession.HandshakeAsync().GetAwaiter().GetResult();
            return ErrorCode.Success;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return ErrorCode.HandshakeFailed;
        }
    }

    /// <summary>
    /// Performs a graceful disconnect by first sending a DISCONNECT control frame 
    /// to the server, then closes the local connection.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="reason">Protocol reason code. Default = <see cref="ProtocolReason.NONE"/>.</param>
    /// <returns><see cref="ErrorCode.Success"/> if successful.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.DisconnectGraceful,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpDisconnectGraceful(IntPtr handle, ushort reason = 0)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return ErrorCode.InvalidHandle;
        }

        try
        {
            wrapper.UnderlyingSession.DisconnectGracefullyAsync(reason: (ProtocolReason)reason).GetAwaiter().GetResult();
            return ErrorCode.Success;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return ErrorCode.DisconnectFailed;
        }
    }

    /// <summary>
    /// Sends PING and measures round-trip time.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 5000).</param>
    /// <returns><see cref="TcpPingResult"/> containing RTT and error code.</returns>
    [SkipLocalsInit]
    [SuppressGCTransition]
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.Ping,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static TcpPingResult TcpPing(IntPtr handle, int timeoutMs = 5000)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return new TcpPingResult { ErrorCode = ErrorCode.InvalidHandle };
        }

        try
        {
            double rtt = wrapper.UnderlyingSession.PingAsync(timeoutMs).GetAwaiter().GetResult();
            return new TcpPingResult { RttMs = rtt, ErrorCode = ErrorCode.Success };
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return new TcpPingResult { ErrorCode = ErrorCode.Timeout };
        }
    }

    /// <summary>
    /// Performs time synchronization with the server.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 5000).</param>
    /// <returns><see cref="TcpTimeSyncResult"/> containing RTT, adjustment and error code.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.SyncTime,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static TcpTimeSyncResult TcpSyncTime(IntPtr handle, int timeoutMs = 5000)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return new TcpTimeSyncResult { ErrorCode = ErrorCode.InvalidHandle };
        }

        try
        {
            (double RttMs, double AdjustedMs) = wrapper.UnderlyingSession.SyncTimeAsync(timeoutMs).GetAwaiter().GetResult();
            return new TcpTimeSyncResult
            {
                RttMs = RttMs,
                AdjustedMs = AdjustedMs,
                ErrorCode = ErrorCode.Success
            };
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return new TcpTimeSyncResult { ErrorCode = ErrorCode.Timeout };
        }
    }

    /// <summary>
    /// Attempts to resume a previous session.
    /// </summary>
    /// <param name="handle">The session handle (must be connected).</param>
    /// <returns><see cref="TcpResumeResult"/> containing reason and error code.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.ResumeSession,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static TcpResumeResult TcpResumeSession(IntPtr handle)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return new TcpResumeResult { ErrorCode = ErrorCode.InvalidHandle };
        }

        try
        {
            ProtocolReason reason = wrapper.UnderlyingSession.ResumeSessionAsync().GetAwaiter().GetResult();
            return new TcpResumeResult
            {
                Reason = (ushort)reason,
                ErrorCode = ErrorCode.Success
            };
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return new TcpResumeResult { ErrorCode = ErrorCode.OperationFailed };
        }
    }

    /// <summary>
    /// Dynamically updates the cipher suite of the active connection.
    /// Both client and server will switch after successful ACK.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="cipherSuite">New <see cref="CipherSuiteType"/> value.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 5000).</param>
    /// <returns><see cref="ErrorCode.Success"/> if cipher was updated successfully.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.UpdateCipher,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpUpdateCipher(IntPtr handle, byte cipherSuite, int timeoutMs = 5000)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);
        if (wrapper == null)
        {
            return ErrorCode.InvalidHandle;
        }

        try
        {
            wrapper.UnderlyingSession.UpdateCipherAsync(cipherSuite: (CipherSuiteType)cipherSuite, timeoutMs: timeoutMs).GetAwaiter().GetResult();

            return ErrorCode.Success;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return ErrorCode.OperationFailed;
        }
    }

    /// <summary>
    /// Sends a CONTROL frame with common parameters.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <param name="opCode">Operation code (usually 0 for SYSTEM_CONTROL).</param>
    /// <param name="controlType"><see cref="ControlType"/> value.</param>
    /// <param name="seq">Sequence number (optional).</param>
    /// <param name="reason">Reason code (optional).</param>
    /// <returns><see cref="ErrorCode.Success"/> if the control frame was sent.</returns>
    [UnmanagedCallersOnly(
        EntryPoint = NativeMethods.Tcp.SendControl,
        CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    public static int TcpSendControl(IntPtr handle, ushort opCode, ushort controlType, ushort seq = 0, ushort reason = 0)
    {
        NativeTcpSession? wrapper = GET_WRAPPER(handle);

        if (wrapper == null)
        {
            return ErrorCode.InvalidHandle;
        }

        try
        {
            wrapper.UnderlyingSession.SendControlAsync(
                opCode: opCode,
                type: (ControlType)controlType,
                configure: ctrl =>
                {
                    ctrl.SequenceId = seq;
                    ctrl.Reason = (ProtocolReason)reason;
                }).GetAwaiter().GetResult();

            return ErrorCode.Success;
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            LastError.Set(ex);
            return ErrorCode.SendFailed;
        }
    }
}
