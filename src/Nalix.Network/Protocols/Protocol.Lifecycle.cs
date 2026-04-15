// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Nalix.Common.Abstractions;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking;
using Nalix.Common.Networking.Packets;
using Nalix.Framework.DataFrames.Transforms;
using Nalix.Framework.Extensions;
using Nalix.Framework.Memory.Buffers;
using Nalix.Network.Connections;

namespace Nalix.Network.Protocols;

public abstract partial class Protocol
{
    #region Fields

    private int _isDisposed;
    private int _keepConnectionOpen;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets or sets whether the protocol keeps the connection open after a message is processed.
    /// The flag is stored atomically because it is read during hot-path post-processing.
    /// </summary>
    public virtual bool KeepConnectionOpen
    {
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Interlocked.CompareExchange(ref _keepConnectionOpen, 0, 0) == 1;

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected set => Interlocked.Exchange(ref _keepConnectionOpen, value ? 1 : 0);
    }

    #endregion Properties

    #region Disposal

    /// <summary>
    /// Disposes resources used by this protocol instance.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);

        s_logger?.Trace($"[NW.{nameof(Protocol)}:{nameof(Dispose)}] disposed");

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Core disposal logic. Override to release managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose, false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        // The first caller flips the disposed flag from 0 to 1; later callers are ignored.
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        // Derived protocols can release managed resources when disposing == true.
    }

    #endregion Disposal

    #region Helpers

    private bool TryHandleProcessError(IConnectEventArgs args, Exception ex)
    {
        if (ex is CipherException or InvalidCastException or InvalidOperationException or SerializationFailureException)
        {
            s_logger?.Trace($"[NW.{nameof(Protocol)}:{nameof(ProcessMessage)}] {ex.Message}");
            return true;
        }

        args.Connection.ThrottledError(s_logger, "protocol.process_error", $"[NW.{nameof(Protocol)}:{nameof(ProcessMessage)}] Unhandled exception during message processing.", ex);
        return false;
    }

    private void ProcessDecrypt(IConnectEventArgs args)
    {
        IBufferLease? lease = args.Lease;

        if (args is not ConnectionEventArgs replaceable)
        {
            throw new InvalidCastException("IConnectEventArgs must be ConnectionEventArgs.");
        }

        if (lease is null)
        {
            throw new InvalidOperationException("Event args must have Lease.");
        }

        if ((uint)lease.Length <= (uint)PacketHeaderOffset.Flags)
        {
            throw new InvalidOperationException("Buffer length is invalid for decryption.");
        }

        PacketFlags flags = lease.Span.ReadFlagsLE();

        if (!flags.HasFlag(PacketFlags.ENCRYPTED))
        {
            return;
        }

        if (args.Connection.Secret is not { } secret)
        {
            args.Connection.Disconnect("Encrypted frame received before session key establishment.");
            throw new InvalidOperationException("Encrypted frame received before session key establishment.");
        }

        IBufferLease? dest = null;

        try
        {
            dest = PacketCipher.DecryptFrame(lease, secret, args.Connection.Algorithm);

            IBufferLease? old = replaceable.ExchangeLease(dest);
            old?.Dispose();
        }
        catch
        {
            dest?.Dispose();
            throw;
        }
    }

    private void ProcessDecompress(IConnectEventArgs args)
    {
        IBufferLease? lease = args.Lease;

        if (args is not ConnectionEventArgs replaceable)
        {
            throw new InvalidCastException("IConnectEventArgs must be ConnectionEventArgs.");
        }

        if (lease is null)
        {
            throw new InvalidOperationException("Event args must have Lease.");
        }

        if ((uint)lease.Length <= (uint)PacketHeaderOffset.Flags)
        {
            throw new InvalidOperationException("Buffer length is invalid for decompression.");
        }

        PacketFlags flags = lease.Span.ReadFlagsLE();

        if (!flags.HasFlag(PacketFlags.COMPRESSED))
        {
            return;
        }

        BufferLease? dest = null;

        try
        {
            dest = (BufferLease?)PacketCompression.DecompressFrame(lease);

            IBufferLease? old = replaceable.ExchangeLease(dest);
            old?.Dispose();
        }
        catch
        {
            dest?.Dispose();
            throw;
        }
    }

    #endregion Helpers
}
