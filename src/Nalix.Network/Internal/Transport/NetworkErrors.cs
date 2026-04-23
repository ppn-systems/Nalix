// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using Nalix.Common.Exceptions;
using Nalix.Network.Internal.Pooling;

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Provides cached, zero-allocation exception instances for common transport errors.
/// These instances avoid the overhead of stack trace generation by overriding the StackTrace property.
/// </summary>
internal static class NetworkErrors
{
    public static readonly SocketException ConnectionResetInternal = new CachedSocketException((int)SocketError.ConnectionReset);

    public static readonly NetworkException ConnectionReset = new CachedNetworkException("Connection closed by peer.", ConnectionResetInternal);

    public static readonly NetworkException SendFailed = new CachedNetworkException("The socket closed while sending.");

    public static readonly NetworkException MessageTooLarge = new CachedNetworkException("Frame size exceeds the wire header limit.");

    public static readonly NetworkException UdpPayloadTooLarge = new CachedNetworkException("UDP payload too large. Use TCP for large data.");

    public static readonly NetworkException UdpPartialSend = new CachedNetworkException("UDP partial send occurred.");

    public static readonly NetworkException UdpSendFailed = new CachedNetworkException("UDP transmission failed.");

    public static readonly SocketException MessageSize = new CachedSocketException((int)SocketError.MessageSize);

    public static readonly SocketException OperationAborted = new CachedSocketException((int)SocketError.OperationAborted);

    public static readonly SocketException ProtocolNotSupported = new CachedSocketException((int)SocketError.ProtocolNotSupported);

    public static readonly SocketException ConnectionAborted = new CachedSocketException((int)SocketError.ConnectionAborted);

    public static readonly SocketException Shutdown = new CachedSocketException((int)SocketError.Shutdown);

    /// <summary>
    /// Returns a cached <see cref="SocketException"/> for the given error code if available, 
    /// otherwise returns a new instance.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases", Justification = "<Pending>")]
    public static SocketException GetSocketError(SocketError error) => error switch
    {
        SocketError.OperationAborted => OperationAborted,
        SocketError.ConnectionAborted => ConnectionAborted,
        SocketError.ConnectionReset => ConnectionResetInternal,
        SocketError.MessageSize => MessageSize,
        SocketError.ProtocolNotSupported => ProtocolNotSupported,
        SocketError.Shutdown => Shutdown,
        _ => new SocketException((int)error)
    };

    public static readonly ObjectDisposedException PooledContextDisposed = new CachedObjectDisposedException(nameof(PooledSocketReceiveContext));

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
