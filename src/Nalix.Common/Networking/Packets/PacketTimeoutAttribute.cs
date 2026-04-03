// Copyright (c) 2025-2026 PPN Corporation. All rights reserved.
// Licensed under the Apache License, Version 2.0.

using System;

namespace Nalix.Common.Networking.Packets;

/// <summary>
/// Marks a handler with the maximum processing time allowed for its packet work.
/// </summary>
/// <remarks>
/// If execution runs longer than this limit, the dispatcher may treat it as a timeout failure.
/// </remarks>
/// <param name="timeoutMilliseconds">
/// The timeout duration in milliseconds before the packet operation is considered to have timed out.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketTimeoutAttribute(int timeoutMilliseconds) : Attribute
{
    /// <summary>
    /// Gets the timeout duration, in milliseconds.
    /// </summary>
    public int TimeoutMilliseconds { get; } = timeoutMilliseconds;
}
