// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Specifies the preferred network transport protocol for an individual packet handler method.
/// </summary>
/// <remarks>
/// When this attribute is present, the dispatcher will prioritize this transport for outbound
/// responses triggered by the handler. If absent, the system defaults to <see cref="NetworkTransport.TCP"/>.
/// </remarks>
/// <param name="transportType">The network transport protocol to use.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketTransportAttribute(NetworkTransport transportType) : Attribute
{
    /// <summary>
    /// Gets the network transport protocol specified for the target handler.
    /// </summary>
    public NetworkTransport TransportType { get; } = transportType;
}
