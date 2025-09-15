// Copyright (c) 2025 PPN Corporation. All rights reserved.

namespace Nalix.Common.Packets.Attributes;

/// <summary>
/// <c>PacketRateLimitAttribute</c> is an attribute used to limit the rate of requests for a method.
/// Apply this attribute to methods to specify how many requests per second are allowed, and the burst size.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketRateLimitAttribute"/> class.
/// </remarks>
/// <param name="requestsPerSecond">Maximum requests per second allowed.</param>
/// <param name="burst">Burst size (default is 1).</param>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PacketRateLimitAttribute(System.Int32 requestsPerSecond, System.Int32 burst = 1) : System.Attribute
{
    /// <summary>
    /// The burst size allowed for requests. Default is 1.
    /// </summary>
    public System.Int32 Burst { get; } = burst;

    /// <summary>
    /// The maximum number of requests allowed per second.
    /// </summary>
    public System.Int32 RequestsPerSecond { get; } = requestsPerSecond;
}