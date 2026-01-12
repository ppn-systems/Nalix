// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Core.Abstractions;

/// <summary>
/// Represents a normalized, hashable network endpoint key.
/// </summary>
/// <remarks>
/// Implementations should provide a canonical representation of the
/// underlying network address (IPv4/IPv6) and an optional port
/// component when applicable.
/// </remarks>
public interface INetworkEndpoint
{
    /// <summary>
    /// Gets the canonical textual representation of the address part
    /// (IPv4 or IPv6), without any port information.
    /// </summary>
    System.String Address { get; }
}
