// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Abstractions.Networking.Packets;

/// <summary>
/// Marks a handler with a request rate limit.
/// </summary>
/// <remarks>
/// The dispatcher can use this information to throttle bursts and protect the handler
/// from sustained overload.
/// </remarks>
/// <param name="requestsPerSecond">Maximum requests per second allowed.</param>
/// <param name="burst">Burst size, where 1.0 means no burst beyond the steady rate.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketRateLimitAttribute(int requestsPerSecond, double burst = 1.0) : Attribute
{
    /// <summary>
    /// Gets the maximum number of requests allowed per second.
    /// </summary>
    public int RequestsPerSecond { get; } = requestsPerSecond;

    /// <summary>
    /// Gets the burst size allowed for requests.
    /// </summary>
    public double Burst { get; } = burst;

    /// <summary>
    /// Gets or sets a custom policy identifier. 
    /// Handlers sharing the exact same <see cref="PolicyId"/> will share the same rate limit bucket per IP. 
    /// If left null, the limit is isolated automatically by OpCode.
    /// </summary>
    public string? PolicyId { get; init; }

    /// <summary>
    /// Gets or sets a custom hard lockout duration in seconds for this specific handler.
    /// Set to -1 (default) to inherit the global fallback configuration.
    /// </summary>
    public int HardLockoutSeconds { get; init; } = -1;

    /// <summary>
    /// Gets or sets a custom soft violation threshold before triggering a hard lockout.
    /// Set to -1 (default) to inherit the global fallback configuration.
    /// </summary>
    public int MaxSoftViolations { get; init; } = -1;
}
