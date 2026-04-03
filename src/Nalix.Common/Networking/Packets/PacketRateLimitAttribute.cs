// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Marks a handler with a request rate limit.
/// </summary>
/// <remarks>
/// The dispatcher can use this information to throttle bursts and protect the handler
/// from sustained overload.
/// </remarks>
/// <param name="requestsPerSecond">Maximum requests per second allowed.</param>
/// <param name="burst">Burst size, where 1 means no burst beyond the steady rate.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketRateLimitAttribute(int requestsPerSecond, double burst = 1) : Attribute
{
    /// <summary>
    /// Gets the burst size allowed for requests.
    /// </summary>
    public double Burst { get; } = burst;

    /// <summary>
    /// Gets the maximum number of requests allowed per second.
    /// </summary>
    public int RequestsPerSecond { get; } = requestsPerSecond;
}
