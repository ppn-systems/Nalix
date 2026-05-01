// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using Nalix.Codec.DataFrames;

namespace Nalix.Runtime.Pooling;

/// <summary>
/// A zero-allocation wrapper that ensures a rented packet is returned to its pool upon disposal.
/// </summary>
/// <typeparam name="TPacket">The packet type.</typeparam>
public readonly struct PacketScope<TPacket> : IDisposable where TPacket : PacketBase<TPacket>, new()
{
    #region Fields

    private readonly TPacket _packet;

    #endregion Fields

    #region Constructor

    /// <summary>
    /// Initializes a new lease for the specified packet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketScope(TPacket packet) => _packet = packet;

    #endregion Constructor

    #region Properties

    /// <summary>
    /// Gets the rented packet instance.
    /// </summary>
    public TPacket Value => _packet;

    /// <summary>
    /// Returns true if this lease is valid (contains a packet).
    /// </summary>
    public bool IsValid => _packet != null;

    #endregion Properties

    #region Methods

    /// <summary>
    /// Returns the packet to its pool by disposing the underlying instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _packet?.Dispose();

    /// <summary>
    /// Implicitly converts the lease to its underlying packet instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TPacket(PacketScope<TPacket> lease) => lease._packet;

    #endregion Methods
}
