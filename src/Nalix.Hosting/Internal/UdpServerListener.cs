// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions;
using Nalix.Abstractions.Exceptions;
using Nalix.Abstractions.Networking;
using Nalix.Codec.Transforms;
using Nalix.Hosting.Internal.Exceptions;
using Nalix.Network.Connections;
using Nalix.Network.Listeners.Udp;

namespace Nalix.Hosting.Internal;

/// <inheritdoc />
internal sealed class UdpServerListener : UdpListenerBase
{
    private readonly Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool>? _authen;

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol, IConnectionHub hub) : base(protocol, hub) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol, IConnectionHub hub) : base(port, protocol, hub) { }

    /// <inheritdoc />
    public UdpServerListener(ushort port, IProtocol protocol, IConnectionHub hub, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(port, protocol, hub) => _authen = authen;

    /// <inheritdoc />
    public UdpServerListener(IProtocol protocol, IConnectionHub hub, Func<IConnection, System.Net.EndPoint, ReadOnlySpan<byte>, bool> authen)
        : base(protocol, hub) => _authen = authen;

    /// <inheritdoc />
    public override bool IsAuthenticated(IConnection connection, System.Net.EndPoint remoteEndPoint, ReadOnlySpan<byte> payload)
    {
        if (_authen != null)
        {
            return _authen(connection, remoteEndPoint, payload);
        }

        // By default, hosting allows all datagrams that pass the session token check.
        return true;
    }

    public override void ProcessFrame(object? sender, IConnectEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args is not ConnectionEventArgs replaceable)
        {
            return;
        }

        if (args.Lease is not { } lease)
        {
            Throw.EventArgsMustHaveLease();
            return;
        }
        IBufferLease current = lease;
        bool exchanged = false;

        try
        {
            FramePipeline.ProcessInbound(ref current, args.Connection.Secret.AsSpan(), args.Connection.Algorithm, out uint? seq);

            if (!args.Connection.UDP.ReceiveSequence.IsValid(seq, window: this.SequenceOptions.UdpWindow))
            {
                current.Dispose();
                return;
            }

            if (!ReferenceEquals(current, lease))
            {
                replaceable.ExchangeLease(current)?.Dispose();
                lease = current;
                exchanged = true;
            }

            this.Protocol.ProcessMessage(sender, args);

            if (seq.HasValue)
            {
                args.Connection.UDP.ReceiveSequence.UpdateTo(seq.Value);
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (ex is CipherException or InvalidCastException or InvalidOperationException or SerializationFailureException or ArgumentOutOfRangeException)
            {
#if DEBUG
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessFrame)}] {ex.Message}");
                }
#endif
            }
            else
            {
                args.Connection.ThrottledError(this.Logger, "protocol.process_error", $"[NW.{nameof(UdpListenerBase)}:{nameof(ProcessFrame)}] Unhandled exception during message processing.", ex);
            }
        }
        finally
        {
            if (!exchanged && !ReferenceEquals(current, lease))
            {
                current.Dispose();
            }
        }
    }
}
