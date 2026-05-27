// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using Nalix.Abstractions.Networking;
using Nalix.Environment.Configuration;
using Nalix.Network.Options;

namespace Nalix.Network.Internal.Transport;

internal sealed partial class SocketConnection
{
    private static readonly VarIntFramingOptions s_varIntOpts = ConfigurationManager.Instance.Get<VarIntFramingOptions>();

    private int _framingLocked;
    private int _maxVarIntPayloadSize = s_varIntOpts.MaxPayloadSize;
    private TransportFraming _framing = TransportFraming.UInt16LengthPrefixed;

    /// <summary>
    /// Sets the framing mode for this connection.
    /// Must be called before <see cref="BeginReceive"/> is invoked.
    /// </summary>
    public void SetFraming(TransportFraming framing, int maxPacketSize)
    {
        if (Volatile.Read(ref _framingLocked) == 1)
        {
            throw new InvalidOperationException("Cannot change framing mode after receiving has started or framing has already been locked.");
        }

        if (Interlocked.CompareExchange(ref _framingLocked, 1, 0) == 0)
        {
            _framing = framing;
            if (maxPacketSize > 0)
            {
                _maxVarIntPayloadSize = maxPacketSize;
            }
        }
        else
        {
            throw new InvalidOperationException("Framing mode has already been locked.");
        }
    }
}
