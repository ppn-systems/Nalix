// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Net.Sockets;
using Nalix.Common.Exceptions;

namespace Nalix.Network.Internal.Transport;

/// <summary>
/// Provides cached, zero-allocation exception instances for common transport errors.
/// These instances avoid the overhead of stack trace generation by overriding the StackTrace property.
/// </summary>
internal static class NetworkErrors
{
    public static readonly NetworkException ConnectionReset = new CachedNetworkException("Connection closed by peer.", new SocketException((int)SocketError.ConnectionReset));

    public static readonly NetworkException SendFailed = new CachedNetworkException("The socket closed while sending.");

    public static readonly NetworkException MessageTooLarge = new CachedNetworkException("Frame size exceeds the wire header limit.");

    public static readonly SocketException MessageSize = new CachedSocketException((int)SocketError.MessageSize);

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
