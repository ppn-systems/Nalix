// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;

namespace Nalix.Framework.DataFrames.Pooling;

/// <summary>
/// Represents exclusive ownership of a pooled packet instance.
/// Disposing the lease returns the packet to its originating pool.
/// </summary>
/// <typeparam name="TPacket">The packet type.</typeparam>
public sealed class PacketLease<TPacket> : IDisposable where TPacket : PacketBase<TPacket>, new()
{
    #region Fields

    private int _disposed;
    private readonly Action<TPacket> _return;

    #endregion Fields


    #region Constructors

    internal PacketLease(TPacket value, Action<TPacket> @return)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(@return);

        _return = @return;
        this.Value = value;
    }

    #endregion Constructors

    #region APIs

    /// <summary>
    /// Gets the rented packet instance.
    /// </summary>
    public TPacket Value { get; }

    /// <summary>
    /// Returns the packet to its pool. Double-dispose is ignored.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _return(this.Value);
    }

    #endregion APIs
}
