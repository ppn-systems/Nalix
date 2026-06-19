// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

namespace Nalix.Abstractions.Networking;

/// <summary>
/// Represents a normalized and hashable network endpoint identifier.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="INetworkEndpoint"/> provides a canonical representation of a
/// network address that can be safely used for comparisons, dictionary keys,
/// or caching scenarios.
/// </para>
/// <para>
/// Implementations are expected to normalize address formatting
/// (for example IPv6 compression rules or IPv4 standard notation)
/// so that logically equivalent endpoints produce identical values.
/// </para>
/// <para>
/// The endpoint may optionally include a port component depending on
/// the underlying transport or protocol requirements.
/// </para>
/// </remarks>
public interface INetworkEndpoint
{
    /// <summary>
    /// Gets the canonical textual representation of the network address.
    /// </summary>
    /// <value>
    /// A normalized IPv4 or IPv6 address string that does not include
    /// port information. For authentication/pinning checks, compare this
    /// value together with <see cref="Port"/> when <see cref="HasPort"/> is true.
    /// </value>
    /// <remarks>
    /// The returned value must be stable and suitable for equality
    /// comparisons and hashing.
    /// </remarks>
    string Address { get; }

    /// <summary>
    /// Gets the port associated with the endpoint.
    /// </summary>
    /// <value>
    /// The port number when <see cref="HasPort"/> is <see langword="true"/>;
    /// otherwise the value is implementation-defined.
    /// </value>
    int Port { get; }

    /// <summary>
    /// Gets a value indicating whether the endpoint contains a port component.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if a port is defined; otherwise,
    /// <see langword="false"/>.
    /// </value>
    bool HasPort { get; }

    /// <summary>
    /// Gets a value indicating whether the address represents an IPv6 endpoint.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the address is IPv6; otherwise,
    /// <see langword="false"/> (typically IPv4).
    /// </value>
    bool IsIPv6 { get; }

    /// <summary>
    /// Formats the IP address directly into a character span without heap allocation.
    /// </summary>
    /// <param name="destination">Buffer to write into.</param>
    /// <param name="charsWritten">Number of characters written on success.</param>
    /// <returns>True if the address was formatted; false if the destination was too small.</returns>
    bool TryFormatAddress(System.Span<char> destination, out int charsWritten)
    {
        string address = this.Address;

        if (address is null)
        {
            charsWritten = 0;
            return false;
        }

        if (System.MemoryExtensions.AsSpan(address).TryCopyTo(destination))
        {
            charsWritten = address.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }
}
