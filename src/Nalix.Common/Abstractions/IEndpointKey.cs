// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Abstractions;

/// <summary>
/// Represents a normalized, hashable network endpoint key.
/// </summary>
/// <remarks>
/// Implementations should provide a canonical representation of the
/// underlying network address (IPv4/IPv6) and an optional port
/// component when applicable.
/// </remarks>
public interface IEndpointKey : System.IEquatable<IEndpointKey>
{
    /// <summary>
    /// Gets the canonical textual representation of the address part
    /// (IPv4 or IPv6), without any port information.
    /// </summary>
    System.String Address { get; }

    /// <summary>
    /// Gets the port associated with the endpoint, or <c>0</c>
    /// when the key does not include a port component.
    /// </summary>
    System.Int32 Port { get; }

    /// <summary>
    /// Indicates whether this key represents an endpoint that includes
    /// a port component.
    /// </summary>
    System.Boolean HasPort { get; }

    /// <summary>
    /// Indicates whether the underlying address is IPv6.
    /// </summary>
    System.Boolean IsIPv6 { get; }
}
