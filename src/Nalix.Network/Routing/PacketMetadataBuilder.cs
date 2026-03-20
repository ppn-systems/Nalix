// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.


// Copyright (c) 2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using Nalix.Common.Networking.Packets;
using Nalix.Network.Routing.Metadata;

namespace Nalix.Network.Routing;

/// <summary>
/// Provides a mutable builder for constructing <see cref="PacketMetadata"/>
/// instances from attributes and metadata providers.
/// </summary>
public sealed class PacketMetadataBuilder
{
    /// <summary>
    /// Gets or sets the opcode attribute associated with the handler.
    /// </summary>
    public PacketOpcodeAttribute Opcode { get; set; }

    /// <summary>
    /// Gets or sets the timeout attribute associated with the handler.
    /// </summary>
    public PacketTimeoutAttribute Timeout { get; set; }

    /// <summary>
    /// Gets or sets the permission attribute associated with the handler.
    /// </summary>
    public PacketPermissionAttribute Permission { get; set; }

    /// <summary>
    /// Gets or sets the encryption attribute associated with the handler.
    /// </summary>
    public PacketEncryptionAttribute Encryption { get; set; }

    /// <summary>
    /// Gets or sets the rate limit attribute associated with the handler.
    /// </summary>
    public PacketRateLimitAttribute RateLimit { get; set; }

    /// <summary>
    /// Gets or sets the concurrency limit attribute associated with the handler.
    /// </summary>
    public PacketConcurrencyLimitAttribute ConcurrencyLimit { get; set; }

    private readonly System.Collections.Generic.Dictionary<System.Type, System.Attribute> _custom = [];

    /// <summary>
    /// Adds or replaces a custom attribute in the metadata builder.
    /// </summary>
    /// <param name="attribute">The attribute to add.</param>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="attribute"/> is <see langword="null"/>.
    /// </exception>
    public void Add(System.Attribute attribute)
    {
        System.ArgumentNullException.ThrowIfNull(attribute);
        _custom[attribute.GetType()] = attribute;
    }

    /// <summary>
    /// Gets a custom attribute of the specified type if it has been added.
    /// </summary>
    /// <typeparam name="TAttribute">The attribute type to retrieve.</typeparam>
    /// <returns>
    /// The attribute instance if it exists; otherwise, <see langword="null"/>.
    /// </returns>
    public TAttribute Get<TAttribute>() where TAttribute : System.Attribute => _custom.TryGetValue(typeof(TAttribute), out System.Attribute value) ? (TAttribute)value : null;

    /// <summary>
    /// Builds an immutable <see cref="PacketMetadata"/> instance from
    /// the current builder state.
    /// </summary>
    /// <returns>A new <see cref="PacketMetadata"/> instance.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the <see cref="Opcode"/> is <see langword="null"/>.
    /// </exception>
    public PacketMetadata Build()
    {
        return Opcode is null
            ? throw new System.InvalidOperationException("PacketMetadata requires a non-null Opcode. Ensure that a PacketOpcodeAttribute is present.")
            : new PacketMetadata(
                Opcode,
                Timeout,
                Permission,
                Encryption,
                RateLimit,
                ConcurrencyLimit,
                new System.Collections.Generic.Dictionary<System.Type, System.Attribute>(_custom));
    }
}