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
using Nalix.Network.Listeners.Tcp;

namespace Nalix.Hosting.Internal;

/// <inheritdoc />
internal sealed class TcpServerListener : TcpListenerBase
{
    /// <inheritdoc />
    public TcpServerListener(IProtocol protocol, IConnectionHub hub) : base(protocol, hub) { }

    /// <inheritdoc />
    public TcpServerListener(ushort port, IProtocol protocol, IConnectionHub hub) : base(port, protocol, hub) { }

    /// <inheritdoc />
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

        bool exchanged = false;
        IBufferLease current = lease;

        try
        {
            FramePipeline.ProcessInbound(ref current, args.Connection.Secret.AsSpan(), args.Connection.Algorithm, out uint? seq);

            if (!args.Connection.TCP.ReceiveSequence.IsValid(seq, window: this.SequenceOptions.TcpWindow))
            {
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
                args.Connection.TCP.ReceiveSequence.UpdateTo(seq.Value);
            }
        }
        catch (Exception ex) when (ExceptionClassifier.IsNonFatal(ex))
        {
            if (ex is CipherException or InternalErrorException or SerializationFailureException or LZ4Exception)
            {
                if (this.Logger != null && this.Logger.IsEnabled(LogLevel.Trace))
                {
                    this.Logger.LogTrace($"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessFrame)}] {ex.Message}");
                }
            }
            else
            {
                args.Connection.ThrottledError(
                    this.Logger, "protocol.process_error",
                    $"[NW.{nameof(TcpListenerBase)}:{nameof(ProcessFrame)}] Unhandled exception during message processing.", ex);
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
