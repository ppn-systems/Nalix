// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Nalix.Abstractions.Exceptions;
using Nalix.Network.Internal.Pooling;

namespace Nalix.Network.Internal;

/// <summary>
/// Provides cached, zero-allocation exception instances for Abstractions transport errors.
/// These instances avoid the overhead of stack trace generation by overriding the StackTrace property.
/// </summary>
internal static class Throw
{
    /// <inheritdoc/>
    public static readonly SocketException Shutdown = new CachedSocketException((int)SocketError.Shutdown);

    /// <inheritdoc/>
    public static readonly SocketException MessageSize = new CachedSocketException((int)SocketError.MessageSize);

    /// <inheritdoc/>
    public static readonly NetworkException UdpSendFailed = new CachedNetworkException("UDP transmission failed.");

    /// <inheritdoc/>
    public static readonly NetworkException UdpPartialSend = new CachedNetworkException("UDP partial send occurred.");

    /// <inheritdoc/>
    public static readonly NetworkException SendFailed = new CachedNetworkException("The socket closed while sending.");

    /// <inheritdoc/>
    public static readonly SocketException OperationAborted = new CachedSocketException((int)SocketError.OperationAborted);

    /// <inheritdoc/>
    public static readonly SocketException ConnectionAborted = new CachedSocketException((int)SocketError.ConnectionAborted);

    /// <inheritdoc/>
    public static readonly SocketException ConnectionResetInternal = new CachedSocketException((int)SocketError.ConnectionReset);

    /// <inheritdoc/>
    public static readonly SocketException ProtocolNotSupported = new CachedSocketException((int)SocketError.ProtocolNotSupported);

    /// <inheritdoc/>
    public static readonly NetworkException UdpPayloadTooLarge = new CachedNetworkException("UDP payload too large. Use TCP for large data.");

    /// <inheritdoc/>
    public static readonly NetworkException ConnectionReset = new CachedNetworkException("Connection closed by peer.", ConnectionResetInternal);

    /// <summary>
    /// Returns a cached <see cref="SocketException"/> for the given error code if available, 
    /// otherwise returns a new instance.
    /// </summary>
    [SuppressMessage("Style", "IDE0072:Add missing cases", Justification = "<Pending>")]
    public static SocketException GetSocketError(SocketError error) => error switch
    {
        SocketError.Shutdown => Shutdown,
        SocketError.MessageSize => MessageSize,
        SocketError.OperationAborted => OperationAborted,
        SocketError.ConnectionAborted => ConnectionAborted,
        SocketError.ConnectionReset => ConnectionResetInternal,
        SocketError.ProtocolNotSupported => ProtocolNotSupported,
        _ => new SocketException((int)error)
    };

    public static readonly ObjectDisposedException PooledContextDisposed = new CachedObjectDisposedException(nameof(PooledSocketReceiveContext));

    #region Throw Helpers

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ConnectionResetNow() => throw ConnectionReset;

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SendFailedNow() => throw SendFailed;

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UdpPayloadTooLargeNow() => throw UdpPayloadTooLarge;

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UdpPartialSendNow() => throw UdpPartialSend;

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UdpSendFailedNow() => throw UdpSendFailed;

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ProtocolNotSupportedNow() => throw ProtocolNotSupported;

    [StackTraceHidden]
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void PooledContextDisposedNow() => throw PooledContextDisposed;

    #endregion Throw Helpers

    private sealed class CachedObjectDisposedException(string objectName) : ObjectDisposedException(objectName)
    {
        public override string? StackTrace => "   at Nalix.Network.Internal.Transport (Cached Exception)";
    }

    private sealed class CachedNetworkException : NetworkException
    {
        public CachedNetworkException(string message) : base(message) { }
        public CachedNetworkException(string message, Exception inner) : base(message, inner) { }

        // Override StackTrace to return a static string, avoiding the expensive scan.
        public override string? StackTrace => "   at Nalix.Network.Internal.Transport (Cached Exception)";
    }

    private sealed class CachedSocketException(int errorCode) : SocketException(errorCode)
    {
        public override string? StackTrace => "   at Nalix.Network.Internal.Transport (Cached Exception)";
    }
}
