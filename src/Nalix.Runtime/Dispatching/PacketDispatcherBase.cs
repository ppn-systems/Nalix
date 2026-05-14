// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nalix.Abstractions.Networking.Packets;
using Nalix.Network.Routing;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Serves as the base class for packet dispatchers, offering Abstractions configuration and logging support.
/// </summary>
/// <typeparam name="TPacket">
/// The packet type that implements both <see cref="IPacket"/>.
/// </typeparam>
public abstract class PacketDispatcherBase<TPacket> where TPacket : IPacket
{
    #region Properties

    /// <summary>
    /// Gets the logger instance associated with this dispatcher, if configured.
    /// </summary>
    protected ILogger? Logging => this.Options.Logging;

    /// <summary>
    /// Gets the configuration options for this dispatcher instance.
    /// </summary>
    protected PacketDispatchOptions<TPacket> Options { get; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}"/> class
    /// using the provided <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The dispatcher configuration options. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    [SuppressMessage("Style", "IDE0016:Use 'throw' expression", Justification = "<Pending>")]
    [SuppressMessage("Maintainability", "CA1510:Use ArgumentNullException throw helper", Justification = "<Pending>")]
    protected PacketDispatcherBase(PacketDispatchOptions<TPacket> options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        this.Options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketDispatcherBase{TPacket}"/> class
    /// with optional configuration logic.
    /// </summary>
    /// <param name="configure">
    /// An optional delegate to configure the <see cref="PacketDispatchOptions{TPacket}"/> instance.
    /// </param>
    [SuppressMessage("Style", "IDE1005:Delegate invocation can be simplified.", Justification = "<Pending>")]
    protected PacketDispatcherBase(Action<PacketDispatchOptions<TPacket>>? configure = null) : this(new PacketDispatchOptions<TPacket>())
    {
        if (configure != null)
        {
            configure(this.Options);
        }
    }

    #endregion Constructors
}
