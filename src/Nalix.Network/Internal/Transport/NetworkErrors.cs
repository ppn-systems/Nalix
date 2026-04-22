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

    public static readonly SocketException MessageSize = new CachedSocketException((int)SocketError.MessageSize);

    public static readonly SocketException OperationAborted = new CachedSocketException((int)SocketError.OperationAborted);

    public static readonly SocketException ProtocolNotSupported = new CachedSocketException((int)SocketError.ProtocolNotSupported);

    public static readonly SocketException ConnectionAborted = new CachedSocketException((int)SocketError.ConnectionAborted);

    public static readonly SocketException Shutdown = new CachedSocketException((int)SocketError.Shutdown);

    /// <summary>
    /// Returns a cached <see cref="SocketException"/> for the given error code if available, 
    /// otherwise returns a new instance.
    /// </summary>
    public static SocketException GetSocketError(SocketError error) => error switch
    {
        SocketError.OperationAborted => OperationAborted,
        SocketError.ConnectionAborted => ConnectionAborted,
        SocketError.ConnectionReset => ConnectionResetInternal,
        SocketError.MessageSize => MessageSize,
        SocketError.ProtocolNotSupported => ProtocolNotSupported,
        SocketError.Shutdown => Shutdown,
        SocketError.SocketError => throw new NotImplementedException(),
        SocketError.Success => throw new NotImplementedException(),
        SocketError.IOPending => throw new NotImplementedException(),
        SocketError.Interrupted => throw new NotImplementedException(),
        SocketError.AccessDenied => throw new NotImplementedException(),
        SocketError.Fault => throw new NotImplementedException(),
        SocketError.InvalidArgument => throw new NotImplementedException(),
        SocketError.TooManyOpenSockets => throw new NotImplementedException(),
        SocketError.WouldBlock => throw new NotImplementedException(),
        SocketError.InProgress => throw new NotImplementedException(),
        SocketError.AlreadyInProgress => throw new NotImplementedException(),
        SocketError.NotSocket => throw new NotImplementedException(),
        SocketError.DestinationAddressRequired => throw new NotImplementedException(),
        SocketError.ProtocolType => throw new NotImplementedException(),
        SocketError.ProtocolOption => throw new NotImplementedException(),
        SocketError.SocketNotSupported => throw new NotImplementedException(),
        SocketError.OperationNotSupported => throw new NotImplementedException(),
        SocketError.ProtocolFamilyNotSupported => throw new NotImplementedException(),
        SocketError.AddressFamilyNotSupported => throw new NotImplementedException(),
        SocketError.AddressAlreadyInUse => throw new NotImplementedException(),
        SocketError.AddressNotAvailable => throw new NotImplementedException(),
        SocketError.NetworkDown => throw new NotImplementedException(),
        SocketError.NetworkUnreachable => throw new NotImplementedException(),
        SocketError.NetworkReset => throw new NotImplementedException(),
        SocketError.NoBufferSpaceAvailable => throw new NotImplementedException(),
        SocketError.IsConnected => throw new NotImplementedException(),
        SocketError.NotConnected => throw new NotImplementedException(),
        SocketError.TimedOut => throw new NotImplementedException(),
        SocketError.ConnectionRefused => throw new NotImplementedException(),
        SocketError.HostDown => throw new NotImplementedException(),
        SocketError.HostUnreachable => throw new NotImplementedException(),
        SocketError.ProcessLimit => throw new NotImplementedException(),
        SocketError.SystemNotReady => throw new NotImplementedException(),
        SocketError.VersionNotSupported => throw new NotImplementedException(),
        SocketError.NotInitialized => throw new NotImplementedException(),
        SocketError.Disconnecting => throw new NotImplementedException(),
        SocketError.TypeNotFound => throw new NotImplementedException(),
        SocketError.HostNotFound => throw new NotImplementedException(),
        SocketError.TryAgain => throw new NotImplementedException(),
        SocketError.NoRecovery => throw new NotImplementedException(),
        SocketError.NoData => throw new NotImplementedException(),
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
