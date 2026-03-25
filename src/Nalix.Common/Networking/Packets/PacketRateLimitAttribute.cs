// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// <c>PacketRateLimitAttribute</c> is an attribute used to limit the rate of requests for a method.
/// Apply this attribute to methods to specify how many requests per second are allowed, and the burst size.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketRateLimitAttribute"/> class.
/// </remarks>
/// <param name="requestsPerSecond">Maximum requests per second allowed.</param>
/// <param name="burst">Burst size (default is 1).</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketRateLimitAttribute(int requestsPerSecond, double burst = 1) : Attribute
{
    /// <summary>
    /// The burst size allowed for requests. Default is 1.
    /// </summary>
    public double Burst { get; } = burst;

    /// <summary>
    /// The maximum number of requests allowed per second.
    /// </summary>
    public int RequestsPerSecond { get; } = requestsPerSecond;
}
