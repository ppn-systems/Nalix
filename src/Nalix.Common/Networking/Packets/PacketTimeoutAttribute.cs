// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Specifies the maximum allowed processing time, in milliseconds, for a packet-handling method.
/// </summary>
/// <remarks>
/// Apply this attribute to a packet handler method to define a time limit for processing.
/// If the operation exceeds the specified timeout, it can be treated as a failure or trigger
/// a timeout handling routine.
/// </remarks>
/// <param name="timeoutMilliseconds">
/// The timeout duration in milliseconds before the packet operation is considered to have timed out.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketTimeoutAttribute(int timeoutMilliseconds) : Attribute
{
    /// <summary>
    /// Gets the timeout duration, in milliseconds, specified for the method.
    /// </summary>
    public int TimeoutMilliseconds { get; } = timeoutMilliseconds;
}
