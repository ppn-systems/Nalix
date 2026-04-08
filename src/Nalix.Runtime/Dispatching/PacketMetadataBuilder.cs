// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using Nalix.Common.Exceptions;
using Nalix.Common.Networking.Packets;

namespace Nalix.Runtime.Dispatching;

/// <summary>
/// Collects packet-related attributes and turns them into a single immutable
/// <see cref="PacketMetadata"/> instance.
/// The builder is intended for one registration pass, then one build step.
/// </summary>
public sealed class PacketMetadataBuilder
{
    #region Fields

    private readonly Dictionary<Type, Attribute> _custom = [];

    #endregion Fields

    /// <summary>
    /// Gets or sets the opcode attribute that identifies the packet handler.
    /// </summary>
    public PacketOpcodeAttribute? Opcode { get; set; }

    /// <summary>
    /// Gets or sets the timeout attribute that limits handler execution time.
    /// </summary>
    public PacketTimeoutAttribute? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the permission attribute required to execute the handler.
    /// </summary>
    public PacketPermissionAttribute? Permission { get; set; }

    /// <summary>
    /// Gets or sets the encryption attribute that describes transport protection.
    /// </summary>
    public PacketEncryptionAttribute? Encryption { get; set; }

    /// <summary>
    /// Gets or sets the rate limit attribute that constrains handler throughput.
    /// </summary>
    public PacketRateLimitAttribute? RateLimit { get; set; }

    /// <summary>
    /// Gets or sets the concurrency attribute that caps parallel handler execution.
    /// </summary>
    public PacketConcurrencyLimitAttribute? ConcurrencyLimit { get; set; }

    /// <summary>
    /// Adds or replaces a custom attribute in the metadata builder.
    /// The last attribute of a given type wins, which keeps repeated scans
    /// deterministic when multiple providers contribute metadata.
    /// </summary>
    /// <param name="attribute">The attribute to add.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="attribute"/> is <see langword="null"/>.
    /// </exception>
    public void Add(Attribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        _custom[attribute.GetType()] = attribute;
    }

    /// <summary>
    /// Gets a custom attribute of the specified type if it has been added.
    /// This is primarily useful when a later stage wants to inspect optional
    /// metadata without knowing which provider produced it.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to retrieve.</typeparam>
    /// <returns>
    /// The attribute instance if it exists; otherwise, <see langword="null"/>.
    /// </returns>
    public TAttribute? Get<TAttribute>() where TAttribute : Attribute => _custom.TryGetValue(typeof(TAttribute), out Attribute? value) ? value as TAttribute : null;

    /// <summary>
    /// Builds an immutable <see cref="PacketMetadata"/> instance from the
    /// current builder state.
    /// The builder contents are copied into the result so previously built
    /// metadata stays isolated from later edits.
    /// </summary>
    /// <returns>A new <see cref="PacketMetadata"/> instance.</returns>
    /// <exception cref="InternalErrorException">
    /// Thrown when the <see cref="Opcode"/> is <see langword="null"/>.
    /// </exception>
    public PacketMetadata Build()
    {
        return this.Opcode is null
            ? throw new InternalErrorException("PacketMetadata requires a non-null Opcode. Ensure that a PacketOpcodeAttribute is present.")
            : new PacketMetadata(
                this.Opcode,
                this.Timeout,
                this.Permission,
                this.Encryption,
                this.RateLimit,
                this.ConcurrencyLimit,
                new Dictionary<Type, Attribute>(_custom));
    }
}
